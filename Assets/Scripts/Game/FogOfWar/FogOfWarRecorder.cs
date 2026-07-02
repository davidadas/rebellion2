using System.Collections.Generic;
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
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the observation is recorded.</param>
        public void RecordPlanetSnapshot(
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

            PlanetSnapshot planetSnapshot = CreatePlanetSnapshot(planet, currentTick);
            AddOfficersToSnapshot(faction, planet, planetSnapshot);
            AddFleetsToSnapshot(faction, planet, planetSnapshot);
            AddEntityCopiesToSnapshot(
                planet.Regiments,
                planetSnapshot.Regiments,
                faction,
                planet.InstanceID
            );
            AddEntityCopiesToSnapshot(
                planet.Buildings,
                planetSnapshot.Buildings,
                faction,
                planet.InstanceID
            );
            AddEntityCopiesToSnapshot(
                planet.Starfighters,
                planetSnapshot.Starfighters,
                faction,
                planet.InstanceID
            );
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
        private void AddFleetsToSnapshot(Faction faction, Planet planet, PlanetSnapshot snapshot)
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
                if (fleet.OwnerInstanceID != faction.InstanceID && fleet.Movement != null)
                    continue;

                snapshot.Fleets.Add(fleet.GetShallowCopy(CloneMode.Full));
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
        private void AddEntityCopiesToSnapshot<T>(
            IEnumerable<T> source,
            List<T> destination,
            Faction faction,
            string planetId
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

                destination.Add(entity.GetShallowCopy(CloneMode.Full));
                InvalidateEntityFromOtherSnapshots(faction, entity.InstanceID, planetId);
            }
        }

        /// <summary>
        /// Copies an officer for storage in fog state.
        /// </summary>
        /// <param name="officer">The officer to copy.</param>
        /// <returns>The copied officer.</returns>
        private Officer CopyOfficerForSnapshot(Officer officer)
        {
            Officer copy = officer.GetShallowCopy(CloneMode.Full);
            copy.Ratings = new Dictionary<OfficerRating, int>(officer.Ratings);
            return copy;
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
            oldPlanetSnapshot.Buildings.RemoveAll(b => b.InstanceID == entityId);
            oldPlanetSnapshot.Starfighters.RemoveAll(s => s.InstanceID == entityId);
        }

        private static void RemoveEntityFromSnapshot(PlanetSnapshot snapshot, string entityId)
        {
            snapshot.Officers.RemoveAll(o => o.InstanceID == entityId);
            snapshot.Fleets.RemoveAll(f => f.InstanceID == entityId);
            snapshot.Regiments.RemoveAll(r => r.InstanceID == entityId);
            snapshot.Buildings.RemoveAll(b => b.InstanceID == entityId);
            snapshot.Starfighters.RemoveAll(s => s.InstanceID == entityId);

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
