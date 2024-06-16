using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using Verse.Steam;
using System.Reflection;

namespace MultiReinstall
{
    public class Designator_MultiReinstall : Designator
    {
        public IEnumerable<Building> BuildingsToReinstall
        {
            get
            {
                return Find.Selector.SelectedObjects.Select(o => o as Building).Where(b => b?.def.Minifiable ?? false);
            }
        }

        public override bool Visible => BuildingsToReinstall.Count() > 1;

        public override int DraggableDimensions => 2;

        private List<IntVec3> OffsetPos
        {
            get
            {
                return cachedBuildings.Select((building, i) =>
                {
                    var pos = cachedBuildingPositions[i];
                    if (!building.def.rotatable)
                    {
                        if (globalRot == Rot4.West || globalRot == Rot4.South) pos.x -= (building.def.Size.x - 1) % 2;
                        if (globalRot == Rot4.South || globalRot == Rot4.East) pos.z -= (building.def.Size.z - 1) % 2;
                    }

                    if (Event.current.shift)
                    {
                        pos.x = -pos.x;
                        if (!building.def.rotatable)
                        {
                            pos.x -= (building.def.Size.x - 1) % 2;
                        }
                        else
                        {
                            pos.x -= (globalRot.AsInt - 2) % 2 * (building.def.Size.x - 1) % 2;
                            pos.z += (globalRot.AsInt - 1) % 2 * (building.def.Size.z - 1) % 2;
                        }
                    }
                    return pos;
                }).ToList();
            }
        }

        private List<Rot4> FlipRot
        {
            get
            {
                return cachedBuildingRotations.Select((rot, i) =>
                {
                    if (Event.current.shift && rot.IsHorizontal && cachedBuildings.ElementAt(i).def.rotatable)
                    {
                        rot = rot.Opposite;
                    }
                    return rot;
                }).ToList();
            }
        }

        public Designator_MultiReinstall()
        {
            this.defaultLabel = "CommandMultiReinstall".Translate();
            this.defaultDesc = "CommandReinstallDesc".Translate();
            this.icon = ContentFinder<Texture2D>.Get("MultiReinstall/Designators/MultiReinstall", true);
            this.soundDragSustain = SoundDefOf.Designate_DragStandard;
            this.soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            this.useMouseIcon = true;
            this.Order = -9f;
        }

        public override void ProcessInput(Event ev)
        {
            this.cachedBuildings = this.BuildingsToReinstall;
            var xList = cachedBuildings.Select(b => b.Position.x);
            var zList = cachedBuildings.Select(b => b.Position.z);
            var center = new IntVec3((xList.Min() + xList.Max()) / 2, 0, (zList.Min() + zList.Max()) / 2);
            foreach (var building in cachedBuildings)
            {
                InstallBlueprintUtility.CancelBlueprintsFor(building);
                this.cachedBuildingPositions.Add(building.Position - center);
                this.cachedBuildingRotations.Add(building.Rotation);
            }
            base.ProcessInput(ev);
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return canDesignate;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            for (var i = 0; i < cachedBuildings.Count(); i++)
            {
                Multi_GenConstruct.PlaceBlueprintForReinstall(cachedBuildings.ElementAt(i), c + offsetPositions[i], Map, flippedRotations[i], Faction.OfPlayer);
                if (ModsConfig.IsActive("erdelf.MinifyEverything") && ModsConfig.IsActive("Mlie.SmarterDeconstructionAndMining") && cachedBuildings.ElementAt(i).def.IsEdifice())
                    Map.designationManager.AddDesignation(new Designation(cachedBuildings.ElementAt(i), DesignationDefOf.Haul));
            }
            Find.DesignatorManager.Deselect();
        }

        public override void SelectedProcessInput(Event ev)
        {
            this.HandleRotationShortcuts();
        }

