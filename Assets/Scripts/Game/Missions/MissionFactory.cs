using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Thin router that delegates mission creation to each Mission subclass's TryCreate method.
/// Handles shared validation (faction restrictions, participant eligibility) before routing.
/// </summary>
public class MissionFactory
{
    private readonly GameRoot _game;
    private readonly FogOfWarSystem _fogOfWar;

    public MissionFactory(GameRoot game, FogOfWarSystem fogOfWar = null)
    {
        _game = game;
        _fogOfWar = fogOfWar;
    }

    /// <summary>
    /// Returns whether a mission of the given type can be created for the target.
    /// Uses the same TryCreate path as CreateMission without requiring participants.
    /// </summary>
    public bool CanCreateMission(
        MissionType missionType,
        string ownerInstanceId,
        ISceneNode target,
        Officer targetOfficer = null
    )
    {
        Faction faction = _game.Factions.Find(f => f.InstanceID == ownerInstanceId);
        if (faction?.DisallowedMissionTypes.Contains(missionType) == true)
            return false;

        MissionContext ctx = new MissionContext
        {
            Game = _game,
            OwnerInstanceId = ownerInstanceId,
            Target = target,
            MainParticipants = new List<IMissionParticipant>(),
            DecoyParticipants = new List<IMissionParticipant>(),
            FogOfWar = _fogOfWar,
            TargetOfficer = targetOfficer,
        };

        return RouteToTryCreate(missionType, ctx) != null;
    }

    /// <summary>
    /// Creates a mission of the specified type, or throws if creation fails.
    /// Performs shared validation, then delegates to the mission-specific TryCreate.
    /// </summary>
    public Mission CreateMission(
        MissionType missionType,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target,
        IRandomNumberProvider provider = null,
        Officer targetOfficer = null
    )
    {
        if (mainParticipants == null || mainParticipants.Count == 0)
            throw new ArgumentException(
                "At least one main participant is required.",
                nameof(mainParticipants)
            );

        Faction faction = _game.Factions.Find(f => f.InstanceID == ownerInstanceId);
        if (faction?.DisallowedMissionTypes.Contains(missionType) == true)
            throw new InvalidOperationException(
                $"Faction '{ownerInstanceId}' cannot perform {missionType} missions."
            );

        foreach (IMissionParticipant participant in mainParticipants.Concat(decoyParticipants))
        {
            if (!participant.CanPerformMission(missionType))
                throw new InvalidOperationException(
                    $"Participant '{((ISceneNode)participant).GetDisplayName()}' cannot perform {missionType} missions."
                );
        }

        MissionContext ctx = new MissionContext
        {
            Game = _game,
            OwnerInstanceId = ownerInstanceId,
            Target = target,
            MainParticipants = mainParticipants,
            DecoyParticipants = decoyParticipants,
            RNG = provider,
            FogOfWar = _fogOfWar,
            TargetOfficer = targetOfficer,
        };

        Mission mission =
            RouteToTryCreate(missionType, ctx)
            ?? throw new InvalidOperationException(
                $"Cannot create {missionType} mission with the given parameters."
            );

        GameConfig.MissionProbabilityTablesConfig missionTables = _game
            .Config
            ?.ProbabilityTables
            ?.Mission;

        if (missionTables != null)
            mission.Configure(missionTables);

        return mission;
    }

    /// <summary>
    /// Routes to the appropriate Mission subclass TryCreate based on mission type.
    /// </summary>
    private static Mission RouteToTryCreate(MissionType missionType, MissionContext ctx)
    {
        return missionType switch
        {
            MissionType.Diplomacy => DiplomacyMission.TryCreate(ctx),
            MissionType.Recruitment => RecruitmentMission.TryCreate(ctx),
            MissionType.SubdueUprising => SubdueUprisingMission.TryCreate(ctx),
            MissionType.Abduction => AbductionMission.TryCreate(ctx),
            MissionType.Assassination => AssassinationMission.TryCreate(ctx),
            MissionType.Espionage => EspionageMission.TryCreate(ctx),
            MissionType.Sabotage => SabotageMission.TryCreate(ctx),
            MissionType.InciteUprising => InciteUprisingMission.TryCreate(ctx),
            MissionType.Rescue => RescueMission.TryCreate(ctx),
            MissionType.ShipDesignResearch => ResearchMission.TryCreate(
                ctx,
                ManufacturingType.Ship
            ),
            MissionType.TroopTrainingResearch => ResearchMission.TryCreate(
                ctx,
                ManufacturingType.Troop
            ),
            MissionType.FacilityDesignResearch => ResearchMission.TryCreate(
                ctx,
                ManufacturingType.Building
            ),
            MissionType.JediTraining => JediTrainingMission.TryCreate(ctx),
            _ => null,
        };
    }
}
