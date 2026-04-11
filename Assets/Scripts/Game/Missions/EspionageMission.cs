using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public class EspionageMission : Mission
{
    private readonly FogOfWarSystem _fogOfWar;

    public override bool CanceledOnOwnershipChange => false;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public EspionageMission()
        : base()
    {
        ConfigKey = "Espionage";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Espionage;
    }

    /// <summary>
    /// Returns a new EspionageMission if the target is a visited planet, or null.
    /// </summary>
    public static EspionageMission TryCreate(MissionContext ctx)
    {
        if (!(ctx.Target is Planet planet))
            return null;

        if (!planet.WasVisitedBy(ctx.OwnerInstanceId))
            return null;

        return new EspionageMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            ctx.MainParticipants,
            ctx.DecoyParticipants,
            ctx.FogOfWar
        );
    }

    private EspionageMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        FogOfWarSystem fogOfWar
    )
        : base(
            "Espionage",
            ownerInstanceId,
            RequirePlanetTarget(target, "Espionage").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Espionage,
            null
        )
    {
        _fogOfWar = fogOfWar;
    }

    protected override bool IsMissionSatisfied(GameRoot game)
    {
        return GetParent() is Planet;
    }

    /// <summary>
    /// Espionage does not award mission skill improvements.
    /// </summary>
    protected override void ImproveMissionParticipantsSkill() { }

    /// <summary>
    /// Captures a fog-of-war snapshot of the target planet for the owning faction.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Planet planet = GetParent() as Planet;

        if (_fogOfWar != null)
        {
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (faction != null && system != null)
                _fogOfWar.CaptureSnapshot(faction, planet, system, game.CurrentTick);
        }

        return new List<GameResult>();
    }

    /// <summary>
    /// Espionage missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
