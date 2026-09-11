using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Combat;
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

public static partial class HeadlessSimulationRunner
{
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
    /// <param name="missionOutcomeTracker">The mission outcome tracker.</param>
    /// <param name="personnelOutcomeTracker">The personnel outcome tracker.</param>
    /// <param name="specialForcesLifecycleTracker">The special-forces lifecycle tracker.</param>
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
        MissionOutcomeTracker missionOutcomeTracker,
        PersonnelOutcomeTracker personnelOutcomeTracker,
        SpecialForcesLifecycleTracker specialForcesLifecycleTracker,
        PlanetaryAssaultTracker planetaryAssaultTracker,
        GarrisonRemovalBombardmentTracker garrisonRemovalBombardmentTracker,
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
                    ConstructionFacilityCount = CountProductionFacilities(
                        game,
                        faction,
                        BuildingType.ConstructionFacility
                    ),
                    ProjectedConstructionFacilityCount = CountProjectedProductionFacilities(
                        game,
                        faction,
                        BuildingType.ConstructionFacility
                    ),
                    AdvancedShipyardCount = CountAdvancedProductionFacilities(
                        game,
                        faction,
                        BuildingType.Shipyard
                    ),
                    ShipyardCount = CountProductionFacilities(game, faction, BuildingType.Shipyard),
                    ProjectedShipyardCount = CountProjectedProductionFacilities(
                        game,
                        faction,
                        BuildingType.Shipyard
                    ),
                    AdvancedTrainingFacilityCount = CountAdvancedProductionFacilities(
                        game,
                        faction,
                        BuildingType.TrainingFacility
                    ),
                    TrainingFacilityCount = CountProductionFacilities(
                        game,
                        faction,
                        BuildingType.TrainingFacility
                    ),
                    ProjectedTrainingFacilityCount = CountProjectedProductionFacilities(
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
                    ManufacturedUnitTypes = manufacturedUnitTracker.GetManufacturedUnitTypes(
                        faction.InstanceID
                    ),
                    SpecialForcesLifecycle = specialForcesLifecycleTracker.BuildSummary(
                        faction.InstanceID
                    ),
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
                    MissionOutcomes = missionOutcomeTracker.BuildSummary(faction.InstanceID),
                    PersonnelOutcomes = personnelOutcomeTracker.BuildSummary(faction.InstanceID),
                    PlanetaryAssaults = planetaryAssaultTracker.BuildSummary(faction.InstanceID),
                    GarrisonRemovalBombardments = garrisonRemovalBombardmentTracker.BuildSummary(
                        faction.InstanceID
                    ),
                    AttackReadiness = attackReadinessTracker.BuildSummary(faction.InstanceID),
                    ProductionFacilityPlanets = BuildProductionFacilityPlanetSummaries(
                        game,
                        faction
                    ),
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
            new SystemRandomProvider(0),
            new FogOfWarSystem(game).BuildFactionView(faction)
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
    /// Counts completed, deployed production facilities owned by one faction.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose facilities are counted.</param>
    /// <param name="buildingType">The production-facility category to count.</param>
    /// <returns>The number of completed, deployed facilities.</returns>
    private static int CountProductionFacilities(
        GameRoot game,
        Faction faction,
        BuildingType buildingType
    )
    {
        return game.GetSceneNodesByOwnerInstanceID<Building>(faction.InstanceID)
            .Count(building =>
                building.GetBuildingType() == buildingType
                && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                && building.Movement == null
            );
    }

    /// <summary>
    /// Counts complete, constructing, and in-transit production facilities.
    /// </summary>
    /// <param name="game">The simulated game state.</param>
    /// <param name="faction">The faction whose facilities are counted.</param>
    /// <param name="buildingType">The production-facility type.</param>
    /// <returns>The projected production-facility count.</returns>
    private static int CountProjectedProductionFacilities(
        GameRoot game,
        Faction faction,
        BuildingType buildingType
    )
    {
        return game.GetSceneNodesByOwnerInstanceID<Building>(faction.InstanceID)
            .Count(building => building.GetBuildingType() == buildingType);
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
        return building.IsPlanetaryShieldGenerator();
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
}
