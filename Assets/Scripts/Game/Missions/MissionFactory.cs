using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Extensions;

/// <summary>
/// Identifies the type of covert mission to create.
/// </summary>
public enum MissionType
{
    Diplomacy,
    Recruitment,
    SubdueUprising,
    Abduction,
    Assassination,
    Espionage,
    Sabotage,
    InciteUprising,
    Rescue,
    ShipDesignResearch,
    TroopTrainingResearch,
    FacilityDesignResearch,
    JediTraining,
}

/// <summary>
/// Factory class responsible for creating and initializing missions.
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
    /// Returns whether a mission of the given type can be created with the specified parameters.
    /// Checks the same preconditions that constructors enforce, but returns false instead of throwing.
    /// </summary>
    public bool CanCreateMission(
        MissionType missionType,
        string ownerInstanceId,
        ISceneNode target,
        IRandomNumberProvider provider = null
    )
    {
        if (!(target is Planet planet))
            return missionType == MissionType.Recruitment;

        return missionType switch
        {
            MissionType.Diplomacy => planet.IsColonized
                && !planet.IsInUprising
                && planet.WasVisitedBy(ownerInstanceId)
                && planet.GetPopularSupport(ownerInstanceId) < 100
                && (
                    planet.GetOwnerInstanceID() == null
                    || planet.GetOwnerInstanceID() == ownerInstanceId
                ),

            MissionType.Recruitment => SelectRecruitmentTarget(ownerInstanceId, provider) != null,

            MissionType.SubdueUprising => planet.IsInUprising
                && planet.GetOwnerInstanceID() == ownerInstanceId,

            MissionType.Abduction => SelectAbductionTarget(ownerInstanceId, target, provider)
                != null,

            MissionType.Assassination => SelectAssassinationTarget(
                ownerInstanceId,
                target,
                provider
            ) != null,

            MissionType.Espionage => planet.WasVisitedBy(ownerInstanceId)
                && planet.GetOwnerInstanceID() != ownerInstanceId,

            MissionType.Sabotage => true,

            MissionType.InciteUprising => planet.GetOwnerInstanceID() != ownerInstanceId
                && !planet.IsInUprising,

            MissionType.Rescue => SelectRescueTarget(ownerInstanceId, target, provider) != null,

            MissionType.ShipDesignResearch => planet.GetOwnerInstanceID() == ownerInstanceId,

            MissionType.TroopTrainingResearch => planet.GetOwnerInstanceID() == ownerInstanceId,

            MissionType.FacilityDesignResearch => planet.GetOwnerInstanceID() == ownerInstanceId,

            MissionType.JediTraining => planet.GetOwnerInstanceID() == ownerInstanceId
                && SelectJediTeacher(ownerInstanceId, target) != null,

            _ => false,
        };
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
        GameConfig.MissionProbabilityTablesConfig missionTables = _game
            .Config
            ?.ProbabilityTables
            ?.Mission;

        Mission mission = missionType switch
        {
            MissionType.Diplomacy => new DiplomacyMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            ),
            MissionType.Recruitment => new RecruitmentMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                SelectRecruitmentTarget(ownerInstanceId, provider)
            ),
            MissionType.SubdueUprising => new SubdueUprisingMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            ),
            MissionType.Abduction => new AbductionMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                SelectAbductionTarget(ownerInstanceId, target, provider)
            ),
            MissionType.Assassination => new AssassinationMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                SelectAssassinationTarget(ownerInstanceId, target, provider)
            ),
            MissionType.Espionage => new EspionageMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                _fogOfWar
            ),
            MissionType.Sabotage => new SabotageMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            ),
            MissionType.InciteUprising => new InciteUprisingMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            ),
            MissionType.Rescue => new RescueMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                SelectRescueTarget(ownerInstanceId, target, provider)
            ),
            MissionType.ShipDesignResearch => new ResearchMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                ManufacturingType.Ship
            ),
            MissionType.TroopTrainingResearch => new ResearchMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                ManufacturingType.Troop
            ),
            MissionType.FacilityDesignResearch => new ResearchMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                ManufacturingType.Building
            ),
            MissionType.JediTraining => new JediTrainingMission(
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                SelectJediTeacher(ownerInstanceId, target)
            ),
            _ => throw new ArgumentException($"Unhandled mission type: {missionType}"),
        };

        if (missionTables != null)
            mission.Configure(missionTables);

        return mission;
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
        _game.AttachNode(mission, closestPlanet);

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
        List<Officer> unrecruited = _game.GetUnrecruitedOfficers(ownerInstanceId);
        if (unrecruited.Count == 0 || provider == null)
            return null;
        return unrecruited.RandomElement(provider).InstanceID;
    }

    private string SelectAbductionTarget(
        string ownerInstanceId,
        ISceneNode target,
        IRandomNumberProvider provider
    )
    {
        if (!(target is Planet planet) || provider == null)
            return null;
        List<Officer> enemies = _game
            .GetSceneNodesByType<Officer>()
            .Where(o =>
                o.GetOwnerInstanceID() != ownerInstanceId
                && o.GetParentOfType<Planet>() == planet
                && !o.IsCaptured
            )
            .ToList();
        return enemies.Count > 0 ? enemies.RandomElement(provider).InstanceID : null;
    }

    private string SelectAssassinationTarget(
        string ownerInstanceId,
        ISceneNode target,
        IRandomNumberProvider provider
    )
    {
        if (!(target is Planet planet) || provider == null)
            return null;
        List<Officer> enemies = _game
            .GetSceneNodesByType<Officer>()
            .Where(o =>
                o.GetOwnerInstanceID() != ownerInstanceId
                && o.GetParentOfType<Planet>() == planet
                && !o.IsCaptured
                && !o.IsKilled
            )
            .ToList();
        return enemies.Count > 0 ? enemies.RandomElement(provider).InstanceID : null;
    }

    private string SelectJediTeacher(string ownerInstanceId, ISceneNode target)
    {
        if (!(target is Planet planet))
            return null;
        Officer teacher = _game
            .GetSceneNodesByType<Officer>()
            .FirstOrDefault(o =>
                o.GetOwnerInstanceID() == ownerInstanceId
                && o.IsJediTeacher
                && o.IsForceEligible
                && o.GetParentOfType<Planet>() == planet
                && !o.IsCaptured
                && !o.IsOnMission()
            );
        return teacher?.InstanceID;
    }

    private string SelectRescueTarget(
        string ownerInstanceId,
        ISceneNode target,
        IRandomNumberProvider provider
    )
    {
        if (!(target is Planet planet) || provider == null)
            return null;
        List<Officer> captured = _game
            .GetSceneNodesByType<Officer>()
            .Where(o =>
                o.GetOwnerInstanceID() == ownerInstanceId
                && o.IsCaptured
                && o.GetParentOfType<Planet>() == planet
            )
            .ToList();
        return captured.Count > 0 ? captured.RandomElement(provider).InstanceID : null;
    }
}
