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
            int pos = codes.FindIndex(c => c.opcode == OpCodes.Ldnull);
            codes[pos] = CodeInstruction.LoadField(typeof(DesignationDefOf), nameof(DesignationDefOf.Deconstruct));
            codes[pos] = CodeInstruction.Call(typeof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch), "IsReinstall");
            codes.Insert(pos, CodeInstruction.LoadField(typeof(SmartDeconstructMod).GetNestedType("<>c__DisplayClass12_0", BindingFlags.NonPublic), "__instance"));
            codes.Insert(pos, new CodeInstruction(OpCodes.Ldarg_0));

            pos = codes.FindLastIndex(c => c.opcode.Equals(OpCodes.Call) && (c.operand as MethodInfo).DeclaringType.Equals(typeof(JobMaker))) + 2;
            var addCodes = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloc_3),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                CodeInstruction.StoreField(typeof(Job), "count")
            };
            codes.InsertRange(pos, addCodes);

            return codes;
        }

        public static DesignationDef IsReinstall(JobDriver __instance)
        {
            if (__instance is JobDriver_HaulToContainer) return DesignationDefOf.Uninstall;
            return null;
        }
    }
}
