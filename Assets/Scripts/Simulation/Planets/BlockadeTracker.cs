using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Tracks blockade transitions between simulation ticks.
    /// </summary>
    public class BlockadeTracker
    {
        private readonly GameRoot _game;
        private readonly HashSet<string> _blockadedPlanets;

        /// <summary>
        /// Creates blockade tracking for the active game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public BlockadeTracker(GameRoot game)
        {
            _game = game;
            _blockadedPlanets = new HashSet<string>();
        }

        /// <summary>
        /// Scans all planets and returns the set currently under blockade.
        /// </summary>
        /// <returns>Instance IDs of all currently blockaded planets.</returns>
        internal HashSet<string> DetectBlockadedPlanets()
        {
            HashSet<string> blockaded = new HashSet<string>();
            foreach (PlanetSector sector in _game.GetGalaxyMap().GetChildren<PlanetSector>())
            {
                foreach (Planet planet in sector.GetChildren<Planet>())
                {
                    if (planet.IsBlockaded())
                        blockaded.Add(planet.InstanceID);
                }
            }
            return blockaded;
        }

        /// <summary>
        /// Emits results for blockades that started since the last tick.
        /// </summary>
        /// <param name="currentBlockades">Planets blockaded this tick.</param>
        /// <param name="results">Results list to append transitions to.</param>
        internal void ApplyBlockadeStatus(
            HashSet<string> currentBlockades,
            List<GameResult> results
        )
        {
            foreach (string planetId in currentBlockades)
            {
                if (_blockadedPlanets.Contains(planetId))
                    continue;

                Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetId);
                if (planet == null)
                    continue;

                results.Add(
                    new BlockadeChangedResult
                    {
                        Planet = planet,
                        BlockadingFleet = planet
                            .GetChildren<Fleet>()
                            .FirstOrDefault(f =>
                                f.Movement == null
                                && f.OwnerInstanceID != planet.OwnerInstanceID
                                && f.HasOperationalCapitalShips()
                            ),
                        Blockaded = true,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Emits results for blockades that ended since the last tick.
        /// </summary>
        /// <param name="currentBlockades">Planets blockaded this tick.</param>
        /// <param name="results">Results list to append transitions to.</param>
        internal void ClearBlockadeStatus(
            HashSet<string> currentBlockades,
            List<GameResult> results
        )
        {
            foreach (string planetId in _blockadedPlanets)
            {
                if (currentBlockades.Contains(planetId))
                    continue;

                Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetId);
                if (planet == null)
                    continue;

                results.Add(
                    new BlockadeChangedResult
                    {
                        Planet = planet,
                        BlockadingFleet = null,
                        Blockaded = false,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Replaces the blockade state retained for the next transition comparison.
        /// </summary>
        /// <param name="currentBlockades">The planets blockaded during the current tick.</param>
        internal void RememberBlockades(HashSet<string> currentBlockades)
        {
            _blockadedPlanets.Clear();
            _blockadedPlanets.UnionWith(currentBlockades);
        }
    }
}
