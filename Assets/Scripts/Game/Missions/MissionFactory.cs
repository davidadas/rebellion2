using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

/// <summary>
///
/// </summary>
public enum MissionType
{
    Diplomacy,
    Recruitment,
    SubdueUprising,
}

/// <summary>
/// Factory class responsible for creating and initializing missions.
/// </summary>
public class MissionFactory
{
    private readonly GameRoot game;

    public MissionFactory(GameRoot game)
    {
        this.game = game;
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
        GameConfig.MissionProbabilityTablesConfig missionTables = game.Config
            ?.ProbabilityTables
            ?.Mission;

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
            MissionType.SubdueUprising => new SubdueUprisingMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                missionTables != null ? new ProbabilityTable(missionTables.SubdueUprising) : null
            ),
            _ => throw new ArgumentException($"Unhandled mission type: {missionType}"),
        };
    }

    /// <summary>
    /// Creates a mission and attaches it to the scene graph at the target planet.
    /// Participant movement and mission initiation are handled by MissionSystem.
    /// </summary>
    public Mission CreateAndAttachMission(
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

        return mission;
    }

    /// <summary>
    /// Creates and attaches a mission from a string mission type.
    /// </summary>
    public Mission CreateAndAttachMission(
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
            return CreateAndAttachMission(
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
