using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class SubdueUprisingMission : Mission
{
    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public SubdueUprisingMission()
        : base()
    {
        ConfigKey = "SubdueUprising";
        DisplayName = "Subdue Uprising";
        ParticipantSkill = MissionParticipantSkill.Leadership;
    }

    /// <summary>
    /// Returns a new SubdueUprisingMission if the target is an own planet in uprising, or null.
    /// </summary>
    /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
    /// <returns>A configured mission, or null if the planet is not owned by this faction or not in uprising.</returns>
    public static SubdueUprisingMission TryCreate(MissionContext ctx)
    {
        if (!(ctx.Target is Planet planet))
            return null;

        if (!planet.IsInUprising || planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
            return null;

        return new SubdueUprisingMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            ctx.MainParticipants,
            ctx.DecoyParticipants
        );
    }

    private SubdueUprisingMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants
    )
        : base(
            "SubdueUprising",
            ownerInstanceId,
            RequirePlanetTarget(target, "Subdue Uprising").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            null
        )
    {
        DisplayName = "Subdue Uprising";
    }

    /// <summary>
    /// Extends base cancellation to also cancel when the uprising ends before execution.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the mission should be aborted.</returns>
    public override bool ShouldAbort(GameRoot game)
    {
        return base.ShouldAbort(game) || !(GetParent() is Planet p && p.IsInUprising);
    }

    /// <summary>
    /// Returns false if the uprising has ended on the target planet before execution.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the planet is still in uprising.</returns>
    protected override bool IsMissionSatisfied(GameRoot game)
    {
        return GetParent() is Planet p && p.IsInUprising;
    }

    /// <summary>
    /// Subdue Uprising missions are never foiled — they target own planets.
    /// </summary>
    /// <param name="defenseScore">Ignored.</param>
    /// <returns>Always 0.</returns>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Ends the uprising on the target planet.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider (unused for subdue uprising).</param>
    /// <returns>One PlanetUprisingEndedResult, or an empty list if the mission planet is null.</returns>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Planet planet = GetParent() as Planet;
        if (planet == null)
            return new List<GameResult>();
        planet.EndUprising();

        return new List<GameResult>
        {
            new PlanetUprisingEndedResult
            {
                Planet = planet,
                Faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Subdue Uprising missions do not repeat — one attempt per mission.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>Always false.</returns>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