        public override void SelectedUpdate()
        {
            IntVec3 center = UI.MouseCell();
            canDesignate = AcceptanceReport.WasAccepted;

            offsetPositions = OffsetPos;
            flippedRotations = FlipRot;
            for (var i = 0; i < cachedBuildings.Count(); i++)
            {
                Color ghostCol = Designator_Place.CanPlaceColor;
                var building = cachedBuildings.ElementAt(i);
                AcceptanceReport result;
                if ((result = Multi_GenConstruct.CanPlaceBlueprintAt(building.def, offsetPositions.Select((p, j) => center + p), flippedRotations, Map, false, cachedBuildings, building)) == AcceptanceReport.WasRejected)
                {
                    ghostCol = Designator_Place.CannotPlaceColor;
                    canDesignate = result;
                }
                GhostDrawer.DrawGhostThing(center + offsetPositions[i], flippedRotations[i], building.def, null, ghostCol, AltitudeLayer.Blueprint, building);
            }
        }

        public override void DoExtraGuiControls(float leftX, float bottomY)
        {
            Rect winRect = new Rect(leftX, bottomY - 120f, 200f, 120f);
            Find.WindowStack.ImmediateWindow(73095, winRect, WindowLayer.GameUI, delegate
            {
                RotationDirection rotationDirection = RotationDirection.None;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                Rect rect = new Rect(winRect.width / 2f - 64f - 5f, 15f, 64f, 64f);
                if (Widgets.ButtonImage(rect, TexUI.RotLeftTex, true, null))
                {
                    SoundDefOf.DragSlider.PlayOneShotOnCamera(null);
                    rotationDirection = RotationDirection.Counterclockwise;
                    Event.current.Use();
                }
                if (!SteamDeck.IsSteamDeck)
                {
                    Widgets.Label(rect, KeyBindingDefOf.Designator_RotateLeft.MainKeyLabel);
                }
                Rect rect2 = new Rect(winRect.width / 2f + 5f, 15f, 64f, 64f);
                if (Widgets.ButtonImage(rect2, TexUI.RotRightTex, true, null))
                {
                    SoundDefOf.DragSlider.PlayOneShotOnCamera(null);
                    rotationDirection = RotationDirection.Clockwise;
                    Event.current.Use();
                }
                if (!SteamDeck.IsSteamDeck)
                {
                    Widgets.Label(rect2, KeyBindingDefOf.Designator_RotateRight.MainKeyLabel);
                }
                if (rotationDirection != RotationDirection.None)
                {
                    this.Rotate(rotationDirection);
                }
                Widgets.Label(new Rect(0f, winRect.height - 38f, winRect.width, 30f), "MR.HoldShiftToFlip".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }, true, false, 1f, null);
        }

        private void HandleRotationShortcuts()
        {
            RotationDirection rotationDirection = RotationDirection.None;
            if (Event.current.button == 2)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                    this.middleMouseDownTime = Time.realtimeSinceStartup;
                }
                if (Event.current.type == EventType.MouseUp && Time.realtimeSinceStartup - this.middleMouseDownTime < 0.15f)
                {
                    rotationDirection = RotationDirection.Clockwise;
                }
            }
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                rotationDirection = RotationDirection.Clockwise;
            }
            if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                rotationDirection = RotationDirection.Counterclockwise;
            }
            if (rotationDirection != RotationDirection.None)
            {
                this.Rotate(rotationDirection);
            }
        }

        public override void Rotate(RotationDirection rotDir)
        {
            cachedBuildingPositions = cachedBuildingPositions.Select((p, i) => p.RotatedBy(rotDir)).ToList();
            cachedBuildingRotations = cachedBuildingRotations.Select((r, i) =>
            {
                if (cachedBuildings.ElementAt(i).def.rotatable) return r.Rotated(rotDir);
                return r;
            }).ToList();
            globalRot = globalRot.Rotated(rotDir);
        }

        public IEnumerable<Building> cachedBuildings;

        private List<IntVec3> cachedBuildingPositions = new List<IntVec3>();

        private List<IntVec3> offsetPositions = new List<IntVec3>();

        private List<Rot4> cachedBuildingRotations = new List<Rot4>();

        private List<Rot4> flippedRotations = new List<Rot4>();

        private Rot4 globalRot = Rot4.North;

        private float middleMouseDownTime;

        private AcceptanceReport canDesignate;
    }
}
