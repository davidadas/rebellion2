using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Extensions;

/// <summary>
/// Factory class for creating missions based on type and parameters.
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
    /// </summary>
    /// <param name="missionType">The type of mission to check.</param>
    /// <param name="ownerInstanceId">The faction that would own the mission.</param>
    /// <param name="target">The target scene node (usually a planet).</param>
    /// <param name="provider">RNG provider for missions that require random target selection.</param>
    /// <returns>True if the mission can be created with these parameters.</returns>
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
                && SelectJediTrainer(ownerInstanceId, target) != null,

            _ => false,
        };
    }

    /// <summary>
    /// Creates a mission based on the specified type and parameters.
    /// </summary>
    /// <param name="missionType">The type of mission to create.</param>
    /// <param name="ownerInstanceId">The faction that owns the mission.</param>
    /// <param name="mainParticipants">Officers assigned to the mission.</param>
    /// <param name="decoyParticipants">Decoy officers for the mission.</param>
    /// <param name="target">The target scene node (usually a planet).</param>
    /// <param name="provider">RNG provider for missions that require random target selection.</param>
    /// <returns>A configured Mission instance ready to be attached to the scene graph.</returns>
    public Mission CreateMission(
        MissionType missionType,
        string ownerInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target,
        IRandomNumberProvider provider = null
    )
    {
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
                SelectJediTrainer(ownerInstanceId, target)
            ),
            _ => throw new ArgumentException($"Unhandled mission type: {missionType}"),
        };

        if (missionTables != null)
            mission.Configure(missionTables);

        return mission;
    }

    /// <summary>
    /// Picks a random unrecruited officer for the given faction, or null if none available.
    /// </summary>
    /// <param name="ownerInstanceId">The faction recruiting.</param>
    /// <param name="provider">RNG provider for random selection.</param>
    /// <returns>The InstanceID of the selected officer, or null.</returns>
    private string SelectRecruitmentTarget(string ownerInstanceId, IRandomNumberProvider provider)
    {
        List<Officer> unrecruited = _game.GetUnrecruitedOfficers(ownerInstanceId);
        if (unrecruited.Count == 0 || provider == null)
            return null;
        return unrecruited.RandomElement(provider).InstanceID;
    }

    /// <summary>
    /// Picks a random non-captured enemy officer on the target planet, or null if none available.
    /// </summary>
    /// <param name="ownerInstanceId">The faction performing the abduction.</param>
    /// <param name="target">The target scene node (must be a planet).</param>
    /// <param name="provider">RNG provider for random selection.</param>
    /// <returns>The InstanceID of the selected enemy officer, or null.</returns>
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

    /// <summary>
    /// Picks a random living, non-captured enemy officer on the target planet, or null if none available.
    /// </summary>
    /// <param name="ownerInstanceId">The faction performing the assassination.</param>
    /// <param name="target">The target scene node (must be a planet).</param>
    /// <param name="provider">RNG provider for random selection.</param>
    /// <returns>The InstanceID of the selected enemy officer, or null.</returns>
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

    /// <summary>
    /// Finds an available friendly Jedi trainer on the target planet, or null if none available.
    /// </summary>
    /// <param name="ownerInstanceId">The faction that owns the trainer.</param>
    /// <param name="target">The target scene node (must be a planet).</param>
    /// <returns>The InstanceID of the selected trainer, or null.</returns>
    private string SelectJediTrainer(string ownerInstanceId, ISceneNode target)
    {
        if (!(target is Planet planet))
            return null;
        Officer trainer = _game
            .GetSceneNodesByType<Officer>()
            .FirstOrDefault(o =>
                o.GetOwnerInstanceID() == ownerInstanceId
                && o.IsJediTrainer
                && o.IsForceEligible
                && o.GetParentOfType<Planet>() == planet
                && !o.IsCaptured
                && !o.IsOnMission()
            );
        return trainer?.InstanceID;
    }

    /// <summary>
    /// Picks a random captured friendly officer on the target planet, or null if none available.
    /// </summary>
    /// <param name="ownerInstanceId">The faction performing the rescue.</param>
    /// <param name="target">The target scene node (must be a planet).</param>
    /// <param name="provider">RNG provider for random selection.</param>
    /// <returns>The InstanceID of the selected captured officer, or null.</returns>
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
