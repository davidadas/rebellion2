using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

public static partial class HeadlessSimulationRunner
{
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
        int requiredBombardmentStrength = PlanetaryAssaultResolver.IsBlockedByShields(
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

        int leadershipBonus = PlanetaryAssaultResolver.GetLeadershipBonus(
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
        int leadershipBonus = PlanetaryAssaultResolver.GetLeadershipBonus(
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
    /// Builds the final production-facility distribution for one faction.
    /// </summary>
    /// <param name="game">The game state to inspect.</param>
    /// <param name="faction">The faction whose planets are summarized.</param>
    /// <returns>Owned planets containing at least one completed production facility.</returns>
    private static ProductionFacilityPlanetSummary[] BuildProductionFacilityPlanetSummaries(
        GameRoot game,
        Faction faction
    )
    {
        return game.GetSceneNodesByOwnerInstanceID<Planet>(faction.InstanceID)
            .Select(planet => new ProductionFacilityPlanetSummary
            {
                PlanetId = planet.InstanceID,
                PlanetName = planet.GetDisplayName(),
                ConstructionFacilities = planet.GetBuildingTypeCount(
                    BuildingType.ConstructionFacility
                ),
                Shipyards = planet.GetBuildingTypeCount(BuildingType.Shipyard),
                TrainingFacilities = planet.GetBuildingTypeCount(BuildingType.TrainingFacility),
            })
            .Where(summary =>
                summary.ConstructionFacilities > 0
                || summary.Shipyards > 0
                || summary.TrainingFacilities > 0
            )
            .OrderBy(summary => summary.PlanetId, StringComparer.Ordinal)
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

    /// <summary>
    /// Enumerates faction-owned nodes from the ownership indexes without traversing the galaxy.
    /// </summary>
    /// <typeparam name="T">The scene-node type to retrieve.</typeparam>
    /// <param name="game">The game containing the faction indexes.</param>
    /// <returns>The active owned nodes of the requested type.</returns>
    private static IEnumerable<T> GetFactionOwnedNodes<T>(GameRoot game)
        where T : ISceneNode
    {
        return game.GetFactions().SelectMany(faction => faction.GetOwnedUnitsByType<T>());
    }

    private sealed class ManufacturingIdleTracker
    {
        // Manufacturing queues and resource constraints persist for many ticks; weighting a
        // 25-tick sample preserves long-run idle-capacity trends without hot-path graph polling.
        public const int SampleInterval = 25;

        private readonly Dictionary<string, FactionIdleCounters> _factions = new();

        /// <summary>
        /// Samples idle manufacturing capacity and weights it across the sampling interval.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        public void RecordSample(GameRoot game)
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
                    counters.BuildingIdlePlanetTicks += SampleInterval;
                    counters.BuildingIdleCapacityTicks += idleCapacity * SampleInterval;
                    planetCounters.BuildingIdleTicks += SampleInterval;
                    planetCounters.BuildingIdleCapacityTicks += idleCapacity * SampleInterval;
                    break;
                case ManufacturingType.Ship:
                    counters.ShipIdlePlanetTicks += SampleInterval;
                    counters.ShipIdleCapacityTicks += idleCapacity * SampleInterval;
                    planetCounters.ShipIdleTicks += SampleInterval;
                    planetCounters.ShipIdleCapacityTicks += idleCapacity * SampleInterval;
                    break;
                case ManufacturingType.Troop:
                    counters.TroopIdlePlanetTicks += SampleInterval;
                    counters.TroopIdleCapacityTicks += idleCapacity * SampleInterval;
                    planetCounters.TroopIdleTicks += SampleInterval;
                    planetCounters.TroopIdleCapacityTicks += idleCapacity * SampleInterval;
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
                FundedCapacityTicks += idleCapacity * SampleInterval;
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
