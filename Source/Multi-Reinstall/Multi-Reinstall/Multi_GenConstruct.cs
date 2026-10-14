using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiReinstall;

public static class Multi_GenConstruct
{
	public static readonly FastInvokeHandler SetBuildingToReinstall =
		MethodInvoker.GetHandler(AccessTools.Method(typeof(Blueprint_Install), "SetBuildingToReinstall"));

	public static Blueprint_InstallMulti PlaceBlueprintForReinstall(Building buildingToReinstall, IntVec3 center, Map map,
		Rot4 rotation, Faction faction, bool sendBPSpawnedSignal = true)
	{
		var blueprint_Install = new Blueprint_InstallMulti
		{
			def = buildingToReinstall.def.installBlueprintDef
		};
		blueprint_Install.PostMake();
		blueprint_Install.PostPostMake();
		SetBuildingToReinstall(blueprint_Install, buildingToReinstall);
		blueprint_Install.SetFactionDirect(faction);
		GenSpawn.Spawn(blueprint_Install, center, map, rotation);
		if (faction != null && sendBPSpawnedSignal)
		{
			QuestUtility.SendQuestTargetSignals(faction.questTags, "PlacedBlueprint", blueprint_Install.Named("SUBJECT"));
		}

		return blueprint_Install;
	}

