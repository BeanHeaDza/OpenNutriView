using Eco.Core.Plugins.Interfaces;
using Eco.Gameplay.Components;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players.Food;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.NewTooltip.TooltipLibraryFiles;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using Eco.Simulation;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using System.Linq;

namespace OpenNutriView
{
    public class OpenNutriView
    {
        public class TooltipPatcher : IModInit
        {
            public static void Initialize()
            {
                var harmony = new Harmony("eco.play");
                harmony.PatchAll();
            }
        }

        static class NextFood
        {

            static Dictionary<FoodItem, HashSet<WorldObject>> GetAccessableFood(User user)
            {
                var foods = new Dictionary<FoodItem, HashSet<WorldObject>>();

                void addFood(ItemStack stack, WorldObject obj)
                {
                    if (stack.Item is not FoodItem food || food.Calories <= 0)
                        return;
                    if (!foods.ContainsKey(food))
                        foods.Add(food, new HashSet<WorldObject>());
                    foods[food].Add(obj);
                }

                // Stores
                foreach (var store in WorldObjectUtil.AllObjsWithComponent<StoreComponent>().Where(store => store.Enabled))
                    foreach (var tradeOffer in store.AllOffers.Where(o => o.Buying == false && o.Stack.Item is FoodItem && o.Stack.Quantity > 0))
                        addFood(tradeOffer.Stack, store.Parent);

                // Containers
                foreach (var storageComponent in WorldObjectUtil.AllObjsWithComponent<StorageComponent>().Where(i => i.Parent.Auth.Owners != null && i.Parent.Auth.Owners.ContainsUser(user)))
                    foreach (var itemStack in storageComponent.Inventory.Stacks.Where(item => item.Item is FoodItem))
                        addFood(itemStack, storageComponent.Parent);

                // Player inventory
                foreach (var stack in user.Inventory.Stacks.Where(s => s.Item is FoodItem))
                    addFood(stack, null);

                return foods;
            }

            private static (Nutrients stomachNutrients, float stomachCalories, Dictionary<Type, float> foodCalories) LoadCurrentStomach(Stomach stomach)
            {
                var stomachNutrients = new Nutrients();
                var stomachCalories = 0f;
                var foodCalories = new Dictionary<Type, float>();
                foreach (var content in stomach.Contents)
                {
                    if (content.Food.Calories <= 0)
                        continue;
                    stomachCalories += content.Food.Calories;
                    stomachNutrients += content.Food.Nutrition * content.Food.Calories;
                    foodCalories.AddOrUpdate(content.Food.Type, content.Food.Calories, (old, val) => old + val);
                }
                return (stomachNutrients, stomachCalories, foodCalories);
            }

            public static LocString Stomach(Stomach stomach)
            {
                var (stomachNutrients, stomachCalories, foodCalories) = LoadCurrentStomach(stomach);

                var foods = GetAccessableFood(stomach.Owner);
                var foodGains = new Dictionary<FoodItem, (float gain, bool assumedTaste)>();
                foreach (var food in foods.Keys)
                    foodGains.Add(food, CalculateGain(food, stomach, stomachNutrients, stomachCalories, foodCalories));

                var sb = new LocStringBuilder();
                sb.AppendLine(new LocString(Text.ColorUnity(Color.Yellow.UInt, TextLoc.SizeLoc(0.8f, FormattableStringFactory.Create("These are the best 3 foods available to you:")).Italic())));
                var addAssumedFooter = false;
                foreach (var (food, worldObjs) in foods.OrderByDescending(f => foodGains[f.Key].gain).Take(3))
                {
                    var (gain, assumedTaste) = foodGains[food];
                    addAssumedFooter |= assumedTaste;
                    var locString = Localizer.DoStr((gain >= 0 ? "+" : "") + Math.Round(gain, 2).ToString()).Style(gain >= 0.0 ? Text.Styles.Positive : Text.Styles.Negative);
                    var str2 = string.Join(", ", worldObjs.Select(o => o == null ? new LocString("Your inventory") : o.UILink()));
                    sb.AppendLine(Localizer.Do(FormattableStringFactory.Create("{0}{1} will provide you {2} XP and can be found here: {3}", food.UILink(), assumedTaste ? "*" : "", locString, str2)));
                }
                if (addAssumedFooter)
                    sb.AppendLine(new LocString("* Assumed delicious tasting"));

                return sb.ToLocString();
            }

