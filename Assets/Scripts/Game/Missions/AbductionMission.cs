using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class AbductionMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    public override bool CanceledOnOwnershipChange => false;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public AbductionMission()
        : base()
    {
        ConfigKey = "Abduction";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Combat;
    }

    public AbductionMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Abduction",
            ownerInstanceId,
            RequirePlanetTarget(target, "Abduction").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            successProbabilityTable
        )
    {
        if (string.IsNullOrEmpty(targetOfficerInstanceId))
            throw new ArgumentNullException(nameof(targetOfficerInstanceId));

        TargetOfficerInstanceID = targetOfficerInstanceId;
    }

    /// <summary>
    /// Returns false if the target officer has already been captured or has moved
    /// away from the mission's planet before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        return target?.IsCaptured == false
            && target.GetParentOfType<Planet>() == GetParent() as Planet;
    }

    /// <summary>
    /// Marks the target officer as captured and assigns the captor faction.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        if (target == null)
            return new List<GameResult>();
        target.IsCaptured = true;
        target.CaptorInstanceID = OwnerInstanceID;

        return new List<GameResult>
        {
            new OfficerCaptureStateResult
            {
                TargetOfficer = target,
                IsCaptured = true,
                Context = GetParent() as Planet,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Abduction missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
