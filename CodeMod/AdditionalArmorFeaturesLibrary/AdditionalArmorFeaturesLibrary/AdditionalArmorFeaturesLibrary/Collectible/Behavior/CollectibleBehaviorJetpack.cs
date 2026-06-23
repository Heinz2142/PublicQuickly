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

namespace AdditionalArmorFeaturesLibrary.Collectible.Behavior
{

#nullable enable

    class CollectibleBehaviorJetpack : CollectibleBehavior
    {

        private ICoreAPI? api { get; set; }

        public ArmorFeaturesProp? armorFeaturesProp => ArmorFeaturesProp.ReadFrom(this.collObj);
        private class ParticleCache
        {
            public Vec3d localPoint;
            public string stepParent = "";
        }

        //For Particles For Jet
        private static readonly Dictionary<string, ParticleCache> particleStartByShapePath = new Dictionary<string, ParticleCache>();

        public CollectibleBehaviorJetpack(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;

            base.OnLoaded(api);
        }

        public bool JetpackState(ItemStack stack)
        {
            return stack.Attributes.GetBool("togglejetpack");
        }

        public virtual void SetJetpackActive(ItemSlot slot, bool active, EntityPlayer player)
        {
            Console.WriteLine("Jetpack turned");
            Console.WriteLine(active);
            if (slot == null || slot.Empty || api == null) return;

            ItemStack stack = slot.Itemstack;

            // Update state
            stack.Attributes.SetBool("togglejetpack", active);

            slot.MarkDirty();
        }


        public virtual void FlyJetpack(ItemSlot slot, EntityPlayer player)
        {
            if (slot == null || slot.Empty || api == null) return;

            ItemStack stack = slot.Itemstack;
            //Toggle required
            if (!JetpackState(stack)) return;

            //Only if player is holding jump
            if (!player.Controls.Jump) return;

            var source = stack.Collectible.GetCollectibleInterface<IPowerSource>();
            if (source == null || !source.HasPower(stack))
            {
                return;
            }

            //Propels person, also limits speed.
            player.Pos.Motion.Y = Math.Min(
                player.Pos.Motion.Y + (ArmorFeaturesProp.ReadFrom(stack).jetUpwardVel),
                ArmorFeaturesProp.ReadFrom(stack).jetMaxUpwardVel
            );

            //May god help me.
            var AttachPointPose = player?.AnimManager?.Animator?.GetAttachmentPointPose("Particleprop"); //"RightHand"
            var AttachPoint = AttachPointPose?.AttachPoint;
            
            if (player is null || AttachPointPose is null || AttachPoint is null) return;

            float bodyYaw = player.BodyYaw + MathF.PI/2;
            //var shape = player.Properties.Client.shape;


            float[] ModelMat = Mat4f.Create();
            Matrixf particleMatrix = new Matrixf().Set(ModelMat)
                .Rotate(0f, bodyYaw, 0f)
                .Translate(-0.5f, 0f, -0.5f) //wtf magic offset???
                .Mul(AttachPointPose.AnimModelMatrix);

            Vec3d particleOffset = particleMatrix.TransformVector(new Vec4f(0.0f, 0f, 0.0f, 1f)).XYZ.ToVec3d();
            Vec3d localPoint = particleOffset + player.Pos.XYZ;

            if (localPoint != null)
            {
                var motion = new Vec3f((float)-player.Pos.Motion.X * 0.5f, -1.0f, (float)-player.Pos.Motion.Z * 0.5f);

                api.World.SpawnParticles(
                    4,                      // quantity
                    ColorUtil.ToRgba(255, 0, 0, 255),    // color
                    localPoint,
                    localPoint,
                    motion,  // motion
                    motion,
                    1.0f,                   // life length
                    0.1f,                   // gravity / other param
                    0.1f                   // size 
                    );
            }

            //Commented this out because it appears to break animations, hopefully won't cause issues
            //slot.MarkDirty();
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