using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Game.World;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds fleet proposals for attack orders and fleet reinforcement.
    /// </summary>
    public sealed class AIFleetPlanner : IAIProposalPlanner
    {
        /// <summary>
        /// Returns fleet proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Fleet proposals generated for this faction.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();

            if (context?.Game == null || context.Faction == null)
                return proposals;

            foreach (Fleet fleet in context.Assessment.OwnedFleets)
                AddFleetProposal(context, fleet, proposals);

            AddCapitalShipTransferProposals(context, proposals);
            // AddAttackFleetCreationProposals(context, proposals);

            return proposals;
        }

        /// <summary>
        /// Adds proposals for one fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddFleetProposal(
            AITurnContext context,
            Fleet fleet,
            List<AIProposal> proposals
        )
        {
            FleetOrder order = fleet.Order;
            Planet currentPlanet = context.Assessment.GetFleetPlanet(fleet);

            if (order == null)
            {
                AddAttackOrderProposals(context, fleet, currentPlanet, proposals);
                return;
            }

            if (order.OrderType != FleetOrderType.Attack)
                return;

            Planet targetPlanet = context.Game.GetSceneNodeByInstanceID<Planet>(
                order.TargetPlanetId
            );
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (
                targetPlanet == null
                || string.IsNullOrEmpty(targetOwnerId)
                || targetOwnerId == context.Faction.InstanceID
            )
            {
                fleet.Order = null;
                return;
            }

            proposals.Add(
                new AIFleetAttackProposal(fleet, order.OrderType, order.Status, targetPlanet)
            );
        }

        /// <summary>
        /// Adds attack order proposals for an idle fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="currentPlanet">The fleet's current planet.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddAttackOrderProposals(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet,
            List<AIProposal> proposals
        )
        {
            if (!CanStartAttackOrder(fleet))
                return;

            if (HasAttackFleetStillAssembling(context))
                return;

            foreach (Planet targetPlanet in context.Assessment.EnemyPlanets)
            {
                if (HasAttackFleetForTarget(context, targetPlanet, fleet))
                    continue;

                proposals.Add(
                    new AIFleetAttackProposal(
                        fleet,
                        FleetOrderType.Attack,
                        GetInitialAttackStatus(currentPlanet, targetPlanet),
                        targetPlanet
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether a fleet can receive a new attack order.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet can start an attack order.</returns>
        private bool CanStartAttackOrder(Fleet fleet)
        {
            if (fleet.RoleType != FleetRoleType.Battle)
                return false;

            return fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips();
        }

        /// <summary>
        /// Returns the initial status for a new attack order.
        /// </summary>
        /// <param name="currentPlanet">The fleet's current planet.</param>
        /// <param name="targetPlanet">The attack target planet.</param>
        /// <returns>The initial order status.</returns>
        private FleetOrderStatus GetInitialAttackStatus(Planet currentPlanet, Planet targetPlanet)
        {
            return currentPlanet == targetPlanet
                ? FleetOrderStatus.Ready
                : FleetOrderStatus.Staging;
        }

        /*
        private void AddAttackFleetCreationProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            if (!CanCreateAttackFleet(context))
                return;

            foreach (Planet targetPlanet in context.Assessment.EnemyPlanets)
            {
                if (HasAttackFleetForTarget(context, targetPlanet, null))
                    continue;

                Planet stagingPlanet = FindAttackStagingPlanet(context, targetPlanet);
                if (stagingPlanet == null)
                    continue;

                proposals.Add(
                    new AICreateFleetForOrderProposal(
                        context.Faction.InstanceID,
                        stagingPlanet,
                        FleetOrderType.Attack,
                        FleetOrderStatus.Building,
                        targetPlanet
                    )
                );
            }
        }
        */

        /// <summary>
        /// Adds transfer proposals that can reinforce an assembling attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddCapitalShipTransferProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            Fleet targetFleet = GetCapitalShipTransferTargetFleet(context);
            Planet targetPlanet = GetAttackTargetPlanet(context, targetFleet);
            if (!CanReceiveCapitalShipTransfer(context, targetFleet, targetPlanet))
                return;

            foreach (Fleet sourceFleet in context.Assessment.OwnedFleets)
            {
                AddCapitalShipTransferProposals(
                    context,
                    proposals,
                    sourceFleet,
                    targetFleet,
                    targetPlanet
                );
            }
        }

        /// <summary>
        /// Adds transfer proposals from one source fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal list to update.</param>
        /// <param name="sourceFleet">The fleet that may donate a capital ship.</param>
        /// <param name="targetFleet">The fleet that may receive a capital ship.</param>
        /// <param name="targetPlanet">The target assigned to the receiving fleet.</param>
        private void AddCapitalShipTransferProposals(
            AITurnContext context,
            List<AIProposal> proposals,
            Fleet sourceFleet,
            Fleet targetFleet,
            Planet targetPlanet
        )
        {
            if (!CanDonateCapitalShip(context, sourceFleet, targetFleet))
                return;

            foreach (
                CapitalShip capitalShip in sourceFleet
                    .CapitalShips.Where(capitalShip =>
                        CanTransferCapitalShipToAttackFleet(
                            context,
                            sourceFleet,
                            targetFleet,
                            targetPlanet,
                            capitalShip
                        )
                    )
                    .OrderByDescending(capitalShip =>
                        GetCapitalShipTransferValue(context, targetFleet, targetPlanet, capitalShip)
                    )
                    .ThenBy(capitalShip => capitalShip.InstanceID)
            )
            {
                proposals.Add(
                    new AITransferUnitProposal(
                        sourceFleet,
                        targetFleet,
                        capitalShip,
                        targetFleet,
                        targetPlanet
                    )
                );
            }
        }

        /*
        private bool CanCreateAttackFleet(AITurnContext context)
        {
            return context.Assessment.IdleBattleFleets.Count == 0
                && !HasAttackFleetStillAssembling(context);
        }
        */

        /// <summary>
        /// Returns whether any attack fleet is still waiting for enough force.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if an attack fleet is still assembling.</returns>
        private bool HasAttackFleetStillAssembling(AITurnContext context)
        {
            foreach (Fleet fleet in context.Assessment.AttackOrderedFleets)
            {
                Planet targetPlanet = context.Game.GetSceneNodeByInstanceID<Planet>(
                    fleet.Order?.TargetPlanetId
                );
                if (targetPlanet == null)
                    continue;

                string targetOwnerId = targetPlanet.GetOwnerInstanceID();
                if (
                    string.IsNullOrEmpty(targetOwnerId)
                    || targetOwnerId == context.Faction.InstanceID
                )
                    continue;

                if (!IsAttackReady(context, fleet, targetPlanet))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns whether an attack fleet already targets a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The planet to inspect.</param>
        /// <param name="ignoredFleet">Fleet excluded from the check.</param>
        /// <returns>True if another attack fleet targets the planet.</returns>
        private bool HasAttackFleetForTarget(
            AITurnContext context,
            Planet targetPlanet,
            Fleet ignoredFleet
        )
        {
            return context.Assessment.AttackOrderedFleets.Any(fleet =>
                fleet != ignoredFleet && IsAttackFleetAssignedToTarget(context, fleet, targetPlanet)
            );
        }

        /// <summary>
        /// Returns whether a fleet has an active attack order for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet to compare.</param>
        /// <returns>True if the fleet is assigned to the target.</returns>
        private bool IsAttackFleetAssignedToTarget(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            if (
                fleet?.Order?.OrderType != FleetOrderType.Attack
                || targetPlanet == null
                || fleet.Order.TargetPlanetId != targetPlanet.InstanceID
            )
                return false;

            string targetOwnerId = targetPlanet.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(targetOwnerId)
                && targetOwnerId != context.Faction.InstanceID;
        }

        /// <summary>
        /// Returns the active attack target for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The attack target planet, or null.</returns>
        private Planet GetAttackTargetPlanet(AITurnContext context, Fleet fleet)
        {
            string targetPlanetId = fleet?.Order?.TargetPlanetId;
            if (string.IsNullOrEmpty(targetPlanetId))
                return null;

            Planet targetPlanet = context.Game.GetSceneNodeByInstanceID<Planet>(targetPlanetId);
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == context.Faction.InstanceID)
                return null;

            return targetPlanet;
        }

        /// <summary>
        /// Returns whether a fleet can receive a capital ship transfer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <returns>True if the fleet can receive a transfer.</returns>
        private bool CanReceiveCapitalShipTransfer(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet
        )
        {
            if (
                targetFleet == null
                || targetPlanet == null
                || targetFleet.RoleType != FleetRoleType.Battle
                || targetFleet.Movement != null
                || targetFleet.IsInCombat
                || targetFleet.Order?.OrderType != FleetOrderType.Attack
            )
                return false;

            return !IsAttackReady(context, targetFleet, targetPlanet)
                && !CanFillCapitalShipNeedWithProduction(context, targetFleet, targetPlanet);
        }

        /// <summary>
        /// Returns the attack fleet most suitable for a capital ship transfer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The target fleet, or null.</returns>
        private Fleet GetCapitalShipTransferTargetFleet(AITurnContext context)
        {
            return context
                .Assessment.AttackOrderedFleets.Select(fleet => new
                {
                    Fleet = fleet,
                    TargetPlanet = GetAttackTargetPlanet(context, fleet),
                })
                .Where(candidate =>
                    CanReceiveCapitalShipTransfer(context, candidate.Fleet, candidate.TargetPlanet)
                )
                .OrderByDescending(candidate =>
                    GetAttackReadinessGateCount(context, candidate.Fleet, candidate.TargetPlanet)
                )
                .ThenByDescending(candidate =>
                    context.Assessment.GetPlanetValue(candidate.TargetPlanet)
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID)
                .Select(candidate => candidate.Fleet)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a source fleet may donate a capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="sourceFleet">The potential source fleet.</param>
        /// <param name="targetFleet">The potential target fleet.</param>
        /// <returns>True if the source fleet can donate.</returns>
        private bool CanDonateCapitalShip(
            AITurnContext context,
            Fleet sourceFleet,
            Fleet targetFleet
        )
        {
            if (
                sourceFleet == null
                || targetFleet == null
                || sourceFleet == targetFleet
                || sourceFleet.GetOwnerInstanceID() != targetFleet.GetOwnerInstanceID()
                || sourceFleet.Movement != null
                || sourceFleet.IsInCombat
                || sourceFleet.Order != null
            )
                return false;

            Planet sourcePlanet = context.Assessment.GetFleetPlanet(sourceFleet);
            return sourcePlanet != null
                && sourcePlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                && !sourcePlanet.IsHeadquarters;
        }

        /// <summary>
        /// Returns whether a source fleet can spare a specific capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="sourceFleet">The potential source fleet.</param>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the ship can be spared.</returns>
        private bool CanSourceFleetSpareCapitalShip(
            AITurnContext context,
            Fleet sourceFleet,
            CapitalShip capitalShip
        )
        {
            if (
                sourceFleet == null
                || capitalShip == null
                || capitalShip.ManufacturingStatus != ManufacturingStatus.Complete
                || capitalShip.Movement != null
            )
                return false;

            if (
                sourceFleet
                    .CapitalShips.Where(ship => ship != capitalShip)
                    .Count(ship =>
                        ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                    ) <= 0
            )
                return false;

            Planet sourcePlanet = context.Assessment.GetFleetPlanet(sourceFleet);
            if (
                sourcePlanet == null
                || sourcePlanet.GetOwnerInstanceID() != context.Faction.InstanceID
            )
                return false;

            int requiredDefense = sourcePlanet.IsHeadquarters
                ? context.Game.Config.AI.FleetDeployment.MinimumDefenseStrength
                : context.Assessment.GetHostileFleetCombatValue(sourcePlanet);
            if (requiredDefense <= 0)
                return true;

            return GetLocalDefenseAfterTransfer(context, sourceFleet, sourcePlanet, capitalShip)
                >= requiredDefense;
        }

        /// <summary>
        /// Returns local defense strength after a capital ship transfer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="sourceFleet">The source fleet.</param>
        /// <param name="sourcePlanet">The source fleet planet.</param>
        /// <param name="capitalShip">The capital ship being transferred.</param>
        /// <returns>The remaining local defense strength.</returns>
        private int GetLocalDefenseAfterTransfer(
            AITurnContext context,
            Fleet sourceFleet,
            Planet sourcePlanet,
            CapitalShip capitalShip
        )
        {
            int sourceCombatAfterTransfer =
                sourceFleet.GetCombatValue() - capitalShip.GetCombatValue();
            int otherLocalFleetCombat = context
                .Assessment.GetFriendlyFleets(sourcePlanet)
                .Where(fleet => fleet != sourceFleet && fleet.Movement == null)
                .Sum(context.Assessment.GetFleetCombatValue);
            return sourceCombatAfterTransfer + otherLocalFleetCombat;
        }

        /// <summary>
        /// Returns whether ship production can satisfy an attack fleet need.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The fleet being reinforced.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <returns>True if a useful capital ship can be manufactured.</returns>
        private bool CanManufactureUsefulCapitalShipForAttackFleet(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet
        )
        {
            if (!HasAvailableShipProductionLane(context))
                return false;

            return GetSelectableCapitalShipTechnologies(context)
                .Any(technology =>
                    technology.GetReference() is CapitalShip capitalShip
                    && capitalShip.HasAllowedOwnerInstanceID(context.Faction.InstanceID)
                    && HasMaintenanceHeadroomFor(context, capitalShip)
                    && CapitalShipWouldHelpAttackFleet(
                        context,
                        targetFleet,
                        targetPlanet,
                        capitalShip
                    )
                );
        }

        /// <summary>
        /// Returns whether any owned planet can currently produce ships.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if ship production capacity is available.</returns>
        private bool HasAvailableShipProductionLane(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Any(planet =>
                planet.IsColonized
                && !planet.IsDestroyed
                && planet.GetAvailableManufacturingCapacity(ManufacturingType.Ship) > 0
            );
        }

        /// <summary>
        /// Returns capital ship technologies eligible for attack fleet production.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Selectable capital ship technologies.</returns>
        private IEnumerable<Technology> GetSelectableCapitalShipTechnologies(AITurnContext context)
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

            if (routineTechnologies.Count > 0)
                return routineTechnologies;

            return technologies.Where(technology =>
                technology.GetReference() is CapitalShip capitalShip
                && CanSelectPremiumCapitalShip(context, config, capitalShip)
            );
        }

        /// <summary>
        /// Returns whether a capital ship is a premium production choice.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="capitalShip">The capital ship to inspect.</param>
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
        /// Returns whether a premium capital ship can be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the premium ship can be selected.</returns>
        private bool CanSelectPremiumCapitalShip(
            AITurnContext context,
            GameConfig.AISelectionConfig config,
            CapitalShip capitalShip
        )
        {
            if (!IsPremiumCapitalShip(config, capitalShip))
                return true;

            if (config.MaxPremiumCapitalsPerFaction <= 0)
                return true;

            return context
                    .Faction.GetOwnedUnitsByType<CapitalShip>()
                    .Count(ship =>
                        ship.ConstructionCost >= config.PremiumCapitalConstructionCostThreshold
                        && ship.IsCommittedToFleet()
                    ) < config.MaxPremiumCapitalsPerFaction;
        }

        /// <summary>
        /// Returns whether maintenance can support a capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if maintenance headroom is sufficient.</returns>
        private bool HasMaintenanceHeadroomFor(AITurnContext context, CapitalShip capitalShip)
        {
            int minimumHeadroom = context
                .Game
                .Config
                .AI
                .Selection
                .MinimumMaintenanceHeadroomAfterProduction;
            return context.Faction.ProjectedMaintenanceHeadroom - capitalShip.MaintenanceCost
                >= minimumHeadroom;
        }

        /// <summary>
        /// Returns whether a capital ship improves an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The fleet being reinforced.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the capital ship helps the fleet.</returns>
        private bool CapitalShipWouldHelpAttackFleet(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet,
            CapitalShip capitalShip
        )
        {
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);
            return GetProjectedFleetCombatValue(targetFleet) < requiredCombat
                    && capitalShip.GetPrimaryWeaponStrength() > 0
                || targetFleet.GetRegimentCapacity() < requiredRegiments
                    && capitalShip.GetRegimentCapacity() > 0;
        }

        private static int GetProjectedFleetCombatValue(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            int committedCapitalCombat = fleet
                .CapitalShips.Where(ship => ship?.IsCommittedToFleet() == true)
                .Sum(ship => ship.GetPrimaryWeaponStrength());
            return System.Math.Max(fleet.GetCombatValue(), committedCapitalCombat);
        }

        /// <summary>
        /// Returns whether production can satisfy a capital ship need.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The fleet being reinforced.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <returns>True if production can fill the need.</returns>
        private bool CanFillCapitalShipNeedWithProduction(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet
        )
        {
            return targetFleet != null
                && targetPlanet != null
                && CanManufactureUsefulCapitalShipForAttackFleet(
                    context,
                    targetFleet,
                    targetPlanet
                );
        }

        /// <summary>
        /// Returns whether a capital ship can transfer into an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="sourceFleet">The source fleet.</param>
        /// <param name="targetFleet">The target fleet.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The ship to inspect.</param>
        /// <returns>True if the capital ship can transfer.</returns>
        private bool CanTransferCapitalShipToAttackFleet(
            AITurnContext context,
            Fleet sourceFleet,
            Fleet targetFleet,
            Planet targetPlanet,
            CapitalShip capitalShip
        )
        {
            if (
                capitalShip == null
                || capitalShip.Movement != null
                || capitalShip.ManufacturingStatus != ManufacturingStatus.Complete
            )
                return false;

            if (!CanSourceFleetSpareCapitalShip(context, sourceFleet, capitalShip))
                return false;

            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);

            return targetFleet.GetCombatValue() < requiredCombat && capitalShip.GetCombatValue() > 0
                || targetFleet.GetCurrentRegimentCount() < requiredRegiments
                    && capitalShip.GetCurrentRegimentCount() > 0
                || targetFleet.GetRegimentCapacity() < requiredRegiments
                    && capitalShip.GetRegimentCapacity() > 0;
        }

        /// <summary>
        /// Returns the benefit of transferring a capital ship to an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The target fleet.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The ship to inspect.</param>
        /// <returns>The transfer value.</returns>
        private int GetCapitalShipTransferValue(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet,
            CapitalShip capitalShip
        )
        {
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);
            int combatGap = System.Math.Max(0, requiredCombat - targetFleet.GetCombatValue());
            int loadedRegimentGap = System.Math.Max(
                0,
                requiredRegiments - targetFleet.GetCurrentRegimentCount()
            );
            int regimentCapacityGap = System.Math.Max(
                0,
                requiredRegiments - targetFleet.GetRegimentCapacity()
            );

            return System.Math.Min(combatGap, capitalShip.GetCombatValue())
                + System.Math.Min(loadedRegimentGap, capitalShip.GetCurrentRegimentCount())
                + System.Math.Min(regimentCapacityGap, capitalShip.GetRegimentCapacity());
        }

        /// <summary>
        /// Returns whether a fleet has met the attack readiness gates.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <returns>True if the fleet is attack ready.</returns>
        private bool IsAttackReady(AITurnContext context, Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);
            return fleet.HasOperationalCapitalShips()
                && fleet.GetCombatValue() >= requiredCombat
                && fleet.GetCurrentRegimentCount() >= requiredRegiments
                && fleet.GetRegimentCapacity() >= requiredRegiments;
        }

        /// <summary>
        /// Returns how many attack readiness gates the fleet currently satisfies.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <returns>The number of satisfied readiness gates.</returns>
        private int GetAttackReadinessGateCount(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);
            int gateCount = 0;

            if (fleet.HasOperationalCapitalShips())
                gateCount++;

            if (fleet.GetCombatValue() >= requiredCombat)
                gateCount++;

            if (fleet.GetCurrentRegimentCount() >= requiredRegiments)
                gateCount++;

            if (fleet.GetRegimentCapacity() >= requiredRegiments)
                gateCount++;

            return gateCount;
        }

        /*
        private Planet FindAttackStagingPlanet(AITurnContext context, Planet targetPlanet)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => planet.GetRawDistanceTo(targetPlanet))
                .ThenByDescending(planet => planet.GetProductionRate(ManufacturingType.Ship))
                .ThenByDescending(planet => planet.GetProductionRate(ManufacturingType.Troop))
                .ThenBy(planet => planet.InstanceID)
                .FirstOrDefault();
        }
        */
    }
}
