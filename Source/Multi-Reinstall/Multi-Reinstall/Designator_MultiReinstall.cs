using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace MultiReinstall;

public sealed class Designator_MultiReinstall : Designator
{
	public List<Building> cachedBuildings;
	private readonly List<IntVec3> cachedBuildingPositions = [];
	private readonly List<IntVec3> offsetPositions = [];
	private readonly List<Rot4> cachedBuildingRotations = [];
	private readonly List<Rot4> flippedRotations = [];
	private Rot4 globalRot = Rot4.North;
	private float middleMouseDownTime;
	private AcceptanceReport canDesignate;
	
	public static IEnumerable<Building> BuildingsToReinstall
	{
		get { return Find.Selector.SelectedObjects.Select(o => o as Building).Where(b => b?.def.Minifiable ?? false); }
	}

	public override bool Visible => BuildingsToReinstall.Count() > 1;

	private IEnumerable<IntVec3> OffsetPos
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
						pos.x += ((globalRot.AsInt + building.Rotation.AsInt) % 4 - 1) % 2 * (building.def.Size.x - 1) % 2;
						pos.z -= ((globalRot.AsInt + 1 + building.Rotation.AsInt) % 4 - 1) % 2 * (building.def.Size.x - 1) % 2;
					}
				}

				return pos;
			});
		}
	}

	private IEnumerable<Rot4> FlipRot
	{
		get
		{
			return cachedBuildingRotations.Select((rot, i) =>
			{
				if (Event.current.shift && rot.IsHorizontal && cachedBuildings[i].def.rotatable)
				{
					rot = rot.Opposite;
				}

				return rot;
			});
		}
	}

	public Designator_MultiReinstall()
	{
		defaultLabel = "CommandMultiReinstall".Translate();
		defaultDesc = "CommandReinstallDesc".Translate();
		icon = ContentFinder<Texture2D>.Get("MultiReinstall/Designators/MultiReinstall");
		soundDragSustain = SoundDefOf.Designate_DragStandard;
		soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
		useMouseIcon = true;
		Order = -9f;
	}

	public override void ProcessInput(Event ev)
	{
		cachedBuildings = [.. BuildingsToReinstall];

		var center = CellRect.FromCellList(cachedBuildings.Select(static b => b.Position)).CenterCell;
		foreach (var building in cachedBuildings)
		{
			InstallBlueprintUtility.CancelBlueprintsFor(building);
			cachedBuildingPositions.Add(building.Position - center);
			cachedBuildingRotations.Add(building.Rotation);
		}

		base.ProcessInput(ev);
	}

	public override AcceptanceReport CanDesignateCell(IntVec3 c)
	{
		return canDesignate;
	}

	public override void DesignateSingleCell(IntVec3 c)
	{
		for (var i = 0; i < cachedBuildings.Count; i++)
		{
			Multi_GenConstruct.PlaceBlueprintForReinstall(cachedBuildings[i], offsetPositions[i], Map, flippedRotations[i],
				Faction.OfPlayer);
			if (ModsConfig.IsActive("erdelf.MinifyEverything") &&
			    ModsConfig.IsActive("Mlie.SmarterDeconstructionAndMining") && cachedBuildings[i].def.IsEdifice())
				Map.designationManager.AddDesignation(new Designation(cachedBuildings[i], DesignationDefOf.Haul));
		}

		Find.DesignatorManager.Deselect();
	}

	public override void SelectedProcessInput(Event ev)
	{
		HandleRotationShortcuts();
	}

	public override void SelectedUpdate()
	{
		var center = UI.MouseCell();
		canDesignate = AcceptanceReport.WasAccepted;

		offsetPositions.Clear();
		offsetPositions.AddRange(OffsetPos.Select(p => center + p));
		flippedRotations.Clear();
		flippedRotations.AddRange(FlipRot);
		for (var i = 0; i < cachedBuildings.Count; i++)
		{
			var ghostCol = Designator_Place.CanPlaceColor;
			var building = cachedBuildings[i];
			AcceptanceReport result;
			if ((result = Multi_GenConstruct.CanPlaceBlueprintAt(building.def, offsetPositions, flippedRotations, Map,
				    false, cachedBuildings, building)) == AcceptanceReport.WasRejected)
			{
				ghostCol = Designator_Place.CannotPlaceColor;
				canDesignate = result;
			}

			GhostDrawer.DrawGhostThing(offsetPositions[i], flippedRotations[i], building.def, null, ghostCol,
				AltitudeLayer.Blueprint, building);
		}
	}

	public override void DoExtraGuiControls(float leftX, float bottomY)
	{
		var winRect = new Rect(leftX, bottomY - 120f, 200f, 120f);
		Find.WindowStack.ImmediateWindow(73095, winRect, WindowLayer.GameUI, delegate
		{
			var rotationDirection = RotationDirection.None;
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			var rect = new Rect(winRect.width / 2f - 64f - 5f, 15f, 64f, 64f);
			if (Widgets.ButtonImage(rect, TexUI.RotLeftTex))
			{
				SoundDefOf.DragSlider.PlayOneShotOnCamera();
				rotationDirection = RotationDirection.Counterclockwise;
				Event.current.Use();
			}

			if (!SteamDeck.IsSteamDeck)
			{
				Widgets.Label(rect, KeyBindingDefOf.Designator_RotateLeft.MainKeyLabel);
			}

			var rect2 = new Rect(winRect.width / 2f + 5f, 15f, 64f, 64f);
			if (Widgets.ButtonImage(rect2, TexUI.RotRightTex))
			{
				SoundDefOf.DragSlider.PlayOneShotOnCamera();
				rotationDirection = RotationDirection.Clockwise;
				Event.current.Use();
			}

			if (!SteamDeck.IsSteamDeck)
			{
				Widgets.Label(rect2, KeyBindingDefOf.Designator_RotateRight.MainKeyLabel);
			}

			if (rotationDirection != RotationDirection.None)
			{
				Rotate(rotationDirection);
			}

			Widgets.Label(new Rect(0f, winRect.height - 38f, winRect.width, 30f), "MR.HoldShiftToFlip".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
		});
	}

	private void HandleRotationShortcuts()
	{
		var rotationDirection = RotationDirection.None;
		if (Event.current.button == 2)
		{
			if (Event.current.type == EventType.MouseDown)
			{
				Event.current.Use();
				middleMouseDownTime = Time.realtimeSinceStartup;
			}

			if (Event.current.type == EventType.MouseUp && Time.realtimeSinceStartup - middleMouseDownTime < 0.15f)
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
			Rotate(rotationDirection);
		}
	}

	private void Rotate(RotationDirection rotDir)
	{
		for (var i = 0; i < cachedBuildings.Count; i++)
		{
			cachedBuildingPositions[i] = cachedBuildingPositions[i].RotatedBy(rotDir);
			if (cachedBuildings[i].def.rotatable)
			{
				cachedBuildingRotations[i] = cachedBuildingRotations[i].Rotated(rotDir);
			}
		}

		globalRot = globalRot.Rotated(rotDir);
	}
}