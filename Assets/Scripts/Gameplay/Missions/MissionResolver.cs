using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Resolves mission encounters, objectives, interruption, and participant teardown.
    /// </summary>
    internal sealed class MissionResolver
    {
        private const double _percentScale = 100.0;
        private static readonly MissionEncounterPhase[] _departureEncounterPhases =
        {
            MissionEncounterPhase.DepartureStart,
            MissionEncounterPhase.DepartureComplete,
        };

        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementCommands _movementManager;
        private readonly MovementQueries _movementQueries;
        private readonly UprisingResolver _uprisingSystem;
        private readonly MissionBetrayalResolver _betrayalResolver;
        private readonly PersonnelCommands _personnelCommands;
        private readonly MissionQueries _queries;
        private readonly JediCommands _jediCommands;
        private readonly DuelCommands _duelCommands;
        private readonly MissionForceEncounterSelector _forceEncounters;

        /// <summary>
        /// Gets the random provider shared by mission lifecycle operations.
        /// </summary>
        internal IRandomNumberProvider RandomProvider => _provider;

        /// <summary>
        /// Creates mission resolution with its simulation dependencies.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random number provider for mission resolution.</param>
        /// <param name="movementManager">The movement system used for participant travel.</param>
        /// <param name="uprisingSystem">The uprising system used by uprising missions.</param>
        /// <param name="movementQueries">The mission-return destination rules.</param>
        /// <param name="queries">The mission eligibility and probability queries.</param>
        /// <param name="duelCommands">The synchronous officer encounter operations.</param>
        /// <param name="betrayalResolver">The mission-betrayal resolver.</param>
        /// <param name="personnelCommands">The personnel lifecycle commands.</param>
        /// <param name="jediCommands">The Force progression operations.</param>
        internal MissionResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementCommands movementManager,
            UprisingResolver uprisingSystem,
            MissionQueries queries,
            MovementQueries movementQueries,
            DuelCommands duelCommands,
            MissionBetrayalResolver betrayalResolver = null,
            PersonnelCommands personnelCommands = null,
            JediCommands jediCommands = null
        )
        {
            _game = game;
            _provider = provider;
            _movementManager = movementManager;
            _uprisingSystem =
                uprisingSystem ?? throw new ArgumentNullException(nameof(uprisingSystem));
            _betrayalResolver = betrayalResolver ?? new MissionBetrayalResolver(game, provider);
            _personnelCommands =
                personnelCommands ?? new PersonnelCommands(game, new PersonnelQueries(game));
            _jediCommands = jediCommands ?? new JediCommands(game, provider);
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _movementQueries = movementQueries;
            _duelCommands = duelCommands ?? throw new ArgumentNullException(nameof(duelCommands));
            _forceEncounters = new MissionForceEncounterSelector(game.Config.Jedi, provider);
        }

        /// <summary>
        /// Resolves departure encounters and begins participant travel for an attached mission.
        /// </summary>
        /// <param name="mission">The mission being started.</param>
        /// <param name="startingParticipants">The participants assigned before departure.</param>
        /// <param name="results">The result collection receiving departure outcomes.</param>
        internal void BeginMission(
            Mission mission,
            List<IMissionParticipant> startingParticipants,
            List<GameResult> results
        )
        {
            List<GameResult> departureResults = new List<GameResult>();
            bool departureFoiled = ResolveDepartureEncounters(
                mission,
                startingParticipants,
                departureResults,
                out string foilingFactionInstanceID
            );
            AddMissionResults(mission, departureResults, results);
            if (mission.GetParent() == null)
                return;
            if (departureFoiled)
            {
                AddMissionResults(mission, mission.ResolveInterruption(_game, _provider), results);
                MissionCompletedResult completed = mission.BuildTerminatingResult(
                    MissionOutcome.Foiled,
                    MissionCompletionReason.Foiled,
                    _game,
                    startingParticipants
                );
                completed.FoilingFactionInstanceID = foilingFactionInstanceID;
                completed.ReturnDestination = startingParticipants
                    .Where(IsFreeParticipant)
                    .Select(participant => participant.GetParentOfType<Planet>())
                    .FirstOrDefault(planet => planet != null);
                mission.SetResultMissionID(completed);
                results.Add(completed);
                _game.DetachNode(mission);
                return;
            }

            BeginMissionTravel(mission);
        }

        /// <summary>
        /// Resolves both departure encounter checkpoints at every participant origin.
        /// </summary>
        /// <param name="mission">The mission preparing to depart.</param>
        /// <param name="startingParticipants">The participant snapshot taken before encounters.</param>
        /// <param name="results">The result collection receiving encounter consequences.</param>
        /// <param name="foilingFactionInstanceID">The faction whose forces foiled the mission, or null when no encounter foils it.</param>
        /// <returns>True when a departure encounter foils the mission.</returns>
        private bool ResolveDepartureEncounters(
            Mission mission,
            IReadOnlyList<IMissionParticipant> startingParticipants,
            List<GameResult> results,
            out string foilingFactionInstanceID
        )
        {
            foilingFactionInstanceID = null;
            List<Planet> origins = startingParticipants
                .Select(participant => participant.GetParentOfType<Planet>())
                .Where(planet => planet != null)
                .Distinct()
                .ToList();

            foreach (MissionEncounterPhase phase in _departureEncounterPhases)
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
                    bool foiled = ResolveEncounter(
                        mission,
                        phase,
                        origin,
                        mainParticipants,
                        decoys,
                        results,
                        out foilingFactionInstanceID
                    );
                    if (foiled || mission.GetParent() == null)
                        return foiled;
                }
            }

            return false;
        }

        /// <summary>
        /// Interrupts a selected mission and appends its teardown consequences.
        /// </summary>
        /// <param name="mission">The mission selected while its participants still belong to it.</param>
        /// <param name="results">The batch receiving interruption and teardown results.</param>
        internal void InterruptMission(Mission mission, List<GameResult> results)
        {
            AddMissionResults(mission, mission.ResolveInterruption(_game, _provider), results);
            TearDownMission(mission, null, results);
        }

        /// <summary>
        /// Removes captured or killed officers from an attached mission and reevaluates the remaining team.
        /// </summary>
        /// <param name="mission">The mission that contained the officers when they became unavailable.</param>
        /// <param name="officers">The captured or killed officers to remove from the mission.</param>
        /// <param name="results">The batch receiving relocation or interruption results.</param>
        internal void RemoveUnavailableParticipants(
            Mission mission,
            IReadOnlyCollection<Officer> officers,
            List<GameResult> results
        )
        {
            if (mission?.GetParent() is not Planet missionPlanet || officers == null)
                return;
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            List<IMissionParticipant> attachedOfficers = officers
                .Where(officer =>
                    officer != null
                    && (officer.IsKilled || officer.IsCaptured)
                    && officer.GetParent() == mission
                )
                .Distinct()
                .Cast<IMissionParticipant>()
                .ToList();
            _movementManager.CompleteMissionAtLocation(attachedOfficers, missionPlanet, results);

            mission.AcceptCurrentParticipantRoster();
            if (mission.GetAbortReason(_game).HasValue)
                InterruptMission(mission, results);
        }

        /// <summary>
        /// Removes newly captured officers and interrupts only missions whose remaining team is invalid.
        /// </summary>
        /// <param name="officers">The newly captured officers.</param>
        /// <returns>The participant relocation and mission interruption results.</returns>
        internal List<GameResult> ReconcileMissionsForCapturedOfficers(
            IReadOnlyList<Officer> officers
        )
        {
            List<GameResult> results = new List<GameResult>();
            List<IGrouping<Mission, Officer>> missions = (officers ?? Array.Empty<Officer>())
                .Where(officer => officer?.IsCaptured == true && officer.GetParent() is Mission)
                .GroupBy(officer => (Mission)officer.GetParent())
                .Where(group => group.Key.GetParent() != null)
                .ToList();

            foreach (IGrouping<Mission, Officer> mission in missions)
                RemoveUnavailableParticipants(mission.Key, mission.ToList(), results);

            return results;
        }

        /// <summary>
        /// Withdraws injured officers from their recorded mission while preserving a valid remaining team.
        /// Officers on friendly ground remain there; others follow the shared mission return policy.
        /// </summary>
        /// <param name="mission">The mission that contained the officers when they were injured.</param>
        /// <param name="officers">The officers whose injury requires withdrawal.</param>
        /// <param name="results">The batch receiving withdrawal and interruption consequences.</param>
        internal void WithdrawInjuredParticipants(
            Mission mission,
            IReadOnlyCollection<Officer> officers,
            List<GameResult> results
        )
        {
            if (mission?.GetParent() is not Planet missionPlanet || officers == null)
                return;
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            List<IMissionParticipant> attachedOfficers = officers
                .Where(officer =>
                    officer != null && IsFreeParticipant(officer) && officer.GetParent() == mission
                )
                .Distinct()
                .Cast<IMissionParticipant>()
                .ToList();
            List<IMissionParticipant> local = attachedOfficers
                .Where(officer => CanRemainAtMissionLocation(officer, missionPlanet))
                .ToList();
            List<GameResult> withdrawalResults = new();
            List<IMovable> stranded = _movementManager.CompleteMissionAtLocation(
                local,
                missionPlanet,
                withdrawalResults
            );
            stranded.AddRange(
                _movementManager.ReturnFromMission(
                    attachedOfficers.Except(local).ToList(),
                    Array.Empty<IMovable>(),
                    out _,
                    withdrawalResults
                )
            );
            ResolveStrandedMissionUnits(stranded, missionPlanet, withdrawalResults);
            AddMissionResults(mission, withdrawalResults, results);

            mission.AcceptCurrentParticipantRoster();
            if (mission.GetAbortReason(_game).HasValue)
                InterruptMission(mission, results);
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
        /// Resolves one mission encounter through the mission system's external services.
        /// </summary>
        /// <param name="mission">The mission executing its lifecycle.</param>
        /// <param name="phase">The encounter checkpoint being resolved.</param>
        /// <param name="results">The result collection receiving detection consequences.</param>
        /// <param name="foilingFactionInstanceID">The faction whose forces foiled the mission, or null when the foil was internal.</param>
        /// <returns>True when detection foils the mission.</returns>
        internal bool ResolveEncounter(
            Mission mission,
            MissionEncounterPhase phase,
            List<GameResult> results,
            out string foilingFactionInstanceID
        )
        {
            Planet planet = mission?.GetParent() as Planet;
            return ResolveEncounter(
                mission,
                phase,
                planet,
                mission?.GetMainParticipants(),
                mission?.GetDecoyParticipants(),
                results,
                out foilingFactionInstanceID
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
        /// <param name="foilingFactionInstanceID">The faction whose forces foiled the mission, or null when the foil was internal.</param>
        /// <returns>True when the encounter foils the mission.</returns>
        private bool ResolveEncounter(
            Mission mission,
            MissionEncounterPhase phase,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> decoys,
            List<GameResult> results,
            out string foilingFactionInstanceID
        )
        {
            foilingFactionInstanceID = null;
            if (mission == null || planet == null || mainParticipants == null || decoys == null)
                return false;

            List<ISceneNode> detectors = _queries.GetDetectors(
                mission,
                planet,
                phase,
                out MissionForceEncounterScope scope
            );
            Dictionary<Officer, Officer> opponents = _forceEncounters
                .SelectPairs(
                    scope.GetParticipants(mainParticipants, decoys, _game.Config.Jedi),
                    scope.GetDefenders(_game.RegisteredOfficers, _game.Config.Jedi)
                )
                .ToDictionary(pair => pair.Participant, pair => pair.Defender);
            HashSet<Officer> primaryOfficers = mainParticipants.OfType<Officer>().ToHashSet();
            Officer foilingDefender = opponents
                .FirstOrDefault(pair => primaryOfficers.Contains(pair.Key))
                .Value;
            bool foiled = foilingDefender != null;
            foilingFactionInstanceID = foilingDefender?.GetOwnerInstanceID();
            if (!foiled && phase == MissionEncounterPhase.PreObjective)
            {
                foiled = _betrayalResolver.TryResolve(
                    mission,
                    out List<GameResult> betrayalResults
                );
                results.AddRange(betrayalResults);
            }

            ResolveForceEncounters(mission, decoys, opponents, results);
            if (mission.GetParent() == null)
            {
                ApplyOfficerDeaths(results);
                return foiled;
            }
            if (
                phase
                is MissionEncounterPhase.DepartureStart
                    or MissionEncounterPhase.DepartureComplete
            )
            {
                foreach (IMissionParticipant decoy in decoys)
                {
                    if (
                        decoy is Officer officer
                        && opponents.ContainsKey(officer)
                        && (!IsFreeParticipant(officer) || officer.InjuryPoints != 0)
                    )
                        mission.RemoveDecoyParticipant(officer);
                }
            }
            if (!foiled && detectors.Count > 0)
            {
                List<IMissionParticipant> availableDecoys = decoys
                    .Where(participant =>
                        IsFreeParticipant(participant)
                        && (participant is not Officer officer || !opponents.ContainsKey(officer))
                    )
                    .ToList();
                ResolveDecoys(
                    mission,
                    availableDecoys,
                    detectors,
                    planet,
                    phase
                        is MissionEncounterPhase.DepartureStart
                            or MissionEncounterPhase.DepartureComplete,
                    results
                );
                int modifier = _queries.GetFoilChanceModifier(mission);
                ISceneNode detector = detectors.FirstOrDefault(candidate =>
                    DoesDetectorFoilMission(mission, candidate, modifier, mainParticipants, planet)
                );
                if (detector != null)
                {
                    foiled = true;
                    foilingFactionInstanceID = detector.GetOwnerInstanceID();
                }
            }

            if (foiled)
            {
                ResolveForceEncounters(mission, mainParticipants, opponents, results);
                if (mission.GetParent() != null)
                    ResolveFoiledParticipants(
                        mission,
                        mainParticipants,
                        opponents,
                        detectors,
                        planet,
                        results
                    );
            }
            ApplyOfficerDeaths(results);
            return foiled;
        }

        /// <summary>
        /// Resolves the selected pairs for one participant role in major-officer and roster order.
        /// Paired officers do not take an additional ordinary detector confrontation.
        /// Stops when synchronous authored reactions remove the mission.
        /// </summary>
        /// <param name="mission">The mission that reached the checkpoint.</param>
        /// <param name="participants">The primary or decoy role being resolved.</param>
        /// <param name="opponents">The pairs selected before any consequences were applied.</param>
        /// <param name="results">The collection receiving completed encounter consequences.</param>
        private void ResolveForceEncounters(
            Mission mission,
            IReadOnlyList<IMissionParticipant> participants,
            IReadOnlyDictionary<Officer, Officer> opponents,
            List<GameResult> results
        )
        {
            foreach (
                Officer officer in participants
                    .OfType<Officer>()
                    .OrderBy(officer => officer.IsMain ? 0 : 1)
                    .ThenBy(officer => officer.RosterOrder)
            )
            {
                if (mission.GetParent() == null)
                    return;
                if (opponents.TryGetValue(officer, out Officer defender))
                    results.AddRange(
                        _duelCommands.Resolve(
                            officer,
                            defender,
                            sourceEventInstanceID: mission.SourceEventInstanceID
                        )
                    );
            }
        }

        /// <summary>
        /// Resolves the objective for a mission that completed its progress.
        /// </summary>
        /// <param name="mission">The mission resolving its completed objective.</param>
        /// <returns>The results produced by mission resolution.</returns>
        internal List<GameResult> ResolveCompletedObjective(Mission mission)
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
            HashSet<Officer> handledOfficers = new HashSet<Officer>();
            foreach (OfficerKilledResult result in results.OfType<OfficerKilledResult>())
            {
                Officer officer = result.TargetOfficer;
                if (officer == null)
                    continue;

                result.ParentAtDeath ??= officer.GetParent();
                if (!officer.IsKilled && handledOfficers.Add(officer))
                    _personnelCommands.KillOfficer(officer);
            }
        }

        /// <summary>
        /// Repeats or tears down a mission after its lifecycle reaches a terminal state.
        /// </summary>
        /// <param name="mission">The mission to finish.</param>
        /// <param name="completedResult">The terminal result, or null for an invalid mission.</param>
        /// <param name="results">Results produced by this mission tick.</param>
        internal void FinishMission(
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
            List<IMovable> additionalPassengers = GetAdditionalReturnPassengers(
                    mission,
                    completedResult
                )
                .Except(freeParticipants.Cast<IMovable>())
                .Distinct()
                .ToList();
            List<IMissionParticipant> localParticipants = freeParticipants
                .Where(participant =>
                    (
                        mission.SuccessfulParticipantsRemainAtLocation
                        && completedResult?.Outcome == MissionOutcome.Success
                    ) || CanRemainAtMissionLocation(participant, missionPlanet)
                )
                .ToList();
            List<IMovable> localPassengers = new List<IMovable>();
            if (additionalPassengers.Count > 0)
            {
                List<IMovable> localGroup = freeParticipants
                    .Cast<IMovable>()
                    .Concat(additionalPassengers)
                    .ToList();
                bool canRemainTogether =
                    localParticipants.Count == freeParticipants.Count
                    && localGroup.All(unit => CanRemainAtMissionLocation(unit, missionPlanet))
                    && MovementQueries.HasRequiredCapturedOfficerEscorts(localGroup, out _);
                if (canRemainTogether)
                    localPassengers.AddRange(additionalPassengers);
                else
                    localParticipants.Clear();
            }
            List<IMissionParticipant> returnParticipants = freeParticipants
                .Except(localParticipants)
                .ToList();
            List<IMovable> returnPassengers = additionalPassengers.Except(localPassengers).ToList();

            MoveNonReturningParticipantsToPlanet(mission, missionPlanet, results);
            List<IMovable> strandedUnits = _movementManager.CompleteMissionAtLocation(
                localParticipants.Cast<IMovable>().Concat(localPassengers).ToList(),
                missionPlanet,
                results
            );
            List<IMovable> failedReturns = _movementManager.ReturnFromMission(
                returnParticipants,
                returnPassengers,
                out Planet returnLocation,
                results
            );
            strandedUnits.AddRange(failedReturns);
            if (completedResult != null)
            {
                completedResult.ReturnDestination =
                    localParticipants.Count > 0 ? missionPlanet : returnLocation;
            }
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
        private bool CanRemainAtMissionLocation(IMovable participant, Planet missionPlanet)
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
        /// <param name="results">The mission results receiving placement changes.</param>
        private void MoveNonReturningParticipantsToPlanet(
            Mission mission,
            Planet missionPlanet,
            List<GameResult> results
        )
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

            _movementManager.CompleteMissionAtLocation(participants, missionPlanet, results);
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
        /// Applies escape or capture consequences to a foiled mission's primary team.
        /// </summary>
        /// <param name="mission">The mission that was foiled.</param>
        /// <param name="mainParticipants">The primary participants exposed by the encounter.</param>
        /// <param name="opponents">The participants whose Force encounter replaces ordinary evasion.</param>
        /// <param name="detectors">The hostile detectors available for confrontations.</param>
        /// <param name="planet">The planet where the mission was foiled.</param>
        /// <param name="results">The result collection receiving encounter consequences.</param>
        private void ResolveFoiledParticipants(
            Mission mission,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyDictionary<Officer, Officer> opponents,
            IReadOnlyList<ISceneNode> detectors,
            Planet planet,
            List<GameResult> results
        )
        {
            if (!mission.AppliesFoiledParticipantConsequences)
                return;

            foreach (
                IMissionParticipant participant in mainParticipants
                    .Where(participant =>
                        participant is not Officer officer || !opponents.ContainsKey(officer)
                    )
                    .OrderBy(participant =>
                        participant is Officer { IsMain: true } ? 0
                        : participant is Officer ? 1
                        : 2
                    )
                    .ThenBy(participant => (participant as Officer)?.RosterOrder ?? 0)
                    .ToList()
            )
            {
                ResolveFoiledParticipant(mission, participant, detectors, planet, results);
            }
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
        /// <param name="availableDecoys">The mutable pool of decoys still participating in this encounter.</param>
        /// <param name="activeDetectors">The detectors that have not been diverted.</param>
        /// <param name="planet">The planet where detection occurs.</param>
        /// <param name="isDeparture">Whether the encounter occurs before travel begins.</param>
        /// <param name="results">The result collection receiving confrontation outcomes.</param>
        private void ResolveDecoys(
            Mission mission,
            List<IMissionParticipant> availableDecoys,
            List<ISceneNode> activeDetectors,
            Planet planet,
            bool isDeparture,
            List<GameResult> results
        )
        {
            foreach (ISceneNode detector in activeDetectors.ToList())
            {
                if (availableDecoys.Count == 0)
                    return;

                IMissionParticipant decoy = availableDecoys[
                    _provider.NextInt(0, availableDecoys.Count)
                ];
                if (RollProbability(_queries.GetDecoyProbability(mission, decoy, detector, planet)))
                {
                    activeDetectors.Remove(detector);
                    continue;
                }

                availableDecoys.Remove(decoy);
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
            if (participant is SpecialForces specialForces)
            {
                if (!evaded)
                {
                    mission.RemoveParticipant(specialForces);
                    DestroySpecialForces(specialForces, planet, results);
                }
                return evaded;
            }

            if (participant is not Officer officer || officer.IsCaptured || officer.IsKilled)
                return false;

            ISceneNode opposingUnit = commander ?? detector;
            bool killed = Mission.ApplyEvasionInjury(
                officer,
                opposingUnit,
                planet,
                _game,
                _provider,
                results
            );
            if (evaded)
            {
                ForceExperienceResult growth = _jediCommands.ApplyCaptureEvasionGrowth(
                    officer,
                    mission.SourceEventInstanceID
                );
                if (growth != null)
                    results.Add(growth);
            }
            if (killed)
                return false;
            if (evaded)
            {
                return true;
            }

            CaptureOfficer(
                officer,
                opposingUnit.GetOwnerInstanceID(),
                planet,
                results,
                opposingUnit
            );
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
                Array.Empty<IMovable>(),
                out _,
                results
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
                    Reason = UnitDestructionReason.Detection,
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
            if (!officer.TryCapture(captorInstanceId, _game, _provider))
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
        private void BeginMissionTravel(Mission mission)
        {
            _movementManager.SendToMission(
                mission
                    .GetAllParticipants()
                    .Where(participant => participant.GetParent() != mission)
                    .ToList(),
                mission
            );

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

            long denominator = (long)_percentScale + increasePercent;
            return (int)Math.Ceiling(rolledTicks * (double)_percentScale / denominator);
        }
    }
}
