using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

/// <summary>
/// Provides scene construction helpers for tests that exercise consumers rather than placement rules.
/// </summary>
public static class SceneTestExtensions
{
    /// <summary>
    /// Adds a child directly to a detached test or projection node without invoking placement validation.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="child">The child.</param>
    public static void AddTestChild(this ISceneNode parent, ISceneNode child)
    {
        switch (parent)
        {
            case Planet planet:
                planet.SetChildren(
                    AppendWhen<Fleet>(planet.GetChildren<Fleet>(includeDisabled: true), child),
                    AppendWhen<Officer>(planet.GetChildren<Officer>(includeDisabled: true), child),
                    AppendWhen<Regiment>(
                        planet.GetChildren<Regiment>(includeDisabled: true),
                        child
                    ),
                    AppendWhen<SpecialForces>(
                        planet.GetChildren<SpecialForces>(includeDisabled: true),
                        child
                    ),
                    AppendWhen<Starfighter>(
                        planet.GetChildren<Starfighter>(includeDisabled: true),
                        child
                    ),
                    AppendWhen<Mission>(planet.GetChildren<Mission>(includeDisabled: true), child),
                    AppendWhen<Building>(planet.GetChildren<Building>(includeDisabled: true), child)
                );
                return;
            case CapitalShip ship:
                ship.SetChildren(
                    AppendWhen<Officer>(ship.GetChildren<Officer>(includeDisabled: true), child),
                    AppendWhen<Regiment>(ship.GetChildren<Regiment>(includeDisabled: true), child),
                    AppendWhen<SpecialForces>(
                        ship.GetChildren<SpecialForces>(includeDisabled: true),
                        child
                    ),
                    AppendWhen<Starfighter>(
                        ship.GetChildren<Starfighter>(includeDisabled: true),
                        child
                    )
                );
                return;
            case Fleet fleet when child is CapitalShip capitalShip:
                fleet.SetCapitalShips(
                    fleet.GetChildren<CapitalShip>(includeDisabled: true).Append(capitalShip)
                );
                return;
            default:
                parent.AddChild(child);
                return;
        }
    }

    /// <summary>
    /// Appends a child when it matches the requested collection type.
    /// </summary>
    /// <param name="existing">The existing.</param>
    /// <param name="child">The child.</param>
    /// <typeparam name="T">The t type.</typeparam>
    /// <returns>The result of append when.</returns>
    private static IEnumerable<T> AppendWhen<T>(IEnumerable<T> existing, ISceneNode child)
        where T : class, ISceneNode
    {
        return child is T typedChild ? existing.Append(typedChild) : existing;
    }
}

/// <summary>
/// Always returns the minimum value — use when tests need every action to succeed.
/// Replaces AlwaysSucceedRNG in MissionSystemTests and DiplomacyMissionTests.
/// </summary>
public class StubRNG : IRandomNumberProvider
{
    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => 0.01;

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
    public int NextInt(int min, int max) => min;
}

/// <summary>
/// Always returns the same double value.
/// Replaces MockRNG(double value) in JediSystemTests.
/// </summary>
public class FixedRNG : IRandomNumberProvider
{
    private readonly double _value;

    /// <summary>
    /// Initializes a new instance of the FixedRNG class.
    /// </summary>
    /// <param name="value">The value.</param>
    public FixedRNG(double value = 0.5)
    {
        _value = value;
    }

    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => _value;

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
    public int NextInt(int min, int max) => min;
}

/// <summary>
/// Fails when code unexpectedly requests a random value.
/// </summary>
public sealed class ThrowingRNG : IRandomNumberProvider
{
    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => throw new InvalidOperationException("Unexpected random roll.");

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
    public int NextInt(int min, int max) =>
        throw new InvalidOperationException("Unexpected random roll.");
}

/// <summary>
/// Returns the highest value permitted by each random-number request.
/// </summary>
public sealed class MaximumRNG : IRandomNumberProvider
{
    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => 0.99;

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
    public int NextInt(int min, int max) => max > min ? max - 1 : min;
}

