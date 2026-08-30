using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds fleet proposals for attack orders and fleet reinforcement.
    /// </summary>
    public sealed class AIFleetPlanner : IAIProposalPlanner
    {
        // Specialized Planners.
        private readonly AIFleetDefensePlanner _defensePlanner = new AIFleetDefensePlanner();

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

            proposals.AddRange(_defensePlanner.Plan(context));

            foreach (Fleet fleet in context.Assessment.OwnedFleets)
                AddFleetProposal(context, fleet, proposals);

            AddCapitalShipTransferProposals(context, proposals);

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
                AddColonizationOrderProposals(context, fleet, currentPlanet, proposals);
                return;
            }

            if (order.OrderType == FleetOrderType.Colonize)
            {
                AddExistingColonizationOrderProposal(context, fleet, order, proposals);
                return;
            }

            if (order.OrderType == FleetOrderType.Defend)
            {
                AddExistingDefenseProposal(context, fleet, order, proposals);
                return;
            }

            if (order.OrderType != FleetOrderType.Attack)
                return;

            if (
                currentPlanet != null
                && context.Assessment.IsFactionHeadquarters(currentPlanet)
                && !context.Assessment.CanFleetDepartHeadquarters(fleet)
            )
            {
                proposals.Add(new AIClearFleetOrderProposal(fleet, order));
                return;
            }

            Planet targetPlanet = context.Assessment.GetKnownPlanet(order.TargetPlanetId);
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (
                targetPlanet == null
                || string.IsNullOrEmpty(targetOwnerId)
                || targetOwnerId == context.Faction.InstanceID
            )
            {
                proposals.Add(new AIClearFleetOrderProposal(fleet, order));
                return;
            }

            if (
                CanRetargetAttackOrder(context, fleet)
                && TryAddHeadquartersRetargetProposal(context, fleet, currentPlanet, proposals)
            )
                return;

            AIFleetAttackProposal continuation = new AIFleetAttackProposal(
                fleet,
                order.OrderType,
                order.Status,
                targetPlanet
            );
            if (!CanRetargetAttackOrder(context, fleet))
            {
                proposals.Add(continuation);
                return;
            }

            bool targetIsImpenetrable = context.Assessment.IsFleetBlockedByTargetShields(
                fleet,
                targetPlanet
            );
            if (!targetIsImpenetrable)
                proposals.Add(continuation);

            bool addedAlternative = AddRetargetAttackOrderProposals(
                context,
                fleet,
                currentPlanet,
                proposals
            );
            if (targetIsImpenetrable && !addedAlternative)
                proposals.Add(continuation);
        }

        /// <summary>
        /// Retargets a staged attack fleet to the enemy headquarters during the endgame.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet being considered for retargeting.</param>
        /// <param name="currentPlanet">The fleet's current planet.</param>
        /// <param name="proposals">The proposal list to update.</param>
        /// <returns>True when a headquarters retarget proposal was added.</returns>
        private bool TryAddHeadquartersRetargetProposal(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet,
            List<AIProposal> proposals
        )
        {
            if (!ShouldPrioritizeHeadquartersCampaign(context))
                return false;

            Planet headquarters = context.Assessment.EnemyPlanets.FirstOrDefault(planet =>
                planet.IsHeadquarters && !HasAttackFleetForTarget(context, planet, fleet)
            );
            if (headquarters == null)
                return false;

            proposals.Add(
                new AIFleetAttackProposal(
                    fleet,
                    FleetOrderType.Attack,
                    GetInitialAttackStatus(currentPlanet, headquarters),
                    headquarters
                )
            );
            return true;
        }

        /// <summary>
        /// Adds alternative targets within an assembling fleet's current campaign.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being retargeted.</param>
        /// <param name="currentPlanet">Fleet's current planet.</param>
        /// <param name="proposals">Proposal list to update.</param>
        /// <returns>True when at least one alternative target was added.</returns>
        private bool AddRetargetAttackOrderProposals(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet,
            List<AIProposal> proposals
        )
        {
            Planet currentTarget = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
            string campaignSystemId = context.Assessment.GetPlanetSystemId(currentTarget);
            bool mayLeaveCampaign = context.Assessment.IsFleetBlockedByTargetShields(
                fleet,
                currentTarget
            );
            bool addedProposal = false;
            foreach (
                Planet targetPlanet in context.Assessment.EnemyPlanets.Where(targetPlanet =>
                    targetPlanet.InstanceID != fleet.Order.TargetPlanetId
                    && (
                        mayLeaveCampaign
                        || context.Assessment.GetPlanetSystemId(targetPlanet) == campaignSystemId
                    )
                    && !HasAttackFleetForTarget(context, targetPlanet, fleet)
                )
            )
            {
                proposals.Add(
                    new AIFleetAttackProposal(
                        fleet,
                        FleetOrderType.Attack,
                        GetInitialAttackStatus(currentPlanet, targetPlanet),
                        targetPlanet
                    )
                );
                addedProposal = true;
            }

            return addedProposal;
        }

        /// <summary>
        /// Returns whether an assembling attack fleet can change targets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet can be retargeted.</returns>
        private bool CanRetargetAttackOrder(AITurnContext context, Fleet fleet)
        {
            return fleet?.Order?.OrderType == FleetOrderType.Attack
                && fleet.Order.Status is FleetOrderStatus.Building or FleetOrderStatus.Staging
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips()
                && context.Assessment.GetReadyFleetCombatValue(fleet) > 0
                && context.Assessment.CanFleetDepartHeadquarters(fleet);
        }

        /// <summary>
        /// Adds continuation or cleanup for an existing colonization order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Ordered fleet.</param>
        /// <param name="order">Existing order.</param>
        /// <param name="proposals">Proposal list to update.</param>
        private void AddExistingColonizationOrderProposal(
            AITurnContext context,
            Fleet fleet,
            FleetOrder order,
            List<AIProposal> proposals
        )
        {
            Planet targetPlanet = context.Assessment.GetKnownPlanet(order.TargetPlanetId);
            if (!IsKnownColonizationTarget(targetPlanet))
            {
                proposals.Add(new AIClearFleetOrderProposal(fleet, order));
                return;
            }

            proposals.Add(new AIColonizationProposal(fleet, order.Status, targetPlanet));
        }

        /// <summary>
        /// Adds continuation or cleanup for an existing defense order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Ordered fleet.</param>
        /// <param name="order">Existing order.</param>
        /// <param name="proposals">Proposal list to update.</param>
        private void AddExistingDefenseProposal(
            AITurnContext context,
            Fleet fleet,
            FleetOrder order,
            List<AIProposal> proposals
        )
        {
            Planet targetPlanet = context.Assessment.GetKnownPlanet(order.TargetPlanetId);
            if (
                !context.Assessment.IsOwnedPlanet(targetPlanet)
                || !context.Assessment.IsPriorityDefensePlanet(targetPlanet)
                    && context.Assessment.GetRequiredPlanetDefenseStrength(targetPlanet) <= 0
            )
            {
                proposals.Add(new AIClearFleetOrderProposal(fleet, order));
                return;
            }

            proposals.Add(new AIFleetDefenseProposal(fleet, targetPlanet));
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
            bool canStartAttack = CanStartAttackOrder(context, fleet);
            string preferredSystemId = canStartAttack
                ? FindPreferredAttackSystemId(context, fleet, currentPlanet)
                : string.Empty;

            foreach (Planet targetPlanet in context.Assessment.EnemyPlanets)
            {
                bool isPreferredCampaignTarget =
                    canStartAttack
                    && context.Assessment.GetPlanetSystemId(targetPlanet) == preferredSystemId;
                if (!isPreferredCampaignTarget)
                    continue;

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
        /// Selects the preferred system for a fleet's next attack campaign.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being assigned.</param>
        /// <param name="currentPlanet">Fleet's current planet.</param>
        /// <returns>The preferred system identifier, or null.</returns>
        private string FindPreferredAttackSystemId(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet
        )
        {
            return context
                .Assessment.EnemyPlanets.Select(context.Assessment.GetPlanetSystemId)
                .Where(systemId => !string.IsNullOrEmpty(systemId))
                .Distinct()
                .Where(systemId => !HasAttackFleetForSystem(context, systemId, fleet))
                .OrderByDescending(systemId =>
                    ShouldPrioritizeHeadquartersCampaign(context)
                    && IsHeadquartersSystem(context, systemId)
                )
                .ThenByDescending(systemId =>
                    GetAttackSystemReadinessGateCount(context, fleet, systemId)
                )
                .ThenBy(context.Assessment.GetEnemyPlanetCountInSystem)
                .ThenBy(context.Assessment.GetRequiredAttackCampaignCombatStrength)
                .ThenBy(context.Assessment.GetRequiredAttackCampaignRegimentCount)
                .ThenBy(context.Assessment.GetRequiredAttackCampaignBombardmentStrength)
                .ThenByDescending(context.Assessment.GetOwnedSystemPresenceRatio)
                .ThenByDescending(systemId => IsHeadquartersSystem(context, systemId))
                .ThenByDescending(context.Assessment.GetEnemySystemValue)
                .ThenBy(systemId => GetDistanceToAttackSystem(context, currentPlanet, systemId))
                .ThenBy(systemId => systemId, System.StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether the faction has enough territorial control to focus on ending the war.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when owned planets outnumber known enemy planets by more than two to one.</returns>
        private static bool ShouldPrioritizeHeadquartersCampaign(AITurnContext context)
        {
            return context.Assessment.EnemyPlanets.Count > 0
                && context.Assessment.OwnedPlanets.Count
                    > context.Assessment.EnemyPlanets.Count * 2;
        }

        /// <summary>
        /// Returns whether a campaign system contains an enemy headquarters.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="systemId">The candidate system identifier.</param>
        /// <returns>True when the campaign system contains a headquarters.</returns>
        private static bool IsHeadquartersSystem(AITurnContext context, string systemId)
        {
            return context
                .Assessment.GetAttackCampaignPlanets(systemId)
                .Any(planet => planet.IsHeadquarters);
        }

        /// <summary>
        /// Returns the number of campaign readiness gates a fleet satisfies for a system.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet being assigned.</param>
        /// <param name="systemId">The candidate system identifier.</param>
        /// <returns>The satisfied campaign readiness gate count.</returns>
        private int GetAttackSystemReadinessGateCount(
            AITurnContext context,
            Fleet fleet,
            string systemId
        )
        {
            Planet targetPlanet = context
                .Assessment.GetAttackCampaignPlanets(systemId)
                .FirstOrDefault();
            return context.Assessment.GetFleetAttackCampaignReadinessGateCount(fleet, targetPlanet);
        }

        /// <summary>
        /// Returns the shortest distance from a planet to a campaign system.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="currentPlanet">Fleet's current planet.</param>
        /// <param name="systemId">Candidate system identifier.</param>
        /// <returns>The shortest raw distance.</returns>
        private double GetDistanceToAttackSystem(
            AITurnContext context,
            Planet currentPlanet,
            string systemId
        )
        {
            if (currentPlanet == null)
                return double.MaxValue;

            return context
                .Assessment.GetAttackCampaignPlanets(systemId)
                .Select(currentPlanet.GetRawDistanceTo)
                .DefaultIfEmpty(double.MaxValue)
                .Min();
        }

        /// <summary>
        /// Returns whether a fleet can start an attack order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet is available to attack.</returns>
        private bool CanStartAttackOrder(AITurnContext context, Fleet fleet)
        {
            if (fleet.RoleType != FleetRoleType.Battle)
                return false;

            return fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips()
                && context.Assessment.GetReadyFleetCombatValue(fleet) > 0
                && context.Assessment.CanFleetDepartHeadquarters(fleet);
        }

        /// <summary>
        /// Adds colonization proposals for an available colonization fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being assigned.</param>
        /// <param name="currentPlanet">Fleet's current planet.</param>
        /// <param name="proposals">Proposal list to update.</param>
        private void AddColonizationOrderProposals(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet,
            List<AIProposal> proposals
        )
        {
            if (!CanStartColonizationOrder(context, fleet))
                return;

            foreach (
                Planet targetPlanet in context
                    .Assessment.KnownUncolonizedPlanets.Where(targetPlanet =>
                        !HasColonizationFleetForTarget(context, targetPlanet, fleet)
                    )
                    .OrderBy(targetPlanet =>
                        currentPlanet?.GetRawDistanceTo(targetPlanet) ?? double.MaxValue
                    )
                    .ThenByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(targetPlanet => targetPlanet.InstanceID)
            )
            {
                proposals.Add(
                    new AIColonizationProposal(
                        fleet,
                        currentPlanet?.InstanceID == targetPlanet.InstanceID
                            ? FleetOrderStatus.Ready
                            : FleetOrderStatus.Staging,
                        targetPlanet
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether a fleet can start a colonization order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet is available to colonize.</returns>
        private bool CanStartColonizationOrder(AITurnContext context, Fleet fleet)
        {
            return fleet.RoleType == FleetRoleType.Colonization
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips()
                && AIColonizationProposal.FindCarrier(fleet) != null
                && context.Assessment.CanFleetDepartHeadquarters(fleet);
        }

        /// <summary>
        /// Returns whether another fleet is colonizing a target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">Target planet.</param>
        /// <param name="ignoredFleet">Fleet excluded from the check.</param>
        /// <returns>True when another colonization fleet targets the planet.</returns>
        private bool HasColonizationFleetForTarget(
            AITurnContext context,
            Planet targetPlanet,
            Fleet ignoredFleet
        )
        {
            return context.Assessment.ColonizationOrderedFleets.Any(fleet =>
                fleet != ignoredFleet
                && fleet.Order?.TargetPlanetId == targetPlanet.InstanceID
                && fleet.Order.OrderType == FleetOrderType.Colonize
            );
        }

        /// <summary>
        /// Returns whether a known planet remains eligible for colonization.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True when the planet can be colonized.</returns>
        private static bool IsKnownColonizationTarget(Planet planet)
        {
            return planet?.IsColonized == false
                && !planet.IsDestroyed
                && string.IsNullOrEmpty(planet.GetOwnerInstanceID());
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
            Planet targetPlanet = GetReinforcementTargetPlanet(context, targetFleet);
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
                    .GetChildren<CapitalShip>()
                    .Where(capitalShip =>
                        CanTransferCapitalShip(
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
        /// Returns whether another fleet is attacking a system.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="systemId">System identifier.</param>
        /// <param name="ignoredFleet">Fleet excluded from the check.</param>
        /// <returns>True when another fleet has an attack campaign there.</returns>
        private bool HasAttackFleetForSystem(
            AITurnContext context,
            string systemId,
            Fleet ignoredFleet
        )
        {
            return context.Assessment.AttackOrderedFleets.Any(fleet =>
            {
                if (fleet == ignoredFleet)
                    return false;

                Planet targetPlanet = context.Assessment.GetKnownPlanet(
                    fleet.Order?.TargetPlanetId
                );
                return context.Assessment.GetPlanetSystemId(targetPlanet) == systemId;
            });
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
        /// Returns the strategic target for a fleet being reinforced.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The fleet target planet, or null.</returns>
        private Planet GetReinforcementTargetPlanet(AITurnContext context, Fleet fleet)
        {
            string targetPlanetId = fleet?.Order?.TargetPlanetId;
            if (string.IsNullOrEmpty(targetPlanetId))
                return null;

            Planet targetPlanet = context.Assessment.GetKnownPlanet(targetPlanetId);
            if (fleet.Order.OrderType == FleetOrderType.Defend)
            {
                return
                    context.Assessment.IsOwnedPlanet(targetPlanet)
                    && context.Assessment.GetRequiredDefenseStrength(targetPlanet) > 0
                    ? targetPlanet
                    : null;
            }

            if (fleet.Order.OrderType != FleetOrderType.Attack)
                return null;

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
            )
                return false;

            if (targetFleet.Order?.OrderType == FleetOrderType.Defend)
            {
                return context.Assessment.IsOwnedPlanet(targetPlanet)
                    && context.Assessment.GetProjectedFleetCombatValue(targetFleet)
                        < context.Assessment.GetRequiredDefenseStrength(targetPlanet);
            }

            return targetFleet.Order?.OrderType == FleetOrderType.Attack
                && !context.Assessment.IsFleetProjectedReadyToAttackCampaign(
                    targetFleet,
                    targetPlanet
                );
        }

        /// <summary>
        /// Returns the attack fleet most suitable for a capital ship transfer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The target fleet, or null.</returns>
        private Fleet GetCapitalShipTransferTargetFleet(AITurnContext context)
        {
            Fleet defenseFleet = context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.Order?.OrderType == FleetOrderType.Defend
                )
                .Select(fleet => new
                {
                    Fleet = fleet,
                    TargetPlanet = GetReinforcementTargetPlanet(context, fleet),
                })
                .Where(candidate =>
                    CanReceiveCapitalShipTransfer(context, candidate.Fleet, candidate.TargetPlanet)
                )
                .OrderByDescending(candidate =>
                    context.Assessment.GetRequiredDefenseStrength(candidate.TargetPlanet)
                    - context.Assessment.GetReadyFleetCombatValue(candidate.Fleet)
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID)
                .Select(candidate => candidate.Fleet)
                .FirstOrDefault();
            if (defenseFleet != null)
                return defenseFleet;

            return context
                .Assessment.AttackOrderedFleets.Select(fleet => new
                {
                    Fleet = fleet,
                    TargetPlanet = GetReinforcementTargetPlanet(context, fleet),
                })
                .Where(candidate =>
                    CanReceiveCapitalShipTransfer(context, candidate.Fleet, candidate.TargetPlanet)
                )
                .OrderByDescending(candidate =>
                    context.Assessment.GetFleetAttackCampaignReadinessGateCount(
                        candidate.Fleet,
                        candidate.TargetPlanet
                    )
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
                && !context.Assessment.IsFactionHeadquarters(sourcePlanet);
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
                    .GetChildren<CapitalShip>()
                    .Where(ship => ship != capitalShip)
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

            int requiredDefense = context.Assessment.IsPriorityDefensePlanet(sourcePlanet)
                ? context.Assessment.GetRequiredHeadquartersDefenseStrength(sourcePlanet)
                : context.Assessment.GetRequiredPlanetDefenseStrength(sourcePlanet);
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
                sourceFleet.GetCombatValue()
                - context.Assessment.GetReadyCapitalShipCombatValue(capitalShip);
            int otherLocalFleetCombat = context
                .Assessment.GetFriendlyFleets(sourcePlanet)
                .Where(fleet => fleet != sourceFleet && fleet.Movement == null)
                .Select(context.Assessment.GetFleetCombatValue)
                .DefaultIfEmpty()
                .Max();
            return Math.Max(Math.Max(0, sourceCombatAfterTransfer), otherLocalFleetCombat);
        }

        /// <summary>
        /// Returns whether a capital ship can reinforce the target fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="sourceFleet">The source fleet.</param>
        /// <param name="targetFleet">The target fleet.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The ship to inspect.</param>
        /// <returns>True if the capital ship can transfer.</returns>
        private bool CanTransferCapitalShip(
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

            if (targetFleet.Order?.OrderType == FleetOrderType.Defend)
            {
                return context.Assessment.GetProjectedFleetCombatValue(targetFleet)
                        < context.Assessment.GetRequiredDefenseStrength(targetPlanet)
                    && context.Assessment.GetProjectedCapitalShipCombatValue(capitalShip) > 0;
            }

            return GetCapitalShipTransferValue(context, targetFleet, targetPlanet, capitalShip) > 0;
        }

        /// <summary>
        /// Returns the benefit of transferring a capital ship to an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The target fleet.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The ship to inspect.</param>
        /// <returns>The transfer value.</returns>
        private double GetCapitalShipTransferValue(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet,
            CapitalShip capitalShip
        )
        {
            if (targetFleet.Order?.OrderType == FleetOrderType.Defend)
            {
                int requiredDefense = context.Assessment.GetRequiredDefenseStrength(targetPlanet);
                int defenseGap = Math.Max(
                    0,
                    requiredDefense - context.Assessment.GetProjectedFleetCombatValue(targetFleet)
                );
                return Math.Min(
                    defenseGap,
                    context.Assessment.GetProjectedCapitalShipCombatValue(capitalShip)
                );
            }

            int requiredCombat = context.Assessment.GetRequiredAttackCampaignCombatStrength(
                targetPlanet
            );
            int requiredRegiments = context.Assessment.GetRequiredAttackCampaignRegimentCount(
                targetPlanet
            );
            int requiredRegimentStrength =
                context.Assessment.GetRequiredAttackCampaignRegimentStrength(targetPlanet);
            int requiredBombardment =
                context.Assessment.GetRequiredAttackCampaignBombardmentStrength(targetPlanet);

            int currentRegimentCapacity = context.Assessment.GetFleetRegimentCapacity(targetFleet);
            if (currentRegimentCapacity < requiredRegiments)
            {
                return GetFulfillmentGain(
                    currentRegimentCapacity,
                    context.Assessment.GetReadyCapitalShipRegimentCapacity(capitalShip),
                    requiredRegiments
                );
            }

            int currentBombardment = context.Assessment.GetProjectedFleetBombardmentStrength(
                targetFleet
            );
            if (currentBombardment < requiredBombardment)
            {
                return GetFulfillmentGain(
                    currentBombardment,
                    context.Assessment.GetProjectedCapitalShipBombardmentStrength(
                        targetFleet,
                        capitalShip
                    ),
                    requiredBombardment
                );
            }

            int currentRegimentCount = context.Assessment.GetFleetLoadedRegimentCount(targetFleet);
            int currentRegimentStrength =
                context.Assessment.GetProjectedFleetRegimentAttackStrength(targetFleet);
            if (
                currentRegimentCount < requiredRegiments
                || currentRegimentStrength < requiredRegimentStrength
            )
            {
                return GetFulfillmentGain(
                        currentRegimentCount,
                        context.Assessment.GetReadyCapitalShipRegimentCount(capitalShip),
                        requiredRegiments
                    )
                    + GetFulfillmentGain(
                        currentRegimentStrength,
                        context.Assessment.GetProjectedCapitalShipRegimentAttackStrength(
                            targetFleet,
                            capitalShip
                        ),
                        requiredRegimentStrength
                    );
            }

            return GetFulfillmentGain(
                context.Assessment.GetProjectedFleetCombatValue(targetFleet),
                context.Assessment.GetProjectedCapitalShipCombatValue(capitalShip),
                requiredCombat
            );
        }

        /// <summary>
        /// Returns the normalized requirement gain from one reinforcement.
        /// </summary>
        /// <param name="current">Current requirement value.</param>
        /// <param name="contribution">Candidate contribution.</param>
        /// <param name="target">Required target value.</param>
        /// <returns>The fulfillment gain from zero through one.</returns>
        private double GetFulfillmentGain(double current, double contribution, double target)
        {
            if (target <= 0 || contribution <= 0)
                return 0;

            double before = Math.Min(1, Math.Max(0, current / target));
            double after = Math.Min(1, Math.Max(0, (current + contribution) / target));
            return after - before;
        }
    }
}
