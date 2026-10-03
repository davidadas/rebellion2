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
                    results.AddRange(AdvanceMission(game, mission));
            }

            _commands.AddRecruitmentExhaustedResults(results, recruitmentAvailabilityBefore);
            return results;
        }

        /// <summary>
        /// Advances one attached mission through its current lifecycle step.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="mission">The mission to advance.</param>
        /// <returns>The results produced while advancing the mission.</returns>
        private List<GameResult> AdvanceMission(GameRoot game, Mission mission)
        {
            if (mission == null || mission.GetParent() == null)
                return new List<GameResult>();

            if (mission.IsWaitingForParticipants())
                return new List<GameResult>();

            List<GameResult> results = mission.Execute(game, _commands.RandomProvider, _commands);
            foreach (GameResult result in results)
                mission.SetResultMissionID(result);

            return results;
        }
    }
}
