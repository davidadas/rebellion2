using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
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
                or AIProductionDemandKind.TrainingFacility => GetUnlockedBuildingTechnology(
                    context.Faction,
                    demand.BuildingType
                ),
                AIProductionDemandKind.FleetCapitalShip
                or AIProductionDemandKind.FleetStarfighter
                or AIProductionDemandKind.FleetRegiment
                or AIProductionDemandKind.LocalStarfighterReserve
                or AIProductionDemandKind.GarrisonRegimentReserve => GetUnlockedUnitTechnology(
                    context,
                    demand
                ),
                _ => null,
            };
        }

        /// <summary>
        /// Returns the unlocked building technology for a building type.
        /// </summary>
        /// <param name="faction">The faction to inspect.</param>
        /// <param name="buildingType">Building type to manufacture.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedBuildingTechnology(Faction faction, BuildingType buildingType)
        {
            if (faction == null || buildingType == BuildingType.None)
                return null;

            return faction
                .GetUnlockedTechnologies(ManufacturingType.Building)
                .Where(technology =>
                    technology.GetReference() is Building building
                    && building.GetBuildingType() == buildingType
                )
                .OrderBy(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
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
                    demand,
                    demand.DestinationFleet
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
                _ => null,
            };
        }

        /// <summary>
        /// Returns the unlocked capital ship technology for a fleet demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <param name="fleet">Fleet receiving the ship.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedCapitalShipTechnology(
            AITurnContext context,
            AIProductionDemand demand,
            Fleet fleet
        )
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            List<Technology> technologies = context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Where(technology => technology.GetReference() is CapitalShip)
                .ToList();
            List<Technology> routineTechnologies = technologies
                .Where(technology =>
                    technology.GetReference() is CapitalShip capitalShip
                    && !IsPremiumCapitalShip(config, capitalShip)
                )
                .ToList();
            List<Technology> selectableTechnologies =
                routineTechnologies.Count > 0
                    ? routineTechnologies
                    : technologies
                        .Where(technology =>
                            technology.GetReference() is CapitalShip capitalShip
                            && CanSelectPremiumCapitalShip(context, config, capitalShip)
                        )
                        .ToList();
            List<Technology> combatTechnologies = selectableTechnologies
                .Where(technology =>
                    technology.GetReference() is CapitalShip capitalShip
                    && CanServeFleetCombatDemand(capitalShip)
                )
                .ToList();
            List<Technology> candidateTechnologies =
                NeedsFleetCombat(context, fleet) && combatTechnologies.Count > 0
                    ? combatTechnologies
                    : selectableTechnologies;
            List<Technology> preferredTechnologies = candidateTechnologies
                .Where(technology =>
                    CountFleetUnitsByType<CapitalShip>(fleet, technology.GetReference().GetTypeID())
                    < config.MaxDuplicateCapitalTypePerFleet
                )
                .ToList();

            return (preferredTechnologies.Count > 0 ? preferredTechnologies : candidateTechnologies)
                .OrderByDescending(technology =>
                    ScoreCapitalShipTechnology(
                        config,
                        demand,
                        fleet,
                        (CapitalShip)technology.GetReference()
                    )
                )
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a capital ship can satisfy combat demand.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the capital ship can serve combat demand.</returns>
        private bool CanServeFleetCombatDemand(CapitalShip capitalShip)
        {
            return capitalShip.GetPrimaryWeaponStrength() > 0
                && !IsPureTransportCapitalShip(capitalShip);
        }

        /// <summary>
        /// Returns whether a fleet needs more combat strength.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet needs combat strength.</returns>
        private bool NeedsFleetCombat(AITurnContext context, Fleet fleet)
        {
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);
            if (targetPlanet == null)
                return true;

            return GetProjectedFleetCombatValue(fleet)
                < context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
        }

        /// <summary>
        /// Returns current or committed combat value for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The projected combat value.</returns>
        private static int GetProjectedFleetCombatValue(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            int committedCapitalCombat = fleet
                .CapitalShips.Where(IsPresentOrUnderConstruction)
                .Sum(ship => ship.GetPrimaryWeaponStrength());
            return System.Math.Max(fleet.GetCombatValue(), committedCapitalCombat);
        }

        /// <summary>
        /// Returns whether a capital ship only provides transport capacity.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the capital ship is only a transport.</returns>
        private bool IsPureTransportCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip.HasRole(CapitalShipRole.Transport)
                && !capitalShip.HasAnyRole(
                    CapitalShipRole.PrimaryLine,
                    CapitalShipRole.SecondaryLine,
                    CapitalShipRole.Escort,
                    CapitalShipRole.Interdictor,
                    CapitalShipRole.Carrier,
                    CapitalShipRole.Flagship
                );
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
            return GetUnlockedFleetTechnology<Starfighter>(
                context,
                fleet,
                ManufacturingType.Ship,
                config.MaxDuplicateStarfighterTypePerFleet,
                starfighter => ScoreStarfighterTechnology(config, fleet, starfighter)
            );
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
            return GetUnlockedFleetTechnology<Regiment>(
                context,
                fleet,
                ManufacturingType.Troop,
                config.MaxDuplicateRegimentTypePerDestination,
                regiment => ScoreRegimentTechnology(config, regiment)
            );
        }

        /// <summary>
        /// Selects an unlocked fleet-unit technology through shared diversity and tie-break rules.
        /// </summary>
        /// <typeparam name="T">The fleet-unit type referenced by eligible technologies.</typeparam>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet receiving the manufactured unit.</param>
        /// <param name="manufacturingType">The technology catalog to inspect.</param>
        /// <param name="maximumDuplicateCount">The preferred per-type duplicate limit.</param>
        /// <param name="getScore">Returns the unit-specific selection score.</param>
        /// <returns>The selected technology, or null when none is unlocked.</returns>
        private Technology GetUnlockedFleetTechnology<T>(
            AITurnContext context,
            Fleet fleet,
            ManufacturingType manufacturingType,
            int maximumDuplicateCount,
            Func<T, double> getScore
        )
            where T : class, IManufacturable
        {
            List<Technology> technologies = context
                .Faction.GetUnlockedTechnologies(manufacturingType)
                .Where(technology => technology.GetReference() is T)
                .ToList();
            List<Technology> preferredTechnologies = technologies
                .Where(technology =>
                    CountFleetUnitsByType<T>(fleet, technology.GetReference().GetTypeID())
                    < maximumDuplicateCount
                )
                .ToList();

            return (preferredTechnologies.Count > 0 ? preferredTechnologies : technologies)
                .OrderByDescending(technology => getScore((T)technology.GetReference()))
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

            return score;
        }

        /// <summary>
        /// Returns the score for a regiment technology.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="regiment">Regiment to score.</param>
        /// <returns>The regiment technology score.</returns>
        private double ScoreRegimentTechnology(
            GameConfig.AISelectionConfig config,
            Regiment regiment
        )
        {
            return regiment.AttackRating * config.RegimentAttackWeight
                + regiment.DefenseRating * config.RegimentDefenseWeight
                + regiment.BombardmentDefense * config.RegimentBombardmentDefenseWeight
                + config.RegimentFleetAttackBoost
                - regiment.MaintenanceCost * config.RegimentMaintenanceCostWeight;
        }

        /// <summary>
        /// Returns the score for a capital ship technology.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <param name="fleet">Fleet receiving the capital ship.</param>
        /// <param name="capitalShip">Capital ship to score.</param>
        /// <returns>The capital ship technology score.</returns>
        private double ScoreCapitalShipTechnology(
            GameConfig.AISelectionConfig config,
            AIProductionDemand demand,
            Fleet fleet,
            CapitalShip capitalShip
        )
        {
            int combatStrength = capitalShip.GetPrimaryWeaponStrength();
            int requiredCombat = demand?.QuantityNeeded ?? combatStrength;
            int usefulCombat =
                requiredCombat <= 0
                    ? combatStrength
                    : System.Math.Min(combatStrength, requiredCombat);
            int excessCombat =
                requiredCombat <= 0 ? 0 : System.Math.Max(0, combatStrength - requiredCombat);
            double score =
                usefulCombat * config.CapitalCombatWeight
                + capitalShip.StarfighterCapacity * config.CapitalStarfighterCapacityWeight
                + capitalShip.RegimentCapacity * config.CapitalRegimentCapacityWeight
                + capitalShip.Bombardment * config.CapitalBombardmentWeight
                - excessCombat * config.CapitalExcessCombatPenaltyWeight
                - capitalShip.ConstructionCost * config.CapitalConstructionCostWeight
                - capitalShip.MaintenanceCost * config.CapitalMaintenanceCostWeight;

            if (capitalShip.HasGravityWell)
                score += config.CapitalGravityWellWeight;

            if (fleet?.CapitalShips.Count == 0)
                score += config.CapitalEmptyFleetCombatBoost;

            if (fleet?.GetExcessStarfighterCapacity() <= 0)
                score += config.CapitalMissingStarfighterCapacityBoost;

            if (fleet?.GetExcessRegimentCapacity() <= 0)
                score += config.CapitalMissingRegimentCapacityBoost;

            if (fleet?.CapitalShips.Any(ship => ship.HasGravityWell) == false)
                score += config.CapitalMissingGravityWellBoost;

            return score;
        }

        /// <summary>
        /// Returns whether a premium capital ship can be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="capitalShip">Capital ship to inspect.</param>
        /// <returns>True if the capital ship can be selected.</returns>
        private bool CanSelectPremiumCapitalShip(
            AITurnContext context,
            GameConfig.AISelectionConfig config,
            CapitalShip capitalShip
        )
        {
            if (config.PremiumCapitalConstructionCostThreshold <= 0)
                return true;

            if (config.MaxPremiumCapitalsPerFaction <= 0)
                return true;

            if (!IsPremiumCapitalShip(config, capitalShip))
                return true;

            return CountPremiumCapitalShips(context, config) < config.MaxPremiumCapitalsPerFaction;
        }

        /// <summary>
        /// Returns whether a capital ship is a premium production choice.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="capitalShip">Capital ship to inspect.</param>
        /// <returns>True if the capital ship is premium.</returns>
        private bool IsPremiumCapitalShip(
            GameConfig.AISelectionConfig config,
            CapitalShip capitalShip
        )
        {
            return config.PremiumCapitalConstructionCostThreshold > 0
                && capitalShip.ConstructionCost >= config.PremiumCapitalConstructionCostThreshold;
        }

        /// <summary>
        /// Returns the faction's committed premium capital ship count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI selection configuration.</param>
        /// <returns>The premium capital ship count.</returns>
        private int CountPremiumCapitalShips(
            AITurnContext context,
            GameConfig.AISelectionConfig config
        )
        {
            return context
                .Faction.GetOwnedUnitsByType<CapitalShip>()
                .Count(capitalShip =>
                    capitalShip.ConstructionCost >= config.PremiumCapitalConstructionCostThreshold
                    && IsPresentOrUnderConstruction(capitalShip)
                );
        }

        /// <summary>
        /// Returns whether a capital ship is present or being built.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the capital ship is present or under construction.</returns>
        private static bool IsPresentOrUnderConstruction(CapitalShip capitalShip)
        {
            return capitalShip?.ManufacturingStatus
                    is ManufacturingStatus.Complete
                        or ManufacturingStatus.Building
                && capitalShip.Movement == null;
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
