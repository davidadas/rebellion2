using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Always returns the minimum value — use when tests need every action to succeed.
/// Replaces AlwaysSucceedRNG in MissionSystemTests and DiplomacyMissionTests.
/// </summary>
public class StubRNG : IRandomNumberProvider
{
    public double NextDouble() => 0.01;

    public int NextInt(int min, int max) => min;
}

/// <summary>
/// Always returns the same double value.
/// Replaces MockRNG(double value) in JediSystemTests.
/// </summary>
public class FixedRNG : IRandomNumberProvider
{
    private readonly double value;

    public FixedRNG(double value = 0.5)
    {
        this.value = value;
    }

    public double NextDouble() => value;

    public int NextInt(int min, int max) => min;
}

/// <summary>
/// Returns a fixed sequence of doubles, then falls back to 0.5.
/// Replaces MockRNG in CombatSystemTests and UprisingSystemTests.
/// </summary>
public class QueueRNG : IRandomNumberProvider
{
    private Queue<double> values;

    public QueueRNG(params double[] values)
    {
        this.values = new Queue<double>(values);
    }

    public double NextDouble() => values.Count > 0 ? values.Dequeue() : 0.5;

    public int NextInt(int min, int max) => (int)(NextDouble() * (max - min)) + min;
}

/// <summary>
/// Minimal no-op Mission for use in tests.
/// Default constructor is for tests that only need to parent an officer to a mission.
/// Parameterized constructor is for tests that attach the mission to the scene graph.
/// Replaces TestMission (OfficerTests, FogOfWarSystemTests) and InstantMission (MissionSystemTests).
/// </summary>
public class StubMission : Mission
{
    /// <summary>
    /// Default constructor — sets empty participant lists.
    /// Use when the mission only needs to exist as a parent node, not in the scene graph.
    /// </summary>
    public StubMission()
    {
        MainParticipants = new List<IMissionParticipant>();
        DecoyParticipants = new List<IMissionParticipant>();
    }

    /// <summary>
    /// Full constructor — use when the mission is attached to a planet in the scene graph.
    /// Runs for exactly 1 tick and always succeeds.
    /// </summary>
    public StubMission(string ownerInstanceId, string targetInstanceId)
        : base(
            "Stub",
            ownerInstanceId,
            targetInstanceId,
            new List<IMissionParticipant>(),
            new List<IMissionParticipant>(),
            MissionParticipantSkill.Diplomacy,
            new ProbabilityTable(new Dictionary<int, int> { { 0, 100 } }),
            minTicks: 1,
            maxTicks: 1
        ) { }

    protected override List<GameResult> OnSuccess(GameRoot game) => new List<GameResult>();

    public override bool CanContinue(GameRoot game) => false;
}

/// <summary>
/// Static factories for common game entities used in tests.
/// Each method returns an unattached entity — call game.AttachNode() as needed.
/// Replaces duplicated factory methods in FogOfWarSystemTests.
/// </summary>
public static class MissionSceneBuilder
{
    public static (
        GameRoot game,
        Planet empPlanet,
        Planet enemyPlanet,
        Officer officer,
        FogOfWarSystem fog
    ) Build()
    {
        GameRoot game = new GameRoot(new GameConfig());

        Faction empire = new Faction { InstanceID = "empire" };
        Faction rebels = new Faction { InstanceID = "rebels" };
        game.Factions.Add(empire);
        game.Factions.Add(rebels);

        PlanetSystem system = new PlanetSystem
        {
            InstanceID = "sys1",
            PositionX = 0,
            PositionY = 0,
        };
        game.AttachNode(system, game.Galaxy);

        Planet empPlanet = new Planet
        {
            InstanceID = "emp_planet",
            OwnerInstanceID = "empire",
            IsColonized = true,
            PositionX = 0,
            PositionY = 0,
            PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
        };
        game.AttachNode(empPlanet, system);

        Planet enemyPlanet = new Planet
        {
            InstanceID = "enemy_planet",
            OwnerInstanceID = "rebels",
            IsColonized = true,
            PositionX = 100,
            PositionY = 0,
            GroundSlots = 5,
            OrbitSlots = 5,
            PopularSupport = new Dictionary<string, int> { { "rebels", 60 } },
        };
        game.AttachNode(enemyPlanet, system);

        Officer officer = EntityFactory.CreateOfficer("o1", "empire");
        game.AttachNode(officer, empPlanet);

        FogOfWarSystem fog = new FogOfWarSystem(game);
        return (game, empPlanet, enemyPlanet, officer, fog);
    }

    public static void RunToSuccess(Mission mission, GameRoot game)
    {
        while (!mission.IsComplete())
            mission.IncrementProgress();
        mission.Execute(game, new FixedRNG(0.0));
    }
}

public static class EntityFactory
{
    public static Officer CreateOfficer(string id, string factionId)
    {
        return new Officer
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
            Skills = new Dictionary<MissionParticipantSkill, int>
            {
                { MissionParticipantSkill.Diplomacy, 50 },
                { MissionParticipantSkill.Espionage, 50 },
                { MissionParticipantSkill.Combat, 50 },
                { MissionParticipantSkill.Leadership, 50 },
            },
        };
    }

    public static Fleet CreateFleet(string id, string factionId)
    {
        return new Fleet
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    public static Regiment CreateRegiment(string id, string factionId)
    {
        return new Regiment
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    public static Building CreateBuilding(string id, string factionId)
    {
        return new Building
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    public static Starfighter CreateStarfighter(string id, string factionId)
    {
        return new Starfighter
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    public static StubMission CreateMission(
        string id,
        string ownerInstanceId,
        string targetInstanceId
    )
    {
        return new StubMission(ownerInstanceId, targetInstanceId) { InstanceID = id };
    }
}
