using AdditionalArmorFeaturesLibrary.Interfaces;
using AdditionalArmorFeaturesLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Server;

namespace AdditionalArmorFeaturesLibrary.Collectible.Behavior
{

    internal class CollectibleBehaviorNightvision : CollectibleBehavior
    {
        private ICoreAPI? api { get; set; }

        public ArmorFeaturesProp? armorFeaturesProp => ArmorFeaturesProp.ReadFrom(this.collObj);

        public ParticleEmitter particleEmitter = new ParticleEmitter();

        public CollectibleBehaviorNightvision(CollectibleObject collObj) : base(collObj) { }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;

            base.OnLoaded(api);
        }

        public bool NightvisionState(ItemStack stack)
        {
            return stack.Attributes.GetBool("togglenightvision");
        }

        public virtual void SetNightvisionActive(ItemSlot slot, bool active, EntityPlayer player)
        {
            if (slot == null || slot.Empty || api == null) return;

            ItemStack stack = slot.Itemstack;

            stack.Attributes.SetBool("togglenightvision", active);

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