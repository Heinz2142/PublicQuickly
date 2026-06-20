using AdditionalArmorFeaturesLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AdditionalArmorFeaturesLibrary.Items
{
    public class ItemAdditionalFeatures : Item
    {
        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack)
        {
            Console.WriteLine("GOT IN LIGHTHSV");
            if (!stack.Attributes.GetBool("togglelight"))
            {
                return new byte[] { 0, 0, 0 };
            }

            return ArmorFeaturesProp.ReadFrom(stack)?.lightHSV
                ?? new byte[] { 0, 0, 0 };
        }
    }
}
