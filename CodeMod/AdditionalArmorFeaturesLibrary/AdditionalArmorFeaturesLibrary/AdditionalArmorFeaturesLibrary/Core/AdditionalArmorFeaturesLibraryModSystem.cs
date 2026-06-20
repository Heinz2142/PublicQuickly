using AdditionalArmorFeaturesLibrary.Collectible.Behavior;
using AdditionalArmorFeaturesLibrary.Config;
using AdditionalArmorFeaturesLibrary.Interfaces;
using AdditionalArmorFeaturesLibrary.Items;
using AdditionalArmorFeaturesLibrary.Network;
using AdditionalArmorFeaturesLibrary.Util;
using AdditionalArmorFeaturesLibrary.Utils;
using AdditionalArmorFeaturesLibrary.HarmonyPatches;
using HarmonyLib;
using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace AdditionalArmorFeaturesLibrary;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class TogglePacket
{
    public string HotKeyCode { get; set; } = "";
}

public partial class AdditionalArmorFeaturesLibrarySystem : ModSystem
{

    //To override bug in vanilla -Cry emoji-
    private Harmony harmonyInstance => new(Mod.Info.ModID);

    private static readonly string ConfigServerName = "additionalarmorfeatureslibrary-server.json";

    private static readonly string ConfigClientName = "additionalarmorfeatureslibrary-client.json";

    public static Server? ServerConfig { get; set; }

    public static Client? ClientConfig { get; set; }

    public long OnLongRefreshTick { get; set; }
    public long OnLongServerFuelTick { get; set; }
    public long OnLongServerTick { get; set; }

    private ICoreServerAPI? Sapi { get; set; }
    private ICoreClientAPI? Capi { get; set; }

    private IClientNetworkChannel? ClientToggleChannel { get; set; }

    private IServerNetworkChannel? ServerToggleChannel { get; set; }

    public ConfigSyncSystem? ConfigSync { get; set; }


    double lastCheckTotalHours;

  
    public override void StartPre(ICoreAPI api)
    {
        //All to make some lights work...........
        harmonyInstance.Patch(
        AccessTools.Method(typeof(EntityBehaviorContainer), nameof(EntityBehaviorContainer.OnTesselation)),
        postfix: AccessTools.Method(typeof(LightRenderPatch), nameof(LightRenderPatch.OnTesselationPatch)));
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        if (api.Side == EnumAppSide.Server)
        {
            ServerConfig = new LoadOrCreate().SapiConfig(api, ConfigServerName);
            AdditionalArmorFeaturesLibraryConfigHelper.Init(ServerConfig);

            if (AdditionalArmorFeaturesLibraryConfigHelper.oldVersion >= ServerConfig.Version)
            {
                AdditionalArmorFeaturesLibraryConfigHelper.MigrateConfig(api);
            }
        }

        ConfigSync = new ConfigSyncSystem(
            api,
            api.Side == EnumAppSide.Server ? ServerConfig : null
        );

        api.RegisterItemClass("additionalfeatures",typeof(ItemAdditionalFeatures));

        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:ArmorFeatures", typeof(CollectibleBehaviorArmorFeatures));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Fuel", typeof(CollectibleBehaviorFuel));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        Capi = api;

        ClientConfig = new LoadOrCreate().CapiConfig(api, ConfigClientName);

        ClientToggleChannel = api.Network
           .RegisterChannel("additionalarmorfeatureslibrarytoggle")
           .RegisterMessageType<AdditionalArmorFeaturesLibraryPacket>();

