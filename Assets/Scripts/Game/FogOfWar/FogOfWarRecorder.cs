using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
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
                planet.CapitalShips,
                planetSnapshot.CapitalShips,
                faction,
                planet.InstanceID
            );
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
                if (officer.IsCaptured && officer.OwnerInstanceID == faction.InstanceID)
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
        private void AddFleetsToSnapshot(Faction faction, Planet planet, PlanetSnapshot snapshot)
        {
            foreach (Fleet fleet in planet.Fleets)
            {
                if (fleet.CapitalShips.Count == 0)
                    continue;
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
            where T : class, IGameEntity
        {
            foreach (T entity in source)
            {
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
            copy.Skills = new Dictionary<MissionParticipantSkill, int>(officer.Skills);
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
            oldPlanetSnapshot.CapitalShips.RemoveAll(c => c.InstanceID == entityId);
            oldPlanetSnapshot.Regiments.RemoveAll(r => r.InstanceID == entityId);
            oldPlanetSnapshot.Buildings.RemoveAll(b => b.InstanceID == entityId);
            oldPlanetSnapshot.Starfighters.RemoveAll(s => s.InstanceID == entityId);
        }
    }
}
