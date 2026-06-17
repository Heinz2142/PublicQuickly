using AdditionalArmorFeaturesLibrary.Utils;
using AdditionalArmorFeaturesLibrary.Config;
using ProperVersion;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace AdditionalArmorFeaturesLibrary.Util
{
    public static class AdditionalArmorFeaturesLibraryConfigHelper
    {
        private static Server config;
        public static int oldVersion = 1;
        public const int newVersion = 2;

        public static void Init(Server cfg)
        {
            _ = config;

            config = cfg;
        }

        public static void MigrateConfig(ICoreAPI api)
        {
            if (config == null) return;

            config.Version = newVersion;

            api.StoreModConfig(config, "awearablelight-server.json");

            api.Logger.Notification($"[WearableLight] Config migrated to version {config.Version}.");

        }
    }

    public class LoggerExt
    {
        public static void SendLogger(ICoreClientAPI capi, string[] logs)
        {
            if (capi == null) return;

            var match = capi.Settings.Bool.Get("developerMode");

            if (match)
            {
                foreach (var log in logs)
                {
                    if (string.IsNullOrEmpty(log)) continue;

                    capi.Logger.Debug(log);

                }
            }
        }
    }

}