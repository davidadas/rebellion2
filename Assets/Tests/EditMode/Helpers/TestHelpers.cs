using System.Collections.Generic;
using System.IO;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

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
    private readonly double _value;

    public FixedRNG(double value = 0.5)
    {
        _value = value;
    }

    public double NextDouble() => _value;

    public int NextInt(int min, int max) => min;
}

/// <summary>
/// Returns a fixed sequence of doubles, then falls back to 0.5.
/// Replaces MockRNG in CombatSystemTests and UprisingSystemTests.
/// </summary>
public class QueueRNG : IRandomNumberProvider
{
    private Queue<double> _values;

    public QueueRNG(params double[] values)
    {
        _values = new Queue<double>(values);
    }

    public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.5;

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
            new ProbabilityTable(new Dictionary<int, int> { { 0, 100 } })
        )
    {
        BaseTicks = 1;
        SpreadTicks = 0;
    }

    protected override List<GameResult> OnSuccess(
        GameRoot game,
        IRandomNumberProvider provider
    ) => new List<GameResult>();

    public override bool CanContinue(GameRoot game) => false;
}

/// <summary>
/// Cycles through int values 0, 1, 2, ... wrapping at the range.
/// Useful for tests that need variety (e.g., different ship types per shipyard).
/// </summary>
public class CyclingRNG : IRandomNumberProvider
{
    private int _counter;

    public double NextDouble() => 0.5;

    public int NextInt(int min, int max)
    {
        int range = max - min;
        if (range <= 0)
            return min;
        int value = min + (_counter % range);
        _counter++;
        return value;
    }
}

/// <summary>
/// Returns queued int values for NextInt, queued double values for NextDouble.
/// Falls back to 0 when queues are exhausted.
/// </summary>
public class SequenceRNG : IRandomNumberProvider
{
    private readonly Queue<int> _ints;
    private readonly Queue<double> _doubles;

    public SequenceRNG(int[] intValues = null, double[] doubleValues = null)
    {
        _ints = new Queue<int>(intValues ?? new int[0]);
        _doubles = new Queue<double>(doubleValues ?? new double[0]);
    }

    public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.0;

    public int NextInt(int min, int max) =>
        _ints.Count > 0 ? System.Math.Max(min, System.Math.Min(max - 1, _ints.Dequeue())) : min;
}

public static class TestConfig
{
    private static readonly string ConfigPath = Path.Combine(
        UnityEngine.Application.dataPath,
        "Resources",
        "Configs",
        "GameConfig.xml"
    );

    public static GameConfig Create()
    {
        string xml = File.ReadAllText(ConfigPath);
        GameSerializer serializer = new GameSerializer(typeof(GameConfig));
        using StringReader reader = new StringReader(xml);
        GameConfig config = (GameConfig)serializer.Deserialize(reader);
        config.Validate();
        return config;
    }
}

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
        GameRoot game = new GameRoot(TestConfig.Create());

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
            EnergyCapacity = 5,
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

/// <summary>
/// Static factories for common game entities used in tests.
/// Each method returns an unattached entity — call game.AttachNode() as needed.
/// </summary>
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
