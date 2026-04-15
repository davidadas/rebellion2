using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class DiplomacyMission : Mission
{
    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public DiplomacyMission()
        : base()
    {
        ConfigKey = "Diplomacy";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Diplomacy;
    }

    /// <summary>
    /// Returns a new DiplomacyMission if the target is a valid planet, or null.
    /// </summary>
    /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
    /// <returns>A configured mission, or null if the planet is ineligible.</returns>
    public static DiplomacyMission TryCreate(MissionContext ctx)
    {
        if (!(ctx.Target is Planet planet))
            return null;

        if (
            !planet.IsColonized
            || planet.IsInUprising
            || !planet.WasVisitedBy(ctx.OwnerInstanceId)
            || planet.GetPopularSupport(ctx.OwnerInstanceId) >= 100
        )
            return null;

        string planetOwner = planet.GetOwnerInstanceID();
        if (planetOwner != null && planetOwner != ctx.OwnerInstanceId)
            return null;

        return new DiplomacyMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            ctx.MainParticipants,
            ctx.DecoyParticipants
        );
    }

    private DiplomacyMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants
    )
        : base(
            "Diplomacy",
            ownerInstanceId,
            RequirePlanetTarget(target, "Diplomacy").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Diplomacy,
            null
        ) { }

    /// <summary>
    /// Extends base cancellation to also cancel when the target planet enters uprising or
    /// is taken by a third faction.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the mission should be aborted.</returns>
    public override bool ShouldAbort(GameRoot game)
    {
        if (base.ShouldAbort(game))
            return true;
        if (GetParent() is Planet planet)
        {
            if (planet.IsInUprising)
                return true;
            string owner = planet.GetOwnerInstanceID();
            if (owner != null && owner != OwnerInstanceID)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Diplomacy missions are never foiled — they target own or neutral planets.
    /// </summary>
    /// <param name="defenseScore">Ignored.</param>
    /// <returns>Always 0.</returns>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Increments popular support on the target planet.
    /// Ownership transfer is handled by PlanetaryControlSystem when support crosses the threshold.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider (unused for diplomacy).</param>
    /// <returns>Always empty; diplomacy only adjusts support.</returns>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Planet planet = GetParent() as Planet;
        if (planet == null)
            return new List<GameResult>();

        int currentSupport = planet.GetPopularSupport(OwnerInstanceID);
        int increment = 1;

        if (SuccessProbabilityTable != null && MainParticipants.Count > 0)
        {
            Officer officer = MainParticipants[0] as Officer;
            if (officer != null)
            {
                int score =
                    (int)officer.CurrentRank
                    - currentSupport
                    + officer.Skills[MissionParticipantSkill.Diplomacy];
                int tableValue = SuccessProbabilityTable.Lookup(score);
                if (tableValue > 0)
                    increment = tableValue;
            }
        }

        int newSupport = Math.Min(100, currentSupport + increment);
        game.SetPlanetPopularSupport(planet, OwnerInstanceID, newSupport);

        return new List<GameResult>();
    }

    /// <summary>
    /// Returns true while the planet remains eligible for further diplomacy attempts.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the planet is owned or neutral, not in uprising, and support is below 100.</returns>
    public override bool CanContinue(GameRoot game)
    {
        if (GetParent() is Planet planet)
        {
            return (
                    planet.GetOwnerInstanceID() == GetOwnerInstanceID()
                    || planet.GetOwnerInstanceID() == null
                )
                && planet.GetPopularSupport(GetOwnerInstanceID()) < 100
                && !planet.IsInUprising;
        }
        return false;
    }
}
