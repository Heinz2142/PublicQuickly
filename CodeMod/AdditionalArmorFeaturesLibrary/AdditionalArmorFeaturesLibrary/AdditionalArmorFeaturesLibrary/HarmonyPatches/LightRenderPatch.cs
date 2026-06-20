using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AdditionalArmorFeaturesLibrary.HarmonyPatches
{
    public class LightRenderPatch
    {
        public static void OnTesselationPatch(EntityBehaviorContainer __instance, ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements) {
            //__instance.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
            if (__instance.Inventory != null)
            {
                ItemSlot brightestSlot = __instance.Inventory.MaxBy(delegate (ItemSlot slot)
                {
                    if (!slot.Empty)
                    {
                        byte[] hsv = slot.Itemstack.Collectible.GetLightHsv(__instance.entity.World.BlockAccessor, null, slot.Itemstack);

                        return hsv?[2] ?? 0;
                    }
                    return 0;
                });
                if (!brightestSlot.Empty)
                {
                    __instance.entity.LightHsv = brightestSlot.Itemstack.Collectible.GetLightHsv(__instance.entity.World.BlockAccessor, null, brightestSlot.Itemstack);
                    return;
                }
                __instance.entity.LightHsv = null;
            }
        }
    }
}
