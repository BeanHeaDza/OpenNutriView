using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.NewTooltip;
using Eco.Gameplay.Systems.NewTooltip.TooltipLibraryFiles;
using Eco.Shared.Items;
using Eco.Shared.Localization;

namespace OpenNutriView
{
    [TooltipLibrary]
    public static class FoodItemExtension
    {
        [NewTooltip(CacheAs.Disabled, overrideType: typeof(Stomach))]
        public static LocString StomachTooltip(this Stomach stomach)
        {
            return OpenNutriView.NextFood.Stomach(stomach);
        }

        [NewTooltip(CacheAs.Disabled, overrideType: typeof(FoodItem))]
        public static LocString FoodTasteTooltip(this FoodItem type, User user)
        {
            return OpenNutriView.NextFood.FoodItem(user.Player, type);
        }
    }
}
