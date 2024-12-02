using System;
using System.Collections.Generic;

/// <summary>
///
/// </summary>
public enum MissionType
{
    Diplomacy,
    Recruitment,
}

/// <summary>
/// Factory class responsible for creating and initializing missions.
/// </summary>
public class MissionFactory
{
    private readonly Game game;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionFactory"/> class.
    /// </summary>
    /// <param name="game">The game instance for which the factory creates missions.</param>
    public MissionFactory(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// Creates a mission based on the specified mission type and parameters.
    /// </summary>
    /// <param name="missionType">The type of mission to create.</param>
    /// <param name="ownerInstanceId">The Instance ID of the owner of the mission.</param>
    /// <param name="mainParticipants">The main participants of the mission.</param>
    /// <param name="decoyParticipants">The decoy participants of the mission.</param>
    /// <param name="target">The target of the mission.</param>
    /// <returns>A new instance of the requested mission type.</returns>
    public Mission CreateMission(
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
    /// Creates and initiates a mission, attaching it to the game scene graph.
    /// </summary>
    /// <param name="missionType">The type of mission to create.</param>
    /// <param name="ownerInstanceId">The Instance ID of the mission owner.</param>
    /// <param name="mainParticipants">The main participants of the mission.</param>
    /// <param name="decoyParticipants">The decoy participants of the mission.</param>
    /// <param name="target">The target of the mission.</param>
    public Mission CreateAndInitiateMission(
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

        // Create the mission.
        Mission mission = CreateMission(
            missionType,
            ownerInstanceId,
            mainParticipants,
            decoyParticipants,
            target
        );

        // Determine the closest planet to the target for attaching the mission.
        Planet closestPlanet = target is Planet ? (Planet)target : target.GetParentOfType<Planet>();

        // Attach the mission to the scene graph.
        game.AttachNode(mission, closestPlanet);

        // Initiate the mission (e.g., set participants to InTransit).
        mission.Initiate();

        return mission;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="missionTypeString"></param>
    /// <param name="ownerInstanceId"></param>
    /// <param name="mainParticipants"></param>
    /// <param name="decoyParticipants"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public Mission CreateAndInitiateMission(
        string missionTypeString,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target
    )
    {
        if (Enum.TryParse(missionTypeString, true, out MissionType missionType))
        {
            return CreateAndInitiateMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target
            );
        }
        else
        {
            throw new ArgumentException($"Invalid mission type: {missionTypeString} .");
        }
    }
}
