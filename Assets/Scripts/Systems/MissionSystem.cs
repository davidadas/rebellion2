using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
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
        /// <param name="fogOfWar">Optional fog-of-war system passed to MissionFactory for visibility checks.</param>
        public MissionSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movementManager,
            FogOfWarSystem fogOfWar = null
        )
        {
            _game = game;
            _provider = provider;
            _movementManager = movementManager;
            _missionFactory = new MissionFactory(game, fogOfWar);
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
        /// <returns>True if a mission of this type can be created.</returns>
        public bool CanCreateMission(
            MissionType missionType,
            string ownerInstanceId,
            ISceneNode target,
            Officer targetOfficer = null
        ) =>
            _missionFactory.CanCreateMission(
                missionType,
                ownerInstanceId,
                target,
                _provider,
                targetOfficer
            );

        /// <summary>
        /// Initiates a mission with a single participant and target.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="participant">The officer or unit performing the mission.</param>
        /// <param name="target">The target planet or scene node.</param>
        /// <param name="targetOfficer">Optional specific officer target for abduction/assassination/rescue.</param>
        public void InitiateMission(
            MissionType missionType,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null
        )
        {
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant>
            {
                participant,
            };
            List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
            string ownerInstanceId = participant.OwnerInstanceID;

            Mission mission = _missionFactory.CreateMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                _provider,
                targetOfficer
            );

            Planet planet = target is Planet p ? p : target.GetParentOfType<Planet>();
            _game.AttachNode(mission, planet);

            BeginMission(mission);
        }

        /// <summary>
        /// Updates a single mission's state for this tick.
        /// Returns results from execution and participant movement on completion.
        /// </summary>
        /// <param name="mission">The mission to update.</param>
        /// <returns>Results produced if the mission executed this tick; empty otherwise.</returns>
        public List<GameResult> UpdateMission(Mission mission)
        {
            List<GameResult> results = new List<GameResult>();

            // Pre-tick guard: cancel immediately if an external event has invalidated this mission.
            if (mission.ShouldAbort(_game))
            {
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
        /// Moves all participants back to their recorded origin (planet or fleet), falling back to
        /// the nearest friendly planet if the origin has moved away or no longer exists, then
        /// detaches the mission. Called when CanContinue returns false or ShouldAbort fires.
        /// </summary>
        /// <param name="mission">The mission to tear down and clean up.</param>
        private void TearDownMission(Mission mission)
        {
            Planet missionPlanet = mission.GetParent() as Planet;
            Faction faction = _game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);

            // Resolve the recorded origin (planet or fleet). If the origin has moved away from
            // the mission planet since the mission started, don't chase it — fall back instead.
            ISceneNode origin =
                mission.OriginInstanceID != null
                    ? _game.GetSceneNodeByInstanceID<ISceneNode>(mission.OriginInstanceID)
                    : null;
            if (origin != null)
            {
                Planet originPlanet = origin is Planet p ? p : origin.GetParentOfType<Planet>();
                if (originPlanet != missionPlanet)
                    origin = null;
            }

            if (origin == null)
                origin = faction.GetNearestFriendlyPlanetTo(mission);
            if (origin == null && missionPlanet?.OwnerInstanceID == mission.OwnerInstanceID)
                origin = missionPlanet;

            List<IMissionParticipant> allParticipants = mission.GetAllParticipants();

            foreach (IMovable movable in allParticipants.Cast<IMovable>())
            {
                if (origin != null)
                    _movementManager.RequestMove(movable, origin);
            }

            _game.DetachNode(mission);
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