        api.Input.RegisterHotKey("toggleLight", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description"), GlKeys.L);
        api.Input.SetHotKeyHandler("toggleLight", _ => OnToggleLightHotkey(api.World.Player));

        api.Input.RegisterHotKey("toggleHoveredGearLight", Lang.Get("additionalarmorfeatureslibrary:keybind-gearslot-description"), GlKeys.L, HotkeyType.GUIOrOtherControls, false, true, false);
        api.Input.SetHotKeyHandler("toggleHoveredGearLight", _ => OnToggleHoveredGearLightHotkey(api.World.Player));

    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        Sapi = api;

        ServerToggleChannel = api.Network
            .RegisterChannel("additionalarmorfeatureslibrarytoggle")
            .RegisterMessageType<AdditionalArmorFeaturesLibraryPacket>()
            .SetMessageHandler<AdditionalArmorFeaturesLibraryPacket>(ToggleWearableItem);

        api.Event.PlayerNowPlaying += (player) =>
        {
            ConfigSync?.SendToPlayer(player);
        };

        OnLongServerFuelTick = api.Event.RegisterGameTickListener(OnServerFuelTick, 2000);
        OnLongServerTick = api.Event.RegisterGameTickListener(OnServerTick, 100);
    }

    public override void Dispose()
    {
        harmonyInstance.UnpatchAll(Mod.Info.ModID);
    }

    private void OnServerTick(float obj)
    {
        if (Sapi == null) return;

        foreach (IServerPlayer player in Sapi.World.AllOnlinePlayers)
        {
            if (player?.Entity == null) continue;

            var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (invGear == null) continue;

            foreach (ItemSlot slot in invGear)
            {
                if (slot == null || slot.Empty) continue;

                var item = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorArmorFeatures>(true);
                if (item == null) continue;

                if (!(ArmorFeaturesProp.ReadFrom(slot.Itemstack)?.UseFuel ?? false))
                {
                    continue;
                }

                var dresstype = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorWearable>(true)?.GetDressType(slot);

            }
        }
    }
    
    private void OnServerFuelTick(float dt)
    {
        if (Sapi == null) return;

        double totalHours = Sapi.World.Calendar.TotalHours;
        double hoursPassed = totalHours - lastCheckTotalHours;

        hoursPassed = Math.Min(hoursPassed, 0.5);

        if (hoursPassed <= 0)
        {
            lastCheckTotalHours = totalHours;
            return;
        }

        foreach (IServerPlayer player in Sapi.World.AllOnlinePlayers)
        {
            if (player != null)
            {
                var invGear =
                player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

                if (invGear == null) continue;

                foreach (ItemSlot slot in invGear)
                {
                    if (slot == null || slot.Empty) continue;

                    var source = slot.Itemstack.Collectible.GetCollectibleInterface<IPowerSource>();
                    if (source == null) continue;

                    if (!(ArmorFeaturesProp.ReadFrom(slot.Itemstack)?.UseFuel ?? false))
                    {
                        continue;
                    }

                    source.ConsumePower(slot, player.Entity, hoursPassed);

                    slot.MarkDirty();
                }
            }
        }
        lastCheckTotalHours = totalHours;
    }

    private bool ToggleWearableItem(IPlayer player, int itemslot = -1)
    {
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        // Gear slot override
        if (itemslot != -1 && invGear != null)
        {
            currentSlot = invGear[itemslot];
        }

        if (currentSlot == null || currentSlot.Empty) return false;
        var attachmentableLight = currentSlot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorArmorFeatures>(true);

        if (attachmentableLight == null) return false;
        var newState = !attachmentableLight.LightState(currentSlot.Itemstack);
        attachmentableLight.SetLightActive(currentSlot, newState, player.Entity);
        currentSlot.MarkDirty();

        ClientToggleChannel?.SendPacket(
                new AdditionalArmorFeaturesLibraryPacket
                {
                    ItemSlot = itemslot
                }
            );

        return true;
    }

    private bool OnToggleLightHotkey(IPlayer player)
    {
        return ToggleWearableItem(player);
    }

    private bool OnToggleHoveredGearLightHotkey(IPlayer player)
    {
        player = Capi.World.Player;
        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? hoveredSlot = player.InventoryManager.CurrentHoveredSlot;
        int itemslot = invGear?.GetSlotId(hoveredSlot) ?? -1;

        if (itemslot == -1)
        {
            return false;
        }

        LoggerExt.SendLogger(Capi, [$"The current slot that {player.PlayerName} is hovered over: {itemslot}"]);

        return ToggleWearableItem(player, itemslot);
    }

    public void ToggleWearableItem(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => ToggleWearableItem(player, packet.ItemSlot);

}
