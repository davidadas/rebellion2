using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Systems;

namespace Rebellion.Generation
{
    /// <summary>
    /// Builds the game using a multi-phase pipeline that faithfully reproduces
    /// the original Star Wars: Rebellion generation system.
    ///
    /// Pipeline phases:
    /// 0. Load templates, filter systems by galaxy size
    /// 1. GalaxyClassifier: classify planets into 5 faction buckets
    /// 2. SystemConfigurator: set energy, raw materials, colonization, support
    /// 3. Set faction research levels, HQ flags
    /// 4. FacilitySeeder: place buildings using weighted probability tables
    /// 5. UnitDeployer: fixed garrisons, fleets, and budget-based deployment
    /// 6. OfficerGenerator: select and deploy officers (existing, kept as-is)
    /// 7. BalancePass: post-placement support adjustments
    /// 8. Technology tree setup + fog of war init
    /// </summary>
    public sealed class GameBuilder
    {
        private readonly GameSummary summary;
        private readonly IResourceManager resourceManager;
        private readonly IRandomNumberProvider randomProvider;

        public GameBuilder(GameSummary summary)
        {
            this.summary = summary;
            this.resourceManager = ResourceManager.Instance;
            this.randomProvider = new SystemRandomProvider(Environment.TickCount);
        }

        public GameRoot BuildGame()
        {
            GameConfig gameConfig = ConfigLoader.LoadGameConfig();
            GameGenerationRules rules = resourceManager.GetConfig<GameGenerationRules>();

            // Phase 0: Load templates and filter systems by galaxy size
            PlanetSystem[] allSystems = resourceManager.GetGameData<PlanetSystem>();
            PlanetSystem[] systems = FilterSystemsByGalaxySize(allSystems);
            Faction[] factions = resourceManager.GetGameData<Faction>();
            Building[] buildingTemplates = resourceManager.GetGameData<Building>();
            CapitalShip[] shipTemplates = resourceManager.GetGameData<CapitalShip>();
            Starfighter[] fighterTemplates = resourceManager.GetGameData<Starfighter>();
            Regiment[] regimentTemplates = resourceManager.GetGameData<Regiment>();
            GameEvent[] gameEvents = resourceManager.GetGameData<GameEvent>();

            // Phase 1: Galaxy classification
            GalaxyClassifier classifier = new GalaxyClassifier();
            GalaxyClassificationResult classification = classifier.Classify(
                systems,
                factions,
                summary,
                rules,
                randomProvider
            );

            // Ensure StartingFactionIDs is populated
            if (summary.StartingFactionIDs == null || summary.StartingFactionIDs.Length == 0)
            {
                summary.StartingFactionIDs = factions.Select(f => f.InstanceID).ToArray();
            }

            // Phase 2: System configuration (energy, raw materials, colonization, support)
            SystemConfigurator configurator = new SystemConfigurator();
            configurator.Configure(
                systems,
                classification,
                rules,
                summary.StartingFactionIDs,
                randomProvider
            );

            // Phase 3: Set faction starting research
            SetFactionStartingResearch(factions);

            // Phase 4: Facility seeding
            FacilitySeeder facilitySeeder = new FacilitySeeder();
            List<Building> deployedBuildings = facilitySeeder.Seed(
                systems,
                buildingTemplates,
                rules,
                randomProvider
            );

            // Phase 5: Unit deployment
            UnitDeployer unitDeployer = new UnitDeployer();
            unitDeployer.Deploy(
                systems,
                factions,
                regimentTemplates,
                fighterTemplates,
                shipTemplates,
                rules,
                classification,
                gameConfig,
                randomProvider,
                (int)summary.GalaxySize,
                (int)summary.Difficulty,
                summary.PlayerFactionID
            );

            // Phase 6: Officers
            OfficerGenerator officerGenerator = new OfficerGenerator();
            OfficerGenerator.OfficerResults officerResults = officerGenerator.Deploy(
                systems,
                rules,
                summary,
                randomProvider
            );
            Officer[] unrecruitedOfficers = officerResults.Unrecruited;

            // Phase 7: Balance pass
            BalancePass balancePass = new BalancePass();
            balancePass.Apply(systems, factions);

            // Phase 8: Technology tree setup
            SetupFactionTechnologies(
                factions,
                buildingTemplates,
                shipTemplates,
                fighterTemplates,
                regimentTemplates
            );

            // Create the game
            return CreateGame(systems, factions, gameEvents, unrecruitedOfficers, gameConfig);
        }

