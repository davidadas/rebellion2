using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Applies Force discovery, growth, and force-user scanning operations.
    /// </summary>
    public class JediCommands : IDisposable
    {
        private static readonly IComparer<(int Tick, long Order, Officer Scanner)> _scanComparer =
            Comparer<(int Tick, long Order, Officer Scanner)>.Create(
                (left, right) =>
                {
                    int dueOrder = left.Tick.CompareTo(right.Tick);
                    return dueOrder != 0 ? dueOrder : left.Order.CompareTo(right.Order);
                }
            );

        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly List<GameResult> _pendingDiscoveryResults = new();

        /// <summary>
        /// Creates a new JediCommands.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for initial Force ratings.</param>
        public JediCommands(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _game.ForceDiscoveryStarted += RecordDiscoveryStarted;
        }

        /// <summary>
        /// Grants the configured Force increase after an eligible officer evades capture.
        /// </summary>
        /// <param name="officer">The officer that evaded capture.</param>
        /// <param name="sourceEventInstanceID">The authored event that initiated the encounter, when applicable.</param>
        /// <returns>The resulting Force experience, or null when the officer cannot gain Force experience.</returns>
        internal ForceExperienceResult ApplyCaptureEvasionGrowth(
            Officer officer,
            string sourceEventInstanceID = null
        )
        {
            int growth = _game.Config.Jedi.ForceGrowthPerMission;
            if (growth <= 0 || officer?.GrowsForceOnMission != true || !officer.IsForceEligible)
                return null;

            int previousForceRank = officer.ForceRank;
            officer.ForceValue += growth;
            GameLogger.Log(
                $"{officer.GetDisplayName()} gained {growth} ForceValue after evading capture (now {officer.ForceValue})"
            );
            return new ForceExperienceResult
            {
                Officer = officer,
                ExperienceGained = growth,
                PreviousForceRank = previousForceRank,
                CurrentForceRank = officer.ForceRank,
                SourceEventInstanceID = sourceEventInstanceID,
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Disconnects the model's discovery-transition producer from this runtime.
        /// </summary>
        public void Dispose()
        {
            _game.ForceDiscoveryStarted -= RecordDiscoveryStarted;
        }

        /// <summary>
        /// Takes the discovery transitions recorded since the preceding Force tick.
        /// </summary>
        /// <returns>The completed transitions in mutation order.</returns>
        internal List<GameResult> TakeDiscoveryResults()
        {
            List<GameResult> results = new(_pendingDiscoveryResults);
            _pendingDiscoveryResults.Clear();
            return results;
        }

        /// <summary>
        /// Records a model transition without postponing its timer or eligibility update.
        /// </summary>
        /// <param name="officer">The officer whose scanner was enabled.</param>
        private void RecordDiscoveryStarted(Officer officer)
        {
            _pendingDiscoveryResults.Add(
                new ForceDiscoveryResult
                {
                    EventType = ForceEventType.DiscoveringForceUser,
                    Officer = officer,
                    ForceRank = officer.ForceRank,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Restores discovery eligibility and arms newly available scanners when a session starts.
        /// </summary>
        internal void ReconcileDiscoveryState()
        {
            foreach (Officer officer in _game.RegisteredOfficers)
                officer.ReconcileForceDiscoveryState();
            _pendingDiscoveryResults.Clear();
        }

        /// <summary>
        /// Scans for hidden force users at each active scanner's location.
        /// </summary>
        /// <param name="officers">The officers evaluated during this tick.</param>
        /// <param name="results">Collection to append any discovered force user results to.</param>
        internal void ScanForHiddenForceUsers(
            IReadOnlyList<Officer> officers,
            List<GameResult> results
        )
        {
            Dictionary<ISceneNode, List<Officer>> candidatesByLocation =
                new Dictionary<ISceneNode, List<Officer>>();
            foreach (Officer candidate in officers.OrderBy(candidate => candidate.RosterOrder))
            {
                if (
                    !candidate.IsActive()
                    || !candidate.IsUndiscoveredForceUser()
                    || ((IMovable)candidate).GetTransitMovement() != null
                )
                    continue;
                ISceneNode location = candidate.GetParent();
                if (location == null)
                    continue;
                if (!candidatesByLocation.TryGetValue(location, out List<Officer> candidates))
                {
                    candidates = new List<Officer>();
                    candidatesByLocation.Add(location, candidates);
                }
                candidates.Add(candidate);
            }

            SortedSet<(int Tick, long Order, Officer Scanner)> scans = new(_scanComparer);
            foreach (Officer scanner in officers)
                if (scanner.IsDiscoveringForceUser)
                    scans.Add(
                        (scanner.NextForceDiscoveryTick, scanner.ForceDiscoveryOrder, scanner)
                    );

            while (scans.Count > 0)
            {
                (int Tick, long Order, Officer Scanner) scan = scans.Min;
                if (scan.Tick > _game.CurrentTick)
                    break;
                scans.Remove(scan);
                if (!IsCurrentDiscoveryScan(scan))
                    continue;

                ISceneNode location = scan.Scanner.GetParent();
                if (
                    location != null
                    && candidatesByLocation.TryGetValue(location, out List<Officer> candidates)
                )
                    ScanScannerLocation(scan.Scanner, candidates, results);
                if (!IsCurrentDiscoveryScan(scan))
                    continue;

                scan.Scanner.NextForceDiscoveryTick = unchecked(scan.Tick + 1);
                scan.Scanner.ForceDiscoveryOrder = _game.AllocateForceDiscoveryOrder();
                scans.Add(
                    (
                        scan.Scanner.NextForceDiscoveryTick,
                        scan.Scanner.ForceDiscoveryOrder,
                        scan.Scanner
                    )
                );
            }
        }

        /// <summary>
        /// Rejects scans canceled, replaced, or removed while an earlier scan was running.
        /// </summary>
        /// <param name="scan">The saved scheduling state captured for this due scan.</param>
        /// <returns>True when the registered officer still owns this active scan.</returns>
        private bool IsCurrentDiscoveryScan((int Tick, long Order, Officer Scanner) scan) =>
            scan.Scanner.IsDiscoveringForceUser
            && scan.Scanner.NextForceDiscoveryTick == scan.Tick
            && scan.Scanner.ForceDiscoveryOrder == scan.Order
            && ReferenceEquals(
                _game.GetSceneNodeByInstanceID<Officer>(
                    scan.Scanner.InstanceID,
                    includeDisabled: true
                ),
                scan.Scanner
            );

        /// <summary>
        /// Scans officers sharing the scanner's immediate container and faction.
        /// </summary>
        /// <param name="scanner">The scanning officer.</param>
        /// <param name="candidates">The candidates indexed at the scanner's location this tick.</param>
        /// <param name="results">Collection to append discovery results to.</param>
        private void ScanScannerLocation(
            Officer scanner,
            IReadOnlyList<Officer> candidates,
            List<GameResult> results
        )
        {
            foreach (Officer candidate in candidates)
            {
                if (CanDiscoverForceUser(scanner, candidate))
                    DiscoverForceUser(scanner, candidate, results);
            }
        }

        /// <summary>
        /// Checks the shared discovery requirements and the candidate's authored conditions.
        /// </summary>
        /// <param name="scanner">The scanning officer.</param>
        /// <param name="candidate">The hidden force user candidate.</param>
        /// <returns>True when the candidate is discovered.</returns>
        private bool CanDiscoverForceUser(Officer scanner, Officer candidate)
        {
            string scannerOwnerInstanceID = scanner.GetOwnerInstanceID();
            if (
                string.IsNullOrEmpty(scannerOwnerInstanceID)
                || candidate.GetOwnerInstanceID() != scannerOwnerInstanceID
                || candidate == scanner
                || !candidate.IsUndiscoveredForceUser()
                || ((IMovable)candidate).GetTransitMovement() != null
            )
                return false;

            if (candidate.ForceDiscoveryConditionals.Count == 0)
                return true;

            GameEvaluationContext evaluation = new GameEvaluationContext(candidate.InstanceID);
            evaluation.Bind("$discoverer", new[] { scanner });
            GameConditionContext context = new GameConditionContext(_game, evaluation);
            return candidate.ForceDiscoveryConditionals.All(condition =>
                GameConditionEvaluator.IsMet(condition, context)
            );
        }

        /// <summary>
        /// Activates a discovered force user and records the discovery.
        /// </summary>
        /// <param name="scanner">The scanning officer.</param>
        /// <param name="candidate">The discovered force user.</param>
        /// <param name="results">Collection to append discovery results to.</param>
        private void DiscoverForceUser(Officer scanner, Officer candidate, List<GameResult> results)
        {
            int previousForceRank = candidate.ForceRank;
            candidate.IsForceEligible = true;
            candidate.InitializeForceValue(_provider);
            int currentForceRank = candidate.ForceRank;

            results.Add(
                new ForceExperienceResult
                {
                    Officer = candidate,
                    ExperienceGained = Math.Max(0, currentForceRank - previousForceRank),
                    PreviousForceRank = previousForceRank,
                    CurrentForceRank = currentForceRank,
                    Tick = _game.CurrentTick,
                }
            );

            results.Add(
                new ForceDiscoveryResult
                {
                    EventType = ForceEventType.ForceUserDiscovered,
                    Officer = candidate,
                    Discoverer = scanner,
                    ForceRank = candidate.ForceRank,
                    Tick = _game.CurrentTick,
                }
            );

            GameLogger.Log(
                $"{scanner.GetDisplayName()} discovered {candidate.GetDisplayName()}'s Force potential (rank {candidate.ForceRank})"
            );
        }
    }
}
