using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Routes headquarters losses to victory resolution in batch order.</summary>
    public sealed class VictoryObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly VictoryCommands _commands;
        private IDisposable _subscription;

        /// <summary>Creates the victory result listener.</summary>
        /// <param name="game">The active game containing the configured victory condition.</param>
        /// <param name="commands">The general victory declaration operation.</param>
        public VictoryObserver(GameRoot game, VictoryCommands commands)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
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
                    || !MeetsConfiguredVictoryCondition(result.Defender)
                )
                    continue;

                _commands.TryDeclareVictory(result.Attacker, result.Defender);
                if (_commands.IsDeclared)
                    break;
            }

            return new List<GameResult>();
        }

        /// <summary>Determines whether an headquarters loss satisfies the selected game mode.</summary>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <returns>True when the loss may declare victory.</returns>
        private bool MeetsConfiguredVictoryCondition(Faction defender)
        {
            return _game.Summary.VictoryCondition != GameVictoryCondition.Conquest
                || _game
                    .GetSceneNodesByType<Officer>()
                    .Where(officer =>
                        officer.GetOwnerInstanceID() == defender.InstanceID && officer.IsMain
                    )
                    .All(officer => officer.IsCaptured);
        }
    }
}
