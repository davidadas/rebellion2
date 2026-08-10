using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
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
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        public void RecordPlanetSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            RecordPlanetSnapshot(faction, planet, system, currentTick, false);
        }

        /// <summary>
        /// Records the complete planet intelligence revealed by successful espionage.
        /// </summary>
        /// <param name="faction">The faction receiving the intelligence.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        public void RecordEspionageSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            RecordPlanetSnapshot(faction, planet, system, currentTick, true);
        }

        /// <summary>
        /// Records current intelligence for only the requested planet categories.
        /// </summary>
        /// <param name="faction">The faction receiving intelligence.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="system">The planet's containing system.</param>
        /// <param name="currentTick">The observation tick.</param>
        /// <param name="categories">The categories revealed by this observation.</param>
        public void RecordIntelligenceSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick,
            PlanetIntelligenceCategory categories
        )
        {
            if (
                faction == null
                || planet == null
                || system == null
                || categories == PlanetIntelligenceCategory.None
            )
                return;

            planet.AddVisitor(faction.InstanceID);
            SystemSnapshot systemSnapshot = GetOrCreateSystemSnapshot(faction, system);
            faction.Fog.PlanetToSystem[planet.InstanceID] = system.InstanceID;
            if (!systemSnapshot.Planets.TryGetValue(planet.InstanceID, out PlanetSnapshot snapshot))
            {
                snapshot = new PlanetSnapshot
                {
                    TickCaptured = currentTick,
                    OwnerInstanceID = planet.OwnerInstanceID,
                    IsColonized = planet.IsColonized,
                    IsDestroyed = planet.IsDestroyed,
                };
                systemSnapshot.Planets[planet.InstanceID] = snapshot;
            }

            snapshot.TickCaptured = currentTick;
            if (categories.HasFlag(PlanetIntelligenceCategory.System))
                UpdatePlanetState(snapshot, planet, currentTick);
            snapshot.IntelligenceCategories |= categories;
            if (categories.HasFlag(PlanetIntelligenceCategory.Officers))
            {
                snapshot.Officers.Clear();
                snapshot.Officers.AddRange(
                    planet
                        .GetChildren<Officer>(_ => true)
                        .Where(officer => officer.OwnerInstanceID != faction.InstanceID)
                        .Select(CopyOfficerForSnapshot)
                );
            }
            if (categories.HasFlag(PlanetIntelligenceCategory.CapitalShips))
            {
                snapshot.Fleets.Clear();
                AddFleetsToSnapshot(faction, planet, snapshot, true);
                FilterFleetIntelligence(snapshot.Fleets, PlanetIntelligenceCategory.CapitalShips);
            }
            if (categories.HasFlag(PlanetIntelligenceCategory.GroundForces))
            {
                snapshot.Regiments.Clear();
                snapshot.SpecialForces.Clear();
                AddIntelligenceEntityCopies(
                    planet.GetChildren<Regiment>(_ => true),
                    snapshot.Regiments,
                    faction
                );
                AddEntityCopiesToSnapshot(
                    planet.GetChildren<SpecialForces>(_ => true),
                    snapshot.SpecialForces,
                    faction,
                    true
                );
            }
            if (categories.HasFlag(PlanetIntelligenceCategory.Starfighters))
            {
                snapshot.Starfighters.Clear();
                AddIntelligenceEntityCopies(
                    planet.GetChildren<Starfighter>(_ => true),
                    snapshot.Starfighters,
                    faction
                );
            }
            if (categories.HasFlag(PlanetIntelligenceCategory.Buildings))
            {
                snapshot.Buildings.Clear();
                AddEntityCopiesToSnapshot(planet.Buildings, snapshot.Buildings, faction, true);
            }

            ReconcileEntityLocations(faction, planet.InstanceID, snapshot);
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
            foreach (CapitalShip ship in fleets.SelectMany(fleet => fleet.CapitalShips))
            {
                if (!categories.HasFlag(PlanetIntelligenceCategory.Officers))
                    ship.Officers.Clear();
                if (!categories.HasFlag(PlanetIntelligenceCategory.GroundForces))
                {
                    ship.Regiments.Clear();
                    ship.SpecialForces.Clear();
                }
                if (!categories.HasFlag(PlanetIntelligenceCategory.Starfighters))
                    ship.Starfighters.Clear();
            }
        }

        /// <summary>
        /// Updates a faction's recorded owner for a planet without revealing other current state.
        /// </summary>
        /// <param name="faction">The faction receiving the ownership observation.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick used when a new planet snapshot is required.</param>
        public void RecordPlanetOwnershipSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            if (faction == null || planet == null || system == null)
                return;

            SystemSnapshot systemSnapshot = GetOrCreateSystemSnapshot(faction, system);
            faction.Fog.PlanetToSystem[planet.InstanceID] = system.InstanceID;
            if (!systemSnapshot.Planets.TryGetValue(planet.InstanceID, out PlanetSnapshot snapshot))
            {
                snapshot = new PlanetSnapshot
                {
                    TickCaptured = currentTick,
                    IsColonized = planet.IsColonized,
                    IsDestroyed = planet.IsDestroyed,
                };
                systemSnapshot.Planets[planet.InstanceID] = snapshot;
            }

            snapshot.OwnerInstanceID = planet.OwnerInstanceID;
        }

        /// <summary>
        /// Replaces a faction's planet snapshot with the currently observable state.
        /// </summary>
        /// <param name="faction">The faction receiving the observation.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        /// <param name="includeEspionageIntelligence">Whether complete espionage intelligence is observable.</param>
        private void RecordPlanetSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick,
            bool includeEspionageIntelligence
        )
        {
            if (faction == null || planet == null || system == null)
                return;

            planet.AddVisitor(faction.InstanceID);

            SystemSnapshot systemSnapshot = GetOrCreateSystemSnapshot(faction, system);
            faction.Fog.PlanetToSystem[planet.InstanceID] = system.InstanceID;
            systemSnapshot.Planets.TryGetValue(
                planet.InstanceID,
                out PlanetSnapshot previousSnapshot
            );

            PlanetSnapshot planetSnapshot = CreatePlanetSnapshot(planet, currentTick);
            AddOfficersToSnapshot(faction, planet, planetSnapshot, includeEspionageIntelligence);
            AddFleetsToSnapshot(faction, planet, planetSnapshot, includeEspionageIntelligence);
            AddEntityCopiesToSnapshot(
                planet.Regiments,
                planetSnapshot.Regiments,
                faction,
                includeEspionageIntelligence
            );
            AddEntityCopiesToSnapshot(
                planet.SpecialForces,
                planetSnapshot.SpecialForces,
                faction,
                includeEspionageIntelligence
            );
            AddEntityCopiesToSnapshot(
                planet.Buildings,
                planetSnapshot.Buildings,
                faction,
                includeEspionageIntelligence
            );
            AddEntityCopiesToSnapshot(
                planet.Starfighters,
                planetSnapshot.Starfighters,
                faction,
                includeEspionageIntelligence
            );

            if (includeEspionageIntelligence)
            {
                planetSnapshot.HasEspionageIntelligence = true;
                planetSnapshot.IntelligenceCategories = PlanetIntelligenceCategory.All;
                AddEnemyMissionsToSnapshot(faction, planet, planetSnapshot);
                AddManufacturingQueueToSnapshot(planet, planetSnapshot);
            }
            else
            {
                planetSnapshot.HasEspionageIntelligence =
                    previousSnapshot?.HasEspionageIntelligence == true;
                planetSnapshot.IntelligenceCategories =
                    previousSnapshot?.IntelligenceCategories ?? PlanetIntelligenceCategory.None;
                CarryForwardRevealedMissions(previousSnapshot, planetSnapshot);
                PreserveIncomingFleetIntelligence(previousSnapshot, planetSnapshot);
                PreserveManufacturingIntelligence(previousSnapshot, planetSnapshot, planet);
            }

            ReconcileEntityLocations(faction, planet.InstanceID, planetSnapshot);
            systemSnapshot.Planets[planet.InstanceID] = planetSnapshot;
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
                    .Missions.Where(mission => mission.GetOwnerInstanceID() != faction.InstanceID)
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
            if (previousSnapshot?.HasEspionageIntelligence != true)
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
                previousSnapshot == null
                || (
                    !previousSnapshot.HasEspionageIntelligence
                    && !previousSnapshot.IntelligenceCategories.HasFlag(
                        PlanetIntelligenceCategory.CapitalShips
                    )
                )
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
                fleet.GetChildren<ISceneNode>(_ => true).Prepend(fleet)
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

            foreach (SystemSnapshot systemSnapshot in faction.Fog.Snapshots.Values)
            {
                foreach (PlanetSnapshot planetSnapshot in systemSnapshot.Planets.Values)
                    RemoveEntityFromSnapshot(planetSnapshot, entityId);
            }

            faction.Fog.EntityLastSeenAt.Remove(entityId);
        }

        /// <summary>
        /// Returns the system snapshot for a faction, creating it when needed.
        /// </summary>
        /// <param name="faction">The faction that owns the fog state.</param>
        /// <param name="system">The system being observed.</param>
        /// <returns>The snapshot for the observed system.</returns>
        private SystemSnapshot GetOrCreateSystemSnapshot(Faction faction, PlanetSystem system)
        {
            if (!faction.Fog.Snapshots.TryGetValue(system.InstanceID, out SystemSnapshot snapshot))
            {
                snapshot = new SystemSnapshot();
                faction.Fog.Snapshots[system.InstanceID] = snapshot;
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
            foreach (Officer officer in planet.Officers)
            {
                if (officer.OwnerInstanceID == faction.InstanceID)
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
            foreach (Fleet fleet in planet.Fleets)
            {
                if (fleet.CapitalShips.Count == 0)
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
                .GetChildren<ISceneNode>(_ => true)
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
                .CapitalShips.Select(ship => ship.InstanceID)
                .ToHashSet();

            foreach (CapitalShip sourceShip in source.CapitalShips)
            {
                CapitalShip destinationShip = destination.CapitalShips.FirstOrDefault(ship =>
                    ship.InstanceID == sourceShip.InstanceID
                );
                if (destinationShip != null)
                {
                    MergeManufacturingEntities(
                        destinationShip.Regiments,
                        sourceShip.Regiments.Where(item => liveEntityIds.Contains(item.InstanceID))
                    );
                    MergeManufacturingEntities(
                        destinationShip.SpecialForces,
                        sourceShip.SpecialForces.Where(item =>
                            liveEntityIds.Contains(item.InstanceID)
                        )
                    );
                    MergeManufacturingEntities(
                        destinationShip.Starfighters,
                        sourceShip.Starfighters.Where(item =>
                            liveEntityIds.Contains(item.InstanceID)
                        )
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
                destination.CapitalShips.Add(shipCopy);
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
            ship.Officers.RemoveAll(officer => !liveEntityIds.Contains(officer.InstanceID));
            ship.Regiments.RemoveAll(regiment => !liveEntityIds.Contains(regiment.InstanceID));
            ship.SpecialForces.RemoveAll(specialForces =>
                !liveEntityIds.Contains(specialForces.InstanceID)
            );
            ship.Starfighters.RemoveAll(starfighter =>
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
                .Concat(snapshot.Fleets.SelectMany(fleet => fleet.CapitalShips))
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
            if (entity is Building building)
                return CopyBuildingForSnapshot(building) as T;
            if (entity is CapitalShip capitalShip)
                return CopyCapitalShipForSnapshot(capitalShip) as T;
            if (entity is Officer officer)
                return CopyOfficerForSnapshot(officer) as T;
            if (entity is Regiment regiment)
                return CopyRegimentForSnapshot(regiment) as T;
            if (entity is SpecialForces specialForces)
                return CopySpecialForcesForSnapshot(specialForces) as T;
            if (entity is Starfighter starfighter)
                return CopyStarfighterForSnapshot(starfighter) as T;

            T copy = entity.GetShallowCopy(CloneMode.Full);
            ClearParentReferences(copy);
            return copy;
        }

        /// <summary>
        /// Copies an officer for storage in fog state.
        /// </summary>
        /// <param name="officer">The officer to copy.</param>
        /// <returns>The copied officer.</returns>
        internal static Officer CopyOfficerForSnapshot(Officer officer)
        {
            Officer copy = officer.GetShallowCopy(CloneMode.Full);
            copy.Ratings = new Dictionary<OfficerRating, int>(officer.Ratings);
            copy.RatingModifiers =
                officer.RatingModifiers?.ConvertAll(modifier => new OfficerRatingModifier
                {
                    Key = modifier.Key,
                    Rating = modifier.Rating,
                    Amount = modifier.Amount,
                }) ?? new List<OfficerRatingModifier>();
            copy.Movement = CopyMovementForSnapshot(officer.Movement);
            ClearParentReferences(copy);
            return copy;
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

            Fleet copy = fleet.GetShallowCopy(CloneMode.Full);
            copy.Movement = CopyMovementForSnapshot(fleet.Movement);
            copy.Order = fleet.Order?.GetShallowCopy(CloneMode.Full);
            copy.CapitalShips = fleet.CapitalShips.ConvertAll(CopyCapitalShipForSnapshot);
            ClearParentReferences(copy);

            foreach (CapitalShip capitalShip in copy.CapitalShips)
                capitalShip.SetParent(copy);

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

            copy.CapitalShips.RemoveAll(ship =>
                (!includeInTransit && !IsObservableAtPlanet(ship, observerFactionInstanceID))
                || (!includeManufacturing && IsManufacturingInProgress(ship))
            );
            foreach (CapitalShip ship in copy.CapitalShips)
            {
                ship.Officers.RemoveAll(officer =>
                    !includeInTransit && !IsObservableAtPlanet(officer, observerFactionInstanceID)
                );
                ship.Regiments.RemoveAll(regiment =>
                    (
                        !includeInTransit
                        && !IsObservableAtPlanet(regiment, observerFactionInstanceID)
                    ) || (!includeManufacturing && IsManufacturingInProgress(regiment))
                );
                ship.SpecialForces.RemoveAll(specialForces =>
                    (
                        !includeInTransit
                        && !IsObservableAtPlanet(specialForces, observerFactionInstanceID)
                    ) || (!includeManufacturing && IsManufacturingInProgress(specialForces))
                );
                ship.Starfighters.RemoveAll(starfighter =>
                    (
                        !includeInTransit
                        && !IsObservableAtPlanet(starfighter, observerFactionInstanceID)
                    ) || (!includeManufacturing && IsManufacturingInProgress(starfighter))
                );
            }

            return copy.CapitalShips.Count > 0 ? copy : null;
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

            copy.CapitalShips.RemoveAll(ship =>
                !liveEntityIds.Contains(ship.InstanceID) || !IsManufacturingInProgress(ship)
            );
            foreach (CapitalShip ship in copy.CapitalShips)
                RemoveAbsentShipChildren(ship, liveEntityIds);
            return copy.CapitalShips.Count > 0 ? copy : null;
        }

        /// <summary>
        /// Copies a capital ship and its passengers for storage in fog state.
        /// </summary>
        /// <param name="capitalShip">The capital ship to copy.</param>
        /// <returns>The detached capital-ship snapshot.</returns>
        private static CapitalShip CopyCapitalShipForSnapshot(CapitalShip capitalShip)
        {
            CapitalShip copy = capitalShip.GetShallowCopy(CloneMode.Full);
            copy.Roles = new List<CapitalShipRole>(capitalShip.Roles);
            copy.PrimaryWeapons = capitalShip.PrimaryWeapons.ToDictionary(
                entry => entry.Key,
                entry => entry.Value?.ToArray()
            );
            copy.Movement = CopyMovementForSnapshot(capitalShip.Movement);
            copy.Officers = capitalShip.Officers.ConvertAll(CopyOfficerForSnapshot);
            copy.Regiments = capitalShip.Regiments.ConvertAll(CopyRegimentForSnapshot);
            copy.SpecialForces = capitalShip.SpecialForces.ConvertAll(CopySpecialForcesForSnapshot);
            copy.Starfighters = capitalShip.Starfighters.ConvertAll(CopyStarfighterForSnapshot);
            ClearParentReferences(copy);

            foreach (ISceneNode child in copy.GetChildren())
                child.SetParent(copy);

            return copy;
        }

        /// <summary>
        /// Copies a building for storage in fog state.
        /// </summary>
        /// <param name="building">The building to copy.</param>
        /// <returns>The detached building snapshot.</returns>
        private static Building CopyBuildingForSnapshot(Building building)
        {
            Building copy = building.GetShallowCopy(CloneMode.Full);
            copy.Movement = CopyMovementForSnapshot(building.Movement);
            ClearParentReferences(copy);
            return copy;
        }

        /// <summary>
        /// Copies a regiment for storage in fog state.
        /// </summary>
        /// <param name="regiment">The regiment to copy.</param>
        /// <returns>The detached regiment snapshot.</returns>
        private static Regiment CopyRegimentForSnapshot(Regiment regiment)
        {
            Regiment copy = regiment.GetShallowCopy(CloneMode.Full);
            copy.Movement = CopyMovementForSnapshot(regiment.Movement);
            ClearParentReferences(copy);
            return copy;
        }

        /// <summary>
        /// Copies a special-forces unit for storage in fog state.
        /// </summary>
        /// <param name="specialForces">The special-forces unit to copy.</param>
        /// <returns>The detached special-forces snapshot.</returns>
        private static SpecialForces CopySpecialForcesForSnapshot(SpecialForces specialForces)
        {
            SpecialForces copy = specialForces.GetShallowCopy(CloneMode.Full);
            copy.Ratings = new Dictionary<OfficerRating, int>(specialForces.Ratings);
            copy.Movement = CopyMovementForSnapshot(specialForces.Movement);
            ClearParentReferences(copy);
            return copy;
        }

        /// <summary>
        /// Copies a starfighter unit for storage in fog state.
        /// </summary>
        /// <param name="starfighter">The starfighter to copy.</param>
        /// <returns>The detached starfighter snapshot.</returns>
        private static Starfighter CopyStarfighterForSnapshot(Starfighter starfighter)
        {
            Starfighter copy = starfighter.GetShallowCopy(CloneMode.Full);
            copy.Movement = CopyMovementForSnapshot(starfighter.Movement);
            ClearParentReferences(copy);
            return copy;
        }

        /// <summary>
        /// Removes live scene-graph parent references from a snapshot node.
        /// </summary>
        /// <param name="node">The copied node to detach.</param>
        private static void ClearParentReferences(ISceneNode node)
        {
            node.ParentInstanceID = null;
            node.LastParentInstanceID = null;
            node.ParentNode = null;
            node.LastParentNode = null;
        }

        /// <summary>
        /// Copies movement state for storage in fog state.
        /// </summary>
        /// <param name="movement">The movement state to copy.</param>
        /// <returns>The copied movement state, or null when the entity is stationary.</returns>
        private static MovementState CopyMovementForSnapshot(MovementState movement)
        {
            return movement?.GetShallowCopy(CloneMode.Full);
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
                !faction.Fog.PlanetToSystem.TryGetValue(oldPlanetId, out string oldSystemId)
                || !faction.Fog.Snapshots.TryGetValue(
                    oldSystemId,
                    out SystemSnapshot systemSnapshot
                )
                || !systemSnapshot.Planets.TryGetValue(
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

            snapshot.Fleets.RemoveAll(f => f.CapitalShips.Count == 0);
        }

        /// <summary>
        /// Removes an entity from a retained fleet and its nested capital ships.
        /// </summary>
        private static void RemoveEntityFromFleet(Fleet fleet, string entityId)
        {
            foreach (CapitalShip ship in fleet.CapitalShips)
                RemoveEntityFromCapitalShip(ship, entityId);

            fleet.CapitalShips.RemoveAll(s => s.InstanceID == entityId);
        }

        /// <summary>
        /// Removes an entity from the retained contents of a capital ship.
        /// </summary>
        private static void RemoveEntityFromCapitalShip(CapitalShip ship, string entityId)
        {
            ship.Officers.RemoveAll(o => o.InstanceID == entityId);
            ship.Regiments.RemoveAll(r => r.InstanceID == entityId);
            ship.SpecialForces.RemoveAll(s => s.InstanceID == entityId);
            ship.Starfighters.RemoveAll(s => s.InstanceID == entityId);
        }
    }
}
