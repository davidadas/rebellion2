using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Reconciles planetary support and ownership during a game tick.
    /// </summary>
    internal sealed class PlanetaryControlTickProcessor : ITickProcessor
    {
        private readonly PlanetaryControlCommands _commands;

        /// <summary>
        /// Creates planetary-control tick processing.
        /// </summary>
        /// <param name="commands">The planetary support and ownership operations.</param>
        public PlanetaryControlTickProcessor(PlanetaryControlCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Advances support drift and applies ownership transitions.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The planetary-control results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            _commands.UpdateBlockadeSupport();
            _commands.UpdateUncolonizedPlanets(results);
            _commands.CheckOwnershipTransfers(results);
            return results;
        }
    }
}
