using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using MURWallLight;
using Mono.Cecil.Cil;
using System.Reflection.Emit;
using RimWorld;
using System.IO;

namespace MultiReinstall.WallLightPatch
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            if (ModsConfig.IsActive("erdelf.MinifyEverything"))
            {
                var harmony = new Harmony("com.harmony.rimworld.multireinstall.walllightpatch");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
        }
    }

    [HarmonyPatch(typeof(PlaceWorker_WallLight), nameof(PlaceWorker_WallLight.AllowsPlacing))]
    public static class PlaceWorker_WallLight_AllowsPlacing_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && (c.operand as MethodInfo).DeclaringType == typeof(ThingGrid)) + 1;
            codes.Insert(pos, CodeInstruction.Call(typeof(PlaceWorker_WallLight_AllowsPlacing_Patch), "AddVirtualThingList"));

            return codes;
        }

        public static List<Thing> AddVirtualThingList(IEnumerable<Thing> thingList)
        {
            Designator designator;
            if ((designator = Find.DesignatorManager.SelectedDesignator) is Designator_MultiReinstall)
            {
                Designator_MultiReinstall designator_MultiReinstall = designator as Designator_MultiReinstall;
                var virtualThingList = designator_MultiReinstall.cachedBuildings.Where(b => b.def.holdsRoof && b.def.IsEdifice() && !b.def.IsDoor)
                    .Select((b, i) =>
                    {
                        return new Thing()
                        {
                            def = b.def,
                            Position = designator_MultiReinstall.cachedBuildingPositions[i],
                            Rotation = designator_MultiReinstall.cachedBuildingRotations[i]
                        };
                    });
                thingList = thingList.Concat(virtualThingList);
            }
            return thingList.ToList();
        }
    }

    [HarmonyPatch(typeof(CompMountableWall), "PostDeSpawn")]
    public static class  CompMountableWall_PostDeSpawn_Patch
    {
        public static void Prefix(Map map, CompMountableWall __instance)
        {
            foreach (Thing thing in __instance.attachedThings)
            {
                var minifiedThing = thing.MakeMinified();
                GenDrop.TryDropSpawn(minifiedThing, thing.Position, map, ThingPlaceMode.Near, out var resultingThing);
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.Select(c => c.opcode == OpCodes.Ldc_I4_4 ? new CodeInstruction(OpCodes.Ldc_I4_0) : c);
        }
    }
}
