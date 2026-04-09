using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Research mission that awards capacity points to a faction's research pool.
/// Three subtypes (Ship Design, Troop Training, Facility Design) share this class,
/// differing only in which <see cref="ManufacturingType"/> they target.
/// </summary>
public class ResearchMission : Mission
{
    public ManufacturingType ResearchType { get; set; }

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

    public ResearchMission(
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

        Planet planet = (Planet)target;
        if (planet.GetOwnerInstanceID() != ownerInstanceId)
            throw new InvalidOperationException(
                $"Research target planet '{planet.DisplayName}' is not owned by this faction."
            );
    }

    /// <summary>
    /// Returns the officer's research skill as a direct probability (0-100).
    /// Only Officers should be assigned to research missions; non-Officer participants
    /// return 0 and will always fail the success check.
    /// </summary>
    /// <param name="agent">The mission participant to evaluate.</param>
    /// <returns>The officer's research skill for this mission's type, or 0 if not an officer.</returns>
    protected override double GetAgentProbability(IMissionParticipant agent)
    {
        if (agent is Officer officer)
            return officer.GetResearchSkill(ResearchType);
        return 0;
    }

    /// <summary>
    /// Research missions target own planets and are never foiled.
    /// </summary>
    /// <param name="defenseScore">The defense score (unused).</param>
    /// <returns>Always 0.</returns>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Improves the officer's research skill for this mission's manufacturing type
    /// instead of the base ParticipantSkill (Leadership).
    /// </summary>
    protected override void ImproveMissionParticipantsSkill()
    {
        foreach (IMissionParticipant participant in MainParticipants)
        {
            if (participant is Officer officer)
                officer.IncrementResearchSkill(ResearchType);
        }
    }

    /// <summary>
    /// Awards research capacity points to the faction's research pool.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">The random number provider.</param>
    /// <returns>An empty list (no additional results).</returns>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
        if (faction == null)
            return new List<GameResult>();

        GameConfig.ResearchConfig config = game.Config.Research;
        int reward = config.BaseResearchPoints + provider.NextInt(0, config.ResearchDiceRange + 1);

        if (faction.ResearchCapacity.ContainsKey(ResearchType))
            faction.ResearchCapacity[ResearchType] += reward;

        return new List<GameResult>();
    }

    /// <summary>
    /// Checks whether the mission target planet is still owned by the mission's faction.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>True if the parent planet is owned by this faction.</returns>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
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

    /// <summary>
    /// Maps a manufacturing type to the display name for the research subtype.
    /// </summary>
    /// <param name="type">The manufacturing type.</param>
    /// <returns>The mission display name.</returns>
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
}
