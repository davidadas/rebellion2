using System.Collections.Generic;
using System.Linq;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Tracks typed cross-domain reservations during one selection pass.
    /// </summary>
    internal sealed class AISelectionState
    {
        private readonly HashSet<AISelectionReservation> _reservations = new();

        /// <summary>
        /// Returns whether all requested resources remain available.
        /// </summary>
        /// <param name="reservations">Resources requested by one proposal.</param>
        /// <returns>True when none has already been reserved.</returns>
        internal bool CanReserve(IEnumerable<AISelectionReservation> reservations)
        {
            return reservations?.Any(_reservations.Contains) != true;
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
    }
}
