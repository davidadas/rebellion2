using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public class RescueMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    public override bool CanceledOnOwnershipChange => false;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public RescueMission()
        : base()
    {
        ConfigKey = "Rescue";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Combat;
    }

    /// <summary>
    /// Returns a new RescueMission targeting a random captured friendly officer, or null.
    /// </summary>
    public static RescueMission TryCreate(MissionContext ctx)
    {
        if (!(ctx.Target is Planet planet) || ctx.RNG == null)
            return null;

        string targetId = SelectTarget(ctx.Game, ctx.OwnerInstanceId, planet, ctx.RNG);
        if (targetId == null)
            return null;

        return new RescueMission(
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
        List<Officer> captured = game.GetSceneNodesByType<Officer>()
            .Where(o =>
                o.GetOwnerInstanceID() == ownerInstanceId
                && o.IsCaptured
                && o.GetParentOfType<Planet>() == planet
            )
            .ToList();
        return captured.Count > 0 ? captured.RandomElement(provider).InstanceID : null;
    }

    private RescueMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId
    )
        : base(
            "Rescue",
            ownerInstanceId,
            RequirePlanetTarget(target, "Rescue").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            null
        )
    {
        TargetOfficerInstanceID = targetOfficerInstanceId;
    }

    /// <summary>
    /// Returns false if the target officer is no longer captured or has moved
    /// away from the mission's planet before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        Officer captive = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        return captive?.IsCaptured == true
            && captive.GetParentOfType<Planet>() == GetParent() as Planet;
    }

    /// <summary>
    /// Clears the captured state and captor from the rescued officer.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
        if (target == null)
            return new List<GameResult>();
        target.IsCaptured = false;
        target.CaptorInstanceID = null;

        return new List<GameResult>
        {
            new OfficerCaptureStateResult
            {
                TargetOfficer = target,
                IsCaptured = false,
                Context = GetParent() as Planet,
                Tick = game.CurrentTick,
            },
            new OfficerRescuedResult
            {
                Officer = target,
                RescuingFaction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                Location = GetParent() as Planet,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Rescue missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
