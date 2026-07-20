using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production proposals from current demand.
    /// </summary>
    public sealed class AIProductionPlanner : IAIProposalPlanner
    {
        private const int _percentageScale = 100;
        private const int _primaryLaserDivisor = 6;
        private const int _primaryWeaponWeight = 100;
        private const int _roleMetricScale = 10;
        private const int _weaponArcCount = 4;

        private readonly AIProductionDemandGenerator _demandGenerator =
            new AIProductionDemandGenerator();

        /// <summary>
        /// Returns production proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production proposals generated for this faction.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProductionDemand> demands = _demandGenerator.Generate(context);
            return GenerateProposals(context, demands);
        }

        /// <summary>
        /// Generates manufacture proposals for demand items.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">Demand items to satisfy.</param>
        /// <returns>Manufacture proposals generated for the demands.</returns>
        private List<AIProposal> GenerateProposals(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            List<AIProposal> proposals = new List<AIProposal>();

            if (context?.Faction == null || demands == null || demands.Count == 0)
                return proposals;

            foreach (AIProductionDemand demand in demands)
            {
                AddManufactureProposal(context, demand, proposals);
            }

            return proposals;
        }

        /// <summary>
        /// Adds manufacture proposals for one demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddManufactureProposal(
            AITurnContext context,
            AIProductionDemand demand,
            List<AIProposal> proposals
        )
        {
            Technology product = GetUnlockedTechnology(context, demand);
            if (product == null)
                return;

            foreach (Planet producerPlanet in FindProducerPlanets(context, demand))
            {
                AIManufactureProposal proposal = new AIManufactureProposal(
                    demand,
                    producerPlanet,
                    product
                );

                if (proposal.CanExecute(context))
                    proposals.Add(proposal);
            }
        }

        /// <summary>
        /// Returns the unlocked technology that can satisfy a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedTechnology(AITurnContext context, AIProductionDemand demand)
        {
            if (demand == null)
                return null;

            return demand.Kind switch
            {
                AIProductionDemandKind.Mine
                or AIProductionDemandKind.Refinery
                or AIProductionDemandKind.ConstructionFacility
                or AIProductionDemandKind.Shipyard
                or AIProductionDemandKind.TrainingFacility
                or AIProductionDemandKind.HeadquartersDefense => GetUnlockedBuildingTechnology(
                    context,
                    demand.BuildingType
                ),
                AIProductionDemandKind.FleetCapitalShip
                or AIProductionDemandKind.FleetStarfighter
                or AIProductionDemandKind.FleetRegiment
                or AIProductionDemandKind.LocalStarfighterReserve
                or AIProductionDemandKind.GarrisonRegimentReserve
                or AIProductionDemandKind.SpecialForces
                or AIProductionDemandKind.FleetSeedCapitalShip => GetUnlockedUnitTechnology(
                    context,
                    demand
                ),
                _ => null,
            };
        }

        /// <summary>
        /// Returns the unlocked building technology for a building type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Building type to manufacture.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedBuildingTechnology(
            AITurnContext context,
            BuildingType buildingType
        )
        {
            if (context?.Faction == null || buildingType == BuildingType.None)
                return null;

            int maintenanceBudget = System.Math.Max(
                0,
                context.Faction.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor
            );
            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Building)
                .Where(technology =>
                    technology.GetReference() is Building building
                    && building.GetBuildingType() == buildingType
                    && building.HasAllowedOwnerInstanceID(context.Faction.InstanceID)
                    && building.MaintenanceCost <= maintenanceBudget
                )
                .OrderByDescending(technology =>
                    GetBuildingCapability((Building)technology.GetReference())
                )
                .ThenByDescending(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetMaintenanceCost())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        private static int GetBuildingCapability(Building building)
        {
            return building.GetBuildingType() switch
            {
                BuildingType.ConstructionFacility
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.Mine
                or BuildingType.Refinery => building.ProcessRate > 0
                    ? -building.ProcessRate
                    : int.MinValue,
                BuildingType.Weapon => building.WeaponPower,
                BuildingType.Defense => building.ShieldStrength,
                _ => int.MinValue,
            };
        }

        /// <summary>
        /// Returns the unlocked unit technology for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedUnitTechnology(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            if (context?.Faction == null || demand == null)
                return null;

            return demand.Kind switch
            {
                AIProductionDemandKind.FleetCapitalShip => GetUnlockedCapitalShipTechnology(
                    context,
                    demand
                ),
                AIProductionDemandKind.FleetSeedCapitalShip => GetUnlockedCapitalShipTechnology(
                    context,
                    demand
                ),
                AIProductionDemandKind.FleetStarfighter => GetUnlockedStarfighterTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIProductionDemandKind.FleetRegiment => GetUnlockedRegimentTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIProductionDemandKind.LocalStarfighterReserve => GetUnlockedStarfighterTechnology(
                    context,
                    null
                ),
                AIProductionDemandKind.GarrisonRegimentReserve => GetUnlockedRegimentTechnology(
                    context,
                    null
                ),
                AIProductionDemandKind.SpecialForces => GetUnlockedSpecialForcesTechnology(
                    context,
                    demand.ProductTypeId
                ),
                _ => null,
            };
        }

        private Technology GetUnlockedSpecialForcesTechnology(
            AITurnContext context,
            string productTypeId
        )
        {
            if (string.IsNullOrEmpty(productTypeId))
                return null;

            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                .Where(technology =>
                    technology.GetReference() is SpecialForces specialForces
                    && specialForces.GetTypeID() == productTypeId
                )
                .OrderBy(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .FirstOrDefault();
        }

        private Technology GetUnlockedCapitalShipTechnology(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            if (context?.Faction == null || demand == null)
                return null;

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int maintenanceBudget = GetCapitalShipMaintenanceBudget(context);
            Technology selectedTechnology = null;
            long selectedMetric = long.MinValue;

            foreach (
                Technology technology in context.Faction.GetUnlockedTechnologies(
                    ManufacturingType.Ship
                )
            )
            {
                if (technology.GetReference() is not CapitalShip capitalShip)
                    continue;

                if (!capitalShip.HasAllowedOwnerInstanceID(context.Faction.InstanceID))
                    continue;

                if (!CanFillCapitalShipRole(context, capitalShip, demand.CapitalShipRole))
                    continue;

                if (capitalShip.MaintenanceCost > maintenanceBudget)
                    continue;

                long metric = GetCapitalShipRoleMetric(capitalShip, demand.CapitalShipRole);
                if (metric < selectedMetric)
                    continue;

                if (
                    metric == selectedMetric
                    && context.Random.NextInt(0, _percentageScale)
                        >= config.CapitalShipTieReplacementPercent
                )
                    continue;

                selectedTechnology = technology;
                selectedMetric = metric;
            }

            return selectedTechnology;
        }

        private int GetCapitalShipMaintenanceBudget(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int allocatedMaintenance = ScaleByPercent(
                context.Faction.MaintenanceCapacity,
                config.CapitalMaintenanceAllocationPercent
            );
            int targetCapitalMaintenance = ScaleByPercent(
                allocatedMaintenance,
                config.CapitalMaintenanceSafetyPercent
            );
            int committedCapitalMaintenance = context
                .Faction.GetOwnedUnitsByType<CapitalShip>()
                .Where(IsCommittedCapitalShip)
                .Sum(capitalShip => capitalShip.MaintenanceCost);
            int budget = System.Math.Max(0, targetCapitalMaintenance - committedCapitalMaintenance);

            return context.Faction.ProjectedMaintenanceHeadroom < budget ? 0 : budget;
        }

        private static bool IsCommittedCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip?.ManufacturingStatus
                is ManufacturingStatus.Complete
                    or ManufacturingStatus.Building;
        }

        private static int ScaleByPercent(int value, int percent)
        {
            return (int)((long)value * percent / _percentageScale);
        }

        private static bool CanFillCapitalShipRole(
            AITurnContext context,
            CapitalShip capitalShip,
            AICapitalShipProductionRole role
        )
        {
            if (
                context.Game.Config.Combat.Bombardment.PlanetDestroyingCapitalShipTypeIDs?.Contains(
                    capitalShip.GetTypeID()
                ) == true
            )
                return false;

            return role switch
            {
                AICapitalShipProductionRole.General => !capitalShip.HasGravityWell
                    && GetMaximumPrimaryWeaponWeight(capitalShip) > 0,
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity > 0
                    && !capitalShip.HasGravityWell
                    && GetMaximumPrimaryWeaponWeight(capitalShip) == 0,
                AICapitalShipProductionRole.Bombardment => capitalShip.Bombardment > 0,
                _ => false,
            };
        }

        private static long GetCapitalShipRoleMetric(
            CapitalShip capitalShip,
            AICapitalShipProductionRole role
        )
        {
            return role switch
            {
                AICapitalShipProductionRole.General => GetPrimaryWeaponMetric(capitalShip)
                    * _roleMetricScale
                    / System.Math.Max(1, capitalShip.MaintenanceCost),
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity,
                AICapitalShipProductionRole.Bombardment => capitalShip.Bombardment > 0 ? 1 : 0,
                _ => 0,
            };
        }

        private static long GetPrimaryWeaponMetric(CapitalShip capitalShip)
        {
            long maximumWeight = 0;
            int selectedWeaponCount = 0;

            for (int arc = 0; arc < _weaponArcCount; arc++)
            {
                int turbolasers = GetWeaponCount(capitalShip, PrimaryWeaponType.Turbolaser, arc);
                int ionCannons = GetWeaponCount(capitalShip, PrimaryWeaponType.IonCannon, arc);
                int laserCannons = GetWeaponCount(capitalShip, PrimaryWeaponType.LaserCannon, arc);
                long weight =
                    (long)_primaryWeaponWeight * turbolasers
                    + (long)_primaryWeaponWeight * ionCannons
                    + (long)_primaryWeaponWeight * laserCannons / _primaryLaserDivisor;
                if (weight <= maximumWeight)
                    continue;

                maximumWeight = weight;
                selectedWeaponCount = turbolasers + ionCannons + laserCannons;
            }

            if (selectedWeaponCount <= 0)
                return 0;

            return (long)capitalShip.WeaponRecharge * maximumWeight / selectedWeaponCount;
        }

        private static long GetMaximumPrimaryWeaponWeight(CapitalShip capitalShip)
        {
            long maximumWeight = 0;
            for (int arc = 0; arc < _weaponArcCount; arc++)
            {
                long weight =
                    (long)_primaryWeaponWeight
                        * GetWeaponCount(capitalShip, PrimaryWeaponType.Turbolaser, arc)
                    + (long)_primaryWeaponWeight
                        * GetWeaponCount(capitalShip, PrimaryWeaponType.IonCannon, arc)
                    + (long)_primaryWeaponWeight
                        * GetWeaponCount(capitalShip, PrimaryWeaponType.LaserCannon, arc)
                        / _primaryLaserDivisor;
                maximumWeight = System.Math.Max(maximumWeight, weight);
            }

            return maximumWeight;
        }

        private static int GetWeaponCount(
            CapitalShip capitalShip,
            PrimaryWeaponType weaponType,
            int arc
        )
        {
            if (
                capitalShip?.PrimaryWeapons == null
                || !capitalShip.PrimaryWeapons.TryGetValue(weaponType, out int[] values)
                || values == null
                || arc < 0
                || arc >= values.Length
            )
                return 0;

            return values[arc];
        }

        /// <summary>
        /// Returns the unlocked starfighter technology for a fleet demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving the starfighter.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedStarfighterTechnology(AITurnContext context, Fleet fleet)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            List<Technology> technologies = context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Where(technology => technology.GetReference() is Starfighter)
                .ToList();
            List<Technology> preferredTechnologies = technologies
                .Where(technology =>
                    CountFleetUnitsByType<Starfighter>(fleet, technology.GetReference().GetTypeID())
                    < config.MaxDuplicateStarfighterTypePerFleet
                )
                .ToList();

            return (preferredTechnologies.Count > 0 ? preferredTechnologies : technologies)
                .OrderByDescending(technology =>
                    ScoreStarfighterTechnology(
                        config,
                        fleet,
                        (Starfighter)technology.GetReference()
                    )
                )
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the unlocked regiment technology for a fleet demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving the regiment.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedRegimentTechnology(AITurnContext context, Fleet fleet)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            List<Technology> technologies = context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                .Where(technology => technology.GetReference() is Regiment)
                .ToList();
            List<Technology> preferredTechnologies = technologies
                .Where(technology =>
                    CountFleetUnitsByType<Regiment>(fleet, technology.GetReference().GetTypeID())
                    < config.MaxDuplicateRegimentTypePerDestination
                )
                .ToList();

            return (preferredTechnologies.Count > 0 ? preferredTechnologies : technologies)
                .OrderByDescending(technology =>
                    ScoreRegimentTechnology(config, fleet, (Regiment)technology.GetReference())
                )
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the score for a starfighter technology.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="fleet">Fleet receiving the starfighter.</param>
        /// <param name="starfighter">Starfighter to score.</param>
        /// <returns>The starfighter technology score.</returns>
        private double ScoreStarfighterTechnology(
            GameConfig.AISelectionConfig config,
            Fleet fleet,
            Starfighter starfighter
        )
        {
            double score =
                starfighter.LaserCannon * config.StarfighterEscortWeight
                + starfighter.IonCannon * config.StarfighterInterceptorWeight
                + starfighter.Torpedoes * config.StarfighterBomberWeight;

            if (starfighter.IonCannon > 0 && !FleetHasIonStarfighter(fleet))
                score += config.StarfighterMissingInterceptorBoost;

            if (starfighter.Torpedoes > 0 && !FleetHasTorpedoStarfighter(fleet))
                score += config.StarfighterMissingBomberBoost;

            score -=
                CountFleetUnitsByType<Starfighter>(fleet, starfighter.GetTypeID())
                * config.LocalDuplicatePenaltyPerSelection;

            return score;
        }

        /// <summary>
        /// Returns the score for a regiment technology.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="fleet">Fleet receiving the regiment.</param>
        /// <param name="regiment">Regiment to score.</param>
        /// <returns>The regiment technology score.</returns>
        private double ScoreRegimentTechnology(
            GameConfig.AISelectionConfig config,
            Fleet fleet,
            Regiment regiment
        )
        {
            return regiment.AttackRating * config.RegimentAttackWeight
                + regiment.DefenseRating * config.RegimentDefenseWeight
                + regiment.BombardmentDefense * config.RegimentBombardmentDefenseWeight
                + config.RegimentFleetAttackBoost
                - regiment.MaintenanceCost * config.RegimentMaintenanceCostWeight
                - CountFleetUnitsByType<Regiment>(fleet, regiment.GetTypeID())
                    * config.LocalDuplicatePenaltyPerSelection;
        }

        /// <summary>
        /// Returns producer planets eligible for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <returns>Eligible producer planets.</returns>
        private IEnumerable<Planet> FindProducerPlanets(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            if (context?.Assessment == null || demand?.Destination == null)
                return Enumerable.Empty<Planet>();

            Planet destinationPlanet = GetDestinationPlanet(context, demand);
            return context
                .Assessment.OwnedPlanets.Where(planet =>
                    CanProduce(planet, demand.ManufacturingType)
                )
                .OrderBy(planet =>
                    destinationPlanet == null ? 0 : destinationPlanet.GetRawDistanceTo(planet)
                )
                .ThenByDescending(planet => planet.GetProductionRate(demand.ManufacturingType))
                .ThenBy(planet => planet.InstanceID);
        }

        /// <summary>
        /// Returns the destination planet for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to inspect.</param>
        /// <returns>The destination planet, or null.</returns>
        private Planet GetDestinationPlanet(AITurnContext context, AIProductionDemand demand)
        {
            if (demand?.Destination is Planet planet)
                return planet;

            if (demand?.Destination is Fleet fleet)
                return context.Assessment.GetFleetPlanet(fleet);

            return null;
        }

        /// <summary>
        /// Returns whether a planet can produce a manufacturing type.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="manufacturingType">Manufacturing type to produce.</param>
        /// <returns>True if the planet can produce the type.</returns>
        private bool CanProduce(Planet planet, ManufacturingType manufacturingType)
        {
            if (planet == null)
                return false;

            return planet.IsColonized
                && !planet.IsDestroyed
                && planet.GetAvailableManufacturingCapacity(manufacturingType) > 0;
        }

        /// <summary>
        /// Returns how many fleet units match a type id.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="typeId">Unit type id to count.</param>
        /// <returns>The matching unit count.</returns>
        private int CountFleetUnitsByType<T>(Fleet fleet, string typeId)
            where T : class, IManufacturable
        {
            if (fleet == null || string.IsNullOrEmpty(typeId))
                return 0;

            if (typeof(T) == typeof(Starfighter))
                return fleet
                    .GetStarfighters()
                    .Count(starfighter => starfighter.GetTypeID() == typeId);

            if (typeof(T) == typeof(Regiment))
                return fleet.GetRegiments().Count(regiment => regiment.GetTypeID() == typeId);

            if (typeof(T) == typeof(CapitalShip))
                return fleet.CapitalShips.Count(capitalShip => capitalShip.GetTypeID() == typeId);

            return 0;
        }

        /// <summary>
        /// Returns whether a fleet already has an ion starfighter.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet has an ion starfighter.</returns>
        private bool FleetHasIonStarfighter(Fleet fleet)
        {
            return fleet?.GetStarfighters().Any(starfighter => starfighter.IonCannon > 0) == true;
        }

        /// <summary>
        /// Returns whether a fleet already has a torpedo starfighter.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet has a torpedo starfighter.</returns>
        private bool FleetHasTorpedoStarfighter(Fleet fleet)
        {
            return fleet?.GetStarfighters().Any(starfighter => starfighter.Torpedoes > 0) == true;
        }
    }
}
