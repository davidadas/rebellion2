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
    /// Resolves the mission: each participant rolls independently; successful rolls
    /// accumulate research points and bump the rolling officer's base rating, then the
    /// total is applied to the faction in one shot and any transition results are emitted.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for chance rolls and reward rolls.</param>
    /// <returns>Transition results produced this execution, with a MissionCompletedResult appended.</returns>
    public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
    {
        EarnedResearchPoints = 0;
        OrdersAdvanced = 0;

        List<GameResult> results = new List<GameResult>();
        Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
        MissionOutcome outcome = MissionOutcome.Failed;

        if (faction != null && IsMissionSatisfied(game))
        {
            GameConfig.ResearchConfig config = game.Config.Research;

            foreach (IMissionParticipant participant in MainParticipants)
            {
                if (!(participant is Officer officer))
                    continue;

                int chance = officer.GetResearchSuccessChance(ResearchType);
                double roll = provider.NextDouble() * 100;
                if (roll >= chance)
                    continue;

                int reward =
                    config.BaseResearchPoints + provider.NextInt(0, config.ResearchDiceRange + 1);
                EarnedResearchPoints += reward;
                officer.IncrementBaseResearchRating(ResearchType);
            }

            if (EarnedResearchPoints > 0)
            {
                outcome = MissionOutcome.Success;
                ResearchDiscipline discipline = Faction.ToResearchDiscipline(ResearchType);
                Technology unlocked = faction.ApplyResearchProgress(
                    discipline,
                    EarnedResearchPoints
                );
                if (unlocked != null)
                {
                    OrdersAdvanced = 1;
                    results.Add(
                        new ResearchOrderedResult
                        {
                            Tick = game.CurrentTick,
                            Faction = faction,
                            FacilityType = ResearchType,
                            ResearchOrder = faction.GetHighestUnlockedOrder(ResearchType),
                            Capacity = faction.GetResearchCapacityRemaining(discipline),
                            Technology = unlocked,
                        }
                    );
                    if (faction.IsResearchExhausted(discipline))
                    {
                        results.Add(
                            new ResearchExhaustedResult
                            {
                                Tick = game.CurrentTick,
                                Faction = faction,
                                FacilityType = ResearchType,
                                PreviousState = 0,
                                NewState = 1,
                            }
                        );
                    }
                }
            }
        }

        results.Add(BuildCompletedResult(outcome, game));
        return results;
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
    /// Research missions repeat as long as the planet remains owned by this faction.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>True if the mission should continue.</returns>
    public override bool CanContinue(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }
}
