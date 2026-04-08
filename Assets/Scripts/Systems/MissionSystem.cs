using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages the lifecycle of missions each tick.
    /// Mission creation and scene graph attachment are delegated to MissionFactory.
    /// Participant movement and mission initiation are orchestrated here.
    /// </summary>
    public class MissionSystem
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movementManager;
        private readonly OwnershipSystem _ownershipSystem;
        private readonly MissionFactory _missionFactory;

        /// <summary>
        /// Creates a MissionSystem wired to the given game, movement, and ownership systems.
        /// </summary>
        public MissionSystem(
            GameRoot game,
            MovementSystem movementManager,
            OwnershipSystem ownershipSystem,
            FogOfWarSystem fogOfWar = null
        )
        {
            _game = game;
            _movementManager = movementManager;
            _ownershipSystem = ownershipSystem;
            _missionFactory = new MissionFactory(game, fogOfWar);
        }

        /// <summary>
        /// Initiates a mission with a single participant and target.
        /// </summary>
        public void InitiateMission(
            MissionType missionType,
            IMissionParticipant participant,
            ISceneNode target,
            IRandomNumberProvider provider
        )
        {
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant>
            {
                participant,
            };
            List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
            string ownerInstanceId = participant.OwnerInstanceID;

            Mission mission = _missionFactory.CreateAndAttachMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                provider
            );

            BeginMission(mission, provider);
        }

        /// <summary>
        /// Processes all active missions and returns aggregate results.
        /// </summary>
        public List<GameResult> ProcessTick(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            List<Mission> missions = game.GetSceneNodesByType<Mission>();

            foreach (Mission mission in missions)
            {
                results.AddRange(UpdateMission(mission, provider));
            }

            return results;
        }

        /// <summary>
        /// Updates a single mission's state for this tick.
        /// Returns results from execution and participant movement on completion.
        /// </summary>
        public List<GameResult> UpdateMission(Mission mission, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();

            // Pre-tick guard: cancel immediately if an external event has invalidated this mission.
            if (mission.IsCanceled(_game))
            {
                TearDownMission(mission);
                return results;
            }

            mission.IncrementProgress();

            if (!mission.IsComplete())
                return results;

            results.AddRange(mission.Execute(_game, provider));

            foreach (GameResult result in results)
            {
                if (result is PlanetOwnershipChangedResult ownershipResult)
                {
                    Planet planet = _game.GetSceneNodeByInstanceID<Planet>(
                        ownershipResult.PlanetInstanceID
                    );
                    Faction newOwner = _game.GetFactionByOwnerInstanceID(
                        ownershipResult.NewOwnerInstanceID
                    );
                    if (planet != null && newOwner != null)
                        _ownershipSystem.TransferPlanet(planet, newOwner);
                }
            }

            if (mission.CanContinue(_game))
            {
                BeginMission(mission, provider);
            }
            else
            {
                TearDownMission(mission);
            }

            return results;
        }

        /// <summary>
        /// Moves all participants to the nearest friendly planet and detaches the mission.
        /// Called both when CanContinue returns false after completion and when IsCanceled
        /// fires as a pre-tick guard.
        /// </summary>
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
        private void BeginMission(Mission mission, IRandomNumberProvider provider)
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

            mission.Initiate(provider);
        }
    }
}
