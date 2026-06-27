using AdditionalArmorFeaturesLibrary.Collectible.Behavior;
using AdditionalArmorFeaturesLibrary.Config;
using AdditionalArmorFeaturesLibrary.HarmonyPatches;
using AdditionalArmorFeaturesLibrary.Interfaces;
using AdditionalArmorFeaturesLibrary.Items;
using AdditionalArmorFeaturesLibrary.Network;
using AdditionalArmorFeaturesLibrary.Util;
using AdditionalArmorFeaturesLibrary.Utils;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace AdditionalArmorFeaturesLibrary;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class TogglePacket
{
    public string HotKeyCode { get; set; } = "";
}

public partial class AdditionalArmorFeaturesLibrarySystem : ModSystem, IRenderer
{


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


    JetpackSoundHelper jetpackSoundHelper { get; set; } = new JetpackSoundHelper();


    double lastCheckTotalHours;

    //For Jumppack
    private bool wasJumping;
    private long lastJumpPress;
    private const int DoubleTapWindowMs = 300;

    //For rendering and nightvision
    public double RenderOrder => 0;
    public int RenderRange => 1;


    public override void StartPre(ICoreAPI api)
    {
        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            var harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAllUncategorized();
        }
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

        api.RegisterItemClass("additionalfeatures", typeof(ItemAdditionalFeatures));

        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Power", typeof(CollectibleBehaviorPower));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Light", typeof(CollectibleBehaviorLight));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Fuel", typeof(CollectibleBehaviorFuel));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Exstate", typeof(CollectibleBehaviorExstate));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Jumppack", typeof(CollectibleBehaviorJumppack));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Jetpack", typeof(CollectibleBehaviorJetpack));
        api.RegisterCollectibleBehaviorClass("additionalarmorfeatureslibrary:Nightvision", typeof(CollectibleBehaviorNightvision));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        Capi = api;

        ClientConfig = new LoadOrCreate().CapiConfig(api, ConfigClientName);

        Capi.Event.RegisterGameTickListener(_ => CheckDoubleJump(Capi), 100);
        Capi.Event.RegisterGameTickListener(_ => CheckJetPack(Capi), 100);

        Capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "nightvision");

        ClientToggleChannel = api.Network
           .RegisterChannel("additionalarmorfeatureslibrarytoggle")
           .RegisterMessageType<AdditionalArmorFeaturesLibraryPacket>();

        Capi.Input.RegisterHotKey("togglePower", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description-power"), GlKeys.P);
        Capi.Input.SetHotKeyHandler("togglePower", _ => OnTogglePowerHotkey(Capi.World.Player));

        Capi.Input.RegisterHotKey("toggleLight", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description-light"), GlKeys.L);
        Capi.Input.SetHotKeyHandler("toggleLight", _ => OnToggleLightHotkey(Capi.World.Player));

        Capi.Input.RegisterHotKey("toggleExstate", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description-exstate"), GlKeys.K);
        Capi.Input.SetHotKeyHandler("toggleExstate", _ => OnToggleExstateHotkey(Capi.World.Player));

        Capi.Input.RegisterHotKey("toggleJumppack", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description-jumppack"), GlKeys.Z);
        Capi.Input.SetHotKeyHandler("toggleJumppack", _ => OnToggleJumppackHotkey(Capi.World.Player));

        Capi.Input.RegisterHotKey("toggleJetpack", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description-jetpack"), GlKeys.B);
        Capi.Input.SetHotKeyHandler("toggleJetpack", _ => OnToggleJetpackHotkey(Capi.World.Player));

        Capi.Input.RegisterHotKey("toggleNightvision", Lang.Get("additionalarmorfeatureslibrary:keybind-activeslot-description-jetpack"), GlKeys.N);
        Capi.Input.SetHotKeyHandler("toggleNightvision", _ => OnToggleNightvisionHotkey(Capi.World.Player));

    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        Sapi = api;

        ServerToggleChannel = Sapi.Network
            .RegisterChannel("additionalarmorfeatureslibrarytoggle")
            .RegisterMessageType<AdditionalArmorFeaturesLibraryPacket>()
            .SetMessageHandler<AdditionalArmorFeaturesLibraryPacket>(OnTogglePacket);

        Sapi.Event.PlayerNowPlaying += (player) =>
        {
            ConfigSync?.SendToPlayer(player);
        };

        OnLongServerFuelTick = Sapi.Event.RegisterGameTickListener(OnServerFuelTick, 2000);
        OnLongServerTick = Sapi.Event.RegisterGameTickListener(OnServerTick, 100);

    }

    //To distinguish between what to toggle.
    private void OnTogglePacket(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet)
    {
        switch (packet.Toggle)
        {
            case ToggleType.Power:
                TogglePowerWearableItem(player, packet.ItemSlot);
                break;
            case ToggleType.Light:
                ToggleLightWearableItem(player, packet.ItemSlot);
                break;
            case ToggleType.Exstate:
                ToggleExstateWearableItem(player, packet.ItemSlot);
                break;
            case ToggleType.Jumppack:
                JumppackActivate(player, packet.ItemSlot);
                break;
            case ToggleType.JumppackActivation:
                ToggleJumppackWearableItem(player, packet.ItemSlot);
                break;
            case ToggleType.Jetpack:
                ToggleJetpackWearableItem(player, packet.ItemSlot);
                break;
            case ToggleType.Nightvision:
                ToggleNightvisionWearableItem(player, packet.ItemSlot);
                break;
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        ServerToggleChannel = null;
        ClientToggleChannel = null;

        if (Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            var harmony = new Harmony(Mod.Info.ModID);
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private void OnServerTick(float obj)
    {
        if (Sapi == null) return;

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
                    double Consumption = hoursPassed;

                    if (ArmorFeaturesProp.ReadFrom(slot.Itemstack).FeaturesUsePower)
                    {
                        //For Each Turned on Passive, Increase Consumption by 1x
                        if (slot.Itemstack.Attributes.GetBool("togglelight")) { Consumption = hoursPassed * 2; }
                        if (slot.Itemstack.Attributes.GetBool("togglenightvision")) { Consumption = hoursPassed * 2; }

                        //Consumption for Jetpack
                        if (slot.Itemstack.Attributes.GetBool("togglejetpack"))
                        {
                            // Only burn extra fuel while actively flying
                            if (player.Entity.Controls.Jump)
                            {
                                Consumption += hoursPassed * (
                                    ArmorFeaturesProp.ReadFrom(slot.Itemstack)?.jetConsumption ?? 0
                                );
                            }
                        }
                    }

                    source.ConsumePower(slot, player.Entity, Consumption);

                    slot.MarkDirty();
                }
            }
        }
        lastCheckTotalHours = totalHours;
    }

    //Checks for jetpack flight.
    private void CheckJetPack(ICoreClientAPI api)
    {
        var player = api.World.Player;
        bool flying = player.Entity.Controls.Jump;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (invGear == null) return;

        ItemStack activeJetpack = null;
        bool active = false;

        if (flying)
        {
            foreach (ItemSlot slot in invGear)
            {
                if (slot.Empty) continue;

                var jetpack = slot.Itemstack.Collectible
                    .GetCollectibleBehavior<CollectibleBehaviorJetpack>(true);

                if (jetpack == null) continue;

                jetpack.FlyJetpack(slot, player.Entity);

                active = true;
                activeJetpack = slot.Itemstack;
            }
        }

        jetpackSoundHelper.ToggleJetpackSounds(api, player.Entity, activeJetpack, active);
    }

    //To activate jumppack on double tap.
    private void CheckDoubleJump(ICoreClientAPI api)
    {
        bool jumping = api.World.Player.Entity.Controls.Jump;

        // Detect key press (not key held)
        if (jumping && !wasJumping)
        {
            long now = api.World.ElapsedMilliseconds;

            if (now - lastJumpPress <= DoubleTapWindowMs)
            {
                JumppackHotkey(api.World.Player);
            }

            lastJumpPress = now;
        }

        wasJumping = jumping;
    }
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var player = Capi.World.Player;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (invGear == null) return;

        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var nightvis = slot.Itemstack.Collectible
                .GetCollectibleBehavior<CollectibleBehaviorNightvision>(true);

            if (nightvis == null) continue;

            if (nightvis?.NightvisionState(slot.Itemstack) == true)
            {
                Capi.Render.ShaderUniforms.NightVisionStrength = (float)GameMath.Clamp(20, 0, 0.8);
                return; // Found one active item, we're done.
            }
        }

        // No active night vision item was found.
        Capi.Render.ShaderUniforms.NightVisionStrength = 0;
    }

    ///POWER///
    //To Toggle Power
    public void TogglePowerWearableItem(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => TogglePowerWearableItem(player);
    private bool TogglePowerWearableItem(IPlayer player, int itemslot = -1)
    {
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        //Toggle for all worn items.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorPower>(true);
            if (armorPiece == null) continue;

            var newState = !armorPiece.PowerState(slot.Itemstack);
            armorPiece.SetPowerActive(slot, newState, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.Power
            }
        );

        return true;
    }

    private bool OnTogglePowerHotkey(IPlayer player)
    {
        return TogglePowerWearableItem(player);
    }

    ///LIGHT///
    //To Toggle Light
    public void ToggleLightWearableItem(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => ToggleLightWearableItem(player);
    private bool ToggleLightWearableItem(IPlayer player, int itemslot = -1)
    {
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        // Toggle for all worn items.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorLight>(true);
            if (armorPiece == null) continue;

            var newState = !armorPiece.LightState(slot.Itemstack);
            armorPiece.SetLightActive(slot, newState, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.Light
            }
        );

        return true;
    }

    private bool OnToggleLightHotkey(IPlayer player)
    {
        return ToggleLightWearableItem(player);
    }

    ///EXTRA STATE///
    //To Toggle Extra State
    public void ToggleExstateWearableItem(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => ToggleExstateWearableItem(player);
    private bool ToggleExstateWearableItem(IPlayer player, int itemslot = -1)
    {
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        //Toggle for all worn items.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorExstate>(true);
            if (armorPiece == null) continue;

            var newState = !armorPiece.ExstateState(slot.Itemstack);
            armorPiece.SwitchExstatestate(slot, newState, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.Exstate
            }
        );

        return true;
    }

    private bool OnToggleExstateHotkey(IPlayer player)
    {
        return ToggleExstateWearableItem(player);
    }

    ///JUMPPACK///
    //Jumppack action.
    public void JumppackActivate(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => JumppackActivate(player);
    private bool JumppackActivate(IPlayer player, int itemslot = -1)
    {
        Console.WriteLine("Jumppack Activated");
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        //Toggle for all worn items Though not recommended to have more than 1 item with jump function.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorJumppack>(true);
            if (armorPiece == null) continue;

            armorPiece.JumpJumppack(slot, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.Jumppack
            }
        );

        return true;
    }

    private bool JumppackHotkey(IPlayer player)
    {
        return JumppackActivate(player);
    }

    //To toggle jumppack
    public void ToggleJumppackWearableItem(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => ToggleJumppackWearableItem(player);
    private bool ToggleJumppackWearableItem(IPlayer player, int itemslot = -1)
    {
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        // Toggle for all worn items.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorJumppack>(true);
            if (armorPiece == null) continue;

            var newState = !armorPiece.JumppackState(slot.Itemstack);
            armorPiece.SetJumppackActive(slot, newState, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.JumppackActivation
            }
        );

        return true;
    }

    private bool OnToggleJumppackHotkey(IPlayer player)
    {
        return ToggleJumppackWearableItem(player);
    }

    ///JETPACK///
    //To toggle jetpack
    public void ToggleJetpackWearableItem(IServerPlayer player, AdditionalArmorFeaturesLibraryPacket packet) => ToggleJetpackWearableItem(player);
    private bool ToggleJetpackWearableItem(IPlayer player, int itemslot = -1)
    {
        Console.WriteLine("Toggling Jetpack");
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        // Toggle for all worn items.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorJetpack>(true);
            if (armorPiece == null) continue;

            var newState = !armorPiece.JetpackState(slot.Itemstack);
            armorPiece.SetJetpackActive(slot, newState, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.Jetpack
            }
        );

        return true;
    }

    private bool OnToggleJetpackHotkey(IPlayer player)
    {
        return ToggleJetpackWearableItem(player);
    }

    ///NIGHT VISION///
    //No need to sync to server, all client sided.
    private bool ToggleNightvisionWearableItem(IPlayer player, int itemslot = -1)
    {
        Console.WriteLine("Toggling Nightvision");
        if (player == null) return false;

        var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ItemSlot? currentSlot = player.InventoryManager.ActiveHotbarSlot;

        var logger = player.Entity.Api.Logger;

        // Toggle for all worn items.
        foreach (ItemSlot slot in invGear)
        {
            if (slot.Empty) continue;

            var armorPiece = slot.Itemstack.Collectible.GetCollectibleBehavior<CollectibleBehaviorNightvision>(true);
            if (armorPiece == null) continue;

            var newState = !armorPiece.NightvisionState(slot.Itemstack);
            armorPiece.SetNightvisionActive(slot, newState, player.Entity);

            slot.MarkDirty();
        }

        //sync to server.
        ClientToggleChannel?.SendPacket(
            new AdditionalArmorFeaturesLibraryPacket
            {
                Toggle = ToggleType.Nightvision
            }
        );

        return true;
    }

    private bool OnToggleNightvisionHotkey(IPlayer player)
    {
        return ToggleNightvisionWearableItem(player);
    }


}
