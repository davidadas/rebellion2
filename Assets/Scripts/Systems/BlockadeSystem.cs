using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

/// <summary>
/// Manages blockade mechanics during each game tick.
/// </summary>
namespace Rebellion.Systems
{
    public class BlockadeSystem
    {
        private readonly GameRoot game;
        private readonly HashSet<string> blockadedPlanets;

        /// <summary>
        /// Creates a new BlockadeManager.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public BlockadeSystem(GameRoot game)
        {
            this.game = game;
            this.blockadedPlanets = new HashSet<string>();
        }

        /// <summary>
        /// Processes blockades for the current tick.
        /// Detects blockade start/end transitions and destroys in-transit troops on blockade start.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public void ProcessTick(GameRoot game)
        {
            if (game == null)
                throw new InvalidOperationException(
                    "BlockadeSystem.ProcessTick called with null game"
                );

            // Compute current blockade set from world state
            HashSet<string> nowBlockaded = new HashSet<string>();

            foreach (PlanetSystem system in game.GetGalaxyMap().PlanetSystems)
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
                if (!blockadedPlanets.Contains(planetId))
                {
                    // Blockade started
                    Planet planet = game.GetSceneNodeByInstanceID<Planet>(planetId);
                    if (planet != null)
                    {
                        OnBlockadeStarted(planet, game);
                    }
                }
            }

            // Detect cleared blockades (ended this tick)
            foreach (string planetId in blockadedPlanets)
            {
                if (!nowBlockaded.Contains(planetId))
                {
                    // Blockade ended
                    Planet planet = game.GetSceneNodeByInstanceID<Planet>(planetId);
                    if (planet != null)
                    {
                        OnBlockadeEnded(planet);
                    }
                }
            }

            // Update tracked state
            blockadedPlanets.Clear();
            foreach (string planetId in nowBlockaded)
            {
                blockadedPlanets.Add(planetId);
            }
        }

        private void OnBlockadeStarted(Planet planet, GameRoot game)
        {
            // Destroy in-transit troops belonging to the defending faction
            string defendingFaction = planet.OwnerInstanceID;
            List<Regiment> regimentsToDestroy = planet
                .Regiments.Where(r => r.OwnerInstanceID == defendingFaction && r.Movement != null)
                .ToList();

            foreach (Regiment regiment in regimentsToDestroy)
            {
                game.DetachNode(regiment);
            }
        }

        private void OnBlockadeEnded(Planet planet)
        {
            // No special action needed on blockade end
            // Just the transition is tracked
        }
    }
}
