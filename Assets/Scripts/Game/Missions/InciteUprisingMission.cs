using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public class InciteUprisingMission : Mission
{
    public override bool CanceledOnOwnershipChange => false;

    public InciteUprisingMission()
        : base()
    {
        Name = "Incite Uprising";
        DisplayName = Name;
        ParticipantSkill = MissionParticipantSkill.Espionage;
        BaseTicks = 2;
        SpreadTicks = 10;
    }

    public InciteUprisingMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Incite Uprising",
            ownerInstanceId,
            RequirePlanetTarget(target, "Incite Uprising").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Espionage,
            successProbabilityTable,
            baseTicks: 2,
            spreadTicks: 10
        )
    {
        Planet planet = (Planet)target;

        if (planet.GetOwnerInstanceID() == ownerInstanceId)
            throw new InvalidOperationException(
                $"Incite Uprising target planet '{planet.DisplayName}' is an own planet."
            );
        if (planet.IsInUprising)
            throw new InvalidOperationException(
                $"Incite Uprising target planet '{planet.DisplayName}' is already in uprising."
            );
    }

    /// <summary>
    /// Composite score: (espionage_skill - enemy_popular_support - enemy_regiment_strength).
    /// </summary>
    protected override double GetAgentProbability(IMissionParticipant agent)
    {
        if (!(GetParent() is Planet planet))
            throw new InvalidOperationException(
                "InciteUprisingMission must be attached to a Planet."
            );

        int espionageSkill = agent.GetMissionSkillValue(MissionParticipantSkill.Espionage);
        int enemySupport = planet.GetPopularSupport(planet.OwnerInstanceID);

        int regimentStrength = 0;
        foreach (ISceneNode child in planet.GetChildren())
        {
            if (child is Regiment regiment && regiment.OwnerInstanceID != OwnerInstanceID)
                regimentStrength += regiment.DefenseRating;
        }

        int score = espionageSkill - enemySupport - regimentStrength;
        return SuccessProbabilityTable.Lookup(score);
    }

    /// <summary>
    /// Extends base cancellation to also cancel if an uprising starts before the mission executes.
    /// </summary>
    public override bool IsCanceled(GameRoot game)
    {
        return base.IsCanceled(game) || (GetParent() is Planet p && p.IsInUprising);
    }

    /// <summary>
    /// Returns false if an uprising has already started on the target planet before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && !p.IsInUprising;
    }

    /// <summary>
    /// Starts an uprising on the target planet.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        Planet planet = GetParent() as Planet;
        planet.BeginUprising();

        return new List<GameResult>
        {
            new PlanetUprisingStartedResult
            {
                PlanetInstanceID = planet.InstanceID,
                InstigatorFactionInstanceID = OwnerInstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        SuccessProbabilityTable = new ProbabilityTable(tables.InciteUprising);
        BaseTicks = tables.TickRanges.InciteUprising.Base;
        SpreadTicks = tables.TickRanges.InciteUprising.Spread;
    }

    /// <summary>
    /// Incite Uprising missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
