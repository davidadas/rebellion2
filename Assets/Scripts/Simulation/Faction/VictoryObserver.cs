using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes headquarters losses to victory resolution in batch order.</summary>
    public sealed class VictoryObserver
    {
        private readonly VictoryCommands _commands;

        /// <summary>Creates the victory result listener.</summary>
        /// <param name="commands">The victory operations for this game.</param>
        public VictoryObserver(VictoryCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Applies the configured victory condition after a faction loses its headquarters.
        /// </summary>
        /// <param name="results">The headquarters loss results.</param>
        /// <returns>Any victories caused by the headquarters losses.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<HeadquartersLostResult> results)
        {
            if (_commands.IsDeclared)
                return new List<GameResult>();

            return (results ?? Array.Empty<HeadquartersLostResult>())
                .Where(result => result?.Attacker != null && result.Defender != null)
                .Select(result =>
                    _commands.ResolveHeadquartersLoss(result.Attacker, result.Defender)
                )
                .Where(result => result != null)
                .Cast<GameResult>()
                .ToList();
        }
    }
}
