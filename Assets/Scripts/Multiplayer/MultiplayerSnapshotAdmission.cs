using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Scopes snapshot revision ordering to the initialization that issued those revisions.
    /// </summary>
    internal sealed class MultiplayerSnapshotAdmission
    {
        public string InitializationID { get; private set; }
        public long Revision { get; private set; }

        /// <summary>
        /// Adopts an initialization and resets revision history only when its identity changes.
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
            Revision = 0;
            return true;
        }

        /// <summary>
        /// Tests a received revision against only the currently agreed initialization.
        /// </summary>
        /// <param name="initializationID">The sender's initialization identity.</param>
        /// <param name="revision">The revision issued within that initialization.</param>
        /// <returns>Whether the snapshot belongs to the current initialization and is not older.</returns>
        public bool Accepts(string initializationID, long revision) =>
            MultiplayerSessionIdentity.Matches(InitializationID, initializationID)
            && revision >= Revision;

        /// <summary>
        /// Confirms that the counterpart loaded the exact initial snapshot supplied in this initialization.
        /// </summary>
        /// <param name="receipt">The independently verified snapshot receipt.</param>
        /// <param name="expectedHash">The hash of the supplied world payload.</param>
        /// <param name="expectedFingerprint">The expected state after ordinary loading.</param>
        /// <returns>Whether every identity, revision, and state check matches.</returns>
        public bool AcceptsReceipt(
            MultiplayerSnapshotMessage receipt,
            string expectedHash,
            string expectedFingerprint
        ) =>
            receipt != null
            && MultiplayerSessionIdentity.Matches(InitializationID, receipt.initializationID)
            && receipt.revision == Revision
            && receipt.hash == expectedHash
            && receipt.simulationFingerprint == expectedFingerprint;

        /// <summary>
        /// Issues the next source revision within the current initialization.
        /// </summary>
        /// <returns>The next snapshot revision.</returns>
        public long NextRevision() => ++Revision;

        /// <summary>
        /// Records the revision of an independently verified and applied snapshot.
        /// </summary>
        /// <param name="revision">The accepted snapshot revision.</param>
        public void RecordRevision(long revision) => Revision = revision;

        /// <summary>
        /// Discards initialization ownership when the connection is released.
        /// </summary>
        public void Clear()
        {
            InitializationID = null;
            Revision = 0;
        }
    }
}
