using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Records and invalidates faction intelligence at the time an observation is made.
    /// </summary>
    public class FogOfWarCommands
    {
        private readonly GameRoot _game;
        private readonly FogOfWarRecorder _recorder;

        /// <summary>
        /// Creates a FogOfWarCommands for the given game instance.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public FogOfWarCommands(GameRoot game)
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
    }
}
