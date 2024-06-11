using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MultiReinstall
{
    public class Designator_SelectToMultiReinstall : Designator
    {
        public override int DraggableDimensions
        {
            get
            {
                return 2;
            }
        }

        public Designator_SelectToMultiReinstall()
        {
            this.defaultLabel = "CommandMultiReinstall".Translate();
            this.defaultDesc = "CommandReinstallDesc".Translate();
            this.icon = ContentFinder<Texture2D>.Get("MultiReinstall/Designators/MultiReinstall", true);
            this.soundDragSustain = SoundDefOf.Designate_DragStandard;
            this.soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            this.useMouseIcon = true;
            this.Order = 50f;
            this.isOrder = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            return AcceptanceReport.WasAccepted;
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            return t as Building != null && t.def.Minifiable;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            foreach (var thing in c.GetThingList(Map).Where(t => this.CanDesignateThing(t)))
            {
                Find.Selector.Select(thing);
            }
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            if (TutorSystem.TutorialMode && !TutorSystem.AllowAction(new EventPack(this.TutorTagDesignate, cells)))
            {
                return;
            }
            foreach (IntVec3 intVec in cells)
            {
                DesignateSingleCell(intVec);
            }
            Designator_MultiReinstall designator_MultiReinstall = new Designator_MultiReinstall();
            if (designator_MultiReinstall.BuildingsToReinstall.Count() != 0)
            {
                designator_MultiReinstall.ProcessInput(Event.current);
                Find.DesignatorManager.Select(designator_MultiReinstall);
            }
            if (TutorSystem.TutorialMode)
            {
                TutorSystem.Notify_Event(new EventPack(this.TutorTagDesignate, cells));
            }
        }
    }
}
