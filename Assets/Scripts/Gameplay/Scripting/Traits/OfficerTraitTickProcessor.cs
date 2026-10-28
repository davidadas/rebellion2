using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay.Scripting.Traits
{
    /// <summary>
    /// Runs shared trait scripts before AI planning begins.
    /// </summary>
    internal sealed class OfficerTraitTickProcessor : ITickProcessor
    {
        private readonly OfficerTraitCommands _commands;

        /// <summary>
        /// Creates tick processing for shared trait scripts.
        /// </summary>
        /// <param name="commands">The trait script activation operations.</param>
        public OfficerTraitTickProcessor(OfficerTraitCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Applies shared trait scripts after scheduled events during this tick.
        /// </summary>
        /// <param name="game">The current game's state.</param>
        /// <returns>The produced trait-script results.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return _commands.ProcessTick();
        }
    }
}
