using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

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

    /// <summary>
    /// Returns a new AssassinationMission targeting a random living enemy officer, or null.
    /// </summary>
    public static AssassinationMission TryCreate(MissionContext ctx)
    {
        if (!(ctx.Target is Planet planet) || ctx.RNG == null)
            return null;

        string targetId = SelectTarget(ctx.Game, ctx.OwnerInstanceId, planet, ctx.RNG);
        if (targetId == null)
            return null;

        return new AssassinationMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            ctx.MainParticipants,
            ctx.DecoyParticipants,
            targetId
        );
    }

    private static string SelectTarget(
        GameRoot game,
        string ownerInstanceId,
        Planet planet,
        IRandomNumberProvider provider
    )
    {
        List<Officer> enemies = game.GetSceneNodesByType<Officer>()
            .Where(o =>
                o.GetOwnerInstanceID() != ownerInstanceId
                && o.GetParentOfType<Planet>() == planet
                && !o.IsCaptured
                && !o.IsKilled
            )
            .ToList();
        return enemies.Count > 0 ? enemies.RandomElement(provider).InstanceID : null;
    }

    private AssassinationMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId
    )
        : base(
            "Assassination",
            ownerInstanceId,
            RequirePlanetTarget(target, "Assassination").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            null
        )
    {
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
