using RimWorld;
using UnityEngine;
using Verse;

namespace MultiReinstall;

public sealed class Designator_SelectToMultiReinstall : Designator
{
	public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

	public Designator_SelectToMultiReinstall()
	{
		defaultLabel = "CommandMultiReinstall".Translate();
		defaultDesc = "CommandReinstallDesc".Translate();
		icon = ContentFinder<Texture2D>.Get("MultiReinstall/Designators/MultiReinstall");
		soundDragSustain = SoundDefOf.Designate_DragStandard;
		soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
		useMouseIcon = true;
		Order = 50f;
		isOrder = true;
	}

	public override AcceptanceReport CanDesignateCell(IntVec3 loc)
	{
		return AcceptanceReport.WasAccepted;
	}

	public override AcceptanceReport CanDesignateThing(Thing t)
	{
		return t is Building { def.Minifiable: true };
	}

	public override void DesignateSingleCell(IntVec3 c)
	{
		foreach (var thing in c.GetThingList(Map).Where(t => CanDesignateThing(t)))
		{
			Find.Selector.Select(thing);
		}
	}

	public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
	{
		var cellList = cells.ToList();
		if (TutorSystem.TutorialMode && !TutorSystem.AllowAction(new EventPack(TutorTagDesignate, cellList)))
		{
			return;
		}
		foreach (var intVec in cellList)
		{
			DesignateSingleCell(intVec);
		}
		var designator_MultiReinstall = new Designator_MultiReinstall();
		if (Designator_MultiReinstall.BuildingsToReinstall.Any())
		{
			designator_MultiReinstall.ProcessInput(Event.current);
			Find.DesignatorManager.Select(designator_MultiReinstall);
		}
		if (TutorSystem.TutorialMode)
		{
			TutorSystem.Notify_Event(new EventPack(TutorTagDesignate, cellList));
		}
	}
}