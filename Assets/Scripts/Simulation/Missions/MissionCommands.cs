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
        private const double _percentScale = 100.0;

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
        /// Returns and clears results queued by immediate mission operations.
        /// </summary>
        /// <returns>The pending mission results.</returns>
        internal List<GameResult> TakePendingResults()
        {
            List<GameResult> results = new List<GameResult>(_pendingResults);
            _pendingResults.Clear();
            return results;
        }

        /// <summary>
        /// Captures whether each faction has officers available for recruitment.
        /// </summary>
        /// <returns>Recruitment availability keyed by faction instance ID.</returns>
        internal Dictionary<string, bool> GetRecruitmentAvailabilityByFaction()
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
        internal void AddRecruitmentExhaustedResults(
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
            List<IMissionParticipant> startingParticipants = mission.GetAllParticipants();
            _pendingResults.Add(
                new MissionStartedResult
                {
                    Mission = mission,
                    MissionTypeID = mission.ConfigKey,
                    Location = planet,
                    Participants = mission.GetAllParticipants(),
                    SourceEventInstanceID = mission.SourceEventInstanceID,
                    Tick = _game.CurrentTick,
                }
            );

            if (ResolveDepartureEncounters(mission, startingParticipants, _pendingResults))
            {
                AddMissionResults(
                    mission,
                    mission.ResolveInterruption(_game, _provider),
                    _pendingResults
                );
                MissionCompletedResult completed = mission.BuildCompletedResult(
                    MissionOutcome.Foiled,
                    MissionCompletionReason.Foiled,
                    _game,
                    startingParticipants
                );
                mission.SetResultMissionID(completed);
                _pendingResults.Add(completed);
                _game.DetachNode(mission);
                return true;
            }

            BeginMission(mission);
            return true;
        }

        /// <summary>
        /// Resolves both departure encounter checkpoints at every participant origin.
        /// </summary>
        /// <param name="mission">The mission preparing to depart.</param>
        /// <param name="startingParticipants">The participant snapshot taken before encounters.</param>
        /// <param name="results">The result collection receiving encounter consequences.</param>
        /// <returns>True when a departure encounter foils the mission.</returns>
        private bool ResolveDepartureEncounters(
            Mission mission,
            IReadOnlyList<IMissionParticipant> startingParticipants,
            List<GameResult> results
        )
        {
            List<Planet> origins = startingParticipants
                .Select(participant => participant.GetParentOfType<Planet>())
                .Where(planet => planet != null)
                .Distinct()
                .ToList();

            foreach (
                MissionEncounterPhase phase in new[]
                {
                    MissionEncounterPhase.DepartureStart,
                    MissionEncounterPhase.DepartureComplete,
                }
            )
            {
                foreach (Planet origin in origins)
                {
                    List<IMissionParticipant> mainParticipants = mission
                        .GetMainParticipants()
                        .Where(participant => participant.GetParentOfType<Planet>() == origin)
                        .ToList();
                    if (mainParticipants.Count == 0)
                        continue;

                    List<IMissionParticipant> decoys = mission
                        .GetDecoyParticipants()
                        .Where(participant => participant.GetParentOfType<Planet>() == origin)
                        .ToList();
                    if (ResolveEncounter(mission, phase, origin, mainParticipants, decoys, results))
                        return true;
                }
            }

            return false;
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
        /// Immediately interrupts missions containing newly captured officers.
        /// </summary>
        /// <param name="officers">The newly captured officers.</param>
        /// <returns>The mission interruption and teardown results.</returns>
        internal List<GameResult> InterruptMissionsForCapturedOfficers(
            IReadOnlyList<Officer> officers
        )
        {
            List<GameResult> results = new List<GameResult>();
            List<Mission> missions = (officers ?? Array.Empty<Officer>())
                .Select(officer => officer?.GetParent() as Mission)
                .Where(mission => mission?.GetParent() != null)
                .Distinct()
                .ToList();

            foreach (Mission mission in missions)
                InterruptMission(mission, results);

            return results;
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
        /// Resolves one mission encounter through the mission system's external services.
        /// </summary>
        /// <param name="mission">The mission executing its lifecycle.</param>
        /// <param name="phase">The encounter checkpoint being resolved.</param>
        /// <param name="results">The result collection receiving detection consequences.</param>
        /// <returns>True when detection foils the mission.</returns>
        bool IMissionExecutionRuntime.ResolveEncounter(
            Mission mission,
            MissionEncounterPhase phase,
            List<GameResult> results
        )
        {
            Planet planet = mission?.GetParent() as Planet;
            return ResolveEncounter(
                mission,
                phase,
                planet,
                mission?.GetMainParticipants(),
                mission?.GetDecoyParticipants(),
                results
            );
        }

        /// <summary>
        /// Resolves one encounter for the participant group present at a planet.
        /// </summary>
        /// <param name="mission">The mission reaching an encounter checkpoint.</param>
        /// <param name="phase">The checkpoint being resolved.</param>
        /// <param name="planet">The planet where the encounter occurs.</param>
        /// <param name="mainParticipants">The primary team present at the encounter.</param>
        /// <param name="decoys">The decoy team present at the encounter.</param>
        /// <param name="results">The result collection receiving consequences.</param>
        /// <returns>True when the encounter foils the mission.</returns>
        private bool ResolveEncounter(
            Mission mission,
            MissionEncounterPhase phase,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> decoys,
            List<GameResult> results
        )
        {
            if (ResolveForceEncounter(mission, planet, mainParticipants, phase))
                return true;

            if (
                phase == MissionEncounterPhase.PreObjective
                && _officerLoyaltySystem.TryResolveMissionBetrayal(
                    mission,
                    out List<GameResult> betrayalResults
                )
            )
            {
                results.AddRange(betrayalResults);
                return true;
            }

            bool missionFoiled = ResolveDetection(
                mission,
                planet,
                mainParticipants,
                decoys,
                phase,
                phase
                    is MissionEncounterPhase.DepartureStart
                        or MissionEncounterPhase.DepartureComplete,
                results
            );
            ApplyOfficerDeaths(results);
            return missionFoiled;
        }

        /// <summary>
        /// Resolves hostile Force-user encounters against the mission's primary officer team.
        /// </summary>
        /// <param name="mission">The mission reaching an encounter checkpoint.</param>
        /// <param name="planet">The planet where the encounter occurs.</param>
        /// <param name="mainParticipants">The primary team present at the encounter.</param>
        /// <param name="phase">The mission lifecycle checkpoint being resolved.</param>
        /// <returns>True when a hostile Force user detects a primary participant.</returns>
        private bool ResolveForceEncounter(
            Mission mission,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            MissionEncounterPhase phase
        )
        {
            if (mission == null || planet == null || mainParticipants == null)
                return false;

            GameConfig.JediConfig config = _game.Config.Jedi;
            List<Officer> participants = mainParticipants
                .OfType<Officer>()
                .Where(officer => officer.ForceRank >= config.MissionParticipantEncounterMinimum)
                .ToList();
            if (participants.Count == 0)
                return false;

            List<Officer> defenders = GetForceDefenders(mission, planet, phase);

            foreach (Officer participant in participants)
            {
                foreach (Officer defender in defenders)
                {
                    int probability = Math.Clamp(
                        participant.ForceRank
                            + defender.ForceRank
                            + config.EncounterProbabilityOffset,
                        0,
                        100
                    );
                    if (RollProbability(probability))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns hostile Force users relevant to a mission lifecycle checkpoint.
        /// </summary>
        /// <param name="mission">The mission reaching an encounter checkpoint.</param>
        /// <param name="planet">The planet where the encounter occurs.</param>
        /// <param name="phase">The mission lifecycle checkpoint being resolved.</param>
        /// <returns>The eligible hostile Force users in traversal order.</returns>
        private List<Officer> GetForceDefenders(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase
        )
        {
            IEnumerable<Officer> candidates = phase switch
            {
                MissionEncounterPhase.DepartureStart => planet.GetChildren<Officer>(),
                MissionEncounterPhase.DepartureComplete
                or MissionEncounterPhase.Arrival when mission.HasRemoteOrigin(planet) => planet
                    .GetChildren<Officer>()
                    .Concat(GetFleetOfficers(mission, planet)),
                MissionEncounterPhase.DepartureComplete or MissionEncounterPhase.Arrival =>
                    GetFleetOfficers(mission, planet),
                MissionEncounterPhase.PreObjective => planet
                    .GetChildren<Officer>()
                    .Concat(GetFleetOfficers(mission, planet)),
                _ => Enumerable.Empty<Officer>(),
            };

            return candidates
                .Where(officer =>
                    officer.GetOwnerInstanceID() != mission.GetOwnerInstanceID()
                    && officer.Movement == null
                    && !officer.IsCaptured
                    && !officer.IsKilled
                    && officer.InjuryPoints == 0
                    && officer.ForceRank >= _game.Config.Jedi.MissionDefenderEncounterMinimum
                )
                .ToList();
        }

        /// <summary>
        /// Enumerates officers contained by hostile capital ships at a planet.
        /// </summary>
        /// <param name="mission">The mission selecting the hostile side.</param>
        /// <param name="planet">The planet containing candidate fleets.</param>
        /// <returns>The contained hostile officers in scene traversal order.</returns>
        private static IEnumerable<Officer> GetFleetOfficers(Mission mission, Planet planet)
        {
            foreach (
                Fleet fleet in planet
                    .GetChildren<Fleet>()
                    .Where(fleet =>
                        fleet.GetOwnerInstanceID() != mission.GetOwnerInstanceID()
                        && fleet.Movement == null
                    )
            )
            {
                foreach (
                    CapitalShip ship in fleet
                        .GetChildren<CapitalShip>()
                        .Where(ship =>
                            ship.ManufacturingStatus == ManufacturingStatus.Complete
                            && ship.Movement == null
                        )
                )
                {
                    foreach (Officer officer in ship.GetChildren<Officer>(recursive: true))
                        yield return officer;
                }
            }
        }

        /// <summary>
        /// Resolves the objective for a mission that completed its progress.
        /// </summary>
        /// <param name="mission">The mission resolving its completed objective.</param>
        /// <returns>The results produced by mission resolution.</returns>
        List<GameResult> IMissionExecutionRuntime.ResolveCompletedObjective(Mission mission)
        {
            List<GameResult> results;
            if (!_uprisingSystem.TryExecuteMission(mission, out results))
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
                mission.Repeat(RollMissionDuration(mission));
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
        private bool CanRemainAtMissionLocation(
            IMissionParticipant participant,
            Planet missionPlanet
        )
        {
            return _movementQueries.CanUseSafeRelocationDestination(
                participant,
                missionPlanet,
                missionPlanet,
                allowOriginPlanet: true
            );
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

            List<IMissionParticipant> participants = mission
                .GetAllParticipants(includeDisabled: true)
                .Where(participant => !IsFreeParticipant(participant))
                .Where(participant =>
                    participant.GetParent() == mission && missionPlanet.CanAcceptChild(participant)
                )
                .ToList();

            _movementManager.CompleteMissionAtLocation(participants, missionPlanet);
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
        /// Resolves mission detection at one encounter checkpoint.
        /// </summary>
        /// <param name="mission">The mission to check for detection.</param>
        /// <param name="planet">The planet where detection occurs.</param>
        /// <param name="mainParticipants">The primary team present at the encounter.</param>
        /// <param name="decoys">The decoy team present at the encounter.</param>
        /// <param name="phase">The mission lifecycle checkpoint being resolved.</param>
        /// <param name="isDeparture">Whether the encounter occurs before travel begins.</param>
        /// <param name="results">Collection to append generated results to.</param>
        /// <returns>True if the mission was foiled.</returns>
        private bool ResolveDetection(
            Mission mission,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> decoys,
            MissionEncounterPhase phase,
            bool isDeparture,
            List<GameResult> results
        )
        {
            if (mission == null || planet == null || mainParticipants == null || decoys == null)
                return false;

            List<ISceneNode> activeDetectors = MissionQueries.GetDetectors(mission, planet, phase);
            if (activeDetectors.Count == 0)
                return false;

            ResolveDecoys(mission, decoys, activeDetectors, planet, isDeparture, results);

            int foilChanceModifier = _queries.GetFoilChanceModifier(mission);
            ISceneNode foilingDetector = activeDetectors.FirstOrDefault(detector =>
                DoesDetectorFoilMission(
                    mission,
                    detector,
                    foilChanceModifier,
                    mainParticipants,
                    planet
                )
            );
            if (foilingDetector == null)
                return false;

            if (!mission.AppliesFoiledParticipantConsequences)
                return true;

            foreach (IMissionParticipant participant in mainParticipants.ToList())
                ResolveFoiledParticipant(mission, participant, activeDetectors, planet, results);

            return true;
        }

        /// <summary>
        /// Rolls one hostile unit's attempt to foil a mission.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <param name="foilChanceModifier">The resolved difficulty adjustment.</param>
        /// <param name="mainParticipants">The primary team present at the encounter.</param>
        /// <param name="planet">The planet where detection occurs.</param>
        /// <returns>True when the detector foils the mission.</returns>
        private bool DoesDetectorFoilMission(
            Mission mission,
            ISceneNode detector,
            int foilChanceModifier,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            Planet planet
        )
        {
            if (mission == null || detector == null)
                return false;

            return RollProbability(
                _queries.GetFoilProbability(
                    mission,
                    detector,
                    foilChanceModifier,
                    mainParticipants,
                    planet
                )
            );
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
        /// <param name="decoys">The decoys present at this encounter.</param>
        /// <param name="activeDetectors">The detectors that have not been diverted.</param>
        /// <param name="planet">The planet where detection occurs.</param>
        /// <param name="isDeparture">Whether the encounter occurs before travel begins.</param>
        /// <param name="results">The result collection receiving confrontation outcomes.</param>
        private void ResolveDecoys(
            Mission mission,
            IReadOnlyList<IMissionParticipant> decoys,
            List<ISceneNode> activeDetectors,
            Planet planet,
            bool isDeparture,
            List<GameResult> results
        )
        {
            foreach (ISceneNode detector in activeDetectors.ToList())
            {
                List<IMissionParticipant> availableDecoys = decoys
                    .Where(IsFreeParticipant)
                    .Where(participant => mission.GetDecoyParticipants().Contains(participant))
                    .ToList();
                if (availableDecoys.Count == 0)
                    return;

                IMissionParticipant decoy = availableDecoys[
                    _provider.NextInt(0, availableDecoys.Count)
                ];
                if (mission.RollDecoyCheck(_provider, _game, decoy, detector, planet))
                {
                    activeDetectors.Remove(detector);
                    continue;
                }

                if (ResolveEvasion(mission, decoy, detector, planet, results))
                {
                    if (isDeparture)
                        mission.RemoveDecoyParticipant(decoy);
                    else
                        ReturnEscapedDecoy(decoy, planet, results);
                }
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
        /// <returns>True when the participant escaped the confrontation.</returns>
        private bool ResolveEvasion(
            Mission mission,
            IMissionParticipant participant,
            ISceneNode detector,
            Planet planet,
            List<GameResult> results
        )
        {
            Officer commander = mission.FindDetectorCommander(detector, planet);
            int defenderCombat = commander?.GetEffectiveRating(SkillRating.Combat) ?? 0;
            int score = participant.GetEffectiveRating(SkillRating.Combat) - defenderCombat;
            bool evaded = _provider.NextDouble() * 100 < _queries.GetEvasionProbability(score);
            if (evaded)
            {
                if (
                    participant is Officer escapingOfficer
                    && Mission.ApplyEvasionInjury(
                        escapingOfficer,
                        detector,
                        planet,
                        _game,
                        _provider,
                        results
                    )
                )
                {
                    _personnelCommands.KillOfficer(escapingOfficer);
                    return false;
                }

                return true;
            }

            if (participant is SpecialForces specialForces)
            {
                DestroySpecialForces(specialForces, planet, results);
                return false;
            }

            if (participant is not Officer officer || officer.IsCaptured || officer.IsKilled)
                return false;

            if (Mission.ApplyEvasionInjury(officer, detector, planet, _game, _provider, results))
            {
                _personnelCommands.KillOfficer(officer);
                return false;
            }

            CaptureOfficer(officer, detector.GetOwnerInstanceID(), planet, results, detector);
            return false;
        }

        /// <summary>
        /// Removes an escaped decoy from the active mission and starts its independent return.
        /// </summary>
        /// <param name="decoy">The decoy that escaped its confrontation.</param>
        /// <param name="planet">The planet where the confrontation occurred.</param>
        /// <param name="results">The result collection receiving stranded-unit consequences.</param>
        private void ReturnEscapedDecoy(
            IMissionParticipant decoy,
            Planet planet,
            List<GameResult> results
        )
        {
            List<IMovable> stranded = _movementManager.ReturnFromMission(
                new[] { decoy },
                Array.Empty<IMovable>()
            );
            ResolveStrandedMissionUnits(stranded, planet, results);
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
            if (!officer.TryCapture(captorInstanceId))
                return;

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
            int rolledTicks = baseTicks + _provider.NextInt(0, spreadTicks + 1);
            int increasePercent = _game
                .GetDifficultyModifier(mission.GetOwnerInstanceID())
                .MissionExecutionSpeedIncreasePercent;
            if (increasePercent <= 0 || rolledTicks <= 0)
                return rolledTicks;

            return (int)
                Math.Ceiling(rolledTicks * _percentScale / (_percentScale + increasePercent));
        }
    }
}
