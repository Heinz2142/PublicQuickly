using AdditionalArmorFeaturesLibrary.Utils;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace AdditionalArmorFeaturesLibrary.Items
{
    public class ItemAdditionalFeatures : Item
    {
        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack)
        {
            if (!stack.Attributes.GetBool("togglelight"))
            {
                return new byte[] { 0, 0, 0 };
            }

            return ArmorFeaturesProp.ReadFrom(stack)?.lightHSV
                ?? new byte[] { 0, 0, 0 };
        }

        //For when Equiping/Deequiping armors.
        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack)
        {
            base.OnModifiedInInventorySlot(world, slot, extractedStack);

            if (slot?.Inventory is InventoryBasePlayer inv)
            {
                var player = inv.Player;
                if (player == null) return;

                // Check if this is a gear slot
                if (slot.Inventory is InventoryCharacter invChar)
                {
                    Console.WriteLine("Got in part 1");
                    HandleGearChange(player);
                }
            }
        }

        public void HandleGearChange(IPlayer player)
        {
            if (player?.InventoryManager == null) return;

            var invGear = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (invGear == null) return;

            float bonusDamage = 0;
            float bonusKnockback = 0;
            float fallModifier = 1;

            foreach (var slot in invGear)
            {
                if (slot.Empty) continue;

                var props = ArmorFeaturesProp.ReadFrom(slot.Itemstack);

                if (props != null)
                {
                    bonusDamage += ArmorFeaturesProp.ReadFrom(slot.Itemstack).armorDamageBonus;
                    bonusKnockback += ArmorFeaturesProp.ReadFrom(slot.Itemstack).knockbackBonus;
                    fallModifier += ArmorFeaturesProp.ReadFrom(slot.Itemstack).falldamageModifier;
                    Console.WriteLine("Should get in here once");
                }
            }
            player.Entity.Stats.Set("armorDamageBonus", "armorDamageBonus", bonusDamage, true);

            player.Entity.Stats.Set("knockbackBonus", "knockbackBonus", bonusKnockback, true);

            player.Entity.Properties.FallDamageMultiplier = fallModifier;
        }

    }
}
