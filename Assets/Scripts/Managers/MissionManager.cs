using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manager for handling the lifecycle of missions in the game.
/// Responsible for updating missions and managing their progression.
/// Creation and initiation of missions are delegated to the <see cref="MissionFactory"/>.
/// </summary>
public class MissionManager
{
    private readonly Game game;
    private readonly MissionFactory missionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionManager"/> class.
    /// </summary>
    /// <param name="game">The game instance being managed.</param>
    public MissionManager(Game game)
    {
        this.game = game;
        // Initialize the MissionFactory for mission creation and initiation.
        this.missionFactory = new MissionFactory(game);
    }

    /// <summary>
    /// Initiates a mission with a single participant and target.
    /// </summary>
    /// <param name="missionType">The type of mission to initiate.</param>
    /// <param name="participant">The main participant of the mission.</param>
    /// <param name="target">The target of the mission.</param>
    public void InitiateMission(
        MissionType missionType,
        IMissionParticipant participant,
        ISceneNode target
    )
    {
        // Wrap the participant in a list to use the overload for multiple participants.
        List<IMissionParticipant> mainParticipants = new List<IMissionParticipant> { participant };
        List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
        string ownerInstanceId = participant.OwnerInstanceID;

        missionFactory.CreateAndInitiateMission(
            missionType,
            ownerInstanceId,
            mainParticipants,
            decoyParticipants,
            target
        );
    }

    /// <summary>
    /// Updates the state of an ongoing mission.
    /// This involves incrementing mission progress, evaluating success,
    /// and handling mission completion or continuation.
    /// </summary>
    /// <param name="mission">The mission to update.</param>
    public void UpdateMission(Mission mission)
    {
        // Increment the mission's progress.
        mission.IncrementProgress();

        // Check if the mission is complete.
        if (mission.IsComplete())
        {
            // Evaluate the mission's success or failure.
            mission.Execute(game);

            // Check if the mission can continue (e.g., repeatable missions).
            if (mission.CanContinue(game))
            {
                // Reset progress and re-initiate the mission.
                mission.Initiate();
            }
            else
            {
                // Handle mission completion and return participants to the nearest planet.

                // Get all participants (both main and decoy) that can be moved.
                List<IMovable> combinedParticipants = mission
                    .GetAllParticipants()
                    .Cast<IMovable>()
                    .ToList();

                // Find the nearest planet to the mission's location for participants to return to.
                Faction faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
                Planet nearestPlanet = faction.GetNearestPlanetTo(mission);

                // Move each participant to the nearest planet.
                foreach (IMovable movable in combinedParticipants)
                {
                    movable.MoveTo(nearestPlanet);
                }

                // Remove the mission from the game permanently.
                game.DetachNode(mission);
            }
        }
    }
}
