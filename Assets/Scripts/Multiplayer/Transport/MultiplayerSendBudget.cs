using System;
using System.IO;
using System.Threading;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Accounts for reliable packet ownership before asynchronous transport work can retain it.
    /// </summary>
    internal sealed class MultiplayerSendBudget
    {
        internal const int MaximumPackets = 256;
        internal const int MaximumTacticalPackets = 64;
        internal const int MaximumTacticalBytes = 8 * 1024 * 1024;
        private readonly object _gate = new();
        private readonly int _maximumPackets;
        private readonly long _maximumBytes;
        private readonly int _maximumTacticalPackets;
        private readonly long _maximumTacticalBytes;
        private int _packets;
        private long _bytes;
        private int _tacticalPackets;
        private long _tacticalBytes;

        internal int RetainedPackets
        {
            get
            {
                lock (_gate)
                    return _packets;
            }
        }
        internal long RetainedBytes
        {
            get
            {
                lock (_gate)
                    return _bytes;
            }
        }

        /// <summary>
        /// Captures reliable retention limits while admitting a maximum legal bulk packet by default.
        /// </summary>
        /// <param name="maximumPackets">The total reliable packet count limit.</param>
        /// <param name="maximumBytes">The total retained wire byte limit, or the bulk maximum plus tactical allowance.</param>
        /// <param name="maximumTacticalPackets">The reliable tactical packet count limit.</param>
        /// <param name="maximumTacticalBytes">The reliable tactical retained byte limit.</param>
        internal MultiplayerSendBudget(
            int maximumPackets = MaximumPackets,
            long? maximumBytes = null,
            int maximumTacticalPackets = MaximumTacticalPackets,
            long maximumTacticalBytes = MaximumTacticalBytes
        )
        {
            _maximumPackets = maximumPackets;
            _maximumBytes =
                maximumBytes
                ?? (long)MultiplayerPacketCodec.MaximumPacketLength + MaximumTacticalBytes;
            _maximumTacticalPackets = maximumTacticalPackets;
            _maximumTacticalBytes = maximumTacticalBytes;
            if (
                _maximumPackets <= 0
                || _maximumBytes <= 0
                || _maximumTacticalPackets <= 0
                || _maximumTacticalBytes <= 0
            )
                throw new ArgumentOutOfRangeException(nameof(maximumPackets));
        }

        /// <summary>
        /// Reserves both packet and byte ownership without awaiting or retaining overflow work.
        /// </summary>
        /// <param name="encodedBytes">The validated complete wire packet size.</param>
        /// <param name="tactical">Whether the packet also consumes the tighter tactical allowance.</param>
        /// <returns>The independently idempotent ownership token.</returns>
        internal Reservation Reserve(int encodedBytes, bool tactical)
        {
            if (encodedBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(encodedBytes));
            lock (_gate)
            {
                if (
                    _packets >= _maximumPackets
                    || encodedBytes > _maximumBytes - _bytes
                    || tactical
                        && (
                            _tacticalPackets >= _maximumTacticalPackets
                            || encodedBytes > _maximumTacticalBytes - _tacticalBytes
                        )
                )
                    throw new InvalidDataException(
                        "The reliable multiplayer send queue exceeded its retention limit."
                    );
                _packets++;
                _bytes += encodedBytes;
                if (tactical)
                {
                    _tacticalPackets++;
                    _tacticalBytes += encodedBytes;
                }
                return new Reservation(this, encodedBytes, tactical);
            }
        }

        /// <summary>
        /// Releases the packet ownership belonging to one completed or cancelled reservation.
        /// </summary>
        /// <param name="encodedBytes">The reserved complete wire size.</param>
        /// <param name="tactical">Whether the reservation also consumed tactical allowance.</param>
        private void Release(int encodedBytes, bool tactical)
        {
            lock (_gate)
            {
                _packets--;
                _bytes -= encodedBytes;
                if (tactical)
                {
                    _tacticalPackets--;
                    _tacticalBytes -= encodedBytes;
                }
            }
        }

        /// <summary>
        /// Owns one reliable packet's retention until its transport operation ends.
        /// </summary>
        internal sealed class Reservation : IDisposable
        {
            private MultiplayerSendBudget _owner;
            private readonly int _encodedBytes;
            private readonly bool _tactical;

            /// <summary>
            /// Captures one already admitted reliable packet reservation.
            /// </summary>
            /// <param name="owner">The retention budget that admitted it.</param>
            /// <param name="encodedBytes">The complete reserved wire size.</param>
            /// <param name="tactical">Whether tactical allowance was reserved.</param>
            internal Reservation(MultiplayerSendBudget owner, int encodedBytes, bool tactical)
            {
                _owner = owner;
                _encodedBytes = encodedBytes;
                _tactical = tactical;
            }

            /// <summary>
            /// Releases this ownership once, including repeated cancellation or cleanup calls.
            /// </summary>
            public void Dispose() =>
                Interlocked.Exchange(ref _owner, null)?.Release(_encodedBytes, _tactical);
        }
    }
}
