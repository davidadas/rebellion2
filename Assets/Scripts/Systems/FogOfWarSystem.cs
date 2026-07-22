using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages fog of war operations: capturing snapshots, invalidating moved entities, building faction views.
    /// Operates on faction state, does not hold its own state.
    /// </summary>
    public class FogOfWarSystem
    {
        private readonly GameRoot _game;
        private readonly FogOfWarRecorder _recorder;

        /// <summary>
        /// Creates a FogOfWarSystem for the given game instance.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public FogOfWarSystem(GameRoot game)
        {
            _game = game;
            _recorder = new FogOfWarRecorder();
        }

        /// <summary>
        /// Captures a snapshot of a planet for a faction.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The planet being observed.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the snapshot is captured.</param>
        public void CaptureSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            _recorder.RecordPlanetSnapshot(faction, planet, system, currentTick);
        }

        /// <summary>
        /// Updates ownership knowledge for each faction that observed a control change.
        /// </summary>
        /// <param name="factions">The factions that observed the ownership change.</param>
        /// <param name="planet">The planet whose owner changed.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the change was observed.</param>
        internal void CaptureOwnershipChange(
            IEnumerable<Faction> factions,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            foreach (Faction faction in factions)
                _recorder.RecordPlanetOwnershipSnapshot(faction, planet, system, currentTick);
        }

        /// <summary>
        /// Removes an entity from all saved planet snapshots for a faction.
        /// </summary>
        /// <param name="faction">The faction whose snapshots are updated.</param>
        /// <param name="entityId">The entity instance ID to remove.</param>
        public void RemoveEntityFromSnapshots(Faction faction, string entityId)
        {
            _recorder.RemoveEntityFromSnapshots(faction, entityId);
        }

        /// <summary>
        /// Applies fog-of-war side effects for a result batch.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        public void ProcessResults(List<GameResult> results)
        {
            foreach (
                GameObjectSabotagedResult result in results.OfType<GameObjectSabotagedResult>()
            )
                RemoveSabotagedObjectFromActorSnapshot(result);
        }

        /// <summary>
        /// Determines if a faction currently has real-time visibility of a planet.
        /// </summary>
        /// <param name="planet">The planet to check visibility for.</param>
        /// <param name="faction">The faction whose visibility to evaluate.</param>
        /// <returns>True if the faction owns the planet or has an arrived fleet with ships present.</returns>
        public bool IsPlanetVisible(Planet planet, Faction faction)
        {
            if (planet.OwnerInstanceID == faction.InstanceID)
                return true;

            if (
                planet.Fleets.Any(f =>
                    f.OwnerInstanceID == faction.InstanceID
                    && f.Movement == null
                    && f.CapitalShips.Count > 0
                )
            )
                return true;

            return false;
        }

        /// <summary>
        /// Builds a faction-specific galaxy view.
        /// Creates new systems and planets. Owned visible entities remain live references;
        /// hidden and snapshotted entities are copied for display.
        /// </summary>
        /// <param name="faction">The faction to build a view for.</param>
        /// <returns>A galaxy map filtered by the faction's fog of war state.</returns>
        public GalaxyMap BuildFactionView(Faction faction)
        {
            GalaxyMap factionView = new GalaxyMap();

            foreach (PlanetSystem masterSystem in _game.Galaxy.PlanetSystems)
            {
                PlanetSystem viewSystem = masterSystem.GetShallowCopy(CloneMode.Full);
                viewSystem.Planets = new List<Planet>();
                ClearParentReferences(viewSystem);
                viewSystem.SetParent(factionView);

                faction.Fog.Snapshots.TryGetValue(
                    masterSystem.InstanceID,
                    out SystemSnapshot systemSnapshot
                );

                foreach (Planet masterPlanet in masterSystem.Planets)
                {
                    PlanetSnapshot planetSnapshot = null;
                    systemSnapshot?.Planets.TryGetValue(
                        masterPlanet.InstanceID,
                        out planetSnapshot
                    );

                    Planet viewPlanet;
                    if (IsPlanetVisible(masterPlanet, faction))
                    {
                        viewPlanet = BlankPlanetView(masterPlanet);
                        ApplyRealTimeView(viewPlanet, masterPlanet, faction, planetSnapshot);
                    }
                    else if (planetSnapshot != null)
                    {
                        viewPlanet = BlankPlanetView(masterPlanet);
                        ApplySnapshotView(
                            viewPlanet,
                            masterPlanet,
                            masterSystem,
                            faction,
                            planetSnapshot
                        );
                    }
                    else
                    {
                        viewPlanet = UnexploredPlanetView(masterPlanet, faction);
                    }

                    viewPlanet.VisitingFactionIDs = masterPlanet.WasVisitedBy(faction.InstanceID)
                        ? new List<string> { faction.InstanceID }
                        : new List<string>();

                    MergeOwnLiveUnits(viewPlanet, masterPlanet, faction);

                    foreach (
                        Mission mission in masterPlanet.Missions.Where(mission =>
                            mission.GetOwnerInstanceID() == faction.InstanceID
                        )
                    )
                    {
                        Mission viewMission = mission.GetShallowCopy(CloneMode.Full);
                        ClearParentReferences(viewMission);
                        viewMission.SetParent(viewPlanet);
                        viewPlanet.Missions.Add(viewMission);
                    }

                    AttachDetachedChildrenToView(viewPlanet);
                    viewPlanet.SetParent(viewSystem);
                    viewSystem.Planets.Add(viewPlanet);
                }

                factionView.PlanetSystems.Add(viewSystem);
            }

            return factionView;
        }

        /// <summary>
        /// Attaches copied child nodes to their faction-view planet.
        /// </summary>
        /// <param name="viewPlanet">The faction-view planet receiving detached children.</param>
        private static void AttachDetachedChildrenToView(Planet viewPlanet)
        {
            foreach (ISceneNode child in viewPlanet.GetChildren())
            {
                if (child.GetParent() == null)
                    child.SetParent(viewPlanet);
            }
        }

        /// <summary>
        /// Returns whether a scene node is owned by a faction.
        /// </summary>
        /// <param name="unit">The scene node to inspect.</param>
        /// <param name="faction">The faction to compare against.</param>
        /// <returns>True if the scene node owner matches the faction.</returns>
        private static bool IsOwnedBy(ISceneNode unit, Faction faction)
        {
            return unit.GetOwnerInstanceID() == faction.InstanceID;
        }

        /// <summary>
        /// Removes a sabotaged object from the actor faction's fog-of-war snapshots.
        /// </summary>
        /// <param name="result">The sabotage result to process.</param>
        private void RemoveSabotagedObjectFromActorSnapshot(GameObjectSabotagedResult result)
        {
            if (result?.SabotagedObject == null || result.Saboteur is not ISceneNode saboteur)
                return;

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(f => f.InstanceID == saboteur.GetOwnerInstanceID());
            if (faction == null)
                return;

            RemoveEntityFromSnapshots(faction, result.SabotagedObject.GetInstanceID());
        }

        /// <summary>
        /// Adds live friendly units to a planet view without duplicating existing entries.
        /// </summary>
        /// <param name="viewPlanet">The planet view being populated.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        private static void MergeOwnLiveUnits(
            Planet viewPlanet,
            Planet masterPlanet,
            Faction faction
        )
        {
            string factionId = faction.InstanceID;

            foreach (Fleet fleet in masterPlanet.Fleets)
                if (
                    fleet.OwnerInstanceID == factionId
                    && fleet.CapitalShips.Count > 0
                    && viewPlanet.Fleets.All(f => f.InstanceID != fleet.InstanceID)
                )
                    viewPlanet.Fleets.Add(fleet);

            foreach (Regiment regiment in masterPlanet.Regiments)
                if (
                    regiment.OwnerInstanceID == factionId
                    && viewPlanet.Regiments.All(r => r.InstanceID != regiment.InstanceID)
                )
                    viewPlanet.Regiments.Add(regiment);

            foreach (SpecialForces specialForces in masterPlanet.SpecialForces)
                if (
                    specialForces.OwnerInstanceID == factionId
                    && viewPlanet.SpecialForces.All(s => s.InstanceID != specialForces.InstanceID)
                )
                    viewPlanet.SpecialForces.Add(specialForces);

            foreach (Starfighter starfighter in masterPlanet.Starfighters)
                if (
                    starfighter.OwnerInstanceID == factionId
                    && viewPlanet.Starfighters.All(s => s.InstanceID != starfighter.InstanceID)
                )
                    viewPlanet.Starfighters.Add(starfighter);

            foreach (Officer officer in masterPlanet.Officers)
                if (
                    officer.OwnerInstanceID == factionId
                    && !officer.IsCaptured
                    && viewPlanet.Officers.All(o => o.InstanceID != officer.InstanceID)
                )
                    viewPlanet.Officers.Add(officer);
        }

        /// <summary>
        /// Selects the visible representation for a unit in a faction view.
        /// </summary>
        /// <typeparam name="T">The scene node type being viewed.</typeparam>
        /// <param name="unit">The source unit.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <returns>The live unit for owned nodes, otherwise a copied view.</returns>
        private static T ViewUnit<T>(T unit, Faction faction)
            where T : class, ISceneNode
        {
            return IsOwnedBy(unit, faction) ? unit : FogOfWarRecorder.CopyEntityForSnapshot(unit);
        }

        /// <summary>
        /// Returns whether a unit is visible without access to manufacturing intelligence.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <param name="faction">The faction whose visibility is being evaluated.</param>
        /// <returns>True for owned units and units no longer under construction.</returns>
        private static bool IsVisibleWithoutManufacturingIntelligence(
            ISceneNode unit,
            Faction faction
        )
        {
            return IsOwnedBy(unit, faction)
                || unit is not IManufacturable manufacturable
                || manufacturable.GetManufacturingStatus() != ManufacturingStatus.Building;
        }

        /// <summary>
        /// Creates a planet view shell with all entity lists cleared, ready to be populated
        /// by one of the three visibility branches.
        /// </summary>
        /// <param name="masterPlanet">The source planet to copy structure from.</param>
        /// <returns>A blank planet view with empty entity lists.</returns>
        private Planet BlankPlanetView(Planet masterPlanet)
        {
            Planet viewPlanet = masterPlanet.GetShallowCopy(CloneMode.Full);
            viewPlanet.Officers = new List<Officer>();
            viewPlanet.Fleets = new List<Fleet>();
            viewPlanet.Regiments = new List<Regiment>();
            viewPlanet.SpecialForces = new List<SpecialForces>();
            viewPlanet.Buildings = new List<Building>();
            viewPlanet.Starfighters = new List<Starfighter>();
            viewPlanet.Missions = new List<Mission>();
            viewPlanet.ManufacturingQueue =
                new Dictionary<ManufacturingType, List<IManufacturable>>();
            viewPlanet.VisitingFactionIDs = new List<string>();
            viewPlanet.PopularSupport = new Dictionary<string, int>();
            ClearParentReferences(viewPlanet);
            return viewPlanet;
        }

        /// <summary>
        /// Populates a view planet from live master state. The faction has direct visibility
        /// because it owns the planet or has a fleet present. Previously observed enemy fleets
        /// remain visible until a later observation invalidates them.
        /// </summary>
        /// <param name="viewPlanet">The view planet to populate.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <param name="planetSnapshot">The prior snapshot for the planet, if any.</param>
        private void ApplyRealTimeView(
            Planet viewPlanet,
            Planet masterPlanet,
            Faction faction,
            PlanetSnapshot planetSnapshot
        )
        {
            viewPlanet.OwnerInstanceID = masterPlanet.OwnerInstanceID;
            viewPlanet.PopularSupport = new Dictionary<string, int>(masterPlanet.PopularSupport);
            viewPlanet.NumRawResourceNodes = masterPlanet.NumRawResourceNodes;

            viewPlanet.Officers.AddRange(
                masterPlanet.Officers.Select(officer =>
                    IsOwnedBy(officer, faction) && !officer.IsCaptured
                        ? officer
                        : FogOfWarRecorder.CopyOfficerForSnapshot(officer)
                )
            );
            viewPlanet.Fleets.AddRange(
                masterPlanet
                    .Fleets.Where(f =>
                        f.CapitalShips.Count > 0
                        && (f.Movement == null || f.OwnerInstanceID == faction.InstanceID)
                    )
                    .Select(f =>
                        IsOwnedBy(f, faction) ? f : FogOfWarRecorder.CopyObservedFleetForSnapshot(f)
                    )
                    .Where(fleet => fleet != null)
            );
            viewPlanet.Regiments.AddRange(
                masterPlanet
                    .Regiments.Where(regiment =>
                        IsVisibleWithoutManufacturingIntelligence(regiment, faction)
                    )
                    .Select(regiment => ViewUnit(regiment, faction))
            );
            viewPlanet.SpecialForces.AddRange(
                masterPlanet
                    .SpecialForces.Where(specialForces =>
                        IsVisibleWithoutManufacturingIntelligence(specialForces, faction)
                    )
                    .Select(specialForces => ViewUnit(specialForces, faction))
            );
            viewPlanet.Starfighters.AddRange(
                masterPlanet
                    .Starfighters.Where(starfighter =>
                        IsVisibleWithoutManufacturingIntelligence(starfighter, faction)
                    )
                    .Select(starfighter => ViewUnit(starfighter, faction))
            );

            viewPlanet.Buildings.AddRange(
                masterPlanet
                    .Buildings.Where(building =>
                        IsVisibleWithoutManufacturingIntelligence(building, faction)
                    )
                    .Select(building => ViewUnit(building, faction))
            );

            if (masterPlanet.OwnerInstanceID == faction.InstanceID)
                viewPlanet.ManufacturingQueue = CopyLiveManufacturingQueue(masterPlanet);
            else
                ApplyManufacturingIntelligence(viewPlanet, planetSnapshot);

            if (planetSnapshot == null)
                return;

            HashSet<string> liveFleetIDs = new HashSet<string>(
                viewPlanet.Fleets.Select(fleet => fleet.InstanceID)
            );
            viewPlanet.Fleets.AddRange(
                planetSnapshot
                    .Fleets.Where(fleet =>
                        fleet.GetOwnerInstanceID() != faction.InstanceID
                        && !liveFleetIDs.Contains(fleet.InstanceID)
                    )
                    .Select(FogOfWarRecorder.CopyFleetForSnapshot)
            );
        }

        /// <summary>
        /// Populates a view planet from the last known snapshot. The faction has no current
        /// visibility but has previously observed this planet. Core system popular support
        /// is always shown regardless of fog state. Captured friendly officers are always live.
        /// </summary>
        /// <param name="viewPlanet">The view planet to populate.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="masterSystem">The authoritative system data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <param name="planetSnapshot">The prior snapshot for the planet.</param>
        private void ApplySnapshotView(
            Planet viewPlanet,
            Planet masterPlanet,
            PlanetSystem masterSystem,
            Faction faction,
            PlanetSnapshot planetSnapshot
        )
        {
            viewPlanet.OwnerInstanceID = planetSnapshot.OwnerInstanceID;
            viewPlanet.IsColonized = planetSnapshot.IsColonized;
            viewPlanet.IsInUprising = planetSnapshot.IsInUprising;
            viewPlanet.IsDestroyed = planetSnapshot.IsDestroyed;
            viewPlanet.IsHeadquarters = planetSnapshot.IsHeadquarters;
            viewPlanet.EnergyCapacity = planetSnapshot.EnergyCapacity;
            viewPlanet.AllocatedEnergy = planetSnapshot.AllocatedEnergy;
            viewPlanet.NumRawResourceNodes = 0;

            viewPlanet.PopularSupport =
                masterSystem.SystemType == PlanetSystemType.CoreSystem
                    ? new Dictionary<string, int>(masterPlanet.PopularSupport)
                    : new Dictionary<string, int>();

            viewPlanet.Officers.AddRange(
                planetSnapshot.Officers.Select(FogOfWarRecorder.CopyOfficerForSnapshot)
            );
            viewPlanet.Officers.AddRange(
                masterPlanet
                    .Officers.Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                    .Select(FogOfWarRecorder.CopyOfficerForSnapshot)
            );
            viewPlanet.Fleets.AddRange(
                planetSnapshot.Fleets.Select(FogOfWarRecorder.CopyFleetForSnapshot)
            );
            viewPlanet.Regiments.AddRange(
                planetSnapshot.Regiments.Select(FogOfWarRecorder.CopyEntityForSnapshot)
            );
            viewPlanet.SpecialForces.AddRange(
                planetSnapshot.SpecialForces.Select(FogOfWarRecorder.CopyEntityForSnapshot)
            );
            viewPlanet.Starfighters.AddRange(
                planetSnapshot.Starfighters.Select(FogOfWarRecorder.CopyEntityForSnapshot)
            );
            viewPlanet.Buildings.AddRange(
                planetSnapshot.Buildings.Select(FogOfWarRecorder.CopyEntityForSnapshot)
            );
            ApplyManufacturingQueue(viewPlanet, planetSnapshot);
        }

        /// <summary>
        /// Copies a planet's live manufacturing queue collections for a faction view.
        /// </summary>
        /// <param name="planet">The planet supplying the live queues.</param>
        /// <returns>A copied queue dictionary containing the live item references.</returns>
        private static Dictionary<
            ManufacturingType,
            List<IManufacturable>
        > CopyLiveManufacturingQueue(Planet planet)
        {
            return planet.ManufacturingQueue.ToDictionary(
                entry => entry.Key,
                entry => new List<IManufacturable>(entry.Value)
            );
        }

        /// <summary>
        /// Applies previously observed unfinished units and queue contents to a planet view.
        /// </summary>
        /// <param name="viewPlanet">The faction-view planet to update.</param>
        /// <param name="snapshot">The snapshot containing manufacturing intelligence.</param>
        private static void ApplyManufacturingIntelligence(
            Planet viewPlanet,
            PlanetSnapshot snapshot
        )
        {
            if (snapshot?.HasManufacturingIntelligence != true)
                return;

            MergeManufacturingEntities(viewPlanet.Regiments, snapshot.Regiments);
            MergeManufacturingEntities(viewPlanet.SpecialForces, snapshot.SpecialForces);
            MergeManufacturingEntities(viewPlanet.Buildings, snapshot.Buildings);
            MergeManufacturingEntities(viewPlanet.Starfighters, snapshot.Starfighters);
            ApplyManufacturingQueue(viewPlanet, snapshot);
        }

        /// <summary>
        /// Adds missing unfinished snapshot entities to a faction-view list.
        /// </summary>
        /// <typeparam name="T">The manufacturable scene-node type.</typeparam>
        /// <param name="destination">The faction-view list receiving copied entities.</param>
        /// <param name="source">The snapshot entities to inspect.</param>
        private static void MergeManufacturingEntities<T>(
            List<T> destination,
            IEnumerable<T> source
        )
            where T : class, IManufacturable
        {
            HashSet<string> existingIds = destination.Select(item => item.InstanceID).ToHashSet();
            foreach (
                T item in source.Where(item =>
                    item.GetManufacturingStatus() == ManufacturingStatus.Building
                )
            )
            {
                if (existingIds.Add(item.InstanceID))
                    destination.Add(FogOfWarRecorder.CopyEntityForSnapshot(item));
            }
        }

        /// <summary>
        /// Rebuilds a faction-view manufacturing queue from observed snapshot items.
        /// </summary>
        /// <param name="planet">The faction-view planet to update.</param>
        /// <param name="snapshot">The snapshot containing observed queue items.</param>
        private static void ApplyManufacturingQueue(Planet planet, PlanetSnapshot snapshot)
        {
            if (snapshot?.HasManufacturingIntelligence != true)
                return;

            planet.ManufacturingQueue = snapshot
                .ManufacturingQueueItems.Select(item =>
                    FogOfWarRecorder.CopyEntityForSnapshot(item) as IManufacturable
                )
                .Where(item => item != null)
                .GroupBy(item => item.GetManufacturingType())
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        /// <summary>
        /// Creates a view planet for a completely unexplored location. No ownership or
        /// entity data is surfaced. Captured friendly officers remain visible.
        /// </summary>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <returns>A planet view containing only the data visible for an unexplored planet.</returns>
        private Planet UnexploredPlanetView(Planet masterPlanet, Faction faction)
        {
            Planet viewPlanet = new Planet
            {
                InstanceID = masterPlanet.InstanceID,
                DisplayName = masterPlanet.DisplayName,
                SystemDataId = masterPlanet.SystemDataId,
                PositionX = masterPlanet.PositionX,
                PositionY = masterPlanet.PositionY,
                PlanetIconPath = masterPlanet.PlanetIconPath,
                IsUnexploredView = true,
            };

            viewPlanet.Officers.AddRange(
                masterPlanet
                    .Officers.Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                    .Select(FogOfWarRecorder.CopyOfficerForSnapshot)
            );

            return viewPlanet;
        }

        /// <summary>
        /// Removes live scene-graph parent references from a copied view node.
        /// </summary>
        /// <param name="node">The copied node to detach.</param>
        private static void ClearParentReferences(ISceneNode node)
        {
            node.ParentInstanceID = null;
            node.LastParentInstanceID = null;
            node.ParentNode = null;
            node.LastParentNode = null;
        }
    }
}
