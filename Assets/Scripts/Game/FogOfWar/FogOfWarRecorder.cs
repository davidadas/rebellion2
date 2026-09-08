using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Game.FogOfWar
{
    /// <summary>
    /// Records faction views of observed planets.
    /// </summary>
    public sealed class FogOfWarRecorder
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public FogOfWarRecorder() { }

        /// <summary>
        /// Records the current state of a planet for a faction.
        /// </summary>
        /// <param name="faction">The faction receiving the observation.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        public void RecordPlanetSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            RecordPlanetSnapshot(faction, planet, sector, currentTick, false);
        }

        /// <summary>
        /// Records the complete planet intelligence revealed by successful espionage.
        /// </summary>
        /// <param name="faction">The faction receiving the intelligence.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        public void RecordEspionageSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            RecordPlanetSnapshot(faction, planet, sector, currentTick, true);
        }

        /// <summary>
        /// Records current information about explicitly selected scene objects.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="faction">The faction receiving the observations.</param>
        /// <param name="observations">The scene objects to record.</param>
        /// <param name="currentTick">The tick when the objects were observed.</param>
        public void RecordSelectedObservations(
            GameRoot game,
            Faction faction,
            IEnumerable<ISceneNode> observations,
            int currentTick
        )
        {
            if (game == null || faction == null || observations == null)
                return;

            foreach (ISceneNode observation in observations.Where(node => node != null).Distinct())
            {
                if (observation is PlanetSector selectedSector)
                {
                    foreach (Planet planet in selectedSector.GetChildren<Planet>())
                        RecordObservation(game, faction, planet, currentTick);
                    continue;
                }

                RecordObservation(game, faction, observation, currentTick);
            }
        }

        /// <summary>
        /// Records one selected object in the snapshot for its containing or producing planet.
        /// </summary>
        private void RecordObservation(
            GameRoot game,
            Faction faction,
            ISceneNode observation,
            int currentTick
        )
        {
            Planet planet = GetContainingOrProducingPlanet(game, observation);
            PlanetSector sector = planet?.GetParentOfType<PlanetSector>();
            if (planet == null || sector == null)
                return;

            PlanetSnapshot snapshot = GetOrCreateObservedPlanetSnapshot(
                faction,
                planet,
                sector,
                currentTick
            );
            if (observation is Planet)
            {
                UpdatePlanetState(snapshot, planet, currentTick);
                return;
            }

            if (!string.IsNullOrEmpty(observation.InstanceID))
            {
                RemoveEntityFromSnapshotState(faction, observation.InstanceID);
                InvalidateEntityFromOtherSnapshots(
                    faction,
                    observation.InstanceID,
                    planet.InstanceID
                );
            }

            if (RecordManufacturingObservation(snapshot, planet, observation))
                return;

            RecordEntityObservation(snapshot, observation);
        }

        /// <summary>
        /// Records a selected entity using the snapshot collection appropriate to its type.
        /// </summary>
        private static void RecordEntityObservation(PlanetSnapshot snapshot, ISceneNode observation)
        {
            switch (observation)
            {
                case Officer officer:
                    RecordCarriedUnitSnapshot(snapshot, officer, CopyOfficerForSnapshot(officer));
                    break;
                case Fleet fleet:
                    Upsert(snapshot.Fleets, CopyFleetForSnapshot(fleet));
                    break;
                case CapitalShip capitalShip:
                    AddCapitalShipObservation(snapshot, capitalShip);
                    break;
                case Regiment regiment:
                    RecordCarriedUnitSnapshot(snapshot, regiment, CopyEntityForSnapshot(regiment));
                    break;
                case SpecialForces specialForces:
                    RecordCarriedUnitSnapshot(
                        snapshot,
                        specialForces,
                        CopyEntityForSnapshot(specialForces)
                    );
                    break;
                case Starfighter starfighter:
                    RecordCarriedUnitSnapshot(
                        snapshot,
                        starfighter,
                        CopyEntityForSnapshot(starfighter)
                    );
                    break;
                case Building building:
                    Upsert(snapshot.Buildings, CopyEntityForSnapshot(building));
                    break;
                case Mission mission:
                    Upsert(snapshot.Missions, CopyEntityForSnapshot(mission));
                    break;
            }
        }

        /// <summary>
        /// Gets the planet snapshot that receives one selected observation.
        /// </summary>
        private PlanetSnapshot GetOrCreateObservedPlanetSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            planet.AddVisitor(faction.InstanceID);
            PlanetSectorSnapshot sectorSnapshot = GetOrCreatePlanetSectorSnapshot(faction, sector);
            faction.Fog.PlanetToSector[planet.InstanceID] = sector.InstanceID;
            if (!sectorSnapshot.Planets.TryGetValue(planet.InstanceID, out PlanetSnapshot snapshot))
            {
                snapshot = new PlanetSnapshot();
                sectorSnapshot.Planets[planet.InstanceID] = snapshot;
            }

            snapshot.TickCaptured = currentTick;
            return snapshot;
        }

        /// <summary>
        /// Records a selected item from the planet's active manufacturing queue.
        /// </summary>
        private static bool RecordManufacturingObservation(
            PlanetSnapshot snapshot,
            Planet planet,
            ISceneNode observation
        )
        {
            if (
                observation is not IManufacturable queuedItem
                || queuedItem.ManufacturingStatus != ManufacturingStatus.Building
                || !planet.ManufacturingQueue.Values.Any(queue => queue.Contains(queuedItem))
            )
                return false;

            snapshot.HasManufacturingIntelligence = true;
            Upsert(snapshot.ManufacturingQueueItems, CopyManufacturableForSnapshot(queuedItem));
            return true;
        }

        /// <summary>
        /// Gets the planet containing an active object or producing an object under construction.
        /// </summary>
        private static Planet GetContainingOrProducingPlanet(GameRoot game, ISceneNode observation)
        {
            if (observation is Planet planet)
                return planet;

            Planet ancestor = observation.GetParentOfType<Planet>();
            if (ancestor != null)
                return ancestor;

            return
                observation is IManufacturable manufacturable
                && !string.IsNullOrEmpty(manufacturable.ProducerPlanetID)
                ? game.GetSceneNodeByInstanceID<Planet>(manufacturable.ProducerPlanetID)
                : null;
        }

        /// <summary>
        /// Records one capital ship without exposing any unselected embarked units.
        /// </summary>
        private static void AddCapitalShipObservation(
            PlanetSnapshot snapshot,
            CapitalShip capitalShip
        )
        {
            Fleet sourceFleet = capitalShip.GetParentOfType<Fleet>();
            if (sourceFleet == null)
                return;

            Fleet fleet = GetOrCreatePartialFleetSnapshot(snapshot, sourceFleet);
            CapitalShip copy = CopyCapitalShipForSnapshot(capitalShip);
            RemoveUnobservedCargo(copy);
            UpsertChild(fleet, copy);
            copy.SetParent(fleet);
        }

        /// <summary>
        /// Records a unit directly at a planet or within a partial ship and fleet snapshot.
        /// </summary>
        private static void RecordCarriedUnitSnapshot<T>(PlanetSnapshot snapshot, T source, T copy)
            where T : class, ISceneNode
        {
            CapitalShip sourceShip = source.GetParentOfType<CapitalShip>();
            if (sourceShip != null)
            {
                Fleet sourceFleet = sourceShip.GetParentOfType<Fleet>();
                if (sourceFleet == null)
                    return;

                Fleet fleet = GetOrCreatePartialFleetSnapshot(snapshot, sourceFleet);
                CapitalShip ship = GetOrCreatePartialCapitalShipSnapshot(fleet, sourceShip);
                AddCapitalShipChild(ship, copy);
                return;
            }

            if (copy is Officer officer)
                Upsert(snapshot.Officers, officer);
            else if (copy is Regiment regiment)
                Upsert(snapshot.Regiments, regiment);
            else if (copy is SpecialForces specialForces)
                Upsert(snapshot.SpecialForces, specialForces);
            else if (copy is Starfighter starfighter)
                Upsert(snapshot.Starfighters, starfighter);
        }

        /// <summary>
        /// Gets a detached fleet snapshot containing only explicitly observed ships and cargo.
        /// </summary>
        private static Fleet GetOrCreatePartialFleetSnapshot(PlanetSnapshot snapshot, Fleet source)
        {
            Fleet existing = snapshot.Fleets.FirstOrDefault(fleet =>
                fleet.InstanceID == source.InstanceID
            );
            if (existing != null)
                return existing;

            Fleet partialSnapshot = CopyFleetForSnapshot(source);
            partialSnapshot.RemoveAllChildren();
            snapshot.Fleets.Add(partialSnapshot);
            return partialSnapshot;
        }

        /// <summary>
        /// Gets a detached ship snapshot containing only explicitly observed cargo.
        /// </summary>
        private static CapitalShip GetOrCreatePartialCapitalShipSnapshot(
            Fleet fleet,
            CapitalShip source
        )
        {
            CapitalShip existing = fleet
                .GetChildren<CapitalShip>()
                .FirstOrDefault(ship => ship.InstanceID == source.InstanceID);
            if (existing != null)
                return existing;

            CapitalShip partialSnapshot = CopyCapitalShipForSnapshot(source);
            RemoveUnobservedCargo(partialSnapshot);
            fleet.AddChild(partialSnapshot);
            partialSnapshot.SetParent(fleet);
            return partialSnapshot;
        }

        /// <summary>
        /// Removes copied cargo that was not part of the current intelligence selection.
        /// </summary>
        private static void RemoveUnobservedCargo(CapitalShip ship)
        {
            ship.RemoveAllChildren();
        }

        /// <summary>
        /// Adds one observed carried unit to the matching collection on a detached ship snapshot.
        /// </summary>
        private static void AddCapitalShipChild(CapitalShip ship, ISceneNode child)
        {
            UpsertChild(ship, child);

            child.SetParent(ship);
        }

        /// <summary>
        /// Replaces a prior snapshot with the same instance ID or appends a newly observed item.
        /// </summary>
        private static void Upsert<T>(List<T> items, T item)
            where T : class, ISceneNode
        {
            items.RemoveAll(existing => existing.InstanceID == item.InstanceID);
            items.Add(item);
        }

        /// <summary>
        /// Replaces a matching detached child snapshot or adds a newly observed child.
        /// </summary>
        private static void UpsertChild<T>(ContainerNode container, T item)
            where T : class, ISceneNode
        {
            T existing = container
                .GetChildren(includeDisabled: true)
                .OfType<T>()
                .FirstOrDefault(child => child.InstanceID == item.InstanceID);
            if (existing != null)
                container.RemoveChild(existing);
            container.AddChild(item);
        }

        /// <summary>
        /// Records current intelligence for only the requested planet categories.
        /// </summary>
        /// <param name="faction">The faction receiving intelligence.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="sector">The planet's containing sector.</param>
        /// <param name="currentTick">The observation tick.</param>
        /// <param name="categories">The categories revealed by this observation.</param>
        public void RecordIntelligenceSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick,
            PlanetIntelligenceCategory categories
        )
        {
            if (
                faction == null
                || planet == null
                || sector == null
                || categories == PlanetIntelligenceCategory.None
            )
                return;

            planet.AddVisitor(faction.InstanceID);
            PlanetSectorSnapshot sectorSnapshot = GetOrCreatePlanetSectorSnapshot(faction, sector);
            faction.Fog.PlanetToSector[planet.InstanceID] = sector.InstanceID;
            PlanetSnapshot snapshot = GetOrCreatePlanetSnapshot(
                sectorSnapshot,
                planet,
                currentTick
            );
            UpdateSelectedIntelligence(faction, planet, snapshot, currentTick, categories);

            ReconcileEntityLocations(faction, planet.InstanceID, snapshot);
        }

        /// <summary>Returns the existing planet snapshot or creates its initial strategic state.</summary>
        private static PlanetSnapshot GetOrCreatePlanetSnapshot(
            PlanetSectorSnapshot sectorSnapshot,
            Planet planet,
            int currentTick
        )
        {
            if (sectorSnapshot.Planets.TryGetValue(planet.InstanceID, out PlanetSnapshot snapshot))
                return snapshot;

            snapshot = new PlanetSnapshot
            {
                TickCaptured = currentTick,
                OwnerInstanceID = planet.OwnerInstanceID,
                IsColonized = planet.IsColonized,
                IsDestroyed = planet.IsDestroyed,
            };
            sectorSnapshot.Planets[planet.InstanceID] = snapshot;
            return snapshot;
        }

        /// <summary>Refreshes only the intelligence categories revealed by this observation.</summary>
        private void UpdateSelectedIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            int currentTick,
            PlanetIntelligenceCategory categories
        )
        {
            snapshot.TickCaptured = currentTick;
            PlanetIntelligenceCategory accumulatedCategories =
                snapshot.RevealedCategories | categories;
            if (categories.HasFlag(PlanetIntelligenceCategory.Planet))
                UpdatePlanetState(snapshot, planet, currentTick);
            snapshot.RevealedCategories = accumulatedCategories;
            UpdateOfficerIntelligence(faction, planet, snapshot, categories);
            UpdateFleetIntelligence(faction, planet, snapshot, categories, accumulatedCategories);
            UpdateGroundForceIntelligence(faction, planet, snapshot, categories);
            UpdateStarfighterIntelligence(faction, planet, snapshot, categories);
            UpdateBuildingIntelligence(faction, planet, snapshot, categories);
            UpdateMissionIntelligence(faction, planet, snapshot, categories);
        }

        /// <summary>
        /// Replaces the directly stationed officer intelligence when that category is revealed.
        /// </summary>
        private static void UpdateOfficerIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory categories
        )
        {
            if (!categories.HasFlag(PlanetIntelligenceCategory.Officers))
                return;

            snapshot.Officers.Clear();
            snapshot.Officers.AddRange(
                planet
                    .GetChildren<Officer>(recursive: true)
                    .Where(officer =>
                        officer.OwnerInstanceID != faction.InstanceID || officer.IsCaptured
                    )
                    .Select(CopyOfficerForSnapshot)
            );
        }

        /// <summary>
        /// Rebuilds fleet intelligence and removes cargo categories that remain hidden.
        /// </summary>
        private void UpdateFleetIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory categories,
            PlanetIntelligenceCategory accumulatedCategories
        )
        {
            PlanetIntelligenceCategory fleetCategories =
                PlanetIntelligenceCategory.CapitalShips
                | PlanetIntelligenceCategory.Officers
                | PlanetIntelligenceCategory.GroundForces
                | PlanetIntelligenceCategory.Starfighters;
            if (
                !accumulatedCategories.HasFlag(PlanetIntelligenceCategory.CapitalShips)
                || (categories & fleetCategories) == PlanetIntelligenceCategory.None
            )
                return;

            snapshot.Fleets.Clear();
            AddFleetsToSnapshot(faction, planet, snapshot, true);
            FilterFleetIntelligence(snapshot.Fleets, accumulatedCategories);
        }

        /// <summary>
        /// Replaces stationed regiment and special-forces intelligence when revealed.
        /// </summary>
        private void UpdateGroundForceIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory categories
        )
        {
            if (!categories.HasFlag(PlanetIntelligenceCategory.GroundForces))
                return;

            snapshot.Regiments.Clear();
            snapshot.SpecialForces.Clear();
            AddIntelligenceEntityCopies(
                planet.GetChildren<Regiment>(recursive: true),
                snapshot.Regiments,
                faction
            );
            AddEntityCopiesToSnapshot(
                planet.GetChildren<SpecialForces>(recursive: true),
                snapshot.SpecialForces,
                faction,
                true
            );
        }

        /// <summary>
        /// Replaces stationed starfighter intelligence when revealed.
        /// </summary>
        private void UpdateStarfighterIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory categories
        )
        {
            if (!categories.HasFlag(PlanetIntelligenceCategory.Starfighters))
                return;

            snapshot.Starfighters.Clear();
            AddIntelligenceEntityCopies(
                planet.GetChildren<Starfighter>(recursive: true),
                snapshot.Starfighters,
                faction
            );
        }

        /// <summary>
        /// Replaces building intelligence when revealed.
        /// </summary>
        private void UpdateBuildingIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory categories
        )
        {
            if (!categories.HasFlag(PlanetIntelligenceCategory.Buildings))
                return;

            snapshot.Buildings.Clear();
            AddEntityCopiesToSnapshot(
                planet.GetChildren<Building>(),
                snapshot.Buildings,
                faction,
                true
            );
        }

        /// <summary>
        /// Replaces enemy mission intelligence when revealed.
        /// </summary>
        private void UpdateMissionIntelligence(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            PlanetIntelligenceCategory categories
        )
        {
            if (!categories.HasFlag(PlanetIntelligenceCategory.Missions))
                return;

            snapshot.Missions.Clear();
            AddEnemyMissionsToSnapshot(faction, planet, snapshot);
        }

        /// <summary>
        /// Adds fully revealed entity copies to a category-limited snapshot.
        /// </summary>
        /// <typeparam name="T">The scene-node type being copied.</typeparam>
        /// <param name="source">The live entities.</param>
        /// <param name="destination">The snapshot collection.</param>
        /// <param name="faction">The observing faction.</param>
        private void AddIntelligenceEntityCopies<T>(
            IEnumerable<T> source,
            List<T> destination,
            Faction faction
        )
            where T : class, ISceneNode
        {
            AddEntityCopiesToSnapshot(source, destination, faction, true);
        }

        /// <summary>
        /// Removes fleet cargo categories not included by an intelligence result.
        /// </summary>
        /// <param name="fleets">The copied fleets to filter.</param>
        /// <param name="categories">The revealed intelligence categories.</param>
        private static void FilterFleetIntelligence(
            IEnumerable<Fleet> fleets,
            PlanetIntelligenceCategory categories
        )
        {
            foreach (
                CapitalShip ship in fleets.SelectMany(fleet => fleet.GetChildren<CapitalShip>())
            )
            {
                if (!categories.HasFlag(PlanetIntelligenceCategory.Officers))
                    ship.RemoveChildren<Officer>(_ => true);
                if (!categories.HasFlag(PlanetIntelligenceCategory.GroundForces))
                {
                    ship.RemoveChildren<Regiment>(_ => true);
                    ship.RemoveChildren<SpecialForces>(_ => true);
                }
                if (!categories.HasFlag(PlanetIntelligenceCategory.Starfighters))
                    ship.RemoveChildren<Starfighter>(_ => true);
            }
        }

        /// <summary>
        /// Updates a faction's recorded owner for a planet without revealing other current state.
        /// </summary>
        /// <param name="faction">The faction receiving the ownership observation.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick used when a new planet snapshot is required.</param>
        public void RecordPlanetOwnershipSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            if (faction == null || planet == null || sector == null)
                return;

            PlanetSectorSnapshot sectorSnapshot = GetOrCreatePlanetSectorSnapshot(faction, sector);
            faction.Fog.PlanetToSector[planet.InstanceID] = sector.InstanceID;
            if (!sectorSnapshot.Planets.TryGetValue(planet.InstanceID, out PlanetSnapshot snapshot))
            {
                snapshot = new PlanetSnapshot
                {
                    TickCaptured = currentTick,
                    IsColonized = planet.IsColonized,
                    IsDestroyed = planet.IsDestroyed,
                };
                sectorSnapshot.Planets[planet.InstanceID] = snapshot;
            }

            snapshot.OwnerInstanceID = planet.OwnerInstanceID;
        }

        /// <summary>
        /// Replaces a faction's planet snapshot with the currently observable state.
        /// </summary>
        /// <param name="faction">The faction receiving the observation.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        /// <param name="includeEspionageIntelligence">Whether complete espionage intelligence is observable.</param>
        private void RecordPlanetSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick,
            bool includeEspionageIntelligence
        )
        {
            if (faction == null || planet == null || sector == null)
                return;

            planet.AddVisitor(faction.InstanceID);

            PlanetSectorSnapshot sectorSnapshot = GetOrCreatePlanetSectorSnapshot(faction, sector);
            faction.Fog.PlanetToSector[planet.InstanceID] = sector.InstanceID;
            sectorSnapshot.Planets.TryGetValue(
                planet.InstanceID,
                out PlanetSnapshot previousSnapshot
            );

            PlanetSnapshot planetSnapshot = CreatePlanetSnapshot(planet, currentTick);
            AddOfficersToSnapshot(faction, planet, planetSnapshot, includeEspionageIntelligence);
            AddFleetsToSnapshot(faction, planet, planetSnapshot, includeEspionageIntelligence);
            AddEntityCopiesToSnapshot(
                planet.GetChildren<Regiment>(),
                planetSnapshot.Regiments,
                faction,
                includeEspionageIntelligence
            );
            AddEntityCopiesToSnapshot(
                planet.GetChildren<SpecialForces>(),
                planetSnapshot.SpecialForces,
                faction,
                includeEspionageIntelligence
            );
            AddEntityCopiesToSnapshot(
                planet.GetChildren<Building>(),
                planetSnapshot.Buildings,
                faction,
                includeEspionageIntelligence
            );
            AddEntityCopiesToSnapshot(
                planet.GetChildren<Starfighter>(),
                planetSnapshot.Starfighters,
                faction,
                includeEspionageIntelligence
            );

            if (includeEspionageIntelligence)
            {
                planetSnapshot.RevealedCategories = PlanetIntelligenceCategory.All;
                AddEnemyMissionsToSnapshot(faction, planet, planetSnapshot);
                AddManufacturingQueueToSnapshot(planet, planetSnapshot);
            }
            else
            {
                planetSnapshot.RevealedCategories =
                    previousSnapshot?.RevealedCategories ?? PlanetIntelligenceCategory.None;
                CarryForwardRevealedMissions(previousSnapshot, planetSnapshot);
                PreserveIncomingFleetIntelligence(previousSnapshot, planetSnapshot);
                PreserveManufacturingIntelligence(previousSnapshot, planetSnapshot, planet);
            }

            ReconcileEntityLocations(faction, planet.InstanceID, planetSnapshot);
            sectorSnapshot.Planets[planet.InstanceID] = planetSnapshot;
        }

        /// <summary>
        /// Records enemy missions exposed by a successful espionage observation.
        /// </summary>
        /// <param name="faction">The faction receiving the intelligence.</param>
        /// <param name="planet">The planet whose missions were observed.</param>
        /// <param name="snapshot">The snapshot receiving mission copies.</param>
        private static void AddEnemyMissionsToSnapshot(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot
        )
        {
            snapshot.Missions.AddRange(
                planet
                    .GetChildren<Mission>()
                    .Where(mission => mission.GetOwnerInstanceID() != faction.InstanceID)
                    .Select(mission => CopyEntityForSnapshot(mission))
            );
        }

        /// <summary>
        /// Retains previously gathered mission intelligence during an ordinary observation.
        /// </summary>
        /// <param name="previousSnapshot">The prior intelligence snapshot.</param>
        /// <param name="snapshot">The replacement snapshot.</param>
        private static void CarryForwardRevealedMissions(
            PlanetSnapshot previousSnapshot,
            PlanetSnapshot snapshot
        )
        {
            if (
                previousSnapshot?.RevealedCategories.HasFlag(PlanetIntelligenceCategory.Missions)
                != true
            )
                return;

            snapshot.Missions.AddRange(
                previousSnapshot.Missions.Select(mission => CopyEntityForSnapshot(mission))
            );
        }

        /// <summary>
        /// Retains incoming fleets learned through espionage during an ordinary observation.
        /// </summary>
        /// <param name="previousSnapshot">The prior intelligence snapshot.</param>
        /// <param name="snapshot">The replacement snapshot.</param>
        private static void PreserveIncomingFleetIntelligence(
            PlanetSnapshot previousSnapshot,
            PlanetSnapshot snapshot
        )
        {
            if (
                previousSnapshot?.RevealedCategories.HasFlag(
                    PlanetIntelligenceCategory.CapitalShips
                ) != true
            )
                return;

            foreach (
                Fleet fleet in previousSnapshot.Fleets.Where(fleet =>
                    fleet.Movement != null
                    && snapshot.Fleets.All(current => current.InstanceID != fleet.InstanceID)
                )
            )
            {
                snapshot.Fleets.Add(CopyFleetForSnapshot(fleet));
            }
        }

        /// <summary>
        /// Reconciles the entity-location index with every entity represented by a new snapshot.
        /// </summary>
        /// <param name="faction">The faction whose fog state is being updated.</param>
        /// <param name="planetId">The observed planet instance ID.</param>
        /// <param name="snapshot">The replacement snapshot for the observed planet.</param>
        private void ReconcileEntityLocations(
            Faction faction,
            string planetId,
            PlanetSnapshot snapshot
        )
        {
            HashSet<string> snapshotEntityIds = GetSnapshotEntityIDs(snapshot);
            List<string> absentEntityIds = faction
                .Fog.EntityLastSeenAt.Where(entry =>
                    entry.Value == planetId && !snapshotEntityIds.Contains(entry.Key)
                )
                .Select(entry => entry.Key)
                .ToList();

            foreach (string entityId in absentEntityIds)
                faction.Fog.EntityLastSeenAt.Remove(entityId);

            foreach (string entityId in snapshotEntityIds)
                InvalidateEntityFromOtherSnapshots(faction, entityId, planetId);
        }

        /// <summary>
        /// Collects every entity instance ID represented by a planet snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to inspect.</param>
        /// <returns>The represented entity instance IDs.</returns>
        private static HashSet<string> GetSnapshotEntityIDs(PlanetSnapshot snapshot)
        {
            IEnumerable<ISceneNode> fleetEntities = snapshot.Fleets.SelectMany(fleet =>
                fleet.GetChildren<ISceneNode>(recursive: true).Prepend(fleet)
            );
            return snapshot
                .Officers.Cast<ISceneNode>()
                .Concat(fleetEntities)
                .Concat(snapshot.Regiments)
                .Concat(snapshot.SpecialForces)
                .Concat(snapshot.Buildings)
                .Concat(snapshot.Starfighters)
                .Concat(snapshot.ManufacturingQueueItems.OfType<ISceneNode>())
                .Select(entity => entity.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
        }

        /// <summary>
        /// Removes an entity from all saved planet snapshots for a faction.
        /// </summary>
        /// <param name="faction">The faction whose snapshots are updated.</param>
        /// <param name="entityId">The entity instance ID to remove.</param>
        public void RemoveEntityFromSnapshots(Faction faction, string entityId)
        {
            if (faction == null || string.IsNullOrEmpty(entityId))
                return;

            foreach (PlanetSectorSnapshot sectorSnapshot in faction.Fog.Snapshots.Values)
            {
                foreach (PlanetSnapshot planetSnapshot in sectorSnapshot.Planets.Values)
                    RemoveEntityFromSnapshot(planetSnapshot, entityId);
            }

            faction.Fog.EntityLastSeenAt.Remove(entityId);
        }

        /// <summary>
        /// Returns the sector snapshot for a faction, creating it when needed.
        /// </summary>
        /// <param name="faction">The faction that owns the fog state.</param>
        /// <param name="sector">The sector being observed.</param>
        /// <returns>The snapshot for the observed sector.</returns>
        private PlanetSectorSnapshot GetOrCreatePlanetSectorSnapshot(
            Faction faction,
            PlanetSector sector
        )
        {
            if (
                !faction.Fog.Snapshots.TryGetValue(
                    sector.InstanceID,
                    out PlanetSectorSnapshot snapshot
                )
            )
            {
                snapshot = new PlanetSectorSnapshot();
                faction.Fog.Snapshots[sector.InstanceID] = snapshot;
            }

            return snapshot;
        }

        /// <summary>
        /// Creates a planet snapshot from the current planet state.
        /// </summary>
        /// <param name="planet">The observed planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        /// <returns>The planet snapshot.</returns>
        private PlanetSnapshot CreatePlanetSnapshot(Planet planet, int currentTick)
        {
            return new PlanetSnapshot
            {
                TickCaptured = currentTick,
                OwnerInstanceID = planet.OwnerInstanceID,
                IsColonized = planet.IsColonized,
                IsInUprising = planet.IsInUprising,
                IsDestroyed = planet.IsDestroyed,
                IsHeadquarters = planet.IsHeadquarters,
                NumRawResourceNodes = planet.NumRawResourceNodes,
                EnergyCapacity = planet.EnergyCapacity,
                AllocatedEnergy = planet.AllocatedEnergy,
                PopularSupport = new Dictionary<string, int>(planet.PopularSupport),
            };
        }

        /// <summary>
        /// Copies current strategic planet state into an existing snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to update.</param>
        /// <param name="planet">The live planet state.</param>
        /// <param name="currentTick">The observation tick.</param>
        private static void UpdatePlanetState(
            PlanetSnapshot snapshot,
            Planet planet,
            int currentTick
        )
        {
            snapshot.TickCaptured = currentTick;
            snapshot.OwnerInstanceID = planet.OwnerInstanceID;
            snapshot.IsColonized = planet.IsColonized;
            snapshot.IsInUprising = planet.IsInUprising;
            snapshot.IsDestroyed = planet.IsDestroyed;
            snapshot.IsHeadquarters = planet.IsHeadquarters;
            snapshot.NumRawResourceNodes = planet.NumRawResourceNodes;
            snapshot.EnergyCapacity = planet.EnergyCapacity;
            snapshot.AllocatedEnergy = planet.AllocatedEnergy;
            snapshot.PopularSupport = new Dictionary<string, int>(planet.PopularSupport);
        }

        /// <summary>
        /// Adds visible officers to a planet snapshot.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="snapshot">The snapshot being populated.</param>
        /// <param name="includeInTransit">Whether moving officers should be included.</param>
        private void AddOfficersToSnapshot(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            bool includeInTransit
        )
        {
            foreach (Officer officer in planet.GetChildren<Officer>())
            {
                if (officer.OwnerInstanceID == faction.InstanceID && !officer.IsCaptured)
                {
                    RemoveEntityFromSnapshotState(faction, officer.InstanceID);
                    continue;
                }

                if (!includeInTransit && !IsObservableAtPlanet(officer, faction.InstanceID))
                    continue;

                snapshot.Officers.Add(CopyOfficerForSnapshot(officer));
            }
        }

        /// <summary>
        /// Adds visible fleets to a planet snapshot.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="snapshot">The snapshot being populated.</param>
        /// <param name="includeEspionageIntelligence">Whether hidden intelligence should be included.</param>
        private void AddFleetsToSnapshot(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            bool includeEspionageIntelligence
        )
        {
            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                if (fleet.GetChildren<CapitalShip>().Count == 0)
                    continue;
                if (fleet.OwnerInstanceID == faction.InstanceID)
                {
                    RemoveEntityFromSnapshotState(faction, fleet.InstanceID);
                    continue;
                }

                if (
                    !includeEspionageIntelligence
                    && !IsObservableAtPlanet(fleet, faction.InstanceID)
                )
                    continue;

                Fleet fleetCopy = CopyObservedFleetForSnapshot(
                    fleet,
                    faction.InstanceID,
                    includeEspionageIntelligence,
                    includeEspionageIntelligence
                );
                if (fleetCopy == null)
                    continue;

                snapshot.Fleets.Add(fleetCopy);
            }
        }

        /// <summary>
        /// Adds visible entities to a planet snapshot list.
        /// </summary>
        /// <typeparam name="T">The entity type to copy.</typeparam>
        /// <param name="source">The live entities to copy.</param>
        /// <param name="destination">The snapshot list to populate.</param>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="includeEspionageIntelligence">Whether hidden intelligence should be included.</param>
        private void AddEntityCopiesToSnapshot<T>(
            IEnumerable<T> source,
            List<T> destination,
            Faction faction,
            bool includeEspionageIntelligence
        )
            where T : class, ISceneNode
        {
            foreach (T entity in source)
            {
                if (entity.GetOwnerInstanceID() == faction.InstanceID)
                {
                    RemoveEntityFromSnapshotState(faction, entity.InstanceID);
                    continue;
                }

                if (
                    !includeEspionageIntelligence
                    && !IsObservableAtPlanet(entity, faction.InstanceID)
                )
                    continue;

                if (
                    !includeEspionageIntelligence
                    && entity is IManufacturable manufacturable
                    && IsManufacturingInProgress(manufacturable)
                )
                    continue;

                destination.Add(CopyEntityForSnapshot(entity));
            }
        }

        /// <summary>
        /// Copies the planet's current manufacturing queues into a snapshot.
        /// </summary>
        /// <param name="planet">The planet supplying queue contents.</param>
        /// <param name="snapshot">The snapshot receiving the copied queue contents.</param>
        private static void AddManufacturingQueueToSnapshot(Planet planet, PlanetSnapshot snapshot)
        {
            snapshot.HasManufacturingIntelligence = true;
            foreach (List<IManufacturable> queue in planet.ManufacturingQueue.Values)
            {
                foreach (IManufacturable item in queue)
                    snapshot.ManufacturingQueueItems.Add(CopyManufacturableForSnapshot(item));
            }
        }

        /// <summary>
        /// Carries previously observed manufacturing state into a snapshot without current access.
        /// </summary>
        /// <param name="previousSnapshot">The snapshot containing prior manufacturing intelligence.</param>
        /// <param name="snapshot">The new snapshot receiving preserved intelligence.</param>
        /// <param name="planet">The currently observed planet.</param>
        private static void PreserveManufacturingIntelligence(
            PlanetSnapshot previousSnapshot,
            PlanetSnapshot snapshot,
            Planet planet
        )
        {
            if (previousSnapshot?.HasManufacturingIntelligence != true)
                return;

            snapshot.HasManufacturingIntelligence = true;
            HashSet<string> observedIds = GetManufacturableIDs(snapshot);
            HashSet<string> liveEntityIds = planet
                .GetChildren<ISceneNode>(recursive: true)
                .Select(entity => entity.InstanceID)
                .Concat(
                    planet.ManufacturingQueue.Values.SelectMany(queue =>
                        queue.Select(item => item.InstanceID)
                    )
                )
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
            foreach (IManufacturable item in previousSnapshot.ManufacturingQueueItems)
            {
                if (
                    liveEntityIds.Contains(item.InstanceID)
                    && !observedIds.Contains(item.InstanceID)
                )
                    snapshot.ManufacturingQueueItems.Add(CopyManufacturableForSnapshot(item));
            }

            MergeManufacturingEntities(
                snapshot.Regiments,
                previousSnapshot.Regiments.Where(item => liveEntityIds.Contains(item.InstanceID))
            );
            MergeManufacturingEntities(
                snapshot.SpecialForces,
                previousSnapshot.SpecialForces.Where(item =>
                    liveEntityIds.Contains(item.InstanceID)
                )
            );
            MergeManufacturingEntities(
                snapshot.Buildings,
                previousSnapshot.Buildings.Where(item => liveEntityIds.Contains(item.InstanceID))
            );
            MergeManufacturingEntities(
                snapshot.Starfighters,
                previousSnapshot.Starfighters.Where(item => liveEntityIds.Contains(item.InstanceID))
            );
            MergeManufacturingFleets(snapshot.Fleets, previousSnapshot.Fleets, liveEntityIds);
        }

        /// <summary>
        /// Adds unfinished entities that are absent from the destination snapshot list.
        /// </summary>
        /// <typeparam name="T">The manufacturable scene-node type.</typeparam>
        /// <param name="destination">The snapshot list receiving unfinished entities.</param>
        /// <param name="source">The previously observed entities to inspect.</param>
        internal static void MergeManufacturingEntities<T>(
            List<T> destination,
            IEnumerable<T> source
        )
            where T : class, IManufacturable
        {
            HashSet<string> existingIds = destination.Select(item => item.InstanceID).ToHashSet();
            foreach (T item in source.Where(IsManufacturingInProgress))
            {
                if (existingIds.Add(item.InstanceID))
                    destination.Add(CopyEntityForSnapshot(item));
            }
        }

        /// <summary>
        /// Adds unfinished entities that are absent from a detached container snapshot. Entities
        /// the snapshot container cannot accept (for example a building on a view planet already
        /// at energy capacity) are skipped rather than failing view construction.
        /// </summary>
        internal static void MergeManufacturingEntities<T>(
            ContainerNode destination,
            IEnumerable<T> source
        )
            where T : class, ISceneNode, IManufacturable
        {
            HashSet<string> existingIds = destination
                .GetChildren(includeDisabled: true)
                .OfType<T>()
                .Select(item => item.InstanceID)
                .ToHashSet();
            foreach (T item in source.Where(IsManufacturingInProgress))
            {
                if (!existingIds.Add(item.InstanceID))
                    continue;

                T copy = CopyEntityForSnapshot(item);
                if (destination.CanAcceptChild(copy))
                    destination.AddChild(copy);
            }
        }

        /// <summary>
        /// Preserves unfinished ships and their cargo within previously observed fleets.
        /// </summary>
        /// <param name="destination">The current snapshot fleets.</param>
        /// <param name="source">The previously observed fleets to merge.</param>
        /// <param name="liveEntityIds">The entities still present at the observed planet.</param>
        private static void MergeManufacturingFleets(
            List<Fleet> destination,
            IEnumerable<Fleet> source,
            HashSet<string> liveEntityIds
        )
        {
            foreach (
                Fleet sourceFleet in source.Where(fleet => liveEntityIds.Contains(fleet.InstanceID))
            )
            {
                Fleet destinationFleet = destination.FirstOrDefault(fleet =>
                    fleet.InstanceID == sourceFleet.InstanceID
                );
                if (destinationFleet == null)
                {
                    Fleet manufacturingFleet = CopyManufacturingFleetForSnapshot(
                        sourceFleet,
                        liveEntityIds
                    );
                    if (manufacturingFleet != null)
                        destination.Add(manufacturingFleet);
                    continue;
                }

                MergeManufacturingShips(destinationFleet, sourceFleet, liveEntityIds);
            }
        }

        /// <summary>
        /// Preserves unfinished ships and cargo from one previously observed fleet.
        /// </summary>
        /// <param name="destination">The current fleet snapshot.</param>
        /// <param name="source">The previously observed fleet.</param>
        /// <param name="liveEntityIds">The entities still present at the observed planet.</param>
        private static void MergeManufacturingShips(
            Fleet destination,
            Fleet source,
            HashSet<string> liveEntityIds
        )
        {
            HashSet<string> existingShipIds = destination
                .GetChildren<CapitalShip>()
                .Select(ship => ship.InstanceID)
                .ToHashSet();

            foreach (CapitalShip sourceShip in source.GetChildren<CapitalShip>())
            {
                CapitalShip destinationShip = destination
                    .GetChildren<CapitalShip>()
                    .FirstOrDefault(ship => ship.InstanceID == sourceShip.InstanceID);
                if (destinationShip != null)
                {
                    MergeManufacturingEntities(
                        destinationShip,
                        sourceShip
                            .GetChildren<Regiment>()
                            .Where(item => liveEntityIds.Contains(item.InstanceID))
                    );
                    MergeManufacturingEntities(
                        destinationShip,
                        sourceShip
                            .GetChildren<SpecialForces>()
                            .Where(item => liveEntityIds.Contains(item.InstanceID))
                    );
                    MergeManufacturingEntities(
                        destinationShip,
                        sourceShip
                            .GetChildren<Starfighter>()
                            .Where(item => liveEntityIds.Contains(item.InstanceID))
                    );
                    continue;
                }

                if (
                    !liveEntityIds.Contains(sourceShip.InstanceID)
                    || !IsManufacturingInProgress(sourceShip)
                    || !existingShipIds.Add(sourceShip.InstanceID)
                )
                    continue;

                CapitalShip shipCopy = CopyEntityForSnapshot(sourceShip);
                RemoveAbsentShipChildren(shipCopy, liveEntityIds);
                shipCopy.SetParent(destination);
                destination.AddChild(shipCopy);
            }
        }

        /// <summary>
        /// Removes previously observed ship occupants that are no longer at the observed planet.
        /// </summary>
        /// <param name="ship">The copied ship whose occupants should be reconciled.</param>
        /// <param name="liveEntityIds">The entities still present at the observed planet.</param>
        private static void RemoveAbsentShipChildren(
            CapitalShip ship,
            HashSet<string> liveEntityIds
        )
        {
            ship.RemoveChildren<Officer>(officer => !liveEntityIds.Contains(officer.InstanceID));
            ship.RemoveChildren<Regiment>(regiment => !liveEntityIds.Contains(regiment.InstanceID));
            ship.RemoveChildren<SpecialForces>(specialForces =>
                !liveEntityIds.Contains(specialForces.InstanceID)
            );
            ship.RemoveChildren<Starfighter>(starfighter =>
                !liveEntityIds.Contains(starfighter.InstanceID)
            );
        }

        /// <summary>
        /// Collects identifiers for manufacturable entities already represented by a snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to inspect.</param>
        /// <returns>The represented manufacturable entity identifiers.</returns>
        private static HashSet<string> GetManufacturableIDs(PlanetSnapshot snapshot)
        {
            return snapshot
                .Regiments.Cast<IManufacturable>()
                .Concat(snapshot.SpecialForces)
                .Concat(snapshot.Buildings)
                .Concat(snapshot.Starfighters)
                .Concat(snapshot.Fleets.SelectMany(fleet => fleet.GetChildren<CapitalShip>()))
                .Select(item => item.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
        }

        /// <summary>
        /// Returns whether an item is still being manufactured.
        /// </summary>
        /// <param name="item">The manufacturable item to inspect.</param>
        /// <returns>True when the item is currently building.</returns>
        private static bool IsManufacturingInProgress(IManufacturable item)
        {
            return item?.GetManufacturingStatus() == ManufacturingStatus.Building;
        }

        /// <summary>
        /// Creates a detached snapshot copy of a manufacturable item.
        /// </summary>
        /// <param name="item">The manufacturable item to copy.</param>
        /// <returns>The detached manufacturable copy.</returns>
        private static IManufacturable CopyManufacturableForSnapshot(IManufacturable item)
        {
            return CopyEntityForSnapshot(item) as IManufacturable;
        }

        /// <summary>
        /// Copies a visible scene node without retaining mutable runtime or parent state.
        /// </summary>
        /// <typeparam name="T">The scene-node type to copy.</typeparam>
        /// <param name="entity">The scene node to copy.</param>
        /// <returns>The detached snapshot copy.</returns>
        internal static T CopyEntityForSnapshot<T>(T entity)
            where T : class, ISceneNode
        {
            return entity?.CreateCopy(recursive: true, includeDisabled: entity is Mission) as T;
        }

        /// <summary>
        /// Copies an officer for storage in fog state.
        /// </summary>
        /// <param name="officer">The officer to copy.</param>
        /// <returns>The copied officer.</returns>
        internal static Officer CopyOfficerForSnapshot(Officer officer)
        {
            return officer?.CreateCopy() as Officer;
        }

        /// <summary>
        /// Copies a fleet and its complete ship hierarchy for storage in fog state.
        /// </summary>
        /// <param name="fleet">The fleet to copy.</param>
        /// <returns>The detached fleet snapshot, or null when no fleet was supplied.</returns>
        internal static Fleet CopyFleetForSnapshot(Fleet fleet)
        {
            if (fleet == null)
                return null;

            Fleet copy = fleet.CreateCopy(recursive: true) as Fleet;
            copy.Order = null;
            copy.Waypoints.Clear();
            return copy;
        }

        /// <summary>
        /// Copies the completed portion of an observed fleet for fog state.
        /// </summary>
        /// <param name="fleet">The observed fleet to copy.</param>
        /// <param name="observerFactionInstanceID">The faction receiving the observation.</param>
        /// <param name="includeManufacturing">Whether unfinished units should be retained.</param>
        /// <param name="includeInTransit">Whether moving fleets and their contents should be retained.</param>
        /// <returns>The detached fleet copy, or null when no completed ships remain.</returns>
        internal static Fleet CopyObservedFleetForSnapshot(
            Fleet fleet,
            string observerFactionInstanceID,
            bool includeManufacturing = false,
            bool includeInTransit = false
        )
        {
            if (!includeInTransit && !IsObservableAtPlanet(fleet, observerFactionInstanceID))
                return null;

            Fleet copy = CopyFleetForSnapshot(fleet);
            if (copy == null)
                return null;

            copy.RemoveChildren<CapitalShip>(ship =>
                (!includeInTransit && !IsObservableAtPlanet(ship, observerFactionInstanceID))
                || (!includeManufacturing && IsManufacturingInProgress(ship))
            );
            foreach (CapitalShip ship in copy.GetChildren<CapitalShip>())
            {
                ship.RemoveChildren<Officer>(officer =>
                    !includeInTransit && !IsObservableAtPlanet(officer, observerFactionInstanceID)
                );
                ship.RemoveChildren<Regiment>(regiment =>
                    (
                        !includeInTransit
                        && !IsObservableAtPlanet(regiment, observerFactionInstanceID)
                    ) || (!includeManufacturing && IsManufacturingInProgress(regiment))
                );
                ship.RemoveChildren<SpecialForces>(specialForces =>
                    (
                        !includeInTransit
                        && !IsObservableAtPlanet(specialForces, observerFactionInstanceID)
                    ) || (!includeManufacturing && IsManufacturingInProgress(specialForces))
                );
                ship.RemoveChildren<Starfighter>(starfighter =>
                    (
                        !includeInTransit
                        && !IsObservableAtPlanet(starfighter, observerFactionInstanceID)
                    ) || (!includeManufacturing && IsManufacturingInProgress(starfighter))
                );
            }

            return copy.GetChildren<CapitalShip>().Count > 0 ? copy : null;
        }

        /// <summary>
        /// Returns whether a faction can observe an entity at its current scene-graph location.
        /// </summary>
        /// <param name="entity">The entity whose presence is being evaluated.</param>
        /// <param name="observerFactionInstanceID">The faction receiving the observation.</param>
        /// <returns>True for owned entities and enemy entities that are not in transit.</returns>
        internal static bool IsObservableAtPlanet(
            ISceneNode entity,
            string observerFactionInstanceID
        )
        {
            return entity != null
                && (
                    entity.GetOwnerInstanceID() == observerFactionInstanceID
                    || entity is not IMovable movable
                    || movable.GetTransitMovement() == null
                );
        }

        /// <summary>
        /// Copies only unfinished ships from a previously observed fleet.
        /// </summary>
        /// <param name="fleet">The previously observed fleet.</param>
        /// <param name="liveEntityIds">The entities still present at the observed planet.</param>
        /// <returns>The detached fleet copy, or null when no unfinished ships remain.</returns>
        private static Fleet CopyManufacturingFleetForSnapshot(
            Fleet fleet,
            HashSet<string> liveEntityIds
        )
        {
            Fleet copy = CopyFleetForSnapshot(fleet);
            if (copy == null)
                return null;

            copy.RemoveChildren<CapitalShip>(ship =>
                !liveEntityIds.Contains(ship.InstanceID) || !IsManufacturingInProgress(ship)
            );
            foreach (CapitalShip ship in copy.GetChildren<CapitalShip>())
                RemoveAbsentShipChildren(ship, liveEntityIds);
            return copy.GetChildren<CapitalShip>().Count > 0 ? copy : null;
        }

        /// <summary>
        /// Copies a capital ship and its passengers for storage in fog state.
        /// </summary>
        /// <param name="capitalShip">The capital ship to copy.</param>
        /// <returns>The detached capital-ship snapshot.</returns>
        private static CapitalShip CopyCapitalShipForSnapshot(CapitalShip capitalShip)
        {
            return capitalShip?.CreateCopy(recursive: true) as CapitalShip;
        }

        /// <summary>
        /// Reconciles an entity's last known planet across existing snapshots.
        /// </summary>
        /// <param name="faction">The faction that owns the fog state.</param>
        /// <param name="entityId">The observed entity instance ID.</param>
        /// <param name="currentPlanetId">The current planet instance ID.</param>
        private void InvalidateEntityFromOtherSnapshots(
            Faction faction,
            string entityId,
            string currentPlanetId
        )
        {
            if (!faction.Fog.EntityLastSeenAt.TryGetValue(entityId, out string oldPlanetId))
            {
                faction.Fog.EntityLastSeenAt[entityId] = currentPlanetId;
                return;
            }

            if (oldPlanetId != currentPlanetId)
                RemoveEntityFromOldSnapshot(faction, entityId, oldPlanetId);

            faction.Fog.EntityLastSeenAt[entityId] = currentPlanetId;
        }

        /// <summary>
        /// Removes a faction-owned entity from tracked snapshot state.
        /// </summary>
        /// <param name="faction">The faction that owns the fog state.</param>
        /// <param name="entityId">The entity instance ID to remove.</param>
        private void RemoveEntityFromSnapshotState(Faction faction, string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
                return;

            if (faction.Fog.EntityLastSeenAt.TryGetValue(entityId, out string oldPlanetId))
                RemoveEntityFromOldSnapshot(faction, entityId, oldPlanetId);

            faction.Fog.EntityLastSeenAt.Remove(entityId);
        }

        /// <summary>
        /// Removes an entity from the snapshot where it was previously seen.
        /// </summary>
        /// <param name="faction">The faction that owns the fog state.</param>
        /// <param name="entityId">The observed entity instance ID.</param>
        /// <param name="oldPlanetId">The previous planet instance ID.</param>
        private void RemoveEntityFromOldSnapshot(
            Faction faction,
            string entityId,
            string oldPlanetId
        )
        {
            if (
                !faction.Fog.PlanetToSector.TryGetValue(oldPlanetId, out string oldSectorId)
                || !faction.Fog.Snapshots.TryGetValue(
                    oldSectorId,
                    out PlanetSectorSnapshot sectorSnapshot
                )
                || !sectorSnapshot.Planets.TryGetValue(
                    oldPlanetId,
                    out PlanetSnapshot oldPlanetSnapshot
                )
            )
                return;

            RemoveEntityFromSnapshot(oldPlanetSnapshot, entityId);
        }

        /// <summary>
        /// Removes an entity from every retained collection in a planet snapshot.
        /// </summary>
        private static void RemoveEntityFromSnapshot(PlanetSnapshot snapshot, string entityId)
        {
            snapshot.Officers.RemoveAll(o => o.InstanceID == entityId);
            snapshot.Fleets.RemoveAll(f => f.InstanceID == entityId);
            snapshot.Regiments.RemoveAll(r => r.InstanceID == entityId);
            snapshot.SpecialForces.RemoveAll(s => s.InstanceID == entityId);
            snapshot.Buildings.RemoveAll(b => b.InstanceID == entityId);
            snapshot.Starfighters.RemoveAll(s => s.InstanceID == entityId);
            snapshot.Missions.RemoveAll(m => m.InstanceID == entityId);
            snapshot.ManufacturingQueueItems.RemoveAll(item => item.InstanceID == entityId);

            foreach (Fleet fleet in snapshot.Fleets)
                RemoveEntityFromFleet(fleet, entityId);

            snapshot.Fleets.RemoveAll(f => f.GetChildren<CapitalShip>().Count == 0);
        }

        /// <summary>
        /// Removes an entity from a retained fleet and its nested capital ships.
        /// </summary>
        private static void RemoveEntityFromFleet(Fleet fleet, string entityId)
        {
            foreach (CapitalShip ship in fleet.GetChildren<CapitalShip>())
                RemoveEntityFromCapitalShip(ship, entityId);

            fleet.RemoveChildren<CapitalShip>(ship => ship.InstanceID == entityId);
        }

        /// <summary>
        /// Removes an entity from the retained contents of a capital ship.
        /// </summary>
        private static void RemoveEntityFromCapitalShip(CapitalShip ship, string entityId)
        {
            ship.RemoveChildren<Officer>(officer => officer.InstanceID == entityId);
            ship.RemoveChildren<Regiment>(regiment => regiment.InstanceID == entityId);
            ship.RemoveChildren<SpecialForces>(specialForces =>
                specialForces.InstanceID == entityId
            );
            ship.RemoveChildren<Starfighter>(starfighter => starfighter.InstanceID == entityId);
        }
    }
}
