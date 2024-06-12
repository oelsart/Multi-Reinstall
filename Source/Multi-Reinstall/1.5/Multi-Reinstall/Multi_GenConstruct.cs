using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace MultiReinstall
{
    public static class Multi_GenConstruct
    {
        public static Blueprint_Install2 PlaceBlueprintForReinstall(Building buildingToReinstall, IntVec3 center, Map map, Rot4 rotation, Faction faction, bool sendBPSpawnedSignal = true)
        {
            Blueprint_Install2 blueprint_Install = new Blueprint_Install2();
            blueprint_Install.def = buildingToReinstall.def.installBlueprintDef;
            blueprint_Install.PostMake();
            blueprint_Install.PostPostMake();
            AccessTools.Method(typeof(Blueprint_Install2), "SetBuildingToReinstall").Invoke(blueprint_Install, BindingFlags.NonPublic, null, new object[] { buildingToReinstall }, null);
            blueprint_Install.SetFactionDirect(faction);
            GenSpawn.Spawn(blueprint_Install, center, map, rotation, WipeMode.Vanish, false, false);
            if (faction != null && sendBPSpawnedSignal)
            {
                QuestUtility.SendQuestTargetSignals(faction.questTags, "PlacedBlueprint", blueprint_Install.Named("SUBJECT"));
            }
            return blueprint_Install;
        }
        public static AcceptanceReport CanPlaceBlueprintAt(BuildableDef entDef, IEnumerable<IntVec3> centerList, IEnumerable<Rot4> rotList, Map map, bool godMode = false, IEnumerable<Thing> thingToIgnoreList = null, Thing thing = null, ThingDef stuffDef = null, bool ignoreEdgeArea = false, bool ignoreInteractionSpots = false, bool ignoreClearableFreeBuildings = false)
        {
            var pos = thingToIgnoreList.FirstIndexOf(t => t == thing);
            var center = centerList.ElementAt(pos);
            var rot = rotList.ElementAt(pos);
            if (thing.Position == center && thing.Rotation == rot) return new AcceptanceReport("IdenticalThingExists".Translate());

            CellRect cellRect = GenAdj.OccupiedRect(center, rot, entDef.Size);
            if (stuffDef == null && thing != null)
            {
                stuffDef = thing.Stuff;
            }
            foreach (IntVec3 c in cellRect)
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
            List<Thing> thingList = center.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing2 = thingList[i];
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
            ThingDef thingDef = entDef as ThingDef;
            if (thingDef != null && thingDef.HasSingleOrMultipleInteractionCells)
            {
                foreach (IntVec3 c2 in ThingUtility.InteractionCellsWhenAt(thingDef, center, rot, map, false))
                {
                    if (!c2.InBounds(map))
                    {
                        return new AcceptanceReport("InteractionSpotOutOfBounds".Translate());
                    }
                    List<Thing> list = map.thingGrid.ThingsListAtFast(c2);
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (!thingToIgnoreList.Contains(list[j]))
                        {
                            if (list[j].def.passability != Traversability.Standable || list[j].def == thingDef)
                            {
                                return new AcceptanceReport("InteractionSpotBlocked".Translate(list[j].LabelNoCount, list[j]).CapitalizeFirst());
                            }
                            BuildableDef entityDefToBuild = list[j].def.entityDefToBuild;
                            if (entityDefToBuild != null && (entityDefToBuild.passability != Traversability.Standable || entityDefToBuild == thingDef))
                            {
                                return new AcceptanceReport("InteractionSpotWillBeBlocked".Translate(list[j].LabelNoCount, list[j]).CapitalizeFirst());
                            }
                        }
                    }
                }
            }
            if (!ignoreInteractionSpots)
            {
                foreach (IntVec3 c3 in GenAdj.CellsAdjacentCardinal(center, rot, entDef.Size))
                {
                    if (c3.InBounds(map))
                    {
                        thingList = c3.GetThingList(map);
                        for (int k = 0; k < thingList.Count; k++)
                        {
                            Thing thing3 = thingList[k];
                            if (!thingToIgnoreList.Contains(thing3))
                            {
                                Blueprint blueprint;
                                ThingDef thingDef3;
                                Frame frame;
                                if ((blueprint = (thing3 as Blueprint)) != null)
                                {
                                    ThingDef thingDef2 = blueprint.def.entityDefToBuild as ThingDef;
                                    if (thingDef2 == null)
                                    {
                                        goto end;
                                    }
                                    thingDef3 = thingDef2;
                                }
                                else if ((frame = (thing3 as Frame)) != null)
                                {
                                    ThingDef thingDef4 = frame.def.entityDefToBuild as ThingDef;
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
                                if (thingDef3.HasSingleOrMultipleInteractionCells && (entDef.passability != Traversability.Standable || entDef == thingDef3))
                                {
                                    foreach (IntVec3 c4 in ThingUtility.InteractionCellsWhenAt(thingDef3, thing3.Position, thing3.Rotation, thing3.Map, false))
                                    {
                                        if (cellRect.Contains(c4))
                                        {
                                            return new AcceptanceReport("WouldBlockInteractionSpot".Translate(entDef.label, thingDef3.label).CapitalizeFirst());
                                        }
                                    }
                                }
                            }
                        end:;
                        }
                    }
                }
            }
            TerrainDef terrainDef = entDef as TerrainDef;
            if (terrainDef != null)
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
            if (Multi_GenConstruct.CanBuildOnTerrain(entDef, center, map, rot, thingToIgnoreList, stuffDef))
            {
                if (ModsConfig.RoyaltyActive)
                {
                    List<Thing> list2 = map.listerThings.ThingsOfDef(ThingDefOf.MonumentMarker);
                    for (int l = 0; l < list2.Count; l++)
                    {
                        MonumentMarker monumentMarker = (MonumentMarker)list2[l];
                        if (!monumentMarker.complete && !monumentMarker.AllowsPlacingBlueprint(entDef, center, rot, stuffDef))
                        {
                            return new AcceptanceReport("BlueprintWouldCollideWithMonument".Translate());
                        }
                    }
                }
                if (!godMode)
                {
                    foreach (IntVec3 c5 in cellRect)
                    {
                        thingList = c5.GetThingList(map);
                        for (int m = 0; m < thingList.Count; m++)
                        {
                            Thing thing4 = thingList[m];
                            Building building;
                            if (!thingToIgnoreList.Contains(thing4) && ((building = (thing4 as Building)) == null || !building.IsClearableFreeBuilding || !ignoreClearableFreeBuildings) && !GenConstruct.CanPlaceBlueprintOver(entDef, thing4.def))
                            {
                                return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
                            }
                        }
                    }
                }
                if (entDef.PlaceWorkers != null)
                {
                    for (int n = 0; n < entDef.PlaceWorkers.Count; n++)
                    {
                        AcceptanceReport result;
                        if (entDef.PlaceWorkers[n] is Placeworker_AttachedToWall)
                        {
                            result = Multi_GenConstruct.AllowsPlacing(entDef, centerList, rotList, map, thingToIgnoreList, thing);
                        }
                        else
                        {
                            result = thingToIgnoreList.Select(t => entDef.PlaceWorkers[n].AllowsPlacing(entDef, center, rot, map, t, thing)).FirstOrFallback(a => a == AcceptanceReport.WasRejected, AcceptanceReport.WasAccepted);
                        }
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
                return new AcceptanceReport("TerrainCannotSupport_TerrainAffordanceFromStuff".Translate(entDef, entDef.GetTerrainAffordanceNeed(stuffDef), stuffDef).CapitalizeFirst());
            }
            return new AcceptanceReport("TerrainCannotSupport_TerrainAffordance".Translate(entDef, entDef.GetTerrainAffordanceNeed(stuffDef)).CapitalizeFirst());
        }

        public static bool CanBuildOnTerrain(BuildableDef entDef, IntVec3 c, Map map, Rot4 rot, IEnumerable<Thing> thingToIgnoreList = null, ThingDef stuffDef = null)
        {
            if (entDef is TerrainDef && !c.GetTerrain(map).changeable)
            {
                return false;
            }
            TerrainAffordanceDef terrainAffordanceNeed = entDef.GetTerrainAffordanceNeed(stuffDef);
            if (terrainAffordanceNeed != null)
            {
                CellRect cellRect = GenAdj.OccupiedRect(c, rot, entDef.Size);
                cellRect.ClipInsideMap(map);
                foreach (IntVec3 c2 in cellRect)
                {
                    if (!map.terrainGrid.TerrainAt(c2).affordances.Contains(terrainAffordanceNeed))
                    {
                        return false;
                    }
                    List<Thing> thingList = c2.GetThingList(map);
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        if (!thingToIgnoreList.Contains(thingList[i]))
                        {
                            TerrainDef terrainDef = thingList[i].def.entityDefToBuild as TerrainDef;
                            if (terrainDef != null && !terrainDef.affordances.Contains(terrainAffordanceNeed))
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            return true;
        }

        public static AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IEnumerable<IntVec3> locList, IEnumerable<Rot4> rotList, Map map, IEnumerable<Thing> thingToIgnoreList = null, Thing thing = null)
        {
            var pos = thingToIgnoreList.FirstIndexOf(t => t == thing);
            var loc = locList.ElementAt(pos);
            var rot = rotList.ElementAt(pos);

            IEnumerable<Thing> thingList = loc.GetThingList(map).Except(thingToIgnoreList);
            for (int i = 0; i < thingList.Count(); i++)
            {
                Thing thing2 = thingList.ElementAt(i);
                ThingDef thingDef = GenConstruct.BuiltDefOf(thing2.def) as ThingDef;
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
            IntVec3 c = loc + GenAdj.CardinalDirections[rot.AsInt];
            if (!c.InBounds(map))
            {
                return false;
            }

            IEnumerable<Thing> virtualThingList = thingToIgnoreList.Select((t, i) =>
            {
                return new Thing()
                {
                    def = t.def,
                    Position = locList.ElementAt(i),
                    Rotation = rotList.ElementAt(i)
                };
            });
            thingList = c.GetThingList(map).Except(thingToIgnoreList).Concat(virtualThingList);
            bool flag = false;
            for (int j = 0; j < thingList.Count(); j++)
            {
                ThingDef thingDef2 = GenConstruct.BuiltDefOf(thingList.ElementAt(j).def) as ThingDef;
                if (thingDef2 != null && thingDef2.building != null)
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
            if (flag)
            {
                return "CannotSupportAttachment".Translate();
            }
            return "MustPlaceOnWall".Translate();
        }
    }
}