/// <summary>
/// Returns a fixed sequence of doubles, then falls back to 0.5.
/// Replaces MockRNG in SpaceCombatSystemTests and UprisingSystemTests.
/// </summary>
public class QueueRNG : IRandomNumberProvider
{
    private Queue<double> _values;

    /// <summary>
    /// Initializes a new instance of the QueueRNG class.
    /// </summary>
    /// <param name="values">The values.</param>
    public QueueRNG(params double[] values)
    {
        _values = new Queue<double>(values);
    }

    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.5;

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
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
    /// <summary>Creates an empty stub mission copy.</summary>
    /// <returns>An empty stub mission.</returns>
    protected override BaseSceneNode CreateNodeCopy() => new StubMission();

    /// <summary>
    /// Default constructor — sets empty participant lists.
    /// Use when the mission only needs to exist as a parent node, not in the scene graph.
    /// </summary>
    public StubMission() { }

    /// <summary>
    /// Full constructor — use when the mission is attached to a planet in the scene graph.
    /// Runs for exactly 1 tick and always succeeds.
    /// </summary>
    /// <param name="ownerInstanceId">The owner instance id.</param>
    /// <param name="locationInstanceId">The location instance id.</param>
    public StubMission(string ownerInstanceId, string locationInstanceId)
        : base(
            "Stub",
            ownerInstanceId,
            locationInstanceId,
            new List<IMissionParticipant>(),
            new List<IMissionParticipant>(),
            OfficerRating.Diplomacy
        ) { }

    /// <summary>
    /// Executes on success.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="successfulParticipant">The successful participant.</param>
    /// <returns>The result of on success.</returns>
    protected override List<GameResult> OnSuccess(
        GameRoot game,
        IRandomNumberProvider provider,
        IMissionParticipant successfulParticipant
    ) => new List<GameResult>();

    /// <summary>
    /// Checks whether the repeat after completion condition is met.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <returns>True when the repeat after completion condition is met; otherwise false.</returns>
    public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;
}

/// <summary>
/// Cycles through int values 0, 1, 2, ... wrapping at the range.
/// Useful for tests that need variety (e.g., different ship types per shipyard).
/// </summary>
public class CyclingRNG : IRandomNumberProvider
{
    private int _counter;

    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => 0.5;

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
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

    /// <summary>
    /// Initializes a new instance of the SequenceRNG class.
    /// </summary>
    /// <param name="intValues">The int values.</param>
    /// <param name="doubleValues">The double values.</param>
    public SequenceRNG(int[] intValues = null, double[] doubleValues = null)
    {
        _ints = new Queue<int>(intValues ?? new int[0]);
        _doubles = new Queue<double>(doubleValues ?? new double[0]);
    }

    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.0;

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
    public int NextInt(int min, int max) =>
        _ints.Count > 0 ? System.Math.Max(min, System.Math.Min(max - 1, _ints.Dequeue())) : min;
}

/// <summary>
/// Returns a repeating sequence of normalized values and derives integer rolls from that sequence.
/// </summary>
public class FixedRandomProvider : IRandomNumberProvider
{
    private readonly double[] _values;
    private int _index;

    /// <summary>
    /// Initializes a new instance of the FixedRandomProvider class.
    /// </summary>
    /// <param name="values">The values.</param>
    public FixedRandomProvider(double[] values)
    {
        _values = values;
    }

    /// <summary>
    /// Executes next double.
    /// </summary>
    /// <returns>The result of next double.</returns>
    public double NextDouble() => _values[_index++ % _values.Length];

    /// <summary>
    /// Executes next int.
    /// </summary>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>The result of next int.</returns>
    public int NextInt(int min, int max) => (int)(NextDouble() * (max - min)) + min;
}

public static class TestConfig
{
    private static string SchemaPath =>
        Path.Combine(TestContent.Pack.ContentRootPath, "Application", "Schemas", "game-config.xsd");

    /// <summary>
    /// Creates the requested operation.
    /// </summary>
    /// <returns>The created value.</returns>
    public static GameConfig Create()
    {
        return ContentPackLoader.LoadGameConfig(
            TestContent.Pack.ContentRootPath,
            TestContent.Pack.PackRootPath,
            TestContent.Pack.Definition.GameConfigPath
        );
    }

