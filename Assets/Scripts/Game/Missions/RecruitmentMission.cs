using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class RecruitmentMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    public RecruitmentMission()
        : base()
    {
        ConfigKey = "Recruitment";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Leadership;
    }

    public RecruitmentMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Recruitment",
            ownerInstanceId,
            target.GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            successProbabilityTable
        )
    {
        if (string.IsNullOrEmpty(targetOfficerInstanceId))
            throw new ArgumentNullException(nameof(targetOfficerInstanceId));

        TargetOfficerInstanceID = targetOfficerInstanceId;
    }

    /// <summary>
    /// Returns false if the target officer no longer exists or has already joined this faction.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        return target != null && target.OwnerInstanceID != OwnerInstanceID;
    }

    /// <summary>
    /// Transfers the target officer to this faction and moves them to the mission planet.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        Planet planet = GetParent() as Planet;
        if (target == null || planet == null)
            return new List<GameResult>();

        target.OwnerInstanceID = OwnerInstanceID;
        game.RemoveUnrecruitedOfficer(target);
        game.AttachNode(target, planet);

        return new List<GameResult>
        {
            new CharacterMovedResult
            {
                OfficerInstanceID = target.InstanceID,
                FromLocationInstanceID = "UNRECRUITED_POOL",
                ToLocationInstanceID = planet?.InstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Recruitment missions are never foiled — they target unaffiliated officers, not enemy planets.
    /// </summary>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Returns true while there are still unrecruited officers available for this faction.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
    }
}
