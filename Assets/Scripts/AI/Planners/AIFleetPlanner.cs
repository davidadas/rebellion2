using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.Game;
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
        private readonly AIFleetProposalScorer _scorer = new AIFleetProposalScorer();

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

            proposals.AddRange(PlanDefense(context));

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
        /// Returns fleet-defense proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Fleet-defense proposals generated for the faction.</returns>
        private static List<AIProposal> PlanDefense(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Game == null || context.Faction == null)
                return proposals;

            Fleet headquartersDefense = AddHeadquartersDefenseProposal(context, proposals);
            AddPlanetDefenseProposals(context, proposals, headquartersDefense);
            return proposals;
        }

        /// <summary>
        /// Adds a headquarters defense proposal when current commitments are insufficient.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <returns>The assigned fleet, or null when no assignment is needed or available.</returns>
        private static Fleet AddHeadquartersDefenseProposal(
            AITurnContext context,
            ICollection<AIProposal> proposals
        )
        {
            Planet headquarters = context.Assessment.OwnedPlanets.FirstOrDefault(
                context.Assessment.IsFactionHeadquarters
            );
            if (headquarters == null)
                return null;

            List<Fleet> assignedFleets = context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.Order?.OrderType == FleetOrderType.Defend
                    && fleet.Order.TargetPlanetId == headquarters.InstanceID
                )
                .OrderByDescending(context.Assessment.GetFleetCombatValue)
                .ThenBy(fleet => fleet.InstanceID)
                .ToList();
            if (assignedFleets.Count > 0)
            {
                Fleet primaryFleet = assignedFleets[0];
                proposals.Add(new AIFleetDefenseProposal(primaryFleet, headquarters));
                foreach (Fleet redundantFleet in assignedFleets.Skip(1))
                    proposals.Add(
                        new AIClearFleetOrderProposal(redundantFleet, redundantFleet.Order)
                    );

                return primaryFleet;
            }

            int requiredDefense = context.StrategicPlan.GetHeadquartersDefenseStrength(
                headquarters
            );
            Fleet inboundFleet = context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.Movement != null
                    && context.Assessment.GetFleetPlanet(fleet)?.InstanceID
                        == headquarters.InstanceID
                )
                .OrderByDescending(context.Assessment.GetFleetCombatValue)
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
            if (inboundFleet != null)
                return inboundFleet;

            Fleet fleet = FindHeadquartersDefenseFleet(context, headquarters, requiredDefense);
            if (fleet != null)
                proposals.Add(new AIFleetDefenseProposal(fleet, headquarters));

            return fleet;
        }

        /// <summary>
        /// Adds defense proposals for threatened non-headquarters planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <param name="headquartersDefense">The fleet already reserved for headquarters defense.</param>
        private static void AddPlanetDefenseProposals(
            AITurnContext context,
            ICollection<AIProposal> proposals,
            Fleet headquartersDefense
        )
        {
            HashSet<Fleet> assignedFleets = new HashSet<Fleet>();
            if (headquartersDefense != null)
                assignedFleets.Add(headquartersDefense);

            foreach (
                Planet targetPlanet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        !context.Assessment.IsFactionHeadquarters(planet)
                        && context.StrategicPlan.GetPlanetDefenseStrength(planet) > 0
                        && !HasDefenseOrder(context, planet)
                    )
                    .OrderByDescending(planet =>
                        AIFleetProposalScorer.ScoreDefenseTarget(context, planet)
                    )
                    .ThenBy(planet => planet.InstanceID)
            )
            {
                Fleet fleet = FindPlanetDefenseFleet(context, targetPlanet, assignedFleets);
                if (fleet == null)
                    continue;

                assignedFleets.Add(fleet);
                proposals.Add(new AIFleetDefenseProposal(fleet, targetPlanet));
            }
        }

        /// <summary>
        /// Finds the least costly available fleet capable of defending a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The threatened planet.</param>
        /// <param name="assignedFleets">Fleets already assigned during this planning pass.</param>
        /// <returns>The selected fleet, or null when none can defend the planet.</returns>
        private static Fleet FindPlanetDefenseFleet(
            AITurnContext context,
            Planet targetPlanet,
            ISet<Fleet> assignedFleets
        )
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    !assignedFleets.Contains(fleet)
                    && CanAssignPlanetDefense(context, fleet)
                    && context.StrategicPlan.CanDefend(fleet, targetPlanet)
                )
                .OrderByDescending(fleet =>
                    AIFleetProposalScorer.ScoreDefenseAssignment(context, fleet, targetPlanet)
                )
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet can be reassigned to defend an ordinary planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet is available for defense.</returns>
        private static bool CanAssignPlanetDefense(AITurnContext context, Fleet fleet)
        {
            if (
                fleet?.RoleType != FleetRoleType.Battle
                || fleet.Movement != null
                || fleet.IsInCombat
                || !fleet.HasOperationalCapitalShips()
                || !context.StrategicPlan.CanFleetDepart(fleet)
            )
                return false;

            return fleet.Order == null
                || fleet.Order.OrderType != FleetOrderType.Defend
                    && fleet.Order.Status == FleetOrderStatus.Staging;
        }

        /// <summary>
        /// Finds the best available fleet for headquarters defense.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="headquarters">The headquarters planet.</param>
        /// <param name="requiredDefense">The required defense strength.</param>
        /// <returns>The selected fleet, or null when none is available.</returns>
        private static Fleet FindHeadquartersDefenseFleet(
            AITurnContext context,
            Planet headquarters,
            int requiredDefense
        )
        {
            List<Fleet> candidates = context
                .Assessment.OwnedFleets.Where(fleet =>
                    CanAssignHeadquartersDefense(context, fleet, headquarters)
                )
                .ToList();
            Fleet sufficientFleet = candidates
                .Where(fleet => context.Assessment.GetFleetCombatValue(fleet) >= requiredDefense)
                .OrderByDescending(fleet =>
                    AIFleetProposalScorer.ScoreDefenseAssignment(context, fleet, headquarters)
                )
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
            if (sufficientFleet != null)
                return sufficientFleet;

            return candidates
                .OrderByDescending(context.Assessment.GetFleetCombatValue)
                .ThenByDescending(fleet =>
                    AIFleetProposalScorer.ScoreDefenseTravelEfficiency(context, fleet, headquarters)
                )
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet can be assigned to headquarters defense.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="headquarters">The headquarters planet.</param>
        /// <returns>True when the fleet is idle and combat-capable.</returns>
        private static bool CanAssignHeadquartersDefense(
            AITurnContext context,
            Fleet fleet,
            Planet headquarters
        )
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet.Order == null
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips()
                && (
                    context.Assessment.GetFleetPlanet(fleet)?.InstanceID == headquarters.InstanceID
                    || context.StrategicPlan.CanFleetDepart(fleet)
                );
        }

        /// <summary>
        /// Returns whether a fleet is already ordered to defend a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The planet to inspect.</param>
        /// <returns>True when a matching defense order exists.</returns>
        private static bool HasDefenseOrder(AITurnContext context, Planet targetPlanet)
        {
            return context.Assessment.OwnedFleets.Any(fleet =>
                fleet.Order?.OrderType == FleetOrderType.Defend
                && fleet.Order.TargetPlanetId == targetPlanet.InstanceID
            );
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
                && !CanAdvance(context, fleet, currentPlanet);
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
                && !CanAdvance(context, fleet, targetPlanet);
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

            bool mayLeaveCampaign = IsBlockedByShields(context, fleet, targetPlanet);
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

            return !CanAct(context, fleet, currentPlanet);
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
                .OrderByDescending(planet => ScoreColonizationTarget(context, planet))
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
                .OrderByDescending(planet => ScoreColonizationTarget(context, planet))
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a colony target's configured economic utility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The candidate colony.</param>
        /// <returns>The colony target utility score.</returns>
        private static double ScoreColonizationTarget(AITurnContext context, Planet planet)
        {
            if (context?.Game?.Config == null || planet == null)
                return 0;

            GameConfig.AIColonizationTargetUtilityConfig utility = context
                .Game
                .Config
                .AI
                .FleetDeployment
                .ColonizationTargetUtility;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(planet.GetEnergyCapacity(), utility.Energy),
                utility.Energy
            );
            score.Add(
                AIUtility.Fulfillment(planet.GetRawResourceNodes(), utility.Resources),
                utility.Resources
            );
            return score.Value;
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
            AIFleetAttackProposal proposal = SelectAttack(
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
        /// Returns the strongest attack proposal from the eligible fleets and targets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleets">The fleets available for a new attack order.</param>
        /// <param name="targets">Eligible attack targets.</param>
        /// <returns>The highest-scoring proposal, or null when no target has value.</returns>
        private AIFleetAttackProposal SelectAttack(
            AITurnContext context,
            IEnumerable<Fleet> fleets,
            IEnumerable<Planet> targets
        )
        {
            AIFleetAttackProposal selected = null;
            IReadOnlyList<Fleet> availableFleets = fleets.ToList();
            IEnumerable<(Planet Target, double UpperBound)> orderedTargets = targets
                .Select(target =>
                    (
                        Target: target,
                        UpperBound: _scorer.GetNewAttackScoreUpperBound(context, target)
                    )
                )
                .OrderByDescending(candidate => candidate.UpperBound)
                .ThenBy(candidate => candidate.Target.InstanceID, StringComparer.Ordinal);
            foreach ((Planet Target, double UpperBound) candidate in orderedTargets)
            {
                if (selected != null && candidate.UpperBound < selected.Score)
                    break;

                foreach (Fleet fleet in availableFleets)
                {
                    Planet currentPlanet = context.Assessment.GetFleetPlanet(fleet);
                    AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                        fleet,
                        FleetOrderType.Attack,
                        currentPlanet == candidate.Target
                            ? FleetOrderStatus.Ready
                            : FleetOrderStatus.Staging,
                        candidate.Target
                    );
                    proposal.SetScore(_scorer.Score(context, proposal));
                    if (proposal.Score <= 0 || !IsPreferred(proposal, selected))
                        continue;

                    selected = proposal;
                }
            }

            return selected;
        }

        /// <summary>
        /// Returns whether a candidate outranks the currently selected proposal.
        /// </summary>
        /// <param name="candidate">The candidate proposal.</param>
        /// <param name="selected">The currently selected proposal.</param>
        /// <returns>True when the candidate should replace the selection.</returns>
        private static bool IsPreferred(
            AIFleetAttackProposal candidate,
            AIFleetAttackProposal selected
        )
        {
            if (selected == null || candidate.Score != selected.Score)
                return selected == null || candidate.Score > selected.Score;

            return string.Compare(
                    candidate.GetSortKey(),
                    selected.GetSortKey(),
                    StringComparison.Ordinal
                ) < 0;
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
                    CountTargetCapabilitiesMet(
                        context,
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

            int requiredCount = GetProjectedRegimentCount(context, fleet, targetPlanet);
            int currentCount = context.Assessment.GetFleetLoadedRegimentCount(fleet);
            bool needsRegiments =
                currentCount < requiredCount
                || context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < GetProjectedRegimentStrength(context, fleet, targetPlanet);
            return needsRegiments
                && context.Assessment.GetFleetRegimentCapacity(fleet) > currentCount
                && context.Assessment.GetProjectedFleetCombatValue(fleet)
                    >= (context.GetAttackDemand(targetPlanet)?.CombatStrength ?? 0)
                && context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                    >= (context.GetAttackDemand(targetPlanet)?.BombardmentStrength ?? 0);
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
                && !WillMeetAttackDemand(context, targetFleet, targetPlanet);
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
                    AIFleetProductionAllocationScorer.ScoreDefenseNeed(
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
                    CountTargetCapabilitiesMet(context, candidate.Fleet, candidate.TargetPlanet)
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

            AIAttackDemand attackDemand = context.GetAttackDemand(targetPlanet);
            int requiredCombat = attackDemand?.CombatStrength ?? 0;
            int requiredRegiments = attackDemand?.RegimentCount ?? 0;
            int requiredRegimentStrength = attackDemand?.RegimentStrength ?? 0;
            int requiredBombardment = attackDemand?.BombardmentStrength ?? 0;

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

        /// <summary>
        /// Returns whether a fleet can immediately bombard or assault a hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The hostile planet.</param>
        /// <returns>True when the fleet can make immediate progress.</returns>
        private static bool CanAdvance(AITurnContext context, Fleet fleet, Planet targetPlanet)
        {
            if (CanBombardMilitaryTargets(context, fleet, targetPlanet))
                return true;
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            if (demand?.IsAssaultBlockedByShields != false)
                return false;

            return context.Assessment.GetReadyFleetRegimentCount(fleet)
                    >= GetCurrentRegimentCount(context, fleet, targetPlanet)
                && context.Assessment.GetReadyFleetRegimentAttackStrength(fleet)
                    >= GetCurrentRegimentStrength(context, fleet, targetPlanet)
                && context.Assessment.GetPlanetaryAssaultSuccessPercent(fleet, targetPlanet)
                    >= context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent;
        }

        /// <summary>
        /// Returns whether a fleet has a viable immediate action at a hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="planet">The hostile planet.</param>
        /// <returns>True when the fleet can win orbit or advance against the planet.</returns>
        private static bool CanAct(AITurnContext context, Fleet fleet, Planet planet)
        {
            if (fleet == null || planet == null)
                return false;
            return context.Assessment.GetStrongestHostileFleetStrength(planet) > 0
                ? CanWinProjectedOrbitalCombat(context, fleet, planet)
                : CanAdvance(context, fleet, planet);
        }

        /// <summary>
        /// Returns whether target shields exceed the fleet's current bombardment strength.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when shields block the fleet's assault.</returns>
        private static bool IsBlockedByShields(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            return demand?.IsAssaultBlockedByShields == true
                && context.Assessment.GetFleetBombardmentStrength(fleet)
                    < demand.BombardmentStrength;
        }

        /// <summary>
        /// Counts the target attack capabilities currently satisfied by a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The number of satisfied capabilities.</returns>
        private static int CountTargetCapabilitiesMet(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            if (demand == null)
                return 0;

            int count = 0;
            if (fleet?.HasOperationalCapitalShips() == true)
                count++;
            int combat = context.Assessment.GetReadyFleetCombatValue(fleet);
            if (combat > 0 && combat >= demand.CombatStrength)
                count++;
            if (context.Assessment.GetReadyFleetRegimentCount(fleet) >= demand.RegimentCount)
                count++;
            if (context.Assessment.GetReadyFleetRegimentCapacity(fleet) >= demand.RegimentCount)
                count++;
            if (
                context.Assessment.GetReadyFleetRegimentAttackStrength(fleet)
                >= demand.RegimentStrength
            )
                count++;
            if (context.Assessment.GetFleetBombardmentStrength(fleet) >= demand.BombardmentStrength)
                count++;
            return count;
        }

        /// <summary>
        /// Returns whether projected fleet capability satisfies the complete target demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when projected fleet capability satisfies every demand.</returns>
        private static bool WillMeetAttackDemand(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            return demand != null
                && fleet?.GetChildren<CapitalShip>().Any(capitalShip => capitalShip != null) == true
                && context.Assessment.GetProjectedFleetCombatValue(fleet) > 0
                && context.Assessment.GetProjectedFleetCombatValue(fleet) >= demand.CombatStrength
                && context.Assessment.GetFleetLoadedRegimentCount(fleet) >= demand.RegimentCount
                && context.Assessment.GetFleetRegimentCapacity(fleet) >= demand.RegimentCount
                && context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    >= demand.RegimentStrength
                && context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                    >= demand.BombardmentStrength;
        }

        /// <summary>
        /// Returns whether projected combat strength defeats known orbital defenders.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when projected combat strength is sufficient.</returns>
        private static bool CanWinProjectedOrbitalCombat(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            int required = context.GetAttackDemand(targetPlanet)?.OrbitalStrength ?? 0;
            return required > 0
                && context.Assessment.GetProjectedFleetCombatValue(fleet) >= required;
        }

        /// <summary>
        /// Returns whether current bombardment can strike hostile military targets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when hostile targets are exposed.</returns>
        private static bool CanBombardMilitaryTargets(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            return fleet != null
                && targetPlanet != null
                && context.Assessment.GetFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldResistance(targetPlanet)
                && context.Assessment.HasBombardmentTargets(targetPlanet);
        }

        /// <summary>
        /// Returns the current regiment count required after available bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The current regiment count requirement.</returns>
        private static int GetCurrentRegimentCount(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            if (demand == null)
                return 0;
            return CanBombardDefenders(context, fleet, targetPlanet, false)
                ? demand.OccupationRegimentCount
                : demand.RegimentCount;
        }

        /// <summary>
        /// Returns the current regiment strength required after available bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The current regiment strength requirement.</returns>
        private static int GetCurrentRegimentStrength(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            return CanBombardDefenders(context, fleet, targetPlanet, false)
                ? 0
                : context.GetAttackDemand(targetPlanet)?.RegimentStrength ?? 0;
        }

        /// <summary>
        /// Returns the projected regiment count required after available bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The projected regiment count requirement.</returns>
        private static int GetProjectedRegimentCount(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            if (demand == null)
                return 0;
            return CanBombardDefenders(context, fleet, targetPlanet, true)
                ? demand.OccupationRegimentCount
                : demand.RegimentCount;
        }

        /// <summary>
        /// Returns the projected regiment strength required after available bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The projected regiment strength requirement.</returns>
        private static int GetProjectedRegimentStrength(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            return CanBombardDefenders(context, fleet, targetPlanet, true)
                ? 0
                : context.GetAttackDemand(targetPlanet)?.RegimentStrength ?? 0;
        }

        /// <summary>
        /// Returns whether fleet bombardment can remove defending regiments.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <param name="projected">Whether projected bombardment is used.</param>
        /// <returns>True when bombardment can penetrate target shields.</returns>
        private static bool CanBombardDefenders(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            bool projected
        )
        {
            if (
                fleet == null
                || targetPlanet == null
                || context.Assessment.GetDefendingRegimentCount(targetPlanet) == 0
            )
                return false;
            int bombardment = projected
                ? context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                : context.Assessment.GetFleetBombardmentStrength(fleet);
            return bombardment > context.Assessment.GetBombardmentShieldResistance(targetPlanet);
        }
    }
}
