using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Selects newly blockaded destinations and requests their inbound-unit reactions.
    /// </summary>
    public sealed class MovementObserver : IDisposable
    {
        private readonly MovementCommands _commands;
        private readonly IDisposable _subscription;

        /// <summary>
        /// Connects blockade observation to movement execution.
        /// </summary>
        /// <param name="commands">The movement operations that handle inbound units.</param>
        /// <param name="results">The bus that delivers blockade changes.</param>
        public MovementObserver(MovementCommands commands, GameResultBus results)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<BlockadeChangedResult>(HandleResults);
        }

        /// <summary>Stops receiving blockade changes.</summary>
        public void Dispose() => _subscription.Dispose();

        /// <summary>
        /// Applies movement reactions to newly started blockades.
        /// </summary>
        /// <param name="results">The blockade changes to inspect.</param>
        /// <returns>The movement and destruction results caused by blockade starts.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BlockadeChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> handledPlanets = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlockadeChangedResult result in results)
            {
                if (
                    result?.Blockaded != true
                    || result.Planet == null
                    || result.BlockadingFleet == null
                    || !result.Planet.IsBlockaded()
                    || !handledPlanets.Add(result.Planet.InstanceID)
                )
                    continue;

                _commands.HandleBlockadeStarted(result, reactions);
            }

            return reactions;
        }
    }
}
