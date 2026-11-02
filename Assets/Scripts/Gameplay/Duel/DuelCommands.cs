using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Completes officer comparisons and their authored reactions before the next operation runs.
    /// </summary>
    public sealed class DuelCommands
    {
        private readonly DuelResolver _resolver;
        private readonly Func<DuelResult, List<GameResult>> _completeEncounter;

        /// <summary>
        /// Creates comparison execution with its synchronous authored completion handler.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="random">The deterministic simulation random source.</param>
        /// <param name="completeEncounter">Executes scripts for the completed comparison.</param>
        /// <param name="jediCommands">The Force progression operations.</param>
        public DuelCommands(
            GameRoot game,
            IRandomNumberProvider random,
            Func<DuelResult, List<GameResult>> completeEncounter,
            JediCommands jediCommands = null
        )
        {
            _resolver = new DuelResolver(game, random, jediCommands);
            _completeEncounter =
                completeEncounter ?? throw new ArgumentNullException(nameof(completeEncounter));
        }

        /// <summary>
        /// Resolves a comparison and applies its authored effects before returning its consequences.
        /// The comparison context is consumed by its completion handler rather than published again.
        /// </summary>
        /// <param name="encountered">The officer facing capture or evasion.</param>
        /// <param name="opposing">The opposing officer.</param>
        /// <param name="imagePath">The image accompanying the duel report.</param>
        /// <param name="audioPath">The audio accompanying the duel report.</param>
        /// <param name="sourceEventInstanceID">The authored event initiating the encounter, when applicable.</param>
        /// <returns>The capture, injury, progression, and authored reaction results.</returns>
        public List<GameResult> Resolve(
            Officer encountered,
            Officer opposing,
            string imagePath = null,
            string audioPath = null,
            string sourceEventInstanceID = null
        )
        {
            List<GameResult> results = _resolver.Resolve(
                encountered,
                opposing,
                imagePath,
                audioPath,
                sourceEventInstanceID
            );
            DuelResult encounter = results.OfType<DuelResult>().SingleOrDefault();
            if (encounter == null)
                return results;

            results.Remove(encounter);
            results.AddRange(_completeEncounter(encounter));
            return results;
        }
    }
}
