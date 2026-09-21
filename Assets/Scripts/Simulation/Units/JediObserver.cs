using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes mission completions to Force growth in batch order.</summary>
    public sealed class JediObserver
    {
        private readonly JediCommands _commands;

        /// <summary>Creates the jedi result listener.</summary>
        /// <param name="commands">The jedi operations for this game.</param>
        public JediObserver(JediCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Applies Force growth for successful missions reported in a result batch.
        /// </summary>
        /// <param name="results">The result batch to inspect.</param>
        /// <returns>Any Force experience results produced by successful missions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<MissionCompletedResult> results)
        {
            List<GameResult> forceResults = new List<GameResult>();
            if (results == null)
                return forceResults;

            foreach (
                MissionCompletedResult result in results.Where(result =>
                    result.Outcome == MissionOutcome.Success
                    && result.Mission?.GetMainParticipants() != null
                )
            )
            {
                forceResults.AddRange(
                    _commands.ApplyForceGrowth(result.Mission.GetMainParticipants())
                );
            }

            return forceResults;
        }
    }
}
