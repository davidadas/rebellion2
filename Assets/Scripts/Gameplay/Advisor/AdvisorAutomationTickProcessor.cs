using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Runs delegated advisor automation during a game tick.
    /// </summary>
    internal sealed class AdvisorAutomationTickProcessor : ITickProcessor
    {
        private readonly AdvisorAutomationCommands _commands;

        /// <summary>
        /// Creates advisor automation tick processing.
        /// </summary>
        /// <param name="commands">The delegated advisor operations.</param>
        public AdvisorAutomationTickProcessor(AdvisorAutomationCommands commands)
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
            using var measurement = PerformanceLogger.Measure(
                "Game system tick",
                subjectType: typeof(AdvisorAutomationTickProcessor)
            );
            foreach (Faction faction in game.GetFactions())
            {
                using var factionMeasurement = PerformanceLogger.Measure(
                    "Faction system tick",
                    faction?.PeekInstanceID(),
                    subjectType: typeof(AdvisorAutomationTickProcessor)
                );
                _commands.ProcessFaction(faction);
            }

            return Array.Empty<GameResult>();
        }
    }
}
