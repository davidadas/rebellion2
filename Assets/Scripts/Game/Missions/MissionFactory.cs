using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

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
    private readonly GameRoot game;
    private readonly MovementSystem movementManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionFactory"/> class.
    /// </summary>
    /// <param name="game">The game instance for which the factory creates missions.</param>
    /// <param name="movementManager">The movement manager for ordering unit movement.</param>
    public MissionFactory(GameRoot game, MovementSystem movementManager)
    {
        this.game = game;
        this.movementManager = movementManager;
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
        // Get probability tables from GameConfig
        var missionTables = game.Config?.ProbabilityTables?.Mission;

        return missionType switch
        {
            MissionType.Diplomacy => new DiplomacyMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                missionTables != null ? new ProbabilityTable(missionTables.Diplomacy) : null
            ),
            MissionType.Recruitment => new RecruitmentMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                missionTables != null ? new ProbabilityTable(missionTables.Recruitment) : null
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
    /// <param name="provider">Random number provider for mission duration.</param>
    public Mission CreateAndInitiateMission(
        MissionType missionType,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target,
        IRandomNumberProvider provider
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

        // Initiate the mission (e.g., order participants to move to mission location).
        mission.Initiate(game, movementManager, provider);

        return mission;
    }

    /// <summary>
    /// Creates and initiates a mission from a string mission type.
    /// </summary>
    /// <param name="missionTypeString">String representation of mission type.</param>
    /// <param name="ownerInstanceId">The Instance ID of the mission owner.</param>
    /// <param name="mainParticipants">The main participants of the mission.</param>
    /// <param name="decoyParticipants">The decoy participants of the mission.</param>
    /// <param name="target">The target of the mission.</param>
    /// <param name="provider">Random number provider for mission duration.</param>
    /// <returns>The created and initiated mission.</returns>
    public Mission CreateAndInitiateMission(
        string missionTypeString,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target,
        IRandomNumberProvider provider
    )
    {
        if (Enum.TryParse(missionTypeString, true, out MissionType missionType))
        {
            return CreateAndInitiateMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                provider
            );
        }
        else
        {
            throw new ArgumentException($"Invalid mission type: {missionTypeString} .");
        }
    }
}
