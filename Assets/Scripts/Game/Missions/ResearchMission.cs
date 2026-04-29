using System;
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

    public int EarnedResearchPoints { get; set; }

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
        if (ctx.MainParticipants != null && ctx.MainParticipants.Count > 0)
            actingParticipants.Add(ctx.MainParticipants[0]);

        return new ResearchMission(
            ctx.OwnerInstanceId,
            ctx.Target,
            actingParticipants,
            ctx.DecoyParticipants,
            researchType
        );
    }

    /// <summary>
    /// Returns the display name for a research mission based on the manufacturing type.
    /// </summary>
    /// <param name="type">The manufacturing type to look up.</param>
    /// <returns>The human-readable mission name.</returns>
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
    /// Checks whether the mission target planet is still owned by the mission's faction.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>True if the parent planet is owned by this faction.</returns>
    protected override bool IsMissionSatisfied(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }

    /// <summary>
    /// Returns the participant's research success chance for this subtype.
    /// Only officers are expected here; unsupported participant types return 0.
    /// </summary>
    /// <param name="agent">The mission participant to evaluate.</param>
    /// <returns>The participant's research success chance, or 0 if unsupported.</returns>
    protected override double GetAgentProbability(IMissionParticipant agent)
    {
        if (agent is Officer officer)
        {
            int researchChance = officer.GetResearchSuccessChance(ResearchType);
            return researchChance == 0 ? -0.01 : researchChance;
        }

        return 0;
    }

    /// <summary>
    /// Research missions target own planets and are never foiled.
    /// </summary>
    /// <param name="defenseScore">The defense score (unused).</param>
    /// <returns>Always 0.</returns>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Research mission success uses a strict probability comparison.
    /// </summary>
    /// <param name="rolledProbability">The rolled value on the 0-100 probability scale.</param>
    /// <param name="successProbability">The success threshold on the 0-100 probability scale.</param>
    /// <returns>True if the roll is strictly below the threshold.</returns>
    protected override bool IsSuccessfulProbabilityRoll(
        double rolledProbability,
        double successProbability
    )
    {
        return rolledProbability < successProbability;
    }

    /// <summary>
    /// Mission-base skill growth remains disabled. Research-stat growth is handled
    /// explicitly inside the success path for the acting officer.
    /// </summary>
    protected override void ImproveMissionParticipantsSkill() { }

    /// <summary>
    /// Awards research points into the faction's side-level research discipline state
    /// and improves the acting officer's matching base research rating.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">The random number provider.</param>
    /// <returns>Research progression results produced by the awarded points.</returns>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        EarnedResearchPoints = 0;
        OrdersAdvanced = 0;

        Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
        if (faction == null)
            return new List<GameResult>();

        Officer researchOfficer =
            MainParticipants.Count > 0 ? MainParticipants[0] as Officer : null;

        GameConfig.ResearchConfig config = game.Config.Research;
        EarnedResearchPoints =
            config.BaseResearchPoints + provider.NextInt(0, config.ResearchDiceRange + 1);

        researchOfficer?.IncrementBaseResearchRating(ResearchType);

        ResearchDiscipline discipline = Faction.ToResearchDiscipline(ResearchType);
        int oldOrder = faction.GetHighestUnlockedOrder(ResearchType);
        List<GameResult> results = faction.ApplyResearchCapacityChange(
            discipline,
            EarnedResearchPoints
        );

        OrdersAdvanced = faction.GetHighestUnlockedOrder(ResearchType) - oldOrder;

        return results;
    }

    /// <summary>
    /// Returns research mission failure results.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">The random number provider.</param>
    /// <returns>Mission failure results.</returns>
    protected override List<GameResult> OnFailed(GameRoot game, IRandomNumberProvider provider)
    {
        EarnedResearchPoints = 0;
        OrdersAdvanced = 0;
        return new List<GameResult>();
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