            public static LocString FoodItem(Player player, FoodItem food)
            {
                var (stomachNutrients, stomachCalories, foodCalories) = LoadCurrentStomach(player.User.Stomach);

                var (gain, assumedTaste) = CalculateGain(food, player.User.Stomach, stomachNutrients, stomachCalories, foodCalories);
                var sb = new LocStringBuilder();
                var gainText = gain > 0.0 ? Text.Color(Color.Green, string.Format("+{0}", Math.Round(gain, 2))) : Text.Color(Color.Red, string.Format("{0}", Math.Round(gain, 2)));
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine(Localizer.Do(FormattableStringFactory.Create("This food will provide you {0}{1} XP", gainText, assumedTaste ? "*" : "")));
                if (assumedTaste) sb.AppendLine(new LocString("* Assumed delicious tasting"));
                sb.AppendLineLocStr(Text.Size(0.8f, Text.Italics(Text.ColorUnity(Color.Yellow.UInt, Localizer.Do(FormattableStringFactory.Create("(Report generated for user {0})", (object)player.DisplayName))))));

                return sb.ToLocString();
            }
            private static (float gain, bool assumedTaste) CalculateGain(FoodItem food, Stomach stomach, Nutrients stomachNutrients, float stomachCalories, Dictionary<Type, float> foodCalories)
            {
                foodCalories = new Dictionary<Type, float>(foodCalories);

                if (food.Calories <= 0)
                    return (0, false);
                stomachCalories += food.Calories;
                stomachNutrients += food.Nutrition * food.Calories;
                foodCalories.AddOrUpdate(food.Type, food.Calories, (sum, val) => sum + val);

                if (stomachCalories > 0)
                    stomachNutrients *= 1f / stomachCalories;

                var nutrientTotal = stomachNutrients.NutrientTotal();
                var varietyMultiplier = VarietyMultiplier(foodCalories);
                var (tastinessMultiplier, assumedTaste) = TastinessMultiplier(foodCalories, stomachCalories, stomach.TasteBuds);
                var (balancedDietMultiplier, _) = stomachNutrients.CalcBalancedDietMult();
                var cravingMultiplier = CravingMultiplier(food, stomach);

                var subTotal = nutrientTotal * varietyMultiplier * tastinessMultiplier * balancedDietMultiplier * cravingMultiplier;
                if (subTotal < 0) subTotal = 0;
                var newSkillRate = (subTotal + EcoSim.BaseSkillGainRate) * DifficultySettings.SkillGainMultiplier;
                return (newSkillRate - stomach.NutrientSkillRate(), assumedTaste);
            }

            private static float CravingMultiplier(FoodItem food, Stomach stomach)
            {
                var (multiplier, _) = stomach.Cravings.GetMult();
                if (stomach.Craving == null || stomach.Craving != food.Type)
                    return multiplier;

                var count = Convert.ToInt32((multiplier - 1) / Cravings.CravingsBoost) + 1;
                return 1 + count * Cravings.CravingsBoost;
            }

            private static float VarietyMultiplier(IEnumerable<KeyValuePair<Type, float>> foodToCalories)
            {
                foodToCalories = foodToCalories.Where(x => x.Value > FoodVariety.Settings.MinCaloriesToBeIncludedInVariertyBonus);
                var typesEatenOverLimit = foodToCalories.Count();
                return MathUtil.SoftCapMap(typesEatenOverLimit, 1f, FoodVariety.Settings.SoftCapMult, FoodVariety.Settings.HardCapMult, 0.0f, FoodVariety.Settings.FoodTypesToReachSoftCapMult, FoodVariety.Settings.FoodTypesToReachHardCapMult, FoodVariety.Settings.FoodTypesDiminishingReturns);
            }

            private static (float multiplier, bool assumedTaste) TastinessMultiplier(Dictionary<Type, float> foodToCalories, float totalCalories, TasteBuds tasteBuds)
            {
                if (totalCalories == 0)
                    return (1, false);

                var num1 = 0.0f;
                var assumedTaste = false;
                foreach (var (food, calories) in foodToCalories.OrderByDescending(x => x.Value))
                {
                    var taste = !tasteBuds.FoodToTaste.ContainsKey(food) ? new ItemTaste?() : new ItemTaste?(tasteBuds.FoodToTaste[food]);
                    if (taste.HasValue && taste.Value.Discovered)
                        num1 += taste.Value.TastinessMult * calories;
                    else
                    {
                        num1 += ItemTaste.TastinessMultiplier[(int)ItemTaste.TastePreference.Delicious] * calories;
                        assumedTaste = true;
                    }
                }
                return (num1 / totalCalories, assumedTaste);
            }
        }

        [HarmonyPatch(typeof(StomachTooltipLibrary), nameof(StomachTooltipLibrary.FoodStatus))]
        class Patch
        {
            static void Prefix(Stomach stomach, out LocString __state)
            {
                __state = NextFood.Stomach(stomach);
            }

            static void Postfix(ref LocString __result, LocString __state)
            {
                LocStringBuilder sb = new();
                var split = TextLoc.InfoBoldLocStr("Stomach Contents") + ":";
                var parts = __result.ToString().Split(split);
                sb.Append(parts[0]);
                sb.AppendLine(__state);
                if (parts.Length > 1)
                {
                    sb.AppendLine();
                    sb.Append(split);
                    sb.Append(parts[1]);
                }
                __result = sb.ToLocString();
            }

        }

        [HarmonyPatch(typeof(FoodItem), nameof(FoodItem.FoodTooltip))]
        class PatchItemTooltip
        {
            static void Prefix(Player player, FoodItem __instance, out LocString __state)
            {
                __state = NextFood.FoodItem(player, __instance);
            }

            static void Postfix(ref string __result, LocString __state)
            {
                __result += __state;
            }
        }
    }
}
