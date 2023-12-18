using Eco.Core.Plugins.Interfaces;
using Eco.Core.Plugins;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
namespace OpenNutriView
{
    public class OpenNutriViewPlugin :
      Singleton<OpenNutriViewPlugin>,
      IModKitPlugin,
      IServerPlugin,
      ISaveablePlugin
    {
        private readonly OpenNutriViewData data;

        public OpenNutriViewPlugin()
        {
            try
            {
                data = StorageManager.LoadOrCreate<OpenNutriViewData>("OpenNutriView", null);
            }
            catch
            {
                foreach (string file in StorageManager.GetFiles("OpenNutriView"))
                    StorageManager.Delete(file);
                data = StorageManager.LoadOrCreate<OpenNutriViewData>("OpenNutriView", null);
            }
        }

        public string GetCategory() => Localizer.DoStr("Mods");

        public string GetStatus() => string.Empty;

        public void SaveAll() => Singleton<StorageManager>.Obj.MarkDirty(data);
    }
}
