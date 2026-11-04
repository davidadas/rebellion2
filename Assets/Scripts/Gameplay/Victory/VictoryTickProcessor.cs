using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Evaluates faction victory conditions during a game tick.
    /// </summary>
    internal sealed class VictoryTickProcessor : ITickProcessor
    {
        private readonly VictoryCommands _commands;
        private readonly VictoryQueries _queries;

        /// <summary>
        /// Creates victory tick processing.
        /// </summary>
        /// <param name="commands">The victory operations and declaration state.</param>
        /// <param name="queries">The configured victory eligibility rules.</param>
        public VictoryTickProcessor(VictoryCommands commands, VictoryQueries queries)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Evaluates all currently eligible victory conditions.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>No queued results; declarations publish through the victory command.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            if (_commands.IsDeclared)
                return Array.Empty<GameResult>();

            List<Officer> officers = game.GetSceneNodesByType<Officer>();
            Faction winner = _queries.FindWinner(officers);
            VictoryResult outcome = _commands.TryDeclareVictory(winner);
            if (outcome != null)
                GameLogger.Log($"Victory condition met: {outcome.Winner.GetDisplayName()} won.");

            return Array.Empty<GameResult>();
        }
    }
}
