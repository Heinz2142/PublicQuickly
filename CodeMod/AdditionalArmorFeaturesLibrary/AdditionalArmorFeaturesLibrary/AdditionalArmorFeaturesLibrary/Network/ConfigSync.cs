using AdditionalArmorFeaturesLibrary.Config;
using AdditionalArmorFeaturesLibrary.Util;
using AdditionalArmorFeaturesLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AdditionalArmorFeaturesLibrary.Network
{
#nullable enable

    [ProtoContract]
    public class ServerConfigPacket
    {
        [ProtoMember(1)]
        public List<ItemTypePacket> ItemTypes = new();
    }

    [ProtoContract]
    public class ItemTypePacket
    {
        [ProtoMember(1)]
        public string Code = string.Empty;

        [ProtoMember(2)]
        public bool UseFuel;

        [ProtoMember(5)]
        public string PowerType = "fuel";

        [ProtoMember(6)]
        public float Capacity;

        [ProtoMember(7)]
        public Dictionary<string, float> FuelList = new();
    }

    public class ConfigSyncSystem
    {
        private IServerNetworkChannel? serverChannel;
        private IClientNetworkChannel? clientChannel;

        private string ConfigServerName = "additionalarmorfeatureslibrary-server.json";

        private DateTime lastConfigWriteTime;
        private readonly string configPath;

        private Server? Config;
        private ICoreAPI api;

        public ConfigSyncSystem(ICoreAPI api, Server? config)
        {
            this.api = api;
            this.Config = config;

            configPath = Path.Combine(api.GetOrCreateDataPath("ModConfig"), ConfigServerName);

            if (File.Exists(configPath))
            {
                lastConfigWriteTime = File.GetLastWriteTimeUtc(configPath);
            }

            if (api.Side == EnumAppSide.Server)
            {
                serverChannel = (api as ICoreServerAPI)?.Network
                    .RegisterChannel("additionalarmorfeatureslibrary_config")
                    .RegisterMessageType<ServerConfigPacket>();
            }

            if (api.Side == EnumAppSide.Client)
            {
                clientChannel = (api as ICoreClientAPI)?.Network
                    .RegisterChannel("additionalarmorfeatureslibrary_config")
                    .RegisterMessageType<ServerConfigPacket>()
                    .SetMessageHandler<ServerConfigPacket>(OnConfigPacketReceived);
            }

            api.ModLoader.GetModSystem<AdditionalArmorFeaturesLibrarySystem>().OnLongRefreshTick = api.Event.RegisterGameTickListener(CheckConfigChanges, 100000); // every 1 mins
        }

        protected void CheckConfigChanges(float dt)
        {
            if (!File.Exists(configPath)) return;

            var currentWriteTime = File.GetLastWriteTimeUtc(configPath);

            if (currentWriteTime <= lastConfigWriteTime) return;

            lastConfigWriteTime = currentWriteTime;

            ReloadConfig();
        }

        private void ReloadConfig()
        {
            Config = new LoadOrCreate().SapiConfig(api, ConfigServerName);

            if (Config == null)
            {
                api.Logger.Error($"issue with {ConfigServerName}");
                return;
            }

            AdditionalArmorFeaturesLibraryConfigHelper.Init(Config);

            if (api is ICoreServerAPI sapi)
            {
                foreach (var player in sapi.World.AllOnlinePlayers)
                {
                    if (player is IServerPlayer sp)
                    {
                        SendToPlayer(sp);
                    }
                }
            }
            api.Logger.Debug("[additionalarmorfeatureslibrary_config] Config reloaded");
        }

        public void SendToPlayer(IServerPlayer player)
        {
            if (serverChannel == null || Config == null) return;

            var packet = new ServerConfigPacket();

            serverChannel.SendPacket(packet, player);
        }

        private void OnConfigPacketReceived(ServerConfigPacket packet)
        {
            var capi = api as ICoreClientAPI;

            capi?.Logger.Notification("Config synced from server");

            var config = new Server{};

            // Init helper
            AdditionalArmorFeaturesLibraryConfigHelper.Init(config);

            // Refresh UI (IMPORTANT)
            capi?.World.Player?.InventoryManager.BroadcastHotbarSlot();
        }
    }
}