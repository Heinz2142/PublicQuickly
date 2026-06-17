using AdditionalArmorFeaturesLibrary.Config;
using Vintagestory.API.Common;

namespace AdditionalArmorFeaturesLibrary.Config
{
    public class LoadOrCreate
    {
        public Server SapiConfig(ICoreAPI api, string ConfigServerName)
        {
            var serverConfig = api.LoadModConfig<Server>(ConfigServerName);

            if (serverConfig == null)
            {
                serverConfig = new Server();
                api.StoreModConfig(serverConfig, ConfigServerName);
                api.Logger?.Notification($"Created default config '{ConfigServerName}'.");
            }
            else
            {
                api.Logger?.VerboseDebug($"Loaded config '{ConfigServerName}'.");
            }


            return serverConfig;
        }

        public Client CapiConfig(ICoreAPI api, string ConfigClientName)
        {
            var clientConfig = api.LoadModConfig<Client>(ConfigClientName);

            if (clientConfig == null)
            {
                clientConfig = new Client();
                api.StoreModConfig(clientConfig, ConfigClientName);
                api.Logger?.Notification($"Created default config '{ConfigClientName}'.");
            }
            else
            {
                api.Logger?.VerboseDebug($"Loaded config '{ConfigClientName}'.");
            }

            return clientConfig;
        }
    }
}