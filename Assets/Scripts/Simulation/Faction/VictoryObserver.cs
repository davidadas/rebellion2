using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes headquarters losses to victory resolution in batch order.</summary>
    public sealed class VictoryObserver : IResultObserver, IDisposable
    {
        private readonly VictoryCommands _commands;
        private readonly VictoryQueries _queries;
        private IDisposable _subscription;

        /// <summary>Creates the victory result listener.</summary>
        /// <param name="commands">The general victory declaration operation.</param>
        /// <param name="queries">The configured victory eligibility rules.</param>
        public VictoryObserver(VictoryCommands commands, VictoryQueries queries)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>Registers the headquarters-loss callback with the result bus.</summary>
        /// <param name="results">The bus that delivers headquarters losses.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Victory observer is already connected.");
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<HeadquartersLostResult>(HandleResults);
        }

        /// <summary>Stops receiving headquarters losses.</summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Applies the configured victory condition after a faction loses its headquarters.
        /// </summary>
        /// <param name="results">The headquarters loss results.</param>
        /// <returns>Any victories caused by the headquarters losses.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<HeadquartersLostResult> results)
        {
            if (_commands.IsDeclared)
                return new List<GameResult>();

            foreach (
                HeadquartersLostResult result in results ?? Array.Empty<HeadquartersLostResult>()
            )
            {
                if (
                    result?.Attacker == null
                    || result.Defender == null
                    || !_queries.CanDeclareVictoryAfterHeadquartersLoss(result.Defender)
                )
                    continue;

                _commands.TryDeclareVictory(result.Attacker, result.Defender);
                if (_commands.IsDeclared)
                    break;
            }

            return new List<GameResult>();
        }
    }
}
