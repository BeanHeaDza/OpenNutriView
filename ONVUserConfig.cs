using Eco.Core.Controller;
using Eco.Core.Utils;
using Eco.Gameplay.Economy;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.PropertyHandling;
using Eco.Gameplay.Utils;
using Eco.Shared.Networking;
using Eco.Shared.Serialization;
using Eco.Shared.Utils;
using PropertyChanged;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenNutriView
{
    [Serialized]
    public class ONVUserConfig
    {
        [Serialized] public float MinimumNutrients { get; set; } = 0;
        [Serialized] public float MaxCostPer1000Calories { get; set; } = float.PositiveInfinity;
        [Serialized] public float MaxShopDistance { get; set; } = float.PositiveInfinity;
        [Serialized] public ThreadSafeList<int> IgnoredCurrencies = new();

        public ONVUserConfigUI ToUI()
        {
            ONVUserConfigUI ui = new()
            {
                MaxCostPerThousandCalories = MaxCostPer1000Calories,
                MaxShopDistance = MaxShopDistance,
                MinimumNutrients = MinimumNutrients,
            };
            var idToCurrency = new Dictionary<int, Currency>();
            CurrencyManager.Currencies.ForEach(c => idToCurrency.Add(c.Id, c));
            IgnoredCurrencies.Select(id => idToCurrency.GetOrDefault(id)).Where(p => p != null).ForEach(ui.IgnoredCurrencies.Add);
            return ui;
        }

        public void UpdateFromUI(ONVUserConfigUI config)
        {
            MaxCostPer1000Calories = config.MaxCostPerThousandCalories;
            MinimumNutrients = config.MinimumNutrients;
            MaxShopDistance = config.MaxShopDistance;
            IgnoredCurrencies.Clear();
            IgnoredCurrencies.AddRange(config.IgnoredCurrencies.Select(c => c.Id));
        }
    }

    public class ONVUserConfigUI : IController, INotifyPropertyChanged, Eco.Core.PropertyHandling.INotifyPropertyChangedInvoker, IHasClientControlledContainers
    {
        [Eco] public float MinimumNutrients { get; set; } = 0;

        [Eco] public float MaxCostPerThousandCalories { get; set; } = float.PositiveInfinity;

        [Eco] public float MaxShopDistance { get; set; } = float.PositiveInfinity;
        [Eco, AllowEmpty] public ControllerList<Currency> IgnoredCurrencies { get; set; }

        public ONVUserConfigUI()
        {
            IgnoredCurrencies = new ControllerList<Currency>(this, nameof(IgnoredCurrencies));
        }


        #region IController
        public event PropertyChangedEventHandler? PropertyChanged;
        int controllerID;
        [DoNotNotify] public ref int ControllerID => ref controllerID;

        public void InvokePropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            if (PropertyChanged == null)
                return;
            PropertyChanged(this, eventArgs);
        }

        protected void OnPropertyChanged(string propertyName, object before, object after) => PropertyChangedNotificationInterceptor.Intercept(this, propertyName, before, after);
        #endregion
    }
}
