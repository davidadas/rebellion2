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
    public class JediCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly Dictionary<string, bool> _growthEligibilityByTypeID = new();

        /// <summary>
        /// Creates a new JediCommands.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for initial Force ratings.</param>
        /// <param name="officerTemplates">Authored growth policies used by saves without an explicit override.</param>
        public JediCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            IEnumerable<Officer> officerTemplates = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            foreach (Officer template in officerTemplates ?? Array.Empty<Officer>())
                if (
                    template?.ForceGrowthRequiresEligibility is bool requiresEligibility
                    && !string.IsNullOrEmpty(template.TypeID)
                )
                    _growthEligibilityByTypeID.Add(template.TypeID, requiresEligibility);
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
            if (
                growth <= 0
                || officer?.GrowsForceOnMission != true
                || (RequiresForceEligibility(officer) && !officer.IsForceEligible)
            )
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
        /// Resolves the unit's growth policy, inheriting authored defaults for older saves.
        /// Units without an authored override retain the eligibility requirement.
        /// </summary>
        /// <param name="officer">The officer receiving a capture-evasion reward.</param>
        /// <returns>Whether the reward requires Force eligibility.</returns>
        private bool RequiresForceEligibility(Officer officer)
        {
            if (officer.ForceGrowthRequiresEligibility.HasValue)
                return officer.ForceGrowthRequiresEligibility.Value;

            return string.IsNullOrEmpty(officer.TypeID)
                || !_growthEligibilityByTypeID.TryGetValue(
                    officer.TypeID,
                    out bool requiresEligibility
                )
                || requiresEligibility;
        }

        /// <summary>
        /// Sets or clears IsDiscoveringForceUser based on whether a Jedi trainer meets the
        /// ForceRank threshold and is available to scan.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="results">Collection to append any discovery state change results to.</param>
        internal void UpdateForceDiscoveryState(Officer officer, List<GameResult> results)
        {
            int threshold = _game.Config.Jedi.DiscoveringForceUserThreshold;
            bool shouldDiscover =
                officer.IsJediTrainer
                && officer.ForceRank >= threshold
                && officer.IsActive()
                && ((IMovable)officer).GetTransitMovement() == null
                && !officer.IsCaptured
                && !officer.IsKilled
                && !officer.IsOnMission();

            if (shouldDiscover && !officer.IsDiscoveringForceUser)
            {
                officer.IsDiscoveringForceUser = true;
                officer.NextForceDiscoveryTick = _game.CurrentTick + 1;

                results.Add(
                    new ForceDiscoveryResult
                    {
                        EventType = ForceEventType.DiscoveringForceUser,
                        Officer = officer,
                        ForceRank = officer.ForceRank,
                        Tick = _game.CurrentTick,
                    }
                );

                GameLogger.Log(
                    $"{officer.GetDisplayName()} is discovering Force abilities (rank {officer.ForceRank})"
                );
            }
            else if (!shouldDiscover && officer.IsDiscoveringForceUser)
            {
                officer.IsDiscoveringForceUser = false;
                officer.NextForceDiscoveryTick = 0;
            }
        }

        /// <summary>
        /// Restores discovery eligibility and arms newly available scanners when a session starts.
        /// </summary>
        internal void ReconcileDiscoveryState()
        {
            List<GameResult> results = new List<GameResult>();
            foreach (
                Officer officer in _game.GetRegisteredSceneNodesByType<Officer>(
                    includeDisabled: true
                )
            )
                UpdateForceDiscoveryState(officer, results);
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
            foreach (Officer candidate in officers)
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

            foreach (Officer scanner in officers)
                if (
                    scanner.IsDiscoveringForceUser
                    && _game.CurrentTick >= scanner.NextForceDiscoveryTick
                )
                {
                    ISceneNode location = scanner.GetParent();
                    if (
                        location != null
                        && candidatesByLocation.TryGetValue(location, out List<Officer> candidates)
                    )
                        ScanScannerLocation(scanner, candidates, results);
                    scanner.NextForceDiscoveryTick = _game.CurrentTick + 1;
                }
        }

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
                || candidate.IsForceEligible
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
