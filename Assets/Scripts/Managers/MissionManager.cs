using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///
/// </summary>
public enum MissionType
{
    Diplomacy,
    Recruitment,
}

/// <summary>
/// Manager for handling missions in the game.
/// This includes scheduling missions and rescheduling mission events.
/// </summary>
public class MissionManager
{
    private Game game;
    private readonly Random random = new Random();

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    public MissionManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="missionType"></param>
    /// <param name="ownerInstanceId"></param>
    /// <param name="mainParticipants"></param>
    /// <param name="decoyParticipants"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private Mission CreateMission(
        MissionType missionType,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target
    )
    {
        return missionType switch
        {
            MissionType.Diplomacy => new DiplomacyMission(
                ownerInstanceId,
                target.InstanceID,
                mainParticipants,
                decoyParticipants
            ),
            MissionType.Recruitment => new RecruitmentMission(
                ownerInstanceId,
                target.InstanceID,
                mainParticipants,
                decoyParticipants
            ),
            _ => throw new ArgumentException($"Unhandled mission type: {missionType}"),
        };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="type"></param>
    /// <param name="participant"></param>
    /// <param name="target"></param>
    public void InitiateMission(
        MissionType type,
        IMissionParticipant participant,
        ISceneNode target
    )
    {
        List<IMissionParticipant> mainParticipants = new List<IMissionParticipant> { participant };
        List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
        string ownerInstanceId = participant.OwnerInstanceID;

        InitiateMission(type, ownerInstanceId, mainParticipants, decoyParticipants, target);
    }

    /// <summary>
    /// Initiates a mission with the specified parameters.
    /// The mission is scheduled to occur at the next possible tick.
    /// </summary>
    /// <param name="missionType">The type of mission to initiate.</param>
    /// <param name="ownerInstanceId">The Instance ID of the owner of the mission.</param>
    /// <param name="mainParticipants">The main participants of the mission.</param>
    /// <param name="decoyParticipants">The decoy participants of the mission.</param>
    /// <param name="target">The target of the mission. This can be a planet or a unit.</param>
    public void InitiateMission(
        MissionType missionType,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target
    )
    {
        if (mainParticipants.Count == 0)
        {
            throw new ArgumentException("Main participants list cannot be empty.");
        }

        // Get the nearest planet related to the target and the participants' current planet.
        Planet closestPlanet = target is Planet ? (Planet)target : target.GetParentOfType<Planet>();
        IMissionParticipant firstParticipant = mainParticipants.FirstOrDefault();
        Planet currentPlanet = firstParticipant.GetParentOfType<Planet>();

        // Instantiate the mission based on the mission type.
        Mission mission = CreateMission(
            missionType,
            ownerInstanceId,
            mainParticipants,
            decoyParticipants,
            target
        );

        // Attach the mission to scene graph.
        game.AttachNode(mission, closestPlanet, false);

        // Initiate the mission with the given arguments.
        // This will set the movement status of all participants to InTransit.
        mission.Initiate();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="mission"></param>
    public void UpdateMission(Mission mission)
    {
        List<Mission> missions = game.GetSceneNodesByType<Mission>();
        GalaxyMap galaxyMap = game.GetGalaxyMap();

        // Increment the mission progress.
        mission.IncrementProgress();

        if (mission.IsComplete())
        {
            // Evaluate the mission success.
            mission.Execute(game);

            // Check if the mission can continue.
            // If so, reset the mission progress and re-initiate the mission.
            if (mission.CanContinue(game))
            {
                mission.Initiate();
            }
            // Otherwise, move the participants to the closest planet and remove the mission.
            else
            {
                // Move the units to the closest planet.
                List<IMovable> combinedParticipants = mission
                    .GetAllParticipants()
                    .Cast<IMovable>()
                    .ToList();

                Faction faction = game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
                Planet planet = faction.GetNearestPlanetTo(mission);

                foreach (IMovable movable in combinedParticipants)
                {
                    movable.MoveTo(planet);
                }

                // Permanently remove the mission from the game.
                game.DetachNode(mission);
            }
        }
    }
}
