using AdditionalArmorFeaturesLibrary.Collectible.Behavior;
using AdditionalArmorFeaturesLibrary.Interfaces;
using AdditionalArmorFeaturesLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

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
            Console.WriteLine("Jetpack trigger");
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

            Console.WriteLine("Actually gets here!?!?!");
            
            //Propels person, also limits speed.
            player.Pos.Motion.Y = Math.Min(
                player.Pos.Motion.Y + (ArmorFeaturesProp.ReadFrom(stack).jetUpwardVel),
                ArmorFeaturesProp.ReadFrom(stack).jetMaxUpwardVel
            );

            //May god help me.
            Vec3d localPoint = ParticleOrigin(player, stack);

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
                    1.0f                   // size 
                    );
            }

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

        private Vec3d ParticleOrigin(EntityAgent player, ItemStack item)
        {
            string openShapePath =  new AssetLocation(item.Item.Shape.Base.Domain, "shapes/" + item.Item.Shape.Base.Path + ".json");
            ParticleCache localPoint = GetParticlePoint(player, item, openShapePath);
            if (localPoint == null)
            {
                return null;
            }

            return AttachmentLocalToWorld(player, localPoint);
        }

        private ParticleCache GetParticlePoint(EntityAgent player, ItemStack item, string shapePath)
        {
            if (string.IsNullOrEmpty(shapePath))
            {
                return null;
            }

            ParticleCache cached;
            if (particleStartByShapePath.TryGetValue(shapePath, out cached))
            {
                return cached;
            }
            try
            {
                IAsset shapeAsset = api.Assets.TryGet(shapePath, true);
                if (shapeAsset == null)
                {   
                    return null;
                }

                JsonObject shapeRoot = JsonObject.FromJson(shapeAsset.ToText());
                ParticleCache foundPoint;
                if (!TryFindParticlePoint(player, shapeRoot["elements"].AsArray(), "Particleprop", out foundPoint))
                {
                    return null;
                }


                particleStartByShapePath[shapePath] = foundPoint;
                return foundPoint;
            }
            catch
            {
                return null;
            }
        }

        private bool TryFindParticlePoint(EntityAgent player, JsonObject[] elements, string pointCode, out ParticleCache point)
        {
            point = null;
            if (elements == null || string.IsNullOrEmpty(pointCode))
            {
                return false;
            }

            for (int i = 0; i < elements.Length; i++)
            {
                JsonObject element = elements[i];
                if (!element.Exists)
                {
                    continue;
                }

                JsonObject[] attachmentPoints = element["attachmentpoints"].AsArray();
                if (attachmentPoints != null)
                {
                    for (int pointIndex = 0; pointIndex < attachmentPoints.Length; pointIndex++)
                    {
                        JsonObject attachmentPoint = attachmentPoints[pointIndex];
                        if (!attachmentPoint.Exists)
                        {
                            continue;
                        }

                        if (!string.Equals(attachmentPoint["code"].AsString(), pointCode, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        point = new ParticleCache
                        {
                            localPoint = new Vec3d(
                            attachmentPoint["posX"].AsDouble(),
                            attachmentPoint["posY"].AsDouble(),
                            attachmentPoint["posZ"].AsDouble()
                            ),
                            stepParent = element["stepParentName"]?.AsString()
                        };
                        return true;
                    }
                }

                JsonObject[] children = element["children"].AsArray();
                if (TryFindParticlePoint(player, children, pointCode, out point))
                {
                    return true;
                }
            }

            return false;
        }

        private Vec3d AttachmentLocalToWorld(EntityAgent player, ParticleCache localPoint)
        {
            if (player == null || localPoint == null)
            {
                return null;
            }

            // Convert from model coordinates (0-16) to block units
            double x = (localPoint.localPoint.X - 8.0) / 8.0;
            double y = localPoint.localPoint.Y / 16.0;
            double z = (localPoint.localPoint.Z - 8.0) / 8.0;

            float yaw = player.Pos.Yaw;

            double cos = Math.Cos(-yaw);
            double sin = Math.Sin(-yaw);

            // Rotate around the Y axis
            double rx = x * cos - z * sin;
            double rz = x * sin + z * cos;

            Console.WriteLine("Player POS:" + player.Pos);
            Console.WriteLine("Eyes POS:" + player.Pos.XYZ.AddCopy(player.LocalEyePos));


            //To correct for body model.
            Vec3d bodyModifier;
            switch (localPoint.stepParent)
            {
                case "Head":
                    bodyModifier = player.Pos.XYZ.Add(0, player.LocalEyePos.Y, 0);
                    break;
                case "Body":
                    bodyModifier = player.Pos.XYZ.Add(0, -0.1, 0);
                    break;
                case "Legs":
                    bodyModifier = player.Pos.XYZ.Add(0, -0.6, 0);
                    break;
                default:
                    bodyModifier = player.Pos.XYZ;
                    break;

            }
            return new Vec3d(
                bodyModifier.X + rx,
                bodyModifier.Y + y,
                bodyModifier.Z + rz
            );

        }
    }
}