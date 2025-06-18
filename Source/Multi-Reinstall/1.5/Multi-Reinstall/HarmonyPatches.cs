using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace MultiReinstall
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("com.harmony.rimworld.multireinstall");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            if (ModsConfig.IsActive("Mlie.SmarterDeconstructionAndMining"))
            {
                var patch = AccessTools.Method("SmartDeconstruct.SmartDeconstructMod:CheckForRoofsBeforeJob");
                harmony.Patch(AccessTools.Method(typeof(JobDriver_HaulToContainer), "MakeNewToils"), postfix: patch);
                patch = AccessTools.Method(typeof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch), nameof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch.Transpiler));
                harmony.Patch(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch.TargetMethod(), transpiler: patch);
            }
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
            if (thing.def.Minifiable && constructible is Blueprint_InstallMulti) return JobDefOf.Uninstall;
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
            if (Find.DesignatorManager.SelectedDesignator is Designator_MultiReinstall designator_MultiReinstall)
            {
                tmpList.Clear();
                tmpList.AddRange(thingList.Except(designator_MultiReinstall.cachedBuildings));
            }
            return thingList.ToList();
        }

        private static readonly List<Thing> tmpList = new List<Thing>();
    }

    public static class SmartDeconstructMod_CheckForRoofsBeforeJob_Patch
    {
        static FieldInfo field;

        public static MethodInfo TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes(AccessTools.TypeByName("SmartDeconstruct.SmartDeconstructMod"), t =>
            {
                Log.Message(t);
                var fields = t.GetDeclaredFields();
                if ((field = fields.FirstOrDefault(f => f.FieldType == typeof(JobDriver) && f.Name == "__instance")) == null) return null;
                if (!fields.Any(f => f.FieldType == typeof(bool) && f.Name == "isMine")) return null;
                if (!fields.Any(f => f.FieldType == typeof(bool) && f.Name == "isDecon")) return null;
                if (!fields.Any(f => f.FieldType == typeof(Action))) return null;
                return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<CheckForRoofsBeforeJob>b__0"));
            });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int pos = codes.FindIndex(c => c.opcode == OpCodes.Ldnull);
            codes[pos] = CodeInstruction.LoadField(typeof(DesignationDefOf), nameof(DesignationDefOf.Deconstruct));
            codes[pos] = CodeInstruction.Call(typeof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch), "IsReinstall");
            codes.Insert(pos, new CodeInstruction(OpCodes.Ldfld, field));
            codes.Insert(pos, CodeInstruction.LoadArgument(0));

            pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 9) + 1;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(9),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                CodeInstruction.Call(typeof(JobMaker), "WithCount"),
                CodeInstruction.StoreLocal(9)
            });
            field = null;
            return codes;
        }

        public static DesignationDef IsReinstall(JobDriver __instance)
        {
            if (__instance is JobDriver_HaulToContainer) return DesignationDefOf.Haul;
            return null;
        }
    }
}