    /// <summary>
    /// Creates with schema.
    /// </summary>
    /// <returns>The created with schema.</returns>
    public static GameConfig CreateWithSchema()
    {
        return Create();
    }

    /// <summary>
    /// Deserializes with schema.
    /// </summary>
    /// <param name="xml">The xml.</param>
    public static void DeserializeWithSchema(string xml)
    {
        GameSerializerSettings settings = BuildSchemaSettings();
        GameSerializer serializer = new GameSerializer(typeof(GameConfig), settings);
        using StringReader reader = new StringReader(xml);
        serializer.Deserialize(reader);
    }

    /// <summary>
    /// Builds schema settings.
    /// </summary>
    /// <returns>The constructed schema settings.</returns>
    private static GameSerializerSettings BuildSchemaSettings()
    {
        XmlSchemaSet schemas = new XmlSchemaSet();
        schemas.Add(null, XmlReader.Create(new StringReader(File.ReadAllText(SchemaPath))));
        return new GameSerializerSettings { Schemas = schemas };
    }
}

/// <summary>
/// Creates synthetic game-data catalogs for engine tests.
/// </summary>
public static class TestGameData
{
    /// <summary>
    /// Creates a synthetic catalog around the supplied configuration and message definitions.
    /// </summary>
    /// <param name="config">The config.</param>
    /// <param name="messageDefinitions">The message definitions.</param>
    /// <returns>The created value.</returns>
    public static GameDataCatalog Create(
        GameConfig config = null,
        MessageDefinition[] messageDefinitions = null
    )
    {
        return new GameDataCatalog(
            config ?? new GameConfig(),
            new GameGenerationConfig(),
            Array.Empty<Faction>(),
            Array.Empty<PlanetSector>(),
            Array.Empty<Building>(),
            Array.Empty<CapitalShip>(),
            Array.Empty<Starfighter>(),
            Array.Empty<Regiment>(),
            Array.Empty<SpecialForces>(),
            Array.Empty<Officer>(),
            Array.Empty<GameEvent>(),
            messageDefinitions ?? Array.Empty<MessageDefinition>(),
            new EncyclopediaEntries(),
            new FactionThemes()
        );
    }
}

public static class MapPositionTestHelper
{
    /// <summary>
    /// Executes with map position.
    /// </summary>
    /// <param name="planet">The planet.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>The result of with map position.</returns>
    public static Planet WithMapPosition(this Planet planet, int x, int y)
    {
        planet.PositionX = x;
        planet.PositionY = y;
        return planet;
    }

    /// <summary>
    /// Executes with map position.
    /// </summary>
    /// <param name="planetSector">The planet sector.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>The result of with map position.</returns>
    public static PlanetSector WithMapPosition(this PlanetSector planetSector, int x, int y)
    {
        planetSector.PositionX = x;
        planetSector.PositionY = y;
        return planetSector;
    }
}

public static class MissionSceneBuilder
{
    /// <summary>
    /// Builds the requested operation.
    /// </summary>
    /// <param name="config">The config.</param>
    /// <returns>The constructed value.</returns>
    public static (
        GameRoot game,
        Planet empirePlanet,
        Planet enemyPlanet,
        Officer officer,
        FogOfWarSystem fog
    ) Build(GameConfig config = null)
    {
        GameRoot game = new GameRoot(config ?? TestConfig.Create());

        Faction empire = new Faction { InstanceID = "empire" };
        Faction rebels = new Faction { InstanceID = "rebels" };
        game.GetFactions().Add(empire);
        game.GetFactions().Add(rebels);

        PlanetSector planetSector = new PlanetSector
        {
            InstanceID = "sector1",
            PositionX = 0,
            PositionY = 0,
        };
        game.AttachNode(planetSector, game.Galaxy);

        Planet empirePlanet = new Planet
        {
            InstanceID = "emp_planet",
            OwnerInstanceID = "empire",
            IsColonized = true,
            PositionX = 0,
            PositionY = 0,
            PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
        };
        game.AttachNode(empirePlanet, planetSector);

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
        game.AttachNode(enemyPlanet, planetSector);

        Officer officer = EntityFactory.CreateOfficer("o1", "empire");
        game.AttachNode(officer, empirePlanet);
        officer.MissionReturnParentInstanceID = empirePlanet.InstanceID;
        officer.MissionReturnLocationInstanceID = empirePlanet.InstanceID;

        FogOfWarSystem fog = new FogOfWarSystem(game);
        return (game, empirePlanet, enemyPlanet, officer, fog);
    }

