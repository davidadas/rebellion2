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
        /// <param name="participant">The officer or unit performing the mission.</param>
        /// <param name="target">The target planet or scene node.</param>
        /// <param name="targetOfficer">Optional specific officer target for abduction/assassination/rescue.</param>
        /// <param name="discipline">Research discipline for Research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True if a mission of this type can be created.</returns>
        public bool CanCreateMission(
            MissionType missionType,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            ISceneNode liveTarget = ResolveSceneNode(target);
            if (liveTarget == null)
                return false;

            IMissionParticipant liveParticipant = ResolveMissionParticipant(participant);
            if (liveParticipant == null)
                return false;

            Officer selectedTargetOfficer = targetOfficer ?? specificTarget as Officer;

            return _missionFactory.TryCreateMission(
                missionType,
                liveParticipant.GetOwnerInstanceID(),
                new List<IMissionParticipant> { liveParticipant },
                new List<IMissionParticipant>(),
                liveTarget,
                out _,
                _provider,
                selectedTargetOfficer,
                discipline,
                specificTarget
            );
        }

        /// <summary>
        /// Initiates a mission with a single participant and target.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="participant">The officer or unit performing the mission.</param>
        /// <param name="target">The target planet or scene node.</param>
        /// <param name="targetOfficer">Optional specific officer target for abduction/assassination/rescue.</param>
        /// <param name="discipline">Research discipline for Research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True when the mission was created and begun; otherwise false.</returns>
        public bool InitiateMission(
            MissionType missionType,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant>
            {
                participant,
            };
            List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
            return InitiateMission(
                missionType,
                mainParticipants,
                decoyParticipants,
                target,
                targetOfficer,
                discipline,
                specificTarget
            );
        }

        /// <summary>
        /// Initiates a mission with participant lists and a target.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="mainParticipants">Primary participants assigned to the mission.</param>
        /// <param name="decoyParticipants">Decoy participants assigned to the mission.</param>
        /// <param name="target">The mission target.</param>
        /// <param name="targetOfficer">Optional officer target for officer-targeted missions.</param>
        /// <param name="discipline">Optional research discipline for research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True when the mission was created and begun; otherwise false.</returns>
        public bool InitiateMission(
            MissionType missionType,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            if (mainParticipants == null || mainParticipants.Count == 0 || target == null)
                return false;

            decoyParticipants ??= new List<IMissionParticipant>();
            mainParticipants = ResolveMissionParticipants(mainParticipants);
            decoyParticipants = ResolveMissionParticipants(decoyParticipants);
            target = ResolveSceneNode(target);

            Officer selectedTargetOfficer = targetOfficer ?? specificTarget as Officer;

            if (mainParticipants == null || decoyParticipants == null || target == null)
                return false;

            return CreateAndBeginMission(
                missionType,
                mainParticipants,
                decoyParticipants,
                target,
                selectedTargetOfficer,
                discipline,
                specificTarget
            );
        }

        /// <summary>
        /// Creates a mission through the factory and attaches it to its target planet.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="mainParticipants">Primary participants assigned to the mission.</param>
        /// <param name="decoyParticipants">Decoy participants assigned to the mission.</param>
        /// <param name="target">The mission target.</param>
        /// <param name="targetOfficer">Optional officer target for officer-targeted missions.</param>
        /// <param name="discipline">Optional research discipline for research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True when the mission was created and begun; otherwise false.</returns>
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
            if (
                !_missionFactory.TryCreateMission(
                    missionType,
                    ownerInstanceId,
                    mainParticipants,
                    decoyParticipants,
                    target,
                    out Mission mission,
                    _provider,
                    targetOfficer,
                    discipline,
                    specificTarget
                )
            )
                return false;

            Planet planet = target is Planet p ? p : target.GetParentOfType<Planet>();
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
                ISceneNode node = participant as ISceneNode;
                IMissionParticipant resolvedParticipant =
                    ResolveSceneNode(node) as IMissionParticipant;
                if (resolvedParticipant == null)
                    return null;

                resolvedParticipants.Add(resolvedParticipant);
            }

            return resolvedParticipants;
        }

        /// <summary>
        /// Resolves one mission participant to its live scene graph instance.
        /// </summary>
        /// <param name="participant">The participant reference to resolve.</param>
        /// <returns>The live participant, or null when no participant is supplied or resolvable.</returns>
        private IMissionParticipant ResolveMissionParticipant(IMissionParticipant participant)
        {
            return participant is ISceneNode node
                ? ResolveSceneNode(node) as IMissionParticipant
                : null;
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
            return AdvanceMission(mission);
        }

        /// <summary>
        /// Advances a single mission through one lifecycle step.
        /// </summary>
        /// <param name="mission">The mission to advance.</param>
        /// <returns>Results produced by detection or execution this tick; empty otherwise.</returns>
        private List<GameResult> AdvanceMission(Mission mission)
        {
            List<GameResult> results = new List<GameResult>();

            if (mission.IsWaitingForParticipants())
                return results;

            MissionCompletionReason? abortReason = mission.GetAbortReason(_game);
            if (abortReason.HasValue)
            {
                results.Add(
                    BuildTerminatingMissionResult(
                        mission,
                        MissionOutcome.Failed,
                        abortReason.Value,
                        mission.GetAllParticipants()
                    )
                );
                TearDownMission(mission, null);
                return results;
            }

            results.AddRange(ResolveDetectionInterruption(mission));
            if (FinishMissionIfCompleted(mission, results))
                return results;

            mission.IncrementProgress();
            if (!mission.IsComplete())
                return results;

            results.AddRange(mission.Execute(_game, _provider));
            FinishMissionIfCompleted(mission, results);
            return results;
        }

        /// <summary>
        /// Resolves mission detection before a mission advances progress.
        /// </summary>
        /// <param name="mission">The mission to inspect.</param>
        /// <returns>Detection results, including a mission completion result when detection ends the mission.</returns>
        private List<GameResult> ResolveDetectionInterruption(Mission mission)
        {
            List<GameResult> results = new List<GameResult>();
            List<IMissionParticipant> participantsBeforeDetection = mission.GetAllParticipants();
            bool missionFoiled = ResolveDetection(mission, results);

            if (!missionFoiled)
                return results;

            results.Add(
                BuildTerminatingMissionResult(
                    mission,
                    MissionOutcome.Foiled,
                    MissionCompletionReason.Foiled,
                    participantsBeforeDetection
                )
            );
            return results;
        }

        /// <summary>
        /// Returns the mission completion result for a terminal mission state.
        /// </summary>
        /// <param name="mission">The mission being terminated.</param>
        /// <param name="outcome">The mission outcome to report.</param>
        /// <param name="completionReason">The mission completion reason to report.</param>
        /// <param name="participants">Participants captured before teardown side effects.</param>
        /// <returns>A non-continuing mission completion result.</returns>
        private MissionCompletedResult BuildTerminatingMissionResult(
            Mission mission,
            MissionOutcome outcome,
            MissionCompletionReason completionReason,
            List<IMissionParticipant> participants
        )
        {
            MissionCompletedResult result = mission.BuildCompletedResult(
                outcome,
                completionReason,
                _game,
                participants
            );
            result.CanContinue = false;
            return result;
        }

        /// <summary>
        /// Repeats or tears down a mission when the results include a completion result.
        /// </summary>
        /// <param name="mission">The mission to finish.</param>
        /// <param name="results">Results produced by this mission tick.</param>
        /// <returns>True if the mission completed this tick.</returns>
        private bool FinishMissionIfCompleted(Mission mission, List<GameResult> results)
        {
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .LastOrDefault();
            if (completedResult == null)
                return false;

            if (completedResult.CanContinue)
            {
                BeginMission(mission);
            }
            else
            {
                TearDownMission(mission, completedResult);
            }

            return true;
        }

        /// <summary>
        /// Moves all participants back to their recorded origin (planet or fleet), falling back to
        /// the nearest friendly planet if the origin has moved away or no longer exists, then
        /// detaches the mission. Called when a mission completion result cannot continue.
        /// </summary>
        /// <param name="mission">The mission to tear down and clean up.</param>
        /// <param name="completedResult">The completed mission result, or null for pre-execution teardown.</param>
        private void TearDownMission(Mission mission, MissionCompletedResult completedResult)
        {
            Planet missionPlanet = mission.GetParent() as Planet;
            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == mission.OwnerInstanceID);
            ISceneNode origin = ResolveReturnOrigin(mission, missionPlanet, faction);

            MoveCapturedParticipants(mission, missionPlanet);
            MoveReturnPassengersToOrigin(mission, completedResult, origin);
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

            if (origin == null && faction != null)
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
        /// Moves all eligible mission return passengers to the resolved return location.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="completedResult">The completed mission result, or null for pre-execution teardown.</param>
        /// <param name="origin">The location passengers should return to.</param>
        private void MoveReturnPassengersToOrigin(
            Mission mission,
            MissionCompletedResult completedResult,
            ISceneNode origin
        )
        {
            if (origin == null)
                return;

            List<IMovable> returnPassengers = GetReturnPassengers(mission, completedResult)
                .Distinct()
                .ToList();
            if (returnPassengers.Count > 0)
                _movementManager.RequestMove(returnPassengers, origin);
        }

        /// <summary>
        /// Moves captured mission participants to the mission planet before teardown.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="missionPlanet">The planet that hosts the mission.</param>
        private void MoveCapturedParticipants(Mission mission, Planet missionPlanet)
        {
            if (missionPlanet == null)
                return;

            foreach (Officer officer in GetCapturedMissionParticipants(mission))
                _movementManager.RequestMove(officer, missionPlanet);
        }

        /// <summary>
        /// Returns mission participants that can travel back after teardown.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <returns>The movable participants that are neither killed nor captured.</returns>
        private IEnumerable<IMovable> GetFreeMissionParticipants(Mission mission)
        {
            return mission
                .GetAllParticipants()
                .OfType<IMovable>()
                .Where(IsFreeParticipant)
                .Distinct();
        }

        /// <summary>
        /// Returns all units that should return with a completed mission group.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <param name="completedResult">The completed mission result, or null before execution.</param>
        /// <returns>The movable units that should return to the mission origin.</returns>
        private IEnumerable<IMovable> GetReturnPassengers(
            Mission mission,
            MissionCompletedResult completedResult
        )
        {
            foreach (IMovable participant in GetFreeMissionParticipants(mission))
                yield return participant;

            if (completedResult?.Outcome != MissionOutcome.Success)
                yield break;

            foreach (IMovable passenger in mission.GetSuccessfulReturnPassengers(_game))
                yield return passenger;
        }

        /// <summary>
        /// Returns mission officers that were captured during the mission.
        /// </summary>
        /// <param name="mission">The mission being torn down.</param>
        /// <returns>The captured mission officers.</returns>
        private static IEnumerable<Officer> GetCapturedMissionParticipants(Mission mission)
        {
            return mission.GetAllParticipants().OfType<Officer>().Where(IsCapturedParticipant);
        }

        /// <summary>
        /// Returns whether a movable participant can travel home after mission teardown.
        /// </summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True when the participant is not a killed or captured officer.</returns>
        private static bool IsFreeParticipant(IMovable participant)
        {
            return participant is not Officer officer || (!officer.IsKilled && !officer.IsCaptured);
        }

        /// <summary>
        /// Returns whether an officer was captured during mission resolution.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>True when the officer is captured.</returns>
        private static bool IsCapturedParticipant(Officer officer)
        {
            return officer.IsCaptured;
        }

        /// <summary>
        /// Resolves per-tick mission detection.
        /// </summary>
        /// <param name="mission">The mission to check for detection.</param>
        /// <param name="results">Collection to append generated results to.</param>
        /// <returns>True if the mission was foiled.</returns>
        private bool ResolveDetection(Mission mission, List<GameResult> results)
        {
            if (!mission.RollFoilCheck(_provider, _game))
                return false;

            if (mission.RollDecoyCheck(_provider))
                return false;

            if (!mission.AppliesFoiledParticipantConsequences)
                return true;

            int defenderCombat = GetFoilDefenderCombatSkill(mission);
            Planet planet = mission.GetParent() as Planet;

            foreach (IMissionParticipant participant in mission.MainParticipants.ToList())
                ResolveFoiledParticipant(participant, defenderCombat, planet, mission, results);

            return true;
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

            if (_provider.NextDouble() * 100 < captureProbability)
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
