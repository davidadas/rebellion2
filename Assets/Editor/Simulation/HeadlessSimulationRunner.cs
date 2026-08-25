using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public static class HeadlessSimulationRunner
{
    private const string _tickCountFlag = "-simTicks";
    private const string _outputPathFlag = "-simOut";
    private const string _seedFlag = "-simSeed";
    private const string _logDirectory = "/tmp/rebellion2-sim-logs";
    private const string _savedSimulationPlayerId = "PLAYER1";
    private const int _percentScale = 100;

    /// <summary>
    /// Runs the command-line simulation entry point.
    /// </summary>
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

    /// <summary>
    /// Runs a simulation from an already-open editor session.
    /// </summary>
    /// <param name="tickCount">The number of ticks to simulate.</param>
    /// <param name="outputPath">The summary output path.</param>
    /// <param name="seed">The optional generation seed.</param>
    /// <param name="saveFileName">The optional save file name.</param>
    /// <param name="saveDisplayName">The optional save display name.</param>
    /// <param name="playerFactionId">The faction assigned to the player in the saved game.</param>
    /// <returns>The completed simulation result.</returns>
    public static SimulationRunResult RunPersistentSimulation(
        int tickCount,
        string outputPath,
        int? seed,
        string saveFileName = null,
        string saveDisplayName = null,
        string playerFactionId = null
    )
    {
        return RunSimulation(
            new SimulationOptions
            {
                TickCount = tickCount,
                OutputPath = outputPath,
                Seed = seed,
                SaveFileName = saveFileName,
                SaveDisplayName = saveDisplayName,
                PlayerFactionId = playerFactionId,
            }
        );
    }

    /// <summary>
    /// Runs a simulation with the specified options.
    /// </summary>
    /// <param name="options">The simulation options.</param>
    /// <returns>The completed simulation result.</returns>
    private static SimulationRunResult RunSimulation(SimulationOptions options)
    {
        string logPath = GetLogPath(options.OutputPath);
        GameLogger.Configure(logPath, enableFileLogging: true);
        GameLogger.SetMinimumLevel(GameLogger.LogLevel.Warning);
        BaseGameEntity.SetInstanceIdSeed(options.Seed);

        try
        {
            ContentPack contentPack = ContentPackLoader.OpenActive();
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Large,
                Difficulty = GameDifficulty.Easy,
                VictoryCondition = GameVictoryCondition.Conquest,
                ResourceAvailability = GameResourceAvailability.Normal,
                StartingResearchLevel = 1,
                StartingFactionIDs = contentPack.Scenario.PlayableFactionIDs.ToArray(),
                PlayerFactionID = contentPack.Scenario.PlayableFactionIDs.FirstOrDefault(),
                PackID = contentPack.Definition.ID,
                PackVersion = contentPack.Definition.Version,
                ScenarioID = contentPack.Scenario.ID,
            };
            if (options.Seed.HasValue)
                summary.Seed = options.Seed.Value;

            string startMessage =
                $"[HeadlessSim] starting ticks={options.TickCount} seed={options.Seed?.ToString() ?? "random"} galaxySize={summary.GalaxySize}";
            UnityEngine.Debug.Log(startMessage);
            LogToFile(logPath, startMessage);

            GameRoot game = CreateGameBuilder(summary, contentPack.GameData, options.Seed)
                .BuildGame();
            foreach (Faction faction in game.GetFactions())
                faction.PlayerID = null;

            GameManager manager = new GameManager(game, contentPack.GameData);
            ManufacturingIdleTracker idleTracker = new ManufacturingIdleTracker();
            ManufacturedUnitTracker manufacturedUnitTracker = new ManufacturedUnitTracker();
            FleetHistoryTracker fleetHistoryTracker = new FleetHistoryTracker();
            ActivityTracker activityTracker = new ActivityTracker();
            PersonnelOutcomeTracker personnelOutcomeTracker = new PersonnelOutcomeTracker();
            PlanetaryAssaultTracker planetaryAssaultTracker = new PlanetaryAssaultTracker();
            AttackReadinessTracker attackReadinessTracker = new AttackReadinessTracker();
            VictoryResult victory = null;
            manager.PlanetaryAssaultsResolved += planetaryAssaultTracker.Record;
            manager.VictoriesResolved += results => victory ??= results.FirstOrDefault();
            manufacturedUnitTracker.RecordInitialState(game);
            fleetHistoryTracker.RecordTick(game);
            activityTracker.RecordInitialState(game);
            personnelOutcomeTracker.RecordInitialState(game);

            for (int i = 0; i < options.TickCount && victory == null; i++)
            {
                manager.ProcessTick();
                idleTracker.RecordTick(game);
                manufacturedUnitTracker.RecordTick(game);
                fleetHistoryTracker.RecordTick(game);
                activityTracker.RecordTick(game);
                personnelOutcomeTracker.RecordTick(game);
                attackReadinessTracker.RecordTick(game);
            }

            string savePath = SaveSimulation(game, options);
            SimulationSummary report = BuildSimulationSummary(
                game,
                summary,
                options,
                idleTracker,
                manufacturedUnitTracker,
                fleetHistoryTracker,
                activityTracker,
                personnelOutcomeTracker,
                planetaryAssaultTracker,
                attackReadinessTracker,
                victory
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
                SavePath = savePath,
            };
        }
        finally
        {
            BaseGameEntity.SetInstanceIdSeed(null);
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Debug);
            GameLogger.Configure(enableFileLogging: false);
        }
    }

    /// <summary>
    /// Saves and reloads a completed simulation when the options request a save artifact.
    /// </summary>
    /// <param name="game">The completed simulated game.</param>
    /// <param name="options">The simulation options containing save configuration.</param>
    /// <returns>The validated save-file path, or an empty string when saving is disabled.</returns>
    private static string SaveSimulation(GameRoot game, SimulationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SaveFileName))
            return string.Empty;

        Faction playerFaction = game.GetFactions()
            .FirstOrDefault(faction => faction.InstanceID == options.PlayerFactionId);
        if (playerFaction == null)
        {
            throw new InvalidOperationException(
                $"Cannot save simulation with unknown player faction '{options.PlayerFactionId}'."
            );
        }

        game.Summary.PlayerFactionID = playerFaction.InstanceID;
        foreach (Faction faction in game.GetFactions())
        {
            faction.PlayerID =
                faction.InstanceID == playerFaction.InstanceID ? _savedSimulationPlayerId : null;
        }

        SaveGameManager saveManager = SaveGameManager.Instance;
        saveManager.SaveGameData(game, options.SaveFileName, options.SaveDisplayName);
        GameRoot loadedGame = saveManager.LoadGameData(options.SaveFileName);
        if (
            loadedGame.CurrentTick != game.CurrentTick
            || loadedGame.Summary?.PlayerFactionID != playerFaction.InstanceID
        )
        {
            throw new InvalidOperationException(
                $"Saved simulation validation failed for '{options.SaveFileName}'."
            );
        }

        return saveManager.GetSaveFilePath(options.SaveFileName);
    }

    /// <summary>
    /// Creates a game builder for the requested scenario.
    /// </summary>
    /// <param name="summary">The game summary used for generation.</param>
    /// <param name="gameData">The active pack's composed game data.</param>
    /// <param name="seed">The optional generation seed.</param>
    /// <returns>The configured game builder.</returns>
    private static GameBuilder CreateGameBuilder(
        GameSummary summary,
        GameDataCatalog gameData,
        int? seed
    )
    {
        return seed.HasValue
            ? new GameBuilder(summary, gameData, new SystemRandomProvider(seed.Value))
            : new GameBuilder(summary, gameData);
    }

    /// <summary>
    /// Returns the log path for a simulation output file.
    /// </summary>
    /// <param name="outputPath">The simulation output path.</param>
    /// <returns>The log file path.</returns>
    private static string GetLogPath(string outputPath)
    {
        string resolvedOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(_logDirectory);
        return Path.Combine(
            _logDirectory,
            $"{Path.GetFileNameWithoutExtension(resolvedOutputPath)}.log"
        );
    }

    /// <summary>
    /// Appends a message to the simulation log file.
    /// </summary>
    /// <param name="logPath">The log file path.</param>
    /// <param name="message">The message to append.</param>
    private static void LogToFile(string logPath, string message)
    {
        File.AppendAllText(logPath, message + Environment.NewLine);
    }

    /// <summary>
    /// Builds the JSON summary for a completed simulation.
    /// </summary>
    /// <param name="game">The completed game state.</param>
    /// <param name="summary">The game generation summary.</param>
    /// <param name="options">The simulation options.</param>
    /// <param name="idleTracker">The manufacturing idle tracker.</param>
    /// <param name="manufacturedUnitTracker">The manufactured unit tracker.</param>
    /// <param name="fleetHistoryTracker">The fleet history tracker.</param>
    /// <param name="activityTracker">The strategic activity tracker.</param>
    /// <param name="personnelOutcomeTracker">The personnel outcome tracker.</param>
    /// <param name="planetaryAssaultTracker">The planetary-assault activity tracker.</param>
    /// <param name="attackReadinessTracker">The attack-readiness blocker tracker.</param>
    /// <param name="victory">The first victory reached during the simulation.</param>
    /// <returns>The simulation summary.</returns>
    private static SimulationSummary BuildSimulationSummary(
        GameRoot game,
        GameSummary summary,
        SimulationOptions options,
        ManufacturingIdleTracker idleTracker,
        ManufacturedUnitTracker manufacturedUnitTracker,
        FleetHistoryTracker fleetHistoryTracker,
        ActivityTracker activityTracker,
        PersonnelOutcomeTracker personnelOutcomeTracker,
        PlanetaryAssaultTracker planetaryAssaultTracker,
        AttackReadinessTracker attackReadinessTracker,
        VictoryResult victory
    )
    {
        return new SimulationSummary
        {
            TicksRequested = options.TickCount,
            TicksCompleted = game.CurrentTick,
            Seed = options.Seed ?? -1,
            GalaxySize = summary.GalaxySize.ToString(),
            OutputPath = options.OutputPath,
            AITickInterval = game.Config.AI.TickInterval,
            MinimumAttackStrength = game.Config.AI.FleetDeployment.MinimumAttackStrength,
            MinimumAttackRegimentCount = game.Config
                .AI
                .FleetDeployment
                .MinimumPlanetaryAssaultRegimentCount,
            Victory = BuildVictorySummary(victory),
            FleetHistory = fleetHistoryTracker.ToArray(),
            Factions = game.GetFactions()
                .Select(faction => new FactionSimulationSummary
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
                    OperationalPlanetCount = GetOperationalOwnedPlanets(game, faction).Count,
                    FleetCount = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                        faction.InstanceID
                    ).Count,
                    BuildingCount = game.GetSceneNodesByOwnerInstanceID<Building>(
                        faction.InstanceID
                    ).Count,
                    DefenseFacilityCount = GetOperationalOwnedPlanets(game, faction)
                        .Sum(planet => planet.GetBuildingTypeCount(BuildingType.Defense)),
                    WeaponFacilityCount = GetOperationalOwnedPlanets(game, faction)
                        .Sum(planet => planet.GetBuildingTypeCount(BuildingType.Weapon)),
                    ProjectedDefenseFacilityCount = GetOperationalOwnedPlanets(game, faction)
                        .Sum(planet => planet.GetTotalBuildingTypeCount(BuildingType.Defense)),
                    ProjectedWeaponFacilityCount = GetOperationalOwnedPlanets(game, faction)
                        .Sum(planet => planet.GetTotalBuildingTypeCount(BuildingType.Weapon)),
                    ShieldedPlanetCount = CountShieldedPlanets(game, faction),
                    FullyShieldedPlanetCount = CountFullyShieldedPlanets(game, faction),
                    WeaponDefendedPlanetCount = CountWeaponDefendedPlanets(game, faction),
                    FullyStaticDefendedPlanetCount = CountFullyStaticDefendedPlanets(game, faction),
                    ProjectedFullyShieldedPlanetCount = CountProjectedFullyShieldedPlanets(
                        game,
                        faction
                    ),
                    ProjectedWeaponDefendedPlanetCount = CountProjectedWeaponDefendedPlanets(
                        game,
                        faction
                    ),
                    ProjectedFullyStaticDefendedPlanetCount =
                        CountProjectedFullyStaticDefendedPlanets(game, faction),
                    AdvancedConstructionFacilityCount = CountAdvancedProductionFacilities(
                        game,
                        faction,
                        BuildingType.ConstructionFacility
                    ),
                    AdvancedShipyardCount = CountAdvancedProductionFacilities(
                        game,
                        faction,
                        BuildingType.Shipyard
                    ),
                    AdvancedTrainingFacilityCount = CountAdvancedProductionFacilities(
                        game,
                        faction,
                        BuildingType.TrainingFacility
                    ),
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
                    RawMaterialSupply = faction.RawMaterialSupply,
                    RefinedMaterialSupply = faction.RefinedMaterialSupply,
                    RawMaterialStockpile = faction.RawMaterialStockpile,
                    RefinedMaterialStockpile = faction.RefinedMaterialStockpile,
                    MaintenanceCapacity = faction.MaintenanceCapacity,
                    MaintenanceHeadroom = faction.MaintenanceHeadroom,
                    Economy = BuildEconomySummary(faction),
                    Energy = GetOperationalOwnedPlanets(game, faction)
                        .Sum(planet => planet.GetAvailableEnergy()),
                    UnitCost = faction.GetTotalMaintenanceCost(),
                    StarfighterCoverage = BuildStarfighterCoverageSummary(faction),
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
                    Activity = activityTracker.BuildSummary(faction),
                    PersonnelOutcomes = personnelOutcomeTracker.BuildSummary(faction.InstanceID),
                    PlanetaryAssaults = planetaryAssaultTracker.BuildSummary(faction.InstanceID),
                    AttackReadiness = attackReadinessTracker.BuildSummary(faction.InstanceID),
                    CurrentIdlePlanets = BuildCurrentIdlePlanetSummaries(game, faction),
                    Fleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(faction.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                        .Select(fleet => BuildFleetSummary(game, faction, fleet))
                        .ToArray(),
                })
                .Select(factionSummary => AddProductionPlanningSummary(game, factionSummary))
                .ToArray(),
        };
    }

    /// <summary>
    /// Builds the serialized victory summary for a completed simulation.
    /// </summary>
    /// <param name="victory">The first victory reached during the simulation.</param>
    /// <returns>The victory summary, or null when the simulation reaches its turn limit.</returns>
    private static VictorySimulationSummary BuildVictorySummary(VictoryResult victory)
    {
        if (victory == null)
            return null;

        return new VictorySimulationSummary
        {
            WinnerFactionId = victory.Winner?.InstanceID,
            Winner = victory.Winner?.GetDisplayName(),
            LoserFactionId = victory.Loser?.InstanceID,
            Loser = victory.Loser?.GetDisplayName(),
            Tick = victory.Tick,
            Mode = victory.GameMode?.ToString(),
        };
    }

    /// <summary>
    /// Evaluates the faction's current production plan and adds its counts to the summary.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="summary">The faction summary to enrich.</param>
    /// <returns>The enriched faction summary.</returns>
    private static FactionSimulationSummary AddProductionPlanningSummary(
        GameRoot game,
        FactionSimulationSummary summary
    )
    {
        Faction faction = game.GetFactionByOwnerInstanceID(summary.FactionId);
        FleetSystem fleetSystem = new FleetSystem(game);
        ManufacturingSystem manufacturing = new ManufacturingSystem(game, fleetSystem);
        AITurnContext context = new AITurnContext(
            game,
            faction,
            null,
            null,
            manufacturing,
            null,
            null,
            game.Random
        );
        List<AIDemand> demands = new AIProductionDemandGenerator().Generate(context);
        List<AIManufactureProposal> proposals = new AIProductionPlanner()
            .Plan(context)
            .OfType<AIManufactureProposal>()
            .ToList();
        context.AddProposals(proposals);
        new AIScoringPhase().Execute(context);
        List<AIManufactureProposal> selected = new AISelectionPhase()
            .Select(context)
            .OfType<AIManufactureProposal>()
            .ToList();

        summary.ProductionDemandCount = demands.Count;
        summary.ProductionProposalCount = proposals.Count;
        summary.SelectedProductionProposalCount = selected.Count;
        summary.PlanetaryDefenseDemandCount = demands.Count(demand =>
            demand.Kind == AIDemandKind.PlanetaryDefense
        );
        summary.PlanetaryDefenseDemandQuantity = demands
            .Where(demand => demand.Kind == AIDemandKind.PlanetaryDefense)
            .Sum(demand => demand.QuantityNeeded);
        summary.PlanetaryDefenseProposalCount = proposals.Count(proposal =>
            proposal.Demand.Kind == AIDemandKind.PlanetaryDefense
        );
        summary.SelectedPlanetaryDefenseProposalCount = selected.Count(proposal =>
            proposal.Demand.Kind == AIDemandKind.PlanetaryDefense
        );
        summary.GarrisonDemandCount = demands.Count(demand =>
            demand.Kind == AIDemandKind.GarrisonRegimentReserve
        );
        summary.GarrisonProposalCount = proposals.Count(proposal =>
            proposal.Demand.Kind == AIDemandKind.GarrisonRegimentReserve
        );
        summary.SelectedGarrisonProposalCount = selected.Count(proposal =>
            proposal.Demand.Kind == AIDemandKind.GarrisonRegimentReserve
        );
        summary.BuildingProductionProposalCount = proposals.Count(proposal =>
            proposal.Demand.ManufacturingType == ManufacturingType.Building
        );
        summary.SelectedBuildingProductionProposalCount = selected.Count(proposal =>
            proposal.Demand.ManufacturingType == ManufacturingType.Building
        );
        summary.SelectedProductionMaintenanceCost = selected.Sum(proposal =>
            proposal.GetMaintenanceCost()
        );
        return summary;
    }

    /// <summary>
    /// Counts operational faction planets with at least one completed shield generator.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The number of shielded planets.</returns>
    private static int CountShieldedPlanets(GameRoot game, Faction faction)
    {
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet => GetActiveOwnedBuildings(planet, faction).Any(IsShieldGenerator));
    }

    /// <summary>
    /// Counts operational faction planets that meet the configured shield target.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The number of fully shielded planets.</returns>
    private static int CountFullyShieldedPlanets(GameRoot game, Faction faction)
    {
        int target = game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet =>
                GetActiveOwnedBuildings(planet, faction).Count(IsShieldGenerator) >= target
            );
    }

    /// <summary>
    /// Counts operational faction planets that meet the configured weapon target.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The number of weapon-defended planets.</returns>
    private static int CountWeaponDefendedPlanets(GameRoot game, Faction faction)
    {
        int target = game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount;
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet =>
                GetActiveOwnedBuildings(planet, faction)
                    .Count(building => building.GetBuildingType() == BuildingType.Weapon) >= target
            );
    }

    /// <summary>
    /// Counts operational faction planets that meet both static-defense targets.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The number of fully defended planets.</returns>
    private static int CountFullyStaticDefendedPlanets(GameRoot game, Faction faction)
    {
        int shieldTarget = game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
        int weaponTarget = game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount;
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet =>
            {
                List<Building> buildings = GetActiveOwnedBuildings(planet, faction);
                return buildings.Count(IsShieldGenerator) >= shieldTarget
                    && buildings.Count(building =>
                        building.GetBuildingType() == BuildingType.Weapon
                    ) >= weaponTarget;
            });
    }

    /// <summary>
    /// Counts completed researched production facilities of the requested type.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose facilities are counted.</param>
    /// <param name="buildingType">The production facility type.</param>
    /// <returns>The number of advanced production facilities.</returns>
    private static int CountAdvancedProductionFacilities(
        GameRoot game,
        Faction faction,
        BuildingType buildingType
    )
    {
        return game.GetSceneNodesByOwnerInstanceID<Building>(faction.InstanceID)
            .Count(building =>
                building.GetBuildingType() == buildingType
                && building.ResearchOrder > 0
                && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                && building.Movement == null
            );
    }

    /// <summary>
    /// Counts faction planets projected to meet the configured shield target.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The projected number of fully shielded planets.</returns>
    private static int CountProjectedFullyShieldedPlanets(GameRoot game, Faction faction)
    {
        int target = game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet => GetOwnedBuildings(planet, faction).Count(IsShieldGenerator) >= target);
    }

    /// <summary>
    /// Counts faction planets projected to meet the configured weapon target.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The projected number of weapon-defended planets.</returns>
    private static int CountProjectedWeaponDefendedPlanets(GameRoot game, Faction faction)
    {
        int target = game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount;
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet =>
                GetOwnedBuildings(planet, faction)
                    .Count(building => building.GetBuildingType() == BuildingType.Weapon) >= target
            );
    }

    /// <summary>
    /// Counts faction planets projected to meet both static-defense targets.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose planets are counted.</param>
    /// <returns>The projected number of fully defended planets.</returns>
    private static int CountProjectedFullyStaticDefendedPlanets(GameRoot game, Faction faction)
    {
        int shieldTarget = game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
        int weaponTarget = game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount;
        return GetOperationalOwnedPlanets(game, faction)
            .Count(planet =>
            {
                List<Building> buildings = GetOwnedBuildings(planet, faction);
                return buildings.Count(IsShieldGenerator) >= shieldTarget
                    && buildings.Count(building =>
                        building.GetBuildingType() == BuildingType.Weapon
                    ) >= weaponTarget;
            });
    }

    /// <summary>
    /// Gets every planet owned by the specified faction.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The owning faction.</param>
    /// <returns>The owned planets.</returns>
    private static List<Planet> GetOwnedPlanets(GameRoot game, Faction faction)
    {
        return game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID);
    }

    /// <summary>
    /// Gets the faction's colonized, undestroyed planets.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The owning faction.</param>
    /// <returns>The operational owned planets.</returns>
    private static List<Planet> GetOperationalOwnedPlanets(GameRoot game, Faction faction)
    {
        return GetOwnedPlanets(game, faction)
            .Where(planet => planet.IsColonized && !planet.IsDestroyed)
            .ToList();
    }

    /// <summary>
    /// Gets completed, stationary faction buildings at a planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <param name="faction">The owning faction.</param>
    /// <returns>The active owned buildings.</returns>
    private static List<Building> GetActiveOwnedBuildings(Planet planet, Faction faction)
    {
        return GetOwnedBuildings(planet, faction)
            .Where(building =>
                building.GetManufacturingStatus() == ManufacturingStatus.Complete
                && building.Movement == null
            )
            .ToList();
    }

    /// <summary>
    /// Gets every faction building at a planet, including projected construction.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <param name="faction">The owning faction.</param>
    /// <returns>The owned buildings.</returns>
    private static List<Building> GetOwnedBuildings(Planet planet, Faction faction)
    {
        return planet
            .GetAllBuildings()
            .Where(building => building.GetOwnerInstanceID() == faction.InstanceID)
            .ToList();
    }

    /// <summary>
    /// Determines whether a building is a planetary shield generator.
    /// </summary>
    /// <param name="building">The building to inspect.</param>
    /// <returns>True when the building generates planetary shields.</returns>
    private static bool IsShieldGenerator(Building building)
    {
        return building.DefenseFacilityClass == DefenseFacilityClass.Shield;
    }

    /// <summary>
    /// Builds the capital ship production summary for a faction.
    /// </summary>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The capital ship production summary.</returns>
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

    /// <summary>
    /// Builds the planetary starfighter coverage summary for a faction.
    /// </summary>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The starfighter coverage summary.</returns>
    private static StarfighterCoverageSimulationSummary BuildStarfighterCoverageSummary(
        Faction faction
    )
    {
        if (faction == null)
            return null;

        List<Planet> usablePlanets = faction
            .GetOwnedUnitsByType<Planet>()
            .Where(planet => planet.IsColonized && !planet.IsDestroyed)
            .ToList();
        int coveredPlanetCount = usablePlanets.Count(planet =>
            planet
                .GetAllStarfighters()
                .Any(starfighter => starfighter.GetOwnerInstanceID() == faction.InstanceID)
        );

        return new StarfighterCoverageSimulationSummary
        {
            OwnedUsablePlanetCount = usablePlanets.Count,
            CoveredPlanetCount = coveredPlanetCount,
            UncoveredPlanetCount = usablePlanets.Count - coveredPlanetCount,
        };
    }

    /// <summary>
    /// Builds the economy summary for a faction.
    /// </summary>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The economy summary.</returns>
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

    /// <summary>
    /// Counts queued buildings of a type on a planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <param name="factionId">The faction owner ID.</param>
    /// <param name="type">The building type to count.</param>
    /// <returns>The queued building count.</returns>
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

    /// <summary>
    /// Counts existing and queued buildings of a type on a planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <param name="factionId">The faction owner ID.</param>
    /// <param name="type">The building type to count.</param>
    /// <returns>The projected building count.</returns>
    private static int CountProjectedBuildings(Planet planet, string factionId, BuildingType type)
    {
        return planet
            .GetAllBuildings()
            .Count(building =>
                building.GetBuildingType() == type && building.GetOwnerInstanceID() == factionId
            );
    }

    /// <summary>
    /// Writes a simulation summary file.
    /// </summary>
    /// <param name="outputPath">The requested output path.</param>
    /// <param name="report">The simulation report to write.</param>
    /// <returns>The resolved output path.</returns>
    private static string WriteSimulationSummary(string outputPath, SimulationSummary report)
    {
        string resolvedPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(resolvedPath, UnityEngine.JsonUtility.ToJson(report, true));
        return resolvedPath;
    }

    /// <summary>
    /// Builds the troop production summary for a faction.
    /// </summary>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The troop production summary.</returns>
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

    /// <summary>
    /// Counts owned planets with completed production facilities of a type.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <param name="type">The manufacturing type to count.</param>
    /// <returns>The number of owned facility planets.</returns>
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

    /// <summary>
    /// Counts owned planets with available manufacturing capacity.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <param name="type">The manufacturing type to count.</param>
    /// <returns>The number of available manufacturing planets.</returns>
    private static int CountAvailableManufacturingPlanets(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Count(planet => planet.GetAvailableManufacturingCapacity(type) > 0)
            ?? 0;
    }

    /// <summary>
    /// Counts available manufacturing slots for a faction.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <param name="type">The manufacturing type to count.</param>
    /// <returns>The number of available manufacturing slots.</returns>
    private static int CountAvailableManufacturingSlots(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Sum(planet => planet.GetAvailableManufacturingCapacity(type))
            ?? 0;
    }

    /// <summary>
    /// Counts queued manufacturing items for a faction.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <param name="type">The manufacturing type to count.</param>
    /// <returns>The number of queued manufacturing items.</returns>
    private static int CountManufacturingQueueItems(Faction faction, ManufacturingType type)
    {
        return faction
                ?.GetOwnedUnitsByType<Planet>()
                .Sum(planet => GetManufacturingQueueCount(planet, type))
            ?? 0;
    }

    /// <summary>
    /// Counts completed planet-based starfighters that are not moving.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <returns>The idle starfighter count.</returns>
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

    /// <summary>
    /// Counts open starfighter capacity across owned fleets.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <returns>The total free starfighter capacity.</returns>
    private static int CountOwnedFleetFreeStarfighterCapacity(Faction faction)
    {
        return faction
                ?.GetOwnedUnitsByType<Fleet>()
                .Where(fleet => fleet != null && fleet.GetOwnerInstanceID() == faction.InstanceID)
                .Sum(fleet => Math.Max(0, fleet.GetExcessStarfighterCapacity()))
            ?? 0;
    }

    /// <summary>
    /// Counts unlocked capital ship technologies.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <returns>The unlocked capital ship technology count.</returns>
    private static int CountUnlockedCapitalTechnologies(Faction faction)
    {
        return faction
                ?.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Count(technology => technology.GetReference() is CapitalShip)
            ?? 0;
    }

    /// <summary>
    /// Counts unlocked capital ship technologies that can support fleet infrastructure.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <returns>The unlocked infrastructure capital ship technology count.</returns>
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

    /// <summary>
    /// Counts queued manufacturing items on a planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <param name="type">The manufacturing type to count.</param>
    /// <returns>The queued item count.</returns>
    private static int GetManufacturingQueueCount(Planet planet, ManufacturingType type)
    {
        if (planet == null)
            return 0;

        return planet.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue)
            ? queue.Count
            : 0;
    }

    /// <summary>
    /// Counts capital ships currently under construction.
    /// </summary>
    /// <param name="faction">The faction to inspect.</param>
    /// <returns>The active capital ship manufacturing count.</returns>
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

    /// <summary>
    /// Builds the construction facility expansion summary for a faction.
    /// </summary>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The construction facility expansion summary.</returns>
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
            LargestSectorConstructionFacilityCount = GetLargestSectorConstructionFacilityCount(
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
            LargestSectorConstructionFacilityShare = GetShare(
                GetLargestSectorConstructionFacilityCount(ownedPlanets),
                projectedConstructionFacilities
            ),
        };
    }

    /// <summary>
    /// Returns the largest number of construction facilities in one sector.
    /// </summary>
    /// <param name="planets">The planets to inspect.</param>
    /// <returns>The largest sector construction facility count.</returns>
    private static int GetLargestSectorConstructionFacilityCount(List<Planet> planets)
    {
        return planets
            .GroupBy(planet =>
                planet.GetParentOfType<PlanetSector>()?.InstanceID ?? planet.InstanceID
            )
            .Select(group =>
                group.Sum(planet =>
                    planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
                )
            )
            .DefaultIfEmpty()
            .Max();
    }

    /// <summary>
    /// Returns a value as a share of a total.
    /// </summary>
    /// <param name="value">The numerator.</param>
    /// <param name="total">The denominator.</param>
    /// <returns>The share, or 0 if the total is not positive.</returns>
    private static double GetShare(int value, int total)
    {
        if (total <= 0)
            return 0;

        return (double)value / total;
    }

    /// <summary>
    /// Builds the troop reinforcement package summary for a faction.
    /// </summary>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The troop reinforcement package summary.</returns>
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
        public string SavePath;
    }

    [Serializable]
    private sealed class SimulationSummary
    {
        public int TicksRequested;
        public int TicksCompleted;
        public int Seed = -1;
        public string GalaxySize;
        public string OutputPath;
        public int AITickInterval;
        public int MinimumAttackStrength;
        public int MinimumAttackRegimentCount;
        public VictorySimulationSummary Victory;
        public FleetHistorySnapshot[] FleetHistory;
        public FactionSimulationSummary[] Factions;
    }

    [Serializable]
    private sealed class VictorySimulationSummary
    {
        public string WinnerFactionId;
        public string Winner;
        public string LoserFactionId;
        public string Loser;
        public int Tick;
        public string Mode;
    }

    [Serializable]
    private sealed class FactionSimulationSummary
    {
        public string[] OwnedPlanets;
        public string FactionId;
        public string DisplayName;
        public int PlanetCount;
        public int OperationalPlanetCount;
        public int FleetCount;
        public int BuildingCount;
        public int DefenseFacilityCount;
        public int WeaponFacilityCount;
        public int ProjectedDefenseFacilityCount;
        public int ProjectedWeaponFacilityCount;
        public int ShieldedPlanetCount;
        public int FullyShieldedPlanetCount;
        public int WeaponDefendedPlanetCount;
        public int FullyStaticDefendedPlanetCount;
        public int ProjectedFullyShieldedPlanetCount;
        public int ProjectedWeaponDefendedPlanetCount;
        public int ProjectedFullyStaticDefendedPlanetCount;
        public int AdvancedConstructionFacilityCount;
        public int AdvancedShipyardCount;
        public int AdvancedTrainingFacilityCount;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int SpecialForcesCount;
        public int OfficerCount;
        public int UnlockedSpecialForcesTechCount;
        public int RawMaterialSupply;
        public int RefinedMaterialSupply;
        public int RawMaterialStockpile;
        public int RefinedMaterialStockpile;
        public int MaintenanceCapacity;
        public int MaintenanceHeadroom;
        public EconomySimulationSummary Economy;
        public int Energy;
        public int UnitCost;
        public StarfighterCoverageSimulationSummary StarfighterCoverage;
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
        public int ProductionDemandCount;
        public int ProductionProposalCount;
        public int SelectedProductionProposalCount;
        public int PlanetaryDefenseDemandCount;
        public int PlanetaryDefenseDemandQuantity;
        public int PlanetaryDefenseProposalCount;
        public int SelectedPlanetaryDefenseProposalCount;
        public int GarrisonDemandCount;
        public int GarrisonProposalCount;
        public int SelectedGarrisonProposalCount;
        public int BuildingProductionProposalCount;
        public int SelectedBuildingProductionProposalCount;
        public int SelectedProductionMaintenanceCost;
        public ConstructionFacilityExpansionSimulationSummary ConstructionFacilityExpansion;
        public TroopProductionSimulationSummary TroopProduction;
        public TroopReinforcementPackageSimulationSummary TroopReinforcementPackages;
        public CapitalShipProductionSimulationSummary CapitalShipProduction;
        public ManufacturingIdleSummary ManufacturingIdle;
        public FactionActivitySummary Activity;
        public PersonnelOutcomeSimulationSummary PersonnelOutcomes;
        public PlanetaryAssaultSimulationSummary PlanetaryAssaults;
        public AttackReadinessSimulationSummary AttackReadiness;
        public CurrentIdlePlanetSummary[] CurrentIdlePlanets;
        public FleetSimulationSummary[] Fleets;
    }

    [Serializable]
    private sealed class PersonnelOutcomeSimulationSummary
    {
        public int Captures;
        public int Releases;
        public int Killed;
        public int CurrentlyCaptured;
    }

    [Serializable]
    private sealed class AttackReadinessSimulationSummary
    {
        public int BuildingFleetSamples;
        public AttackReadinessBlockerSummary[] Blockers;
    }

    [Serializable]
    private sealed class AttackReadinessBlockerSummary
    {
        public string Blocker;
        public int Samples;
        public int SoleBlockerSamples;
    }

    [Serializable]
    private sealed class PlanetaryAssaultSimulationSummary
    {
        public int Attempted;
        public int Succeeded;
        public int Failed;
        public PlanetaryAssaultSimulationResult[] Results;
    }

    [Serializable]
    private sealed class PlanetaryAssaultSimulationResult
    {
        public int Tick;
        public string PlanetId;
        public string PlanetName;
        public bool Success;
        public int InitialAttackerRegimentCount;
        public int RemainingAttackerRegimentCount;
        public int InitialDefenderRegimentCount;
        public int RemainingDefenderRegimentCount;
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
        public int LargestSectorConstructionFacilityCount;
        public double LargestPlanetConstructionFacilityShare;
        public double LargestSectorConstructionFacilityShare;
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
    private sealed class StarfighterCoverageSimulationSummary
    {
        public int OwnedUsablePlanetCount;
        public int CoveredPlanetCount;
        public int UncoveredPlanetCount;
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

    [Serializable]
    private sealed class FactionActivitySummary
    {
        public MissionActivitySummary[] Missions;
        public MissionTargetActivitySummary[] MissionTargets;
        public SabotageTargetActivitySummary[] SabotageTargets;
        public int PlanetsAcquired;
        public int PlanetsLost;
        public int PlanetsColonized;
        public int ShipResearchAdvances;
        public int FacilityResearchAdvances;
        public int TroopResearchAdvances;
        public int FinalShipResearchOrder;
        public int FinalFacilityResearchOrder;
        public int FinalTroopResearchOrder;
    }

    [Serializable]
    private sealed class MissionActivitySummary
    {
        public string MissionTypeId;
        public int Started;
        public int Ended;
        public int Active;
    }

    [Serializable]
    private sealed class MissionTargetActivitySummary
    {
        public string MissionTypeId;
        public string PlanetId;
        public int Started;
        public int Ended;
        public int Active;
        public int IntelRefreshes;
        public int EarlyInterruptions;
        public int ArrivalInterruptions;
        public int MinimumMainRating;
        public int MaximumMainRating;
        public double AverageMainRating;
    }

    [Serializable]
    private sealed class SabotageTargetActivitySummary
    {
        public string TargetId;
        public string PlanetId;
        public string TargetType;
        public int Started;
        public int Ended;
        public int Destroyed;
    }

    private sealed class PersonnelOutcomeTracker
    {
        private readonly Dictionary<string, Officer> _knownOfficers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TrackedOfficer> _officers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonnelOutcomeCounts> _outcomes = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records the personnel state before simulation ticks are processed.
        /// </summary>
        /// <param name="game">The initial game state.</param>
        public void RecordInitialState(GameRoot game)
        {
            _knownOfficers.Clear();
            _officers.Clear();
            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
            {
                _knownOfficers[officer.InstanceID] = officer;
                _officers[officer.InstanceID] = TrackedOfficer.From(officer);
            }
        }

        /// <summary>
        /// Records capture transitions, releases, and current officer state.
        /// </summary>
        /// <param name="game">The current game state.</param>
        public void RecordTick(GameRoot game)
        {
            Dictionary<string, Officer> currentOfficers = game.GetSceneNodesByType<Officer>()
                .ToDictionary(officer => officer.InstanceID, StringComparer.Ordinal);
            foreach (Officer officer in currentOfficers.Values)
                _knownOfficers[officer.InstanceID] = officer;

            foreach (TrackedOfficer previous in _officers.Values)
            {
                PersonnelOutcomeCounts counts = GetCounts(previous.OwnerInstanceID);
                if (!currentOfficers.TryGetValue(previous.InstanceID, out Officer current))
                    continue;

                if (!previous.IsCaptured && current.IsCaptured)
                    counts.Captures++;
                else if (previous.IsCaptured && !current.IsCaptured)
                    counts.Releases++;
            }

            _officers.Clear();
            foreach (Officer officer in currentOfficers.Values)
                _officers[officer.InstanceID] = TrackedOfficer.From(officer);
        }

        /// <summary>
        /// Builds the personnel outcome summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The recorded personnel outcomes.</returns>
        public PersonnelOutcomeSimulationSummary BuildSummary(string factionId)
        {
            PersonnelOutcomeCounts counts = GetCounts(factionId);
            return new PersonnelOutcomeSimulationSummary
            {
                Captures = counts.Captures,
                Releases = counts.Releases,
                Killed = _knownOfficers.Values.Count(officer =>
                    officer.OwnerInstanceID == factionId && officer.IsKilled
                ),
                CurrentlyCaptured = _officers.Values.Count(officer =>
                    officer.OwnerInstanceID == factionId && officer.IsCaptured
                ),
            };
        }

        /// <summary>
        /// Gets the mutable outcome counters for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction's outcome counters.</returns>
        private PersonnelOutcomeCounts GetCounts(string factionId)
        {
            factionId ??= string.Empty;
            if (!_outcomes.TryGetValue(factionId, out PersonnelOutcomeCounts counts))
            {
                counts = new PersonnelOutcomeCounts();
                _outcomes[factionId] = counts;
            }

            return counts;
        }

        private sealed class PersonnelOutcomeCounts
        {
            public int Captures;
            public int Releases;
        }

        private sealed class TrackedOfficer
        {
            public string InstanceID;
            public string OwnerInstanceID;
            public bool IsCaptured;
            public bool IsKilled;

            /// <summary>
            /// Captures the state needed to compare an officer across simulation ticks.
            /// </summary>
            /// <param name="officer">The officer to record.</param>
            /// <returns>The tracked officer state.</returns>
            public static TrackedOfficer From(Officer officer)
            {
                return new TrackedOfficer
                {
                    InstanceID = officer.InstanceID,
                    OwnerInstanceID = officer.OwnerInstanceID,
                    IsCaptured = officer.IsCaptured,
                    IsKilled = officer.IsKilled,
                };
            }
        }
    }

    private sealed class ActivityTracker
    {
        private readonly Dictionary<string, TrackedMission> _activeMissions = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, PlanetState> _planetStates = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, FactionActivity> _factionActivities = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, ResearchOrders> _researchOrders = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records the missions, ownership, and research state at simulation start.
        /// </summary>
        /// <param name="game">The initial game state.</param>
        public void RecordInitialState(GameRoot game)
        {
            foreach (Mission mission in game.GetSceneNodesByType<Mission>())
            {
                TrackedMission trackedMission = TrackedMission.From(mission, game);
                trackedMission.InitialIntelTick = GetIntelTick(
                    game,
                    trackedMission.FactionId,
                    trackedMission.PlanetId
                );
                _activeMissions[mission.InstanceID] = trackedMission;
            }

            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
                _planetStates[planet.InstanceID] = PlanetState.From(planet);

            foreach (Faction faction in game.GetFactions())
                _researchOrders[faction.InstanceID] = ResearchOrders.From(faction);
        }

        /// <summary>
        /// Records mission, ownership, and research changes for one simulation tick.
        /// </summary>
        /// <param name="game">The current game state.</param>
        public void RecordTick(GameRoot game)
        {
            RecordMissions(game);
            RecordPlanetOwnership(game);
            RecordResearch(game);
        }

        /// <summary>
        /// Builds the recorded strategic activity summary for one faction.
        /// </summary>
        /// <param name="faction">The faction to summarize.</param>
        /// <returns>The faction activity summary.</returns>
        public FactionActivitySummary BuildSummary(Faction faction)
        {
            FactionActivity activity = GetActivity(faction.InstanceID);
            ResearchOrders orders = ResearchOrders.From(faction);
            return new FactionActivitySummary
            {
                Missions = activity
                    .MissionCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new MissionActivitySummary
                    {
                        MissionTypeId = pair.Key,
                        Started = pair.Value.Started,
                        Ended = pair.Value.Ended,
                        Active = _activeMissions.Values.Count(mission =>
                            mission.FactionId == faction.InstanceID
                            && mission.MissionTypeId == pair.Key
                        ),
                    })
                    .ToArray(),
                MissionTargets = activity
                    .MissionTargetCounts.Values.OrderBy(counts => counts.MissionTypeId)
                    .ThenBy(counts => counts.PlanetId)
                    .Select(counts => new MissionTargetActivitySummary
                    {
                        MissionTypeId = counts.MissionTypeId,
                        PlanetId = counts.PlanetId,
                        Started = counts.Started,
                        Ended = counts.Ended,
                        IntelRefreshes = counts.IntelRefreshes,
                        EarlyInterruptions = counts.EarlyInterruptions,
                        ArrivalInterruptions = counts.ArrivalInterruptions,
                        MinimumMainRating =
                            counts.MainRatingSamples > 0 ? counts.MinimumMainRating : 0,
                        MaximumMainRating = counts.MaximumMainRating,
                        AverageMainRating =
                            counts.MainRatingSamples > 0
                                ? (double)counts.MainRatingTotal / counts.MainRatingSamples
                                : 0,
                        Active = _activeMissions.Values.Count(mission =>
                            mission.FactionId == faction.InstanceID
                            && mission.MissionTypeId == counts.MissionTypeId
                            && mission.PlanetId == counts.PlanetId
                        ),
                    })
                    .ToArray(),
                SabotageTargets = activity
                    .SabotageTargetCounts.Values.OrderBy(counts => counts.PlanetId)
                    .ThenBy(counts => counts.TargetId)
                    .Select(counts => new SabotageTargetActivitySummary
                    {
                        TargetId = counts.TargetId,
                        PlanetId = counts.PlanetId,
                        TargetType = counts.TargetType,
                        Started = counts.Started,
                        Ended = counts.Ended,
                        Destroyed = counts.Destroyed,
                    })
                    .ToArray(),
                PlanetsAcquired = activity.PlanetsAcquired,
                PlanetsLost = activity.PlanetsLost,
                PlanetsColonized = activity.PlanetsColonized,
                ShipResearchAdvances = activity.ShipResearchAdvances,
                FacilityResearchAdvances = activity.FacilityResearchAdvances,
                TroopResearchAdvances = activity.TroopResearchAdvances,
                FinalShipResearchOrder = orders.Ship,
                FinalFacilityResearchOrder = orders.Facility,
                FinalTroopResearchOrder = orders.Troop,
            };
        }

        /// <summary>
        /// Records mission starts, completions, targets, and observed outcomes.
        /// </summary>
        /// <param name="game">The current game state.</param>
        private void RecordMissions(GameRoot game)
        {
            Dictionary<string, TrackedMission> currentMissions = game.GetSceneNodesByType<Mission>()
                .ToDictionary(
                    mission => mission.InstanceID,
                    mission => TrackedMission.From(mission, game)
                );

            foreach (TrackedMission mission in currentMissions.Values)
            {
                if (
                    _activeMissions.TryGetValue(
                        mission.InstanceId,
                        out TrackedMission previousMission
                    )
                )
                    mission.InitialIntelTick = previousMission.InitialIntelTick;
                else
                    mission.InitialIntelTick = GetIntelTick(
                        game,
                        mission.FactionId,
                        mission.PlanetId
                    );
            }

            foreach (
                TrackedMission mission in currentMissions.Values.Where(mission =>
                    !_activeMissions.ContainsKey(mission.InstanceId)
                )
            )
            {
                GetMissionCounts(mission).Started++;
                MissionTargetCounts targetCounts = GetMissionTargetCounts(mission);
                targetCounts.Started++;
                targetCounts.MainRatingTotal += mission.MainRating;
                targetCounts.MainRatingSamples++;
                targetCounts.MinimumMainRating = Math.Min(
                    targetCounts.MinimumMainRating,
                    mission.MainRating
                );
                targetCounts.MaximumMainRating = Math.Max(
                    targetCounts.MaximumMainRating,
                    mission.MainRating
                );
                SabotageTargetCounts sabotageCounts = GetSabotageTargetCounts(mission);
                if (sabotageCounts != null)
                    sabotageCounts.Started++;
            }

            foreach (
                TrackedMission mission in _activeMissions.Values.Where(mission =>
                    !currentMissions.ContainsKey(mission.InstanceId)
                )
            )
            {
                GetMissionCounts(mission).Ended++;
                MissionTargetCounts targetCounts = GetMissionTargetCounts(mission);
                targetCounts.Ended++;
                RecordEspionageOutcome(game, mission, targetCounts);
                SabotageTargetCounts sabotageCounts = GetSabotageTargetCounts(mission);
                if (sabotageCounts == null)
                    continue;

                sabotageCounts.Ended++;
                if (game.GetSceneNodeByInstanceID<ISceneNode>(mission.TargetId) == null)
                    sabotageCounts.Destroyed++;
            }

            _activeMissions.Clear();
            foreach (KeyValuePair<string, TrackedMission> pair in currentMissions)
                _activeMissions[pair.Key] = pair.Value;
        }

        /// <summary>
        /// Classifies the observed outcome of a completed espionage mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="mission">The completed mission state.</param>
        /// <param name="counts">The target counters to update.</param>
        private void RecordEspionageOutcome(
            GameRoot game,
            TrackedMission mission,
            MissionTargetCounts counts
        )
        {
            if (mission.MissionTypeId != MissionTypeIDs.Espionage)
                return;

            if (GetIntelTick(game, mission.FactionId, mission.PlanetId) > mission.InitialIntelTick)
            {
                counts.IntelRefreshes++;
                return;
            }

            if (mission.WaitingForParticipants)
            {
                counts.ArrivalInterruptions++;
                return;
            }

            if (mission.CurrentProgress + 1 < mission.MaxProgress)
                counts.EarlyInterruptions++;
        }

        /// <summary>
        /// Gets the tick of the faction's latest intelligence snapshot for a planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="factionId">The observing faction instance identifier.</param>
        /// <param name="planetId">The observed planet instance identifier.</param>
        /// <returns>The snapshot tick, or -1 when no snapshot exists.</returns>
        private static int GetIntelTick(GameRoot game, string factionId, string planetId)
        {
            Faction faction = game.GetFactionByOwnerInstanceID(factionId);
            if (
                faction?.Fog?.PlanetToSector == null
                || !faction.Fog.PlanetToSector.TryGetValue(planetId, out string systemId)
                || !faction.Fog.Snapshots.TryGetValue(
                    systemId,
                    out PlanetSectorSnapshot systemSnapshot
                )
                || !systemSnapshot.Planets.TryGetValue(planetId, out PlanetSnapshot planetSnapshot)
            )
                return -1;

            return planetSnapshot.TickCaptured;
        }

        /// <summary>
        /// Records planet acquisition, loss, and colonization transitions.
        /// </summary>
        /// <param name="game">The current game state.</param>
        private void RecordPlanetOwnership(GameRoot game)
        {
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
            {
                PlanetState current = PlanetState.From(planet);
                if (_planetStates.TryGetValue(planet.InstanceID, out PlanetState previous))
                {
                    if (previous.OwnerId != current.OwnerId)
                    {
                        if (!string.IsNullOrEmpty(previous.OwnerId))
                            GetActivity(previous.OwnerId).PlanetsLost++;

                        if (!string.IsNullOrEmpty(current.OwnerId))
                            GetActivity(current.OwnerId).PlanetsAcquired++;
                    }

                    if (
                        !previous.IsColonized
                        && current.IsColonized
                        && !string.IsNullOrEmpty(current.OwnerId)
                    )
                        GetActivity(current.OwnerId).PlanetsColonized++;
                }

                _planetStates[planet.InstanceID] = current;
            }
        }

        /// <summary>
        /// Records research-order advances for every faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        private void RecordResearch(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
            {
                ResearchOrders current = ResearchOrders.From(faction);
                if (_researchOrders.TryGetValue(faction.InstanceID, out ResearchOrders previous))
                {
                    FactionActivity activity = GetActivity(faction.InstanceID);
                    activity.ShipResearchAdvances += Math.Max(0, current.Ship - previous.Ship);
                    activity.FacilityResearchAdvances += Math.Max(
                        0,
                        current.Facility - previous.Facility
                    );
                    activity.TroopResearchAdvances += Math.Max(0, current.Troop - previous.Troop);
                }

                _researchOrders[faction.InstanceID] = current;
            }
        }

        /// <summary>
        /// Gets the aggregate counters for a tracked mission type.
        /// </summary>
        /// <param name="mission">The tracked mission.</param>
        /// <returns>The mission-type counters.</returns>
        private MissionCounts GetMissionCounts(TrackedMission mission)
        {
            FactionActivity activity = GetActivity(mission.FactionId);
            if (
                !activity.MissionCounts.TryGetValue(mission.MissionTypeId, out MissionCounts counts)
            )
            {
                counts = new MissionCounts();
                activity.MissionCounts[mission.MissionTypeId] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets or creates the mutable activity counters for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction activity counters.</returns>
        private FactionActivity GetActivity(string factionId)
        {
            string key = factionId ?? string.Empty;
            if (!_factionActivities.TryGetValue(key, out FactionActivity activity))
            {
                activity = new FactionActivity();
                _factionActivities[key] = activity;
            }

            return activity;
        }

        /// <summary>
        /// Gets or creates counters for a mission type and target planet.
        /// </summary>
        /// <param name="mission">The tracked mission.</param>
        /// <returns>The mission-target counters.</returns>
        private MissionTargetCounts GetMissionTargetCounts(TrackedMission mission)
        {
            FactionActivity activity = GetActivity(mission.FactionId);
            string key = $"{mission.MissionTypeId}\0{mission.PlanetId}";
            if (!activity.MissionTargetCounts.TryGetValue(key, out MissionTargetCounts counts))
            {
                counts = new MissionTargetCounts
                {
                    MissionTypeId = mission.MissionTypeId,
                    PlanetId = mission.PlanetId,
                };
                activity.MissionTargetCounts[key] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets or creates counters for a sabotage target.
        /// </summary>
        /// <param name="mission">The tracked sabotage mission.</param>
        /// <returns>The sabotage-target counters, or null when the mission has no target.</returns>
        private SabotageTargetCounts GetSabotageTargetCounts(TrackedMission mission)
        {
            if (string.IsNullOrEmpty(mission.TargetId))
                return null;

            FactionActivity activity = GetActivity(mission.FactionId);
            if (
                !activity.SabotageTargetCounts.TryGetValue(
                    mission.TargetId,
                    out SabotageTargetCounts counts
                )
            )
            {
                counts = new SabotageTargetCounts
                {
                    TargetId = mission.TargetId,
                    PlanetId = mission.PlanetId,
                    TargetType = mission.TargetType,
                };
                activity.SabotageTargetCounts[mission.TargetId] = counts;
            }

            return counts;
        }

        private sealed class FactionActivity
        {
            public Dictionary<string, MissionCounts> MissionCounts { get; } =
                new(StringComparer.Ordinal);
            public Dictionary<string, MissionTargetCounts> MissionTargetCounts { get; } =
                new(StringComparer.Ordinal);
            public Dictionary<string, SabotageTargetCounts> SabotageTargetCounts { get; } =
                new(StringComparer.Ordinal);
            public int PlanetsAcquired;
            public int PlanetsLost;
            public int PlanetsColonized;
            public int ShipResearchAdvances;
            public int FacilityResearchAdvances;
            public int TroopResearchAdvances;
        }

        private sealed class MissionCounts
        {
            public int Started;
            public int Ended;
        }

        private sealed class MissionTargetCounts
        {
            public string MissionTypeId;
            public string PlanetId;
            public int Started;
            public int Ended;
            public int IntelRefreshes;
            public int EarlyInterruptions;
            public int ArrivalInterruptions;
            public int MainRatingTotal;
            public int MainRatingSamples;
            public int MinimumMainRating = int.MaxValue;
            public int MaximumMainRating;
        }

        private sealed class SabotageTargetCounts
        {
            public string TargetId;
            public string PlanetId;
            public string TargetType;
            public int Started;
            public int Ended;
            public int Destroyed;
        }

        private sealed class TrackedMission
        {
            public string InstanceId;
            public string FactionId;
            public string MissionTypeId;
            public string TargetId;
            public string PlanetId;
            public string TargetType;
            public int InitialIntelTick;
            public int CurrentProgress;
            public int MaxProgress;
            public bool WaitingForParticipants;
            public int MainRating;

            /// <summary>
            /// Captures the mission state needed to compare it across ticks.
            /// </summary>
            /// <param name="mission">The mission to record.</param>
            /// <param name="game">The current game state.</param>
            /// <returns>The tracked mission state.</returns>
            public static TrackedMission From(Mission mission, GameRoot game)
            {
                string targetId = (mission as SabotageMission)?.SabotageTargetInstanceID;
                return new TrackedMission
                {
                    InstanceId = mission.InstanceID,
                    FactionId = mission.GetOwnerInstanceID(),
                    MissionTypeId = mission.ConfigKey,
                    TargetId = targetId,
                    PlanetId = mission.LocationInstanceID,
                    TargetType = game.GetSceneNodeByInstanceID<ISceneNode>(targetId)
                        ?.GetType()
                        .Name,
                    CurrentProgress = mission.CurrentProgress,
                    MaxProgress = mission.MaxProgress,
                    WaitingForParticipants = mission.IsWaitingForParticipants(),
                    MainRating =
                        mission.GetMainParticipants().Count > 0
                            ? mission
                                .GetMainParticipants()
                                .Sum(participant =>
                                    participant.GetEffectiveRating(mission.ParticipantRating)
                                ) / mission.GetMainParticipants().Count
                            : 0,
                };
            }
        }

        private sealed class PlanetState
        {
            public string OwnerId;
            public bool IsColonized;

            /// <summary>
            /// Captures the ownership state needed to compare a planet across ticks.
            /// </summary>
            /// <param name="planet">The planet to record.</param>
            /// <returns>The tracked planet state.</returns>
            public static PlanetState From(Planet planet)
            {
                return new PlanetState
                {
                    OwnerId = planet.GetOwnerInstanceID(),
                    IsColonized = planet.IsColonized,
                };
            }
        }

        private sealed class ResearchOrders
        {
            public int Ship;
            public int Facility;
            public int Troop;

            /// <summary>
            /// Captures the faction's current research progression.
            /// </summary>
            /// <param name="faction">The faction to record.</param>
            /// <returns>The tracked research orders.</returns>
            public static ResearchOrders From(Faction faction)
            {
                return new ResearchOrders
                {
                    Ship = faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign),
                    Facility = faction.GetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign),
                    Troop = faction.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining),
                };
            }
        }
    }

    private sealed class AttackReadinessTracker
    {
        private const int _aiTurnSampleInterval = 5;
        private readonly Dictionary<string, AttackReadinessFactionCounters> _counters = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records failed readiness gates for attack fleets waiting to launch.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        public void RecordTick(GameRoot game)
        {
            if (
                game?.Config?.AI == null
                || game.Config.AI.TickInterval <= 0
                || game.CurrentTick % (game.Config.AI.TickInterval * _aiTurnSampleInterval) != 0
            )
                return;

            Dictionary<string, Planet> planets = game.GetSceneNodesByType<Planet>()
                .Where(planet => !string.IsNullOrWhiteSpace(planet.InstanceID))
                .ToDictionary(planet => planet.InstanceID, StringComparer.Ordinal);

            foreach (Faction faction in game.GetFactions())
            {
                List<Fleet> buildingAttackFleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                        faction.InstanceID
                    )
                    .Where(fleet =>
                        fleet.Movement == null
                        && fleet.Order?.OrderType == FleetOrderType.Attack
                        && fleet.Order.Status == FleetOrderStatus.Building
                    )
                    .ToList();
                if (buildingAttackFleets.Count == 0)
                    continue;

                AIAssessment assessment = new AITurnContext(
                    game,
                    faction,
                    null,
                    null,
                    null,
                    null,
                    null,
                    game.Random
                ).Assessment;
                AttackReadinessFactionCounters counters = GetCounters(faction.InstanceID);

                foreach (Fleet fleet in buildingAttackFleets)
                {
                    List<string> blockers = GetBlockers(assessment, fleet, planets);
                    counters.Record(blockers);
                }
            }
        }

        /// <summary>
        /// Builds the readiness summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction's attack-readiness summary.</returns>
        public AttackReadinessSimulationSummary BuildSummary(string factionId)
        {
            return GetCounters(factionId).BuildSummary();
        }

        /// <summary>
        /// Gets or creates attack-readiness counters for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction readiness counters.</returns>
        private AttackReadinessFactionCounters GetCounters(string factionId)
        {
            string key = factionId ?? string.Empty;
            if (!_counters.TryGetValue(key, out AttackReadinessFactionCounters counters))
            {
                counters = new AttackReadinessFactionCounters();
                _counters[key] = counters;
            }

            return counters;
        }

        /// <summary>
        /// Gets the readiness gates currently blocking an attack fleet.
        /// </summary>
        /// <param name="assessment">The faction's current strategic assessment.</param>
        /// <param name="fleet">The attack fleet to inspect.</param>
        /// <param name="planets">Known planets indexed by instance identifier.</param>
        /// <returns>The active readiness blocker names.</returns>
        private static List<string> GetBlockers(
            AIAssessment assessment,
            Fleet fleet,
            IReadOnlyDictionary<string, Planet> planets
        )
        {
            List<string> blockers = new List<string>();
            if (
                string.IsNullOrWhiteSpace(fleet.Order?.TargetPlanetId)
                || !planets.TryGetValue(fleet.Order.TargetPlanetId, out Planet target)
            )
            {
                blockers.Add("MissingTarget");
                return blockers;
            }

            if (!assessment.CanFleetDepartHeadquarters(fleet))
                blockers.Add("HeadquartersReserve");
            if (!fleet.HasOperationalCapitalShips())
                blockers.Add("OperationalCapitalShips");
            if (
                assessment.GetReadyFleetCombatValue(fleet)
                < assessment.GetRequiredAttackCombatStrength(target)
            )
                blockers.Add("CombatStrength");
            if (
                assessment.GetReadyFleetRegimentCount(fleet)
                < assessment.GetRequiredAttackRegimentCount(target)
            )
                blockers.Add("RegimentCount");
            if (
                assessment.GetReadyFleetRegimentCapacity(fleet)
                < assessment.GetRequiredAttackRegimentCount(target)
            )
                blockers.Add("RegimentCapacity");
            if (
                assessment.GetReadyFleetRegimentAttackStrength(fleet)
                < assessment.GetRequiredAttackRegimentStrength(target)
            )
                blockers.Add("RegimentStrength");
            if (
                assessment.GetFleetBombardmentStrength(fleet)
                < assessment.GetRequiredBombardmentStrength(target)
            )
                blockers.Add("BombardmentStrength");

            return blockers;
        }

        private sealed class AttackReadinessFactionCounters
        {
            private readonly Dictionary<string, int> _samples = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _soleBlockerSamples = new(
                StringComparer.Ordinal
            );

            public int BuildingFleetSamples { get; private set; }

            /// <summary>
            /// Records one attack fleet's active readiness blockers.
            /// </summary>
            /// <param name="blockers">The blocker names observed for the fleet.</param>
            public void Record(IReadOnlyList<string> blockers)
            {
                BuildingFleetSamples++;
                foreach (string blocker in blockers)
                    Increment(_samples, blocker);

                if (blockers.Count == 1)
                    Increment(_soleBlockerSamples, blockers[0]);
            }

            /// <summary>
            /// Builds the aggregate attack-readiness blocker summary.
            /// </summary>
            /// <returns>The attack-readiness summary.</returns>
            public AttackReadinessSimulationSummary BuildSummary()
            {
                return new AttackReadinessSimulationSummary
                {
                    BuildingFleetSamples = BuildingFleetSamples,
                    Blockers = _samples
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new AttackReadinessBlockerSummary
                        {
                            Blocker = pair.Key,
                            Samples = pair.Value,
                            SoleBlockerSamples = _soleBlockerSamples.TryGetValue(
                                pair.Key,
                                out int soleSamples
                            )
                                ? soleSamples
                                : 0,
                        })
                        .ToArray(),
                };
            }

            /// <summary>
            /// Increments a named readiness counter.
            /// </summary>
            /// <param name="counters">The counters to update.</param>
            /// <param name="key">The counter name.</param>
            private static void Increment(Dictionary<string, int> counters, string key)
            {
                counters[key] = counters.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }
    }

    private sealed class PlanetaryAssaultTracker
    {
        private readonly Dictionary<string, List<PlanetaryAssaultSimulationResult>> _results = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records resolved planetary assaults.
        /// </summary>
        /// <param name="results">The resolved assault results.</param>
        public void Record(IReadOnlyList<PlanetaryAssaultResult> results)
        {
            foreach (PlanetaryAssaultResult result in results)
            {
                string factionId = result.AttackerOwnerInstanceID ?? string.Empty;
                if (
                    !_results.TryGetValue(
                        factionId,
                        out List<PlanetaryAssaultSimulationResult> items
                    )
                )
                {
                    items = new List<PlanetaryAssaultSimulationResult>();
                    _results[factionId] = items;
                }

                items.Add(
                    new PlanetaryAssaultSimulationResult
                    {
                        Tick = result.Tick,
                        PlanetId = result.Planet?.InstanceID,
                        PlanetName = result.Planet?.GetDisplayName(),
                        Success = result.Success,
                        InitialAttackerRegimentCount = result.InitialAttackerRegimentCount,
                        RemainingAttackerRegimentCount = result.RemainingAttackerRegimentCount,
                        InitialDefenderRegimentCount = result.InitialDefenderRegimentCount,
                        RemainingDefenderRegimentCount = result.RemainingDefenderRegimentCount,
                    }
                );
            }
        }

        /// <summary>
        /// Builds the assault summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction's assault summary.</returns>
        public PlanetaryAssaultSimulationSummary BuildSummary(string factionId)
        {
            PlanetaryAssaultSimulationResult[] results = _results.TryGetValue(
                factionId ?? string.Empty,
                out List<PlanetaryAssaultSimulationResult> items
            )
                ? items.ToArray()
                : Array.Empty<PlanetaryAssaultSimulationResult>();

            return new PlanetaryAssaultSimulationSummary
            {
                Attempted = results.Length,
                Succeeded = results.Count(result => result.Success),
                Failed = results.Count(result => !result.Success),
                Results = results,
            };
        }
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

        /// <summary>
        /// Records units present before simulation ticks are processed.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        public void RecordInitialState(GameRoot game)
        {
            RecordSeenOnly(
                game.GetSceneNodesByType<CapitalShip>().Where(IsComplete),
                _seenCapitalShips
            );
            RecordSeenOnly(
                game.GetSceneNodesByType<Starfighter>().Where(IsComplete),
                _seenStarfighters
            );
            RecordSeenOnly(game.GetSceneNodesByType<Regiment>().Where(IsComplete), _seenRegiments);
            RecordSeenOnly(
                game.GetSceneNodesByType<SpecialForces>().Where(IsComplete),
                _seenSpecialForces
            );
            RecordSeenOnly(game.GetSceneNodesByType<Building>().Where(IsComplete), _seenBuildings);
        }

        /// <summary>
        /// Records units created during the current simulation tick.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
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

        /// <summary>
        /// Gets manufactured capital ships for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured capital ship count.</returns>
        public int GetManufacturedCapitalShips(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.CapitalShips : 0;

        /// <summary>
        /// Gets manufactured starfighters for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured starfighter count.</returns>
        public int GetManufacturedStarfighters(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Starfighters : 0;

        /// <summary>
        /// Gets manufactured regiments for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured regiment count.</returns>
        public int GetManufacturedRegiments(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Regiments : 0;

        /// <summary>
        /// Gets manufactured special forces for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured special forces count.</returns>
        public int GetManufacturedSpecialForces(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.SpecialForces : 0;

        /// <summary>
        /// Gets manufactured buildings for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured building count.</returns>
        public int GetManufacturedBuildings(string factionId) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts) ? counts.Buildings : 0;

        /// <summary>
        /// Gets manufactured buildings of a type for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <param name="buildingType">The building type to count.</param>
        /// <returns>The manufactured building count.</returns>
        public int GetManufacturedBuildings(string factionId, BuildingType buildingType) =>
            TryGetCounts(factionId, out ManufacturedUnitCounts counts)
            && counts.BuildingsByType.TryGetValue(buildingType, out int count)
                ? count
                : 0;

        /// <summary>
        /// Records existing units without counting them as manufactured.
        /// </summary>
        /// <typeparam name="T">The scene node type to record.</typeparam>
        /// <param name="units">The units to record.</param>
        /// <param name="seen">The set that receives unit IDs.</param>
        private static void RecordSeenOnly<T>(IEnumerable<T> units, HashSet<string> seen)
            where T : ISceneNode, IManufacturable
        {
            foreach (T unit in units)
            {
                string instanceId = unit.GetInstanceID();
                if (!string.IsNullOrEmpty(instanceId))
                    seen.Add(instanceId);
            }
        }

        /// <summary>
        /// Records newly discovered units and increments faction counts.
        /// </summary>
        /// <typeparam name="T">The scene node type to record.</typeparam>
        /// <param name="units">The units to inspect.</param>
        /// <param name="seen">The set used to detect new unit IDs.</param>
        /// <param name="increment">The count update to apply.</param>
        private void RecordNewUnits<T>(
            IEnumerable<T> units,
            HashSet<string> seen,
            Action<ManufacturedUnitCounts> increment
        )
            where T : ISceneNode, IManufacturable
        {
            foreach (T unit in units)
            {
                string instanceId = unit.GetInstanceID();
                if (
                    !IsManufactured(unit)
                    || string.IsNullOrEmpty(instanceId)
                    || !seen.Add(instanceId)
                )
                    continue;

                string factionId = unit.GetOwnerInstanceID();
                if (string.IsNullOrEmpty(factionId))
                    continue;

                increment(GetCounts(factionId));
            }
        }

        /// <summary>
        /// Records newly discovered buildings and increments faction counts.
        /// </summary>
        /// <param name="buildings">The buildings to inspect.</param>
        private void RecordNewBuildings(IEnumerable<Building> buildings)
        {
            foreach (Building building in buildings)
            {
                string instanceId = building.GetInstanceID();
                if (
                    !IsManufactured(building)
                    || string.IsNullOrEmpty(instanceId)
                    || !_seenBuildings.Add(instanceId)
                )
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

        /// <summary>
        /// Determines whether a manufacturable item has completed production.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        /// <returns>True when production is complete.</returns>
        private static bool IsComplete(IManufacturable item) =>
            item.ManufacturingStatus == ManufacturingStatus.Complete;

        /// <summary>
        /// Determines whether a completed item was produced during the game.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        /// <returns>True when the item has an originating producer planet.</returns>
        private static bool IsManufactured(IManufacturable item) =>
            IsComplete(item) && !string.IsNullOrEmpty(item.ProducerPlanetID);

        /// <summary>
        /// Gets or creates manufactured unit counts for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The manufactured unit counts.</returns>
        private ManufacturedUnitCounts GetCounts(string factionId)
        {
            if (!_manufacturedByFaction.TryGetValue(factionId, out ManufacturedUnitCounts counts))
            {
                counts = new ManufacturedUnitCounts();
                _manufacturedByFaction[factionId] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets manufactured unit counts for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <param name="counts">The manufactured unit counts.</param>
        /// <returns>True if counts exist for the faction.</returns>
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
        public ManufacturingIdleResourceSummary BuildingResources;
        public ManufacturingIdleResourceSummary ShipResources;
        public ManufacturingIdleResourceSummary TroopResources;
        public ManufacturingIdlePlanetSummary[] TopIdlePlanets;
    }

    [Serializable]
    private sealed class ManufacturingIdleResourceSummary
    {
        public int SampleCount;
        public int FundedSampleCount;
        public int FundedCapacityTicks;
        public double AverageRawMaterialStockpile;
        public double AverageRefinedMaterialStockpile;
        public double AverageMaintenanceHeadroom;
        public int MinimumRawMaterialStockpile;
        public int MinimumRefinedMaterialStockpile;
        public int MinimumMaintenanceHeadroom;
        public int MaximumRawMaterialStockpile;
        public int MaximumRefinedMaterialStockpile;
        public int MaximumMaintenanceHeadroom;
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
        public int GroundAttackStrength;
        public int BombardmentStrength;
        public int RegimentCapacity;
        public int RequiredAttackCombatStrength;
        public int RequiredAttackRegimentCount;
        public int RequiredAttackRegimentStrength;
        public int RequiredBombardmentStrength;
        public int TargetRegimentDefenseStrength;
        public int TargetShieldStrength;
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

        /// <summary>
        /// Records changed fleet state for the current simulation tick.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        public void RecordTick(GameRoot game)
        {
            HashSet<string> liveFleetIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (Faction faction in game.GetFactions().OrderBy(faction => faction.InstanceID))
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

        /// <summary>
        /// Returns the recorded fleet history snapshots.
        /// </summary>
        /// <returns>The recorded fleet history snapshots.</returns>
        public FleetHistorySnapshot[] ToArray()
        {
            return _snapshots.ToArray();
        }

        /// <summary>
        /// Builds a fleet history snapshot.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        /// <param name="faction">The fleet owner faction.</param>
        /// <param name="fleet">The fleet to summarize.</param>
        /// <param name="destroyed">Whether the fleet has been destroyed.</param>
        /// <returns>The fleet history snapshot.</returns>
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
                CapitalShipCount = fleet.GetChildren<CapitalShip>().Count,
                StarfighterCount = fleet.GetStarfighters().Count(),
                RegimentCount = fleet.GetRegiments().Count(),
                OfficerCount = fleet.GetOfficers().Count(),
                OrderType = fleet.Order?.OrderType.ToString(),
                OrderStatus = fleet.Order?.Status.ToString(),
                OrderTargetPlanetId = fleet.Order?.TargetPlanetId,
                OrderTargetPlanetName = targetPlanet?.GetDisplayName(),
                CapitalShips = SummarizeUnits(fleet.GetChildren<CapitalShip>()),
                Starfighters = SummarizeUnits(fleet.GetStarfighters()),
                Regiments = SummarizeUnits(fleet.GetRegiments()),
                Officers = SummarizeUnits(fleet.GetOfficers()),
            };
        }

        /// <summary>
        /// Builds a fleet history snapshot for a destroyed fleet.
        /// </summary>
        /// <param name="tick">The current simulation tick.</param>
        /// <param name="previousSnapshot">The previous fleet snapshot.</param>
        /// <returns>The destroyed fleet history snapshot.</returns>
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

        /// <summary>
        /// Builds the stable comparison key for a fleet snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to key.</param>
        /// <returns>The stable snapshot key.</returns>
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
        public int? Seed { get; set; }
        public string SaveFileName { get; set; }
        public string SaveDisplayName { get; set; }
        public string PlayerFactionId { get; set; }

        /// <summary>
        /// Parses simulation options from command-line arguments.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The parsed simulation options.</returns>
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
                Seed = ParseNullableInt(args, _seedFlag),
            };
        }

        /// <summary>
        /// Parses an integer command-line option.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="flag">The option flag to read.</param>
        /// <param name="defaultValue">The value to use when the flag is absent.</param>
        /// <returns>The parsed integer value.</returns>
        private static int ParseInt(string[] args, string flag, int defaultValue)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        /// <summary>
        /// Parses an optional integer command-line option.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="flag">The option flag to read.</param>
        /// <returns>The parsed integer value, or null if the flag is absent.</returns>
        private static int? ParseNullableInt(string[] args, string flag)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : null;
        }

        /// <summary>
        /// Parses a string command-line option.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="flag">The option flag to read.</param>
        /// <param name="defaultValue">The value to use when the flag is absent.</param>
        /// <returns>The parsed string value.</returns>
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

    /// <summary>
    /// Builds the current summary for a fleet.
    /// </summary>
    /// <param name="game">The game state to inspect.</param>
    /// <param name="faction">The fleet owner faction.</param>
    /// <param name="fleet">The fleet to summarize.</param>
    /// <returns>The fleet simulation summary.</returns>
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
        int groundAttackStrength = GetFleetRegimentAttackStrength(game, fleet);
        int bombardmentStrength = BombardmentSystem.GetBombardmentStrength(
            new[] { fleet },
            game.Config.Combat.Bombardment
        );
        int targetRegimentDefenseStrength = GetTargetRegimentDefenseStrength(game, targetPlanet);
        int targetShieldStrength = BombardmentSystem.GetBombardmentShieldStrength(targetPlanet);
        string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
        int targetRegimentCount =
            targetPlanet
                ?.GetAllRegiments()
                .Count(regiment =>
                    regiment.GetOwnerInstanceID() == targetOwnerId
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
            ?? 0;
        int targetStrongestHostileFleetStrength = GetStrongestHostileFleetStrength(
            faction,
            targetPlanet
        );
        int requiredAttackCombatStrength = GetRequiredAttackCombatStrength(
            game,
            targetStrongestHostileFleetStrength
        );
        int requiredAttackRegimentCount = GetRequiredAttackRegimentCount(
            game,
            faction,
            targetPlanet,
            targetRegimentCount
        );
        int requiredAttackRegimentStrength =
            targetRegimentDefenseStrength
                * game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense
            + _percentScale
            - 1;
        requiredAttackRegimentStrength /= _percentScale;
        int requiredBombardmentStrength = PlanetaryAssaultSystem.IsBlockedByShields(
            targetPlanet,
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
        )
            ? targetShieldStrength + 1
            : 0;

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
            CapitalShipCount = fleet.GetChildren<CapitalShip>().Count,
            StarfighterCount = fleet.GetStarfighters().Count(),
            RegimentCount = fleet.GetRegiments().Count(),
            OfficerCount = fleet.GetOfficers().Count(),
            OrderType = fleet.Order?.OrderType.ToString(),
            OrderStatus = fleet.Order?.Status.ToString(),
            OrderTargetPlanetId = fleet.Order?.TargetPlanetId,
            OrderTargetPlanetName = targetPlanet?.GetDisplayName(),
            OrderTargetOwnerId = targetPlanet?.GetOwnerInstanceID(),
            GroundAttackStrength = groundAttackStrength,
            BombardmentStrength = bombardmentStrength,
            RegimentCapacity = fleet.GetRegimentCapacity(),
            RequiredAttackCombatStrength = requiredAttackCombatStrength,
            RequiredAttackRegimentCount = requiredAttackRegimentCount,
            RequiredAttackRegimentStrength = requiredAttackRegimentStrength,
            RequiredBombardmentStrength = requiredBombardmentStrength,
            TargetRegimentDefenseStrength = targetRegimentDefenseStrength,
            TargetShieldStrength = targetShieldStrength,
            TargetRegimentCount = targetRegimentCount,
            TargetStrongestHostileFleetStrength = targetStrongestHostileFleetStrength,
            CapitalShips = SummarizeUnits(fleet.GetChildren<CapitalShip>()),
            Starfighters = SummarizeUnits(fleet.GetStarfighters()),
            Regiments = SummarizeUnits(fleet.GetRegiments()),
            Officers = SummarizeUnits(fleet.GetOfficers()),
        };
    }

    /// <summary>
    /// Gets the strongest hostile fleet strength at a target planet.
    /// </summary>
    /// <param name="faction">The faction evaluating the target.</param>
    /// <param name="targetPlanet">The target planet.</param>
    /// <returns>The strongest hostile fleet strength.</returns>
    private static int GetStrongestHostileFleetStrength(Faction faction, Planet targetPlanet)
    {
        if (faction == null || targetPlanet == null)
            return 0;

        return targetPlanet
            .GetChildren<Fleet>()
            .Where(fleet =>
                fleet.GetOwnerInstanceID() != null
                && fleet.GetOwnerInstanceID() != faction.InstanceID
                && fleet.Movement == null
            )
            .Select(fleet => fleet.GetCombatValue())
            .DefaultIfEmpty()
            .Max();
    }

    /// <summary>
    /// Gets the combat strength required to attack a target.
    /// </summary>
    /// <param name="game">The game state to inspect.</param>
    /// <param name="targetStrongestHostileFleetStrength">The strongest hostile fleet strength.</param>
    /// <returns>The required attack combat strength.</returns>
    private static int GetRequiredAttackCombatStrength(
        GameRoot game,
        int targetStrongestHostileFleetStrength
    )
    {
        GameConfig.AIFleetDeploymentConfig config = game.Config.AI.FleetDeployment;
        int fleetDefenseRequirement =
            targetStrongestHostileFleetStrength
                * config.AttackStrengthPercentOfStrongestHostileFleet
            + _percentScale
            - 1;
        fleetDefenseRequirement /= _percentScale;

        return Math.Max(config.MinimumAttackStrength, fleetDefenseRequirement);
    }

    /// <summary>
    /// Gets the combined attack strength of ready regiments aboard a fleet.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="fleet">The fleet to inspect.</param>
    /// <returns>The regiment attack strength including leadership bonuses.</returns>
    private static int GetFleetRegimentAttackStrength(GameRoot game, Fleet fleet)
    {
        if (game == null || fleet == null)
            return 0;

        int leadershipBonus = PlanetaryAssaultSystem.GetLeadershipBonus(
            fleet.GetOfficers(),
            OfficerRank.General,
            fleet.GetOwnerInstanceID(),
            game.Config.Combat.PlanetaryAssault
        );
        return fleet
            .GetChildren<CapitalShip>()
            .Where(ship =>
                ship.ManufacturingStatus == ManufacturingStatus.Complete && ship.Movement == null
            )
            .SelectMany(ship => ship.GetChildren<Regiment>())
            .Where(regiment =>
                regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null
            )
            .Sum(regiment => regiment.AttackRating + leadershipBonus);
    }

    /// <summary>
    /// Gets the combined defense strength of ready regiments at a planet.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="planet">The target planet.</param>
    /// <returns>The regiment defense strength including leadership bonuses.</returns>
    private static int GetTargetRegimentDefenseStrength(GameRoot game, Planet planet)
    {
        if (game == null || planet == null)
            return 0;

        string ownerId = planet.GetOwnerInstanceID();
        int leadershipBonus = PlanetaryAssaultSystem.GetLeadershipBonus(
            planet.GetAllOfficers(),
            OfficerRank.General,
            ownerId,
            game.Config.Combat.PlanetaryAssault
        );
        return planet
            .GetAllRegiments()
            .Where(regiment =>
                regiment.GetOwnerInstanceID() == ownerId
                && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null
            )
            .Sum(regiment => regiment.DefenseRating + leadershipBonus);
    }

    /// <summary>
    /// Gets the regiment count required to attack a target.
    /// </summary>
    /// <param name="game">The game state to inspect.</param>
    /// <param name="faction">The faction evaluating the target.</param>
    /// <param name="targetPlanet">The target planet.</param>
    /// <param name="targetRegimentCount">The target regiment count.</param>
    /// <returns>The required attack regiment count.</returns>
    private static int GetRequiredAttackRegimentCount(
        GameRoot game,
        Faction faction,
        Planet targetPlanet,
        int targetRegimentCount
    )
    {
        if (game == null || faction == null || targetPlanet == null)
            return 0;

        int stableGarrison = UprisingSystem.CalculateGarrisonRequirement(
            targetPlanet,
            faction,
            game.Config.AI.Garrison
        );
        return Math.Max(
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount,
            targetRegimentCount + stableGarrison
        );
    }

    /// <summary>
    /// Builds summaries for current planets with idle manufacturing capacity.
    /// </summary>
    /// <param name="game">The game state to inspect.</param>
    /// <param name="faction">The faction to summarize.</param>
    /// <returns>The current idle planet summaries.</returns>
    private static CurrentIdlePlanetSummary[] BuildCurrentIdlePlanetSummaries(
        GameRoot game,
        Faction faction
    )
    {
        return game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID)
            .Where(IsProductionEligiblePlanet)
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

    /// <summary>
    /// Determines whether a planet can use production capacity.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>True if the planet can use production capacity.</returns>
    private static bool IsProductionEligiblePlanet(Planet planet)
    {
        return planet?.IsBlockaded() == false && !planet.IsDestroyed && !planet.IsInUprising;
    }

    /// <summary>
    /// Summarizes units by display label.
    /// </summary>
    /// <typeparam name="T">The unit type to summarize.</typeparam>
    /// <param name="units">The units to summarize.</param>
    /// <returns>The grouped unit labels.</returns>
    private static string[] SummarizeUnits<T>(IEnumerable<T> units)
        where T : class
    {
        return units
            .GroupBy(GetUnitLabel)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();
    }

    /// <summary>
    /// Gets a display label for a summarized unit.
    /// </summary>
    /// <typeparam name="T">The unit type to label.</typeparam>
    /// <param name="unit">The unit to label.</param>
    /// <returns>The unit label.</returns>
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

        /// <summary>
        /// Records idle manufacturing capacity for the current simulation tick.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        public void RecordTick(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
            {
                FactionIdleCounters counters = GetOrCreateFactionCounters(faction.InstanceID);
                foreach (
                    Planet planet in game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID)
                )
                {
                    if (!IsProductionEligiblePlanet(planet))
                        continue;

                    RecordPlanetType(counters, faction, planet, ManufacturingType.Building);
                    RecordPlanetType(counters, faction, planet, ManufacturingType.Ship);
                    RecordPlanetType(counters, faction, planet, ManufacturingType.Troop);
                }
            }
        }

        /// <summary>
        /// Builds the idle manufacturing summary for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The idle manufacturing summary.</returns>
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
                BuildingResources = counters.BuildingResources.BuildSummary(),
                ShipResources = counters.ShipResources.BuildSummary(),
                TroopResources = counters.TroopResources.BuildSummary(),
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

        /// <summary>
        /// Records idle capacity for one planet and manufacturing type.
        /// </summary>
        /// <param name="counters">The faction counters to update.</param>
        /// <param name="faction">The faction that owns the production facilities.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="type">The manufacturing type to inspect.</param>
        private static void RecordPlanetType(
            FactionIdleCounters counters,
            Faction faction,
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
            IdleResourceCounters resourceCounters = counters.GetResourceCounters(type);
            resourceCounters.Record(faction, type, idleCapacity);
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

        /// <summary>
        /// Gets or creates idle manufacturing counters for a faction.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The faction idle counters.</returns>
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
            public IdleResourceCounters BuildingResources { get; } = new();
            public IdleResourceCounters ShipResources { get; } = new();
            public IdleResourceCounters TroopResources { get; } = new();
            public Dictionary<string, PlanetIdleCounters> Planets { get; } = new();

            /// <summary>
            /// Returns resource counters for a manufacturing type.
            /// </summary>
            /// <param name="type">The manufacturing type to retrieve.</param>
            /// <returns>The matching resource counters.</returns>
            public IdleResourceCounters GetResourceCounters(ManufacturingType type)
            {
                return type switch
                {
                    ManufacturingType.Building => BuildingResources,
                    ManufacturingType.Ship => ShipResources,
                    ManufacturingType.Troop => TroopResources,
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                };
            }

            /// <summary>
            /// Gets or creates idle manufacturing counters for a planet.
            /// </summary>
            /// <param name="planet">The planet to inspect.</param>
            /// <returns>The planet idle counters.</returns>
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

        private sealed class IdleResourceCounters
        {
            private long _rawMaterialStockpileTotal;
            private long _refinedMaterialStockpileTotal;
            private long _maintenanceHeadroomTotal;

            public int SampleCount;
            public int FundedSampleCount;
            public int FundedCapacityTicks;
            public int MinimumRawMaterialStockpile = int.MaxValue;
            public int MinimumRefinedMaterialStockpile = int.MaxValue;
            public int MinimumMaintenanceHeadroom = int.MaxValue;
            public int MaximumRawMaterialStockpile = int.MinValue;
            public int MaximumRefinedMaterialStockpile = int.MinValue;
            public int MaximumMaintenanceHeadroom = int.MinValue;

            /// <summary>
            /// Records the faction resources available during one idle planet tick.
            /// </summary>
            /// <param name="faction">The faction whose resources are sampled.</param>
            /// <param name="type">The idle manufacturing type.</param>
            /// <param name="idleCapacity">The unused facility capacity.</param>
            public void Record(Faction faction, ManufacturingType type, int idleCapacity)
            {
                int rawMaterialStockpile = faction.RawMaterialStockpile;
                int refinedMaterialStockpile = faction.RefinedMaterialStockpile;
                int maintenanceHeadroom = faction.MaintenanceHeadroom;

                SampleCount++;
                _rawMaterialStockpileTotal += rawMaterialStockpile;
                _refinedMaterialStockpileTotal += refinedMaterialStockpile;
                _maintenanceHeadroomTotal += maintenanceHeadroom;
                MinimumRawMaterialStockpile = Math.Min(
                    MinimumRawMaterialStockpile,
                    rawMaterialStockpile
                );
                MinimumRefinedMaterialStockpile = Math.Min(
                    MinimumRefinedMaterialStockpile,
                    refinedMaterialStockpile
                );
                MinimumMaintenanceHeadroom = Math.Min(
                    MinimumMaintenanceHeadroom,
                    maintenanceHeadroom
                );
                MaximumRawMaterialStockpile = Math.Max(
                    MaximumRawMaterialStockpile,
                    rawMaterialStockpile
                );
                MaximumRefinedMaterialStockpile = Math.Max(
                    MaximumRefinedMaterialStockpile,
                    refinedMaterialStockpile
                );
                MaximumMaintenanceHeadroom = Math.Max(
                    MaximumMaintenanceHeadroom,
                    maintenanceHeadroom
                );

                if (
                    !CanFundAnyProduct(faction, type, refinedMaterialStockpile, maintenanceHeadroom)
                )
                    return;

                FundedSampleCount++;
                FundedCapacityTicks += idleCapacity;
            }

            /// <summary>
            /// Builds the serializable resource summary.
            /// </summary>
            /// <returns>The recorded idle-resource statistics.</returns>
            public ManufacturingIdleResourceSummary BuildSummary()
            {
                return new ManufacturingIdleResourceSummary
                {
                    SampleCount = SampleCount,
                    FundedSampleCount = FundedSampleCount,
                    FundedCapacityTicks = FundedCapacityTicks,
                    AverageRawMaterialStockpile = GetAverage(_rawMaterialStockpileTotal),
                    AverageRefinedMaterialStockpile = GetAverage(_refinedMaterialStockpileTotal),
                    AverageMaintenanceHeadroom = GetAverage(_maintenanceHeadroomTotal),
                    MinimumRawMaterialStockpile = GetMinimum(MinimumRawMaterialStockpile),
                    MinimumRefinedMaterialStockpile = GetMinimum(MinimumRefinedMaterialStockpile),
                    MinimumMaintenanceHeadroom = GetMinimum(MinimumMaintenanceHeadroom),
                    MaximumRawMaterialStockpile = GetMaximum(MaximumRawMaterialStockpile),
                    MaximumRefinedMaterialStockpile = GetMaximum(MaximumRefinedMaterialStockpile),
                    MaximumMaintenanceHeadroom = GetMaximum(MaximumMaintenanceHeadroom),
                };
            }

            /// <summary>
            /// Returns an average across the recorded idle samples.
            /// </summary>
            /// <param name="total">The accumulated resource value.</param>
            /// <returns>The average value, or zero when no samples exist.</returns>
            private double GetAverage(long total) =>
                SampleCount > 0 ? total / (double)SampleCount : 0;

            /// <summary>
            /// Normalizes an uninitialized minimum value.
            /// </summary>
            /// <param name="value">The recorded minimum.</param>
            /// <returns>The recorded value, or zero when no sample exists.</returns>
            private static int GetMinimum(int value) => value == int.MaxValue ? 0 : value;

            /// <summary>
            /// Normalizes an uninitialized maximum value.
            /// </summary>
            /// <param name="value">The recorded maximum.</param>
            /// <returns>The recorded value, or zero when no sample exists.</returns>
            private static int GetMaximum(int value) => value == int.MinValue ? 0 : value;

            /// <summary>
            /// Returns whether current resources can fund at least one unlocked product.
            /// </summary>
            /// <param name="faction">The faction evaluating production.</param>
            /// <param name="type">The manufacturing type to inspect.</param>
            /// <param name="refinedMaterials">The available refined materials.</param>
            /// <param name="maintenanceHeadroom">The available maintenance headroom.</param>
            /// <returns>True when at least one unlocked product is affordable.</returns>
            private static bool CanFundAnyProduct(
                Faction faction,
                ManufacturingType type,
                int refinedMaterials,
                int maintenanceHeadroom
            )
            {
                return refinedMaterials > 0
                    && faction
                        .GetUnlockedTechnologies(type)
                        .Select(technology => technology.GetReference())
                        .OfType<IManufacturable>()
                        .Any(product => product.GetMaintenanceCost() <= maintenanceHeadroom);
            }
        }
    }
}
