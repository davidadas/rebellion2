using System;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Completes durable host-save publication before exposing its current metadata to a peer.
    /// </summary>
    internal static class MultiplayerSaveCheckpointPublication
    {
        /// <summary>
        /// Waits for durable storage and connection ownership before serializing the published metadata.
        /// </summary>
        /// <param name="command">The reserved save operation.</param>
        /// <param name="metadata">The host metadata updated by durable publication.</param>
        /// <param name="publication">The pending host storage operation.</param>
        /// <param name="retainsOwnership">Reports whether the same runtime and protocol still own the operation.</param>
        /// <param name="cancellationToken">Cancels publication when the connection ends.</param>
        /// <returns>The checkpoint containing metadata from the completed host publication.</returns>
        internal static async Task<MultiplayerSaveCheckpoint> CompleteHostSaveAsync(
            MultiplayerSaveRequest command,
            GameMetadata metadata,
            Task<bool> publication,
            Func<bool> retainsOwnership,
            CancellationToken cancellationToken
        )
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            if (publication == null)
                throw new ArgumentNullException(nameof(publication));
            if (retainsOwnership == null)
                throw new ArgumentNullException(nameof(retainsOwnership));

            if (!await publication)
                throw new InvalidOperationException(
                    command.automatic
                        ? "The scheduled automatic checkpoint was not written."
                        : "The host checkpoint was not at a writable boundary."
                );
            cancellationToken.ThrowIfCancellationRequested();
            if (!retainsOwnership())
                throw new OperationCanceledException(
                    "The outgoing connection no longer owns this checkpoint."
                );

            return new MultiplayerSaveCheckpoint
            {
                Command = command,
                Payload = MultiplayerSnapshotCodec.Serialize(typeof(GameMetadata), metadata),
            };
        }
    }
}
