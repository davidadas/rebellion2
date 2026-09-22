using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Starts, advances, interrupts, and tears down missions and their participants.
    /// Each mission owns its post-arrival lifecycle.
    /// </summary>
    public class MissionCommands : IMissionExecutionRuntime
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementCommands _movementManager;
        private readonly MovementQueries _movementQueries;
        private readonly UprisingCommands _uprisingSystem;
        private readonly OfficerLoyaltyCommands _officerLoyaltySystem;
        private readonly PersonnelCommands _personnelCommands;
        private readonly MissionQueries _queries;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Creates the mission commands with their execution dependencies.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random number provider for mission resolution.</param>
        /// <param name="movementManager">The movement system used for participant travel.</param>
        /// <param name="uprisingSystem">The uprising system used by uprising missions.</param>
        /// <param name="movementQueries">The mission-return destination rules.</param>
        /// <param name="queries">The mission eligibility and probability queries.</param>
        /// <param name="officerLoyaltySystem">The officer loyalty and betrayal resolver.</param>
        /// <param name="personnelCommands">The personnel lifecycle commands.</param>
        public MissionCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementCommands movementManager,
            UprisingCommands uprisingSystem,
            MissionQueries queries,
            MovementQueries movementQueries,
            OfficerLoyaltyCommands officerLoyaltySystem = null,
            PersonnelCommands personnelCommands = null
        )
        {
            _game = game;
            _provider = provider;
            _movementManager = movementManager;
            _uprisingSystem =
                uprisingSystem ?? throw new ArgumentNullException(nameof(uprisingSystem));
            _officerLoyaltySystem =
                officerLoyaltySystem ?? new OfficerLoyaltyCommands(game, provider);
            _personnelCommands =
                personnelCommands ?? new PersonnelCommands(new PersonnelQueries(game));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _movementQueries = movementQueries;
        }

        /// <summary>
        /// Processes all active missions and returns aggregate results.
        /// </summary>
        /// <returns>All results produced by missions that executed this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>(_pendingResults);
            _pendingResults.Clear();
            List<Mission> missions = _game.GetSceneNodesByType<Mission>();
            Dictionary<string, bool> recruitmentAvailabilityBefore =
                GetRecruitmentAvailabilityByFaction();

            foreach (Mission mission in missions)
            {
                if (mission.GetParent() == null)
                    continue;

                results.AddRange(UpdateMission(mission));
            }

            AddRecruitmentExhaustedResults(results, recruitmentAvailabilityBefore);
            return results;
        }

        /// <summary>
        /// Captures whether each faction has officers available for recruitment.
        /// </summary>
        /// <returns>Recruitment availability keyed by faction instance ID.</returns>
        private Dictionary<string, bool> GetRecruitmentAvailabilityByFaction()
        {
            return _game
                .GetFactions()
                .ToDictionary(faction => faction.InstanceID, HasRecruitmentCandidates);
        }

        /// <summary>
        /// Returns whether a faction has unrecruited officers available.
        /// </summary>
        /// <param name="faction">The faction to inspect.</param>
        /// <returns>True when at least one unrecruited officer may join the faction.</returns>
        private bool HasRecruitmentCandidates(Faction faction)
        {
            return faction != null && _game.GetUnrecruitedOfficers(faction.InstanceID).Count > 0;
        }

        /// <summary>
        /// Appends recruitment exhaustion results for factions that exhausted recruitment this tick.
        /// </summary>
        /// <param name="results">The mission results produced this tick.</param>
        /// <param name="recruitmentAvailabilityBefore">Recruitment availability captured before missions advanced.</param>
        private void AddRecruitmentExhaustedResults(
            List<GameResult> results,
            Dictionary<string, bool> recruitmentAvailabilityBefore
        )
        {
            foreach (Faction faction in _game.GetFactions())
            {
                bool hadCandidates =
                    recruitmentAvailabilityBefore != null
                    && recruitmentAvailabilityBefore.TryGetValue(faction.InstanceID, out bool had)
                    && had;
                bool hasCandidates = HasRecruitmentCandidates(faction);

                if (!hadCandidates || hasCandidates)
                    continue;

                results.Add(
                    new RecruitmentExhaustedResult
                    {
                        Faction = faction,
                        Planet = GetRecruitmentExhaustedPlanet(results, faction),
                        Tick = _game.CurrentTick,
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

        /// <summary>
        /// Creates, attaches, and starts a mission from the supplied context.
        /// </summary>
        /// <param name="context">The mission context to resolve and start.</param>
        /// <returns>True when the mission was started.</returns>
        public bool InitiateMission(MissionContext context)
        {
            if (!_queries.TryCreateMission(context, out Mission mission))
                return false;

            ISceneNode liveLocation = _queries.ResolveSceneNode(context.Location);
            Planet planet = liveLocation is Planet p ? p : liveLocation?.GetParentOfType<Planet>();
            if (planet == null)
                return false;

            _game.AttachNode(mission, planet);

            BeginMission(mission);
            return true;
        }

        /// <summary>
        /// Aborts an active mission and resolves its participants' post-mission location.
        /// </summary>
        /// <param name="missionInstanceID">The instance ID of the mission to abort.</param>
        /// <returns>True when the mission was found and aborted.</returns>
        public bool AbortMission(string missionInstanceID)
        {
            if (string.IsNullOrEmpty(missionInstanceID))
                return false;

            Mission mission = _game.GetSceneNodeByInstanceID<Mission>(missionInstanceID);
            if (mission == null)
                return false;
            AddMissionResults(
                mission,
                mission.ResolveInterruption(_game, _provider),
                _pendingResults
            );
            TearDownMission(mission, null, _pendingResults);
            return true;
        }

        /// <summary>Interrupts a selected mission and appends its teardown consequences.</summary>
        /// <param name="mission">The mission selected while its participants still belong to it.</param>
        /// <param name="results">The batch receiving interruption and teardown results.</param>
        internal void InterruptMission(Mission mission, List<GameResult> results)
        {
            AddMissionResults(mission, mission.ResolveInterruption(_game, _provider), results);
            TearDownMission(mission, null, results);
        }

        /// <summary>
        /// Adds the originating mission to interruption results before returning them to the pipeline.
        /// </summary>
        /// <param name="mission">The mission producing the results.</param>
        /// <param name="source">The results to stamp.</param>
        /// <param name="destination">The collection receiving stamped results.</param>
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
        /// Updates a single mission's state for this tick.
        /// </summary>
        /// <param name="mission">The mission to update.</param>
        /// <returns>Results produced by detection or execution this tick; empty otherwise.</returns>
        public List<GameResult> UpdateMission(Mission mission)
        {
            if (mission == null || mission.GetParent() == null)
                return new List<GameResult>();

            if (mission.IsWaitingForParticipants())
                return new List<GameResult>();

            List<GameResult> results = mission.Execute(_game, _provider, this);
            foreach (GameResult result in results)
                mission.SetResultMissionID(result);

            return results;
        }

        /// <summary>
        /// Resolves the mission's initial detection through the mission system's external services.
        /// </summary>
        /// <param name="mission">The mission executing its lifecycle.</param>
        /// <param name="results">The result collection receiving detection consequences.</param>
        /// <returns>True when detection foils the mission.</returns>
        bool IMissionExecutionRuntime.ResolveDetection(Mission mission, List<GameResult> results)
        {
            bool missionFoiled = ResolveDetection(mission, results);
            ApplyOfficerDeaths(results);
            return missionFoiled;
        }

        /// <summary>
        /// Resolves betrayal or the objective for a mission that completed its progress.
        /// </summary>
        /// <param name="mission">The mission resolving its completed objective.</param>
        /// <returns>The results produced by mission resolution.</returns>
        List<GameResult> IMissionExecutionRuntime.ResolveCompletedObjective(Mission mission)
        {
            List<GameResult> results;
            if (
                _officerLoyaltySystem.TryResolveMissionBetrayal(
                    mission,
                    out List<GameResult> betrayalResults
                )
            )
            {
                betrayalResults.AddRange(mission.ResolveBetrayedMission(_game, _provider));
                results = betrayalResults;
            }
            else if (!_uprisingSystem.TryExecuteMission(mission, out results))
            {
                results = mission.ResolveObjective(_game, _provider);
            }

            ApplyOfficerDeaths(results);
            return results;
        }

        /// <summary>
        /// Applies officer-death results before mission teardown relocates surviving participants.
        /// </summary>
        /// <param name="results">The mission results to apply.</param>
        private void ApplyOfficerDeaths(IEnumerable<GameResult> results)
        {
            foreach (
                Officer officer in results
                    .OfType<OfficerKilledResult>()
                    .Select(result => result.TargetOfficer)
                    .Where(officer => officer?.IsKilled == false)
                    .Distinct()
            )
                _personnelCommands.KillOfficer(officer);
        }

        /// <summary>
        /// Repeats or tears down a mission after its lifecycle reaches a terminal state.
        /// </summary>
        /// <param name="mission">The mission to finish.</param>
        /// <param name="completedResult">The terminal result, or null for an invalid mission.</param>
        /// <param name="results">Results produced by this mission tick.</param>
        void IMissionExecutionRuntime.FinishMission(
            Mission mission,
            MissionCompletedResult completedResult,
            List<GameResult> results
        )
        {
            if (completedResult == null)
                TearDownMission(mission, null, results);
            else if (completedResult.CanContinue)
                BeginMission(mission);
            else
                TearDownMission(mission, completedResult, results);
        }

        /// <summary>
        /// Resolves participant capture state and post-mission travel, then detaches the mission.
        /// </summary>
        /// <param name="mission">The mission to tear down and clean up.</param>
        /// <param name="completedResult">The completed mission result, or null for pre-execution teardown.</param>
        /// <param name="results">The result batch receiving teardown outcomes.</param>
        private void TearDownMission(
            Mission mission,
            MissionCompletedResult completedResult,
            List<GameResult> results
        )
        {
            int resultStart = results.Count;
            Planet missionPlanet = mission.GetParent() as Planet;
            List<IMissionParticipant> freeParticipants = GetFreeMissionParticipants(mission)
                .Distinct()
                .ToList();
            if (completedResult != null)
                completedResult.ReturnDestination = freeParticipants
                    .Select(_movementQueries.ResolveMissionReturnDestination)
                    .FirstOrDefault(destination => destination != null);
            List<IMovable> additionalPassengers = GetAdditionalReturnPassengers(
                    mission,
                    completedResult
                )
                .Except(freeParticipants.Cast<IMovable>())
                .Distinct()
                .ToList();
            List<IMissionParticipant> localParticipants =
                additionalPassengers.Count == 0
                    ? freeParticipants
                        .Where(participant =>
                            (
                                mission.SuccessfulParticipantsRemainAtLocation
                                && completedResult?.Outcome == MissionOutcome.Success
                            ) || CanRemainAtMissionLocation(participant, missionPlanet)
                        )
                        .ToList()
                    : new List<IMissionParticipant>();
            List<IMissionParticipant> returnParticipants = freeParticipants
                .Except(localParticipants)
                .ToList();

            MoveNonReturningParticipantsToPlanet(mission, missionPlanet);
            List<IMovable> strandedUnits = _movementManager.CompleteMissionAtLocation(
                localParticipants,
                missionPlanet
            );
            strandedUnits.AddRange(
                _movementManager.ReturnFromMission(returnParticipants, additionalPassengers)
            );
            ResolveStrandedMissionUnits(strandedUnits, missionPlanet, results);

            foreach (GameResult result in results.Skip(resultStart))
                mission.SetResultMissionID(result);

            _game.DetachNode(mission);
        }

        /// <summary>
        /// Returns whether a participant may remain at the planet where its mission ended.
        /// </summary>
        /// <param name="participant">The participant whose destination is being resolved.</param>
        /// <param name="missionPlanet">The planet where the mission ended.</param>
        /// <returns>True when the mission planet is intact, friendly, and can accept the participant.</returns>
        private static bool CanRemainAtMissionLocation(
            IMissionParticipant participant,
            Planet missionPlanet
        )
        {
            if (participant == null || missionPlanet?.IsDestroyed != false)
                return false;

            string participantOwnerId = participant.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(participantOwnerId)
                && participantOwnerId == missionPlanet.GetOwnerInstanceID()
                && missionPlanet.CanAcceptChild(participant);
        }

        /// <summary>
        /// Resolves units that have no friendly destination when a mission ends.
        /// </summary>
        /// <param name="units">The units that could not return.</param>
        /// <param name="missionPlanet">The planet where the mission ended.</param>
        /// <param name="results">The result batch receiving capture or destruction outcomes.</param>
        private void ResolveStrandedMissionUnits(
            IEnumerable<IMovable> units,
            Planet missionPlanet,
            List<GameResult> results
        )
        {
            foreach (IMovable unit in units)
            {
                unit.Movement = null;
                if (unit is Officer officer)
                {
                    if (!officer.IsCaptured)
                        CaptureOfficer(
                            officer,
                            missionPlanet?.GetOwnerInstanceID(),
                            missionPlanet,
                            results
                        );

                    if (missionPlanet != null)
                        _movementManager.RequestMove(officer, missionPlanet);
                }
                else if (unit is SpecialForces specialForces)
                {
                    DestroySpecialForces(specialForces, missionPlanet, results);
                }
            }
        }

        /// <summary>
        /// Moves retained participants that cannot return from the mission to its planet.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="missionPlanet">The planet that hosts the mission.</param>
        private void MoveNonReturningParticipantsToPlanet(Mission mission, Planet missionPlanet)
        {
            if (missionPlanet == null)
                return;

            foreach (
                IMissionParticipant participant in mission
                    .GetAllParticipants(includeDisabled: true)
                    .Where(participant => !IsFreeParticipant(participant))
            )
            {
                if (participant.GetParent() == mission && missionPlanet.CanAcceptChild(participant))
                    _game.MoveNode(participant, missionPlanet);
            }
        }

        /// <summary>
        /// Returns mission participants that need a post-mission location.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <returns>The movable participants that are neither killed nor captured.</returns>
        private IEnumerable<IMissionParticipant> GetFreeMissionParticipants(Mission mission)
        {
            return mission.GetAllParticipants().Where(IsFreeParticipant).Distinct();
        }

        /// <summary>
        /// Returns extra units that must travel with a successful mission's participants.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="completedResult">The completed mission result, or null before execution.</param>
        /// <returns>The additional movable units that should return with the mission.</returns>
        private IEnumerable<IMovable> GetAdditionalReturnPassengers(
            Mission mission,
            MissionCompletedResult completedResult
        )
        {
            if (completedResult?.Outcome != MissionOutcome.Success)
                yield break;

            foreach (IMovable passenger in mission.GetSuccessfulReturnPassengers(_game))
                yield return passenger;
        }

        /// <summary>
        /// Returns whether a movable participant can be relocated after mission teardown.
        /// </summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True when the participant is not a killed or captured officer.</returns>
        private static bool IsFreeParticipant(IMovable participant)
        {
            return participant is not Officer officer || (!officer.IsKilled && !officer.IsCaptured);
        }

        /// <summary>
        /// Resolves per-tick mission detection.
        /// </summary>
        /// <param name="mission">The mission to check for detection.</param>
        /// <param name="results">Collection to append generated results to.</param>
        /// <returns>True if the mission was foiled.</returns>
        private bool ResolveDetection(Mission mission, List<GameResult> results)
        {
            if (mission.GetParent() is not Planet planet)
                return false;

            List<ISceneNode> activeDetectors = MissionQueries.GetDetectors(mission, planet);
            if (activeDetectors.Count == 0)
                return false;

            ResolveDecoys(mission, activeDetectors, planet, results);

            ISceneNode foilingDetector = activeDetectors.FirstOrDefault(detector =>
                DoesDetectorFoilMission(mission, detector)
            );
            if (foilingDetector == null)
                return false;

            if (!mission.AppliesFoiledParticipantConsequences)
                return true;

            foreach (IMissionParticipant participant in mission.GetMainParticipants().ToList())
                ResolveFoiledParticipant(mission, participant, activeDetectors, planet, results);

            return true;
        }

        /// <summary>
        /// Rolls one hostile unit's attempt to foil a mission.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <returns>True when the detector foils the mission.</returns>
        private bool DoesDetectorFoilMission(Mission mission, ISceneNode detector)
        {
            if (mission == null || detector == null)
                return false;

            return RollProbability(_queries.GetFoilProbability(mission, detector));
        }

        /// <summary>
        /// Rolls against a percentage probability.
        /// </summary>
        /// <param name="probability">The percentage chance of success.</param>
        /// <returns>True when the random roll succeeds.</returns>
        private bool RollProbability(int probability)
        {
            return probability > 0 && _provider.NextDouble() * 100 < probability;
        }

        /// <summary>
        /// Lets mission decoys confront detectors before any detector can foil the mission.
        /// A successful decoy removes that detector from this tick's remaining traversal.
        /// </summary>
        /// <param name="mission">The mission being checked.</param>
        /// <param name="activeDetectors">The detectors that have not been diverted.</param>
        /// <param name="planet">The planet where detection occurs.</param>
        /// <param name="results">The result collection receiving confrontation outcomes.</param>
        private void ResolveDecoys(
            Mission mission,
            List<ISceneNode> activeDetectors,
            Planet planet,
            List<GameResult> results
        )
        {
            foreach (ISceneNode detector in activeDetectors.ToList())
            {
                List<IMissionParticipant> decoys = mission
                    .GetDecoyParticipants()
                    .Where(IsFreeParticipant)
                    .ToList();
                if (decoys.Count == 0)
                    return;

                IMissionParticipant decoy = decoys[_provider.NextInt(0, decoys.Count)];
                if (mission.RollDecoyCheck(_provider, _game, decoy, detector))
                {
                    activeDetectors.Remove(detector);
                    continue;
                }

                ResolveEvasion(mission, decoy, detector, planet, results);
            }
        }

        /// <summary>
        /// Applies the post-foil confrontation to one mission participant.
        /// </summary>
        /// <param name="mission">The mission whose participant was detected.</param>
        /// <param name="participant">The exposed participant.</param>
        /// <param name="detectors">The detectors that were not diverted.</param>
        /// <param name="planet">The mission planet.</param>
        /// <param name="results">Collection to append generated results to.</param>
        private void ResolveFoiledParticipant(
            Mission mission,
            IMissionParticipant participant,
            IReadOnlyList<ISceneNode> detectors,
            Planet planet,
            List<GameResult> results
        )
        {
            if (!IsFreeParticipant(participant))
                return;

            ISceneNode detector =
                detectors.Count == 0 ? null : detectors[_provider.NextInt(0, detectors.Count)];
            if (detector != null)
                ResolveEvasion(mission, participant, detector, planet, results);
        }

        /// <summary>
        /// Resolves whether a participant evades the detector that confronted them.
        /// </summary>
        /// <param name="mission">The mission whose participant was detected.</param>
        /// <param name="participant">The participant attempting to evade.</param>
        /// <param name="detector">The detector confronting the participant.</param>
        /// <param name="planet">The planet where the confrontation occurs.</param>
        /// <param name="results">The result collection receiving capture or destruction outcomes.</param>
        private void ResolveEvasion(
            Mission mission,
            IMissionParticipant participant,
            ISceneNode detector,
            Planet planet,
            List<GameResult> results
        )
        {
            Officer commander = mission.FindDetectorCommander(detector);
            int defenderCombat = commander?.GetEffectiveRating(SkillRating.Combat) ?? 0;
            int score = participant.GetEffectiveRating(SkillRating.Combat) - defenderCombat;
            bool evaded = _provider.NextDouble() * 100 < _queries.GetEvasionProbability(score);
            if (evaded)
                return;

            if (participant is SpecialForces specialForces)
            {
                DestroySpecialForces(specialForces, planet, results);
                return;
            }

            if (participant is not Officer officer || officer.IsCaptured || officer.IsKilled)
                return;

            if (
                Mission.ApplyCaptureEvasionInjury(
                    officer,
                    detector,
                    planet,
                    _game,
                    _provider,
                    results
                )
            )
            {
                _personnelCommands.KillOfficer(officer);
                return;
            }

            CaptureOfficer(officer, detector.GetOwnerInstanceID(), planet, results, detector);
        }

        /// <summary>
        /// Removes a special-forces unit and records its destruction.
        /// </summary>
        /// <param name="specialForces">The unit to destroy.</param>
        /// <param name="planet">The planet where the unit was destroyed.</param>
        /// <param name="results">Collection to append the destruction result to.</param>
        private void DestroySpecialForces(
            SpecialForces specialForces,
            Planet planet,
            List<GameResult> results
        )
        {
            _game.DeleteNode(specialForces);
            results.Add(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = specialForces,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Marks an officer captured at a planet and records the capture state change.
        /// </summary>
        /// <param name="officer">The officer being captured.</param>
        /// <param name="captorInstanceId">The faction taking the officer captive.</param>
        /// <param name="planet">The planet where the capture occurred.</param>
        /// <param name="results">Collection to append the capture result to.</param>
        /// <param name="capturingUnit">The unit responsible for the capture, when applicable.</param>
        private void CaptureOfficer(
            Officer officer,
            string captorInstanceId,
            Planet planet,
            List<GameResult> results,
            ISceneNode capturingUnit = null
        )
        {
            officer.IsCaptured = true;
            officer.CaptorInstanceID = captorInstanceId;
            officer.CanEscape = true;
            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    ParentAtCapture = officer.GetParent(),
                    CapturingUnit = capturingUnit,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Sends all participants to the mission and starts its timer.
        /// RequestMove immediately reparents each participant to the mission node
        /// and marks them in transit for the physical journey.
        /// </summary>
        /// <param name="mission">The mission to begin.</param>
        private void BeginMission(Mission mission)
        {
            foreach (IMissionParticipant participant in mission.GetAllParticipants())
            {
                if (participant.GetParent() != mission)
                    _movementManager.SendToMission(participant, mission);
            }

            mission.Initiate(RollMissionDuration(mission));
        }

        /// <summary>
        /// Rolls the configured duration for a mission.
        /// </summary>
        /// <param name="mission">The mission whose duration should be rolled.</param>
        /// <returns>The mission duration in ticks.</returns>
        private int RollMissionDuration(Mission mission)
        {
            GameConfig.MissionTickConfig tickConfig =
                _game.Config?.ProbabilityTables?.Mission?.TickRanges?.GetTickConfig(
                    mission.ConfigKey
                );
            int baseTicks = tickConfig?.Base ?? 0;
            int spreadTicks = tickConfig?.Spread ?? 0;
            return baseTicks + _provider.NextInt(0, spreadTicks + 1);
        }
    }
}
