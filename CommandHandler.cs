using Eco.Core.Plugins;
using Eco.Core.Serialization;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Shared.Localization;
using Eco.Shared.Services;
using Eco.Shared.Utils;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenNutriView
{
    [ChatCommandHandler]
    public static class CommandHandler
    {
        [ChatCommand("Shows commands available from the OpenNutriView mod", "onv")]
        public static void OpenNutriView()
        {
        }

        [ChatSubCommand(nameof(OpenNutriView), "Print out your current configuration")]
        public static void Config(User user)
        {
            ONVUserConfig config = OpenNutriViewData.Obj.GetConfig(user);
            var sb = new StringBuilder();
            sb.AppendLine("OpenNutriView Configurations");
            LocExtensions.AppendLineLoc(sb, FormattableStringFactory.Create("Minimum Nutrients: {0}", Text.Info(Text.Num(config.MinimumNutrients))));
            LocExtensions.AppendLineLoc(sb, FormattableStringFactory.Create("Maximum Cost per 1000 Calories: {0}", Text.Info(Text.Num(config.MaxCostPer1000Calories))));
            LocExtensions.AppendLineLoc(sb, FormattableStringFactory.Create("Maximum Shop Distance: {0}", Text.Info(Text.Num(config.MaxShopDistance))));
            user.TempServerMessage(LocExtensions.ToStringLoc(sb), NotificationCategory.Notifications, NotificationStyle.Chat);
        }

        [ChatSubCommand(nameof(OpenNutriView), "Set minimum Nutrients")]
        public static void SetMinNutrients(User user, float min = 0)
        {
            ONVUserConfig config = OpenNutriViewData.Obj.GetConfig(user);
            config.MinimumNutrients = min;
            StorageManager.Obj.MarkDirty(OpenNutriViewData.Obj);
            user.TempServerMessage(Localizer.Do(FormattableStringFactory.Create("Minimum Nutrients: {0}", Text.Info(Text.Num(config.MinimumNutrients)))), NotificationCategory.Notifications, NotificationStyle.Chat);
        }

        [ChatSubCommand(nameof(OpenNutriView), "Set maximum cost per 1000 calories")]
        public static void SetMaxCost(User user, float max = float.PositiveInfinity)
        {
            ONVUserConfig config = OpenNutriViewData.Obj.GetConfig(user);
            config.MaxCostPer1000Calories = max;
            StorageManager.Obj.MarkDirty(OpenNutriViewData.Obj);
            user.TempServerMessage(Localizer.Do(FormattableStringFactory.Create("Maximum Cost per 1000 Calories: {0}", Text.Info(Text.Num(config.MaxCostPer1000Calories)))), NotificationCategory.Notifications, NotificationStyle.Chat);
        }

        [ChatSubCommand(nameof(OpenNutriView), "Set maximum shop distance")]
        public static void SetMaxDistance(User user, float max = float.PositiveInfinity)
        {
            ONVUserConfig config = OpenNutriViewData.Obj.GetConfig(user);
            config.MaxShopDistance = max;
            StorageManager.Obj.MarkDirty(OpenNutriViewData.Obj);
            user.TempServerMessage(Localizer.Do(FormattableStringFactory.Create("Maximum Shop Distance: {0}", Text.Info(Text.Num(config.MaxShopDistance)))), NotificationCategory.Notifications, NotificationStyle.Chat);
        }
    }
}
