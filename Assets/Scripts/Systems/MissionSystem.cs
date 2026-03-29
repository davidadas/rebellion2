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

        public MissionSystem(
            GameRoot game,
            MovementSystem movementManager,
            OwnershipSystem ownershipSystem
        )
        {
            this.game = game;
            this.movementManager = movementManager;
            this.ownershipSystem = ownershipSystem;
            this.missionFactory = new MissionFactory(game);
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

            mission.IncrementProgress();

            if (!mission.IsComplete())
                return results;

            results.AddRange(mission.Execute(game, provider));

            if (mission.CanContinue(game))
            {
                BeginMission(mission, provider);
            }
            else
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
                        // RequestMove immediately reparents the participant to nearestPlanet,
                        // which calls mission.RemoveChild and drains the participant lists.
                        // DetachNode below will find no children to deregister.
                        movementManager.RequestMove(movable, nearestPlanet);
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
                    // If nearestPlanet is null the faction has no planets; officer is lost.
                }

                game.DetachNode(mission);
            }

            return results;
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
