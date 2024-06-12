using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using SmartDeconstruct;
using RimWorld;

namespace MultiReinstall.SmarterDeconstructionAndMiningPatch
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("com.harmony.rimworld.multireinstall.smarterdeconstructionandminingpatch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            SmartDeconstructMod.Harm.Patch(AccessTools.Method(typeof(JobDriver_HaulToContainer), "MakeNewToils", null, null), null, new HarmonyMethod(typeof(SmartDeconstructMod), "CheckForRoofsBeforeJob", null), null, null);

        }
    }

    [HarmonyPatch()]
    public static class SmartDeconstructMod_CheckForRoofsBeforeJob_Patch
    {
        static MethodInfo TargetMethod()
        {
            return typeof(SmartDeconstructMod)
                .GetNestedType("<>c__DisplayClass12_0", BindingFlags.NonPublic)
                .GetMethod("<CheckForRoofsBeforeJob>b__0", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld && (c.operand as FieldInfo).FieldType == typeof(DesignationDef)) + 2;

            var addCodes = new List<CodeInstruction>()
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(SmartDeconstructMod).GetNestedType("<>c__DisplayClass12_0", BindingFlags.NonPublic), "__instance"),
                CodeInstruction.Call(typeof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch), "IsReinstall"),
                CodeInstruction.StoreField(typeof(SmartDeconstructMod).GetNestedType("<>c__DisplayClass12_1", BindingFlags.NonPublic), "designation"),
                CodeInstruction.LoadArgument(0),
            };
            codes.InsertRange(pos, addCodes);

            pos = codes.FindIndex(c => c.opcode.Equals(OpCodes.Stloc_S) && (c.operand as LocalBuilder).LocalIndex.Equals(9)) + 1;
            addCodes = new List<CodeInstruction>()
            {
                CodeInstruction.LoadLocal(9),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                CodeInstruction.Call(typeof(JobMaker), "WithCount"),
                CodeInstruction.StoreLocal(9)
            };
            codes.InsertRange(pos, addCodes);

            return codes;
        }

        public static DesignationDef IsReinstall(JobDriver __instance)
        {
            if (__instance.GetType() == typeof(JobDriver_Uninstall)) return DesignationDefOf.Haul;
            if (__instance is JobDriver_HaulToContainer) return DesignationDefOf.Haul;
            if (__instance is JobDriver_Mine) return DesignationDefOf.Mine;
            if (__instance is JobDriver_Deconstruct) return DesignationDefOf.Deconstruct;
            return null;
        }
    }
}
