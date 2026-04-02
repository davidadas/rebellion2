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
        private readonly GameRoot game;
        private readonly MovementSystem movementManager;
        private readonly OwnershipSystem ownershipSystem;
        private readonly MissionFactory missionFactory;

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
            this.game = game;
            this.movementManager = movementManager;
            this.ownershipSystem = ownershipSystem;
            this.missionFactory = new MissionFactory(game, fogOfWar);
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

            Mission mission = missionFactory.CreateAndAttachMission(
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
            if (mission.IsCanceled(game))
            {
                TearDownMission(mission, results);
                return results;
            }

            mission.IncrementProgress();

            if (!mission.IsComplete())
                return results;

            results.AddRange(mission.Execute(game, provider));

            foreach (GameResult result in results)
            {
                if (result is PlanetOwnershipChangedResult ownershipResult)
                {
                    Planet planet = game.GetSceneNodeByInstanceID<Planet>(
                        ownershipResult.PlanetInstanceID
                    );
                    Faction newOwner = game.GetFactionByOwnerInstanceID(
                        ownershipResult.NewOwnerInstanceID
                    );
                    if (planet != null && newOwner != null)
                        ownershipSystem.TransferPlanet(planet, newOwner);
                }
            }

            if (mission.CanContinue(game))
            {
                BeginMission(mission, provider);
            }
            else
            {
                TearDownMission(mission, results);
            }

            return results;
        }

        /// <summary>
        /// Moves all participants to the nearest friendly planet and detaches the mission.
        /// Called both when CanContinue returns false after completion and when IsCanceled
        /// fires as a pre-tick guard.
        /// </summary>
        private void TearDownMission(Mission mission, List<GameResult> results)
        {
            Planet missionPlanet = mission.GetParent() as Planet;
            Faction faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            Planet nearestPlanet = faction.GetNearestFriendlyPlanetTo(mission);
            if (nearestPlanet == null)
            {
                if (missionPlanet?.OwnerInstanceID == mission.OwnerInstanceID)
                    nearestPlanet = missionPlanet;
            }

            List<IMissionParticipant> allParticipants = mission.GetAllParticipants();

            foreach (IMovable movable in allParticipants.Cast<IMovable>())
            {
                ISceneNode participantNode = (ISceneNode)movable;

                if (nearestPlanet != null)
                {
                    // RequestMove reparents the participant to nearestPlanet on success,
                    // which calls mission.RemoveChild and drains the participant lists.
                    // It silently returns (without reparenting) for captured officers or on
                    // SceneAccessException. Check the parent to confirm the move succeeded.
                    movementManager.RequestMove(movable, nearestPlanet);
                    if (participantNode.GetParent() == nearestPlanet)
                    {
                        results.Add(
                            new CharacterMovedResult
                            {
                                CharacterInstanceID = participantNode.GetInstanceID(),
                                FromLocationInstanceID = mission.InstanceID,
                                ToLocationInstanceID = nearestPlanet.InstanceID,
                                Tick = game.CurrentTick,
                            }
                        );
                    }
                    else if (
                        missionPlanet != null
                        && missionPlanet.OwnerInstanceID == mission.OwnerInstanceID
                    )
                    {
                        // RequestMove was rejected; reparent to the mission planet so the
                        // participant is not orphaned when DetachNode(mission) runs below.
                        game.AttachNode(participantNode, missionPlanet);
                    }
                }
                else if (
                    missionPlanet != null
                    && missionPlanet.OwnerInstanceID == mission.OwnerInstanceID
                )
                {
                    // No friendly planet; keep participant at the current planet rather than
                    // losing them when the mission node is detached.
                    game.AttachNode(participantNode, missionPlanet);
                }
            }

            game.DetachNode(mission);
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
                    movementManager.RequestMove(participant, mission);
                }
            }

            mission.Initiate(provider);
        }
    }
}
