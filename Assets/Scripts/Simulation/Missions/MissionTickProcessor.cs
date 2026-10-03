using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances active missions during a game tick.
    /// </summary>
    internal sealed class MissionTickProcessor : ITickProcessor
    {
        private readonly MissionCommands _commands;
        private readonly MissionResolver _resolver;

        /// <summary>
        /// Creates mission tick processing.
        /// </summary>
        /// <param name="commands">The mission commands and their shared resolver.</param>
        public MissionTickProcessor(MissionCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _resolver = commands.Resolver;
        }

        /// <summary>
        /// Advances every active mission.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The mission results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = _commands.TakePendingResults();
            Dictionary<string, bool> recruitmentAvailabilityBefore =
                GetRecruitmentAvailabilityByFaction(game);

            foreach (Mission mission in game.GetSceneNodesByType<Mission>())
            {
                if (mission.GetParent() != null)
                    results.AddRange(AdvanceMission(game, mission));
            }

            AddRecruitmentExhaustedResults(game, results, recruitmentAvailabilityBefore);
            return results;
        }

        /// <summary>
        /// Advances one attached mission through its current lifecycle step.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="mission">The mission to advance.</param>
        /// <returns>The results produced while advancing the mission.</returns>
        private List<GameResult> AdvanceMission(GameRoot game, Mission mission)
        {
            if (mission == null || mission.GetParent() == null)
                return new List<GameResult>();

            if (mission.IsWaitingForParticipants())
                return new List<GameResult>();

            List<GameResult> results = new List<GameResult>();
            MissionCompletionReason? abortReason = mission.GetAbortReason(game);
            if (abortReason.HasValue)
            {
                AddMissionResults(
                    mission,
                    mission.ResolveInterruption(game, _resolver.RandomProvider),
                    results
                );
                results.Add(
                    mission.BuildTerminatingResult(
                        MissionOutcome.Failed,
                        abortReason.Value,
                        game,
                        mission.GetAllParticipants()
                    )
                );
                _resolver.FinishMission(mission, null, results);
                return CompleteStep(mission, results);
            }

            List<IMissionParticipant> participantsBeforeDetection = mission.GetAllParticipants();
            bool wasDetected = false;
            string foilingFactionInstanceID = null;
            if (!mission.DetectionResolved)
            {
                mission.DetectionResolved = true;
                wasDetected = _resolver.ResolveEncounter(
                    mission,
                    MissionEncounterPhase.Arrival,
                    results,
                    out foilingFactionInstanceID
                );
            }
            if (wasDetected)
            {
                AddMissionResults(
                    mission,
                    mission.ResolveInterruption(game, _resolver.RandomProvider),
                    results
                );
                MissionCompletedResult completed = mission.BuildTerminatingResult(
                    MissionOutcome.Foiled,
                    MissionCompletionReason.Foiled,
                    game,
                    participantsBeforeDetection
                );
                completed.FoilingFactionInstanceID = foilingFactionInstanceID;
                results.Add(completed);
                _resolver.FinishMission(mission, completed, results);
                return CompleteStep(mission, results);
            }

            mission.IncrementProgress();
            if (!mission.IsComplete())
                return CompleteStep(mission, results);

            participantsBeforeDetection = mission.GetAllParticipants();
            if (!mission.PreObjectiveEncounterResolved)
            {
                mission.PreObjectiveEncounterResolved = true;
                wasDetected = _resolver.ResolveEncounter(
                    mission,
                    MissionEncounterPhase.PreObjective,
                    results,
                    out foilingFactionInstanceID
                );
            }
            if (wasDetected)
            {
                AddMissionResults(
                    mission,
                    mission.ResolveInterruption(game, _resolver.RandomProvider),
                    results
                );
                MissionCompletedResult completed = mission.BuildTerminatingResult(
                    MissionOutcome.Foiled,
                    MissionCompletionReason.Foiled,
                    game,
                    participantsBeforeDetection
                );
                completed.FoilingFactionInstanceID = foilingFactionInstanceID;
                results.Add(completed);
                _resolver.FinishMission(mission, completed, results);
                return CompleteStep(mission, results);
            }

            results.AddRange(_resolver.ResolveCompletedObjective(mission));
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .LastOrDefault();
            if (completedResult != null)
                _resolver.FinishMission(mission, completedResult, results);

            return CompleteStep(mission, results);
        }

        /// <summary>
        /// Adds mission-origin metadata to interruption results before appending them.
        /// </summary>
        /// <param name="mission">The mission producing the results.</param>
        /// <param name="source">The results produced by the interruption.</param>
        /// <param name="destination">The lifecycle result collection.</param>
        private static void AddMissionResults(
            Mission mission,
            IEnumerable<GameResult> source,
            ICollection<GameResult> destination
        )
        {
            if (source == null)
                return;

            foreach (GameResult result in source.Where(result => result != null))
            {
                mission.SetResultMissionID(result);
                if (string.IsNullOrEmpty(result.SourceEventInstanceID))
                    result.SourceEventInstanceID = mission.SourceEventInstanceID;
                destination.Add(result);
            }
        }

        /// <summary>
        /// Stamps every result with its mission before returning the completed lifecycle step.
        /// </summary>
        /// <param name="mission">The mission that produced the results.</param>
        /// <param name="results">The results produced during the lifecycle step.</param>
        /// <returns>The stamped result list.</returns>
        private static List<GameResult> CompleteStep(Mission mission, List<GameResult> results)
        {
            foreach (GameResult result in results)
                mission.SetResultMissionID(result);
            return results;
        }

        /// <summary>
        /// Captures whether each faction has officers available for recruitment.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>Recruitment availability keyed by faction instance ID.</returns>
        private static Dictionary<string, bool> GetRecruitmentAvailabilityByFaction(GameRoot game)
        {
            return game.GetFactions()
                .ToDictionary(
                    faction => faction.InstanceID,
                    faction => HasRecruitmentCandidates(game, faction)
                );
        }

        /// <summary>
        /// Returns whether a faction has unrecruited officers available.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The faction to inspect.</param>
        /// <returns>True when at least one unrecruited officer may join the faction.</returns>
        private static bool HasRecruitmentCandidates(GameRoot game, Faction faction)
        {
            return faction != null && game.GetUnrecruitedOfficers(faction.InstanceID).Count > 0;
        }

        /// <summary>
        /// Appends recruitment exhaustion results for factions that exhausted recruitment this tick.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="results">The mission results produced this tick.</param>
        /// <param name="recruitmentAvailabilityBefore">Recruitment availability captured before missions advanced.</param>
        private static void AddRecruitmentExhaustedResults(
            GameRoot game,
            List<GameResult> results,
            Dictionary<string, bool> recruitmentAvailabilityBefore
        )
        {
            foreach (Faction faction in game.GetFactions())
            {
                bool hadCandidates =
                    recruitmentAvailabilityBefore != null
                    && recruitmentAvailabilityBefore.TryGetValue(faction.InstanceID, out bool had)
                    && had;
                bool hasCandidates = HasRecruitmentCandidates(game, faction);

                if (!hadCandidates || hasCandidates)
                    continue;

                results.Add(
                    new RecruitmentExhaustedResult
                    {
                        Faction = faction,
                        Planet = GetRecruitmentExhaustedPlanet(results, faction),
                        Tick = game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Returns the planet associated with the recruitment exhaustion message.
        /// </summary>
        /// <param name="results">The mission results produced this tick.</param>
        /// <param name="faction">The faction whose recruitment pool was exhausted.</param>
        /// <returns>The most relevant recruitment planet, or null if none can be resolved.</returns>
        private static Planet GetRecruitmentExhaustedPlanet(
            IEnumerable<GameResult> results,
            Faction faction
        )
        {
            Planet recruitedPlanet = results
                .OfType<OfficerRecruitedResult>()
                .Where(result => result.Faction?.InstanceID == faction.InstanceID)
                .Select(result => result.Planet)
                .LastOrDefault(planet => planet != null);
            if (recruitedPlanet != null)
                return recruitedPlanet;

            return results
                .OfType<MissionCompletedResult>()
                .Where(result =>
                    result.Outcome == MissionOutcome.Success
                    && IsRecruitmentMissionResult(result, faction)
                )
                .Select(result => result.Mission?.GetParent() as Planet)
                .LastOrDefault(planet => planet != null);
        }

        /// <summary>
        /// Returns whether the mission completion result belongs to a recruitment mission for the faction.
        /// </summary>
        /// <param name="result">The mission completion result to inspect.</param>
        /// <param name="faction">The faction whose recruitment mission is being matched.</param>
        /// <returns>True when the result is for the faction's recruitment mission.</returns>
        private static bool IsRecruitmentMissionResult(
            MissionCompletedResult result,
            Faction faction
        )
        {
            return result?.Mission?.OwnerInstanceID == faction.InstanceID
                && (
                    result.MissionTypeID == RecruitmentMission.MissionTypeID
                    || result.Mission.ConfigKey == RecruitmentMission.MissionTypeID
                );
        }
    }
}
