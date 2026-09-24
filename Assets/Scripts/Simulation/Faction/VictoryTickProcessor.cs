using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates faction victory conditions during a game tick.
    /// </summary>
    internal sealed class VictoryTickProcessor : ITickProcessor
    {
        private readonly VictoryCommands _commands;

        /// <summary>
        /// Creates victory tick processing.
        /// </summary>
        /// <param name="commands">The victory operations and declaration state.</param>
        public VictoryTickProcessor(VictoryCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Evaluates all currently eligible victory conditions.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The victory result when a faction wins.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            if (_commands.IsDeclared)
                return Array.Empty<GameResult>();

            foreach (Faction faction in game.GetFactions())
            {
                VictoryResult outcome = _commands.CheckHQCapture(faction);
                if (outcome == null)
                    continue;

                GameLogger.Log(
                    $"Victory condition met: {outcome.Winner.GetDisplayName()} defeated {outcome.Loser.GetDisplayName()}."
                );
                return new GameResult[] { outcome };
            }

            return Array.Empty<GameResult>();
        }
    }
}
