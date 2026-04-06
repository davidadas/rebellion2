using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Research mission that awards capacity points to a faction's research pool.
/// Three subtypes (Ship Design, Troop Training, Facility Design) all share this class,
/// differing only in which ManufacturingType they target.
/// Success probability is the officer's research skill for the subtype (0–100).
/// On success: awards BaseResearchPoints + random(0, ResearchDiceRange) capacity points
/// and increments the officer's research skill by 1.
/// </summary>
public class ResearchMission : Mission
{
    public ManufacturingType ResearchType { get; set; }

    public ResearchMission()
        : base()
    {
        Name = "Research";
        DisplayName = Name;
        ParticipantSkill = MissionParticipantSkill.Leadership;
        BaseTicks = 10;
        SpreadTicks = 10;
    }

    public ResearchMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ManufacturingType researchType
    )
        : base(
            GetMissionName(researchType),
            ownerInstanceId,
            RequirePlanetTarget(target, "Research").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            null,
            baseTicks: 10,
            spreadTicks: 10
        )
    {
        ResearchType = researchType;

        Planet planet = (Planet)target;
        if (planet.GetOwnerInstanceID() != ownerInstanceId)
            throw new InvalidOperationException(
                $"Research target planet '{planet.DisplayName}' is not owned by this faction."
            );
    }

    /// <summary>
    /// Uses the officer's research skill for the subtype as a direct probability (0–100).
    /// Matches the original: no table lookup, skill IS the success chance.
    /// </summary>
    protected override double GetAgentProbability(IMissionParticipant agent)
    {
        if (agent is Officer officer)
            return officer.GetResearchSkill(ResearchType);
        return 0;
    }

    /// <summary>
    /// Research missions target own planets — never foiled.
    /// </summary>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Awards research capacity points to the faction and increments the officer's
    /// research skill. Reward = BaseResearchPoints + random(0, ResearchDiceRange).
    /// Original: 1 + random(0, 6) = 1–7 points.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
        if (faction == null)
            return new List<GameResult>();

        GameConfig.ResearchConfig config = game.Config.Research;
        Random rng = new Random(game.CurrentTick ^ InstanceID.GetHashCode());
        int reward = config.BaseResearchPoints + rng.Next(0, config.ResearchDiceRange + 1);

        if (faction.ResearchCapacity.ContainsKey(ResearchType))
            faction.ResearchCapacity[ResearchType] += reward;

        foreach (IMissionParticipant participant in MainParticipants)
        {
            if (participant is Officer officer)
                officer.IncrementResearchSkill(ResearchType);
        }

        return new List<GameResult>();
    }

    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p
            && p.GetOwnerInstanceID() == OwnerInstanceID;
    }

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        if (tables.TickRanges.Research != null)
        {
            BaseTicks = tables.TickRanges.Research.Base;
            SpreadTicks = tables.TickRanges.Research.Spread;
        }
    }

    /// <summary>
    /// Research missions repeat as long as the planet remains owned.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
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
}
