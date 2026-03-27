using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manager for handling the lifecycle of missions in the game.
    /// Responsible for updating missions and managing their progression.
    /// Creation and initiation of missions are delegated to the <see cref="MissionFactory"/>.
    /// </summary>
    public class MissionSystem
    {
        private readonly GameRoot game;
        private readonly MovementSystem movementManager;
        private readonly MissionFactory missionFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MissionSystem"/> class.
        /// </summary>
        /// <param name="game">The game instance being managed.</param>
        /// <param name="movementManager">The movement manager for ordering unit movement.</param>
        public MissionSystem(GameRoot game, MovementSystem movementManager)
        {
            this.game = game;
            this.movementManager = movementManager;
            // Initialize the MissionFactory for mission creation and initiation.
            this.missionFactory = new MissionFactory(game, movementManager);
        }

        /// <summary>
        /// Initiates a mission with a single participant and target.
        /// </summary>
        /// <param name="missionType">The type of mission to initiate.</param>
        /// <param name="participant">The main participant of the mission.</param>
        /// <param name="target">The target of the mission.</param>
        /// <param name="provider">Random number provider for mission duration.</param>
        public void InitiateMission(
            MissionType missionType,
            IMissionParticipant participant,
            ISceneNode target,
            IRandomNumberProvider provider
        )
        {
            // Wrap the participant in a list to use the overload for multiple participants.
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
        /// Processes all active missions for the current tick.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for probability checks.</param>
        public void ProcessTick(GameRoot game, IRandomNumberProvider provider)
        {
            List<Mission> missions = game.GetSceneNodesByType<Mission>();

            foreach (Mission mission in missions)
            {
                UpdateMission(mission, provider);
            }
        }

        /// <summary>
        /// Updates the state of an ongoing mission.
        /// This involves incrementing mission progress, evaluating success,
        /// and handling mission completion or continuation.
        /// </summary>
        /// <param name="mission">The mission to update.</param>
        /// <param name="provider">Random number provider for probability checks.</param>
        public void UpdateMission(Mission mission, IRandomNumberProvider provider)
        {
            // Increment the mission's progress.
            mission.IncrementProgress();

            // Check if the mission is complete.
            if (mission.IsComplete())
            {
                // Evaluate the mission's success or failure.
                mission.Execute(game, provider);

                // Check if the mission can continue (e.g., repeatable missions).
                if (mission.CanContinue(game))
                {
                    // Reset progress and re-initiate the mission.
                    mission.Initiate(game, movementManager, provider);
                }
                else
                {
                    // Handle mission completion and return participants to the nearest planet.

                    // Get all participants (both main and decoy) that can be moved.
                    List<Rebellion.Game.IMovable> combinedParticipants = mission
                        .GetAllParticipants()
                        .Cast<Rebellion.Game.IMovable>()
                        .ToList();

                    // Find the nearest planet to the mission's location for participants to return to.
                    Rebellion.Game.Faction faction = game.GetFactionByOwnerInstanceID(
                        mission.OwnerInstanceID
                    );
                    Rebellion.Game.Planet nearestPlanet = faction.GetNearestPlanetTo(mission);

                    // Order each participant to move to the nearest planet.
                    foreach (Rebellion.Game.IMovable movable in combinedParticipants)
                    {
                        movementManager.RequestMove(movable, nearestPlanet);
                    }

                    // Remove the mission from the game permanently.
                    game.DetachNode(mission);
                }
            }
        }
    }
}
