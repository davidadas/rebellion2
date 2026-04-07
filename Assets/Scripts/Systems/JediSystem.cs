using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Processes Force state updates each tick and force user discovery at mission completion.
///
/// FORCE RANK:
/// ForceRank = ForceValue + ForceTrainingAdjustment.
/// ForceValue grows by +1 per successful mission (gated by IsForceEligible).
/// ForceTrainingAdjustment grows via Jedi training missions (teacher/student).
///
/// TWO-TIER JEDI:
/// Known Jedi (IsKnownJedi=true in template): IsForceEligible=true at start, ForceValue
/// initialized from template. These are visible force users (Luke, Vader, Emperor).
/// Potential Jedi (pass JediProbability roll): IsJedi=true but IsForceEligible=false,
/// ForceValue=0. Dormant until discovered by a known Jedi during a mission.
///
/// DISCOVERY:
/// When ForceRank >= DiscoveringForceUserThreshold (80), a force-eligible Jedi enters
/// "discovering force user" state — meaning they can scan for hidden force users.
/// At mission completion, discovering Jedi scan co-participants and local officers.
/// Discovery probability = scanner.ForceRank + candidate.ForceRank + EncounterProbabilityOffset(-100).
///
/// Matches original REBEXE.EXE mechanics.
/// </summary>
namespace Rebellion.Systems
{
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
        /// Deterministic discovery-state check. When a force-eligible Jedi's ForceRank
        /// crosses the threshold, they enter "discovering force user" state — meaning
        /// they can scan for hidden force users at mission completion.
        /// Matches REBEXE.EXE refresh_character_discovering_force_user_state.
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
        /// Called by MissionSystem after mission.Execute(). Any mission participant
        /// with IsDiscoveringForceUser scans co-participants and officers at the
        /// mission's planet for dormant force-sensitives (IsJedi but not IsForceEligible).
        /// Probability = scanner.ForceRank + candidate.ForceRank + EncounterProbabilityOffset.
        /// Matches REBEXE.EXE scan_local_personnel_for_force_user_discovery.
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
        /// Called by the mission system when an eligible officer completes a mission.
        /// Only force-eligible Jedi gain growth — dormant potentials do not.
        /// Matches REBEXE.EXE get_luke_skywalker_force_increment / get_leia_organa_force_param.
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
        /// Applies Jedi training catch-up mechanic.
        /// If trainee's rank is below teacher's rank, trainee gains a random bonus
        /// up to TrainingCatchUpPercent of the gap.
        /// Matches REBEXE.EXE roll_jedi_training_force_rank_catch_up.
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
    }
}
