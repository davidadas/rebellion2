using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Admits the initial world and its loaded-state receipt only within the negotiated initialization.
    /// </summary>
    internal sealed class MultiplayerSnapshotAdmission
    {
        public string InitializationID { get; private set; }
        public string PreparedSnapshotHash { get; private set; }

        /// <summary>
        /// Adopts an initialization only when its identity changes.
        /// </summary>
        /// <param name="initializationID">The agreed initialization identity.</param>
        /// <param name="preparedSnapshotHash">The world already verified by both load preflights, or null for a new world.</param>
        /// <returns>Whether a different initialization was adopted.</returns>
        public bool Begin(string initializationID, string preparedSnapshotHash = null)
        {
            if (!Guid.TryParseExact(initializationID, "N", out _))
                throw new ArgumentException(
                    "An initialization identity is required.",
                    nameof(initializationID)
                );
            if (preparedSnapshotHash != null && string.IsNullOrWhiteSpace(preparedSnapshotHash))
                throw new ArgumentException(
                    "A prepared snapshot hash cannot be empty.",
                    nameof(preparedSnapshotHash)
                );
            if (InitializationID == initializationID)
            {
                if (PreparedSnapshotHash != preparedSnapshotHash)
                    throw new InvalidOperationException(
                        "The prepared world cannot change within an initialization."
                    );
                return false;
            }
            InitializationID = initializationID;
            PreparedSnapshotHash = preparedSnapshotHash;
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
        /// Admits a payload-free commit only for the world verified before this scene transfer.
        /// </summary>
        /// <param name="snapshot">The host's current initialization and prepared-world identity.</param>
        /// <returns>Whether the loaded world can be verified without receiving its bytes again.</returns>
        public bool AcceptsPreparedSnapshot(MultiplayerSnapshotMessage snapshot) =>
            snapshot != null
            && PreparedSnapshotHash != null
            && Accepts(snapshot.initializationID)
            && snapshot.hash == PreparedSnapshotHash;

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
            PreparedSnapshotHash = null;
        }
    }
}
