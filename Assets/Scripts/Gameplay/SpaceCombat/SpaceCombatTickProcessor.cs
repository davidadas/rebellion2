using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Detects and resolves space-combat encounters during a game tick.
    /// </summary>
    internal sealed class SpaceCombatTickProcessor : ITickProcessor
    {
        private readonly SpaceCombatCommands _commands;

        /// <summary>
        /// Creates space-combat tick processing.
        /// </summary>
        /// <param name="commands">The space-combat operations and pending decision state.</param>
        public SpaceCombatTickProcessor(SpaceCombatCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Collects encounter batches for callers that own their subsequent result delivery.
        /// Tick orchestration settles the lazy batches instead of collecting them in advance.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The encounter results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new();
            foreach (IReadOnlyList<GameResult> encounter in ProcessEncounters())
                results.AddRange(encounter);
            return results;
        }

        /// <summary>
        /// Resolves one encounter at a time so its consequences can settle before scanning again.
        /// </summary>
        /// <returns>The successive encounter batches, ending at the next player decision.</returns>
        internal IEnumerable<IReadOnlyList<GameResult>> ProcessEncounters()
        {
            if (_commands.HasPendingDecision)
                yield break;

            HashSet<string> resolvedFleetIds = new HashSet<string>();
            while (
                _commands.TryBeginFleetCombat(resolvedFleetIds, out SpaceCombatDecision decision)
            )
            {
                List<GameResult> results = new();
                if (_commands.TryAutoResolveAICombat(decision, resolvedFleetIds, results))
                {
                    yield return results;
                    continue;
                }

                results.Add(_commands.DeferCombatDecision(decision));
                yield return results;
                yield break;
            }
        }
    }
}
