using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public class EspionageMission : Mission
{
    private readonly FogOfWarSystem _fogOfWar;

    public override bool CanceledOnOwnershipChange => false;

    public EspionageMission()
        : base()
    {
        ConfigKey = "Espionage";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Espionage;
    }

    public EspionageMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        FogOfWarSystem fogOfWar,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Espionage",
            ownerInstanceId,
            RequirePlanetTarget(target, "Espionage").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Espionage,
            successProbabilityTable
        )
    {
        Planet planet = (Planet)target;

        if (planet.GetOwnerInstanceID() == ownerInstanceId)
            throw new InvalidOperationException(
                $"Espionage target planet '{planet.DisplayName}' is an own planet."
            );

        _fogOfWar = fogOfWar;
    }

    /// <summary>
    /// Returns false if the target planet is no longer owned by an enemy faction at execution time.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() != OwnerInstanceID;
    }

    /// <summary>
    /// Captures a fog-of-war snapshot of the target planet for the owning faction.
    /// </summary>
    protected override List<GameResult> OnSuccess(
        GameRoot game,
        IRandomNumberProvider provider
    )
    {
        Planet planet = GetParent() as Planet;

        if (_fogOfWar != null)
        {
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (faction != null && system != null)
                _fogOfWar.CaptureSnapshot(faction, planet, system, game.CurrentTick);
        }

        return new List<GameResult>();
    }

    /// <summary>
    /// Espionage missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
