using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class AssassinationMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    public override bool CanceledOnOwnershipChange => false;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public AssassinationMission()
        : base()
    {
        ConfigKey = "Assassination";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Combat;
    }

    public AssassinationMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Assassination",
            ownerInstanceId,
            RequirePlanetTarget(target, "Assassination").GetInstanceID(),
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
    /// Returns false if the target officer has already been killed or has moved
    /// away from the mission's planet before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        return target?.IsKilled == false
            && target.GetParentOfType<Planet>() == GetParent() as Planet;
    }

    /// <summary>
    /// Marks the target officer as killed and removes them from the scene graph.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        if (target == null)
            return new List<GameResult>();
        target.IsKilled = true;
        game.DetachNode(target);

        return new List<GameResult>
        {
            new OfficerKilledResult
            {
                TargetOfficer = target,
                Assassin = MainParticipants.Count > 0 ? MainParticipants[0] : null,
                Context = GetParent() as Planet,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Assassination missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
