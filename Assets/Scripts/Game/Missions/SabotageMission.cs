using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class SabotageMission : Mission
{
    public override bool CanceledOnOwnershipChange => false;

    public SabotageMission()
        : base()
    {
        Name = "Sabotage";
        DisplayName = Name;
        ParticipantSkill = MissionParticipantSkill.Combat;
    }

    public SabotageMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Sabotage",
            ownerInstanceId,
            RequirePlanetTarget(target, "Sabotage").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            successProbabilityTable
        ) { }

    /// <summary>
    /// Returns false if the target planet has no buildings remaining before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.GetAllBuildings().Count > 0;
    }

    /// <summary>
    /// Destroys the first building on the target planet.
    /// </summary>
    protected override List<GameResult> OnSuccess(
        GameRoot game,
        IRandomNumberProvider provider
    )
    {
        Planet planet = GetParent() as Planet;
        List<Building> buildings = planet.GetAllBuildings();
        Building target = buildings[0];
        game.DetachNode(target);

        return new List<GameResult>
        {
            new BuildingSabotagedResult
            {
                PlanetInstanceID = planet.InstanceID,
                BuildingType = target.BuildingType.ToString(),
                FactionInstanceID = OwnerInstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        SuccessProbabilityTable = new ProbabilityTable(tables.Sabotage);
        BaseTicks = tables.TickRanges.Sabotage.Base;
        SpreadTicks = tables.TickRanges.Sabotage.Spread;
    }

    /// <summary>
    /// Sabotage missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
