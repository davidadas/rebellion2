using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Routes completed gameplay results to officer-loyalty operations.
    /// </summary>
    public sealed class OfficerLoyaltyObserver : IResultObserver, IDisposable
    {
        private readonly OfficerLoyaltyCommands _commands;
        private IDisposable[] _subscriptions;

        /// <summary>Creates the officer-loyalty result observer.</summary>
        /// <param name="commands">The officer-loyalty operations for the active game.</param>
        public OfficerLoyaltyObserver(OfficerLoyaltyCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Registers officer-loyalty callbacks with the result bus.</summary>
        /// <param name="results">The bus that delivers completed gameplay results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException(
                    "Officer loyalty observer is already connected."
                );
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<SpaceCombatResult>(HandleResults),
                results.Subscribe<PlanetaryAssaultResult>(HandleResults),
                results.Subscribe<BombardmentResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving gameplay results.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>Routes completed space combats to officer-loyalty operations.</summary>
        /// <param name="results">The completed space combats.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<SpaceCombatResult> results)
        {
            foreach (SpaceCombatResult result in results ?? Array.Empty<SpaceCombatResult>())
                _commands.ApplyBattleLoss(result);

            return new List<GameResult>();
        }

        /// <summary>Routes completed planetary assaults to officer-loyalty operations.</summary>
        /// <param name="results">The completed planetary assaults.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetaryAssaultResult> results)
        {
            foreach (
                PlanetaryAssaultResult result in results ?? Array.Empty<PlanetaryAssaultResult>()
            )
            {
                _commands.ApplyBattleLoss(result);
            }

            return new List<GameResult>();
        }

        /// <summary>Routes completed bombardments to officer-loyalty operations.</summary>
        /// <param name="results">The completed bombardments.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BombardmentResult> results)
        {
            foreach (BombardmentResult result in results ?? Array.Empty<BombardmentResult>())
                _commands.ApplyBattleLoss(result);

            return new List<GameResult>();
        }
    }
}
