using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Manages Force state updates, force user discovery, and Jedi training each tick.
/// </summary>
namespace Rebellion.Systems
{
    /// <summary>
    /// Manages Force discovery state and force user scanning each tick.
    /// </summary>
    public class JediSystem
    {
        private readonly GameRoot _game;

        public JediSystem(GameRoot game)
        {
            _game = game;
        }

        public List<GameResult> ProcessTick(IRandomNumberProvider _)
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (!officer.IsJedi || !officer.IsForceEligible)
                    continue;

                RefreshDiscoveringForceUserState(officer, results);
            }

            return results;
        }

        /// <summary>
        /// Updates discovery state based on ForceRank vs threshold.
        /// </summary>
        private void RefreshDiscoveringForceUserState(Officer officer, List<GameResult> results)
        {
            int threshold = _game.Config.Jedi.DiscoveringForceUserThreshold;
            bool shouldDiscover = officer.ForceRank >= threshold
                && !officer.IsCaptured
                && !officer.IsKilled
                && !officer.IsOnMission();

            if (shouldDiscover && !officer.IsDiscoveringForceUser)
            {
                officer.IsDiscoveringForceUser = true;

                results.Add(new ForceDiscoveryResult
                {
                    EventType = ForceEventType.DiscoveringForceUser,
                    Officer = officer,
                    ForceRank = officer.ForceRank,
                    Tick = _game.CurrentTick,
                });

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
        /// Scans for hidden force users at mission completion.
        /// </summary>
        public void ScanForForceUsers(
            Mission mission,
            IRandomNumberProvider rng,
            List<GameResult> results
        )
        {
            Planet planet = mission.GetParent() as Planet;
            if (planet == null)
                return;

            // Find scanners: mission participants who are actively discovering force users
            List<Officer> scanners = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .Where(o => o.IsDiscoveringForceUser)
                .ToList();

            if (scanners.Count == 0)
                return;

            // Build candidate pool: co-participants + officers stationed at this planet
            // Candidates must be dormant force-sensitives (IsJedi but not yet IsForceEligible)
            HashSet<string> candidateIds = new HashSet<string>();
            List<Officer> candidates = new List<Officer>();

            // Co-participants on this mission
            foreach (Officer coParticipant in mission.GetAllParticipants().OfType<Officer>())
            {
                if (IsDiscoveryCandidate(coParticipant) && candidateIds.Add(coParticipant.InstanceID))
                    candidates.Add(coParticipant);
            }

            // Officers stationed at the planet (not on a mission, not captured)
            foreach (Officer local in planet.GetChildren<Officer>(_ => true, recurse: true))
            {
                if (IsDiscoveryCandidate(local) && candidateIds.Add(local.InstanceID))
                    candidates.Add(local);
            }

            int probabilityOffset = _game.Config.Jedi.EncounterProbabilityOffset;

            foreach (Officer scanner in scanners)
            {
                foreach (Officer candidate in candidates)
                {
                    int probability = scanner.ForceRank + candidate.ForceRank + probabilityOffset;

                    if (probability <= 0)
                        continue;

                    double roll = rng.NextDouble() * 100.0;
                    if (roll >= probability)
                        continue;

                    // Discovery success — activate this force user
                    candidate.IsForceEligible = true;
                    candidate.ForceValue = candidate.JediLevel
                        + rng.NextInt(0, candidate.JediLevelVariance + 1);

                    results.Add(new ForceDiscoveryResult
                    {
                        EventType = ForceEventType.ForceUserDiscovered,
                        Officer = candidate,
                        ForceRank = candidate.ForceRank,
                        Tick = _game.CurrentTick,
                    });

                    GameLogger.Log(
                        $"{scanner.GetDisplayName()} discovered {candidate.GetDisplayName()}'s Force potential (rank {candidate.ForceRank})"
                    );

                    // Once discovered, don't let another scanner re-discover
                    break;
                }
            }
        }

        private static bool IsDiscoveryCandidate(Officer officer)
        {
            return officer.IsJedi
                && !officer.IsForceEligible
                && !officer.IsCaptured
                && !officer.IsKilled;
        }

        /// <summary>
        /// Awards force growth after a successful mission.
        /// </summary>
        public void AwardMissionForceGrowth(Officer officer)
        {
            if (!officer.IsJedi || !officer.IsForceEligible)
                return;

            int growth = _game.Config.Jedi.ForceGrowthPerMission;
            officer.ForceValue += growth;

            GameLogger.Log(
                $"{officer.GetDisplayName()} gained {growth} force value from mission (rank {officer.ForceRank})"
            );
        }

        /// <summary>
        /// Applies Jedi training catch-up mechanic based on the rank gap.
        /// </summary>
        public void ApplyTrainingCatchUp(
            Officer trainee,
            Officer teacher,
            IRandomNumberProvider rng
        )
        {
            if (trainee.ForceRank >= teacher.ForceRank)
                return;

            int gap = teacher.ForceRank - trainee.ForceRank;
            int catchUpRange = gap * _game.Config.Jedi.TrainingCatchUpPercent / 100;

            if (catchUpRange > 0)
            {
                int bonus = rng.NextInt(0, catchUpRange + 1);
                trainee.ForceTrainingAdjustment += bonus;

                GameLogger.Log(
                    $"{trainee.GetDisplayName()} gained {bonus} training adjustment from {teacher.GetDisplayName()} (rank {trainee.ForceRank})"
                );
            }
        }

        /// <summary>
        /// Returns the display label for an officer's current force rank.
        /// </summary>
        public ForceRankLabel GetForceRankLabel(Officer officer)
        {
            int rank = officer.ForceRank;
            GameConfig.JediConfig config = _game.Config.Jedi;

            if (rank >= config.RankLabelForceMaster)
                return ForceRankLabel.ForceMaster;
            if (rank >= config.RankLabelForceKnight)
                return ForceRankLabel.ForceKnight;
            if (rank >= config.RankLabelForceStudent)
                return ForceRankLabel.ForceStudent;
            if (rank >= config.RankLabelTrainee)
                return ForceRankLabel.Trainee;
            if (rank >= config.RankLabelNovice)
                return ForceRankLabel.Novice;
            return ForceRankLabel.None;
        }
    }
}
