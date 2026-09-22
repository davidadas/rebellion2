using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Selects missions interrupted by capture before custody changes participant parents.</summary>
    public sealed class MissionObserver : IDisposable
    {
        private readonly MissionCommands _commands;
        private readonly IDisposable _subscription;

        /// <summary>Creates the mission capture listener.</summary>
        /// <param name="commands">The operations that interrupt selected missions.</param>
        /// <param name="results">The bus that delivers officer capture changes.</param>
        public MissionObserver(MissionCommands commands, GameResultBus results)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<OfficerCaptureStateResult>(HandleResults);
        }

        /// <summary>Stops receiving officer capture changes.</summary>
        public void Dispose() => _subscription.Dispose();

        /// <summary>
        /// Ends missions whose participants were captured by another simulation system.
        /// </summary>
        /// <param name="results">The officer capture-state changes to process.</param>
        /// <returns>Mission interruption results produced while tearing down affected missions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> missionResults = new List<GameResult>();
            List<Mission> affectedMissions = results
                .Where(result => result?.IsCaptured == true)
                .Select(result => result.TargetOfficer?.GetParent() as Mission)
                .Where(mission => mission != null)
                .Distinct()
                .ToList();

            foreach (Mission mission in affectedMissions)
                _commands.InterruptMission(mission, missionResults);

            return missionResults;
        }
    }
}
