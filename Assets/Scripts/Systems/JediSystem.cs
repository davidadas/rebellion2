using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages Force discovery state and force user scanning each tick.
    /// </summary>
    public class JediSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a new JediSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for scan rolls.</param>
        public JediSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;
        }

        /// <summary>
        /// Processes Force tier advancement and detection for all officers each tick.
        /// </summary>
        /// <returns>Any force discovery or experience results generated.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            // Update discovery state for all officers.
            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (!officer.IsJedi || !officer.IsForceEligible)
                    continue;

                UpdateForceDiscoveryState(officer, results);
            }
            // Scan for hidden force users at each active scanner's location.
            ScanForHiddenForceUsers(results);

            return results;
        }

        /// <summary>
        /// Grants ForceGrowthPerMission to eligible main participants of a successful mission.
        /// Called from GameManager.ProcessResults when a MissionCompletedResult with Success is seen.
        /// </summary>
        /// <param name="participants">The mission participants to update.</param>
        /// <returns>Any force experience results generated.</returns>
        public List<GameResult> ApplyForceGrowth(List<IMissionParticipant> participants)
        {
            List<GameResult> results = new List<GameResult>();
            int growth = _game.Config.Jedi.ForceGrowthPerMission;
            if (growth <= 0)
                return results;

            // Only main participants gain Force from missions.
            foreach (IMissionParticipant participant in participants)
            {
                if (
                    participant is Officer officer
                    && officer.GrowsForceOnMission
                    && officer.IsForceEligible
                )
                {
                    // ForceValue can exceed the normal max from discovery, but ForceRank cannot.
                    officer.ForceValue += growth;
                    results.Add(
                        new ForceExperienceResult
                        {
                            Officer = officer,
                            ExperienceGained = growth,
                            Tick = _game.CurrentTick,
                        }
                    );
                    GameLogger.Log(
                        $"{officer.GetDisplayName()} gained {growth} ForceValue from mission success (now {officer.ForceValue})"
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Sets or clears IsDiscoveringForceUser based on whether the officer meets the
        /// ForceRank threshold and is available to scan.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="results">Collection to append any discovery state change results to.</param>
        private void UpdateForceDiscoveryState(Officer officer, List<GameResult> results)
        {
            int threshold = _game.Config.Jedi.DiscoveringForceUserThreshold;
            bool shouldDiscover =
                officer.ForceRank >= threshold
                && !officer.IsCaptured
                && !officer.IsKilled
                && !officer.IsOnMission();

            if (shouldDiscover && !officer.IsDiscoveringForceUser)
            {
                officer.IsDiscoveringForceUser = true;

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
            }
        }

        /// <summary>
        /// Scans for hidden force users at each active scanner's location.
        /// </summary>
        /// <param name="results">Collection to append any discovered force user results to.</param>
        private void ScanForHiddenForceUsers(List<GameResult> results)
        {
            List<Officer> scanners = GetActiveForceScanners();
            if (scanners.Count == 0)
                return;

            foreach (Officer scanner in scanners)
                ScanScannerLocation(scanner, results);
        }

        /// <summary>
        /// Gets officers currently scanning for hidden force users.
        /// </summary>
        /// <returns>Active force scanners.</returns>
        private List<Officer> GetActiveForceScanners()
        {
            return _game
                .GetSceneNodesByType<Officer>()
                .Where(o => o.IsDiscoveringForceUser)
                .ToList();
        }

        /// <summary>
        /// Scans one officer's current planet for hidden force users.
        /// </summary>
        /// <param name="scanner">The scanning officer.</param>
        /// <param name="results">Collection to append discovery results to.</param>
        private void ScanScannerLocation(Officer scanner, List<GameResult> results)
        {
            Planet planet = scanner.GetParentOfType<Planet>();
            if (planet == null)
                return;

            foreach (Officer candidate in planet.GetChildren<Officer>(_ => true, recurse: true))
            {
                if (CanDiscoverForceUser(scanner, candidate))
                    DiscoverForceUser(scanner, candidate, results);
            }
        }

        /// <summary>
        /// Returns true when a scanner discovers a candidate this tick.
        /// </summary>
        /// <param name="scanner">The scanning officer.</param>
        /// <param name="candidate">The hidden force user candidate.</param>
        /// <returns>True when the candidate is discovered.</returns>
        private bool CanDiscoverForceUser(Officer scanner, Officer candidate)
        {
            if (!candidate.IsUndiscoveredForceUser())
                return false;

            int probability =
                scanner.ForceRank
                + candidate.ForceRank
                + _game.Config.Jedi.EncounterProbabilityOffset;
            if (probability <= 0)
                return false;

            double roll = _provider.NextDouble() * 100.0;
            return roll < probability;
        }

        /// <summary>
        /// Activates a discovered force user and records the discovery.
        /// </summary>
        /// <param name="scanner">The scanning officer.</param>
        /// <param name="candidate">The discovered force user.</param>
        /// <param name="results">Collection to append discovery results to.</param>
        private void DiscoverForceUser(Officer scanner, Officer candidate, List<GameResult> results)
        {
            candidate.IsForceEligible = true;
            candidate.ForceValue =
                candidate.JediLevel + _provider.NextInt(0, candidate.JediLevelVariance + 1);

            results.Add(
                new ForceExperienceResult
                {
                    Officer = candidate,
                    ExperienceGained = candidate.ForceValue,
                    Tick = _game.CurrentTick,
                }
            );

            results.Add(
                new ForceDiscoveryResult
                {
                    EventType = ForceEventType.ForceUserDiscovered,
                    Officer = candidate,
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
