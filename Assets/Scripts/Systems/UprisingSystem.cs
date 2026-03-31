using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages planetary uprisings based on popular support.
    /// Processes loyalty thresholds and fires uprising events.
    /// </summary>
    public class UprisingSystem
    {
        private readonly GameRoot game;

        public UprisingSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Processes uprising logic for all planets during a tick.
        /// Directly mutates planet state when uprisings begin.
        /// </summary>
        /// <param name="provider">Random number provider for probability rolls.</param>
        public void ProcessTick(IRandomNumberProvider provider)
        {
            // Get all planets in the game
            List<Rebellion.Game.Planet> planets = game.GetSceneNodesByType<Rebellion.Game.Planet>();

            foreach (var planet in planets)
            {
                // Skip planets without an owner
                if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                {
                    continue;
                }

                // Skip planets already in uprising
                if (planet.IsInUprising)
                {
                    continue;
                }

                // Get the owning faction
                Rebellion.Game.Faction faction = game.GetFactionByOwnerInstanceID(
                    planet.OwnerInstanceID
                );
                if (faction == null)
                {
                    continue;
                }

                // Calculate loyalty based on popular support
                int ownerSupport = planet.PopularSupport.GetValueOrDefault(
                    planet.OwnerInstanceID,
                    0
                );

                // Find the highest support from other factions
                int maxOpposingSupport = 0;
                string opposingFactionId = null;
                foreach (KeyValuePair<string, int> kvp in planet.PopularSupport)
                {
                    if (kvp.Key != planet.OwnerInstanceID && kvp.Value > maxOpposingSupport)
                    {
                        maxOpposingSupport = kvp.Value;
                        opposingFactionId = kvp.Key;
                    }
                }

                // If opposing support is higher than owner support, uprising may occur
                if (maxOpposingSupport > ownerSupport)
                {
                    // Simple probability model: higher difference = higher probability
                    int supportDifference = maxOpposingSupport - ownerSupport;
                    double uprisingProbability = supportDifference / 100.0; // Scale to 0-1

                    double roll = provider.NextDouble();
                    if (roll < uprisingProbability)
                    {
                        game.ChangeUnitOwnership(planet, opposingFactionId);
                        planet.BeginUprising();
                    }
                }
            }
        }
    }
}
