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
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Orchestrates mission creation, participant travel, and external execution services.
    /// Each mission owns its post-arrival lifecycle.
    /// </summary>
    public class MissionSystem
        : IMissionExecutionRuntime,
            IGameResultHandler<OfficerCaptureStateResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movementManager;
        private readonly UprisingSystem _uprisingSystem;
        private readonly OfficerLoyaltySystem _officerLoyaltySystem;
        private readonly PersonnelSystem _personnelSystem;
        private readonly MissionFactory _missionFactory;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Creates a mission system with all mission-resolution dependencies.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random number provider for mission resolution.</param>
        /// <param name="movementManager">The movement system used for participant travel.</param>
        /// <param name="uprisingSystem">The uprising system used by uprising missions.</param>
        /// <param name="officerLoyaltySystem">The officer loyalty and betrayal resolver.</param>
        /// <param name="personnelSystem">The personnel lifecycle service.</param>
        public MissionSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movementManager,
            UprisingSystem uprisingSystem,
            OfficerLoyaltySystem officerLoyaltySystem = null,
            PersonnelSystem personnelSystem = null
        )
        {
            _game = game;
            _provider = provider;
            _movementManager = movementManager;
            _uprisingSystem =
                uprisingSystem ?? throw new ArgumentNullException(nameof(uprisingSystem));
            _officerLoyaltySystem =
                officerLoyaltySystem ?? new OfficerLoyaltySystem(game, provider);
            _personnelSystem = personnelSystem ?? new PersonnelSystem(game);
            _missionFactory = new MissionFactory(game);
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
                    result.MissionTypeID == MissionTypeIDs.Recruitment
                    || result.Mission.ConfigKey == MissionTypeIDs.Recruitment
                );
        }

        /// <summary>
        /// Returns whether the supplied request can create a mission.
        /// </summary>
        /// <param name="request">The mission start request to resolve and evaluate.</param>
        /// <returns>True when the mission can be created.</returns>
        public bool CanCreateMission(MissionStartRequest request)
        {
            MissionContext context = ResolveMissionContext(request);
            return context != null && _missionFactory.TryCreateMission(context, out _);
        }

        /// <summary>
        /// Creates a mission from a request without starting it.
        /// </summary>
        /// <param name="request">The mission request to resolve.</param>
        /// <param name="mission">The created mission when successful.</param>
        /// <returns>True when the request creates a valid mission.</returns>
        public bool TryCreateMission(MissionStartRequest request, out Mission mission)
        {
            MissionContext context = ResolveMissionContext(request);
            if (context == null)
            {
                mission = null;
                return false;
            }

            return _missionFactory.TryCreateMission(context, out mission);
        }

        /// <summary>
        /// Returns the mission options available for the supplied mission start request.
        /// </summary>
        /// <param name="request">The mission start request to resolve and evaluate.</param>
        /// <returns>The mission options that can be created from the resolved request.</returns>
        public List<MissionOption> GetAvailableMissionOptions(MissionStartRequest request)
        {
            MissionContext context = ResolveMissionContext(request);
            return context != null
                ? _missionFactory.GetAvailableMissionOptions(context)
                : new List<MissionOption>();
        }

        /// <summary>
        /// Calculates mission success odds without resolving an outcome.
        /// </summary>
        /// <param name="mission">The mission whose probability rules apply.</param>
        /// <param name="participants">The participants to evaluate.</param>
        /// <returns>The calculated mission odds.</returns>
        public MissionOdds GetMissionOdds(
            Mission mission,
            IEnumerable<IMissionParticipant> participants
        )
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));

            return mission.GetMissionOdds(participants, _game);
        }

        /// <summary>
        /// Creates, attaches, and starts a mission from the supplied request.
        /// </summary>
        /// <param name="request">The mission start request to resolve and start.</param>
        /// <returns>True when the mission was started.</returns>
        public bool InitiateMission(MissionStartRequest request)
        {
            MissionContext context = ResolveMissionContext(request);
            return context != null && CreateAndBeginMission(context);
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
            if (mission.IsWaitingForParticipants())
                return false;

            AddMissionResults(
                mission,
                mission.ResolveInterruption(_game, _provider),
                _pendingResults
            );
            TearDownMission(mission, null, _pendingResults);
            return true;
        }

        /// <summary>
        /// Ends missions whose participants were captured by another simulation system.
        /// </summary>
        /// <param name="results">The officer capture-state changes to process.</param>
        /// <returns>Mission interruption results produced while tearing down affected missions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> missionResults = new List<GameResult>();
            List<Mission> affectedMissions = results
                .Where(result => result?.IsCaptured == true)
                .Select(result => result.TargetOfficer?.GetParent() as Mission)
                .Where(mission => mission != null)
                .Distinct()
                .ToList();

            foreach (Mission mission in affectedMissions)
            {
                AddMissionResults(
                    mission,
                    mission.ResolveInterruption(_game, _provider),
                    missionResults
                );
                TearDownMission(mission, null, missionResults);
            }

            return missionResults;
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
                result.MissionInstanceID = mission.InstanceID;
                if (string.IsNullOrEmpty(result.SourceEventInstanceID))
                    result.SourceEventInstanceID = mission.SourceEventInstanceID;
                destination.Add(result);
            }
        }

        /// <summary>
        /// Resolves mission participants while preserving the caller's observed target state.
        /// </summary>
        /// <param name="request">The mission start request to resolve.</param>
        /// <returns>The resolved mission context, or null when any required object is missing.</returns>
        private MissionContext ResolveMissionContext(MissionStartRequest request)
        {
            if (
                request == null
                || request.MainParticipants == null
                || request.MainParticipants.Count == 0
                || request.Location == null
            )
                return null;

            List<IMissionParticipant> mainParticipants = ResolveMissionParticipants(
                request.MainParticipants
            );
            List<IMissionParticipant> decoyParticipants = ResolveMissionParticipants(
                request.DecoyParticipants ?? new List<IMissionParticipant>()
            );

            if (mainParticipants == null || decoyParticipants == null)
                return null;

            return new MissionContext
            {
                Game = _game,
                MissionTypeID = request.MissionTypeID,
                OwnerInstanceId = mainParticipants[0].GetOwnerInstanceID(),
                Location = request.Location,
                SelectedTarget = request.SelectedTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                Discipline = request.Discipline,
            };
        }

        /// <summary>
        /// Creates a mission, attaches it to its location planet, and begins participant travel.
        /// </summary>
        /// <param name="context">The resolved mission context.</param>
        /// <returns>True when the mission was created and started.</returns>
        private bool CreateAndBeginMission(MissionContext context)
        {
            if (!_missionFactory.TryCreateMission(context, out Mission mission))
                return false;

            ISceneNode liveLocation = ResolveSceneNode(context.Location);
            Planet planet = liveLocation is Planet p ? p : liveLocation?.GetParentOfType<Planet>();
            if (planet == null)
                return false;

            _game.AttachNode(mission, planet);

            BeginMission(mission);
            return true;
        }

        /// <summary>
        /// Resolves mission participants to their live scene graph instances.
        /// </summary>
        /// <param name="participants">The participant references to resolve.</param>
        /// <returns>Resolved participants, or null if any participant cannot be resolved.</returns>
        private List<IMissionParticipant> ResolveMissionParticipants(
            List<IMissionParticipant> participants
        )
        {
            List<IMissionParticipant> resolvedParticipants = new List<IMissionParticipant>();

            foreach (IMissionParticipant participant in participants)
            {
                ISceneNode node = participant;
                IMissionParticipant resolvedParticipant =
                    ResolveSceneNode(node) as IMissionParticipant;
                if (resolvedParticipant == null)
                    return null;

                resolvedParticipants.Add(resolvedParticipant);
            }

            return resolvedParticipants;
        }

        /// <summary>
        /// Resolves a scene node reference to its live scene graph instance.
        /// </summary>
        /// <param name="node">The scene node reference to resolve.</param>
        /// <returns>The live scene node, or null if it cannot be resolved.</returns>
        private ISceneNode ResolveSceneNode(ISceneNode node)
        {
            if (node == null)
                return null;

            return _game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
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
                result.MissionInstanceID = mission.InstanceID;

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
                _personnelSystem.KillOfficer(officer);
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
                    .Select(_movementManager.ResolveMissionReturnDestination)
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
                result.MissionInstanceID = mission.InstanceID;

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
                        CaptureOfficer(officer, missionPlanet, results);

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

            List<ISceneNode> activeDetectors = GetDetectors(mission, planet);
            if (activeDetectors.Count == 0)
                return false;

            ResolveDecoys(mission, activeDetectors, planet, results);

            ISceneNode foilingDetector = activeDetectors.FirstOrDefault(detector =>
                IsMissionDetected(mission, detector)
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
        /// Rolls one hostile unit's detection attempt against a mission.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <returns>True when the detector foils the mission.</returns>
        private bool IsMissionDetected(Mission mission, ISceneNode detector)
        {
            if (mission == null || detector == null)
                return false;

            int score = CalculateDetectionScore(mission, detector);
            int probability = LookupProbability(GetMissionTables().Foil, score);
            return RollProbability(probability);
        }

        /// <summary>
        /// Calculates one detector's score against a mission team.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <returns>The score used to look up the detection probability.</returns>
        private int CalculateDetectionScore(Mission mission, ISceneNode detector)
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            IReadOnlyList<IMissionParticipant> participants = mission.GetMainParticipants();
            Officer commander = mission.FindDetectorCommander(detector);
            return GetAverageEspionage(participants)
                - GetScaledCommanderEspionage(commander, missionTables.FoilDefenderScalingPercent)
                - GetDetectorRating(detector)
                - participants.OfType<SpecialForces>().Count()
                - missionTables.FoilFlatScoreAdjustment;
        }

        /// <summary>
        /// Returns the mission team's average effective Espionage rating.
        /// </summary>
        /// <param name="participants">The mission's main participants.</param>
        /// <returns>The average rating, or zero when the mission has no participants.</returns>
        private static int GetAverageEspionage(IReadOnlyList<IMissionParticipant> participants)
        {
            return participants.Count == 0
                ? 0
                : participants.Sum(participant =>
                    participant.GetEffectiveRating(OfficerRating.Espionage)
                ) / participants.Count;
        }

        /// <summary>
        /// Returns the configured portion of a detector commander's Espionage rating.
        /// </summary>
        /// <param name="commander">The detector commander, if one is assigned.</param>
        /// <param name="scalingPercent">The percentage of the rating applied to detection.</param>
        /// <returns>The scaled commander contribution.</returns>
        private static int GetScaledCommanderEspionage(Officer commander, int scalingPercent)
        {
            return (commander?.GetEffectiveRating(OfficerRating.Espionage) ?? 0)
                * scalingPercent
                / 100;
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
            int defenderCombat = commander?.GetEffectiveRating(OfficerRating.Combat) ?? 0;
            int score = participant.GetEffectiveRating(OfficerRating.Combat) - defenderCombat;
            bool evaded = _provider.NextDouble() * 100 < GetEvasionProbability(score);
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
                _personnelSystem.KillOfficer(officer);
                return;
            }

            CaptureOfficer(officer, planet, results);
        }

        /// <summary>
        /// Returns hostile detector units in the original traversal order.
        /// </summary>
        /// <param name="mission">The mission being checked for detection.</param>
        /// <param name="planet">The planet where the mission is operating.</param>
        /// <returns>The ordered detector units.</returns>
        private static List<ISceneNode> GetDetectors(Mission mission, Planet planet)
        {
            List<ISceneNode> detectors = new List<ISceneNode>();
            AddEligibleDetectors(mission, planet.GetChildren<Starfighter>(), detectors);
            AddEligibleDetectors(mission, planet.GetChildren<Regiment>(), detectors);

            bool blocksFleetDetection = planet
                .GetChildren<Building>()
                .Any(building =>
                    building.IsDetectionBlocker
                    && building.OwnerInstanceID == mission.OwnerInstanceID
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                    && building.Movement == null
                );
            if (blocksFleetDetection)
                return detectors;

            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
                {
                    if (mission.IsEligibleDetector(capitalShip))
                        detectors.Add(capitalShip);

                    AddEligibleDetectors(
                        mission,
                        capitalShip.GetChildren<Starfighter>(),
                        detectors
                    );
                    AddEligibleDetectors(mission, capitalShip.GetChildren<Regiment>(), detectors);
                }
            }

            return detectors;
        }

        /// <summary>
        /// Appends eligible detector units without changing their scene order.
        /// </summary>
        /// <param name="mission">The mission being checked for detection.</param>
        /// <param name="candidates">The candidate detector units.</param>
        /// <param name="detectors">The collection receiving eligible detectors.</param>
        private static void AddEligibleDetectors(
            Mission mission,
            IEnumerable<ISceneNode> candidates,
            ICollection<ISceneNode> detectors
        )
        {
            foreach (ISceneNode candidate in candidates)
            {
                if (mission.IsEligibleDetector(candidate))
                    detectors.Add(candidate);
            }
        }

        /// <summary>
        /// Returns the authored detection rating for a detector unit.
        /// </summary>
        /// <param name="detector">The detector unit.</param>
        /// <returns>The detector's authored rating.</returns>
        private static int GetDetectorRating(ISceneNode detector) =>
            detector switch
            {
                Regiment regiment => regiment.DetectionRating,
                Starfighter starfighter => starfighter.DetectionRating,
                CapitalShip capitalShip => capitalShip.DetectionRating,
                _ => 0,
            };

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
        /// <param name="planet">The planet where the capture occurred.</param>
        /// <param name="results">Collection to append the capture result to.</param>
        private void CaptureOfficer(Officer officer, Planet planet, List<GameResult> results)
        {
            officer.IsCaptured = true;
            officer.CaptorInstanceID = planet?.OwnerInstanceID;
            officer.CanEscape = true;
            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Returns the configured evasion probability for a confronted participant.
        /// </summary>
        /// <param name="score">The participant combat rating minus commander combat rating.</param>
        /// <returns>The configured evasion probability.</returns>
        private double GetEvasionProbability(int score)
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            return LookupProbability(
                missionTables.Evasion,
                score,
                missionTables.DefaultEvasionProbability
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

        /// <summary>
        /// Returns the mission probability table config for the current game.
        /// </summary>
        /// <returns>The configured mission probability tables.</returns>
        private GameConfig.MissionProbabilityTablesConfig GetMissionTables()
        {
            return _game.Config?.ProbabilityTables?.Mission
                ?? new GameConfig.MissionProbabilityTablesConfig();
        }

        /// <summary>
        /// Returns the configured probability for a score.
        /// </summary>
        /// <param name="entries">The configured probability table entries.</param>
        /// <param name="score">The score to look up.</param>
        /// <param name="defaultValue">The value returned when the table is empty.</param>
        /// <returns>The configured probability value.</returns>
        private static int LookupProbability(
            Dictionary<int, int> entries,
            int score,
            int defaultValue = 0
        )
        {
            if (entries == null || entries.Count == 0)
                return defaultValue;

            return new ProbabilityTable(entries).Lookup(score);
        }
    }
}