	public static AcceptanceReport CanPlaceBlueprintAt(BuildableDef entDef, List<IntVec3> centerList, List<Rot4> rotList,
		Map map, bool godMode, List<Building> thingToIgnoreList, Thing thing = null, ThingDef stuffDef = null,
		bool ignoreEdgeArea = false, bool ignoreInteractionSpots = false, bool ignoreClearableFreeBuildings = false)
	{
		var pos = thingToIgnoreList.FindIndex(t => t == thing);
		var center = centerList[pos];
		var rot = rotList[pos];
		if (thing is not null && thing.Position == center && thing.Rotation == rot)
			return new AcceptanceReport("IdenticalThingExists".Translate());

		var cellRect = GenAdj.OccupiedRect(center, rot, entDef.Size);
		if (stuffDef == null && thing != null)
		{
			stuffDef = thing.Stuff;
		}

		foreach (var c in cellRect)
		{
			if (!c.InBounds(map))
			{
				return new AcceptanceReport("OutOfBounds".Translate());
			}

			if (c.InNoBuildEdgeArea(map) && !godMode && !ignoreEdgeArea)
			{
				return "TooCloseToMapEdge".Translate();
			}
		}

		if (center.Fogged(map))
		{
			return "CannotPlaceInUndiscovered".Translate();
		}

		var thingList = center.GetThingList(map);
		for (var i = 0; i < thingList.Count; i++)
		{
			var thing2 = thingList[i];
			if (!thingToIgnoreList.Contains(thing2) && thing2.Position == center && thing2.Rotation == rot)
			{
				if (thing2.def == entDef)
				{
					return new AcceptanceReport("IdenticalThingExists".Translate());
				}

				if (thing2.def.entityDefToBuild == entDef)
				{
					if (thing2 is Blueprint)
					{
						return new AcceptanceReport("IdenticalBlueprintExists".Translate());
					}

					return new AcceptanceReport("IdenticalThingExists".Translate());
				}
			}
		}

		if (entDef is ThingDef { HasSingleOrMultipleInteractionCells: true } thingDef)
		{
			foreach (var c2 in ThingUtility.InteractionCellsWhenAt(thingDef, center, rot, map))
			{
				if (!c2.InBounds(map))
				{
					return new AcceptanceReport("InteractionSpotOutOfBounds".Translate());
				}

				var list = map.thingGrid.ThingsListAtFast(c2);
				for (var j = 0; j < list.Count; j++)
				{
					if (!thingToIgnoreList.Contains(list[j]))
					{
						if (list[j].def.passability != Traversability.Standable || list[j].def == thingDef)
						{
							return new AcceptanceReport("InteractionSpotBlocked".Translate(list[j].LabelNoCount, list[j])
								.CapitalizeFirst());
						}

						var entityDefToBuild = list[j].def.entityDefToBuild;
						if (entityDefToBuild != null &&
						    (entityDefToBuild.passability != Traversability.Standable || entityDefToBuild == thingDef))
						{
							return new AcceptanceReport("InteractionSpotWillBeBlocked".Translate(list[j].LabelNoCount, list[j])
								.CapitalizeFirst());
						}
					}
				}
			}
		}

		if (!ignoreInteractionSpots)
		{
			foreach (var c3 in GenAdj.CellsAdjacentCardinal(center, rot, entDef.Size))
			{
				if (c3.InBounds(map))
				{
					thingList = c3.GetThingList(map);
					for (var k = 0; k < thingList.Count; k++)
					{
						var thing3 = thingList[k];
						if (!thingToIgnoreList.Contains(thing3))
						{
							Blueprint blueprint;
							ThingDef thingDef3;
							Frame frame;
							if ((blueprint = (thing3 as Blueprint)) != null)
							{
								var thingDef2 = blueprint.def.entityDefToBuild as ThingDef;
								if (thingDef2 == null)
								{
									goto end;
								}

								thingDef3 = thingDef2;
							}
							else if ((frame = (thing3 as Frame)) != null)
							{
								var thingDef4 = frame.def.entityDefToBuild as ThingDef;
								if (thingDef4 == null)
								{
									goto end;
								}

								thingDef3 = thingDef4;
							}
							else
							{
								thingDef3 = thing3.def;
							}

							if (thingDef3.HasSingleOrMultipleInteractionCells &&
							    (entDef.passability != Traversability.Standable || entDef == thingDef3))
							{
								foreach (var c4 in ThingUtility.InteractionCellsWhenAt(thingDef3, thing3.Position, thing3.Rotation,
									         thing3.Map))
								{
									if (cellRect.Contains(c4))
									{
										return new AcceptanceReport("WouldBlockInteractionSpot".Translate(entDef.label, thingDef3.label)
											.CapitalizeFirst());
									}
								}
							}
						}

						end: ;
					}
				}
			}
		}

		if (entDef is TerrainDef terrainDef)
		{
			if (map.terrainGrid.TerrainAt(center) == terrainDef)
			{
				return new AcceptanceReport("TerrainIsAlready".Translate(terrainDef.label));
			}

			if (map.designationManager.DesignationAt(center, DesignationDefOf.SmoothFloor) != null)
			{
				return new AcceptanceReport("SpaceBeingSmoothed".Translate());
			}
		}

		if (CanBuildOnTerrain(entDef, center, map, rot, thingToIgnoreList, stuffDef))
		{
			if (ModsConfig.RoyaltyActive)
			{
				var list2 = map.listerThings.ThingsOfDef(ThingDefOf.MonumentMarker);
				for (var l = 0; l < list2.Count; l++)
				{
					var monumentMarker = (MonumentMarker)list2[l];
					if (!monumentMarker.complete && !monumentMarker.AllowsPlacingBlueprint(entDef, center, rot, stuffDef))
					{
						return new AcceptanceReport("BlueprintWouldCollideWithMonument".Translate());
					}
				}
			}

			if (!godMode)
			{
				foreach (var c5 in cellRect)
				{
					thingList = c5.GetThingList(map);
					for (var m = 0; m < thingList.Count; m++)
					{
						var thing4 = thingList[m];
						Building building;
						if (!thingToIgnoreList.Contains(thing4) &&
						    ((building = thing4 as Building) == null || !building.IsClearableFreeBuilding ||
						     !ignoreClearableFreeBuildings) && !GenConstruct.CanPlaceBlueprintOver(entDef, thing4.def))
						{
							return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
						}
					}
				}
			}

			if (entDef.PlaceWorkers != null)
			{
				for (var n = 0; n < entDef.PlaceWorkers.Count; n++)
				{
					var result = entDef.PlaceWorkers[n] is Placeworker_AttachedToWall
						? AllowsPlacing(centerList, rotList, map, thingToIgnoreList, thing)
						: entDef.PlaceWorkers[n].AllowsPlacing(entDef, center, rot, map, thing, thing);

					if (!result.Accepted)
					{
						return result;
					}
				}
			}

			return AcceptanceReport.WasAccepted;
		}

		if (entDef.GetTerrainAffordanceNeed(stuffDef) == null)
		{
			return new AcceptanceReport("TerrainCannotSupport".Translate(entDef).CapitalizeFirst());
		}

		if (entDef.useStuffTerrainAffordance && stuffDef != null)
		{
			return new AcceptanceReport("TerrainCannotSupport_TerrainAffordanceFromStuff"
				.Translate(entDef, entDef.GetTerrainAffordanceNeed(stuffDef), stuffDef).CapitalizeFirst());
		}

		return new AcceptanceReport("TerrainCannotSupport_TerrainAffordance"
			.Translate(entDef, entDef.GetTerrainAffordanceNeed(stuffDef)).CapitalizeFirst());
	}

