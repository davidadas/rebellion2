using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Reconciles missions after participant capture or death results are published.
    /// </summary>
    internal sealed class MissionObserver : IResultObserver, IDisposable
    {
        private readonly MissionResolver _resolver;
        private IDisposable[] _subscriptions;

        /// <summary>
        /// Creates the mission participant-state listener.
        /// </summary>
        /// <param name="resolver">The lifecycle resolver that interrupts selected missions.</param>
        public MissionObserver(MissionResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Registers the capture and death callbacks with the result bus.
        /// </summary>
        /// <param name="results">The bus that delivers officer capture and death changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Mission observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<OfficerCaptureStateResult>(HandleResults),
                results.Subscribe<OfficerKilledResult>(HandleResults),
            };
        }

        /// <summary>
        /// Stops receiving officer capture and death changes.
        /// </summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Ends missions whose participants were captured by another simulation system.
        /// </summary>
        /// <param name="results">The officer capture-state changes to process.</param>
        /// <returns>Mission interruption results produced while tearing down affected missions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            IEnumerable<Mission> affectedMissions = results
                .Where(result => result?.IsCaptured == true && result.TargetOfficer != null)
                .Select(result => result.ParentAtCapture as Mission);

            return InterruptMissions(affectedMissions);
        }

        /// <summary>
        /// Removes officers killed by another simulation system and reevaluates their missions.
        /// </summary>
        /// <param name="results">The officer deaths to process.</param>
        /// <returns>Results produced while relocating officers or interrupting invalid missions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerKilledResult> results)
        {
            List<GameResult> missionResults = new List<GameResult>();
            IEnumerable<IGrouping<Mission, OfficerKilledResult>> affectedMissions = results
                .Where(result => result?.TargetOfficer?.IsKilled == true)
                .Where(result => result.ParentAtDeath is Mission)
                .GroupBy(result => (Mission)result.ParentAtDeath)
                .Where(group => group.Key.GetParent() != null);

            foreach (IGrouping<Mission, OfficerKilledResult> affectedMission in affectedMissions)
            {
                _resolver.RemoveKilledParticipants(
                    affectedMission.Key,
                    affectedMission.Select(result => result.TargetOfficer).Distinct().ToList(),
                    missionResults
                );
            }

            return missionResults;
        }

        /// <summary>
        /// Interrupts each distinct mission that is still attached to the game.
        /// </summary>
        /// <param name="missions">The recorded participant missions.</param>
        /// <returns>Mission interruption results produced while tearing down affected missions.</returns>
        private List<GameResult> InterruptMissions(IEnumerable<Mission> missions)
        {
            List<GameResult> missionResults = new List<GameResult>();
            List<Mission> affectedMissions = missions
                .Where(mission => mission?.GetParent() != null)
                .Distinct()
                .ToList();

            foreach (Mission mission in affectedMissions)
                _resolver.InterruptMission(mission, missionResults);

            return missionResults;
        }
    }
}
