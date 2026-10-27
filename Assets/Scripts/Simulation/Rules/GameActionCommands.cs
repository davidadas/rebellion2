using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Rules;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Executes authored game actions and their deferred command requests.
    /// </summary>
    public sealed partial class GameActionCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly UnitFactory _unitFactory;
        private readonly MovementCommands _movementCommands;
        private readonly PlanetaryControlCommands _planetaryControlCommands;
        private readonly DuelCommands _duelCommands;
        private readonly MessageCommands _messageCommands;
        private readonly MissionResolver _missionResolver;
        private readonly BlockadeTransitionResolver _blockadeTransitionResolver;

        /// <summary>
        /// Creates a new GameActionCommands.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for stochastic game actions.</param>
        /// <param name="unitFactory">Factory for actions that create runtime units.</param>
        /// <param name="movementCommands">The movement and placement operations.</param>
        /// <param name="planetaryControlCommands">The ownership operations.</param>
        /// <param name="duelCommands">The officer encounter operations.</param>
        /// <param name="messageCommands">The authored message delivery operations.</param>
        public GameActionCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            UnitFactory unitFactory = null,
            MovementCommands movementCommands = null,
            PlanetaryControlCommands planetaryControlCommands = null,
            DuelCommands duelCommands = null,
            MessageCommands messageCommands = null
        )
            : this(
                game,
                provider,
                unitFactory,
                movementCommands,
                planetaryControlCommands,
                duelCommands,
                messageCommands,
                missionResolver: null,
                blockadeTransitionResolver: null
            ) { }

        /// <summary>
        /// Creates game-action execution with registered mission lifecycle handling.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for stochastic game actions.</param>
        /// <param name="unitFactory">Factory for actions that create runtime units.</param>
        /// <param name="movementCommands">The movement and placement operations.</param>
        /// <param name="planetaryControlCommands">The ownership operations.</param>
        /// <param name="duelCommands">The officer encounter operations.</param>
        /// <param name="messageCommands">The authored message delivery operations.</param>
        /// <param name="missionResolver">The mission lifecycle resolver.</param>
        /// <param name="blockadeTransitionResolver">Resolves blockade changes after game actions.</param>
        internal GameActionCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            UnitFactory unitFactory,
            MovementCommands movementCommands,
            PlanetaryControlCommands planetaryControlCommands,
            DuelCommands duelCommands,
            MessageCommands messageCommands,
            MissionResolver missionResolver,
            BlockadeTransitionResolver blockadeTransitionResolver
        )
        {
            _game = game;
            _provider = provider;
            _unitFactory = unitFactory;
            _movementCommands = movementCommands;
            _planetaryControlCommands = planetaryControlCommands;
            _duelCommands = duelCommands;
            _messageCommands = messageCommands;
            _missionResolver = missionResolver;
            _blockadeTransitionResolver =
                blockadeTransitionResolver ?? new BlockadeTransitionResolver(game);
        }

        /// <summary>
        /// Interprets an action batch, then executes its queued commands in authored order.
        /// </summary>
        /// <param name="actions">The authored action batch.</param>
        /// <param name="evaluation">Bindings, trigger data, and source attribution for this execution.</param>
        /// <returns>Direct results followed by results from deferred command requests.</returns>
        public List<GameResult> Execute(
            IEnumerable<GameAction> actions,
            GameEvaluationContext evaluation
        )
        {
            GameActionContext context = new GameActionContext(
                _game,
                _provider,
                evaluation,
                _unitFactory,
                _missionResolver,
                _blockadeTransitionResolver
            );
            ExecuteActions(actions, context);
            List<GameResult> results = new List<GameResult>(context.Results);
            if (context.DeferredOperations.Count > 0)
            {
                if (
                    _movementCommands == null
                    && _planetaryControlCommands == null
                    && _duelCommands == null
                    && _messageCommands == null
                )
                    throw new InvalidOperationException(
                        $"{evaluation?.SourceKind ?? "Rule"} '{evaluation?.SourceID}' produced deferred work without configured commands."
                    );
                results.AddRange(ExecuteDeferred(context));
            }
            return results;
        }

        /// <summary>
        /// Executes resolved command calls in authored order, retaining per-operation failure containment.
        /// </summary>
        /// <param name="context">The activation containing the queued calls.</param>
        /// <returns>The completed operations' facts, without adding them to action-local evaluation.</returns>
        private List<GameResult> ExecuteDeferred(GameActionContext context)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (var operation in context.DeferredOperations)
            {
                try
                {
                    List<GameResult> produced = operation.Execute(
                        this,
                        operation.SourceEventInstanceID
                    );
                    if (produced == null)
                        continue;
                    foreach (GameResult result in produced.Where(result => result != null))
                    {
                        if (string.IsNullOrWhiteSpace(result.SourceEventInstanceID))
                            result.SourceEventInstanceID = operation.SourceEventInstanceID;
                        results.Add(result);
                    }
                }
                catch (Exception exception)
                {
                    string eventInstanceID = operation.SourceEventInstanceID ?? "unknown";
                    GameLogger.Log(
                        $"{context.Evaluation?.SourceKind ?? "Event"} '{eventInstanceID}' deferred action '{operation.Name}' failed: {exception}",
                        GameLogger.LogLevel.Error
                    );
                }
            }
            return results;
        }
    }
}
