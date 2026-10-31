using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Admits the initial world and its loaded-state receipt only within the negotiated initialization.
    /// </summary>
    internal sealed class MultiplayerSnapshotAdmission
    {
        public string InitializationID { get; private set; }

        /// <summary>
        /// Adopts an initialization only when its identity changes.
        /// </summary>
        /// <param name="initializationID">The agreed initialization identity.</param>
        /// <returns>Whether a different initialization was adopted.</returns>
        public bool Begin(string initializationID)
        {
            if (!Guid.TryParseExact(initializationID, "N", out _))
                throw new ArgumentException(
                    "An initialization identity is required.",
                    nameof(initializationID)
                );
            if (InitializationID == initializationID)
                return false;
            InitializationID = initializationID;
            return true;
        }

        /// <summary>
        /// Tests whether an initial world belongs to the currently agreed initialization.
        /// </summary>
        /// <param name="initializationID">The sender's initialization identity.</param>
        /// <returns>Whether the snapshot belongs to the current initialization.</returns>
        public bool Accepts(string initializationID) =>
            MultiplayerSessionIdentity.Matches(InitializationID, initializationID);

        /// <summary>
        /// Confirms that the counterpart loaded the exact initial snapshot supplied in this initialization.
        /// </summary>
        /// <param name="receipt">The independently verified snapshot receipt.</param>
        /// <param name="expectedHash">The hash of the supplied world payload.</param>
        /// <param name="expectedFingerprint">The expected state after ordinary loading.</param>
        /// <returns>Whether the initialization, payload, and loaded state match.</returns>
        public bool AcceptsReceipt(
            MultiplayerSnapshotMessage receipt,
            string expectedHash,
            string expectedFingerprint
        ) =>
            receipt != null
            && MultiplayerSessionIdentity.Matches(InitializationID, receipt.initializationID)
            && receipt.hash == expectedHash
            && receipt.simulationFingerprint == expectedFingerprint;

        /// <summary>
        /// Discards initialization ownership when the connection is released.
        /// </summary>
        public void Clear()
        {
            InitializationID = null;
        }
    }
}
