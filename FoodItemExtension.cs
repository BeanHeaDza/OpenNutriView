using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.NewTooltip;
using Eco.Gameplay.Systems.NewTooltip.TooltipLibraryFiles;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using System;

namespace OpenNutriView
{
    [TooltipLibrary]
    public static class FoodItemExtension
    {
        [NewTooltip(CacheAs.Disabled, overrideType: typeof(Stomach))]
        public static LocString StomachTooltip(this Stomach stomach)
        {
            try
            {
                return OpenNutriView.NextFood.Stomach(stomach);
            }
            catch (Exception e)
            {
                Log.WriteError(Localizer.Do($"[OpenNutriView] Failed to generate stomach tooltip for {stomach.Owner.Name}. See following exception:"));
                Log.WriteException(e);
                return LocString.Empty;
            }
        }

        [NewTooltip(CacheAs.Disabled, overrideType: typeof(FoodItem))]
        public static LocString FoodTasteTooltip(this FoodItem type, User user)
        {
            return OpenNutriView.NextFood.FoodItem(user.Player, type);
        }
    }
}
