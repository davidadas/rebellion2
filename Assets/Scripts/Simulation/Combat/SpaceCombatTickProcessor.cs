using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
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
        /// Resolves automatic encounters and pauses at the next player decision.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The combat results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            if (_commands.HasPendingDecision)
                return results;

            HashSet<string> resolvedFleetIds = new HashSet<string>();
            while (
                _commands.TryBeginFleetCombat(resolvedFleetIds, out SpaceCombatDecision decision)
            )
            {
                if (_commands.TryAutoResolveAICombat(decision, resolvedFleetIds, results))
                    continue;

                results.Add(_commands.DeferCombatDecision(decision));
                break;
            }

            return results;
        }
    }
}
