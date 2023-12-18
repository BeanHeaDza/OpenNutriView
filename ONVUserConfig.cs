using Eco.Shared.Serialization;

namespace OpenNutriView
{
    [Serialized]
    public class ONVUserConfig
    {
        [Serialized]
        public float MinimumNutrients { get; set; } = 0;

        [Serialized]
        public float MaxCostPer1000Calories { get; set; } = float.PositiveInfinity;

        [Serialized]
        public float MaxShopDistance { get; set; } = float.PositiveInfinity;
    }
}
