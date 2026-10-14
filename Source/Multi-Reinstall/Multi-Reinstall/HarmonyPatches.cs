using HarmonyLib;
using RimWorld;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace MultiReinstall;

[StaticConstructorOnStartup]
internal static class HarmonyPatches
{
	static HarmonyPatches()
	{
		var harmony = new Harmony("com.harmony.rimworld.multireinstall");
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		if (ModsConfig.IsActive("Mlie.SmarterDeconstructionAndMining"))
		{
			var patch = AccessTools.Method("SmartDeconstruct.SmartDeconstructMod:CheckForRoofsBeforeJob");
			harmony.Patch(AccessTools.Method(typeof(JobDriver_HaulToContainer), "MakeNewToils"), postfix: patch);
			patch = AccessTools.Method(typeof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch),
				nameof(SmartDeconstructMod_CheckForRoofsBeforeJob_Patch.Transpiler));
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
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var codes = instructions.ToList();
		var pos = codes.FindIndex(c => c.LoadsField(AccessTools.Field(typeof(JobDefOf), nameof(JobDefOf.Deconstruct))));

		codes.Replace(codes[pos], CodeInstruction.Call(typeof(GenConstruct_HandleBlockingThingJob_Patch), nameof(ModeSelect)));
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

[HarmonyPatch(typeof(PlaceWorker_Conduit), nameof(PlaceWorker_Conduit.AllowsPlacing))]
public static class PlaceWorker_Conduit_AllowsPlacing_Patch
{
	private static readonly List<Thing> tmpList = [];
	
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var codes = instructions.ToList();
		var pos = codes.FindIndex(c => c.Calls(((Delegate)GridsUtility.GetThingList).Method)) + 1;
		codes.Insert(pos, new CodeInstruction(OpCodes.Call, ((Delegate)AddIgnoreThingList).Method));

		return codes;
	}

	public static List<Thing> AddIgnoreThingList(List<Thing> thingList)
	{
		if (Find.DesignatorManager.SelectedDesignator is Designator_MultiReinstall designator_MultiReinstall)
		{
			tmpList.Clear();
			tmpList.AddRange(thingList.Except(designator_MultiReinstall.cachedBuildings));
		}

		return tmpList;
	}
}

public static class SmartDeconstructMod_CheckForRoofsBeforeJob_Patch
{
	private static FieldInfo field;

	public static MethodInfo TargetMethod()
	{
		return AccessTools.FindIncludingInnerTypes(AccessTools.TypeByName("SmartDeconstruct.SmartDeconstructMod"), t =>
		{
			var fields = t.GetDeclaredFields();
			return (field = fields.FirstOrDefault(f => f.FieldType == typeof(JobDriver) && f.Name == "__instance")) ==
			       null ? null : t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<CheckForRoofsBeforeJob>b__0"));
		});
	}

	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var codes = instructions.ToList();
		var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldnull);
		codes[pos] = CodeInstruction.LoadField(typeof(DesignationDefOf), nameof(DesignationDefOf.Deconstruct));
		codes[pos] = new CodeInstruction(OpCodes.Call, ((Delegate)IsReinstall).Method);
		codes.Insert(pos, new CodeInstruction(OpCodes.Ldfld, field));
		codes.Insert(pos, CodeInstruction.LoadArgument(0));

		pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 9) + 1;
		codes.InsertRange(pos, [
			CodeInstruction.LoadLocal(9),
			new CodeInstruction(OpCodes.Ldc_I4_1),
			CodeInstruction.Call(typeof(JobMaker), "WithCount"),
			CodeInstruction.StoreLocal(9)
		]);
		field = null;
		return codes;
	}

	public static DesignationDef IsReinstall(JobDriver __instance)
	{
		return __instance is JobDriver_HaulToContainer ? DesignationDefOf.Haul : null;
	}
}