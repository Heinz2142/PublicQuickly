using AdditionalArmorFeaturesLibrary.Interfaces;
using AdditionalArmorFeaturesLibrary.Util;
using AdditionalArmorFeaturesLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace AdditionalArmorFeaturesLibrary.Collectible.Behavior

{
    public class CollectibleBehaviorFuel : CollectibleBehavior, IPowerSource
    {

#nullable enable
        private ICoreAPI? api { get; set; }

        public ArmorFeaturesProp? armorFeaturesProp => ArmorFeaturesProp.ReadFrom(this.collObj);

        public CollectibleBehaviorFuel(CollectibleObject collObj) : base(collObj) { }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;

            _ = collObj.Attributes;

            base.OnLoaded(api);
        }

        private void SetFuelOnCrafted(ItemSlot slot)
        {
            if (slot.Empty) return;

            SetPower(slot.Itemstack, GetHighestFuel());
        }


        public float GetHighestFuel()
        {
            return 50f;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling)
        {
            base.OnCreatedByCrafting(allInputSlots, outputSlot, byRecipe, ref bhHandling);

            if (!(outputSlot is DummySlot))
            {
                ItemStack itemStack = outputSlot.Itemstack;
                if ((ArmorFeaturesProp.ReadFrom(itemStack)?.OnCraftedFueled ?? false))
                {
                    SetFuelOnCrafted(outputSlot);
                }
            }
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority, ref EnumHandling handling)
        {
            if (priority == EnumMergePriority.DirectMerge)
            {
                if (!(ArmorFeaturesProp.ReadFrom(sinkStack)?.UseFuel ?? false) )
                {
                    return base.GetMergableQuantity(sinkStack, sourceStack, priority, ref handling);
                }

                float fuel = GetStackFuel(sourceStack, sinkStack);

                if (fuel <= 0)
                {
                    return base.GetMergableQuantity(sinkStack, sourceStack, priority, ref handling);
                }

                double fuelHours = GetPower(sinkStack);

                handling = EnumHandling.PreventDefault;

            if (fuelHours >= (ArmorFeaturesProp.ReadFrom(sinkStack)?.fuelCapacity ?? 0f))
                {
                    Console.WriteLine("You still have " + sinkStack.Attributes.GetDouble("fuelHours", 0) +  " fuel left");
                    return 0;
                }
                return 1;
            }
            return base.GetMergableQuantity(sinkStack, sourceStack, priority, ref handling);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op, ref EnumHandling handling)
        {
            Console.WriteLine("TryMergeCall!");
            if (op.CurrentPriority != EnumMergePriority.DirectMerge)
            {
                handling = EnumHandling.PassThrough;
                return;
            }

            if (op.SourceSlot.Empty || op.SinkSlot.Empty)
            {
                handling = EnumHandling.PassThrough;
                return;
            }

            ItemStack sourceStack = op.SourceSlot.Itemstack;
            ItemStack sinkStack = op.SinkSlot.Itemstack;

            if (sourceStack == null || sinkStack == null)
            {
                handling = EnumHandling.PassThrough;
                return;
            }

            if (!(ArmorFeaturesProp.ReadFrom(sinkStack)?.UseFuel ?? false))
            {
                handling = EnumHandling.PassThrough;
                return;
            }

            float stackFuel = GetStackFuel(sourceStack, sinkStack);

            if (stackFuel <= 0f)
            {
                handling = EnumHandling.PassThrough;
                return;
            }

            double fuelHours = GetPower(sinkStack);

            if (fuelHours >= (ArmorFeaturesProp.ReadFrom(sinkStack)?.fuelCapacity ?? 0f))
            {
                if (api?.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(
                        this,
                        "lightfull",
                        Lang.Get(
                            "additionalarmorfeatureslibrary:ingameerror-light-full",
                            sinkStack.Collectible.GetHeldItemName(sinkStack)
                        )
                    );
                }

                handling = EnumHandling.PreventDefault;
                return;
            }

            AddPower(sourceStack, sinkStack, stackFuel);

            op.MovedQuantity = 1;
            op?.SourceSlot?.TakeOut(1);
            op?.SourceSlot?.MarkDirty();

            //api.Event.EnqueueMainThreadTask(() =>
            //{
            //    op.SourceSlot.TakeOut(1);
            //    op.SourceSlot.MarkDirty();
            //}, "consumefuel");

            handling = EnumHandling.PreventDefault;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot == null || inSlot.Empty) return;

            ItemStack itemStack = inSlot.Itemstack;

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (!(ArmorFeaturesProp.ReadFrom(itemStack)?.UseFuel ?? false))
            {
                return;
            }

            var fuel = GetPower(itemStack).ToString("0.0");
            var capfuel = (ArmorFeaturesProp.ReadFrom(itemStack)?.fuelCapacity ?? 0f).ToString("0.0");


            dsc.AppendLine();
            dsc.AppendLine();

            dsc.AppendLine(Lang.Get("additionalarmorfeatureslibrary:fuel-hours", fuel, capfuel));

        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            if (inSlot.Empty) return base.GetHeldInteractionHelp(inSlot, ref handling);

            ItemStack itemStack = inSlot.Itemstack;

            if ((ArmorFeaturesProp.ReadFrom(itemStack).UseFuel ?? false) == false || ArmorFeaturesProp.ReadFrom(itemStack).FuelList == null || ArmorFeaturesProp.ReadFrom(itemStack).FuelList.Count == 0)
            {
                return base.GetHeldInteractionHelp(inSlot, ref handling);
            }

            List<ItemStack> fuelStacks = new();

            foreach (var fuel in ArmorFeaturesProp.ReadFrom(itemStack).FuelList)
            {
                string key = fuel.Key;
                if (string.IsNullOrEmpty(key)) continue;

                var asset = new AssetLocation(key);

                // Item

                var item = api?.World.GetItem(asset);

                if (item != null)
                {
                    fuelStacks.AddIfNotPresent(new ItemStack(item));
                }

                // Wildcard match
                var matches = api?.World.SearchItems(asset);

                if (matches != null)
                {
                    foreach (var match in matches)
                    {
                        fuelStacks.AddIfNotPresent(new ItemStack(match));
                    }
                }

                // Block
                //var block = api?.World.GetBlock(asset);

                //if (block != null)
                //{
                //    fuelStacks.AddIfNotPresent(new ItemStack(block));
                //}


            }

            if (fuelStacks.Count == 0)
            {
                return base.GetHeldInteractionHelp(inSlot, ref handling);
            }

            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "awearablelight:heldhelp-fueltypes",
                    MouseButton = EnumMouseButton.None,
                    Itemstacks = fuelStacks.ToArray()
                }
            }
            .Append(base.GetHeldInteractionHelp(inSlot, ref handling));
        }

        public virtual float GetStackFuel(ItemStack sourceStack, ItemStack sinkStack)
        {
            if (sourceStack == null || sinkStack == null) return 0f;

            if (!(ArmorFeaturesProp.ReadFrom(sinkStack)?.UseFuel ?? false)) return 0f;

            if (ArmorFeaturesProp.ReadFrom(sinkStack).FuelList == null || ArmorFeaturesProp.ReadFrom(sinkStack).FuelList.Count == 0) return 0f;

            AssetLocation sourceCode = sourceStack.Collectible.Code;

            foreach (var (code, value) in ArmorFeaturesProp.ReadFrom(sinkStack).FuelList)
            {
                if (WildcardUtil.Match(new(code), sourceCode))
                {
                    return value;
                }
            }

            return 0f;
        }


        public virtual double GetPower(ItemStack sourceStack)
        {
            return sourceStack.Attributes.GetDouble("fuelHours", 0);
        }

        public virtual void SetPower(ItemStack sourceStack, double amount)
        {
            if (sourceStack == null) return;
            Console.WriteLine("Gets in SetPower");
            sourceStack.Attributes.SetDouble("fuelHours", GameMath.Clamp(amount, 0, ArmorFeaturesProp.ReadFrom(sourceStack)?.fuelCapacity ?? 0f));
        }

        public virtual void ConsumePower(ItemSlot slot, EntityPlayer entityPlayer, double amount)
        {
            if (slot.Empty) return;
            var attachmentableLight =
                slot.Itemstack.Collectible
                    .GetCollectibleBehavior<CollectibleBehaviorArmorFeatures>(true);

            if (attachmentableLight == null) return;
            // Only consume while active
            if (!attachmentableLight.LightState(slot.Itemstack))
            {
                Console.WriteLine("uhoh");
                return;
            }

            double fuel = GetPower(slot.Itemstack);

            SetPower(slot.Itemstack, fuel - amount);

            fuel = GetPower(slot.Itemstack);

            if (fuel <= 0)
            {
                attachmentableLight.SetLightActive(
                    slot,
                    false,
                    entityPlayer
                );
            }
        }

        public virtual void AddPower(ItemStack sourceStack, ItemStack sinkStack, double amount)
        {
            if (sourceStack == null) return;

            SetPower(sinkStack, GetPower(sinkStack) + amount);
        }

        public virtual bool HasPower(ItemStack sourceStack)
        {
            return GetPower(sourceStack) > 0;
        }

        public virtual bool IsFull(ItemStack sourceStack)
        {
            return GetPower(sourceStack) >= (ArmorFeaturesProp.ReadFrom(sourceStack)?.fuelCapacity ?? 0f);
        }
    }
}