    /// <summary>
    /// Executes run to success.
    /// </summary>
    /// <param name="mission">The mission.</param>
    /// <param name="game">The game.</param>
    public static void RunToSuccess(Mission mission, GameRoot game)
    {
        while (!mission.IsComplete())
            mission.IncrementProgress();
        mission.ResolveObjective(game, new FixedRNG(0.0));
    }
}

/// <summary>
/// Builds complete system dependency graphs used by focused system tests.
/// </summary>
public static class TestSystems
{
    /// <summary>
    /// Creates a mission system with the uprising resolution path available.
    /// </summary>
    /// <param name="game">The game state used by every system in the graph.</param>
    /// <param name="provider">The random number provider used by missions and uprisings.</param>
    /// <param name="movement">The movement system used by mission and control behavior.</param>
    /// <returns>A mission system with all required dependencies.</returns>
    public static MissionSystem CreateMissionSystem(
        GameRoot game,
        IRandomNumberProvider provider,
        MovementSystem movement
    )
    {
        FogOfWarSystem fog = new FogOfWarSystem(game);
        FleetSystem fleet = new FleetSystem(game);
        ManufacturingSystem manufacturing = new ManufacturingSystem(game, fleet, movement);
        PlanetaryControlSystem control = new PlanetaryControlSystem(
            game,
            movement,
            manufacturing,
            fog
        );
        UprisingSystem uprising = new UprisingSystem(game, provider, control);
        return new MissionSystem(game, provider, movement, uprising);
    }
}

public static class MissionTestFactory
{
    /// <summary>
    /// Attempts create.
    /// </summary>
    /// <param name="missionTypeId">The mission type id.</param>
    /// <param name="game">The game.</param>
    /// <param name="ownerInstanceId">The owner instance id.</param>
    /// <param name="target">The target.</param>
    /// <param name="mainParticipants">The main participants.</param>
    /// <param name="decoyParticipants">The decoy participants.</param>
    /// <param name="selectedTarget">The selected target.</param>
    /// <param name="targetOfficer">The target officer.</param>
    /// <param name="discipline">The discipline.</param>
    /// <returns>The result of try create.</returns>
    public static Mission TryCreate(
        string missionTypeId,
        GameRoot game,
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants = null,
        ISceneNode selectedTarget = null,
        Officer targetOfficer = null,
        ResearchDiscipline? discipline = null
    )
    {
        MissionContext context = new MissionContext
        {
            Game = game,
            MissionTypeID = missionTypeId,
            OwnerInstanceId = ownerInstanceId,
            Location = target,
            MainParticipants = mainParticipants ?? new List<IMissionParticipant>(),
            DecoyParticipants = decoyParticipants ?? new List<IMissionParticipant>(),
            SelectedTarget = targetOfficer ?? selectedTarget,
            Discipline = discipline,
        };

        return missionTypeId switch
        {
            AbductionMission.MissionTypeID => AbductionMission.TryCreate(context),
            AssassinationMission.MissionTypeID => AssassinationMission.TryCreate(context),
            DiplomacyMission.MissionTypeID => DiplomacyMission.TryCreate(context),
            EspionageMission.MissionTypeID => EspionageMission.TryCreate(context),
            InciteUprisingMission.MissionTypeID => InciteUprisingMission.TryCreate(context),
            JediTrainingMission.MissionTypeID => JediTrainingMission.TryCreate(context),
            ReconnaissanceMission.MissionTypeID => ReconnaissanceMission.TryCreate(context),
            RecruitmentMission.MissionTypeID => RecruitmentMission.TryCreate(context),
            RescueMission.MissionTypeID => RescueMission.TryCreate(context),
            ResearchMission.MissionTypeID when discipline.HasValue => ResearchMission.TryCreate(
                context,
                discipline.Value
            ),
            SabotageMission.MissionTypeID => SabotageMission.TryCreate(context),
            SubdueUprisingMission.MissionTypeID => SubdueUprisingMission.TryCreate(context),
            _ => null,
        };
    }
}

