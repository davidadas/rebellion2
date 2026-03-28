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
    /// Creation and initiation of missions are delegated to MissionFactory.
    /// </summary>
    public class MissionSystem
    {
        private readonly GameRoot game;
        private readonly MovementSystem movementManager;
        private readonly MissionFactory missionFactory;

        public MissionSystem(GameRoot game, MovementSystem movementManager)
        {
            this.game = game;
            this.movementManager = movementManager;
            this.missionFactory = new MissionFactory(game, movementManager);
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

            missionFactory.CreateAndInitiateMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                provider
            );
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
                mission.Initiate(game, movementManager, provider);
            }
            else
            {
                Faction faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
                Planet nearestPlanet = faction.GetNearestPlanetTo(mission);
                if (nearestPlanet == null)
                {
                    Planet missionPlanet = mission.GetParent() as Planet;
                    if (missionPlanet?.OwnerInstanceID == mission.OwnerInstanceID)
                        nearestPlanet = missionPlanet;
                }

                foreach (IMovable movable in mission.GetAllParticipants().Cast<IMovable>())
                {
                    if (nearestPlanet != null)
                    {
                        movementManager.RequestMove(movable, nearestPlanet);

                        results.Add(
                            new CharacterMovedResult
                            {
                                CharacterInstanceID = ((ISceneNode)movable).GetInstanceID(),
                                FromLocationInstanceID = mission.InstanceID,
                                ToLocationInstanceID = nearestPlanet.InstanceID,
                                Tick = game.CurrentTick,
                            }
                        );
                    }
                }

                game.DetachNode(mission);
            }

            return results;
        }
    }
}
