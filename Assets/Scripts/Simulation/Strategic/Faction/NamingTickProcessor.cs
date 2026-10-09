using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Assigns automated capital-ship names during a game tick.
    /// </summary>
    internal sealed class NamingTickProcessor : ITickProcessor
    {
        private readonly NamingCommands _commands;

        /// <summary>
        /// Creates naming tick processing.
        /// </summary>
        /// <param name="commands">The ship-naming operations.</param>
        public NamingTickProcessor(NamingCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Assigns names for factions whose naming is automated.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>No gameplay results.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
                _commands.ProcessFaction(faction);

            return Array.Empty<GameResult>();
        }
    }
}
