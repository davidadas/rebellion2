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

    public MissionManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="missionType"></param>
    /// <param name="ownerTypeId"></param>
    /// <param name="mainParticipants"></param>
    /// <param name="decoyParticipants"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private Mission CreateMission(
        MissionType missionType,
        string ownerTypeId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        SceneNode target
    )
    {
        return missionType switch
        {
            MissionType.Diplomacy => new DiplomacyMission(
                ownerTypeId,
                target.TypeID,
                mainParticipants,
                decoyParticipants
            ),
            MissionType.Recruitment => new RecruitmentMission(
                ownerTypeId,
                target.TypeID,
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
    public void InitiateMission(MissionType type, IMissionParticipant participant, SceneNode target)
    {
        List<IMissionParticipant> mainParticipants = new List<IMissionParticipant> { participant };
        List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant>();
        string ownerTypeId = (participant as SceneNode).OwnerTypeID;

        InitiateMission(type, ownerTypeId, mainParticipants, decoyParticipants, target);
    }

    /// <summary>
    /// Initiates a mission with the specified parameters.
    /// The mission is scheduled to occur at the next possible tick.
    /// </summary>
    /// <param name="missionType">The type of mission to initiate.</param>
    /// <param name="ownerTypeId">The type ID of the owner of the mission.</param>
    /// <param name="mainParticipants">The main participants of the mission.</param>
    /// <param name="decoyParticipants">The decoy participants of the mission.</param>
    /// <param name="target">The target of the mission. This can be a planet or a unit.</param>
    public void InitiateMission(
        MissionType missionType,
        string ownerTypeId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        SceneNode target
    )
    {
        if (mainParticipants.Count == 0)
        {
            throw new ArgumentException("Main participants list cannot be empty.");
        }
        else if (target == null)
        {
            throw new ArgumentException("Target cannot be null.");
        }

        // Get the nearest planet related to the target and the participants' current planet.
        Planet closestPlanet = target is Planet ? (Planet)target : target.GetParentOfType<Planet>();
        IMissionParticipant firstParticipant = mainParticipants.FirstOrDefault();
        Planet currentPlanet = (firstParticipant as SceneNode).GetParentOfType<Planet>();

        // Instantiate the mission based on the mission type.
        Mission mission = CreateMission(
            missionType,
            ownerTypeId,
            mainParticipants,
            decoyParticipants,
            target
        );

        int missionLength = random.Next(mission.GetTickRange()[0], mission.GetTickRange()[1]);
        int executionTick =
            game.CurrentTick + currentPlanet.GetTravelTime(closestPlanet) + missionLength;
        mission.SetExecutionTick(executionTick);

        // Attach the mission to scene graph.
        game.AttachNode(mission, closestPlanet);

        // Set the movement status of all participants to InTransit.
        foreach (IMovable movable in mainParticipants.Concat(decoyParticipants).OfType<IMovable>())
        {
            movable.MoveTo(mission);
        }
    }

    public void Update()
    {
        List<Mission> missions = game.GetSceneNodesByType<Mission>();
        GalaxyMap galaxyMap = game.GetGalaxyMap();

        // Check if any missions are complete and execute the corresponding event.
        foreach (Mission mission in missions)
        {
            // Increment the mission progress.
            mission.IncrementProgress();

            if (mission.IsComplete())
            {
                mission.Execute(game);

                // Move the units to the closest planet.
                List<IMovable> combinedParticipants = mission
                    .GetAllParticipants()
                    .Cast<IMovable>()
                    .ToList();
                Planet planet = galaxyMap.GetClosestFriendlyPlanet(mission);

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
