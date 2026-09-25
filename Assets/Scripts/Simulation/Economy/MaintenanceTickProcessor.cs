using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Reconciles maintenance capacity during a game tick.
    /// </summary>
    internal sealed class MaintenanceTickProcessor : ITickProcessor
    {
        private readonly MaintenanceCommands _commands;

        /// <summary>
        /// Creates maintenance tick processing.
        /// </summary>
        /// <param name="commands">The maintenance operations and shortfall state.</param>
        public MaintenanceTickProcessor(MaintenanceCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Applies maintenance shortfalls and scheduled automatic scrapping.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The maintenance results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (Faction faction in game.GetFactions())
                _commands.ProcessFactionMaintenance(faction, results);

            return results;
        }
    }
}
