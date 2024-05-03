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
using System.Linq;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Components.Storage;
using Eco.Gameplay.Systems;
using Eco.Shared.Items;
using Eco.Shared.Networking;
using Eco.Stats;
using Eco.Shared.Voxel;

namespace OpenNutriView
{
    public class OpenNutriView
    {
        public static class NextFood
        {
            static Dictionary<Type, HashSet<WorldObject>> GetAccessableFood(User user, HashSet<int> ignoredCurrencyIds, float shopMaxCostPer1000Calories = float.PositiveInfinity, float shopMaxDistance = float.PositiveInfinity)
            {
                var foods = new Dictionary<Type, HashSet<WorldObject>>();

                void addFood(ItemStack stack, WorldObject obj)
                {
                    if (stack.Item is not FoodItem food || food.Calories <= 0)
                        return;
                    foods.GetOrAdd(food.Type).Add(obj);
                }

                // Stores
                foreach (StoreComponent store in WorldObjectUtil
                    .AllObjsWithComponent<StoreComponent>()
                    .Where(store => store != null
                        && store.Currency != null
                        && store.Parent != null
                        && store.Enabled
                        && store.IsRPCAuthorized(user.Player, AccessType.ConsumerAccess, Array.Empty<object>())
                        && !ignoredCurrencyIds.Contains(store.Currency.Id)
                        && World.WrappedDistance(user.Player.WorldPosXZ(), store.Parent.WorldPosXZ()) <= shopMaxDistance))
                {
                    foreach (var tradeOffer in store.StoreData.SellOffers
                        .Where(o => o != null
                            && o.Stack != null
                            && o.Stack.Item is FoodItem foodItem
                            && foodItem.Calories > 0
                            && o.Stack.Quantity > 0
                            && o.Price / foodItem.Calories * 1000 < shopMaxCostPer1000Calories))
                        addFood(tradeOffer.Stack, store.Parent);
                }

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
                foreach (var content in stomach.Contents.Where(content => content.Food.Calories > 0))
                {
                    stomachCalories += content.Food.Calories;
                    stomachNutrients += content.Food.Nutrition * content.Food.Calories;
                    foodCalories.AddOrUpdate(content.Food.Type, content.Food.Calories, (old, val) => old + val);
                }
                return (stomachNutrients, stomachCalories, foodCalories);
            }

            public static LocString Stomach(Stomach stomach)
            {
                var config = OpenNutriViewData.Obj.GetConfig(stomach.Owner);
                var (stomachNutrients, stomachCalories, foodCalories) = LoadCurrentStomach(stomach);

                var foods = GetAccessableFood(stomach.Owner, config.IgnoredCurrencies.ToHashSet(), config.MaxCostPer1000Calories, config.MaxShopDistance);
                var foodGains = new Dictionary<Type, (float gain, bool assumedTaste, float nutrition)>();
                foreach (var food in foods.Keys)
                    foodGains.Add(food, CalculateGain(TypeToFood(food), stomach, stomachNutrients, stomachCalories, foodCalories));

                var sb = new LocStringBuilder();
                sb.AppendLine(new LocString(Text.ColorUnity(Color.Yellow.UInt, TextLoc.SizeLoc(0.8f, FormattableStringFactory.Create("These are the best 3 foods available to you (Good for a balanced diet):")).Italic())));
                var addAssumedFooter = false;
                foreach (var (key, worldObjs) in foods.OrderByDescending(f => foodGains[f.Key].gain).Where(f => foodGains[f.Key].nutrition >= config.MinimumNutrients).Take(3))
                {
                    FoodItem food = TypeToFood(key);
                    var (gain, assumedTaste, _) = foodGains[key];
                    addAssumedFooter |= assumedTaste;
                    var locString = Localizer.DoStr((gain >= 0 ? "+" : "") + Math.Round(gain, 2).ToString()).Style(gain >= 0.0 ? Text.Styles.Positive : Text.Styles.Negative);
                    var str2 = string.Join(", ", worldObjs.Select(o => o == null ? new LocString("Your inventory") : o.UILink()));
                    sb.AppendLine(Localizer.Do(FormattableStringFactory.Create("{0}{1} will provide you {2} XP and can be found here: {3}", food.UILink(), assumedTaste ? "*" : "", locString, str2)));
                }
                if (addAssumedFooter)
                    sb.AppendLine(new LocString("* Assumed delicious tasting"));

                sb.AppendLine();
                sb.AppendLine(new LocString(Text.ColorUnity(Color.Yellow.UInt, TextLoc.SizeLoc(0.8f, FormattableStringFactory.Create("These are the most nutrient dense food available to you (Good for when your diet balance is good and previous suggestions are bad):")).Italic())));
                addAssumedFooter = false;
                foreach (var (foodType, worldObjs) in foods.OrderByDescending(f => foodGains[f.Key].Item3).Take(3))
                {
                    FoodItem food = TypeToFood(foodType);
                    var (gain, assumedTaste, nutrition) = foodGains[foodType];
                    addAssumedFooter |= assumedTaste;
                    var locString = Localizer.DoStr((gain >= 0 ? "+" : "") + Math.Round(gain, 2).ToString()).Style(gain >= 0 ? Text.Styles.Positive : Text.Styles.Negative);
                    string str = string.Join(", ", worldObjs.Select(o => o != null ? UILinkExtensions.UILink(o) : new LocString("Your inventory")));
                    sb.AppendLine(Localizer.Do(FormattableStringFactory.Create("{0}{1} has {2} nutrition and will provide you {3} XP and can be found here: {4}", food.UILink(), assumedTaste ? "*" : (object)"", Text.Info(nutrition.ToString("0.0")), locString, str)));
                }
                if (addAssumedFooter)
                    sb.AppendLine(new LocString("* Assumed delicious tasting"));
                return sb.ToLocString();
            }

