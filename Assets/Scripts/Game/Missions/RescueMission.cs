using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class RescueMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    public override bool CanceledOnOwnershipChange => false;

    public RescueMission()
        : base()
    {
        Name = "Rescue";
        DisplayName = Name;
        ParticipantSkill = MissionParticipantSkill.Combat;
        BaseTicks = 1;
        SpreadTicks = 6;
    }

    public RescueMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Rescue",
            ownerInstanceId,
            RequirePlanetTarget(target, "Rescue").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            successProbabilityTable,
            baseTicks: 1,
            spreadTicks: 6
        )
    {
        if (string.IsNullOrEmpty(targetOfficerInstanceId))
            throw new ArgumentNullException(nameof(targetOfficerInstanceId));

        TargetOfficerInstanceID = targetOfficerInstanceId;
    }

    /// <summary>
    /// Returns false if the target officer is no longer captured or has moved
    /// away from the mission's planet before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        Officer captive = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        return captive != null
            && captive.IsCaptured
            && captive.GetParentOfType<Planet>() == GetParent() as Planet;
    }

    /// <summary>
    /// Clears the captured state and captor from the rescued officer.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        if (target == null)
            return new List<GameResult>();
        target.IsCaptured = false;
        target.CaptorInstanceID = null;

        return new List<GameResult>
        {
            new OfficerRescuedResult
            {
                OfficerInstanceID = target.InstanceID,
                RescuingFactionInstanceID = OwnerInstanceID,
                LocationInstanceID = (GetParent() as Planet)?.InstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        SuccessProbabilityTable = new ProbabilityTable(tables.Rescue);
        BaseTicks = tables.TickRanges.Rescue.Base;
        SpreadTicks = tables.TickRanges.Rescue.Spread;
    }

    /// <summary>
    /// Rescue missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
