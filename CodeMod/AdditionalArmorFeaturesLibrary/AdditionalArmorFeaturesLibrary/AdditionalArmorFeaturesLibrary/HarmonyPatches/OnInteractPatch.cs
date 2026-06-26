using AdditionalArmorFeaturesLibrary.Collectible.Behavior;
using AdditionalArmorFeaturesLibrary.Utils;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace AdditionalArmorFeaturesLibrary.HarmonyPatches
{
    [HarmonyPatch(typeof(EntityAgent), nameof(EntityAgent.OnInteract))]
    public static class OnInteractPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            //step 1: damage injection after Collectible.GetAttackPower
            codeMatcher.MatchStartForward(CodeMatch.Calls(() => default(CollectibleObject).GetAttackPower));
            codeMatcher.Advance(3); //advance to after the switch statement ? (after stloc.2) but before the setup for get_Itemstack
            //Console.WriteLine("Transpiler step 1 at " + codeMatcher.Instruction);
            codeMatcher.InsertAfter([
                new CodeInstruction(OpCodes.Ldloca_S, 2), //load reference to damage value
                new CodeInstruction(OpCodes.Ldarg_1), //load byEntity
                new CodeInstruction(OpCodes.Ldarg_0), //load thisEntity
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OnInteractPatch), nameof(DamageInjection)))
                ]);

            //step 2: DamageSource modification before this.GetInterface<IMountable>()
            codeMatcher.MatchStartForward(CodeMatch.Calls(() => default(Entity).GetInterface<IMountable>));
            codeMatcher.Advance(-1); //step back to before the ldarg.0 that sets up that call
            //Console.WriteLine("Transpiler step 2 at " + codeMatcher.Instruction);
            codeMatcher.Insert([
                new CodeInstruction(OpCodes.Ldloc_S, 7), //load DamageSource
                new CodeInstruction(OpCodes.Ldarg_1), //load byEntity
                new CodeInstruction(OpCodes.Ldarg_0), //load thisEntity
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OnInteractPatch), nameof(DamageSourceInjection)))
                ]);

            return codeMatcher.Instructions();
        }

        //DamageInjection runs just after slot.Itemstack.Collectible.GetAttackPower(slot.Itemstack);
        public static void DamageInjection(ref float damage, Entity byEntity, EntityAgent damagedEntity)
        {
            damage += 5;
            byEntity.Api.Logger.Debug($"{byEntity.Api.Side} Dealing {damage} from {byEntity.GetName()} to {damagedEntity.GetName()}");
        }

        //DamageSourceInjection runs just before IMountable im = this.GetInterface<IMountable>();
        public static void DamageSourceInjection(DamageSource damageSource, Entity byEntity, EntityAgent damagedEntity)
        {
            damageSource.KnockbackStrength += 5;
            byEntity.Api.Logger.Debug($"{byEntity.Api.Side} Dealing {damageSource.KnockbackStrength} knockback from {byEntity.GetName()} to {damagedEntity.GetName()}");
        }
    }
}
