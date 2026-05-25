using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public static class HeadlessSimulationRunner
{
    public const string SimulationCodeVersion = "2026-04-27-ai-manufacturing-idle-v1";
    private const string _tickCountFlag = "-simTicks";
    private const string _outputPathFlag = "-simOut";
    private const string _playerFactionFlag = "-simPlayerFaction";
    private const string _seedFlag = "-simSeed";
    private const string _aiProfileFlag = "-aiProfile";
    private const string _aiProfileDefault = "default";
    private const string _aiProfileMigrationOn = "migration-on";
    private const string _logDirectory = "/tmp/rebellion2-sim-logs";
    private static readonly MethodInfo _processTickMethod = typeof(GameManager).GetMethod(
        "ProcessTick",
        BindingFlags.Instance | BindingFlags.NonPublic
    );
    private static readonly FieldInfo _pendingCombatField = typeof(GameManager).GetField(
        "_pendingCombatDecision",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    public static void RunDefaultSimulation()
    {
        try
        {
            SimulationOptions options = SimulationOptions.Parse(Environment.GetCommandLineArgs());
            RunSimulation(options);
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            UnityEditor.EditorApplication.Exit(1);
        }
    }

    public static SimulationRunResult RunPersistentSimulation(
        int tickCount,
        string outputPath,
        string playerFactionId,
        int? seed,
        string aiProfile = null
    )
    {
        return RunSimulation(
            new SimulationOptions
            {
                TickCount = tickCount,
                OutputPath = outputPath,
                PlayerFactionId = string.IsNullOrWhiteSpace(playerFactionId)
                    ? null
                    : playerFactionId,
                Seed = seed,
                AIProfile = aiProfile,
            }
        );
    }

    private static SimulationRunResult RunSimulation(SimulationOptions options)
    {
        EnsureSimulationInternals();
        string logPath = GetLogPath(options.OutputPath);
        GameLogger.Configure(logPath, enableFileLogging: true);
        LogToFile(logPath, $"[HeadlessSimVersion] {SimulationCodeVersion}");

        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingResearchLevel = 1,
            PlayerFactionID = options.PlayerFactionId,
            StartingFactionIDs = new[] { "FNALL1", "FNEMP1" },
        };

        string startMessage =
            $"[HeadlessSim] starting ticks={options.TickCount} seed={options.Seed?.ToString() ?? "random"} playerFaction={options.PlayerFactionId} galaxySize={summary.GalaxySize}";
        UnityEngine.Debug.Log(startMessage);
        LogToFile(logPath, startMessage);

        GameRoot game = CreateGameBuilder(summary, options.Seed).BuildGame();
        ValidateAIOptions(options);
        ApplyAIOptions(game, options);
        GameManager manager = new GameManager(game);
        ManufacturingIdleTracker idleTracker = new ManufacturingIdleTracker();
        ManufacturedUnitTracker manufacturedUnitTracker = new ManufacturedUnitTracker();
        FleetHistoryTracker fleetHistoryTracker = new FleetHistoryTracker();
        manufacturedUnitTracker.RecordInitialState(game);
        fleetHistoryTracker.RecordTick(game);

        for (int i = 0; i < options.TickCount; i++)
        {
            AutoResolvePendingCombat(manager);
            _processTickMethod.Invoke(manager, null);
            AutoResolvePendingCombat(manager);
            idleTracker.RecordTick(game);
            manufacturedUnitTracker.RecordTick(game);
            fleetHistoryTracker.RecordTick(game);
        }

        SimulationSummary report = BuildSimulationSummary(
            game,
            summary,
            options,
            idleTracker,
            manufacturedUnitTracker,
            fleetHistoryTracker
        );
        string resolvedPath = WriteSimulationSummary(options.OutputPath, report);
        string completeMessage =
            $"[HeadlessSim] complete ticks={report.TicksCompleted} output={resolvedPath}";
        UnityEngine.Debug.Log(completeMessage);
        LogToFile(logPath, completeMessage);

        return new SimulationRunResult
        {
            TicksCompleted = report.TicksCompleted,
            OutputPath = resolvedPath,
            Seed = options.Seed ?? -1,
            PlayerFactionId = options.PlayerFactionId,
        };
    }

    private static void EnsureSimulationInternals()
    {
        if (_processTickMethod == null || _pendingCombatField == null)
        {
            throw new InvalidOperationException(
                "Headless simulation could not access GameManager tick internals."
            );
        }
    }

    private static GameBuilder CreateGameBuilder(GameSummary summary, int? seed)
    {
        return seed.HasValue
            ? new GameBuilder(summary, new SystemRandomProvider(seed.Value))
            : new GameBuilder(summary);
    }

    private static void ValidateAIOptions(SimulationOptions options)
    {
        if (options == null)
            return;

        string profile = string.IsNullOrWhiteSpace(options.AIProfile)
            ? _aiProfileDefault
            : options.AIProfile.Trim();
        switch (profile)
        {
            case _aiProfileDefault:
            case _aiProfileMigrationOn:
                break;
            default:
                throw new InvalidOperationException($"Unknown AI simulation profile: {profile}");
        }
    }

    private static void ApplyAIOptions(GameRoot game, SimulationOptions options)
    {
        if (game?.Config?.AI?.Infrastructure == null || options == null)
            return;
    }

    private static string GetLogPath(string outputPath)
    {
        string resolvedOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(_logDirectory);
        return Path.Combine(
            _logDirectory,
            $"{Path.GetFileNameWithoutExtension(resolvedOutputPath)}.log"
        );
    }

    private static void LogToFile(string logPath, string message)
    {
        File.AppendAllText(logPath, message + Environment.NewLine);
    }

    private static void AutoResolvePendingCombat(GameManager manager)
    {
        if (_pendingCombatField.GetValue(manager) != null)
            manager.ResolveCombat(true);
    }

    private static SimulationSummary BuildSimulationSummary(
        GameRoot game,
        GameSummary summary,
        SimulationOptions options,
        ManufacturingIdleTracker idleTracker,
        ManufacturedUnitTracker manufacturedUnitTracker,
        FleetHistoryTracker fleetHistoryTracker
    )
    {
        return new SimulationSummary
        {
            TicksRequested = options.TickCount,
            TicksCompleted = game.CurrentTick,
            PlayerFactionId = options.PlayerFactionId,
            Seed = options.Seed ?? -1,
            GalaxySize = summary.GalaxySize.ToString(),
            OutputPath = options.OutputPath,
            FleetHistory = fleetHistoryTracker.ToArray(),
            Factions = game
                .Factions.Select(faction => new FactionSimulationSummary
                {
                    OwnedPlanets = game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID)
                        .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                        .Select(planet => $"{planet.InstanceID}:{planet.GetDisplayName()}")
                        .ToArray(),
                    FactionId = faction.InstanceID,
                    DisplayName = faction.GetDisplayName(),
                    PlanetCount = game.GetSceneNodesByOwnerInstanceID<Planet>(
                        faction.InstanceID
                    ).Count,
                    FleetCount = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                        faction.InstanceID
                    ).Count,
                    BuildingCount = game.GetSceneNodesByOwnerInstanceID<Building>(
                        faction.InstanceID
                    ).Count,
                    CapitalShipCount = game.GetSceneNodesByOwnerInstanceID<CapitalShip>(
                        faction.InstanceID
                    ).Count,
                    StarfighterCount = game.GetSceneNodesByOwnerInstanceID<Starfighter>(
                        faction.InstanceID
                    ).Count,
                    RegimentCount = game.GetSceneNodesByOwnerInstanceID<Regiment>(
                        faction.InstanceID
                    ).Count,
                    SpecialForcesCount = game.GetSceneNodesByOwnerInstanceID<SpecialForces>(
                        faction.InstanceID
                    ).Count,
                    OfficerCount = game.GetSceneNodesByOwnerInstanceID<Officer>(
                        faction.InstanceID
                    ).Count,
                    UnlockedSpecialForcesTechCount = faction
                        .GetUnlockedTechnologies(ManufacturingType.Troop)
                        .Count(tech => tech.GetReference() is SpecialForces),
                    RawMaterials = faction.RawMaterialSupply,
                    RefinedMaterials = faction.RefinedMaterialSupply,
                    MaintenanceCapacity = faction.MaintenanceCapacity,
                    MaintenanceHeadroom = faction.MaintenanceHeadroom,
                    Economy = BuildEconomySummary(faction),
                    Energy = faction.GetTotalAvailableEnergy(),
                    UnitCost = faction.GetTotalMaintenanceCost(),
                    TotalManufacturedCapitalShips =
                        manufacturedUnitTracker.GetManufacturedCapitalShips(faction.InstanceID),
                    TotalManufacturedStarfighters =
                        manufacturedUnitTracker.GetManufacturedStarfighters(faction.InstanceID),
                    TotalManufacturedRegiments = manufacturedUnitTracker.GetManufacturedRegiments(
                        faction.InstanceID
                    ),
                    TotalManufacturedSpecialForces =
                        manufacturedUnitTracker.GetManufacturedSpecialForces(faction.InstanceID),
                    TotalManufacturedBuildings = manufacturedUnitTracker.GetManufacturedBuildings(
                        faction.InstanceID
                    ),
                    TotalManufacturedMines = manufacturedUnitTracker.GetManufacturedBuildings(
                        faction.InstanceID,
                        BuildingType.Mine
                    ),
                    TotalManufacturedRefineries = manufacturedUnitTracker.GetManufacturedBuildings(
                        faction.InstanceID,
                        BuildingType.Refinery
                    ),
                    TotalManufacturedConstructionFacilities =
                        manufacturedUnitTracker.GetManufacturedBuildings(
                            faction.InstanceID,
                            BuildingType.ConstructionFacility
                        ),
                    TotalManufacturedShipyards = manufacturedUnitTracker.GetManufacturedBuildings(
                        faction.InstanceID,
                        BuildingType.Shipyard
                    ),
                    TotalManufacturedTrainingFacilities =
                        manufacturedUnitTracker.GetManufacturedBuildings(
                            faction.InstanceID,
                            BuildingType.TrainingFacility
                        ),
                    TotalManufacturedDefenseFacilities =
                        manufacturedUnitTracker.GetManufacturedBuildings(
                            faction.InstanceID,
                            BuildingType.Defense
                        ),
                    TotalManufacturedWeapons = manufacturedUnitTracker.GetManufacturedBuildings(
                        faction.InstanceID,
                        BuildingType.Weapon
                    ),
                    ConstructionFacilityExpansion = BuildConstructionFacilityExpansionSummary(
                        faction
                    ),
                    TroopProduction = BuildTroopProductionSummary(faction),
                    TroopReinforcementPackages = BuildTroopReinforcementPackageSummary(faction),
                    CapitalShipProduction = BuildCapitalShipProductionSummary(faction),
                    ManufacturingIdle = idleTracker.BuildSummary(faction.InstanceID),
                    CurrentIdlePlanets = BuildCurrentIdlePlanetSummaries(game, faction),
                    Fleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(faction.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                        .Select(fleet => BuildFleetSummary(game, faction, fleet))
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static CapitalShipProductionSimulationSummary BuildCapitalShipProductionSummary(
        Faction faction
    )
    {
        if (faction == null)
            return null;

        return new CapitalShipProductionSimulationSummary
        {
            OwnedShipyardPlanetCount = CountOwnedFacilityPlanets(faction, ManufacturingType.Ship),
            AvailableShipyardPlanetCount = CountAvailableManufacturingPlanets(
                faction,
                ManufacturingType.Ship
            ),
            OwnedPlanetIdleStarfighterCount = CountOwnedIdlePlanetStarfighters(faction),
            OwnedFleetFreeStarfighterCapacity = CountOwnedFleetFreeStarfighterCapacity(faction),
            CapitalTechnologyCount = CountUnlockedCapitalTechnologies(faction),
            InfrastructureCapitalTechnologyCount = CountUnlockedInfrastructureCapitalTechnologies(
                faction
            ),
            ProducerFound = CountOwnedFacilityPlanets(faction, ManufacturingType.Ship) > 0,
            ProducerShipCapacity = CountAvailableManufacturingSlots(
                faction,
                ManufacturingType.Ship
            ),
            ProducerShipQueueCount = CountManufacturingQueueItems(faction, ManufacturingType.Ship),
            ProducerActiveCapitalShipCount = CountActiveCapitalShipManufacturing(faction),
        };
    }

    private static EconomySimulationSummary BuildEconomySummary(Faction faction)
    {
        if (faction == null)
            return null;

        List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();
        int rawResourceNodes = planets.Sum(planet => planet.GetRawResourceNodes());
        int activeMines = planets.Sum(planet => planet.GetBuildingTypeCount(BuildingType.Mine));
        int queuedMines = planets.Sum(planet =>
            CountQueuedBuildings(planet, faction.InstanceID, BuildingType.Mine)
        );
        int projectedMines = planets.Sum(planet =>
            CountProjectedBuildings(planet, faction.InstanceID, BuildingType.Mine)
        );
        int activeRefineries = planets.Sum(planet =>
            planet.GetBuildingTypeCount(BuildingType.Refinery)
        );
        int queuedRefineries = planets.Sum(planet =>
            CountQueuedBuildings(planet, faction.InstanceID, BuildingType.Refinery)
        );
        int projectedRefineries = planets.Sum(planet =>
            CountProjectedBuildings(planet, faction.InstanceID, BuildingType.Refinery)
        );
        int projectedMinedResources = Math.Min(rawResourceNodes, projectedMines);
        int projectedRefineryCapacity = projectedRefineries;
        int effectiveRefinedOutput = Math.Min(projectedMinedResources, projectedRefineryCapacity);

        return new EconomySimulationSummary
        {
            RawResourceNodes = rawResourceNodes,
            ActiveMines = activeMines,
            QueuedMines = queuedMines,
            ProjectedMines = projectedMines,
            ActiveRefineries = activeRefineries,
            QueuedRefineries = queuedRefineries,
            ProjectedRefineries = projectedRefineries,
            ProjectedMinedResources = projectedMinedResources,
            ProjectedRefineryCapacity = projectedRefineryCapacity,
            EffectiveRefinedOutput = effectiveRefinedOutput,
            MineDeficit = Math.Max(0, rawResourceNodes - projectedMinedResources),
            RefineryDeficit = Math.Max(0, projectedMinedResources - projectedRefineryCapacity),
            UnusedMinedResources = Math.Max(0, projectedMinedResources - effectiveRefinedOutput),
            UnusedRefineryCapacity = Math.Max(
                0,
                projectedRefineryCapacity - effectiveRefinedOutput
            ),
        };
    }

    private static int CountQueuedBuildings(Planet planet, string factionId, BuildingType type)
    {
        return planet
            .GetAllBuildings()
            .Count(building =>
                building.GetBuildingType() == type
                && building.GetOwnerInstanceID() == factionId
                && building.GetManufacturingStatus() == ManufacturingStatus.Building
            );
    }

    private static int CountProjectedBuildings(Planet planet, string factionId, BuildingType type)
    {
        return planet
            .GetAllBuildings()
            .Count(building =>
                building.GetBuildingType() == type && building.GetOwnerInstanceID() == factionId
            );
    }

    private static string WriteSimulationSummary(string outputPath, SimulationSummary report)
    {
        string resolvedPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(resolvedPath, UnityEngine.JsonUtility.ToJson(report, true));
        return resolvedPath;
    }

    private static TroopProductionSimulationSummary BuildTroopProductionSummary(Faction faction)
    {
        if (faction == null)
            return null;

        return new TroopProductionSimulationSummary
        {
            CandidateTargetCount = CountOwnedFacilityPlanets(faction, ManufacturingType.Troop),
            FinalCandidateTargetCount = CountAvailableManufacturingPlanets(
                faction,
                ManufacturingType.Troop
            ),
            CandidateRegimentCount = faction.GetOwnedUnitsByType<Regiment>().Count,
            OwnedTrainingPlanetCount = CountOwnedFacilityPlanets(faction, ManufacturingType.Troop),
        };
    }

    private static int CountOwnedFacilityPlanets(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Count(planet =>
                    planet
                        ?.GetAllBuildings()
                        .Any(building =>
                            building.GetProductionType() == type
                            && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                            && building.Movement == null
                        ) == true
                )
            ?? 0;
    }

    private static int CountAvailableManufacturingPlanets(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Count(planet => planet.GetAvailableManufacturingCapacity(type) > 0)
            ?? 0;
    }

    private static int CountAvailableManufacturingSlots(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Sum(planet => planet.GetAvailableManufacturingCapacity(type))
            ?? 0;
    }

    private static int CountManufacturingQueueItems(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Sum(planet => GetManufacturingQueueCount(planet, type))
            ?? 0;
    }

    private static int CountOwnedIdlePlanetStarfighters(Faction faction)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .SelectMany(planet => planet.GetAllStarfighters())
                .Count(starfighter =>
                    starfighter != null
                    && starfighter.GetOwnerInstanceID() == faction.InstanceID
                    && starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && starfighter.Movement == null
                )
            ?? 0;
    }

    private static int CountOwnedFleetFreeStarfighterCapacity(Faction faction)
    {
        return faction
                ?.GetOwnedUnitsByType<Fleet>()
                .Where(fleet => fleet != null && fleet.GetOwnerInstanceID() == faction.InstanceID)
                .Sum(fleet => Math.Max(0, fleet.GetExcessStarfighterCapacity()))
            ?? 0;
    }

    private static int CountUnlockedCapitalTechnologies(Faction faction)
    {
        return faction
                ?.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Count(technology => technology.GetReference() is CapitalShip)
            ?? 0;
    }

    private static int CountUnlockedInfrastructureCapitalTechnologies(Faction faction)
    {
        return faction
                ?.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Count(technology =>
                    technology.GetReference() is CapitalShip ship
                    && (
                        ship.HasRole(CapitalShipRole.PrimaryLine)
                        || ship.HasRole(CapitalShipRole.SecondaryLine)
                        || ship.HasRole(CapitalShipRole.Escort)
                        || ship.HasRole(CapitalShipRole.Carrier)
                        || ship.HasRole(CapitalShipRole.Interdictor)
                        || ship.HasRole(CapitalShipRole.Flagship)
                    )
                )
            ?? 0;
    }

    private static int GetManufacturingQueueCount(Planet planet, ManufacturingType type)
    {
        if (planet == null)
            return 0;

        return planet.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue)
            ? queue.Count
            : 0;
    }

    private static int CountActiveCapitalShipManufacturing(Faction faction)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Sum(planet =>
                    planet
                        .GetManufacturingQueue()
                        .TryGetValue(ManufacturingType.Ship, out List<IManufacturable> queue)
                        ? queue
                            .OfType<CapitalShip>()
                            .Count(ship => ship.ManufacturingStatus == ManufacturingStatus.Building)
                        : 0
                )
            ?? 0;
    }

    private static ConstructionFacilityExpansionSimulationSummary BuildConstructionFacilityExpansionSummary(
        Faction faction
    )
    {
        if (faction == null)
            return null;

        List<Planet> ownedPlanets = faction.GetOwnedUnitsByType<Planet>();
        int activeConstructionFacilities = ownedPlanets.Sum(planet =>
            planet.GetBuildingTypeCount(BuildingType.ConstructionFacility)
        );
        int projectedConstructionFacilities = ownedPlanets.Sum(planet =>
            planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
        );

        return new ConstructionFacilityExpansionSimulationSummary
        {
            PrimaryCandidateCount = CountOwnedFacilityPlanets(faction, ManufacturingType.Building),
            FinalCandidateCount = CountAvailableManufacturingPlanets(
                faction,
                ManufacturingType.Building
            ),
            ProducerConstructionCapacityLimit = CountAvailableManufacturingSlots(
                faction,
                ManufacturingType.Building
            ),
            ActiveConstructionFacilityCount = activeConstructionFacilities,
            ProjectedConstructionFacilityCount = projectedConstructionFacilities,
            ConstructionFacilityPlanetCount = ownedPlanets.Count(planet =>
                planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility) > 0
            ),
            LargestPlanetConstructionFacilityCount = ownedPlanets
                .Select(planet =>
                    planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
                )
                .DefaultIfEmpty()
                .Max(),
            LargestSystemConstructionFacilityCount = GetLargestSystemConstructionFacilityCount(
                ownedPlanets
            ),
            LargestPlanetConstructionFacilityShare = GetShare(
                ownedPlanets
                    .Select(planet =>
                        planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
                    )
                    .DefaultIfEmpty()
                    .Max(),
                projectedConstructionFacilities
            ),
            LargestSystemConstructionFacilityShare = GetShare(
                GetLargestSystemConstructionFacilityCount(ownedPlanets),
                projectedConstructionFacilities
            ),
        };
    }

    private static int GetLargestSystemConstructionFacilityCount(List<Planet> planets)
    {
        return planets
            .GroupBy(planet =>
                planet.GetParentOfType<PlanetSystem>()?.InstanceID ?? planet.InstanceID
            )
            .Select(group =>
                group.Sum(planet =>
                    planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
                )
            )
            .DefaultIfEmpty()
            .Max();
    }

    private static double GetShare(int value, int total)
    {
        if (total <= 0)
            return 0;

        return (double)value / total;
    }

    private static TroopReinforcementPackageSimulationSummary BuildTroopReinforcementPackageSummary(
        Faction faction
    )
    {
        if (faction == null)
            return null;

        return new TroopReinforcementPackageSimulationSummary
        {
            SecondaryCandidateCount = faction.GetOwnedUnitsByType<Fleet>().Count,
            SelectedCandidateTrainingFacilityCount = CountOwnedFacilityPlanets(
                faction,
                ManufacturingType.Troop
            ),
            SelectedCandidateRegimentCount = faction.GetOwnedUnitsByType<Regiment>().Count,
        };
    }

    [Serializable]
    public sealed class SimulationRunResult
    {
        public int TicksCompleted;
        public string OutputPath;
        public int Seed = -1;
        public string PlayerFactionId;
    }

    [Serializable]
    private sealed class SimulationSummary
    {
        public int TicksRequested;
        public int TicksCompleted;
        public string PlayerFactionId;
        public int Seed = -1;
        public string GalaxySize;
        public string OutputPath;
        public FleetHistorySnapshot[] FleetHistory;
        public FactionSimulationSummary[] Factions;
    }

    [Serializable]
    private sealed class FactionSimulationSummary
    {
        public string[] OwnedPlanets;
        public string FactionId;
        public string DisplayName;
        public int PlanetCount;
        public int FleetCount;
        public int BuildingCount;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int SpecialForcesCount;
        public int OfficerCount;
        public int UnlockedSpecialForcesTechCount;
        public int RawMaterials;
        public int RefinedMaterials;
        public int MaintenanceCapacity;
        public int MaintenanceHeadroom;
        public EconomySimulationSummary Economy;
        public int Energy;
        public int UnitCost;
        public int TotalManufacturedCapitalShips;
        public int TotalManufacturedStarfighters;
        public int TotalManufacturedRegiments;
        public int TotalManufacturedSpecialForces;
        public int TotalManufacturedBuildings;
        public int TotalManufacturedMines;
        public int TotalManufacturedRefineries;
        public int TotalManufacturedConstructionFacilities;
        public int TotalManufacturedShipyards;
        public int TotalManufacturedTrainingFacilities;
        public int TotalManufacturedDefenseFacilities;
        public int TotalManufacturedWeapons;
        public ConstructionFacilityExpansionSimulationSummary ConstructionFacilityExpansion;
        public TroopProductionSimulationSummary TroopProduction;
        public TroopReinforcementPackageSimulationSummary TroopReinforcementPackages;
        public CapitalShipProductionSimulationSummary CapitalShipProduction;
        public ManufacturingIdleSummary ManufacturingIdle;
        public CurrentIdlePlanetSummary[] CurrentIdlePlanets;
        public FleetSimulationSummary[] Fleets;
    }

    [Serializable]
    private sealed class ConstructionFacilityExpansionSimulationSummary
    {
        public int ProducerConstructionCapacityLimit;
        public int PrimaryCandidateCount;
        public int FinalCandidateCount;
        public int ActiveConstructionFacilityCount;
        public int ProjectedConstructionFacilityCount;
        public int ConstructionFacilityPlanetCount;
        public int LargestPlanetConstructionFacilityCount;
        public int LargestSystemConstructionFacilityCount;
        public double LargestPlanetConstructionFacilityShare;
        public double LargestSystemConstructionFacilityShare;
    }

    [Serializable]
    private sealed class TroopProductionSimulationSummary
    {
        public int CandidateTargetCount;
        public int FinalCandidateTargetCount;
        public int CandidateRegimentCount;
        public int OwnedTrainingPlanetCount;
    }

    [Serializable]
    private sealed class TroopReinforcementPackageSimulationSummary
    {
        public int SecondaryCandidateCount;
        public int SelectedCandidateTrainingFacilityCount;
        public int SelectedCandidateRegimentCount;
    }

    [Serializable]
    private sealed class CapitalShipProductionSimulationSummary
    {
        public int OwnedShipyardPlanetCount;
        public int AvailableShipyardPlanetCount;
        public int OwnedPlanetIdleStarfighterCount;
        public int OwnedFleetFreeStarfighterCapacity;
        public int CapitalTechnologyCount;
        public int InfrastructureCapitalTechnologyCount;
        public bool ProducerFound;
        public int ProducerShipCapacity;
        public int ProducerShipQueueCount;
        public int ProducerActiveCapitalShipCount;
    }

    [Serializable]
    private sealed class EconomySimulationSummary
    {
        public int RawResourceNodes;
        public int ActiveMines;
        public int QueuedMines;
        public int ProjectedMines;
        public int ActiveRefineries;
        public int QueuedRefineries;
        public int ProjectedRefineries;
        public int ProjectedMinedResources;
        public int ProjectedRefineryCapacity;
        public int EffectiveRefinedOutput;
        public int MineDeficit;
        public int RefineryDeficit;
        public int UnusedMinedResources;
        public int UnusedRefineryCapacity;
    }

    private sealed class ManufacturedUnitTracker
    {
        private readonly HashSet<string> _seenCapitalShips = new HashSet<string>();
        private readonly HashSet<string> _seenStarfighters = new HashSet<string>();
        private readonly HashSet<string> _seenRegiments = new HashSet<string>();
        private readonly HashSet<string> _seenSpecialForces = new HashSet<string>();
        private readonly HashSet<string> _seenBuildings = new HashSet<string>();
        private readonly Dictionary<string, ManufacturedUnitCounts> _manufacturedByFaction =
            new Dictionary<string, ManufacturedUnitCounts>(StringComparer.Ordinal);

        public void RecordInitialState(GameRoot game)
        {
            RecordSeenOnly(game.GetSceneNodesByType<CapitalShip>(), _seenCapitalShips);
            RecordSeenOnly(game.GetSceneNodesByType<Starfighter>(), _seenStarfighters);
            RecordSeenOnly(game.GetSceneNodesByType<Regiment>(), _seenRegiments);
            RecordSeenOnly(game.GetSceneNodesByType<SpecialForces>(), _seenSpecialForces);
            RecordSeenOnly(game.GetSceneNodesByType<Building>(), _seenBuildings);
        }

        public void RecordTick(GameRoot game)
        {
            RecordNewUnits(
                game.GetSceneNodesByType<CapitalShip>(),
                _seenCapitalShips,
                counts => counts.CapitalShips++
            );
            RecordNewUnits(
                game.GetSceneNodesByType<Starfighter>(),
                _seenStarfighters,
                counts => counts.Starfighters++
            );
            RecordNewUnits(
                game.GetSceneNodesByType<Regiment>(),
                _seenRegiments,
                counts => counts.Regiments++
            );
            RecordNewUnits(
                game.GetSceneNodesByType<SpecialForces>(),
                _seenSpecialForces,
                counts => counts.SpecialForces++
            );
            RecordNewBuildings(game.GetSceneNodesByType<Building>());
        }

        public int GetManufacturedCapitalShips(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.CapitalShips : 0;

        public int GetManufacturedStarfighters(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Starfighters : 0;

        public int GetManufacturedRegiments(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Regiments : 0;

        public int GetManufacturedSpecialForces(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.SpecialForces : 0;

        public int GetManufacturedBuildings(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Buildings : 0;

        public int GetManufacturedBuildings(string factionId, BuildingType buildingType) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts)
            && counts.BuildingsByType.TryGetValue(buildingType, out int count)
                ? count
                : 0;

        private static void RecordSeenOnly<T>(IEnumerable<T> units, HashSet<string> seen)
            where T : ISceneNode
        {
            foreach (T unit in units)
            {
                string instanceId = unit.GetInstanceID();
                if (!string.IsNullOrEmpty(instanceId))
                    seen.Add(instanceId);
            }
        }

        private void RecordNewUnits<T>(
            IEnumerable<T> units,
            HashSet<string> seen,
            Action<ManufacturedUnitCounts> increment
        )
            where T : ISceneNode
        {
            foreach (T unit in units)
            {
                string instanceId = unit.GetInstanceID();
                if (string.IsNullOrEmpty(instanceId) || !seen.Add(instanceId))
                    continue;

                string factionId = unit.GetOwnerInstanceID();
                if (string.IsNullOrEmpty(factionId))
                    continue;

                increment(GetCounts(factionId));
            }
        }

        private void RecordNewBuildings(IEnumerable<Building> buildings)
        {
            foreach (Building building in buildings)
            {
                string instanceId = building.GetInstanceID();
                if (string.IsNullOrEmpty(instanceId) || !_seenBuildings.Add(instanceId))
                    continue;

                string factionId = building.GetOwnerInstanceID();
                if (string.IsNullOrEmpty(factionId))
                    continue;

                ManufacturedUnitCounts counts = GetCounts(factionId);
                counts.Buildings++;
                counts.BuildingsByType.TryGetValue(building.BuildingType, out int count);
                counts.BuildingsByType[building.BuildingType] = count + 1;
            }
        }

        private ManufacturedUnitCounts GetCounts(string factionId)
        {
            if (!_manufacturedByFaction.TryGetValue(factionId, out ManufacturedUnitCounts counts))
            {
                counts = new ManufacturedUnitCounts();
                _manufacturedByFaction[factionId] = counts;
            }

            return counts;
        }

        private bool TryGetCounts(string factionId, out ManufacturedUnitCounts counts) =>
            _manufacturedByFaction.TryGetValue(factionId, out counts);
    }

    private sealed class ManufacturedUnitCounts
    {
        public int CapitalShips;
        public int Starfighters;
        public int Regiments;
        public int SpecialForces;
        public int Buildings;
        public Dictionary<BuildingType, int> BuildingsByType = new Dictionary<BuildingType, int>();
    }

    [Serializable]
    private sealed class ManufacturingIdleSummary
    {
        public int BuildingIdlePlanetTicks;
        public int ShipIdlePlanetTicks;
        public int TroopIdlePlanetTicks;
        public int BuildingIdleCapacityTicks;
        public int ShipIdleCapacityTicks;
        public int TroopIdleCapacityTicks;
        public ManufacturingIdlePlanetSummary[] TopIdlePlanets;
    }

    [Serializable]
    private sealed class ManufacturingIdlePlanetSummary
    {
        public string PlanetId;
        public string PlanetName;
        public int BuildingIdleTicks;
        public int ShipIdleTicks;
        public int TroopIdleTicks;
        public int BuildingIdleCapacityTicks;
        public int ShipIdleCapacityTicks;
        public int TroopIdleCapacityTicks;
    }

    [Serializable]
    private sealed class CurrentIdlePlanetSummary
    {
        public string PlanetId;
        public string PlanetName;
        public int BuildingSlots;
        public int ShipSlots;
        public int TroopSlots;
        public int RawResourceNodes;
        public int ActiveMines;
        public int ActiveRefineries;
        public int ConstructionFacilities;
        public int Shipyards;
        public int TrainingFacilities;
        public int BuildingQueueCount;
        public int ShipQueueCount;
        public int TroopQueueCount;
    }

    [Serializable]
    private sealed class FleetSimulationSummary
    {
        public string FleetId;
        public string DisplayName;
        public string RoleType;
        public string LocationPlanetId;
        public string LocationPlanetName;
        public bool InTransit;
        public int TransitTicksRemaining;
        public int CombatValue;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int OfficerCount;
        public string OrderType;
        public string OrderStatus;
        public string OrderTargetPlanetId;
        public string OrderTargetPlanetName;
        public string OrderTargetOwnerId;
        public int AssaultStrength;
        public int RegimentCapacity;
        public int RequiredAttackCombatStrength;
        public int RequiredAttackRegimentCount;
        public int TargetDefenseStrength;
        public int TargetRegimentCount;
        public int TargetStrongestHostileFleetStrength;
        public string[] CapitalShips;
        public string[] Starfighters;
        public string[] Regiments;
        public string[] Officers;
    }

    [Serializable]
    private sealed class FleetHistorySnapshot
    {
        public int Tick;
        public string FactionId;
        public string FactionName;
        public string FleetId;
        public string DisplayName;
        public string RoleType;
        public string LocationPlanetId;
        public string LocationPlanetName;
        public bool InTransit;
        public bool Destroyed;
        public int TransitTicksRemaining;
        public int CombatValue;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int OfficerCount;
        public string OrderType;
        public string OrderStatus;
        public string OrderTargetPlanetId;
        public string OrderTargetPlanetName;
        public string[] CapitalShips;
        public string[] Starfighters;
        public string[] Regiments;
        public string[] Officers;
    }

    private sealed class FleetHistoryTracker
    {
        private readonly Dictionary<string, string> _lastSnapshotKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FleetHistorySnapshot> _lastSnapshots = new(
            StringComparer.Ordinal
        );
        private readonly List<FleetHistorySnapshot> _snapshots = new();

        public void RecordTick(GameRoot game)
        {
            HashSet<string> liveFleetIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (Faction faction in game.Factions.OrderBy(faction => faction.InstanceID))
            {
                foreach (
                    Fleet fleet in game.GetSceneNodesByOwnerInstanceID<Fleet>(faction.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                )
                {
                    liveFleetIds.Add(fleet.InstanceID);
                    FleetHistorySnapshot snapshot = BuildSnapshot(game, faction, fleet, false);
                    string snapshotKey = BuildSnapshotKey(snapshot);

                    if (
                        _lastSnapshotKeys.TryGetValue(fleet.InstanceID, out string previousKey)
                        && previousKey == snapshotKey
                    )
                        continue;

                    _snapshots.Add(snapshot);
                    _lastSnapshotKeys[fleet.InstanceID] = snapshotKey;
                    _lastSnapshots[fleet.InstanceID] = snapshot;
                }
            }

            List<string> destroyedFleetIds = _lastSnapshotKeys
                .Keys.Where(fleetId => !liveFleetIds.Contains(fleetId))
                .OrderBy(fleetId => fleetId, StringComparer.Ordinal)
                .ToList();

            foreach (string fleetId in destroyedFleetIds)
            {
                FleetHistorySnapshot previousSnapshot = _lastSnapshots[fleetId];
                FleetHistorySnapshot destroyedSnapshot = BuildDestroyedSnapshot(
                    game.CurrentTick,
                    previousSnapshot
                );
                _snapshots.Add(destroyedSnapshot);
                _lastSnapshotKeys.Remove(fleetId);
                _lastSnapshots.Remove(fleetId);
            }
        }

        public FleetHistorySnapshot[] ToArray()
        {
            return _snapshots.ToArray();
        }

        private static FleetHistorySnapshot BuildSnapshot(
            GameRoot game,
            Faction faction,
            Fleet fleet,
            bool destroyed
        )
        {
            Planet location = fleet.GetParentOfType<Planet>();
            Planet targetPlanet = string.IsNullOrEmpty(fleet.Order?.TargetPlanetId)
                ? null
                : game.GetSceneNodeByInstanceID<Planet>(fleet.Order.TargetPlanetId);

            return new FleetHistorySnapshot
            {
                Tick = game.CurrentTick,
                FactionId = faction.InstanceID,
                FactionName = faction.GetDisplayName(),
                FleetId = fleet.InstanceID,
                DisplayName = fleet.GetDisplayName(),
                RoleType = fleet.RoleType.ToString(),
                LocationPlanetId = location?.InstanceID,
                LocationPlanetName = location?.GetDisplayName(),
                InTransit = fleet.Movement != null,
                Destroyed = destroyed,
                TransitTicksRemaining = fleet.Movement?.TicksRemaining() ?? 0,
                CombatValue = fleet.GetCombatValue(),
                CapitalShipCount = fleet.CapitalShips.Count,
                StarfighterCount = fleet.GetStarfighters().Count(),
                RegimentCount = fleet.GetRegiments().Count(),
                OfficerCount = fleet.GetOfficers().Count(),
                OrderType = fleet.Order?.OrderType.ToString(),
                OrderStatus = fleet.Order?.Status.ToString(),
                OrderTargetPlanetId = fleet.Order?.TargetPlanetId,
                OrderTargetPlanetName = targetPlanet?.GetDisplayName(),
                CapitalShips = SummarizeUnits(fleet.CapitalShips),
                Starfighters = SummarizeUnits(fleet.GetStarfighters()),
                Regiments = SummarizeUnits(fleet.GetRegiments()),
                Officers = SummarizeUnits(fleet.GetOfficers()),
            };
        }

        private static FleetHistorySnapshot BuildDestroyedSnapshot(
            int tick,
            FleetHistorySnapshot previousSnapshot
        )
        {
            return new FleetHistorySnapshot
            {
                Tick = tick,
                FactionId = previousSnapshot.FactionId,
                FactionName = previousSnapshot.FactionName,
                FleetId = previousSnapshot.FleetId,
                DisplayName = previousSnapshot.DisplayName,
                RoleType = previousSnapshot.RoleType,
                LocationPlanetId = previousSnapshot.LocationPlanetId,
                LocationPlanetName = previousSnapshot.LocationPlanetName,
                Destroyed = true,
                OrderType = previousSnapshot.OrderType,
                OrderStatus = previousSnapshot.OrderStatus,
                OrderTargetPlanetId = previousSnapshot.OrderTargetPlanetId,
                OrderTargetPlanetName = previousSnapshot.OrderTargetPlanetName,
                CapitalShips = Array.Empty<string>(),
                Starfighters = Array.Empty<string>(),
                Regiments = Array.Empty<string>(),
                Officers = Array.Empty<string>(),
            };
        }

        private static string BuildSnapshotKey(FleetHistorySnapshot snapshot)
        {
            return string.Join(
                "|",
                snapshot.FactionId,
                snapshot.DisplayName,
                snapshot.RoleType,
                snapshot.LocationPlanetId,
                snapshot.InTransit,
                snapshot.Destroyed,
                snapshot.TransitTicksRemaining,
                snapshot.CombatValue,
                snapshot.OrderType,
                snapshot.OrderStatus,
                snapshot.OrderTargetPlanetId,
                string.Join(",", snapshot.CapitalShips),
                string.Join(",", snapshot.Starfighters),
                string.Join(",", snapshot.Regiments),
                string.Join(",", snapshot.Officers)
            );
        }
    }

    private sealed class SimulationOptions
    {
        public int TickCount { get; set; }
        public string OutputPath { get; set; }
        public string PlayerFactionId { get; set; }
        public int? Seed { get; set; }
        public string AIProfile { get; set; }

        public static SimulationOptions Parse(string[] args)
        {
            return new SimulationOptions
            {
                TickCount = ParseInt(args, _tickCountFlag, 20),
                OutputPath = ParseString(
                    args,
                    _outputPathFlag,
                    "SimulationResults/headless-simulation-summary.json"
                ),
                PlayerFactionId = ParseString(args, _playerFactionFlag, null),
                Seed = ParseNullableInt(args, _seedFlag),
                AIProfile = ParseString(args, _aiProfileFlag, null),
            };
        }

        private static int ParseInt(string[] args, string flag, int defaultValue)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        private static int? ParseNullableInt(string[] args, string flag)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : null;
        }

        private static string ParseString(string[] args, string flag, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return defaultValue;
        }
    }

    private static FleetSimulationSummary BuildFleetSummary(
        GameRoot game,
        Faction faction,
        Fleet fleet
    )
    {
        Planet location = fleet.GetParentOfType<Planet>();
        Planet targetPlanet = string.IsNullOrEmpty(fleet.Order?.TargetPlanetId)
            ? null
            : game.GetSceneNodeByInstanceID<Planet>(fleet.Order.TargetPlanetId);
        int assaultStrength = fleet.GetAssaultStrength(game.Config.Combat.AssaultPersonnelDivisor);
        int targetDefenseStrength = targetPlanet?.GetDefenseStrength() ?? 0;
        int targetRegimentCount = targetPlanet?.GetAllRegiments().Count ?? 0;
        int targetStrongestHostileFleetStrength = GetStrongestHostileFleetStrength(
            faction,
            targetPlanet
        );
        int requiredAttackCombatStrength = GetRequiredAttackCombatStrength(
            game,
            targetDefenseStrength,
            targetStrongestHostileFleetStrength
        );
        int requiredAttackRegimentCount = GetRequiredAttackRegimentCount(
            game,
            faction,
            targetPlanet,
            targetRegimentCount
        );

        return new FleetSimulationSummary
        {
            FleetId = fleet.InstanceID,
            DisplayName = fleet.GetDisplayName(),
            RoleType = fleet.RoleType.ToString(),
            LocationPlanetId = location?.InstanceID,
            LocationPlanetName = location?.GetDisplayName(),
            InTransit = fleet.Movement != null,
            TransitTicksRemaining = fleet.Movement?.TicksRemaining() ?? 0,
            CombatValue = fleet.GetCombatValue(),
            CapitalShipCount = fleet.CapitalShips.Count,
            StarfighterCount = fleet.GetStarfighters().Count(),
            RegimentCount = fleet.GetRegiments().Count(),
            OfficerCount = fleet.GetOfficers().Count(),
            OrderType = fleet.Order?.OrderType.ToString(),
            OrderStatus = fleet.Order?.Status.ToString(),
            OrderTargetPlanetId = fleet.Order?.TargetPlanetId,
            OrderTargetPlanetName = targetPlanet?.GetDisplayName(),
            OrderTargetOwnerId = targetPlanet?.GetOwnerInstanceID(),
            AssaultStrength = assaultStrength,
            RegimentCapacity = fleet.GetRegimentCapacity(),
            RequiredAttackCombatStrength = requiredAttackCombatStrength,
            RequiredAttackRegimentCount = requiredAttackRegimentCount,
            TargetDefenseStrength = targetDefenseStrength,
            TargetRegimentCount = targetRegimentCount,
            TargetStrongestHostileFleetStrength = targetStrongestHostileFleetStrength,
            CapitalShips = SummarizeUnits(fleet.CapitalShips),
            Starfighters = SummarizeUnits(fleet.GetStarfighters()),
            Regiments = SummarizeUnits(fleet.GetRegiments()),
            Officers = SummarizeUnits(fleet.GetOfficers()),
        };
    }

    private static int GetStrongestHostileFleetStrength(Faction faction, Planet targetPlanet)
    {
        if (faction == null || targetPlanet == null)
            return 0;

        return targetPlanet
            .GetFleets()
            .Where(fleet =>
                fleet.GetOwnerInstanceID() != null
                && fleet.GetOwnerInstanceID() != faction.InstanceID
                && fleet.Movement == null
            )
            .Select(fleet => fleet.GetCombatValue())
            .DefaultIfEmpty()
            .Max();
    }

    private static int GetRequiredAttackCombatStrength(
        GameRoot game,
        int targetDefenseStrength,
        int targetStrongestHostileFleetStrength
    )
    {
        GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
        int shieldDefenseRequirement =
            targetDefenseStrength * config.AttackStrengthPercentOfDefense / 100;
        int fleetDefenseRequirement =
            targetStrongestHostileFleetStrength
            * config.AttackStrengthPercentOfStrongestHostileFleet
            / 100;

        return Math.Max(
            config.MinimumAttackStrength,
            Math.Max(shieldDefenseRequirement, fleetDefenseRequirement)
        );
    }

    private static int GetRequiredAttackRegimentCount(
        GameRoot game,
        Faction faction,
        Planet targetPlanet,
        int targetRegimentCount
    )
    {
        if (game == null || faction == null || targetPlanet == null)
            return 1;

        return Math.Max(
            1,
            targetRegimentCount
                + UprisingSystem.CalculateGarrisonRequirement(
                    targetPlanet,
                    faction,
                    game.Config.AI.Garrison
                )
        );
    }

    private static CurrentIdlePlanetSummary[] BuildCurrentIdlePlanetSummaries(
        GameRoot game,
        Faction faction
    )
    {
        return game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID)
            .Where(planet => planet.CanManufactureUnits())
            .Select(planet => new CurrentIdlePlanetSummary
            {
                PlanetId = planet.InstanceID,
                PlanetName = planet.GetDisplayName(),
                BuildingSlots = planet.GetAvailableManufacturingCapacity(
                    ManufacturingType.Building
                ),
                ShipSlots = planet.GetAvailableManufacturingCapacity(ManufacturingType.Ship),
                TroopSlots = planet.GetAvailableManufacturingCapacity(ManufacturingType.Troop),
                RawResourceNodes = planet.GetRawResourceNodes(),
                ActiveMines = planet.GetActiveMinedResources(),
                ActiveRefineries = planet.GetActiveRefinementCapacity(),
                ConstructionFacilities = planet.GetBuildingTypeCount(
                    BuildingType.ConstructionFacility
                ),
                Shipyards = planet.GetBuildingTypeCount(BuildingType.Shipyard),
                TrainingFacilities = planet.GetBuildingTypeCount(BuildingType.TrainingFacility),
                BuildingQueueCount = GetManufacturingQueueCount(planet, ManufacturingType.Building),
                ShipQueueCount = GetManufacturingQueueCount(planet, ManufacturingType.Ship),
                TroopQueueCount = GetManufacturingQueueCount(planet, ManufacturingType.Troop),
            })
            .Where(summary =>
                summary.BuildingSlots > 0 || summary.ShipSlots > 0 || summary.TroopSlots > 0
            )
            .OrderByDescending(summary =>
                summary.BuildingSlots + summary.ShipSlots + summary.TroopSlots
            )
            .ThenBy(summary => summary.PlanetId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] SummarizeUnits<T>(IEnumerable<T> units)
        where T : class
    {
        return units
            .GroupBy(GetUnitLabel)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();
    }

    private static string GetUnitLabel<T>(T unit)
        where T : class
    {
        switch (unit)
        {
            case IGameEntity entity when !string.IsNullOrEmpty(entity.GetDisplayName()):
                return entity.GetDisplayName();
            case IGameEntity entity:
                return entity.GetTypeID();
            default:
                return unit?.ToString() ?? "Unknown";
        }
    }

    private sealed class ManufacturingIdleTracker
    {
        private readonly Dictionary<string, FactionIdleCounters> _factions = new();

        public void RecordTick(GameRoot game)
        {
            foreach (Faction faction in game.Factions)
            {
                FactionIdleCounters counters = GetOrCreateFactionCounters(faction.InstanceID);
                foreach (
                    Planet planet in game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID)
                )
                {
                    if (!planet.CanManufactureUnits())
                        continue;

                    RecordPlanetType(counters, planet, ManufacturingType.Building);
                    RecordPlanetType(counters, planet, ManufacturingType.Ship);
                    RecordPlanetType(counters, planet, ManufacturingType.Troop);
                }
            }
        }

        public ManufacturingIdleSummary BuildSummary(string factionId)
        {
            if (!_factions.TryGetValue(factionId, out FactionIdleCounters counters))
                return new ManufacturingIdleSummary
                {
                    TopIdlePlanets = Array.Empty<ManufacturingIdlePlanetSummary>(),
                };

            return new ManufacturingIdleSummary
            {
                BuildingIdlePlanetTicks = counters.BuildingIdlePlanetTicks,
                ShipIdlePlanetTicks = counters.ShipIdlePlanetTicks,
                TroopIdlePlanetTicks = counters.TroopIdlePlanetTicks,
                BuildingIdleCapacityTicks = counters.BuildingIdleCapacityTicks,
                ShipIdleCapacityTicks = counters.ShipIdleCapacityTicks,
                TroopIdleCapacityTicks = counters.TroopIdleCapacityTicks,
                TopIdlePlanets = counters
                    .Planets.Values.OrderByDescending(planet =>
                        planet.BuildingIdleTicks + planet.ShipIdleTicks + planet.TroopIdleTicks
                    )
                    .ThenBy(planet => planet.PlanetId, StringComparer.Ordinal)
                    .Take(10)
                    .Select(planet => new ManufacturingIdlePlanetSummary
                    {
                        PlanetId = planet.PlanetId,
                        PlanetName = planet.PlanetName,
                        BuildingIdleTicks = planet.BuildingIdleTicks,
                        ShipIdleTicks = planet.ShipIdleTicks,
                        TroopIdleTicks = planet.TroopIdleTicks,
                        BuildingIdleCapacityTicks = planet.BuildingIdleCapacityTicks,
                        ShipIdleCapacityTicks = planet.ShipIdleCapacityTicks,
                        TroopIdleCapacityTicks = planet.TroopIdleCapacityTicks,
                    })
                    .ToArray(),
            };
        }

        private static void RecordPlanetType(
            FactionIdleCounters counters,
            Planet planet,
            ManufacturingType type
        )
        {
            int completedFacilityCount = planet
                .GetBuildings(type)
                .Count(building =>
                    building.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && building.Movement == null
                );
            if (completedFacilityCount <= 0)
                return;

            int idleCapacity = planet.GetAvailableManufacturingCapacity(type);
            if (idleCapacity <= 0)
                return;

            PlanetIdleCounters planetCounters = counters.GetOrCreatePlanet(planet);
            switch (type)
            {
                case ManufacturingType.Building:
                    counters.BuildingIdlePlanetTicks++;
                    counters.BuildingIdleCapacityTicks += idleCapacity;
                    planetCounters.BuildingIdleTicks++;
                    planetCounters.BuildingIdleCapacityTicks += idleCapacity;
                    break;
                case ManufacturingType.Ship:
                    counters.ShipIdlePlanetTicks++;
                    counters.ShipIdleCapacityTicks += idleCapacity;
                    planetCounters.ShipIdleTicks++;
                    planetCounters.ShipIdleCapacityTicks += idleCapacity;
                    break;
                case ManufacturingType.Troop:
                    counters.TroopIdlePlanetTicks++;
                    counters.TroopIdleCapacityTicks += idleCapacity;
                    planetCounters.TroopIdleTicks++;
                    planetCounters.TroopIdleCapacityTicks += idleCapacity;
                    break;
            }
        }

        private FactionIdleCounters GetOrCreateFactionCounters(string factionId)
        {
            if (!_factions.TryGetValue(factionId, out FactionIdleCounters counters))
            {
                counters = new FactionIdleCounters();
                _factions[factionId] = counters;
            }

            return counters;
        }

        private sealed class FactionIdleCounters
        {
            public int BuildingIdlePlanetTicks;
            public int ShipIdlePlanetTicks;
            public int TroopIdlePlanetTicks;
            public int BuildingIdleCapacityTicks;
            public int ShipIdleCapacityTicks;
            public int TroopIdleCapacityTicks;
            public Dictionary<string, PlanetIdleCounters> Planets { get; } = new();

            public PlanetIdleCounters GetOrCreatePlanet(Planet planet)
            {
                if (!Planets.TryGetValue(planet.InstanceID, out PlanetIdleCounters counters))
                {
                    counters = new PlanetIdleCounters
                    {
                        PlanetId = planet.InstanceID,
                        PlanetName = planet.GetDisplayName(),
                    };
                    Planets[planet.InstanceID] = counters;
                }

                return counters;
            }
        }

        private sealed class PlanetIdleCounters
        {
            public string PlanetId;
            public string PlanetName;
            public int BuildingIdleTicks;
            public int ShipIdleTicks;
            public int TroopIdleTicks;
            public int BuildingIdleCapacityTicks;
            public int ShipIdleCapacityTicks;
            public int TroopIdleCapacityTicks;
        }
    }
}
