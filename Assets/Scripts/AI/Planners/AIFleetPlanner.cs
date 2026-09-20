using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds fleet proposals for attack orders and fleet reinforcement.
    /// </summary>
    public sealed class AIFleetPlanner : IAIProposalPlanner
    {
        // Specialized Planners.
        private readonly AIFleetDefensePlanner _defensePlanner = new AIFleetDefensePlanner();
        private readonly AIFleetAttackPlanner _attackCandidateSelector = new AIFleetAttackPlanner();

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

            HashSet<string> activeAttackTargetIds = GetActiveAttackTargetIds(context);
            HashSet<string> activeColonizationSystemIds = context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.Order?.OrderType == FleetOrderType.Colonize
                    && !string.IsNullOrEmpty(fleet.Order.TargetSystemId)
                )
                .Select(fleet => fleet.Order.TargetSystemId)
                .ToHashSet(StringComparer.Ordinal);
            IReadOnlyDictionary<string, IReadOnlyList<Planet>> unexploredOuterRimSystems =
                GetUnexploredOuterRimSystems(context);
            int colonizationSlots = Math.Max(
                0,
                context.Game.Config.AI.FleetDeployment.ColonizationFleetTargetCount
                    - context.Assessment.OwnedFleets.Count(fleet =>
                        fleet.RoleType == FleetRoleType.Colonization
                    )
            );
            foreach (Fleet fleet in context.Assessment.OwnedFleets)
            {
                if (fleet.RoleType == FleetRoleType.None)
                {
                    List<CapitalShip> transports = GetColonizationTransports(fleet)
                        .Take(colonizationSlots)
                        .ToList();
                    proposals.Add(
                        transports.Count > 0
                            ? new AIFleetRoleProposal(fleet, transports)
                            : new AIFleetRoleProposal(fleet, FleetRoleType.Battle)
                    );
                    colonizationSlots -= transports.Count;
                    continue;
                }

                AddFleetProposal(
                    context,
                    fleet,
                    unexploredOuterRimSystems,
                    activeColonizationSystemIds,
                    proposals
                );
            }

            AddAttackOrderProposal(context, activeAttackTargetIds, proposals);

            AddCapitalShipTransferProposals(context, proposals);
            AddPlanetRegimentTransferProposals(context, proposals);

            return proposals;
        }

        /// <summary>
        /// Returns transport ships that can seed dedicated colonization fleets.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>Transport ships ordered by stable instance identifier.</returns>
        private static IEnumerable<CapitalShip> GetColonizationTransports(Fleet fleet)
        {
            if (fleet.Order != null || fleet.Movement != null || fleet.IsInCombat)
                return Enumerable.Empty<CapitalShip>();

            return fleet
                .GetChildren<CapitalShip>()
                .Where(ship => ship.HasRole(CapitalShipRole.Transport))
                .OrderBy(ship => ship.InstanceID, StringComparer.Ordinal);
        }

        /// <summary>
        /// Adds proposals for one fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="unexploredOuterRimSystems">Unexplored outer-rim planets grouped by system.</param>
        /// <param name="activeColonizationSystemIds">Systems assigned to another colonization fleet.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddFleetProposal(
            AITurnContext context,
            Fleet fleet,
            IReadOnlyDictionary<string, IReadOnlyList<Planet>> unexploredOuterRimSystems,
            HashSet<string> activeColonizationSystemIds,
            List<AIProposal> proposals
        )
        {
            FleetOrder order = fleet.Order;
            Planet currentPlanet = context.Assessment.GetFleetPlanet(fleet);

            if (ShouldEvacuate(context, fleet, currentPlanet))
            {
                proposals.Add(new AIFleetEvacuationProposal(fleet, currentPlanet));
                return;
            }

            if (order == null)
            {
                if (
                    AddColonizationCampaignProposals(
                        context,
                        fleet,
                        unexploredOuterRimSystems,
                        activeColonizationSystemIds,
                        proposals
                    )
                )
                    return;

                AddColonizationOrderProposals(context, fleet, currentPlanet, proposals);
                return;
            }

            if (
                order.OrderType == FleetOrderType.Colonize
                && string.IsNullOrEmpty(order.TargetPlanetId)
            )
            {
                AddExistingColonizationCampaignProposal(context, fleet, order, proposals);
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
                && !context.StrategicPlan.CanFleetDepart(fleet)
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

            AIFleetAttackProposal continuation = new AIFleetAttackProposal(
                fleet,
                order.OrderType,
                order.Status,
                targetPlanet
            );
            if (order.Status == FleetOrderStatus.Returning)
            {
                proposals.Add(continuation);
                return;
            }

            bool strandedAtHostileNonTarget =
                currentPlanet != null
                && currentPlanet.InstanceID != targetPlanet.InstanceID
                && !string.IsNullOrEmpty(currentPlanet.GetOwnerInstanceID())
                && currentPlanet.GetOwnerInstanceID() != context.Faction.InstanceID
                && !context.AttackRequirements.CanAdvance(fleet, currentPlanet);
            if (strandedAtHostileNonTarget)
            {
                proposals.Add(
                    new AIFleetAttackProposal(
                        fleet,
                        order.OrderType,
                        FleetOrderStatus.Returning,
                        currentPlanet
                    )
                );
                return;
            }

            bool targetCannotBeAttacked =
                currentPlanet?.InstanceID == targetPlanet.InstanceID
                && !context.AttackRequirements.CanAdvance(fleet, targetPlanet);
            if (targetCannotBeAttacked)
            {
                proposals.Add(
                    new AIFleetAttackProposal(
                        fleet,
                        order.OrderType,
                        FleetOrderStatus.Returning,
                        targetPlanet
                    )
                );
                return;
            }

            if (!CanRetargetAttackOrder(context, fleet))
            {
                proposals.Add(continuation);
                return;
            }

            bool mayLeaveCampaign = context.AttackRequirements.IsBlockedByShields(
                fleet,
                targetPlanet
            );
            if (!mayLeaveCampaign)
                proposals.Add(continuation);

            AddRetargetAttackOrderProposals(
                context,
                fleet,
                currentPlanet,
                mayLeaveCampaign,
                proposals
            );
            if (mayLeaveCampaign)
            {
                proposals.Add(
                    new AIFleetAttackProposal(
                        fleet,
                        order.OrderType,
                        FleetOrderStatus.Returning,
                        targetPlanet
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether a stationary fleet must leave a hostile planet where it has no viable
        /// orbital or planetary action.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <param name="currentPlanet">Planet currently containing the fleet.</param>
        /// <returns>True when the fleet should evacuate to friendly territory.</returns>
        private static bool ShouldEvacuate(AITurnContext context, Fleet fleet, Planet currentPlanet)
        {
            if (
                currentPlanet == null
                || fleet.Movement != null
                || fleet.IsInCombat
                || string.IsNullOrEmpty(currentPlanet.GetOwnerInstanceID())
                || currentPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
            )
                return false;

            return !context.AttackRequirements.CanAct(fleet, currentPlanet);
        }

        /// <summary>
        /// Groups unexplored Outer Rim planets once for reuse by every fleet this turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Unexplored planets keyed by sector identifier.</returns>
        private static IReadOnlyDictionary<
            string,
            IReadOnlyList<Planet>
        > GetUnexploredOuterRimSystems(AITurnContext context)
        {
            return context
                .Assessment.UnexploredPlanets.Where(planet =>
                    planet.GetParentOfType<PlanetSector>()?.SectorType == PlanetSectorType.OuterRim
                )
                .GroupBy(context.Assessment.GetPlanetSystemId)
                .Where(group => !string.IsNullOrEmpty(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group =>
                        (IReadOnlyList<Planet>)
                            group
                                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                                .ToList(),
                    StringComparer.Ordinal
                );
        }

        /// <summary>
        /// Adds candidate sector campaign assignments before a colonization fleet selects a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being assigned.</param>
        /// <param name="systems">Unexplored Outer Rim planets keyed by sector.</param>
        /// <param name="activeSystemIds">Sectors already assigned to another campaign fleet.</param>
        /// <param name="proposals">Proposal list to update.</param>
        /// <returns>True when at least one campaign proposal was added.</returns>
        private bool AddColonizationCampaignProposals(
            AITurnContext context,
            Fleet fleet,
            IReadOnlyDictionary<string, IReadOnlyList<Planet>> systems,
            HashSet<string> activeSystemIds,
            List<AIProposal> proposals
        )
        {
            if (!CanStartColonizationOrder(context, fleet))
                return false;

            bool added = false;
            foreach (
                KeyValuePair<string, IReadOnlyList<Planet>> system in systems.Where(system =>
                    !activeSystemIds.Contains(system.Key)
                )
            )
            {
                proposals.Add(new AIColonizationCampaignProposal(fleet, system.Key, system.Value));
                added = true;
            }

            return added;
        }

        /// <summary>
        /// Continues an interrupted campaign or selects its highest-capacity revealed colony.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet assigned to the campaign.</param>
        /// <param name="order">The current campaign order.</param>
        /// <param name="proposals">Proposal list to update.</param>
        private void AddExistingColonizationCampaignProposal(
            AITurnContext context,
            Fleet fleet,
            FleetOrder order,
            List<AIProposal> proposals
        )
        {
            if (fleet.Movement != null || fleet.HasWaypoints() || fleet.IsInCombat)
                return;

            string systemId = order.TargetSystemId;
            if (string.IsNullOrEmpty(systemId))
            {
                proposals.Add(new AIClearFleetOrderProposal(fleet, order));
                return;
            }

            IReadOnlyList<Planet> unexplored = context
                .Assessment.UnexploredPlanets.Where(planet =>
                    context.Assessment.GetPlanetSystemId(planet) == systemId
                )
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .ToList();
            if (unexplored.Count > 0)
            {
                proposals.Add(new AIColonizationCampaignProposal(fleet, systemId, unexplored));
                return;
            }

            Planet target = context
                .Assessment.KnownUncolonizedPlanets.Where(planet =>
                    context.Assessment.GetPlanetSystemId(planet) == systemId
                    && !HasColonizationFleetForTarget(context, planet, fleet)
                )
                .OrderByDescending(planet => AIColonizationTargetScorer.Score(context, planet))
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            proposals.Add(
                new AIColonizationCampaignProposal(fleet, systemId, Array.Empty<Planet>(), target)
            );
        }

        /// <summary>
        /// Adds alternative targets within an assembling fleet's current campaign.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being retargeted.</param>
        /// <param name="currentPlanet">Fleet's current planet.</param>
        /// <param name="mayLeaveCampaign">Whether targets outside the current system are eligible.</param>
        /// <param name="proposals">Proposal list to update.</param>
        /// <returns>True when at least one alternative target was added.</returns>
        private bool AddRetargetAttackOrderProposals(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet,
            bool mayLeaveCampaign,
            List<AIProposal> proposals
        )
        {
            Planet currentTarget = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
            string campaignSystemId = context.Assessment.GetPlanetSystemId(currentTarget);
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
                && context.StrategicPlan.CanFleetDepart(fleet);
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
            if (IsKnownColonizationTarget(targetPlanet))
            {
                proposals.Add(new AIColonizationProposal(fleet, order.Status, targetPlanet));
                return;
            }

            Planet nextTarget = FindNextColonizationTarget(context, fleet, order.TargetSystemId);
            if (nextTarget != null)
            {
                proposals.Add(
                    new AIColonizationProposal(fleet, FleetOrderStatus.Staging, nextTarget)
                );
                return;
            }

            proposals.Add(new AIClearFleetOrderProposal(fleet, order));
        }

        /// <summary>
        /// Returns the strongest unclaimed planet remaining in a colonization campaign.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet conducting the campaign.</param>
        /// <param name="systemId">The campaign system identifier.</param>
        /// <returns>The next colonization target, or null when the campaign is complete.</returns>
        private Planet FindNextColonizationTarget(
            AITurnContext context,
            Fleet fleet,
            string systemId
        )
        {
            if (string.IsNullOrEmpty(systemId))
                return null;

            return context
                .Assessment.KnownUncolonizedPlanets.Where(planet =>
                    context.Assessment.GetPlanetSystemId(planet) == systemId
                    && !HasColonizationFleetForTarget(context, planet, fleet)
                )
                .OrderByDescending(planet => AIColonizationTargetScorer.Score(context, planet))
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
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
            if (context.Assessment.IsFactionHeadquarters(targetPlanet))
                return;

            if (
                !context.Assessment.IsOwnedPlanet(targetPlanet)
                || !context.Assessment.IsPriorityDefensePlanet(targetPlanet)
                    && context.StrategicPlan.GetPlanetDefenseStrength(targetPlanet) <= 0
            )
            {
                proposals.Add(new AIClearFleetOrderProposal(fleet, order));
                return;
            }

            proposals.Add(new AIFleetDefenseProposal(fleet, targetPlanet));
        }

        /// <summary>
        /// Adds the strongest new attack order proposal for the faction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="activeAttackTargetIds">Planets already assigned to attack fleets.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddAttackOrderProposal(
            AITurnContext context,
            HashSet<string> activeAttackTargetIds,
            List<AIProposal> proposals
        )
        {
            IEnumerable<Planet> targets = context.Assessment.EnemyPlanets.Where(target =>
                !activeAttackTargetIds.Contains(target.InstanceID)
            );
            AIFleetAttackProposal proposal = _attackCandidateSelector.Select(
                context,
                context.Assessment.OwnedFleets.Where(fleet =>
                    fleet.Order == null && CanStartAttackOrder(context, fleet)
                ),
                targets
            );
            if (proposal != null)
                proposals.Add(proposal);
        }

        /// <summary>
        /// Returns planets already assigned to an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Planet identifiers with active attack orders.</returns>
        private HashSet<string> GetActiveAttackTargetIds(AITurnContext context)
        {
            return context
                .Assessment.AttackOrderedFleets.Select(fleet => fleet.Order?.TargetPlanetId)
                .Where(targetPlanetId => !string.IsNullOrEmpty(targetPlanetId))
                .ToHashSet(StringComparer.Ordinal);
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
                && context.StrategicPlan.CanFleetDepart(fleet);
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
                Planet targetPlanet in context.Assessment.KnownUncolonizedPlanets.Where(
                    targetPlanet => !HasColonizationFleetForTarget(context, targetPlanet, fleet)
                )
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
                && context.Assessment.GetReadyFleetRegimentCount(fleet)
                    >= Math.Max(
                        1,
                        context.Game.Config.AI.FleetDeployment.ColonizationFleetMinimumRegimentCount
                    );
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
        /// Adds regiment transfers from a safe owned planet to an attack fleet that can capture
        /// its target once its ground force is complete.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddPlanetRegimentTransferProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            Fleet targetFleet = context
                .Assessment.AttackOrderedFleets.Where(fleet =>
                    CanReceivePlanetRegimentTransfer(context, fleet)
                )
                .OrderByDescending(fleet =>
                    context.AttackRequirements.CountTargetMet(
                        fleet,
                        GetReinforcementTargetPlanet(context, fleet)
                    )
                )
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
            if (targetFleet == null)
                return;

            Planet receivingPlanet = context.Assessment.GetFleetPlanet(targetFleet);
            Planet targetPlanet = GetReinforcementTargetPlanet(context, targetFleet);
            Planet sourcePlanet = context
                .Assessment.OwnedPlanets.Where(planet =>
                    GetAvailablePlanetRegimentCount(context, planet) > 0
                )
                .OrderByDescending(context.Assessment.GetFactionPopularSupport)
                .ThenBy(context.Assessment.GetDefensiveSupportRisk)
                .ThenBy(planet => planet.GetRawDistanceTo(receivingPlanet))
                .ThenBy(planet => planet.InstanceID)
                .FirstOrDefault();
            if (sourcePlanet == null)
                return;

            foreach (Regiment regiment in GetAvailablePlanetRegiments(context, sourcePlanet))
            {
                proposals.Add(
                    new AITransferUnitProposal(
                        sourcePlanet,
                        targetFleet,
                        regiment,
                        targetFleet,
                        targetPlanet
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether an attack fleet needs ground reinforcements and otherwise has the
        /// projected strength needed to capture its target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when a planet regiment can complete the fleet's attack force.</returns>
        private bool CanReceivePlanetRegimentTransfer(AITurnContext context, Fleet fleet)
        {
            Planet targetPlanet = GetReinforcementTargetPlanet(context, fleet);
            if (
                fleet == null
                || targetPlanet == null
                || fleet.RoleType != FleetRoleType.Battle
                || fleet.Movement != null
                || fleet.IsInCombat
            )
                return false;

            int requiredCount = context.AttackRequirements.GetRegimentCount(
                fleet,
                targetPlanet,
                projected: true
            );
            int currentCount = context.Assessment.GetFleetLoadedRegimentCount(fleet);
            bool needsRegiments =
                currentCount < requiredCount
                || context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < context.AttackRequirements.GetRegimentStrength(
                        fleet,
                        targetPlanet,
                        projected: true
                    );
            return needsRegiments
                && context.Assessment.GetFleetRegimentCapacity(fleet) > currentCount
                && context.Assessment.GetProjectedFleetCombatValue(fleet)
                    >= context.AttackRequirements.GetCombatStrength(targetPlanet)
                && context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                    >= context.AttackRequirements.GetBombardmentStrength(targetPlanet);
        }

        /// <summary>
        /// Returns completed regiments a safe planet can release without compromising stability.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The potential source planet.</param>
        /// <returns>The regiments available for transfer.</returns>
        private int GetAvailablePlanetRegimentCount(AITurnContext context, Planet planet)
        {
            if (
                planet == null
                || context.Assessment.IsPriorityDefensePlanet(planet)
                || context.Assessment.GetPlanetDefenseThreatStrength(planet) > 0
            )
                return 0;

            int readyRegimentCount = context
                .Assessment.GetPlanetRegiments(planet)
                .Count(regiment =>
                    regiment.GetOwnerInstanceID() == context.Faction.InstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                );
            int requiredGarrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                context.Faction,
                context.Game.Config.AI.Garrison
            );
            return Math.Max(0, readyRegimentCount - requiredGarrison);
        }

        /// <summary>
        /// Returns the strongest completed regiments a selected source planet can spare.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The selected source planet.</param>
        /// <returns>The regiments available for transfer.</returns>
        private List<Regiment> GetAvailablePlanetRegiments(AITurnContext context, Planet planet)
        {
            int availableCount = GetAvailablePlanetRegimentCount(context, planet);
            return context
                .Assessment.GetPlanetRegiments(planet)
                .Where(regiment =>
                    regiment.GetOwnerInstanceID() == context.Faction.InstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                .OrderByDescending(regiment => regiment.AttackRating)
                .ThenBy(regiment => regiment.InstanceID)
                .Take(availableCount)
                .ToList();
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
                    && context.StrategicPlan.GetDefenseStrength(targetPlanet) > 0
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
                        < context.StrategicPlan.GetDefenseStrength(targetPlanet);
            }

            return targetFleet.Order?.OrderType == FleetOrderType.Attack
                && !context.AttackRequirements.WillMeet(targetFleet, targetPlanet);
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
                    AIFleetReinforcementUtility.ScoreDefenseNeed(
                        context,
                        candidate.TargetPlanet,
                        context.Assessment.GetReadyFleetCombatValue(candidate.Fleet)
                    )
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
                    context.AttackRequirements.CountTargetMet(
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
                ? context.StrategicPlan.GetHeadquartersDefenseStrength(sourcePlanet)
                : context.StrategicPlan.GetPlanetDefenseStrength(sourcePlanet);
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
                        < context.StrategicPlan.GetDefenseStrength(targetPlanet)
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
                int requiredDefense = context.StrategicPlan.GetDefenseStrength(targetPlanet);
                int defenseGap = Math.Max(
                    0,
                    requiredDefense - context.Assessment.GetProjectedFleetCombatValue(targetFleet)
                );
                return Math.Min(
                    defenseGap,
                    context.Assessment.GetProjectedCapitalShipCombatValue(capitalShip)
                );
            }

            int requiredCombat = context.AttackRequirements.GetCombatStrength(targetPlanet);
            int requiredRegiments = context.AttackRequirements.GetRegimentCount(targetPlanet);
            int requiredRegimentStrength = context.AttackRequirements.GetRegimentStrength(
                targetPlanet
            );
            int requiredBombardment = context.AttackRequirements.GetBombardmentStrength(
                targetPlanet
            );

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
