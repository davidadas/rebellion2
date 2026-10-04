using System;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Identifies one typed resource that may be reserved by a selected AI proposal.
    /// </summary>
    internal readonly struct AISelectionReservation : IEquatable<AISelectionReservation>
    {
        internal AISelectionReservationKind Kind { get; }

        internal string PrimaryId { get; }

        internal string SecondaryId { get; }

        /// <summary>
        /// Creates a typed selection reservation.
        /// </summary>
        /// <param name="kind">Reserved resource category.</param>
        /// <param name="primaryId">Primary resource identity.</param>
        /// <param name="secondaryId">Optional secondary identity.</param>
        internal AISelectionReservation(
            AISelectionReservationKind kind,
            string primaryId,
            string secondaryId = null
        )
        {
            Kind = kind;
            PrimaryId = primaryId;
            SecondaryId = secondaryId;
        }

        /// <summary>
        /// Returns whether two reservations identify the same resource.
        /// </summary>
        /// <param name="other">Reservation to compare.</param>
        /// <returns>True when both reservations identify the same resource.</returns>
        public bool Equals(AISelectionReservation other)
        {
            return Kind == other.Kind
                && string.Equals(PrimaryId, other.PrimaryId, StringComparison.Ordinal)
                && string.Equals(SecondaryId, other.SecondaryId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether an object is the same reservation.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True when the object is an equal reservation.</returns>
        public override bool Equals(object obj)
        {
            return obj is AISelectionReservation other && Equals(other);
        }

        /// <summary>
        /// Returns the reservation hash code.
        /// </summary>
        /// <returns>The reservation hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, PrimaryId, SecondaryId);
        }
    }

    /// <summary>
    /// Defines resources whose use may conflict during AI proposal selection.
    /// </summary>
    internal enum AISelectionReservationKind
    {
        FleetOrder,
        FleetMovement,
        FleetAttack,
        FleetAttackTarget,
        FleetRole,
        FleetColonization,
        FleetEngagementTarget,
        FleetTransferTarget,
        FleetReinforcement,
        FleetCapitalReinforcement,
        FleetCreation,
        PlanetAttack,
        PlanetDefense,
        PlanetColonization,
        NewColonizationOrder,
        SystemColonization,
        UnitTransfer,
        ContainerTransferSource,
        ContainerTransferTarget,
        MissionActor,
        Mission,
        MissionRecruitment,
        MissionResearch,
        MissionOfficer,
        MissionTarget,
        MissionAtPlanet,
        ProductionDemand,
        FacilityAllocation,
        ProductionBuildingReplacement,
        ManufacturingLane,
    }
}