/// <summary>
/// Static factories for common game entities used in tests.
/// Each method returns an unattached entity — call game.AttachNode() as needed.
/// </summary>
public static class EntityFactory
{
    /// <summary>
    /// Creates officer.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="factionId">The faction id.</param>
    /// <returns>The created officer.</returns>
    public static Officer CreateOfficer(string id, string factionId)
    {
        return new Officer
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
            Ratings = new Dictionary<OfficerRating, int>
            {
                { OfficerRating.Diplomacy, 50 },
                { OfficerRating.Espionage, 50 },
                { OfficerRating.Combat, 50 },
                { OfficerRating.Leadership, 50 },
            },
        };
    }

    /// <summary>
    /// Creates fleet.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="factionId">The faction id.</param>
    /// <returns>The created fleet.</returns>
    public static Fleet CreateFleet(string id, string factionId)
    {
        return new Fleet
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    /// <summary>
    /// Creates regiment.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="factionId">The faction id.</param>
    /// <returns>The created regiment.</returns>
    public static Regiment CreateRegiment(string id, string factionId)
    {
        return new Regiment
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    /// <summary>
    /// Creates building.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="factionId">The faction id.</param>
    /// <returns>The created building.</returns>
    public static Building CreateBuilding(string id, string factionId)
    {
        return new Building
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    /// <summary>
    /// Creates starfighter.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="factionId">The faction id.</param>
    /// <returns>The created starfighter.</returns>
    public static Starfighter CreateStarfighter(string id, string factionId)
    {
        return new Starfighter
        {
            InstanceID = id,
            DisplayName = id,
            OwnerInstanceID = factionId,
        };
    }

    /// <summary>
    /// Creates mission.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="ownerInstanceId">The owner instance id.</param>
    /// <param name="locationInstanceId">The location instance id.</param>
    /// <returns>The created mission.</returns>
    public static StubMission CreateMission(
        string id,
        string ownerInstanceId,
        string locationInstanceId
    )
    {
        return new StubMission(ownerInstanceId, locationInstanceId) { InstanceID = id };
    }
}

/// <summary>
/// Produces a fully-populated <see cref="GenerationContext"/> with empty arrays and
/// minimal config sections, so tests can override only the fields they care about
/// without tripping null-deref inside seeders.
/// </summary>
public static class GenerationContextFactory
{
    /// <summary>
    /// Creates default.
    /// </summary>
    /// <returns>The created default.</returns>
    public static GenerationContext CreateDefault()
    {
        return new GenerationContext
        {
            Sectors = Array.Empty<PlanetSector>(),
            Factions = Array.Empty<Faction>(),
            Buildings = Array.Empty<Building>(),
            CapitalShips = Array.Empty<CapitalShip>(),
            Starfighters = Array.Empty<Starfighter>(),
            Regiments = Array.Empty<Regiment>(),
            SpecialForces = Array.Empty<SpecialForces>(),
            Officers = Array.Empty<Officer>(),
            Events = Array.Empty<GameEvent>(),
            Classification = new GalaxyClassificationResult(),
            Summary = new GameSummary(),
            Config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>(),
                    FactionBuckets = new List<FactionBucketConfig>(),
                },
                UnitDeployment = new UnitDeploymentSection
                {
                    FixedGarrisons = new List<FixedGarrison>(),
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>(),
                },
                Balance = new BalanceSection
                {
                    SupportBoostPerUnit = 2,
                    MaxMilitaryPresenceBoost = 10,
                },
            },
            GameConfig = new GameConfig
            {
                Production = new GameConfig.ProductionConfig(),
                Planet = new GameConfig.PlanetConfig(),
            },
            Rng = new StubRNG(),
        };
    }
}
