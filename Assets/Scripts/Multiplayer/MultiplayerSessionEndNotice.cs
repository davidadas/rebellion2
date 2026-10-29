using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Retains a terminal multiplayer notice until the player acknowledges it, including across scene cleanup.
    /// </summary>
    public sealed class MultiplayerSessionEndNotice
    {
        public const string Message = "Your opponent has left the game.";

        public bool IsPending { get; private set; }

        public event Action Changed;

        /// <summary>
        /// Publishes one acknowledgment request for an ended opponent connection.
        /// </summary>
        public void ShowOpponentDeparture()
        {
            if (IsPending)
                return;
            IsPending = true;
            Changed?.Invoke();
        }

        /// <summary>
        /// Clears the pending notice after its owning presentation accepts acknowledgment.
        /// </summary>
        public void Acknowledge()
        {
            if (!IsPending)
                return;
            IsPending = false;
            Changed?.Invoke();
        }
    }
}
