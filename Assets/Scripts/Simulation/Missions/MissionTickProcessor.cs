using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances active missions during a game tick.
    /// </summary>
    internal sealed class MissionTickProcessor : ITickProcessor
    {
        private readonly MissionCommands _commands;

        /// <summary>
        /// Creates mission tick processing.
        /// </summary>
        /// <param name="commands">The mission lifecycle operations and pending results.</param>
        public MissionTickProcessor(MissionCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Advances every active mission.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The mission results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = _commands.TakePendingResults();
            Dictionary<string, bool> recruitmentAvailabilityBefore =
                _commands.GetRecruitmentAvailabilityByFaction();

            foreach (Mission mission in game.GetSceneNodesByType<Mission>())
            {
                if (mission.GetParent() != null)
                    results.AddRange(_commands.UpdateMission(mission));
            }

            _commands.AddRecruitmentExhaustedResults(results, recruitmentAvailabilityBefore);
            return results;
        }
    }
}
