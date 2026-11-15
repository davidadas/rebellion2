using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Bounds reliable asynchronous sends while keeping replaceable snapshots outside their writer gate.
    /// </summary>
    internal sealed class MultiplayerSendScheduler
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly MultiplayerSendBudget _budget;
        private readonly Func<MultiplayerPacket, CancellationToken, Task> _writeReliable;
        private readonly Func<MultiplayerPacket, bool> _trySnapshot;

        /// <summary>
        /// Captures reliable I/O, immediate snapshot attempts, and their retention owner.
        /// </summary>
        /// <param name="writeReliable">Writes one reliable packet after the writer gate is acquired.</param>
        /// <param name="trySnapshot">Immediately accepts or drops one replaceable snapshot attempt.</param>
        /// <param name="budget">The optional provider-specific reliable retention budget.</param>
        internal MultiplayerSendScheduler(
            Func<MultiplayerPacket, CancellationToken, Task> writeReliable,
            Func<MultiplayerPacket, bool> trySnapshot,
            MultiplayerSendBudget budget = null
        )
        {
            _writeReliable =
                writeReliable ?? throw new ArgumentNullException(nameof(writeReliable));
            _trySnapshot = trySnapshot ?? throw new ArgumentNullException(nameof(trySnapshot));
            _budget = budget ?? new MultiplayerSendBudget();
        }

        /// <summary>
        /// Attempts snapshots immediately or reserves reliable ownership before any asynchronous wait.
        /// </summary>
        /// <param name="packet">The complete typed outgoing packet.</param>
        /// <param name="cancellationToken">Cancels queued or active reliable I/O.</param>
        /// <returns>The reliable send lifetime, or an already completed replaceable attempt.</returns>
        internal Task SendAsync(MultiplayerPacket packet, CancellationToken cancellationToken)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            cancellationToken.ThrowIfCancellationRequested();
            int encodedBytes = MultiplayerPacketCodec.GetEncodedLength(packet);
            if (packet.Type == MultiplayerMessageType.TacticalFrame)
            {
                _trySnapshot(packet);
                return Task.CompletedTask;
            }
            bool tactical =
                packet.Type
                is >= MultiplayerMessageType.TacticalReady
                    and <= MultiplayerMessageType.TacticalResync;
            MultiplayerSendBudget.Reservation reservation = _budget.Reserve(encodedBytes, tactical);
            return SendReliableAsync(packet, reservation, cancellationToken);
        }

        /// <summary>
        /// Releases retained ownership across queued cancellation, I/O failure, and ordinary completion.
        /// </summary>
        /// <param name="packet">The admitted reliable packet.</param>
        /// <param name="reservation">The packet's already reserved retention ownership.</param>
        /// <param name="cancellationToken">Cancels its gate wait and provider write.</param>
        /// <returns>The bounded reliable operation.</returns>
        private async Task SendReliableAsync(
            MultiplayerPacket packet,
            MultiplayerSendBudget.Reservation reservation,
            CancellationToken cancellationToken
        )
        {
            bool acquired = false;
            try
            {
                await _sendGate.WaitAsync(cancellationToken);
                acquired = true;
                await _writeReliable(packet, cancellationToken);
            }
            finally
            {
                try
                {
                    if (acquired)
                        _sendGate.Release();
                }
                finally
                {
                    reservation.Dispose();
                }
            }
        }
    }
}
