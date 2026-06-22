using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages the lifecycle of missions each tick.
    /// Mission creation and scene graph attachment are delegated to MissionFactory.
    /// Participant movement and mission initiation are orchestrated here.
    /// </summary>
    public class MissionSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movementManager;
        private readonly MissionFactory _missionFactory;
        private static readonly List<MissionOption> _missionOptions = new List<MissionOption>
        {
            new MissionOption(MissionType.Diplomacy, null, "Diplomacy", MissionIconKeys.Diplomacy),
            new MissionOption(MissionType.Espionage, null, "Espionage", MissionIconKeys.Espionage),
            new MissionOption(
                MissionType.Research,
                ResearchDiscipline.ShipDesign,
                "Ship Design Research",
                MissionIconKeys.ResearchShipDesign
            ),
            new MissionOption(
                MissionType.Research,
                ResearchDiscipline.FacilityDesign,
                "Facility Design Research",
                MissionIconKeys.ResearchFacilityDesign
            ),
            new MissionOption(
                MissionType.Research,
                ResearchDiscipline.TroopTraining,
                "Troop Training Research",
                MissionIconKeys.ResearchTroopTraining
            ),
            new MissionOption(
                MissionType.Reconnaissance,
                null,
                "Reconnaissance",
                MissionIconKeys.Reconnaissance
            ),
            new MissionOption(
                MissionType.Recruitment,
                null,
                "Recruitment",
                MissionIconKeys.Recruitment
            ),
            new MissionOption(
                MissionType.InciteUprising,
                null,
                "Incite Uprising",
                MissionIconKeys.InciteUprising
            ),
            new MissionOption(
                MissionType.SubdueUprising,
                null,
                "Subdue Uprising",
                MissionIconKeys.SubdueUprising
            ),
            new MissionOption(
                MissionType.JediTraining,
                null,
                "Jedi Training",
                MissionIconKeys.JediTraining
            ),
            new MissionOption(MissionType.Rescue, null, "Rescue", MissionIconKeys.Rescue),
            new MissionOption(MissionType.Abduction, null, "Capture", MissionIconKeys.Abduction),
            new MissionOption(
                MissionType.Assassination,
                null,
                "Assassination",
                MissionIconKeys.Assassination
            ),
            new MissionOption(MissionType.Sabotage, null, "Sabotage", MissionIconKeys.Sabotage),
        };

        /// <summary>
        /// Creates a new MissionSystem.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">Random number provider for mission execution and duration rolls.</param>
        /// <param name="movementManager">Used to move participants to and from missions.</param>
        public MissionSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movementManager
        )
        {
            _game = game;
            _provider = provider;
            _movementManager = movementManager;
            _missionFactory = new MissionFactory(game);
        }

        /// <summary>
        /// Processes all active missions and returns aggregate results.
        /// </summary>
        /// <returns>All results produced by missions that executed this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            List<Mission> missions = _game.GetSceneNodesByType<Mission>();

            foreach (Mission mission in missions)
            {
                results.AddRange(UpdateMission(mission));
            }

            return results;
        }

        /// <summary>
        /// Returns whether a mission of the given type can be created for the target.
        /// </summary>
        /// <param name="missionType">The type of mission to check.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <param name="target">The target planet or scene node.</param>
        /// <param name="targetOfficer">Optional specific officer target for abduction/assassination/rescue.</param>
        /// <param name="discipline">Research discipline for Research missions.</param>
        /// <param name="participant">Optional acting participant for participant-specific mission gates.</param>
        /// <returns>True if a mission of this type can be created.</returns>
        public bool CanCreateMission(
            MissionType missionType,
            string ownerInstanceId,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            IMissionParticipant participant = null
        ) =>
            _missionFactory.CanCreateMission(
                missionType,
                ownerInstanceId,
                target,
                _provider,
                targetOfficer,
                discipline,
                participant
            );

        public List<MissionOption> GetCreatableMissionOptions(
            string ownerInstanceId,
            List<IMissionParticipant> participants,
            Planet targetPlanet,
            ISceneNode specificTarget = null
        )
        {
            List<MissionOption> options = new List<MissionOption>();
            if (
                string.IsNullOrEmpty(ownerInstanceId)
                || participants == null
                || participants.Count == 0
                || targetPlanet == null
            )
                return options;

            foreach (MissionOption option in _missionOptions)
            {
                if (
                    CanCreateMissionOption(
                        option,
                        ownerInstanceId,
                        participants,
                        targetPlanet,
                        specificTarget
                    )
                )
                    options.Add(option);
            }

            return options.OrderBy(option => option.Name, StringComparer.Ordinal).ToList();
        }

        public bool HasCreatableMissionOptions(
            string ownerInstanceId,
            List<IMissionParticipant> participants
        )
        {
            if (
                string.IsNullOrEmpty(ownerInstanceId)
                || participants == null
                || participants.Count == 0
            )
                return false;

            foreach (MissionOption option in _missionOptions)
            {
                if (
                    CanFactionAttemptMission(option.Type, ownerInstanceId)
                    && AllParticipantsCanAttemptMission(option, participants)
                )
                    return true;
            }

            return false;
        }

        private bool CanCreateMissionOption(
            MissionOption option,
            string ownerInstanceId,
            List<IMissionParticipant> participants,
            Planet targetPlanet,
            ISceneNode specificTarget
        )
        {
            if (
                !IsTargetValidForMissionOption(
                    option,
                    ownerInstanceId,
                    targetPlanet,
                    specificTarget
                )
            )
                return false;

            if (!CanFactionAttemptMission(option.Type, ownerInstanceId))
                return false;

            if (!AllParticipantsCanAttemptMission(option, participants))
                return false;

            return _missionFactory.CanCreateMission(
                option.Type,
                ownerInstanceId,
                participants,
                new List<IMissionParticipant>(),
                targetPlanet,
                _provider,
                GetMissionTargetOfficer(option.Type, specificTarget),
                option.Discipline,
                specificTarget
            );
        }

        private static Officer GetMissionTargetOfficer(
            MissionType missionType,
            ISceneNode specificTarget
        )
        {
            if (
                missionType == MissionType.Abduction
                || missionType == MissionType.Assassination
                || missionType == MissionType.Rescue
            )
                return specificTarget as Officer;

            return null;
        }

        private bool CanFactionAttemptMission(MissionType missionType, string ownerInstanceId)
        {
            Faction faction = _game.Factions.Find(faction => faction.InstanceID == ownerInstanceId);
            return faction?.DisallowedMissionTypes.Contains(missionType) != true;
        }

        private static bool AllParticipantsCanAttemptMission(
            MissionOption option,
            List<IMissionParticipant> participants
        )
        {
            foreach (IMissionParticipant participant in participants)
            {
                if (!IsMissionParticipantAvailable(participant))
                    return false;

                if (!participant.CanPerformMission(option.Type))
                    return false;

                if (!CanParticipantAttemptResearch(option, participant))
                    return false;
            }

            return true;
        }

        private static bool IsMissionParticipantAvailable(IMissionParticipant participant)
        {
            if (participant == null || participant.Movement != null || participant.IsOnMission())
                return false;

            if (participant is Officer officer && (officer.IsCaptured || officer.InjuryPoints > 0))
                return false;

            if (
                participant is IManufacturable manufacturable
                && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return false;

            return true;
        }

        private static bool CanParticipantAttemptResearch(
            MissionOption option,
            IMissionParticipant participant
        )
        {
            if (option.Type != MissionType.Research || !option.Discipline.HasValue)
                return true;

            return participant is Officer officer
                && officer.GetEffectiveRating(
                    Officer.GetRatingForResearchDiscipline(option.Discipline.Value)
                ) > 0;
        }

        private static bool IsTargetValidForMissionOption(
            MissionOption option,
            string ownerInstanceId,
            Planet targetPlanet,
            ISceneNode specificTarget
        )
        {
            if (specificTarget != null)
                return IsSpecificTargetValidForMissionOption(
                    option,
                    ownerInstanceId,
                    targetPlanet,
                    specificTarget
                );

            return IsTargetPlanetValidForMissionOption(option, ownerInstanceId, targetPlanet);
        }

        private static bool IsTargetPlanetValidForMissionOption(
            MissionOption option,
            string ownerInstanceId,
            Planet targetPlanet
        )
        {
            string targetOwnerId = targetPlanet.GetOwnerInstanceID();
            return option.Type switch
            {
                MissionType.Diplomacy => targetPlanet.IsColonized
                    && !targetPlanet.IsInUprising
                    && (targetOwnerId == null || targetOwnerId == ownerInstanceId)
                    && targetPlanet.GetPopularSupport(ownerInstanceId) < 100,
                MissionType.Espionage => true,
                MissionType.Reconnaissance => targetOwnerId != ownerInstanceId,
                MissionType.Recruitment => targetOwnerId == ownerInstanceId,
                MissionType.InciteUprising => !string.IsNullOrEmpty(targetOwnerId)
                    && targetOwnerId != ownerInstanceId
                    && !targetPlanet.IsInUprising,
                MissionType.SubdueUprising => targetOwnerId == ownerInstanceId
                    && targetPlanet.IsInUprising,
                MissionType.JediTraining => targetOwnerId == ownerInstanceId,
                MissionType.Research => targetOwnerId == ownerInstanceId
                    && ResearchMission.HasResearchFacility(targetPlanet, option.Discipline),
                MissionType.Sabotage => !string.IsNullOrEmpty(targetOwnerId)
                    && targetOwnerId != ownerInstanceId
                    && HasSabotageTarget(targetPlanet),
                _ => false,
            };
        }

        private static bool IsSpecificTargetValidForMissionOption(
            MissionOption option,
            string ownerInstanceId,
            Planet targetPlanet,
            ISceneNode specificTarget
        )
        {
            if (specificTarget is Officer officer)
                return IsTargetOfficerValidForMissionOption(
                    option,
                    ownerInstanceId,
                    targetPlanet,
                    officer
                );

            if (option.Type != MissionType.Sabotage)
                return false;

            if (!CanSabotageTarget(specificTarget))
                return false;

            if (specificTarget is IMovable movable && movable.Movement != null)
                return false;

            string targetOwnerId = specificTarget.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId))
                targetOwnerId = targetPlanet.GetOwnerInstanceID();

            return !string.IsNullOrEmpty(targetOwnerId) && targetOwnerId != ownerInstanceId;
        }

        private static bool CanSabotageTarget(ISceneNode specificTarget)
        {
            if (specificTarget is not IManufacturable manufacturable)
                return false;

            return manufacturable.GetManufacturingStatus() != ManufacturingStatus.Building;
        }

        private static bool IsTargetOfficerValidForMissionOption(
            MissionOption option,
            string ownerInstanceId,
            Planet targetPlanet,
            Officer officer
        )
        {
            Planet officerPlanet = officer.GetParentOfType<Planet>();
            if (officer.Movement != null || officerPlanet?.InstanceID != targetPlanet.InstanceID)
                return false;

            return option.Type switch
            {
                MissionType.Rescue => officer.IsCaptured,
                MissionType.Abduction => officer.GetOwnerInstanceID() != ownerInstanceId
                    && !officer.IsCaptured,
                MissionType.Assassination => officer.GetOwnerInstanceID() != ownerInstanceId
                    && !officer.IsCaptured
                    && !officer.IsKilled,
                _ => false,
            };
        }

        private static bool HasSabotageTarget(Planet planet)
        {
            return planet?.GetAllBuildings().Any(building => CanSabotageTarget(building)) == true;
        }

        /// <summary>
        /// Initiates a mission with a single participant and target.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="participant">The officer or unit performing the mission.</param>
        /// <param name="target">The target planet or scene node.</param>
        /// <param name="targetOfficer">Optional specific officer target for abduction/assassination/rescue.</param>
        /// <param name="discipline">Research discipline for Research missions.</param>
        public void InitiateMission(
            MissionType missionType,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null
        )
        {
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant>
            {
                participant,
            };
            List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
            InitiateMission(
                missionType,
                mainParticipants,
                decoyParticipants,
                target,
                targetOfficer,
                discipline
            );
        }

        public bool InitiateMission(
            MissionType missionType,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null
        )
        {
            if (mainParticipants == null || mainParticipants.Count == 0 || target == null)
                return false;

            decoyParticipants ??= new List<IMissionParticipant>();
            mainParticipants = ResolveMissionParticipants(mainParticipants);
            decoyParticipants = ResolveMissionParticipants(decoyParticipants);
            target = ResolveSceneNode(target);

            if (mainParticipants == null || decoyParticipants == null || target == null)
                return false;

            return CreateAndBeginMission(
                missionType,
                mainParticipants,
                decoyParticipants,
                target,
                targetOfficer,
                discipline,
                null
            );
        }

        public bool InitiateMissionWithSpecificTarget(
            MissionType missionType,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            Planet targetPlanet,
            ISceneNode specificTarget,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null
        )
        {
            if (mainParticipants == null || mainParticipants.Count == 0 || targetPlanet == null)
                return false;

            decoyParticipants ??= new List<IMissionParticipant>();
            mainParticipants = ResolveMissionParticipants(mainParticipants);
            decoyParticipants = ResolveMissionParticipants(decoyParticipants);
            Planet liveTargetPlanet = ResolveSceneNode(targetPlanet) as Planet;
            Officer missionTargetOfficer =
                targetOfficer ?? GetMissionTargetOfficer(missionType, specificTarget);

            if (mainParticipants == null || decoyParticipants == null || liveTargetPlanet == null)
                return false;

            return CreateAndBeginMission(
                missionType,
                mainParticipants,
                decoyParticipants,
                liveTargetPlanet,
                missionTargetOfficer,
                discipline,
                specificTarget
            );
        }

        private bool CreateAndBeginMission(
            MissionType missionType,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            Officer targetOfficer,
            ResearchDiscipline? discipline,
            ISceneNode specificTarget
        )
        {
            string ownerInstanceId = mainParticipants[0].OwnerInstanceID;

            Mission mission = _missionFactory.CreateMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                _provider,
                targetOfficer,
                discipline,
                specificTarget
            );

            Planet planet = target is Planet p ? p : target.GetParentOfType<Planet>();
            if (planet == null)
                return false;

            _game.AttachNode(mission, planet);

            BeginMission(mission);
            return true;
        }

        private List<IMissionParticipant> ResolveMissionParticipants(
            List<IMissionParticipant> participants
        )
        {
            List<IMissionParticipant> resolvedParticipants = new List<IMissionParticipant>();

            foreach (IMissionParticipant participant in participants)
            {
                ISceneNode node = participant as ISceneNode;
                IMissionParticipant resolvedParticipant =
                    ResolveSceneNode(node) as IMissionParticipant;
                if (resolvedParticipant == null)
                    return null;

                resolvedParticipants.Add(resolvedParticipant);
            }

            return resolvedParticipants;
        }

        private ISceneNode ResolveSceneNode(ISceneNode node)
        {
            if (node == null)
                return null;

            return _game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
        }

        /// <summary>
        /// Updates a single mission's state for this tick.
        /// Runs per-tick foil detection before progress, then executes on completion.
        /// </summary>
        /// <param name="mission">The mission to update.</param>
        /// <returns>Results produced by detection or execution this tick; empty otherwise.</returns>
        public List<GameResult> UpdateMission(Mission mission)
        {
            List<GameResult> results = new List<GameResult>();

            if (mission.ShouldAbort(_game))
            {
                TearDownMission(mission);
                return results;
            }

            if (HasParticipantInTransit(mission))
                return results;

            List<IMissionParticipant> participantsBeforeStart = mission.GetAllParticipants();
            if (!mission.CanStart(_game))
            {
                results.Add(
                    mission.BuildCompletedResult(
                        MissionOutcome.Failed,
                        mission.GetCannotStartReportDetail(_game),
                        _game,
                        participantsBeforeStart
                    )
                );
                TearDownMission(mission);
                return results;
            }

            List<IMissionParticipant> participantsBeforeDetection = mission.GetAllParticipants();
            bool participantStateChanged = ResolveDetection(mission, results);

            if (participantStateChanged || mission.ShouldAbort(_game))
            {
                results.Add(
                    mission.BuildCompletedResult(
                        MissionOutcome.Foiled,
                        MissionReportDetail.Foiled,
                        _game,
                        participantsBeforeDetection
                    )
                );
                TearDownMission(mission);
                return results;
            }

            mission.IncrementProgress();

            if (!mission.IsComplete())
                return results;

            results.AddRange(mission.Execute(_game, _provider));

            if (mission.CanContinue(_game))
            {
                BeginMission(mission);
            }
            else
            {
                TearDownMission(mission);
            }

            return results;
        }

        /// <summary>
        /// Returns whether any mission participant is still travelling.
        /// </summary>
        /// <param name="mission">The mission to inspect.</param>
        /// <returns>True if any participant has active movement.</returns>
        private static bool HasParticipantInTransit(Mission mission)
        {
            return mission.GetAllParticipants().Any(participant => participant.Movement != null);
        }

        /// <summary>
        /// Moves all participants back to their recorded origin (planet or fleet), falling back to
        /// the nearest friendly planet if the origin has moved away or no longer exists, then
        /// detaches the mission. Called when CanContinue returns false or ShouldAbort fires.
        /// </summary>
        /// <param name="mission">The mission to tear down and clean up.</param>
        private void TearDownMission(Mission mission)
        {
            Planet missionPlanet = mission.GetParent() as Planet;
            Faction faction = _game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            ISceneNode origin = ResolveReturnOrigin(mission, missionPlanet, faction);

            MoveAbductedTargetToOrigin(mission, origin);
            MoveCapturedParticipantsToHoldingPlanets(mission, missionPlanet);
            MoveParticipantsToOrigin(mission, origin);
            _game.DetachNode(mission);
        }

        /// <summary>
        /// Resolves the location mission participants should return to.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="missionPlanet">The planet that hosts the mission.</param>
        /// <param name="faction">The faction that owns the mission.</param>
        /// <returns>The return location, or null if no valid location exists.</returns>
        private ISceneNode ResolveReturnOrigin(
            Mission mission,
            Planet missionPlanet,
            Faction faction
        )
        {
            ISceneNode origin = GetMissionReturnOrigin(mission, missionPlanet);

            if (origin == null)
                origin = faction.GetNearestFriendlyPlanetTo(mission);

            if (origin == null && missionPlanet?.OwnerInstanceID == mission.OwnerInstanceID)
                origin = missionPlanet;

            return origin;
        }

        /// <summary>
        /// Returns the recorded origin when it is still at the mission planet.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="missionPlanet">The planet that hosts the mission.</param>
        /// <returns>The recorded origin, or null if it is unavailable or no longer local.</returns>
        private ISceneNode GetMissionReturnOrigin(Mission mission, Planet missionPlanet)
        {
            if (mission.OriginInstanceID == null)
                return null;

            ISceneNode origin = _game.GetSceneNodeByInstanceID<ISceneNode>(
                mission.OriginInstanceID
            );
            if (origin == null)
                return null;

            Planet originPlanet = origin is Planet planet
                ? planet
                : origin.GetParentOfType<Planet>();
            return originPlanet == missionPlanet ? origin : null;
        }

        /// <summary>
        /// Moves all eligible mission participants to the resolved return location.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="origin">The location participants should return to.</param>
        private void MoveParticipantsToOrigin(Mission mission, ISceneNode origin)
        {
            foreach (IMovable movable in mission.GetAllParticipants().Cast<IMovable>().ToList())
            {
                if (movable is Officer officer && (officer.IsCaptured || officer.IsKilled))
                    continue;

                if (origin != null)
                    _movementManager.RequestMove(movable, origin);
            }
        }

        /// <summary>
        /// Moves a captured abduction target to the abductor's return location.
        /// </summary>
        /// <param name="mission">The abduction mission being torn down.</param>
        /// <param name="origin">The abductor's return location.</param>
        private void MoveAbductedTargetToOrigin(Mission mission, ISceneNode origin)
        {
            if (mission is not AbductionMission abduction || origin == null)
                return;

            Officer target = _game.GetSceneNodeByInstanceID<Officer>(
                abduction.TargetOfficerInstanceID
            );
            if (target == null)
                return;

            if (!target.IsCaptured || target.CaptorInstanceID != mission.OwnerInstanceID)
                return;

            MoveOfficerDirectly(target, origin);
        }

        /// <summary>
        /// Moves captured mission participants to planets controlled by their captors.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="missionPlanet">The planet that hosts the mission.</param>
        private void MoveCapturedParticipantsToHoldingPlanets(Mission mission, Planet missionPlanet)
        {
            foreach (Officer officer in mission.GetAllParticipants().OfType<Officer>().ToList())
            {
                if (!officer.IsCaptured || officer.CaptorInstanceID == mission.OwnerInstanceID)
                    continue;

                MoveCapturedOfficerToHoldingPlanet(officer, missionPlanet);
            }
        }

        /// <summary>
        /// Resolves per-tick mission detection.
        /// </summary>
        /// <param name="mission">The mission to check for detection.</param>
        /// <param name="results">Collection to append generated results to.</param>
        /// <returns>True if any participant state changed.</returns>
        private bool ResolveDetection(Mission mission, List<GameResult> results)
        {
            if (mission.AllParticipantsInTransit())
                return false;

            if (!mission.RollFoilCheck(_provider))
                return false;

            if (mission.RollDecoyCheck(_provider))
                return false;

            int defenderCombat = GetFoilDefenderCombatSkill(mission);
            Planet planet = mission.GetParent() as Planet;
            bool participantStateChanged = false;

            foreach (IMissionParticipant participant in mission.MainParticipants.ToList())
            {
                participantStateChanged =
                    ResolveFoiledParticipant(participant, defenderCombat, planet, mission, results)
                    || participantStateChanged;
            }

            return participantStateChanged;
        }

        /// <summary>
        /// Gets the combat value used by the mission foil consequence roll.
        /// </summary>
        /// <param name="mission">The detected mission.</param>
        /// <returns>The defender's combat skill, or 0 when no defender is present.</returns>
        private static int GetFoilDefenderCombatSkill(Mission mission)
        {
            Officer defender = mission.FindDefender();
            return defender != null ? defender.GetEffectiveRating(OfficerRating.Combat) : 0;
        }

        /// <summary>
        /// Applies detection consequences to one mission participant.
        /// </summary>
        /// <param name="participant">The detected participant.</param>
        /// <param name="defenderCombat">The defender combat value.</param>
        /// <param name="planet">The mission planet.</param>
        /// <param name="mission">The detected mission.</param>
        /// <param name="results">Collection to append generated results to.</param>
        /// <returns>True if the participant state changed.</returns>
        private bool ResolveFoiledParticipant(
            IMissionParticipant participant,
            int defenderCombat,
            Planet planet,
            Mission mission,
            List<GameResult> results
        )
        {
            if (participant is Officer officer)
            {
                if (officer.IsCaptured || officer.IsKilled)
                    return false;

                results.AddRange(ResolveKillOrCapture(officer, defenderCombat, planet, mission));
                return true;
            }

            if (participant is SpecialForces specialForces)
            {
                DestroyDetectedSpecialForces(specialForces, planet, results);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Destroys a detected special-forces unit.
        /// </summary>
        /// <param name="specialForces">The unit to destroy.</param>
        /// <param name="planet">The mission planet.</param>
        /// <param name="results">Collection to append generated results to.</param>
        private void DestroyDetectedSpecialForces(
            SpecialForces specialForces,
            Planet planet,
            List<GameResult> results
        )
        {
            _game.DetachNode(specialForces);
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
        /// Resolves whether a detected officer is killed or captured.
        /// </summary>
        /// <param name="officer">The officer who was detected.</param>
        /// <param name="defenderCombat">The defending officer's combat skill.</param>
        /// <param name="planet">The planet where the mission takes place.</param>
        /// <param name="mission">The mission that was foiled.</param>
        /// <returns>One capture or kill result.</returns>
        private List<GameResult> ResolveKillOrCapture(
            Officer officer,
            int defenderCombat,
            Planet planet,
            Mission mission
        )
        {
            List<GameResult> results = new List<GameResult>();

            int delta = defenderCombat - officer.GetEffectiveRating(OfficerRating.Combat);
            double captureProbability =
                mission.KillOrCaptureProbabilityTable != null
                    ? mission.KillOrCaptureProbabilityTable.Lookup(delta)
                    : _game.Config.ProbabilityTables.Mission.DefaultKillOrCaptureProbability;

            if (_provider.NextDouble() * 100 <= captureProbability)
            {
                officer.IsCaptured = true;
                officer.CaptorInstanceID = planet?.OwnerInstanceID;
                officer.CanEscape = true;
                MoveCapturedOfficerToHoldingPlanet(officer, planet);
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
            else
            {
                officer.IsKilled = true;
                _game.DetachNode(officer);
                results.Add(
                    new OfficerKilledResult
                    {
                        TargetOfficer = officer,
                        Context = planet,
                        Tick = _game.CurrentTick,
                    }
                );
            }

            return results;
        }

        /// <summary>
        /// Moves a captured officer to the nearest valid captor-owned planet.
        /// </summary>
        /// <param name="officer">The captured officer to move.</param>
        /// <param name="missionPlanet">The planet where capture occurred.</param>
        private void MoveCapturedOfficerToHoldingPlanet(Officer officer, Planet missionPlanet)
        {
            Planet holdingPlanet = FindNearestCaptivePlanet(officer, missionPlanet);
            if (holdingPlanet == null)
                return;

            MoveOfficerDirectly(officer, holdingPlanet);
        }

        /// <summary>
        /// Finds the nearest captor-owned planet that can hold a captured officer.
        /// </summary>
        /// <param name="officer">The captured officer.</param>
        /// <param name="missionPlanet">The planet where capture occurred.</param>
        /// <returns>The nearest valid holding planet, or null.</returns>
        private Planet FindNearestCaptivePlanet(Officer officer, Planet missionPlanet)
        {
            string captorInstanceId = officer.CaptorInstanceID;
            if (string.IsNullOrEmpty(captorInstanceId))
                return null;

            return _game
                .GetSceneNodesByType<Planet>()
                .Where(planet => planet.OwnerInstanceID == captorInstanceId && !planet.IsDestroyed)
                .OrderBy(planet =>
                    missionPlanet != null ? missionPlanet.GetRawDistanceTo(planet) : 0
                )
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Moves an officer immediately without starting a movement timer.
        /// </summary>
        /// <param name="officer">The officer to move.</param>
        /// <param name="destination">The destination node.</param>
        private void MoveOfficerDirectly(Officer officer, ISceneNode destination)
        {
            if (officer.GetParent() == destination)
                return;

            if (!destination.CanAcceptChild(officer))
                return;

            if (officer.GetParent() == null)
            {
                _game.AttachNode(officer, destination);
                return;
            }

            _game.MoveNode(officer, destination);
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
                {
                    if (mission.OriginInstanceID == null)
                        mission.OriginInstanceID = participant.GetParent()?.GetInstanceID();
                    _movementManager.RequestMove(participant, mission);
                }
            }

            mission.Initiate(_provider);
        }
    }
}
