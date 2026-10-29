using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.Game.Units;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Tracks typed cross-domain reservations during one selection pass.
    /// </summary>
    internal sealed class AISelectionState
    {
        private readonly HashSet<AISelectionReservation> _reservations = new();
        private readonly Dictionary<string, List<CapitalShip>> _fleetReinforcements = new();
        private readonly HashSet<CapitalShip> _departingCapitalShips = new();

        /// <summary>
        /// Returns whether all requested resources remain available.
        /// </summary>
        /// <param name="reservations">Resources requested by one proposal.</param>
        /// <returns>True when none has already been reserved.</returns>
        internal bool CanReserve(IEnumerable<AISelectionReservation> reservations)
        {
            return reservations?.Any(reservation =>
                {
                    if (reservation.Kind == AISelectionReservationKind.FleetOrderContinuation)
                        return _reservations.Contains(
                            new AISelectionReservation(
                                AISelectionReservationKind.FleetOrder,
                                reservation.PrimaryId
                            )
                        );
                    return _reservations.Contains(reservation)
                        || reservation.Kind == AISelectionReservationKind.FleetOrder
                            && _reservations.Contains(
                                new AISelectionReservation(
                                    AISelectionReservationKind.FleetOrderContinuation,
                                    reservation.PrimaryId
                                )
                            );
                }) != true;
        }

        /// <summary>
        /// Reserves resources for an accepted proposal.
        /// </summary>
        /// <param name="reservations">Resources to reserve.</param>
        internal void Reserve(IEnumerable<AISelectionReservation> reservations)
        {
            if (reservations == null)
                return;

            foreach (AISelectionReservation reservation in reservations)
                _reservations.Add(reservation);
        }

        /// <summary>
        /// Checks donor safety and remaining receiving-fleet demand against accepted actions.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The proposed transfer.</param>
        /// <returns>True when the transfer preserves its donor and fills an uncommitted need.</returns>
        internal bool CanReserveTransfer(AITurnContext context, AITransferUnitProposal proposal)
        {
            if (
                proposal.Unit is not CapitalShip ship
                || proposal.SourceContainer is not Fleet donor
            )
                return true;
            if (!context.StrategicPlan.CanReleaseCapitalShip(donor, ship, _departingCapitalShips))
                return false;
            AIProductionDemand demand = context.ProductionDemands.FirstOrDefault(candidate =>
                candidate.Kind == AIProductionDemandKind.FleetCapitalShip
                && candidate.DestinationFleet == proposal.TargetFleet
            );
            if (demand != null && FillsFleetNeed(context, demand, ship))
                return true;

            AIAttackDemand attack = context.GetAttackDemand(
                proposal.TargetPlanet,
                proposal.TargetFleet
            );
            if (proposal.TargetFleet.Order?.OrderType == FleetOrderType.Attack && attack != null)
            {
                _fleetReinforcements.TryGetValue(
                    proposal.TargetFleet.InstanceID,
                    out List<CapitalShip> selected
                );
                int selectedBombardment =
                    selected?.Sum(hull =>
                        context.Assessment.GetProjectedCapitalShipBombardmentStrength(
                            proposal.TargetFleet,
                            hull
                        )
                    )
                    ?? 0;
                int additionalBombardment =
                    context.Assessment.GetProjectedCapitalShipBombardmentStrength(
                        proposal.TargetFleet,
                        ship
                    );
                if (
                    (
                        attack.RegimentCount > attack.OccupationRegimentCount
                        || attack.RegimentStrength > 0
                    )
                    && !context.Assessment.CanBombardDefenders(
                        proposal.TargetFleet,
                        proposal.TargetPlanet,
                        true,
                        selectedBombardment,
                        attack.ShieldStrength
                    )
                    && context.Assessment.CanBombardDefenders(
                        proposal.TargetFleet,
                        proposal.TargetPlanet,
                        true,
                        selectedBombardment + additionalBombardment,
                        attack.ShieldStrength
                    )
                )
                    return true;
                int committedRegiments =
                    selected?.Sum(context.Assessment.GetReadyCapitalShipRegimentCount) ?? 0;
                int committedStrength =
                    selected?.Sum(hull =>
                        context.Assessment.GetProjectedCapitalShipRegimentAttackStrength(
                            proposal.TargetFleet,
                            hull
                        )
                    )
                    ?? 0;
                if (
                    context.Assessment.GetReadyCapitalShipRegimentCount(ship) > 0
                        && context.Assessment.GetFleetLoadedRegimentCount(proposal.TargetFleet)
                            + committedRegiments
                            < attack.RegimentCount
                    || context.Assessment.GetProjectedCapitalShipRegimentAttackStrength(
                        proposal.TargetFleet,
                        ship
                    ) > 0
                        && context.Assessment.GetProjectedFleetRegimentAttackStrength(
                            proposal.TargetFleet
                        ) + committedStrength
                            < attack.RegimentStrength
                )
                    return true;
            }

            return demand == null && !context.HasGeneratedProductionDemands;
        }

        /// <summary>
        /// Records the receiving and departing hull contributions of an accepted transfer.
        /// </summary>
        /// <param name="proposal">The accepted transfer.</param>
        internal void ReserveTransfer(AITransferUnitProposal proposal)
        {
            if (proposal.Unit is CapitalShip ship)
            {
                ReserveFleetHull(proposal.TargetFleet, ship);
                _departingCapitalShips.Add(ship);
            }
        }

        /// <summary>
        /// Records a hull committed by production or movement to a receiving fleet.
        /// </summary>
        /// <param name="fleet">The receiving fleet.</param>
        /// <param name="hull">The committed hull or manufacturing template.</param>
        internal void ReserveFleetHull(Fleet fleet, CapitalShip hull)
        {
            if (!_fleetReinforcements.TryGetValue(fleet.InstanceID, out List<CapitalShip> selected))
                _fleetReinforcements[fleet.InstanceID] = selected = new();
            selected.Add(hull);
        }

        /// <summary>
        /// Returns whether a hull contributes to a capability not covered by accepted actions.
        /// </summary>
        /// <param name="context">The faction combat assessment.</param>
        /// <param name="demand">The receiving fleet's capability shortages.</param>
        /// <param name="hull">The candidate hull.</param>
        /// <returns>True when the hull fills an uncommitted capability.</returns>
        internal bool FillsFleetNeed(
            AITurnContext context,
            AIProductionDemand demand,
            CapitalShip hull
        )
        {
            AIProductionDemand remaining = GetRemainingFleetDemand(context, demand);
            bool hasRequirements = demand
                .GetCapitalShipRequirements(context.Assessment)
                .Any(item => item.Deficit > 0);
            return remaining
                    .GetCapitalShipRequirements(context.Assessment)
                    .Any(item => item.Deficit > 0 && item.Contribution(hull) > 0)
                || !hasRequirements && GetCommittedFleetHulls(demand.DestinationFleet).Count == 0;
        }

        /// <summary>
        /// Returns hulls accepted for a fleet during the current selection pass.
        /// </summary>
        /// <param name="fleet">The receiving fleet.</param>
        /// <returns>Accepted manufacturing and transfer contributions.</returns>
        internal IReadOnlyList<CapitalShip> GetCommittedFleetHulls(Fleet fleet)
        {
            return _fleetReinforcements.TryGetValue(fleet.InstanceID, out var hulls)
                ? hulls
                : Array.Empty<CapitalShip>();
        }

        /// <summary>
        /// Removes accepted hull contributions from a fleet's measured requirements.
        /// </summary>
        /// <param name="context">The current combat measurements.</param>
        /// <param name="demand">The original measured fleet request.</param>
        /// <returns>The requirements still uncovered by this turn's assignments.</returns>
        internal AIProductionDemand GetRemainingFleetDemand(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            return demand.WithoutCommittedShips(
                context.Assessment,
                GetCommittedFleetHulls(demand.DestinationFleet)
            );
        }
    }
}
