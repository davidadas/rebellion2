using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Reconciles smuggling state during a game tick.
    /// </summary>
    internal sealed class SmugglingTickProcessor : ITickProcessor
    {
        private readonly SmugglingCommands _commands;

        /// <summary>
        /// Creates smuggling tick processing.
        /// </summary>
        /// <param name="commands">The smuggling operations and state.</param>
        public SmugglingTickProcessor(SmugglingCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Reconciles current smuggling relationships and diversion percentages.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The smuggling changes produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (
                Planet planet in game.GetGalaxyMap()
                    .GetChildren<PlanetSector>()
                    .SelectMany(sector => sector.GetChildren<Planet>())
                    .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                _commands.RefreshPlanet(planet, results);
            }

            return results;
        }
    }
}
