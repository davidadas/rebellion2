using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
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

            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (!officer.IsJedi || !officer.IsForceEligible)
                    continue;

                UpdateForceDiscoveryState(officer, results);
            }

            ScanForForceUsers(results);

            return results;
        }

        /// <summary>
        /// Grants ForceGrowthPerMission to eligible main participants of a successful mission.
        /// Called from GameManager.ProcessResults when a MissionCompletedResult with Success is seen.
        /// </summary>
        public List<GameResult> ApplyForceGrowth(List<IMissionParticipant> participants)
        {
            List<GameResult> results = new List<GameResult>();
            int growth = _game.Config.Jedi.ForceGrowthPerMission;
            if (growth <= 0)
                return results;

            foreach (IMissionParticipant participant in participants)
            {
                if (
                    participant is Officer officer
                    && officer.GrowsForceOnMission
                    && officer.IsForceEligible
                )
                {
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
        /// Scans for hidden force users each tick at the scanner's location.
        /// </summary>
        /// <param name="results">Collection to append any discovered force user results to.</param>
        private void ScanForForceUsers(List<GameResult> results)
        {
            List<Officer> scanners = _game
                .GetSceneNodesByType<Officer>()
                .Where(o => o.IsDiscoveringForceUser)
                .ToList();

            if (scanners.Count == 0)
                return;

            int probabilityOffset = _game.Config.Jedi.EncounterProbabilityOffset;

            foreach (Officer scanner in scanners)
            {
                Planet planet = scanner.GetParentOfType<Planet>();
                if (planet == null)
                    continue;

                foreach (Officer candidate in planet.GetChildren<Officer>(_ => true, recurse: true))
                {
                    if (!candidate.IsUndiscoveredForceUser())
                        continue;

                    int probability = scanner.ForceRank + candidate.ForceRank + probabilityOffset;

                    if (probability <= 0)
                        continue;

                    double roll = _provider.NextDouble() * 100.0;
                    if (roll >= probability)
                        continue;

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
    }
}
