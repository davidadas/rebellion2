using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public class EspionageMission : Mission
{
    private readonly FogOfWarSystem fogOfWar;

    public override bool CanceledOnOwnershipChange => false;

    public EspionageMission()
        : base()
    {
        Name = "Espionage";
        DisplayName = Name;
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
            successProbabilityTable,
            baseTicks: 1,
            spreadTicks: 20
        )
    {
        Planet planet = (Planet)target;

        if (planet.GetOwnerInstanceID() == ownerInstanceId)
            throw new InvalidOperationException(
                $"Espionage target planet '{planet.DisplayName}' is an own planet."
            );

        this.fogOfWar = fogOfWar;
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
    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        Planet planet = GetParent() as Planet;

        if (fogOfWar != null)
        {
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (faction != null && system != null)
                fogOfWar.CaptureSnapshot(faction, planet, system, game.CurrentTick);
        }

        return new List<GameResult>();
    }

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        SuccessProbabilityTable = new ProbabilityTable(tables.Espionage);
        BaseTicks = tables.TickRanges.Espionage.Base;
        SpreadTicks = tables.TickRanges.Espionage.Spread;
    }

    /// <summary>
    /// Espionage missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
