using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Records and invalidates faction intelligence at the time an observation is made.
    /// </summary>
    public class FogOfWarCommands
    {
        private readonly GameRoot _game;
        private readonly FogOfWarQueries _queries;
        private readonly FogOfWarRecorder _recorder;

        /// <summary>
        /// Creates a FogOfWarCommands for the given game instance.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="queries">The visibility rules used when refreshing live knowledge.</param>
        public FogOfWarCommands(GameRoot game, FogOfWarQueries queries)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _recorder = new FogOfWarRecorder();
        }

        /// <summary>
        /// Creates fog-of-war commands with visibility queries over the same game graph.
        /// </summary>
        /// <param name="game">The game instance.</param>
        internal FogOfWarCommands(GameRoot game)
            : this(game, new FogOfWarQueries(game)) { }

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
        /// Records selected game objects at their current logical locations.
        /// </summary>
        /// <param name="faction">The faction receiving the observations.</param>
        /// <param name="observations">The game objects revealed to the faction.</param>
        /// <param name="currentTick">The tick when the objects were observed.</param>
        internal void RecordObservations(
            Faction faction,
            IEnumerable<ISceneNode> observations,
            int currentTick
        )
        {
            _recorder.RecordSelectedObservations(_game, faction, observations, currentTick);
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
        /// Records the post-transfer location of a captive for both the original faction and the
        /// capturing faction. Releases remove the obsolete custody observation for both sides.
        /// </summary>
        /// <param name="officer">The officer whose custody state changed.</param>
        /// <param name="previousCaptorId">The faction that held the officer before a release.</param>
        /// <param name="currentTick">The tick when custody changed.</param>
        internal void RecordCaptureState(Officer officer, string previousCaptorId, int currentTick)
        {
            if (officer == null || string.IsNullOrEmpty(officer.InstanceID))
                return;

            Faction owner = FindFaction(officer.OwnerInstanceID);
            string captorId = officer.IsCaptured ? officer.CaptorInstanceID : previousCaptorId;
            Faction captor = FindFaction(captorId);
            if (!officer.IsCaptured)
            {
                RemoveEntityFromSnapshots(owner, officer.InstanceID);
                if (captor != owner)
                    RemoveEntityFromSnapshots(captor, officer.InstanceID);
                return;
            }

            if (owner != null)
                RecordObservations(owner, new[] { officer }, currentTick);
            if (captor != null && captor != owner)
                RecordObservations(captor, new[] { officer }, currentTick);
        }

        /// <summary>
        /// Refreshes visible planet snapshots and repairs the one-location-per-entity index.
        /// </summary>
        public void ReconcileKnowledge()
        {
            RefreshVisibleKnowledge();
            foreach (Faction faction in _game.GetFactions())
                _recorder.ReconcileEntityLocations(faction);
        }

        /// <summary>
        /// Replaces remembered state for every planet that its faction can currently observe.
        /// </summary>
        internal void RefreshVisibleKnowledge()
        {
            foreach (Faction faction in _game.GetFactions())
            {
                foreach (PlanetSector sector in _game.Galaxy.GetChildren<PlanetSector>())
                {
                    foreach (
                        Planet planet in sector
                            .GetChildren<Planet>()
                            .Where(planet => _queries.IsPlanetVisible(planet, faction))
                    )
                    {
                        CaptureSnapshot(faction, planet, sector, _game.CurrentTick);
                    }
                }
            }
        }

        /// <summary>
        /// Resolves a faction without throwing when an optional owner identifier is absent.
        /// </summary>
        /// <param name="factionId">The faction identifier.</param>
        /// <returns>The matching faction, or null.</returns>
        private Faction FindFaction(string factionId)
        {
            return string.IsNullOrEmpty(factionId)
                ? null
                : _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == factionId);
        }
    }
}
