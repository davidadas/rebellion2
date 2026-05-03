using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Research mission that awards side research capacity for one discipline.
/// Three subtypes (Ship Design, Troop Training, Facility Design) share this class,
/// differing only in which <see cref="ManufacturingType"/> they target.
/// </summary>
public class ResearchMission : Mission
{
    public ManufacturingType ResearchType { get; set; }

    /// <summary>
    /// Total research points earned across all participants in the most recent execution.
    /// </summary>
    public int EarnedResearchPoints { get; set; }

    /// <summary>
    /// Net order advance applied to the faction's discipline in the most recent execution.
    /// </summary>
    public int OrdersAdvanced { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public ResearchMission()
        : base()
    {
        ConfigKey = "Research";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Leadership;
    }

    private ResearchMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ManufacturingType researchType
    )
        : base(
            "Research",
            ownerInstanceId,
            RequirePlanetTarget(target, "Research").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            null
        )
    {
        ResearchType = researchType;
        DisplayName = GetMissionName(researchType);
    }

    /// <summary>
    /// Returns a new ResearchMission if the target is an own planet, or null.
    /// </summary>
    /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
    /// <param name="researchType">The manufacturing category this mission advances.</param>
    /// <returns>A configured mission, or null if the planet is not owned by this faction.</returns>
    public static ResearchMission TryCreate(MissionContext ctx, ManufacturingType researchType)
    {
        if (!(ctx.Target is Planet planet))
            return null;

        if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
            return null;

        List<IMissionParticipant> actingParticipants = new List<IMissionParticipant>();
        if (ctx.MainParticipants?.Count > 0)
            actingParticipants.Add(ctx.MainParticipants[0]);

        return new ResearchMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            actingParticipants,
            ctx.DecoyParticipants,
            researchType
        );
    }

    private static string GetMissionName(ManufacturingType type)
    {
        return type switch
        {
            ManufacturingType.Ship => "Ship Design",
            ManufacturingType.Troop => "Troop Training",
            ManufacturingType.Building => "Facility Design",
            _ => "Research",
        };
    }

    /// <summary>
    /// Checks whether the mission target planet is still owned by the mission's faction.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>True if the parent planet is owned by this faction.</returns>
    protected override bool IsMissionSatisfied(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }

    /// <summary>
    /// Research missions target own planets and are never foiled.
    /// </summary>
    /// <param name="defenseScore">The defense score (unused).</param>
    /// <returns>Always 0.</returns>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Resolves one mission execution: each main participant rolls independently;
    /// each success accumulates a reward and bumps that officer's research rating.
    /// The total is then applied to the faction and any transitions are emitted.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for chance rolls and reward rolls.</param>
    /// <returns>Transition results, with a MissionCompletedResult appended.</returns>
    public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
    {
        EarnedResearchPoints = 0;
        OrdersAdvanced = 0;

        List<GameResult> results = new List<GameResult>();
        MissionOutcome outcome = MissionOutcome.Failed;
        Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);

        if (faction != null && IsMissionSatisfied(game))
        {
            AccumulatePointsFromParticipants(game.Config.Research, provider);
            if (EarnedResearchPoints > 0)
            {
                outcome = MissionOutcome.Success;
                AwardAccumulatedPoints(faction, game, results);
            }
        }

        results.Add(BuildCompletedResult(outcome, game));
        return results;
    }

    /// <summary>
    /// Rolls each officer's success chance; on success, rolls a reward, adds it to
    /// <see cref="EarnedResearchPoints"/>, and bumps that officer's base research rating.
    /// </summary>
    /// <param name="config">Research configuration providing reward parameters.</param>
    /// <param name="provider">RNG provider for chance and reward rolls.</param>
    private void AccumulatePointsFromParticipants(
        GameConfig.ResearchConfig config,
        IRandomNumberProvider provider
    )
    {
        foreach (IMissionParticipant participant in MainParticipants)
        {
            if (!(participant is Officer officer) || !RollSuccess(officer, provider))
                continue;

            EarnedResearchPoints += RollReward(config, provider);
            officer.IncrementBaseResearchRating(ResearchType);
        }
    }

    /// <summary>
    /// Returns true when the officer's roll comes in strictly under their research chance.
    /// </summary>
    /// <param name="officer">The officer attempting the research.</param>
    /// <param name="provider">RNG provider for the chance roll.</param>
    /// <returns>True if the participant succeeded this attempt.</returns>
    private bool RollSuccess(Officer officer, IRandomNumberProvider provider)
    {
        int chance = officer.GetResearchSuccessChance(ResearchType);
        return provider.NextDouble() * 100 < chance;
    }

    /// <summary>
    /// Rolls one successful participant's reward.
    /// </summary>
    /// <param name="config">Research configuration providing reward parameters.</param>
    /// <param name="provider">RNG provider for the reward roll.</param>
    /// <returns>The number of research points awarded for this success.</returns>
    private static int RollReward(GameConfig.ResearchConfig config, IRandomNumberProvider provider)
    {
        return config.BaseResearchPoints + provider.NextInt(0, config.ResearchDiceRange + 1);
    }

    /// <summary>
    /// Applies <see cref="EarnedResearchPoints"/> to the faction and emits an
    /// ordered result if the order advanced, plus an exhausted result if the
    /// discipline now has no further advances.
    /// </summary>
    /// <param name="faction">The owning faction whose research state advances.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="results">Result list to append transition results to.</param>
    private void AwardAccumulatedPoints(Faction faction, GameRoot game, List<GameResult> results)
    {
        ResearchDiscipline discipline = Faction.ToResearchDiscipline(ResearchType);
        Technology unlocked = faction.ApplyResearchProgress(discipline, EarnedResearchPoints);
        if (unlocked == null)
            return;

        OrdersAdvanced = 1;
        results.Add(BuildOrderedResult(faction, discipline, unlocked, game));
        if (faction.IsResearchExhausted(discipline))
            results.Add(BuildExhaustedResult(faction, game));
    }

    /// <summary>
    /// Builds a <see cref="ResearchOrderedResult"/> capturing the just-advanced
    /// research order and the technology that became available.
    /// </summary>
    /// <param name="faction">The owning faction.</param>
    /// <param name="discipline">The discipline that advanced.</param>
    /// <param name="unlocked">The technology that just became available.</param>
    /// <param name="game">The current game state.</param>
    /// <returns>A populated ordered result.</returns>
    private ResearchOrderedResult BuildOrderedResult(
        Faction faction,
        ResearchDiscipline discipline,
        Technology unlocked,
        GameRoot game
    )
    {
        return new ResearchOrderedResult
        {
            Tick = game.CurrentTick,
            Faction = faction,
            FacilityType = ResearchType,
            ResearchOrder = faction.GetHighestUnlockedOrder(ResearchType),
            Capacity = faction.GetResearchCapacityRemaining(discipline),
            Technology = unlocked,
        };
    }

    /// <summary>
    /// Builds a <see cref="ResearchExhaustedResult"/> for a discipline that now
    /// has no further advances available.
    /// </summary>
    /// <param name="faction">The owning faction.</param>
    /// <param name="game">The current game state.</param>
    /// <returns>A populated exhausted result.</returns>
    private ResearchExhaustedResult BuildExhaustedResult(Faction faction, GameRoot game)
    {
        return new ResearchExhaustedResult
        {
            Tick = game.CurrentTick,
            Faction = faction,
            FacilityType = ResearchType,
            PreviousState = 0,
            NewState = 1,
        };
    }

    /// <summary>
    /// Research missions repeat as long as the planet remains owned by this faction.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>True if the mission should continue.</returns>
    public override bool CanContinue(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }
}
