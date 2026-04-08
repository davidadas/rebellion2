using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages blockade mechanics during each game tick.
    /// </summary>
    public class BlockadeSystem
    {
        private readonly GameRoot _game;
        private readonly HashSet<string> _blockadedPlanets;

        /// <summary>
        /// Creates a new BlockadeManager.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public BlockadeSystem(GameRoot game)
        {
            _game = game;
            _blockadedPlanets = new HashSet<string>();
        }

        /// <summary>
        /// Processes blockades for the current tick.
        /// Detects blockade start/end transitions and destroys in-transit troops on blockade start.
        /// </summary>
        public void ProcessTick()
        {
            // Compute current blockade set from world state
            HashSet<string> nowBlockaded = new HashSet<string>();

            foreach (PlanetSystem system in _game.GetGalaxyMap().PlanetSystems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (planet.IsBlockaded())
                    {
                        nowBlockaded.Add(planet.InstanceID);
                    }
                }
            }

            // Detect new blockades (entered this tick)
            foreach (string planetId in nowBlockaded)
            {
                if (!_blockadedPlanets.Contains(planetId))
                {
                    // Blockade started
                    Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetId);
                    if (planet != null)
                    {
                        OnBlockadeStarted(planet);
                    }
                }
            }

            // Detect cleared blockades (ended this tick)
            foreach (string planetId in _blockadedPlanets)
            {
                if (!nowBlockaded.Contains(planetId))
                {
                    // Blockade ended
                    Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetId);
                    if (planet != null)
                    {
                        OnBlockadeEnded();
                    }
                }
            }

            // Update tracked state
            _blockadedPlanets.Clear();
            foreach (string planetId in nowBlockaded)
            {
                _blockadedPlanets.Add(planetId);
            }
        }

        private void OnBlockadeStarted(Planet planet)
        {
            // Destroy in-transit troops belonging to the defending faction
            string defendingFaction = planet.OwnerInstanceID;
            List<Regiment> regimentsToDestroy = planet
                .Regiments.Where(r => r.OwnerInstanceID == defendingFaction && r.Movement != null)
                .ToList();

            foreach (Regiment regiment in regimentsToDestroy)
            {
                _game.DetachNode(regiment);
            }
        }

        private void OnBlockadeEnded()
        {
            // No special action needed on blockade end
            // Just the transition is tracked
        }
    }
}
