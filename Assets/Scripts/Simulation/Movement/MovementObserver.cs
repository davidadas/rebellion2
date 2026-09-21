using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Selects newly blockaded destinations and requests their inbound-unit reactions.
    /// </summary>
    public sealed class MovementObserver
    {
        private readonly MovementCommands _commands;

        /// <summary>
        /// Connects blockade observation to movement execution.
        /// </summary>
        /// <param name="commands">The movement operations that handle inbound units.</param>
        public MovementObserver(MovementCommands commands)
        {
            _commands = commands;
        }

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
