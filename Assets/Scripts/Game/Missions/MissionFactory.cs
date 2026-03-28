using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

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

    public MissionFactory(GameRoot game, MovementSystem movementManager)
    {
        this.game = game;
        this.movementManager = movementManager;
    }

    /// <summary>
    /// Creates a mission based on the specified type and parameters.
    /// For targeted missions (Recruitment), target selection requires a provider.
    /// </summary>
    public Mission CreateMission(
        MissionType missionType,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target,
        IRandomNumberProvider provider = null
    )
    {
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
                SelectRecruitmentTarget(ownerInstanceId, provider),
                missionTables != null ? new ProbabilityTable(missionTables.Recruitment) : null
            ),
            _ => throw new ArgumentException($"Unhandled mission type: {missionType}"),
        };
    }

    /// <summary>
    /// Creates and initiates a mission, attaching it to the game scene graph.
    /// </summary>
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
            throw new ArgumentException("Main participants list cannot be empty.");

        Mission mission = CreateMission(
            missionType,
            ownerInstanceId,
            mainParticipants,
            decoyParticipants,
            target,
            provider
        );

        Planet closestPlanet = target is Planet ? (Planet)target : target.GetParentOfType<Planet>();
        game.AttachNode(mission, closestPlanet);
        mission.Initiate(game, movementManager, provider);

        return mission;
    }

    /// <summary>
    /// Creates and initiates a mission from a string mission type.
    /// </summary>
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

        throw new ArgumentException($"Invalid mission type: {missionTypeString} .");
    }

    private string SelectRecruitmentTarget(string ownerInstanceId, IRandomNumberProvider provider)
    {
        List<Officer> unrecruited = game.GetUnrecruitedOfficers(ownerInstanceId);
        if (unrecruited.Count == 0 || provider == null)
            return null;
        return unrecruited.RandomElement(provider).InstanceID;
    }
}
