using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Reconciles missions after participant capture, death, injury, or destruction results are published.
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
        /// Registers participant-state callbacks with the result bus.
        /// </summary>
        /// <param name="results">The bus that delivers participant-state changes.</param>
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
                results.Subscribe<OfficerInjuredResult>(HandleResults),
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
            };
        }

        /// <summary>
        /// Stops receiving participant-state changes.
        /// </summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Removes captured participants and reevaluates each mission's remaining team.
        /// </summary>
        /// <param name="results">The officer capture-state changes to process.</param>
        /// <returns>Participant relocation and mission interruption results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> missionResults = new();
            IEnumerable<IGrouping<Mission, OfficerCaptureStateResult>> affectedMissions = results
                .Where(result =>
                    result?.IsCaptured == true && result.TargetOfficer?.IsCaptured == true
                )
                .Where(result => result.ParentAtCapture is Mission)
                .GroupBy(result => (Mission)result.ParentAtCapture)
                .Where(group => group.Key.GetParent() != null);

            foreach (
                IGrouping<Mission, OfficerCaptureStateResult> affectedMission in affectedMissions
            )
                _resolver.RemoveUnavailableParticipants(
                    affectedMission.Key,
                    affectedMission.Select(result => result.TargetOfficer).Distinct().ToList(),
                    missionResults
                );

            return missionResults;
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
                _resolver.RemoveUnavailableParticipants(
                    affectedMission.Key,
                    affectedMission.Select(result => result.TargetOfficer).Distinct().ToList(),
                    missionResults
                );
            }

            return missionResults;
        }

        /// <summary>
        /// Withdraws injured officers and reevaluates each recorded mission's remaining team.
        /// Recovery notices do not remove participants.
        /// </summary>
        /// <param name="results">The officer injury changes to process.</param>
        /// <returns>The withdrawal and mission interruption results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerInjuredResult> results)
        {
            List<GameResult> missionResults = new();
            IEnumerable<IGrouping<Mission, OfficerInjuredResult>> affectedMissions = results
                .Where(result => result?.Severity > 0 && result.Officer != null)
                .Where(result => result.ParentAtInjury is Mission)
                .GroupBy(result => (Mission)result.ParentAtInjury)
                .Where(group => group.Key.GetParent() != null);

            foreach (IGrouping<Mission, OfficerInjuredResult> affectedMission in affectedMissions)
                _resolver.WithdrawInjuredParticipants(
                    affectedMission.Key,
                    affectedMission.Select(result => result.Officer).Distinct().ToList(),
                    missionResults
                );

            return missionResults;
        }

        /// <summary>
        /// Reevaluates the mission that directly contained each destroyed special-forces unit
        /// before its scene removal.
        /// </summary>
        /// <param name="results">The unit-destruction changes to process.</param>
        /// <returns>Results produced while interrupting missions that lost their last primary participant.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectDestroyedResult> results)
        {
            List<GameResult> missionResults = new();
            IEnumerable<IGrouping<Mission, SpecialForces>> affectedMissions = (
                results ?? Array.Empty<GameObjectDestroyedResult>()
            )
                .Select(result => result?.DestroyedObject)
                .OfType<SpecialForces>()
                .Select(participant =>
                    (participant, mission: participant.GetLastParent() as Mission)
                )
                .Where(affected => affected.mission != null)
                .GroupBy(affected => affected.mission, affected => affected.participant);

            foreach (IGrouping<Mission, SpecialForces> affectedMission in affectedMissions)
            {
                _resolver.ReconcileDestroyedSpecialForces(
                    affectedMission.Key,
                    affectedMission.Distinct().ToList(),
                    missionResults
                );
            }
            return missionResults;
        }
    }
}
