using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public class SabotageMission : Mission
{
    public override bool CanceledOnOwnershipChange => false;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public SabotageMission()
        : base()
    {
        ConfigKey = "Sabotage";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Combat;
    }

    /// <summary>
    /// Returns a new SabotageMission if the target is a planet, or null.
    /// </summary>
    /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
    /// <returns>A configured mission, or null if the target is not a planet.</returns>
    public static SabotageMission TryCreate(MissionContext ctx)
    {
        if (!(ctx.Target is Planet))
            return null;

        return new SabotageMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            ctx.MainParticipants,
            ctx.DecoyParticipants
        );
    }

    private SabotageMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants
    )
        : base(
            "Sabotage",
            ownerInstanceId,
            RequirePlanetTarget(target, "Sabotage").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            null
        ) { }

    /// <summary>
    /// Returns false if the target planet has no buildings remaining before execution.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the planet still has at least one building.</returns>
    protected override bool IsMissionSatisfied(GameRoot game)
    {
        return GetParent() is Planet p && p.GetAllBuildings().Count > 0;
    }

    /// <summary>
    /// Sabotage does not award mission skill improvements.
    /// </summary>
    protected override void ImproveMissionParticipantsSkill() { }

    /// <summary>
    /// Destroys the first building on the target planet.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider (unused for sabotage).</param>
    /// <returns>One GameObjectSabotagedResult.</returns>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Planet planet = GetParent() as Planet;
        List<Building> buildings = planet.GetAllBuildings();
        Building target = buildings[0];
        game.DetachNode(target);

        return new List<GameResult>
        {
            new GameObjectSabotagedResult
            {
                SabotagedObject = target,
                Saboteur = MainParticipants.Count > 0 ? MainParticipants[0] : null,
                Context = planet,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Sabotage missions do not repeat — one attempt per mission.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>Always false.</returns>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
