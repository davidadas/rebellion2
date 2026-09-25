using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Runs delegated faction automation during a game tick.
    /// </summary>
    internal sealed class FactionAutomationTickProcessor : ITickProcessor
    {
        private readonly FactionAutomationCommands _commands;

        /// <summary>
        /// Creates faction automation tick processing.
        /// </summary>
        /// <param name="commands">The delegated faction operations.</param>
        public FactionAutomationTickProcessor(FactionAutomationCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Fills currently idle manufacturing capacity with delegated work.
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