	public static bool CanBuildOnTerrain(BuildableDef entDef, IntVec3 c, Map map, Rot4 rot,
		List<Building> thingToIgnoreList, ThingDef stuffDef = null)
	{
		if (entDef is TerrainDef && !c.GetTerrain(map).changeable)
		{
			return false;
		}

		var terrainAffordanceNeed = entDef.GetTerrainAffordanceNeed(stuffDef);
		if (terrainAffordanceNeed != null)
		{
			var cellRect = GenAdj.OccupiedRect(c, rot, entDef.Size);
			cellRect.ClipInsideMap(map);
			foreach (var c2 in cellRect)
			{
				if (!map.terrainGrid.TerrainAt(c2).affordances.Contains(terrainAffordanceNeed))
				{
					return false;
				}

				var thingList = c2.GetThingList(map);
				for (var i = 0; i < thingList.Count; i++)
				{
					if (!thingToIgnoreList.Contains(thingList[i]))
					{
						var terrainDef = thingList[i].def.entityDefToBuild as TerrainDef;
						if (terrainDef != null && !terrainDef.affordances.Contains(terrainAffordanceNeed))
						{
							return false;
						}
					}
				}
			}
		}

		return true;
	}

	public static AcceptanceReport AllowsPlacing(List<IntVec3> locList, List<Rot4> rotList,
		Map map, List<Building> thingToIgnoreList, Thing thing = null)
	{
		var pos = thingToIgnoreList.FirstIndexOf(t => t == thing);
		var loc = locList[pos];
		var rot = rotList[pos];

		var thingList = loc.GetThingList(map).Except(thingToIgnoreList).ToList();
		for (var i = 0; i < thingList.Count; i++)
		{
			var thing2 = thingList.ElementAt(i);
			var thingDef = GenConstruct.BuiltDefOf(thing2.def) as ThingDef;
			if (thingDef?.building != null)
			{
				if (thingDef.Fillage == FillCategory.Full)
				{
					return false;
				}

				if (thingDef.building.isAttachment && thing2.Rotation == rot)
				{
					return "SomethingPlacedOnThisWall".Translate();
				}
			}
		}

		var c = loc + GenAdj.CardinalDirections[rot.AsInt];
		if (!c.InBounds(map))
		{
			return false;
		}

		var virtualThingList = thingToIgnoreList.Select((t, i) => new Thing
		{
			def = t.def,
			Position = locList[i],
			Rotation = rotList[i]
		});
		thingList.Clear();
		thingList.AddRange(c.GetThingList(map).Except(thingToIgnoreList).Concat(virtualThingList));
		var flag = false;
		for (var j = 0; j < thingList.Count; j++)
		{
			if (GenConstruct.BuiltDefOf(thingList[j].def) is ThingDef { building: not null } thingDef2)
			{
				if (!thingDef2.building.supportsWallAttachments)
				{
					flag = true;
				}
				else if (thingDef2.Fillage == FillCategory.Full)
				{
					return true;
				}
			}
		}

		return flag ? "CannotSupportAttachment".Translate() : "MustPlaceOnWall".Translate();
	}
}