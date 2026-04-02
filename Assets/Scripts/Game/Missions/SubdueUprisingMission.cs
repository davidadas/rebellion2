using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class SubdueUprisingMission : Mission
{
    public SubdueUprisingMission()
        : base()
    {
        Name = "Subdue Uprising";
        DisplayName = Name;
        ParticipantSkill = MissionParticipantSkill.Leadership;
        MinTicks = 10;
        MaxTicks = 15;
    }

    public SubdueUprisingMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Subdue Uprising",
            ownerInstanceId,
            RequirePlanetTarget(target, "Subdue Uprising").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            successProbabilityTable,
            minTicks: 10,
            maxTicks: 15
        )
    {
        Planet planet = (Planet)target;

        if (!planet.IsInUprising)
            throw new InvalidOperationException(
                $"Subdue Uprising target planet '{planet.DisplayName}' is not in uprising."
            );

        if (planet.GetOwnerInstanceID() != ownerInstanceId)
            throw new InvalidOperationException(
                $"Subdue Uprising target planet '{planet.DisplayName}' is owned by another faction."
            );
    }

    /// <summary>
    /// Extends base cancellation to also cancel when the uprising ends before execution.
    /// </summary>
    public override bool IsCanceled(GameRoot game)
    {
        return base.IsCanceled(game) || !(GetParent() is Planet p && p.IsInUprising);
    }

    /// <summary>
    /// Returns false if the uprising has ended on the target planet before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.IsInUprising;
    }

    /// <summary>
    /// Ends the uprising on the target planet.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        Planet planet = GetParent() as Planet;
        if (planet == null)
            return new List<GameResult>();
        planet.EndUprising();

        return new List<GameResult>
        {
            new PlanetUprisingEndedResult
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = OwnerInstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Subdue Uprising missions are never foiled — they target own planets.
    /// </summary>
    protected override double GetFoilProbability(double defenseScore) => 0;

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        SuccessProbabilityTable = new ProbabilityTable(tables.SubdueUprising);
        MinTicks = tables.TickRanges.SubdueUprising.Min;
        MaxTicks = tables.TickRanges.SubdueUprising.Max;
    }

    /// <summary>
    /// Subdue Uprising missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
