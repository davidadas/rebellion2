using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public class RecruitmentMission : Mission
{
    public string TargetOfficerInstanceID { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public RecruitmentMission()
        : base()
    {
        ConfigKey = "Recruitment";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Leadership;
    }

    /// <summary>
    /// Returns a new RecruitmentMission targeting a random unrecruited officer, or null.
    /// </summary>
    /// <param name="ctx">Mission context; must include a RandomProvider and a valid target.</param>
    /// <returns>A configured mission, or null if no unrecruited officers exist or provider is missing.</returns>
    public static RecruitmentMission TryCreate(MissionContext ctx)
    {
        if (ctx.RandomProvider == null)
            return null;

        List<Officer> unrecruited = ctx.Game.GetUnrecruitedOfficers(ctx.OwnerInstanceId);
        if (unrecruited.Count == 0)
            return null;

        string targetId = unrecruited.RandomElement(ctx.RandomProvider).InstanceID;

        return new RecruitmentMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            ctx.MainParticipants,
            ctx.DecoyParticipants,
            targetId
        );
    }

    private RecruitmentMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string targetOfficerInstanceId
    )
        : base(
            "Recruitment",
            ownerInstanceId,
            target.GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            null
        )
    {
        if (mainParticipants.OfType<Officer>().Any(o => !o.IsMain))
            throw new ArgumentException(
                "Only main characters may lead a recruitment mission.",
                nameof(mainParticipants)
            );

        TargetOfficerInstanceID = targetOfficerInstanceId;
    }

    /// <summary>
    /// Returns false if the target officer no longer exists in the unrecruited pool or has
    /// already joined this faction.
    /// </summary>
    protected override bool IsMissionSatisfied(GameRoot game)
    {
        Officer target = game.UnrecruitedOfficers.FirstOrDefault(o =>
            o.InstanceID == TargetOfficerInstanceID
        );
        return target != null && target.OwnerInstanceID != OwnerInstanceID;
    }

    /// <summary>
    /// Recruitment missions are never foiled — they target unaffiliated officers, not enemy planets.
    /// </summary>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Transfers the target officer to this faction and moves them to the mission planet.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Officer target = game.UnrecruitedOfficers.FirstOrDefault(o =>
            o.InstanceID == TargetOfficerInstanceID
        );
        Planet planet = GetParent() as Planet;
        if (target == null || planet == null)
            return new List<GameResult>();

        Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
        target.OwnerInstanceID = OwnerInstanceID;
        game.RemoveUnrecruitedOfficer(target);
        game.AttachNode(target, planet);

        GameLogger.Log($"Recruited {target.GetDisplayName()} to {OwnerInstanceID}");

        return new List<GameResult>
        {
            new OfficerRecruitedResult
            {
                Officer = target,
                Faction = faction,
                Planet = planet,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Returns true while there are still unrecruited officers available for this faction.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
    }
}
