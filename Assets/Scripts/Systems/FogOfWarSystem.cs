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

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages fog of war operations: capturing snapshots, invalidating moved entities, building faction views.
    /// Operates on faction state, does not hold its own state.
    /// </summary>
    public class FogOfWarSystem : IGameResultHandler<IntelligenceRevealedResult>
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
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick when the snapshot is captured.</param>
        public void CaptureSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            _recorder.RecordPlanetSnapshot(faction, planet, sector, currentTick);
        }

        /// <summary>
        /// Applies category-limited planet intelligence emitted by a simulation event.
        /// </summary>
        /// <param name="results">The intelligence results to record.</param>
        /// <returns>No reactions; snapshots are updated directly.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<IntelligenceRevealedResult> results)
        {
            foreach (IntelligenceRevealedResult result in results)
                _recorder.RecordSelectedObservations(
                    _game,
                    result.Recipient,
                    result.Observations,
                    result.Tick
                );

            return new List<GameResult>();
        }

        /// <summary>
        /// Updates ownership knowledge for each faction that observed a control change.
        /// </summary>
        /// <param name="factions">The factions that observed the ownership change.</param>
        /// <param name="planet">The planet whose owner changed.</param>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick when the change was observed.</param>
        internal void CaptureOwnershipChange(
            IEnumerable<Faction> factions,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            foreach (Faction faction in factions)
                _recorder.RecordPlanetOwnershipSnapshot(faction, planet, sector, currentTick);
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
        public void ProcessResults(IReadOnlyList<GameObjectSabotagedResult> results)
        {
            foreach (GameObjectSabotagedResult result in results)
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
                planet
                    .GetChildren<Fleet>()
                    .Any(f =>
                        f.OwnerInstanceID == faction.InstanceID
                        && f.Movement == null
                        && f.HasOperationalCapitalShips()
                    )
            )
                return true;

            return false;
        }

        /// <summary>
        /// Builds a faction-specific galaxy view.
        /// Creates new sectors and planets. Owned visible entities remain live references;
        /// hidden and snapshotted entities are copied for display.
        /// </summary>
        /// <param name="faction">The faction to build a view for.</param>
        /// <returns>A galaxy map filtered by the faction's fog of war state.</returns>
        public GalaxyMap BuildFactionView(Faction faction)
        {
            GalaxyMap factionView = new GalaxyMap();

            foreach (PlanetSector masterSector in _game.Galaxy.GetChildren<PlanetSector>())
            {
                PlanetSector viewSector = (PlanetSector)masterSector.CreateCopy();
                viewSector.SetPlanets(Enumerable.Empty<Planet>());
                viewSector.SetParent(factionView);

                faction.Fog.Snapshots.TryGetValue(
                    masterSector.InstanceID,
                    out PlanetSectorSnapshot sectorSnapshot
                );

                foreach (Planet masterPlanet in masterSector.GetChildren<Planet>())
                {
                    PlanetSnapshot planetSnapshot = null;
                    sectorSnapshot?.Planets.TryGetValue(
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
                        ApplySnapshotView(viewPlanet, masterPlanet, faction, planetSnapshot);
                    }
                    else
                    {
                        viewPlanet = UnexploredPlanetView(masterPlanet, faction);
                    }

                    viewPlanet.VisitingFactionIDs = masterPlanet.WasVisitedBy(faction.InstanceID)
                        ? new List<string> { faction.InstanceID }
                        : new List<string>();

                    MergeOwnLiveUnits(viewPlanet, masterPlanet, faction);

                    AddObservedMissions(viewPlanet, planetSnapshot);

                    foreach (
                        Mission mission in masterPlanet
                            .GetChildren<Mission>()
                            .Where(mission => mission.GetOwnerInstanceID() == faction.InstanceID)
                    )
                    {
                        Mission viewMission = (Mission)
                            mission.CreateCopy(recursive: true, includeDisabled: true);
                        viewMission.SetParent(viewPlanet);
                        viewPlanet.AddChild(viewMission);
                    }

                    AttachDetachedChildrenToView(viewPlanet);
                    viewPlanet.SetParent(viewSector);
                    viewSector.AddChild(viewPlanet);
                }

                factionView.AddChild(viewSector);
            }

            return factionView;
        }

        /// <summary>
        /// Adds enemy missions revealed by the latest espionage snapshot.
        /// </summary>
        /// <param name="viewPlanet">The faction-view planet receiving mission copies.</param>
        /// <param name="snapshot">The latest intelligence snapshot, if any.</param>
        private static void AddObservedMissions(Planet viewPlanet, PlanetSnapshot snapshot)
        {
            if (snapshot?.RevealedCategories != PlanetIntelligenceCategory.All)
                return;

            foreach (Mission mission in snapshot.Missions)
            {
                Mission viewMission = FogOfWarRecorder.CopyEntityForSnapshot(mission);
                viewMission.SetParent(viewPlanet);
                viewPlanet.AddChild(viewMission);
            }
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
            if (result?.DestroyedObject == null || result.DestroyedBy is not ISceneNode saboteur)
                return;

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(f => f.InstanceID == saboteur.GetOwnerInstanceID());
            if (faction == null)
                return;

            RemoveEntityFromSnapshots(faction, result.DestroyedObject.GetInstanceID());
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
            viewPlanet.SetChildren(
                MergeMissingByInstanceID(
                    viewPlanet.GetChildren<Fleet>(includeDisabled: true),
                    masterPlanet
                        .GetChildren<Fleet>()
                        .Where(fleet =>
                            fleet.OwnerInstanceID == factionId
                            && fleet.GetChildren<CapitalShip>().Count > 0
                        )
                ),
                MergeMissingByInstanceID(
                    viewPlanet.GetChildren<Officer>(includeDisabled: true),
                    masterPlanet
                        .GetChildren<Officer>()
                        .Where(officer =>
                            officer.OwnerInstanceID == factionId && !officer.IsCaptured
                        )
                ),
                MergeMissingByInstanceID(
                    viewPlanet.GetChildren<Regiment>(includeDisabled: true),
                    masterPlanet
                        .GetChildren<Regiment>()
                        .Where(unit => unit.OwnerInstanceID == factionId)
                ),
                MergeMissingByInstanceID(
                    viewPlanet.GetChildren<SpecialForces>(includeDisabled: true),
                    masterPlanet
                        .GetChildren<SpecialForces>()
                        .Where(unit => unit.OwnerInstanceID == factionId)
                ),
                MergeMissingByInstanceID(
                    viewPlanet.GetChildren<Starfighter>(includeDisabled: true),
                    masterPlanet
                        .GetChildren<Starfighter>()
                        .Where(unit => unit.OwnerInstanceID == factionId)
                ),
                viewPlanet.GetChildren<Mission>(includeDisabled: true),
                viewPlanet.GetChildren<Building>(includeDisabled: true)
            );
        }

        /// <summary>
        /// Appends nodes whose instance IDs are not already represented in a projected collection.
        /// </summary>
        private static IEnumerable<T> MergeMissingByInstanceID<T>(
            IEnumerable<T> existing,
            IEnumerable<T> additions
        )
            where T : ISceneNode
        {
            List<T> merged = existing.ToList();
            HashSet<string> instanceIds = merged.Select(node => node.InstanceID).ToHashSet();
            merged.AddRange(additions.Where(node => instanceIds.Add(node.InstanceID)));
            return merged;
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
                || (
                    FogOfWarRecorder.IsObservableAtPlanet(unit, faction.InstanceID)
                    && (
                        unit is not IManufacturable manufacturable
                        || manufacturable.GetManufacturingStatus() != ManufacturingStatus.Building
                    )
                );
        }

        /// <summary>
        /// Creates a planet view shell with all entity lists cleared, ready to be populated
        /// by one of the three visibility branches.
        /// </summary>
        /// <param name="masterPlanet">The source planet to copy structure from.</param>
        /// <returns>A blank planet view with empty entity lists.</returns>
        private Planet BlankPlanetView(Planet masterPlanet)
        {
            Planet viewPlanet = (Planet)masterPlanet.CreateCopy();
            viewPlanet.SetChildren(
                Enumerable.Empty<Fleet>(),
                Enumerable.Empty<Officer>(),
                Enumerable.Empty<Regiment>(),
                Enumerable.Empty<SpecialForces>(),
                Enumerable.Empty<Starfighter>(),
                Enumerable.Empty<Mission>(),
                Enumerable.Empty<Building>()
            );
            viewPlanet.ManufacturingQueue =
                new Dictionary<ManufacturingType, List<IManufacturable>>();
            viewPlanet.VisitingFactionIDs = new List<string>();
            viewPlanet.PopularSupport = new Dictionary<string, int>();
            return viewPlanet;
        }

        /// <summary>
        /// Populates a view planet from live master state. The faction has direct visibility
        /// because it owns the planet or has a fleet present.
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

            IEnumerable<Officer> officers = masterPlanet
                .GetChildren<Officer>()
                .Where(officer =>
                    FogOfWarRecorder.IsObservableAtPlanet(officer, faction.InstanceID)
                )
                .Select(officer =>
                    IsOwnedBy(officer, faction) && !officer.IsCaptured
                        ? officer
                        : FogOfWarRecorder.CopyOfficerForSnapshot(officer)
                );
            IEnumerable<Fleet> fleets = masterPlanet
                .GetChildren<Fleet>()
                .Where(fleet =>
                    fleet.GetChildren<CapitalShip>().Count > 0
                    && FogOfWarRecorder.IsObservableAtPlanet(fleet, faction.InstanceID)
                )
                .Select(fleet =>
                    IsOwnedBy(fleet, faction)
                        ? fleet
                        : FogOfWarRecorder.CopyObservedFleetForSnapshot(fleet, faction.InstanceID)
                )
                .Where(fleet => fleet != null);
            IEnumerable<Regiment> regiments = masterPlanet
                .GetChildren<Regiment>()
                .Where(regiment => IsVisibleWithoutManufacturingIntelligence(regiment, faction))
                .Select(regiment => ViewUnit(regiment, faction));
            IEnumerable<SpecialForces> specialForces = masterPlanet
                .GetChildren<SpecialForces>()
                .Where(unit => IsVisibleWithoutManufacturingIntelligence(unit, faction))
                .Select(unit => ViewUnit(unit, faction));
            IEnumerable<Starfighter> starfighters = masterPlanet
                .GetChildren<Starfighter>()
                .Where(starfighter =>
                    IsVisibleWithoutManufacturingIntelligence(starfighter, faction)
                )
                .Select(starfighter => ViewUnit(starfighter, faction));
            IEnumerable<Building> buildings = masterPlanet
                .GetChildren<Building>()
                .Where(building => IsVisibleWithoutManufacturingIntelligence(building, faction))
                .Select(building => ViewUnit(building, faction));

            viewPlanet.SetChildren(
                fleets,
                officers,
                regiments,
                specialForces,
                starfighters,
                viewPlanet.GetChildren<Mission>(includeDisabled: true),
                buildings
            );

            if (masterPlanet.OwnerInstanceID == faction.InstanceID)
                viewPlanet.ManufacturingQueue = CopyLiveManufacturingQueue(masterPlanet);
            else
                ApplyManufacturingIntelligence(viewPlanet, planetSnapshot);

            ApplyIncomingFleetIntelligence(viewPlanet, planetSnapshot, faction);
        }

        /// <summary>
        /// Populates a view planet from the last known snapshot. The faction has no current
        /// visibility but has previously observed this planet. Captured friendly officers are
        /// always live.
        /// </summary>
        /// <param name="viewPlanet">The view planet to populate.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <param name="planetSnapshot">The prior snapshot for the planet.</param>
        private void ApplySnapshotView(
            Planet viewPlanet,
            Planet masterPlanet,
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
            viewPlanet.NumRawResourceNodes = planetSnapshot.NumRawResourceNodes;

            viewPlanet.PopularSupport = new Dictionary<string, int>(planetSnapshot.PopularSupport);

            IEnumerable<Officer> officers = planetSnapshot
                .Officers.Select(FogOfWarRecorder.CopyOfficerForSnapshot)
                .Concat(
                    masterPlanet
                        .GetChildren<Officer>()
                        .Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                        .Select(FogOfWarRecorder.CopyOfficerForSnapshot)
                );
            viewPlanet.SetChildren(
                planetSnapshot.Fleets.Select(FogOfWarRecorder.CopyFleetForSnapshot),
                officers,
                planetSnapshot.Regiments.Select(FogOfWarRecorder.CopyEntityForSnapshot),
                planetSnapshot.SpecialForces.Select(FogOfWarRecorder.CopyEntityForSnapshot),
                planetSnapshot.Starfighters.Select(FogOfWarRecorder.CopyEntityForSnapshot),
                viewPlanet.GetChildren<Mission>(includeDisabled: true),
                planetSnapshot.Buildings.Select(FogOfWarRecorder.CopyEntityForSnapshot)
            );
            ApplyManufacturingQueue(viewPlanet, planetSnapshot);
        }

        /// <summary>
        /// Returns whether a snapshot reveals one category through full or selective intelligence.
        /// </summary>
        /// <param name="snapshot">The snapshot to inspect.</param>
        /// <param name="category">The requested intelligence category.</param>
        /// <returns>True when the category is available.</returns>
        private static bool HasIntelligence(
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory category
        )
        {
            return snapshot.RevealedCategories.HasFlag(category);
        }

        /// <summary>
        /// Adds incoming enemy fleets revealed by espionage to an otherwise live planet view.
        /// </summary>
        /// <param name="viewPlanet">The faction-view planet receiving known incoming fleets.</param>
        /// <param name="snapshot">The latest intelligence snapshot, if any.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        private static void ApplyIncomingFleetIntelligence(
            Planet viewPlanet,
            PlanetSnapshot snapshot,
            Faction faction
        )
        {
            if (
                snapshot == null
                || !HasIntelligence(snapshot, PlanetIntelligenceCategory.CapitalShips)
            )
                return;

            foreach (
                Fleet fleet in snapshot.Fleets.Where(fleet =>
                    fleet.Movement != null
                    && viewPlanet
                        .GetChildren<Fleet>(includeDisabled: true)
                        .All(current => current.InstanceID != fleet.InstanceID)
                )
            )
            {
                Fleet fleetCopy = FogOfWarRecorder.CopyObservedFleetForSnapshot(
                    fleet,
                    faction.InstanceID,
                    includeManufacturing: true,
                    includeInTransit: true
                );
                if (fleetCopy != null)
                    viewPlanet.AddChild(fleetCopy);
            }
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

            FogOfWarRecorder.MergeManufacturingEntities(viewPlanet, snapshot.Regiments);
            FogOfWarRecorder.MergeManufacturingEntities(viewPlanet, snapshot.SpecialForces);
            FogOfWarRecorder.MergeManufacturingEntities(viewPlanet, snapshot.Buildings);
            FogOfWarRecorder.MergeManufacturingEntities(viewPlanet, snapshot.Starfighters);
            ApplyManufacturingQueue(viewPlanet, snapshot);
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
                PlanetDataId = masterPlanet.PlanetDataId,
                PositionX = masterPlanet.PositionX,
                PositionY = masterPlanet.PositionY,
                PlanetIconPath = masterPlanet.PlanetIconPath,
                IsUnexploredView = true,
            };

            viewPlanet.SetChildren(
                Enumerable.Empty<Fleet>(),
                masterPlanet
                    .GetChildren<Officer>()
                    .Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                    .Select(FogOfWarRecorder.CopyOfficerForSnapshot),
                Enumerable.Empty<Regiment>(),
                Enumerable.Empty<SpecialForces>(),
                Enumerable.Empty<Starfighter>(),
                Enumerable.Empty<Mission>(),
                Enumerable.Empty<Building>()
            );

            return viewPlanet;
        }
    }
}
