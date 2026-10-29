using System.Collections.Generic;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Owns resource contention among fleet and unit-transfer proposals.
    /// </summary>
    internal sealed class AIFleetSelector
    {
        private readonly AISelectionState _state;

        /// <summary>
        /// Creates a fleet selector using shared typed reservation state.
        /// </summary>
        /// <param name="state">Shared selection reservation state.</param>
        internal AIFleetSelector(AISelectionState state)
        {
            _state = state;
        }

        /// <summary>
        /// Attempts to select a fleet-domain proposal and reserve its resources.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Proposal to select.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (
                !_state.CanReserve(reservations)
                || proposal?.CanSelect(context) != true
                || proposal is AITransferUnitProposal transfer
                    && !_state.CanReserveTransfer(context, transfer)
            )
                return false;

            _state.Reserve(reservations);
            if (proposal is AITransferUnitProposal selectedTransfer)
                _state.ReserveTransfer(selectedTransfer);
            return true;
        }

        /// <summary>
        /// Returns typed reservations for a fleet-domain proposal.
        /// </summary>
        /// <param name="proposal">Proposal to inspect.</param>
        /// <returns>Reservations required by the proposal.</returns>
        private static List<AISelectionReservation> GetReservations(AIProposal proposal)
        {
            List<AISelectionReservation> reservations = new();
            switch (proposal)
            {
                case AIOfficerCommandProposal command:
                    Add(
                        reservations,
                        AISelectionReservationKind.MissionActor,
                        command.Officer.InstanceID
                    );
                    Add(
                        reservations,
                        AISelectionReservationKind.UnitTransfer,
                        command.Officer.InstanceID
                    );
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetCommand,
                        command.Fleet.InstanceID,
                        command.Rank.ToString()
                    );
                    break;
                case AIClearFleetOrderProposal clear:
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetOrder,
                        clear.Fleet?.InstanceID
                    );
                    break;
                case AIFleetAttackProposal attack:
                    AddAttackReservations(reservations, attack);
                    break;
                case AIFleetDefenseProposal defense:
                    bool continuingDefense =
                        defense.Fleet?.Order?.OrderType == FleetOrderType.Defend
                        && defense.Fleet.Order.TargetPlanetId == defense.TargetPlanet?.InstanceID
                        && defense.Fleet.GetParentOfType<Planet>()?.InstanceID
                            == defense.TargetPlanet?.InstanceID
                        && defense.Fleet.Movement == null
                        && !defense.Fleet.IsInCombat;
                    Add(
                        reservations,
                        continuingDefense
                            ? AISelectionReservationKind.FleetOrderContinuation
                            : AISelectionReservationKind.FleetOrder,
                        defense.Fleet?.InstanceID
                    );
                    if (!continuingDefense)
                        Add(
                            reservations,
                            AISelectionReservationKind.FleetMovement,
                            defense.Fleet?.InstanceID
                        );
                    Add(
                        reservations,
                        AISelectionReservationKind.PlanetDefense,
                        defense.TargetPlanet?.InstanceID
                    );
                    break;
                case AIFleetEvacuationProposal evacuation:
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetOrder,
                        evacuation.Fleet?.InstanceID
                    );
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetMovement,
                        evacuation.Fleet?.InstanceID
                    );
                    break;
                case AIFleetReinforcementRedirectionProposal redirection:
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetCapitalReinforcement,
                        redirection.Fleet?.InstanceID
                    );
                    break;
                case AIColonizationProposal colonization:
                    AddColonizationReservations(reservations, colonization);
                    break;
                case AIColonizationCampaignProposal campaign:
                    AddCampaignReservations(reservations, campaign);
                    break;
                case AIOrbitalEngagementProposal engagement:
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetOrder,
                        engagement.Fleet?.InstanceID
                    );
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetMovement,
                        engagement.Fleet?.InstanceID
                    );
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetEngagementTarget,
                        engagement.TargetPlanet?.InstanceID
                    );
                    break;
                case AIFleetRoleProposal role:
                    Add(reservations, AISelectionReservationKind.FleetRole, role.Fleet?.InstanceID);
                    break;
                case AITransferUnitProposal transfer:
                    AddTransferReservations(reservations, transfer);
                    break;
            }

            return reservations;
        }

        /// <summary>
        /// Adds attack-proposal reservations.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="proposal">Attack proposal.</param>
        private static void AddAttackReservations(
            List<AISelectionReservation> reservations,
            AIFleetAttackProposal proposal
        )
        {
            if (proposal.Fleet == null)
                return;
            Add(reservations, AISelectionReservationKind.FleetOrder, proposal.Fleet.InstanceID);
            if (proposal.Status == FleetOrderStatus.Returning)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FleetMovement,
                    proposal.Fleet.InstanceID
                );
                return;
            }
            if (proposal.OrderType != FleetOrderType.Attack)
                return;
            Add(reservations, AISelectionReservationKind.FleetAttack, proposal.Fleet.InstanceID);
            Add(
                reservations,
                AISelectionReservationKind.FleetAttackTarget,
                proposal.TargetPlanet?.InstanceID
            );
            string currentPlanetId = proposal.Fleet.GetParentOfType<Planet>()?.InstanceID;
            if (currentPlanetId != proposal.TargetPlanet?.InstanceID)
                Add(
                    reservations,
                    AISelectionReservationKind.FleetMovement,
                    proposal.Fleet.InstanceID
                );
            if (
                proposal.TargetPlanet != null
                && currentPlanetId == proposal.TargetPlanet.InstanceID
            )
                Add(
                    reservations,
                    AISelectionReservationKind.PlanetAttack,
                    proposal.TargetPlanet.InstanceID
                );
        }

        /// <summary>
        /// Adds single-planet colonization reservations.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="proposal">Colonization proposal.</param>
        private static void AddColonizationReservations(
            List<AISelectionReservation> reservations,
            AIColonizationProposal proposal
        )
        {
            if (proposal.Fleet == null)
                return;
            Add(reservations, AISelectionReservationKind.FleetOrder, proposal.Fleet.InstanceID);
            Add(
                reservations,
                AISelectionReservationKind.FleetColonization,
                proposal.Fleet.InstanceID
            );
            if (proposal.Fleet.Order == null)
                Add(
                    reservations,
                    AISelectionReservationKind.NewColonizationOrder,
                    proposal.Fleet.GetOwnerInstanceID()
                );
            Add(
                reservations,
                AISelectionReservationKind.PlanetColonization,
                proposal.TargetPlanet?.InstanceID
            );
            if (
                proposal.Fleet.GetParentOfType<Planet>()?.InstanceID
                != proposal.TargetPlanet?.InstanceID
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FleetMovement,
                    proposal.Fleet.InstanceID
                );
        }

        /// <summary>
        /// Adds colonization-campaign reservations.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="proposal">Campaign proposal.</param>
        private static void AddCampaignReservations(
            List<AISelectionReservation> reservations,
            AIColonizationCampaignProposal proposal
        )
        {
            if (proposal.Fleet == null)
                return;
            Add(reservations, AISelectionReservationKind.FleetOrder, proposal.Fleet.InstanceID);
            Add(
                reservations,
                AISelectionReservationKind.FleetColonization,
                proposal.Fleet.InstanceID
            );
            Add(reservations, AISelectionReservationKind.SystemColonization, proposal.SystemId);
            if (proposal.Fleet.Order == null)
                Add(
                    reservations,
                    AISelectionReservationKind.NewColonizationOrder,
                    proposal.Fleet.GetOwnerInstanceID()
                );
            if (proposal.UnexploredPlanets.Count > 0)
                Add(
                    reservations,
                    AISelectionReservationKind.FleetMovement,
                    proposal.Fleet.InstanceID
                );
            Add(
                reservations,
                AISelectionReservationKind.PlanetColonization,
                proposal.ColonyTarget?.InstanceID
            );
        }

        /// <summary>
        /// Adds unit-transfer reservations, including reinforcement conflicts shared with production.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="proposal">Transfer proposal.</param>
        private static void AddTransferReservations(
            List<AISelectionReservation> reservations,
            AITransferUnitProposal proposal
        )
        {
            Add(reservations, AISelectionReservationKind.UnitTransfer, proposal.Unit?.InstanceID);
            Add(
                reservations,
                AISelectionReservationKind.ContainerTransferSource,
                proposal.SourceContainer?.InstanceID
            );
            Add(
                reservations,
                AISelectionReservationKind.ContainerTransferTarget,
                proposal.Destination?.InstanceID
            );
            if (proposal.SourceContainer is Fleet sourceFleet)
            {
                Add(
                    reservations,
                    proposal.Unit is CapitalShip
                    && sourceFleet.Order?.OrderType == FleetOrderType.Defend
                        ? AISelectionReservationKind.FleetOrderContinuation
                        : AISelectionReservationKind.FleetOrder,
                    sourceFleet.InstanceID
                );
                Add(reservations, AISelectionReservationKind.FleetMovement, sourceFleet.InstanceID);
            }
            if (proposal.Destination is not Fleet targetFleet)
                return;
            Add(
                reservations,
                AISelectionReservationKind.FleetTransferTarget,
                targetFleet.InstanceID
            );
            if (proposal.Unit is CapitalShip)
                Add(
                    reservations,
                    AISelectionReservationKind.FleetCapitalReinforcement,
                    targetFleet.InstanceID
                );
            else if (proposal.Unit is Regiment)
                Add(
                    reservations,
                    AISelectionReservationKind.FleetReinforcement,
                    targetFleet.InstanceID,
                    AIProductionDemandKind.FleetRegiment.ToString()
                );
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