            public static LocString FoodItem(Player player, FoodItem food)
            {
                var (stomachNutrients, stomachCalories, foodCalories) = LoadCurrentStomach(player.User.Stomach);

                var (gain, assumedTaste, nutrition) = CalculateGain(food, player.User.Stomach, stomachNutrients, stomachCalories, foodCalories);
                var sb = new LocStringBuilder();
                sb.AppendLine();
                var assumedTasteAstrix = assumedTaste ? "*" : "";
                sb.AppendLineLoc($"Nutrition times tastiness {Text.Info(Text.Num(nutrition))}{assumedTasteAstrix}");
                sb.AppendLineLoc($"This food will provide you {gain.StyledNum()}{assumedTasteAstrix} XP");
                if (assumedTaste) sb.AppendLine(new LocString("* Assumed delicious tasting"));
                return sb.ToLocString();
            }

            private static (float gain, bool assumedTaste, float nutrition) CalculateGain(FoodItem food, Stomach stomach, Nutrients stomachNutrients, float stomachCalories, Dictionary<Type, float> foodCalories)
            {
                foodCalories = new Dictionary<Type, float>(foodCalories);

                if (food.Calories <= 0)
                    return (0, false, 0);
                stomachCalories += food.Calories;
                stomachNutrients += food.Nutrition * food.Calories;
                foodCalories.AddOrUpdate(food.Type, food.Calories, (sum, val) => sum + val);

                if (stomachCalories > 0)
                    stomachNutrients *= 1f / stomachCalories;

                var nutrientTotal = stomachNutrients.NutrientTotal();
                var (varietyMultiplier, _) = FoodVariety.UpdateMult(foodCalories);
                var (tastinessMultiplier, assumedTaste) = TastinessMultiplier(foodCalories, stomach.TasteBuds);
                var (balancedDietMultiplier, _) = stomachNutrients.CalcBalancedDietMult();
                var cravingMultiplier = CravingMultiplier(food, stomach);

                if (!FeatureConfig.Obj.FoodVarietyMultiplierEnabled)
                    varietyMultiplier = 1;
                if (!FeatureConfig.Obj.FoodTastinessMultiplierEnabled)
                    tastinessMultiplier = 1;

                var subTotal = nutrientTotal * varietyMultiplier * tastinessMultiplier * balancedDietMultiplier * cravingMultiplier;
                if (subTotal < 0) subTotal = 0;
                var newSkillRate = (subTotal + EcoSim.BaseSkillGainRate) * DifficultySettings.SkillGainMultiplier;
                var itemTasteMultiplier = !stomach.TasteBuds.FoodToTaste.TryGetValue(food.Type, out ItemTaste itemTaste) || !itemTaste.Discovered ? ItemTaste.TastinessMultiplier[(int)ItemTaste.TastePreference.Delicious] : itemTaste.TastinessMult;
                var gain = newSkillRate - stomach.NutrientSkillRate();
                var nutrition = food.Nutrition.NutrientTotal() * itemTasteMultiplier;
                return (gain, assumedTaste, nutrition);
            }

            private static float CravingMultiplier(FoodItem food, Stomach stomach)
            {
                var (multiplier, _) = stomach.Cravings.GetMult();
                if (stomach.Craving == null || stomach.Craving != food.Type)
                    return multiplier;

                var count = Convert.ToInt32((multiplier - 1) / Cravings.CravingsBoost) + 1;
                return 1 + count * Cravings.CravingsBoost;
            }

            private static (float multiplier, bool assumedTaste) TastinessMultiplier(Dictionary<Type, float> foodToCalories, TasteBuds tasteBuds)
            {
                var totalCalories = foodToCalories.Sum(x => x.Value);
                if (totalCalories == 0)
                    return (1, false);

                var num1 = 0.0f;
                var assumedTaste = false;
                foreach (var (food, calories) in foodToCalories.OrderByDescending(x => x.Value))
                {
                    var taste = tasteBuds.FoodToTaste.TryGetValue(food, out ItemTaste value) ? new ItemTaste?(value) : new ItemTaste?();
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
            private static FoodItem TypeToFood(Type type)
            {
                if (Item.Get(type) is not FoodItem food)
                    throw new ArgumentNullException($"Failed to get FoodItem from type {type}.");
                return food;
            }
        }
    }
}
