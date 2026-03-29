using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class RecruitmentMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public RecruitmentMission()
        : base()
    // @TODO: Move the success probability variables to configs.
    {
        Name = "Recruitment";
        ParticipantSkill = MissionParticipantSkill.Leadership;
        QuadraticCoefficient = 0.005558;
        LinearCoefficient = 0.7656;
        ConstantTerm = 20.15;
        MinSuccessProbability = 1;
        MaxSuccessProbability = 100;
        MinTicks = 15;
        MaxTicks = 20;
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
            successProbabilityTable,
            quadraticCoefficient: -0.001748,
            linearCoefficient: 0.8657,
            constantTerm: 11.923,
            minSuccessProbability: 1,
            maxSuccessProbability: 100,
            minTicks: 15,
            maxTicks: 20
        )
    {
        TargetOfficerInstanceID = targetOfficerInstanceId;
    }

    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);

        if (target == null || target.OwnerInstanceID == OwnerInstanceID)
            return new List<GameResult>();

        Planet planet = GetParent() as Planet;

        target.OwnerInstanceID = OwnerInstanceID;
        game.RemoveUnrecruitedOfficer(target);
        game.AttachNode(target, planet);

        return new List<GameResult>
        {
            new CharacterMovedResult
            {
                CharacterInstanceID = target.InstanceID,
                FromLocationInstanceID = "UNRECRUITED_POOL",
                ToLocationInstanceID = planet?.InstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    protected override double GetFoilProbability(double defenseScore) => 0;

    public override bool CanContinue(GameRoot game)
    {
        return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
    }
}
