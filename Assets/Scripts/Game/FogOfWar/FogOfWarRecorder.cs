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
        /// Records a planet snapshot that includes unfinished units and manufacturing queues.
        /// </summary>
        /// <param name="faction">The faction receiving the manufacturing observation.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        public void RecordPlanetManufacturingSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            RecordPlanetSnapshot(faction, planet, system, currentTick, true);
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
        /// <param name="includeManufacturing">Whether unfinished units and queue contents are observable.</param>
        private void RecordPlanetSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick,
            bool includeManufacturing
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
            AddOfficersToSnapshot(faction, planet, planetSnapshot);
            AddFleetsToSnapshot(faction, planet, planetSnapshot, includeManufacturing);
            AddEntityCopiesToSnapshot(
                planet.Regiments,
                planetSnapshot.Regiments,
                faction,
                planet.InstanceID,
                includeManufacturing
            );
            AddEntityCopiesToSnapshot(
                planet.SpecialForces,
                planetSnapshot.SpecialForces,
                faction,
                planet.InstanceID,
                includeManufacturing
            );
            AddEntityCopiesToSnapshot(
                planet.Buildings,
                planetSnapshot.Buildings,
                faction,
                planet.InstanceID,
                includeManufacturing
            );
            AddEntityCopiesToSnapshot(
                planet.Starfighters,
                planetSnapshot.Starfighters,
                faction,
                planet.InstanceID,
                includeManufacturing
            );

            if (includeManufacturing)
                AddManufacturingQueueToSnapshot(planet, planetSnapshot);
            else
                PreserveManufacturingIntelligence(previousSnapshot, planetSnapshot);

            systemSnapshot.Planets[planet.InstanceID] = planetSnapshot;
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
                EnergyCapacity = planet.EnergyCapacity,
                AllocatedEnergy = planet.AllocatedEnergy,
                PopularSupport = new Dictionary<string, int>(planet.PopularSupport),
            };
        }

        /// <summary>
        /// Adds visible officers to a planet snapshot.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="snapshot">The snapshot being populated.</param>
        private void AddOfficersToSnapshot(Faction faction, Planet planet, PlanetSnapshot snapshot)
        {
            foreach (Officer officer in planet.Officers)
            {
                if (officer.OwnerInstanceID == faction.InstanceID)
                {
                    RemoveEntityFromSnapshotState(faction, officer.InstanceID);
                    continue;
                }

                if (!IsObservableAtPlanet(officer, faction.InstanceID))
                    continue;

                snapshot.Officers.Add(CopyOfficerForSnapshot(officer));
                InvalidateEntityFromOtherSnapshots(faction, officer.InstanceID, planet.InstanceID);
            }
        }

        /// <summary>
        /// Adds visible fleets to a planet snapshot.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The observed planet.</param>
        /// <param name="snapshot">The snapshot being populated.</param>
        /// <param name="includeManufacturing">Whether unfinished units should be included.</param>
        private void AddFleetsToSnapshot(
            Faction faction,
            Planet planet,
            PlanetSnapshot snapshot,
            bool includeManufacturing
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

                if (!IsObservableAtPlanet(fleet, faction.InstanceID))
                    continue;

                Fleet fleetCopy = CopyObservedFleetForSnapshot(
                    fleet,
                    faction.InstanceID,
                    includeManufacturing
                );
                if (fleetCopy == null)
                    continue;

                snapshot.Fleets.Add(fleetCopy);
                InvalidateEntityFromOtherSnapshots(faction, fleet.InstanceID, planet.InstanceID);
            }
        }

        /// <summary>
        /// Adds visible entities to a planet snapshot list.
        /// </summary>
        /// <typeparam name="T">The entity type to copy.</typeparam>
        /// <param name="source">The live entities to copy.</param>
        /// <param name="destination">The snapshot list to populate.</param>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planetId">The observed planet instance ID.</param>
        /// <param name="includeManufacturing">Whether unfinished units should be included.</param>
        private void AddEntityCopiesToSnapshot<T>(
            IEnumerable<T> source,
            List<T> destination,
            Faction faction,
            string planetId,
            bool includeManufacturing
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

                if (!IsObservableAtPlanet(entity, faction.InstanceID))
                    continue;

                if (
                    !includeManufacturing
                    && entity is IManufacturable manufacturable
                    && IsManufacturingInProgress(manufacturable)
                )
                    continue;

                destination.Add(CopyEntityForSnapshot(entity));
                InvalidateEntityFromOtherSnapshots(faction, entity.InstanceID, planetId);
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
        private static void PreserveManufacturingIntelligence(
            PlanetSnapshot previousSnapshot,
            PlanetSnapshot snapshot
        )
        {
            if (previousSnapshot?.HasManufacturingIntelligence != true)
                return;

            snapshot.HasManufacturingIntelligence = true;
            HashSet<string> observedIds = GetManufacturableIDs(snapshot);
            foreach (IManufacturable item in previousSnapshot.ManufacturingQueueItems)
            {
                if (!observedIds.Contains(item.InstanceID))
                    snapshot.ManufacturingQueueItems.Add(CopyManufacturableForSnapshot(item));
            }

            MergeManufacturingEntities(snapshot.Regiments, previousSnapshot.Regiments);
            MergeManufacturingEntities(snapshot.SpecialForces, previousSnapshot.SpecialForces);
            MergeManufacturingEntities(snapshot.Buildings, previousSnapshot.Buildings);
            MergeManufacturingEntities(snapshot.Starfighters, previousSnapshot.Starfighters);
            MergeManufacturingFleets(snapshot.Fleets, previousSnapshot.Fleets);
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
        private static void MergeManufacturingFleets(
            List<Fleet> destination,
            IEnumerable<Fleet> source
        )
        {
            foreach (Fleet sourceFleet in source)
            {
                Fleet destinationFleet = destination.FirstOrDefault(fleet =>
                    fleet.InstanceID == sourceFleet.InstanceID
                );
                if (destinationFleet == null)
                {
                    Fleet manufacturingFleet = CopyManufacturingFleetForSnapshot(sourceFleet);
                    if (manufacturingFleet != null)
                        destination.Add(manufacturingFleet);
                    continue;
                }

                MergeManufacturingShips(destinationFleet, sourceFleet);
            }
        }

        /// <summary>
        /// Preserves unfinished ships and cargo from one previously observed fleet.
        /// </summary>
        /// <param name="destination">The current fleet snapshot.</param>
        /// <param name="source">The previously observed fleet.</param>
        private static void MergeManufacturingShips(Fleet destination, Fleet source)
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
                    MergeManufacturingEntities(destinationShip.Regiments, sourceShip.Regiments);
                    MergeManufacturingEntities(
                        destinationShip.SpecialForces,
                        sourceShip.SpecialForces
                    );
                    MergeManufacturingEntities(
                        destinationShip.Starfighters,
                        sourceShip.Starfighters
                    );
                    continue;
                }

                if (
                    !IsManufacturingInProgress(sourceShip)
                    || !existingShipIds.Add(sourceShip.InstanceID)
                )
                    continue;

                CapitalShip shipCopy = CopyEntityForSnapshot(sourceShip);
                shipCopy.SetParent(destination);
                destination.CapitalShips.Add(shipCopy);
            }
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
        /// <returns>The detached fleet copy, or null when no completed ships remain.</returns>
        internal static Fleet CopyObservedFleetForSnapshot(
            Fleet fleet,
            string observerFactionInstanceID,
            bool includeManufacturing = false
        )
        {
            if (!IsObservableAtPlanet(fleet, observerFactionInstanceID))
                return null;

            Fleet copy = CopyFleetForSnapshot(fleet);
            if (copy == null)
                return null;

            copy.CapitalShips.RemoveAll(ship =>
                !IsObservableAtPlanet(ship, observerFactionInstanceID)
                || !includeManufacturing && IsManufacturingInProgress(ship)
            );
            foreach (CapitalShip ship in copy.CapitalShips)
            {
                ship.Officers.RemoveAll(officer =>
                    !IsObservableAtPlanet(officer, observerFactionInstanceID)
                );
                ship.Regiments.RemoveAll(regiment =>
                    !IsObservableAtPlanet(regiment, observerFactionInstanceID)
                    || !includeManufacturing && IsManufacturingInProgress(regiment)
                );
                ship.SpecialForces.RemoveAll(specialForces =>
                    !IsObservableAtPlanet(specialForces, observerFactionInstanceID)
                    || !includeManufacturing && IsManufacturingInProgress(specialForces)
                );
                ship.Starfighters.RemoveAll(starfighter =>
                    !IsObservableAtPlanet(starfighter, observerFactionInstanceID)
                    || !includeManufacturing && IsManufacturingInProgress(starfighter)
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
        /// <returns>The detached fleet copy, or null when no unfinished ships remain.</returns>
        private static Fleet CopyManufacturingFleetForSnapshot(Fleet fleet)
        {
            Fleet copy = CopyFleetForSnapshot(fleet);
            if (copy == null)
                return null;

            copy.CapitalShips.RemoveAll(ship => !IsManufacturingInProgress(ship));
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

            oldPlanetSnapshot.Officers.RemoveAll(o => o.InstanceID == entityId);
            oldPlanetSnapshot.Fleets.RemoveAll(f => f.InstanceID == entityId);
            oldPlanetSnapshot.Regiments.RemoveAll(r => r.InstanceID == entityId);
            oldPlanetSnapshot.SpecialForces.RemoveAll(s => s.InstanceID == entityId);
            oldPlanetSnapshot.Buildings.RemoveAll(b => b.InstanceID == entityId);
            oldPlanetSnapshot.Starfighters.RemoveAll(s => s.InstanceID == entityId);
            oldPlanetSnapshot.ManufacturingQueueItems.RemoveAll(item =>
                item.InstanceID == entityId
            );
        }

        private static void RemoveEntityFromSnapshot(PlanetSnapshot snapshot, string entityId)
        {
            snapshot.Officers.RemoveAll(o => o.InstanceID == entityId);
            snapshot.Fleets.RemoveAll(f => f.InstanceID == entityId);
            snapshot.Regiments.RemoveAll(r => r.InstanceID == entityId);
            snapshot.SpecialForces.RemoveAll(s => s.InstanceID == entityId);
            snapshot.Buildings.RemoveAll(b => b.InstanceID == entityId);
            snapshot.Starfighters.RemoveAll(s => s.InstanceID == entityId);
            snapshot.ManufacturingQueueItems.RemoveAll(item => item.InstanceID == entityId);

            foreach (Fleet fleet in snapshot.Fleets)
                RemoveEntityFromFleet(fleet, entityId);

            snapshot.Fleets.RemoveAll(f => f.CapitalShips.Count == 0);
        }

        private static void RemoveEntityFromFleet(Fleet fleet, string entityId)
        {
            foreach (CapitalShip ship in fleet.CapitalShips)
                RemoveEntityFromCapitalShip(ship, entityId);

            fleet.CapitalShips.RemoveAll(s => s.InstanceID == entityId);
        }

        private static void RemoveEntityFromCapitalShip(CapitalShip ship, string entityId)
        {
            ship.Officers.RemoveAll(o => o.InstanceID == entityId);
            ship.Regiments.RemoveAll(r => r.InstanceID == entityId);
            ship.SpecialForces.RemoveAll(s => s.InstanceID == entityId);
            ship.Starfighters.RemoveAll(s => s.InstanceID == entityId);
        }
    }
}
