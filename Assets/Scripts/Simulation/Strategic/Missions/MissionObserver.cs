using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Selects missions interrupted by capture from the recorded participant parent.</summary>
    internal sealed class MissionObserver : IResultObserver, IDisposable
    {
        private readonly MissionResolver _resolver;
        private IDisposable _subscription;

        /// <summary>Creates the mission capture listener.</summary>
        /// <param name="resolver">The lifecycle resolver that interrupts selected missions.</param>
        public MissionObserver(MissionResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>Registers the capture callback with the result bus.</summary>
        /// <param name="results">The bus that delivers officer capture changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Mission observer is already connected.");
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<OfficerCaptureStateResult>(HandleResults);
        }

        /// <summary>Stops receiving officer capture changes.</summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Ends missions whose participants were captured by another simulation system.
        /// </summary>
        /// <param name="results">The officer capture-state changes to process.</param>
        /// <returns>Mission interruption results produced while tearing down affected missions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> missionResults = new List<GameResult>();
            List<Mission> affectedMissions = results
                .Where(result => result?.IsCaptured == true && result.TargetOfficer != null)
                .Select(result => result.ParentAtCapture as Mission)
                .Where(mission => mission?.GetParent() != null)
                .Distinct()
                .ToList();

            foreach (Mission mission in affectedMissions)
                _resolver.InterruptMission(mission, missionResults);

            return missionResults;
        }
    }
}
