using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Converts ordered victory-relevant game results into one terminal victory result.
    /// </summary>
    public sealed class VictoryObserver : IResultObserver, IDisposable
    {
        private readonly VictoryCommands _commands;
        private readonly VictoryQueries _queries;
        private IDisposable _subscription;

        /// <summary>
        /// Creates the victory result listener.
        /// </summary>
        /// <param name="commands">The general victory declaration operation.</param>
        /// <param name="queries">The configured victory eligibility rules.</param>
        public VictoryObserver(VictoryCommands commands, VictoryQueries queries)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Registers the ordered victory callback with the result bus.
        /// </summary>
        /// <param name="results">The bus that delivers completed game results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Victory observer is already connected.");
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<GameResult>(HandleResults);
        }

        /// <summary>
        /// Stops receiving completed game results.
        /// </summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Applies the configured victory condition immediately after a relevant state change.
        /// </summary>
        /// <param name="results">The ordered completed game results.</param>
        /// <returns>The first victory caused by this result wave, when present.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            List<GameResult> reactions = new();
            if (_commands.IsDeclared || results == null)
                return reactions;

            foreach (GameResult result in results)
            {
                Faction candidate = ResolveCandidate(result);
                if (candidate == null || !_queries.HasDefeatedEveryOpponent(candidate))
                    continue;

                VictoryResult victory = _commands.TryDeclareVictory(candidate);
                if (victory == null)
                    continue;

                reactions.Add(victory);
                GameLogger.Log($"Victory condition met: {victory.Winner.GetDisplayName()} won.");
                break;
            }

            return reactions;
        }

        /// <summary>
        /// Resolves the faction whose completed action may have satisfied victory requirements.
        /// </summary>
        /// <param name="result">The completed game result.</param>
        /// <returns>The affected victory candidate, or null for unrelated or ambiguous results.</returns>
        private Faction ResolveCandidate(GameResult result)
        {
            if (result is HeadquartersLostResult headquarters)
            {
                return _queries.GetFaction(headquarters.Attacker?.InstanceID)
                    ?? _queries.GetSoleOpponent(headquarters.Defender);
            }

            if (result is not OfficerCaptureStateResult { IsCaptured: true } capture)
                return null;

            return _queries.GetFaction(
                !string.IsNullOrEmpty(capture.CaptorInstanceID)
                    ? capture.CaptorInstanceID
                    : capture.TargetOfficer?.CaptorInstanceID
            );
        }
    }
}
