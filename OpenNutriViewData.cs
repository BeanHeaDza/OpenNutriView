using Eco.Core.Serialization;
using Eco.Core.Utils;
using Eco.Gameplay.Players;
using Eco.Shared.Serialization;
using Eco.Shared.Utils;

namespace OpenNutriView
{
    [Serialized]
    public class OpenNutriViewData : Singleton<OpenNutriViewData>, IStorage, ISerializable
    {
        [Serialized]
        public ThreadSafeDictionary<int, ONVUserConfig> UserConfiguration = new();

        public IPersistent StorageHandle { get; set; }

        public ONVUserConfig GetConfig(User user)
        {
            if (!UserConfiguration.TryGetValue(user.Id, out ONVUserConfig config))
            {
                config = new ONVUserConfig();
                UserConfiguration.Add(user.Id, config);
            }
            return config;
        }
    }
}
