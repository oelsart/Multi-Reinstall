using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using HarmonyLib;
using System.Reflection;
using RimWorld;

namespace MultiReinstall
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("com.harmony.rimworld.multireinstall");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
    public static class Building_GetGizmos_Patch
    {
        public static void Postfix(ref IEnumerable<Gizmo> __result, Building __instance)
        {
            if (__instance.def.Minifiable && __instance.Faction == Faction.OfPlayer)
            {
                 __result = __result.AddItem(new Designator_MultiReinstall());
            }
        }
    }

    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob))]
    public static class GenConstruct_HandleBlockingThingJob_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode.Equals(OpCodes.Ldsfld) && (c.operand as FieldInfo).GetValue(typeof(JobDefOf)).Equals(JobDefOf.Deconstruct));

            codes.Replace(codes[pos], CodeInstruction.Call(typeof(GenConstruct_HandleBlockingThingJob_Patch), "ModeSelect"));
            codes.Insert(pos, new CodeInstruction(OpCodes.Ldloc_0));
            codes.Insert(pos, new CodeInstruction(OpCodes.Ldarg_0));

            foreach (var code in codes)
            {
                yield return code;
            }
        }

        public static JobDef ModeSelect(Thing constructible, Thing thing)
        {
            if (thing.def.Minifiable && constructible is Blueprint_Install2) return JobDefOf.Uninstall;
            return JobDefOf.Deconstruct;
        }
    }
}
