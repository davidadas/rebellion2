using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.Game.Units;
using Rebellion.Simulation;

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

            AIAttackDemand attack = context.GetAttackDemand(proposal.TargetPlanet);
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
                        selectedBombardment
                    )
                    && context.Assessment.CanBombardDefenders(
                        proposal.TargetFleet,
                        proposal.TargetPlanet,
                        true,
                        selectedBombardment + additionalBombardment
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
            _fleetReinforcements.TryGetValue(
                demand.DestinationFleet.InstanceID,
                out List<CapitalShip> selected
            );
            selected ??= new();
            if (
                demand.RegimentCapacityDeficit > selected.Sum(ship => ship.RegimentCapacity)
                    && hull.RegimentCapacity > 0
                || demand.StarfighterCapacityDeficit
                    > selected.Sum(ship => ship.StarfighterCapacity)
                    && hull.StarfighterCapacity > 0
                || demand.BombardmentDeficit > selected.Sum(ship => ship.Bombardment)
                    && hull.Bombardment > 0
                || demand.CapitalFirepowerDeficit
                    > selected.Sum(
                        SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstCapitalShips
                    )
                    && SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstCapitalShips(
                        hull
                    ) > 0
                || demand.StarfighterFirepowerDeficit
                    > selected.Sum(
                        SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstStarfighters
                    )
                    && SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstStarfighters(
                        hull
                    ) > 0
                || demand.CombatStrengthDeficit
                    > selected.Sum(ship => GetHullCombatValue(context, demand, ship))
                    && GetHullCombatValue(context, demand, hull) > 0
                || demand.EscortDeficit
                    > selected.Count(SpaceCombatStrengthCalculator.IsArmedEscort)
                    && SpaceCombatStrengthCalculator.IsArmedEscort(hull)
                || demand.InterdictionDeficit > selected.Count(ship => ship.HasGravityWell)
                    && hull.HasGravityWell
                || demand.PlanetDestroyerDeficit > selected.Count(ship => ship.CanDestroyPlanets)
                    && hull.CanDestroyPlanets
            )
                return true;

            bool hasCapabilityDeficit =
                demand.RegimentCapacityDeficit > 0
                || demand.StarfighterCapacityDeficit > 0
                || demand.BombardmentDeficit > 0
                || demand.CapitalFirepowerDeficit > 0
                || demand.StarfighterFirepowerDeficit > 0
                || demand.CombatStrengthDeficit > 0
                || demand.EscortDeficit > 0
                || demand.InterdictionDeficit > 0
                || demand.PlanetDestroyerDeficit > 0;
            return !hasCapabilityDeficit && selected.Count == 0;
        }

        /// <summary>
        /// Values a committed hull using the combat measure required by its receiving fleet.
        /// </summary>
        /// <param name="context">The faction combat assessment.</param>
        /// <param name="demand">The receiving fleet's shortage.</param>
        /// <param name="hull">The committed hull.</param>
        /// <returns>The hull's contribution to the required combat measure.</returns>
        private static int GetHullCombatValue(
            AITurnContext context,
            AIProductionDemand demand,
            CapitalShip hull
        )
        {
            return demand.ReferencePlanet == null
                ? context.Assessment.GetProjectedCapitalShipCombatValue(hull)
                : context.Assessment.GetProjectedCapitalShipCombatValueAgainstCapitalShips(hull);
        }
    }
}
