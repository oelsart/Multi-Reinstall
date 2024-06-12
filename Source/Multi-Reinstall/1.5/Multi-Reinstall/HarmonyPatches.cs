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
            codes.Insert(pos, CodeInstruction.LoadLocal(0));
            codes.Insert(pos, CodeInstruction.LoadArgument(0));

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

    [HarmonyPatch(typeof(PlaceWorker_Conduit), "AllowsPlacing")]
    public static class PlaceWorker_Conduit_AllowsPlacing_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int pos = codes.FindIndex(c => c.opcode == OpCodes.Call && (c.operand as MethodInfo).DeclaringType == typeof(GridsUtility)) + 1;
            codes.Insert(pos, CodeInstruction.Call(typeof(PlaceWorker_Conduit_AllowsPlacing_Patch), "AddIgnoreThingList"));

            return codes;
        }

        public static List<Thing> AddIgnoreThingList(IEnumerable<Thing> thingList)
        {
            Designator designator;
            if ((designator = Find.DesignatorManager.SelectedDesignator) is Designator_MultiReinstall)
            {
                Designator_MultiReinstall designator_MultiReinstall = designator as Designator_MultiReinstall;
                thingList = thingList.Except(designator_MultiReinstall.cachedBuildings);
            }
            return thingList.ToList();
        }
    }
}
