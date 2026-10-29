using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Resolves ranked production requests against currently available capacity.
    /// </summary>
    internal sealed class AIProductionSelector
    {
        private const int _buildingEnergyCost = 1;
        private const string _mixedProductType = "*";
        private static readonly AIProductionProposalScorer _scorer = new();
        private readonly AISelectionState _selectionState;
        private readonly Dictionary<string, int> _reservedProducerCapacity = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, string> _selectedProducerProducts = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, string> _currentProducerProducts = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<
            string,
            Dictionary<BuildingType, int>
        > _reservedDestinationBuildings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildingType> _reservedFacilityRoles = new(
            StringComparer.Ordinal
        );
        private readonly HashSet<string> _drainingProducerStreams = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _continuedBuildingCounts = new(
            StringComparer.Ordinal
        );
        private int _selectedMaintenanceCost;
        private int _selectedPlanetaryStarfighterCount;

        /// <summary>
        /// Creates a production selector backed by the current turn's allocation ledger.
        /// </summary>
        /// <param name="selectionState">Shared typed reservation state.</param>
        internal AIProductionSelector(AISelectionState selectionState)
        {
            _selectionState = selectionState;
        }

        /// <summary>
        /// Resolves a deficit-limited retirement set using marginal damage to cached requirements.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The assessed retirement alternatives.</param>
        /// <param name="selectedProposal">The ordered executable recovery action.</param>
        /// <returns>True when at least one safe retirement can be selected.</returns>
        internal bool TryResolve(
            AITurnContext context,
            AIMaintenanceRecoveryProposal proposal,
            out AIMaintenanceRecoveryProposal selectedProposal
        )
        {
            selectedProposal = proposal;
            if (!proposal.CanSelect(context))
                return false;
            var remaining = proposal
                .Candidates.SelectMany(candidate => candidate.Contributions)
                .Select(contribution => contribution.Key)
                .Distinct()
                .ToDictionary(need => need, need => need.Available);
            var choices = proposal
                .Candidates.Where(candidate =>
                    AIMaintenanceRecoveryProposal.CanRetire(context, candidate.Unit)
                    && _selectionState.CanReserve(GetRetirementReservations(candidate.Unit))
                )
                .ToList();
            var selected = new List<AIMaintenanceRecoveryProposal.Candidate>();
            long needed =
                (long)
                    Math.Max(0, context.Game.Config.AI.Selection.MaintenanceRecovery.TargetHeadroom)
                - ResourceProductionQueries.CalculateMaintenanceHeadroom(
                    context.Game,
                    context.Faction
                );
            long saved = 0;
            while (saved < needed && choices.Count > 0)
            {
                AIMaintenanceRecoveryProposal.Candidate best = null;
                double bestDamage = double.PositiveInfinity;
                foreach (var candidate in choices)
                {
                    double damage = AIProductionProposalScorer.GetRetirementDamage(
                        context,
                        candidate,
                        remaining
                    );
                    if (
                        damage < bestDamage
                        || damage == bestDamage
                            && best != null
                            && (
                                candidate.Unit.GetMaintenanceCost() < best.Unit.GetMaintenanceCost()
                                || candidate.Unit.GetMaintenanceCost()
                                    == best.Unit.GetMaintenanceCost()
                                    && string.CompareOrdinal(
                                        candidate.Unit.InstanceID,
                                        best.Unit.InstanceID
                                    ) < 0
                            )
                    )
                    {
                        best = candidate;
                        bestDamage = damage;
                    }
                }
                if (best == null)
                    break;
                selected.Add(best);
                choices.Remove(best);
                saved += best.Unit.GetMaintenanceCost();
                foreach (var contribution in best.Contributions)
                    remaining[contribution.Key] -= contribution.Value;
            }
            if (selected.Count == 0)
                return false;
            foreach (var candidate in selected)
            {
                _selectionState.Reserve(GetRetirementReservations(candidate.Unit));
                ReserveAffectedDefense(context, candidate.Unit);
                if (
                    candidate.Unit is Building building
                    && building.GetParentOfType<Planet>() is Planet planet
                )
                {
                    string key =
                        building.ProductionType == ManufacturingType.Building
                            ? $"production:building:{planet.InstanceID}"
                            : $"production:{building.ProductionType}:{planet.InstanceID}";
                    _reservedProducerCapacity[key] = int.MaxValue;
                }
            }
            selectedProposal = proposal.WithSelection(selected);
            selectedProposal.SetScore(proposal.Score);
            return true;
        }

        /// <summary>
        /// Prevents departures based on defense totals or stability predicates changed by retirement.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="unit">The retiring unit.</param>
        private void ReserveAffectedDefense(AITurnContext context, IManufacturable unit)
        {
            Planet planet = unit.GetParentOfType<Planet>();
            bool affectsDefense =
                planet != null
                && (
                    unit is Regiment && unit.GetParent() == planet
                    || unit is Building building && building.IsPlanetaryShieldGenerator()
                    || planet.IsHeadquarters && unit is CapitalShip or Starfighter
                );
            if (!affectsDefense)
                return;
            List<AISelectionReservation> reservations = new();
            foreach (
                Fleet fleet in context
                    .Assessment.GetFriendlyFleets(planet)
                    .Where(fleet => fleet.Movement == null)
            )
            {
                Add(reservations, AISelectionReservationKind.FleetOrder, fleet.InstanceID);
                Add(reservations, AISelectionReservationKind.FleetMovement, fleet.InstanceID);
                Add(
                    reservations,
                    AISelectionReservationKind.ContainerTransferSource,
                    fleet.InstanceID
                );
            }
            _selectionState.Reserve(reservations);
        }

        /// <summary>
        /// Reserves actors and affected containers so later proposals cannot use stale capabilities.
        /// </summary>
        /// <param name="unit">The retiring unit.</param>
        /// <returns>The resources affected by retirement.</returns>
        private static List<AISelectionReservation> GetRetirementReservations(IManufacturable unit)
        {
            List<AISelectionReservation> reservations = new();
            Add(reservations, AISelectionReservationKind.MissionActor, unit.InstanceID);
            Add(reservations, AISelectionReservationKind.UnitTransfer, unit.InstanceID);
            Add(
                reservations,
                AISelectionReservationKind.ContainerTransferSource,
                unit.GetParent()?.InstanceID
            );
            if (unit.GetParentOfType<Fleet>() is Fleet fleet)
            {
                Add(reservations, AISelectionReservationKind.FleetOrder, fleet.InstanceID);
                Add(reservations, AISelectionReservationKind.FleetMovement, fleet.InstanceID);
                Add(reservations, AISelectionReservationKind.FleetAttack, fleet.InstanceID);
                Add(
                    reservations,
                    AISelectionReservationKind.FleetCapitalReinforcement,
                    fleet.InstanceID
                );
            }
            if (unit is Building building)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionBuildingReplacement,
                    unit.InstanceID
                );
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    unit.GetParentOfType<Planet>()?.InstanceID,
                    building.BuildingType.ToString()
                );
            }
            return reservations;
        }

        /// <summary>
        /// Attempts to select a facility-removal proposal.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Facility-removal proposal.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIFacilityRemovalProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            IReadOnlyList<Building> resources =
                proposal != null && proposal.ResourceHubType != BuildingType.None
                    ? proposal.GetResourceFacilities(context)
                    : Array.Empty<Building>();
            foreach (Building building in resources)
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionBuildingReplacement,
                    building.InstanceID
                );
            if (!_selectionState.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            if (resources.Count > 0)
            {
                ResourceProductionQueries economy = new(context.Game);
                int lostHeadroom = Math.Max(
                    0,
                    economy.GetSummary(context.Faction).MaintenanceHeadroom
                        - economy.GetSummary(context.Faction, resources).MaintenanceHeadroom
                );
                if (
                    context.Assessment.ProjectedMaintenanceHeadroom
                        - _selectedMaintenanceCost
                        - lostHeadroom
                    < context.Game.Config.AI.Selection.MaintenanceHeadroomTarget
                )
                    return false;
                ReserveMaintenanceCost(lostHeadroom);
            }
            _selectionState.Reserve(reservations);
            return true;
        }

        /// <summary>
        /// Attempts to select a headquarters relocation and reserves its destination capacity.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Headquarters-relocation proposal.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIHeadquartersRelocationProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (!_selectionState.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            _selectionState.Reserve(reservations);
            ReserveDestinationEnergy(
                proposal.Destination,
                BuildingType.Headquarters,
                _buildingEnergyCost
            );
            ReserveMaintenanceCost(proposal.MaintenanceHeadroomCost);
            return true;
        }

        /// <summary>
        /// Resolves the first viable producer and quantity without changing global ordering.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production decision.</param>
        /// <param name="selectedProposal">The exact executable manufacturing action.</param>
        /// <returns>True when the production decision has a selectable exact action.</returns>
        internal bool TryResolve(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            if (proposal.CapitalShipChoices.Count > 0)
            {
                AIProductionDemand remaining = _selectionState.GetRemainingFleetDemand(
                    context,
                    proposal.Demand
                );
                var requirement = remaining
                    .GetCapitalShipRequirements(context.Assessment)
                    .FirstOrDefault(item => item.Deficit > 0);
                if (requirement.Contribution == null)
                {
                    selectedProposal = proposal;
                    return false;
                }
                if (requirement.Contribution((CapitalShip)proposal.Product.GetReference()) <= 0)
                    return TryCreateNextFleetProposal(context, proposal, out selectedProposal);
            }
            int equivalentProducerCount = proposal.ManufacturingCount;
            if (TryResolveProducer(context, proposal, out selectedProposal))
                return true;

            if (proposal.CarriesReducedCountAcrossAlternatives)
                equivalentProducerCount = selectedProposal.ManufacturingCount;

            foreach (AIManufactureProposal alternative in proposal.ProducerAlternatives)
            {
                AIManufactureProposal candidate = alternative;
                if (
                    proposal.CarriesReducedCountAcrossAlternatives
                    && equivalentProducerCount < candidate.ManufacturingCount
                )
                    candidate = alternative.WithManufacturingCount(equivalentProducerCount);
                if (TryResolveProducer(context, candidate, out selectedProposal))
                    return true;

                if (proposal.CarriesReducedCountAcrossAlternatives)
                    equivalentProducerCount = selectedProposal.ManufacturingCount;
            }

            if (
                proposal.CapitalShipChoices.Count > 0
                && TryCreateNextFleetProposal(context, proposal, out selectedProposal)
            )
                return true;
            selectedProposal = proposal;
            return false;
        }

        /// <summary>
        /// Chooses a fresh hull for uncovered fleet needs and ranks its remaining producer options.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="request">The original hull and producer catalogue.</param>
        /// <param name="next">The next independently selected and scored production request.</param>
        /// <returns>True when an available producer can fill an uncovered fleet capability.</returns>
        internal bool TryCreateNextFleetProposal(
            AITurnContext context,
            AIManufactureProposal request,
            out AIManufactureProposal next
        )
        {
            next = null;
            if (request.CapitalShipChoices.Count == 0)
                return false;
            AIProductionDemand remaining = _selectionState.GetRemainingFleetDemand(
                context,
                request.Demand
            );
            foreach (var requirement in remaining.GetCapitalShipRequirements(context.Assessment))
            {
                if (requirement.Deficit <= 0)
                    continue;
                List<AIManufactureProposal> eligible = new();
                foreach (AIManufactureProposal choice in request.CapitalShipChoices)
                {
                    if (requirement.Contribution((CapitalShip)choice.Product.GetReference()) <= 0)
                        continue;
                    AIManufactureProposal[] producers = new[] { choice }
                        .Concat(choice.ProducerAlternatives)
                        .Where(candidate => CanSelect(context, candidate))
                        .ToArray();
                    if (producers.Length > 0)
                        eligible.Add(
                            new AIManufactureProposal(
                                producers,
                                capitalShipChoices: request.CapitalShipChoices
                            )
                        );
                }
                if (eligible.Count == 0)
                    continue;
                int preferredInterdiction = context
                    .Game
                    .Config
                    .AI
                    .Selection
                    .PreferredInterdictionCountPerFleet;
                int committedInterdiction = _selectionState
                    .GetCommittedFleetHulls(request.Demand.DestinationFleet)
                    .Count(hull => hull.HasGravityWell);
                if (
                    preferredInterdiction > 0
                    && context.Assessment.GetProjectedFleetInterdictionCount(
                        request.Demand.DestinationFleet
                    ) + committedInterdiction
                        >= preferredInterdiction
                    && eligible.Any(choice =>
                        !((CapitalShip)choice.Product.GetReference()).HasGravityWell
                    )
                )
                    eligible.RemoveAll(choice =>
                        ((CapitalShip)choice.Product.GetReference()).HasGravityWell
                    );
                next = eligible[context.Random.NextInt(0, eligible.Count)];
                AIManufactureProposal scored = new(
                    remaining,
                    next.ProducerPlanet,
                    next.Product,
                    true
                );
                next.SetScore(_scorer.Score(context, scored));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Scores a resolved fallback after shared resources have been reserved.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production decision.</param>
        /// <param name="selectedProposal">The selected exact action.</param>
        internal void ScoreResolved(
            AITurnContext context,
            AIProposal proposal,
            AIProposal selectedProposal
        )
        {
            if (
                selectedProposal is AIManufactureProposal selectedManufactureProposal
                && !ReferenceEquals(selectedProposal, proposal)
            )
                selectedManufactureProposal.SetScore(
                    _scorer.Score(context, selectedManufactureProposal)
                );
        }

        /// <summary>
        /// Reserves production resources consumed by an accepted manufacturing action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        internal void Reserve(AIManufactureProposal proposal)
        {
            _selectionState.Reserve(GetReservations(proposal));
            ReserveProducerCapacity(proposal);
            ReserveDestinationEnergy(proposal);
            ReserveFacilityRole(proposal);
            _selectedMaintenanceCost +=
                proposal.GetMaintenanceCost() - proposal.ReplacedMaintenanceCost;
            if (proposal.Demand?.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve)
                _selectedPlanetaryStarfighterCount += proposal.GetManufacturingCount();
            if (
                proposal.Demand?.Kind == AIProductionDemandKind.FleetCapitalShip
                && proposal.Demand.DestinationFleet is Fleet fleet
                && proposal.Product.GetReference() is CapitalShip hull
            )
            {
                _selectionState.ReserveFleetHull(fleet, hull);
            }
        }

        /// <summary>
        /// Returns whether an exact manufacturing action fits its maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The exact manufacturing action.</param>
        /// <returns>True when the action can reserve its maintenance cost.</returns>
        internal bool CanReserve(AITurnContext context, AIManufactureProposal proposal)
        {
            return _selectionState.CanReserve(GetReservations(proposal))
                && (proposal.IsContinuation || !WouldExceedMaintenanceHeadroom(context, proposal))
                && CanReserveIdleShipyardCapacity(context, proposal)
                && CanReserveFacilityRole(proposal);
        }

        /// <summary>
        /// Orders competing fleet production by score, using fleet priority to break score ties.
        /// </summary>
        /// <param name="context">The faction turn containing ranked fleet needs.</param>
        /// <param name="proposals">Globally ranked proposals to order without moving other domains.</param>
        internal static void OrderFleetProduction(
            AITurnContext context,
            IList<AIProposal> proposals
        )
        {
            if (context?.FleetProductionPriorities.Count is null or 0)
                return;

            Dictionary<string, int> ranks = context
                .FleetProductionPriorities.Select((fleet, index) => (fleet, index))
                .ToDictionary(item => item.fleet.InstanceID, item => item.index);
            foreach (
                IGrouping<AIProposalPriority, int> group in Enumerable
                    .Range(0, proposals.Count)
                    .Where(index => IsAttackFleetProduction(proposals[index]))
                    .GroupBy(index => proposals[index].Priority)
            )
            {
                int[] slots = group.ToArray();
                AIProposal[] ordered = slots
                    .Select(index => proposals[index])
                    .OrderByDescending(proposal => proposal.Score)
                    .ThenBy(proposal =>
                        ((AIManufactureProposal)proposal).Demand.DestinationFleet is Fleet fleet
                        && ranks.TryGetValue(fleet.InstanceID, out int rank)
                            ? rank
                            : int.MaxValue
                    )
                    .ThenBy(proposal =>
                        ((AIManufactureProposal)proposal).Demand.Kind
                        == AIProductionDemandKind.FleetCapitalShip
                            ? 0
                            : 1
                    )
                    .ToArray();
                for (int index = 0; index < slots.Length; index++)
                    proposals[slots[index]] = ordered[index];
            }
        }

        /// <summary>
        /// Prioritizes completing a sector's primary industrial pair within existing expansion slots.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposals">Ranked proposals whose unrelated domain positions are preserved.</param>
        /// <returns>The proposals with viable industrial steps ordered ahead of further expansion.</returns>
        internal List<AIProposal> OrderInfrastructureProduction(
            AITurnContext context,
            IEnumerable<AIProposal> proposals
        )
        {
            List<AIProposal> ordered = proposals.ToList();
            Dictionary<string, (BuildingType NextType, double Completion)> development = new(
                StringComparer.Ordinal
            );
            Dictionary<AIProposal, (bool NextStep, double Completion)> priorities = new();
            foreach (AIManufactureProposal proposal in ordered.OfType<AIManufactureProposal>())
            {
                if (
                    proposal.Demand?.Kind
                        is not (
                            AIProductionDemandKind.ConstructionFacility
                            or AIProductionDemandKind.Shipyard
                        )
                    || proposal.Demand.DestinationPlanet is not Planet destination
                )
                    continue;

                string sectorId = context.Assessment.GetPlanetSystemId(destination);
                if (!development.TryGetValue(sectorId, out var step))
                {
                    step = context.StrategicPlan.GetInfrastructureDevelopmentStep(
                        destination,
                        _reservedDestinationBuildings
                    );
                    development.Add(sectorId, step);
                }
                bool nextStep =
                    proposal.Demand.InfrastructureHubIndex == 0
                    && proposal.Demand.BuildingType == step.NextType;
                priorities.Add(proposal, (nextStep, nextStep ? step.Completion : 0));
            }

            foreach (
                IGrouping<AIProposalPriority, int> group in Enumerable
                    .Range(0, ordered.Count)
                    .Where(index => priorities.ContainsKey(ordered[index]))
                    .GroupBy(index => ordered[index].Priority)
            )
            {
                int[] slots = group.ToArray();
                AIProposal[] ranked = slots
                    .Select(index => ordered[index])
                    .OrderByDescending(proposal => priorities[proposal].NextStep)
                    .ThenByDescending(proposal => priorities[proposal].Completion)
                    .ThenByDescending(proposal => proposal.Score)
                    .ToArray();
                for (int index = 0; index < slots.Length; index++)
                    ordered[slots[index]] = ranked[index];
            }
            return ordered;
        }

        /// <summary>
        /// Identifies manufacturing actions competing to assemble an attack fleet.
        /// </summary>
        /// <param name="proposal">The globally ranked action.</param>
        /// <returns>True for battle fleet reinforcement and new battle fleet seeds.</returns>
        private static bool IsAttackFleetProduction(AIProposal proposal)
        {
            if (proposal is not AIManufactureProposal manufacture)
                return false;
            AIProductionDemand demand = manufacture.Demand;
            return demand?.Kind == AIProductionDemandKind.FleetSeedCapitalShip
                || demand?.Kind
                    is AIProductionDemandKind.FleetCapitalShip
                        or AIProductionDemandKind.FleetStarfighter
                        or AIProductionDemandKind.FleetRegiment
                    && demand.DestinationFleet?.RoleType == FleetRoleType.Battle
                    && demand.DestinationFleet.Order?.OrderType != FleetOrderType.Defend;
        }

        /// <summary>
        /// Returns whether opportunistic fighter work fits the faction-wide concurrency limit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the proposal is not opportunistic or a filler slot remains.</returns>
        private bool CanReserveIdleShipyardCapacity(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (proposal?.Demand?.UsesIdleShipyardCapacity != true)
                return true;

            int limit = context.Game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount;
            return context.Assessment.UnfinishedPlanetStarfighterCount
                    + _selectedPlanetaryStarfighterCount
                    + proposal.GetManufacturingCount()
                <= limit;
        }

        /// <summary>
        /// Resolves whether one exact producer can accept the requested item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The exact-producer manufacturing option.</param>
        /// <param name="selectedProposal">The exact manufacturing action.</param>
        /// <returns>True when at least one item is selectable.</returns>
        private bool TryResolveProducer(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            selectedProposal = proposal;
            return proposal.GetManufacturingCount() > 0 && CanSelect(context, proposal);
        }

        /// <summary>
        /// Returns whether a manufacturing action is currently feasible.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when its claims and production resources are available.</returns>
        private bool CanSelect(AITurnContext context, AIManufactureProposal proposal)
        {
            return proposal?.CanSelect(context) == true
                && CanReplaceCurrentOrder(context, proposal)
                && FillsUncommittedFleetNeed(context, proposal)
                && _selectionState.CanReserve(GetReservations(proposal))
                && HasProducerCapacity(proposal)
                && GetAvailableDestinationEnergy(context, proposal)
                    >= proposal.GetManufacturingCount()
                && CanReserveFacilityRole(proposal)
                && ContinuesProductionStream(proposal);
        }

        /// <summary>
        /// Compares an incoming request with useful work already occupying its producer.
        /// </summary>
        /// <param name="context">The turn containing the current production needs.</param>
        /// <param name="proposal">The exact incoming manufacturing action.</param>
        /// <returns>True when the incoming work outranks the current order.</returns>
        private bool CanReplaceCurrentOrder(AITurnContext context, AIManufactureProposal proposal)
        {
            IManufacturable current = proposal.CurrentOrder;
            if (current == null || proposal.IsContinuation)
                return true;
            double score = _scorer.Score(context, proposal);
            if (
                proposal.Priority < AIProposalPriority.Mandatory
                && score <= context.Game.Config.AI.Selection.MinimumSelectableScore
            )
                return false;
            AIProductionDemand demand = context.GetCurrentProductionDemand(current);
            if (demand == null)
                return true;
            AIManufactureProposal continuation = new AIManufactureProposal(
                demand,
                proposal.ProducerPlanet,
                new Technology(current),
                false
            );
            return proposal.Priority > continuation.Priority
                || proposal.Priority == continuation.Priority
                    && score > _scorer.Score(context, continuation);
        }

        /// <summary>
        /// Rejects hulls whose useful contribution is already covered by this turn's accepted orders.
        /// </summary>
        /// <param name="context">The faction turn supplying consistent combat valuations.</param>
        /// <param name="proposal">The exact hull and producer under consideration.</param>
        /// <returns>True when the hull satisfies at least one outstanding capability.</returns>
        private bool FillsUncommittedFleetNeed(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            AIProductionDemand demand = proposal.Demand;
            if (
                demand?.Kind != AIProductionDemandKind.FleetCapitalShip
                || demand.DestinationFleet == null
                || proposal.Product.GetReference() is not CapitalShip hull
            )
                return true;

            return _selectionState.FillsFleetNeed(context, demand, hull);
        }

        /// <summary>
        /// Returns whether a facility proposal preserves the destination's role for this turn.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the destination is unreserved or reserved for the same facility category.</returns>
        private bool CanReserveFacilityRole(AIManufactureProposal proposal)
        {
            if (
                proposal?.IsProductionFacilityExpansion != true
                || proposal.Destination is not Planet destination
            )
                return true;

            return !_reservedFacilityRoles.TryGetValue(
                    destination.InstanceID,
                    out BuildingType role
                )
                || role == proposal.Demand.BuildingType;
        }

        /// <summary>
        /// Reserves a destination planet for the selected production-facility category.
        /// </summary>
        /// <param name="proposal">The selected manufacturing action.</param>
        private void ReserveFacilityRole(AIManufactureProposal proposal)
        {
            if (
                proposal?.IsProductionFacilityExpansion == true
                && proposal.Destination is Planet destination
            )
                _reservedFacilityRoles[destination.InstanceID] = proposal.Demand.BuildingType;
        }

        /// <summary>
        /// Returns whether the selected producer has capacity remaining this turn.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the producer has unreserved capacity.</returns>
        private bool HasProducerCapacity(AIManufactureProposal proposal)
        {
            return proposal.IsContinuation
                || !_reservedProducerCapacity.ContainsKey(proposal.GetProducerCapacityKey());
        }

        /// <summary>
        /// Returns whether a proposal preserves the producer's current product stream.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the stream is idle or already produces the proposed item.</returns>
        private bool ContinuesProductionStream(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            string proposedTypeId = proposal.Product?.GetReference()?.GetTypeID();
            if (string.IsNullOrEmpty(proposedTypeId))
                return false;

            if (_selectedProducerProducts.TryGetValue(capacityKey, out string selectedTypeId))
                return selectedTypeId == proposedTypeId;
            if (proposal.CurrentOrder != null)
                return true;
            if (_drainingProducerStreams.Contains(capacityKey))
                return false;

            string currentTypeId = GetCurrentProductTypeId(proposal, capacityKey);
            if (string.IsNullOrEmpty(currentTypeId) || currentTypeId == proposedTypeId)
                return true;

            _drainingProducerStreams.Add(capacityKey);
            return false;
        }

        /// <summary>
        /// Returns the product type currently queued by a producer stream.
        /// </summary>
        /// <param name="proposal">A proposal targeting the producer stream.</param>
        /// <param name="capacityKey">The producer stream key.</param>
        /// <returns>The queued type, an empty value, or the mixed-type marker.</returns>
        private string GetCurrentProductTypeId(AIManufactureProposal proposal, string capacityKey)
        {
            if (_currentProducerProducts.TryGetValue(capacityKey, out string currentTypeId))
                return currentTypeId;

            currentTypeId = string.Empty;
            if (
                proposal
                    .ProducerPlanet.GetManufacturingQueue()
                    .TryGetValue(proposal.Demand.ManufacturingType, out List<IManufacturable> queue)
                && queue != null
            )
            {
                foreach (IManufacturable item in queue)
                {
                    string itemTypeId = item?.GetTypeID();
                    if (string.IsNullOrEmpty(currentTypeId))
                    {
                        currentTypeId = itemTypeId ?? string.Empty;
                        continue;
                    }

                    if (currentTypeId != itemTypeId)
                    {
                        currentTypeId = _mixedProductType;
                        break;
                    }
                }
            }

            _currentProducerProducts.Add(capacityKey, currentTypeId);
            return currentTypeId;
        }

        /// <summary>
        /// Reserves the producer capacity consumed by an accepted action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        private void ReserveProducerCapacity(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            _selectedProducerProducts[capacityKey] = proposal.Product.GetReference().GetTypeID();
            if (proposal.IsContinuation)
                return;
            _reservedProducerCapacity[capacityKey] = int.MaxValue;
        }

        /// <summary>
        /// Returns destination energy remaining after accepted building actions.
        /// </summary>
        /// <param name="context">The current turn's finished layout allocations.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>Remaining energy, or an unbounded value for non-building work.</returns>
        private int GetAvailableDestinationEnergy(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (proposal.IsContinuation)
                return int.MaxValue;
            if (
                proposal?.Product?.GetReference() is not Building building
                || proposal.Demand?.Kind == AIProductionDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
                return int.MaxValue;

            _reservedDestinationBuildings.TryGetValue(
                destination.InstanceID,
                out var selectedBuildings
            );
            int reserved =
                (selectedBuildings?.Values.Sum() ?? 0)
                - (
                    _continuedBuildingCounts.TryGetValue(destination.InstanceID, out int continued)
                        ? continued
                        : 0
                );
            int actualAvailable =
                destination.GetAvailableEnergy() + proposal.ReleasedDestinationSpace - reserved;
            int plannedAvailable = context.StrategicPlan.GetAvailableConstructionSpace(
                destination,
                building.BuildingType,
                proposal.Demand.IsEmergencyDefense,
                selectedBuildings
            );
            return Math.Max(0, Math.Min(actualAvailable, plannedAvailable));
        }

        /// <summary>
        /// Reserves destination energy consumed by an accepted building action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        private void ReserveDestinationEnergy(AIManufactureProposal proposal)
        {
            if (
                proposal.Product?.GetReference() is not Building building
                || proposal.Demand?.Kind == AIProductionDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
                return;

            ReserveDestinationEnergy(
                destination,
                building.BuildingType,
                proposal.GetManufacturingCount()
            );
            if (proposal.IsContinuation)
            {
                _continuedBuildingCounts.TryGetValue(destination.InstanceID, out int count);
                _continuedBuildingCounts[destination.InstanceID] =
                    count + proposal.GetManufacturingCount();
            }
        }

        /// <summary>
        /// Reserves building slots of one type at a destination planet.
        /// </summary>
        /// <param name="destination">Planet whose building capacity is reserved.</param>
        /// <param name="buildingType">The building type occupying the slots.</param>
        /// <param name="count">Number of building slots to reserve.</param>
        private void ReserveDestinationEnergy(
            Planet destination,
            BuildingType buildingType,
            int count
        )
        {
            if (
                !_reservedDestinationBuildings.TryGetValue(
                    destination.InstanceID,
                    out var buildings
                )
            )
            {
                buildings = new Dictionary<BuildingType, int>();
                _reservedDestinationBuildings.Add(destination.InstanceID, buildings);
            }
            buildings.TryGetValue(buildingType, out int reserved);
            buildings[buildingType] = reserved + count;
        }

        /// <summary>
        /// Reserves maintenance headroom consumed by an accepted action.
        /// </summary>
        /// <param name="maintenanceCost">The non-negative maintenance cost to reserve.</param>
        private void ReserveMaintenanceCost(int maintenanceCost)
        {
            long reserved = (long)_selectedMaintenanceCost + Math.Max(0, maintenanceCost);
            _selectedMaintenanceCost = (int)Math.Min(int.MaxValue, reserved);
        }

        /// <summary>
        /// Returns whether an action would exceed its maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the action exceeds its required reserve.</returns>
        private bool WouldExceedMaintenanceHeadroom(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (proposal.Demand?.RestoresMaintenanceCapacity == true)
                return false;

            int maintenanceCost = proposal.GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return false;

            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - maintenanceCost
                + proposal.ReplacedMaintenanceCost;
            return projectedHeadroom < proposal.GetMinimumMaintenanceHeadroom(context);
        }

        /// <summary>
        /// Returns typed reservations for a production proposal.
        /// </summary>
        /// <param name="proposal">Production proposal to inspect.</param>
        /// <returns>Reservations required by the proposal.</returns>
        private static List<AISelectionReservation> GetReservations(AIProposal proposal)
        {
            List<AISelectionReservation> reservations = new();
            if (proposal is AIFacilityRemovalProposal removal)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    removal.Planet?.InstanceID,
                    removal.BuildingType.ToString()
                );
                return reservations;
            }

            if (proposal is AIHeadquartersRelocationProposal relocation)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    relocation.Destination?.InstanceID,
                    relocation.BuildingToRemove?.GetBuildingType().ToString()
                );
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionBuildingReplacement,
                    relocation.BuildingToRemove?.InstanceID
                );
                foreach (Fleet fleet in relocation.DefensiveFleets)
                {
                    Add(reservations, AISelectionReservationKind.FleetOrder, fleet.InstanceID);
                    Add(reservations, AISelectionReservationKind.FleetMovement, fleet.InstanceID);
                }
                Add(
                    reservations,
                    AISelectionReservationKind.MissionActor,
                    relocation.EspionageAgent?.InstanceID
                );
                Add(
                    reservations,
                    AISelectionReservationKind.Mission,
                    relocation.EspionageMission?.InstanceID
                );
                return reservations;
            }

            if (proposal is not AIManufactureProposal manufacture)
                return reservations;

            if (manufacture.Demand != null && !manufacture.DistributesDemand)
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionDemand,
                    manufacture.Demand.Id
                );
            if (manufacture.ProducerPlanet != null)
                Add(
                    reservations,
                    AISelectionReservationKind.ManufacturingLane,
                    manufacture.GetProducerCapacityKey()
                );
            if (
                manufacture.Product?.GetReference() is Building building
                && manufacture.Destination is Planet destinationPlanet
                && building.GetBuildingType()
                    is BuildingType.Shipyard
                        or BuildingType.ConstructionFacility
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    destinationPlanet.InstanceID,
                    building.GetBuildingType().ToString()
                );
            Add(
                reservations,
                AISelectionReservationKind.ProductionBuildingReplacement,
                manufacture.Demand?.BuildingToReplace?.InstanceID
            );
            if (manufacture.Destination is Fleet destinationFleet && !manufacture.DistributesDemand)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FleetReinforcement,
                    destinationFleet.InstanceID,
                    manufacture.Demand?.Kind.ToString()
                );
                if (manufacture.Demand?.Kind == AIProductionDemandKind.FleetCapitalShip)
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetCapitalReinforcement,
                        destinationFleet.InstanceID
                    );
            }
            if (
                manufacture.Demand?.Kind
                is AIProductionDemandKind.FleetSeedCapitalShip
                    or AIProductionDemandKind.ColonizationFleetSeedCapitalShip
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FleetCreation,
                    manufacture.Destination?.GetOwnerInstanceID()
                );
            return reservations;
        }

        /// <summary>
        /// Adds a reservation only when its primary identity exists.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="kind">Reservation category.</param>
        /// <param name="primaryId">Primary resource identity.</param>
        /// <param name="secondaryId">Optional secondary identity.</param>
        private static void Add(
            List<AISelectionReservation> reservations,
            AISelectionReservationKind kind,
            string primaryId,
            string secondaryId = null
        )
        {
            if (!string.IsNullOrEmpty(primaryId))
                reservations.Add(new AISelectionReservation(kind, primaryId, secondaryId));
        }
    }
}
