using Eco.Core.Plugins;
using Eco.Core.Serialization;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems;
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

        [ChatSubCommand(nameof(OpenNutriView), "Opens up a window to change your configurations")]
        public static void Config(User user)
        {
            ONVUserConfig config = OpenNutriViewData.Obj.GetConfig(user);

            ViewEditorUtils.PopupUserEditValue(user, typeof(ONVUserConfigUI), Localizer.DoStr("Trade Assistant Configuration"), config.ToUI(), null, OnSubmit);
            void OnSubmit(object entry)
            {
                if (entry is ONVUserConfigUI uiConfig)
                    config.UpdateFromUI(uiConfig);
                StorageManager.Obj.MarkDirty(OpenNutriViewData.Obj);
            }
        }
    }
}
