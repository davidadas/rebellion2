using System;
using Rebellion.Content;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

/// <summary>
/// Defines the independent ships and rules used by campaign-to-tactical tests.
/// </summary>
internal static class TacticalTestData
{
    internal const string StationTypeID = "test-battle-station";
    internal const int BattleLossLoyaltyDivisor = 10;

    /// <summary>
    /// Adds valid campaign rules required to start and settle a tactical encounter.
    /// </summary>
    /// <param name="config">The test-owned campaign configuration.</param>
    /// <returns>The supplied configuration with explicit test rules.</returns>
    internal static GameConfig ConfigureCampaignRules(GameConfig config)
    {
        config.Smuggling.LossPercentByMinimumSupport.TryAdd(0, 0);
        SpaceCombatSupportConfig support = new();
        SpaceCombatSupportSideConfig side = new()
        {
            LeadingFlat = 100,
            LeadingGapScale = 0,
            TrailingFlat = 100,
            TrailingGapScale = 0,
            MagnitudeCap = 20,
            SupportThreshold = 101,
        };
        foreach (
            SpaceCombatSupportContext context in Enum.GetValues(typeof(SpaceCombatSupportContext))
        )
            support.Profiles.Add(
                context,
                new SpaceCombatSupportProfile
                {
                    Divisor = 10,
                    FirstSide = side.CreateCopy(),
                    SecondSide = side.CreateCopy(),
                }
            );
        config.SupportShift.BattleSupport = support;
        return config;
    }

    /// <summary>
    /// Creates a catalog with small operational fleets for two sides.
    /// </summary>
    /// <param name="config">The rules used by the fixture's campaign.</param>
    /// <returns>The test catalog.</returns>
    internal static GameDataCatalog CreateCatalog(GameConfig config) =>
        TestGameData.Create(
            config,
            capitalShips: new[]
            {
                CreateCapital("ALCS008"),
                CreateCapital("CSEM006"),
                CreateCapital(StationTypeID),
            },
            starfighters: new[] { CreateFighter("SFAL02"), CreateFighter("SFEM01") },
            regiments: new[]
            {
                new Regiment { TypeID = "REAL001" },
                new Regiment { TypeID = "REEM001" },
            },
            tacticalCombat: CreateDefinition()
        );

    /// <summary>
    /// Creates a factory that binds test unit templates to their tactical rules.
    /// </summary>
    /// <param name="config">The rules used by the fixture's campaign.</param>
    /// <returns>The test unit factory.</returns>
    internal static UnitFactory CreateUnitFactory(GameConfig config)
    {
        GameDataCatalog data = CreateCatalog(config);
        return new UnitFactory(
            data.Buildings,
            data.CapitalShips,
            data.Starfighters,
            data.Regiments,
            data.SpecialForces
        );
    }

    /// <summary>
    /// Creates one carrier or station with explicit durability, movement and weapon budgets.
    /// </summary>
    /// <param name="typeID">The test template identifier.</param>
    /// <returns>The defined capital ship.</returns>
    internal static CapitalShip CreateCapital(string typeID)
    {
        CapitalShip ship = new()
        {
            TypeID = typeID,
            MaxHullStrength = 200,
            CurrentHullStrength = 200,
            Hyperdrive = 1,
            SublightSpeed = 8,
            WeaponRecharge = 8,
            StarfighterCapacity = 2,
            RegimentCapacity = 2,
            ManufacturingStatus = ManufacturingStatus.Complete,
            CanDestroyPlanets = typeID == StationTypeID,
        };
        ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 20, 20, 20, 20, 100 };
        return ship;
    }

    /// <summary>
    /// Creates an operational squadron that can attach to or deploy from a carrier.
    /// </summary>
    /// <param name="typeID">The test template identifier.</param>
    /// <returns>The defined fighter squadron.</returns>
    internal static Starfighter CreateFighter(string typeID = "SFAL02") =>
        new()
        {
            TypeID = typeID,
            MaxSquadronSize = 6,
            CurrentSquadronSize = 6,
            Hyperdrive = typeID == "SFAL02" ? 1 : 0,
            SublightSpeed = 12,
            Agility = 8,
            LaserCannon = 10,
            LaserRange = 100,
            ManufacturingStatus = ManufacturingStatus.Complete,
        };

    /// <summary>
    /// Defines the tactical rules and deployment directions used by the fixture.
    /// </summary>
    /// <returns>Fresh tactical data with no external asset addresses.</returns>
    internal static TacticalCombatDefinition CreateDefinition() =>
        new()
        {
            Factions = new()
            {
                new()
                {
                    FactionInstanceID = "FNALL1",
                    FactionIndex = 0,
                    ForwardZ = 1,
                },
                new()
                {
                    FactionInstanceID = "FNEMP1",
                    FactionIndex = 1,
                    ForwardZ = -1,
                },
            },
            Units = new()
            {
                new() { UnitTypeID = "ALCS008", BackupHyperdrive = 2 },
                new() { UnitTypeID = "CSEM006", BackupHyperdrive = 2 },
                new() { UnitTypeID = StationTypeID, PresentationIndex = 21 },
                new() { UnitTypeID = "SFAL02", FighterGroup = 0 },
                new() { UnitTypeID = "SFEM01", FighterGroup = 0 },
                new() { UnitTypeID = "SFEM02", FighterGroup = 2 },
            },
            Notifications = new()
            {
                new()
                {
                    FactionInstanceID = "FNEMP1",
                    MessageID = 260,
                    Variants = new() { new() { Path = "test-personnel-loss" } },
                },
            },
            WithdrawalDistancePercent = 100,
            FastSystemRepairTicks = 2,
            NormalSystemRepairTicks = 30,
        };
}
