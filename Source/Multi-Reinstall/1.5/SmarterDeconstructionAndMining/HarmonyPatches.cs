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
            SmartDeconstructMod
            var harmony = new Harmony("com.harmony.rimworld.multireinstall.smarterdeconstructionandminingpatch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            SmartDeconstructMod.Harm.Patch(AccessTools.Method(typeof(JobDriver_HaulToContainer), "MakeNewToils"), postfix: new HarmonyMethod(typeof(SmartDeconstructMod), "CheckForRoofsBeforeJob"));

        }
    }
}