        private PlanetSystem[] FilterSystemsByGalaxySize(PlanetSystem[] allSystems)
        {
            int galaxySize = (int)summary.GalaxySize;
            return allSystems.Where(s => (int)s.Visibility <= galaxySize).ToArray();
        }

        private void SetFactionStartingResearch(Faction[] factions)
        {
            int startingOrder = summary.StartingResearchLevel;
            foreach (Faction faction in factions)
            {
                faction.SetHighestUnlockedOrder(ManufacturingType.Building, startingOrder);
                faction.SetHighestUnlockedOrder(ManufacturingType.Ship, startingOrder);
                faction.SetHighestUnlockedOrder(ManufacturingType.Troop, startingOrder);
            }
        }

        private void SetupFactionTechnologies(
            Faction[] factions,
            Building[] buildings,
            CapitalShip[] capitalShips,
            Starfighter[] starfighters,
            Regiment[] regiments
        )
        {
            IManufacturable[] allTech = buildings
                .Cast<IManufacturable>()
                .Concat(capitalShips)
                .Concat(starfighters)
                .Concat(regiments)
                .ToArray();

            foreach (Faction faction in factions)
            {
                faction.RebuildResearchQueues(allTech);
            }
        }

        private void InitializeFogOfWar(GameRoot game)
        {
            GameGenerationRules rules = resourceManager.GetConfig<GameGenerationRules>();
            FogOfWarSystem fogSystem = new FogOfWarSystem(game);

            // Build set of (planetID, factionID) visibility overrides from config
            HashSet<(string planetId, string viewerFactionId)> visibilityOverrides = new HashSet<(string planetId, string viewerFactionId)>();
            foreach (FactionSetup factionSetup in rules.GalaxyClassification.FactionSetups)
            {
                if (factionSetup.StartingPlanets == null)
                    continue;
                foreach (StartingPlanet sp in factionSetup.StartingPlanets)
                {
                    if (
                        string.IsNullOrEmpty(sp.PlanetInstanceID)
                        || sp.VisibleToFactionIDs == null
                    )
                        continue;
                    foreach (string viewerId in sp.VisibleToFactionIDs)
                    {
                        visibilityOverrides.Add((sp.PlanetInstanceID, viewerId));
                    }
                }
            }

            foreach (PlanetSystem system in game.Galaxy.PlanetSystems)
            {
                foreach (Faction faction in game.Factions)
                {
                    if (system.SystemType == PlanetSystemType.CoreSystem)
                    {
                        foreach (Planet planet in system.Planets)
                        {
                            bool isOwnPlanet = planet.OwnerInstanceID == faction.InstanceID;
                            if (!isOwnPlanet)
                            {
                                fogSystem.CaptureSnapshot(faction, planet, system, currentTick: 0);
                            }
                        }
                    }

                    // Apply config-driven visibility overrides (e.g. Empire sees Yavin at start)
                    foreach (Planet planet in system.Planets)
                    {
                        if (visibilityOverrides.Contains((planet.InstanceID, faction.InstanceID)))
                        {
                            fogSystem.CaptureSnapshot(faction, planet, system, currentTick: 0);
                        }
                    }
                }
            }
        }

        private GameRoot CreateGame(
            PlanetSystem[] galaxyMap,
            Faction[] factions,
            GameEvent[] gameEvents,
            Officer[] unrecruitedOfficers,
            GameConfig gameConfig
        )
        {
            GalaxyMap galaxy = new GalaxyMap { PlanetSystems = galaxyMap.ToList() };

            GameRoot game = new GameRoot
            {
                EventPool = gameEvents.ToList(),
                Summary = this.summary,
                Factions = factions.ToList(),
                Galaxy = galaxy,
                UnrecruitedOfficers = unrecruitedOfficers.ToList(),
            };
            game.SetConfig(gameConfig);

            InitializeFogOfWar(game);

            return game;
        }
    }
}
