using AdditionalArmorFeaturesLibrary.Collectible.Behavior;
using AdditionalArmorFeaturesLibrary.Interfaces;
using AdditionalArmorFeaturesLibrary.Utils;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace AdditionalArmorFeaturesLibrary.Collectible.Behavior
{

#nullable enable

    class CollectibleBehaviorJumppack : CollectibleBehavior
    {

        private ICoreAPI? api { get; set; }

        public ArmorFeaturesProp? armorFeaturesProp => ArmorFeaturesProp.ReadFrom(this.collObj);

        public CollectibleBehaviorJumppack(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;

            base.OnLoaded(api);
        }

        public bool JumppackState(ItemStack stack)
        {
            Console.WriteLine("jumppack trigger");
            return stack.Attributes.GetBool("togglejumppack");
        }

        public virtual void SetJumppackActive(ItemSlot slot, bool active, EntityPlayer player)
        {
            if (slot == null || slot.Empty || api == null) return;

            ItemStack stack = slot.Itemstack;

            // Update state
            stack.Attributes.SetBool("togglejumppack", active);

            slot.MarkDirty();
        }

        public virtual void JumpJumppack(ItemSlot slot, EntityPlayer player)
        {
            if (slot == null || slot.Empty || api == null) return;

            ItemStack stack = slot.Itemstack;

            //Only continue if jumppack is active.
            if (!JumppackState(stack)) return;

            //NEED TO ADD DELAY STILL
            long lastActivation = slot.Itemstack.TempAttributes.GetLong("jumppackLastUse");
            long now = api.World.ElapsedMilliseconds;

            if (now - lastActivation < (ArmorFeaturesProp.ReadFrom(stack)?.jumpDelay * 1000))
            {
                return;
            }
            slot.Itemstack.TempAttributes.SetLong("jumppackLastUse", now);

            //TILL HERE

            // Play toggle sound
            if (player != null)
            {
                string soundPath = ArmorFeaturesProp.ReadFrom(stack)?.jumppackSoundPath ?? string.Empty;

                if (!string.IsNullOrEmpty(soundPath))
                {
                    player.World.PlaySoundAt(
                        new AssetLocation(soundPath),
                        player.Pos.X + 0.5,
                        player.Pos.Y + 0.75,
                        player.Pos.Z + 0.5,
                        null,
                        randomizePitch: false,
                        volume: 1f
                    );
                }
            }

            player.Pos.Motion.Y = (ArmorFeaturesProp.ReadFrom(stack)?.jumpUpwardVel ?? 0) * 0.1;
            player.Pos.Motion.X = (ArmorFeaturesProp.ReadFrom(stack)?.jumpForwardVel ?? 0) * 0.1;

            //Consumes fuel, only when feature is enabled.
            var fuelbehavior = stack.Collectible.GetCollectibleBehavior<CollectibleBehaviorFuel>(true);
            fuelbehavior.ActionConsumePower(stack, player, ArmorFeaturesProp.ReadFrom(stack).jumpConsumption);

            slot.MarkDirty();
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return new WorldInteraction[2]
            {
                new WorldInteraction
                {
                    ActionLangCode = Lang.GetMatching("awearablelight:heldhelp-toggle-activeslot"),
                    MouseButton = EnumMouseButton.None,
                    HotKeyCode = "toggleLight"
                },
                new WorldInteraction
                {
                    ActionLangCode = Lang.GetMatching("awearablelight:heldhelp-toggle-gearslot"),
                    MouseButton = EnumMouseButton.None,
                    HotKeyCode = "toggleHoveredGearLight"

                }
            }.Append(base.GetHeldInteractionHelp(inSlot, ref handling));
        }
    }
}