using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Stopwatch = global::System.Diagnostics.Stopwatch;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Sends replaceable snapshots without retries and assembles a bounded window of in-flight snapshots.
    /// </summary>
    internal sealed class MultiplayerSnapshotDatagrams
    {
        internal const int MaximumPayloadBytes = MultiplayerPacketCodec.MaximumTacticalFrameBytes;
        internal const int MaximumMetadataBytes =
            MultiplayerPacketCodec.MaximumTacticalMetadataBytes;
        internal const int AssemblyLifetimeMilliseconds = 1000;
        internal const int MaximumConcurrentAssemblies = 8;
        internal const int MaximumBufferedBytes = 8 * 1024 * 1024;
        internal const int DataFragmentsPerParity = 4;
        private const int _headerBytes = 22;
        private const uint _magic = 0x53543252;
        private const byte _version = 2;
        private const byte _dataFragment = 0;
        private const byte _parityFragment = 1;
        private const int _maximumPacketBytes = MaximumPayloadBytes + MaximumMetadataBytes + 4;
        private static readonly Encoding _utf8 = new UTF8Encoding(false, true);
        private readonly int _maximumDatagramBytes;
        private readonly int _fragmentBytes;
        private readonly Func<ArraySegment<byte>, bool> _trySend;
        private readonly Func<double> _clock;
        private int _sending;
        private ulong _nextSequence;
        private ulong _latestCompletedSequence;
        private ulong _retiredThrough;
        private readonly SortedDictionary<ulong, Assembly> _assemblies = new();
        internal int BufferedBytes { get; private set; }
        internal int BufferedAssemblies => _assemblies.Count;

        /// <summary>
        /// Captures the provider limit and a nonqueuing send attempt boundary.
        /// </summary>
        /// <param name="maximumDatagramBytes">The complete provider datagram byte limit.</param>
        /// <param name="trySend">Copies an accepted datagram, or rejects temporary backpressure.</param>
        /// <param name="clock">An optional monotonic millisecond clock for assembly expiration.</param>
        internal MultiplayerSnapshotDatagrams(
            int maximumDatagramBytes,
            Func<ArraySegment<byte>, bool> trySend,
            Func<double> clock = null
        )
        {
            if (maximumDatagramBytes <= _headerBytes || maximumDatagramBytes > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maximumDatagramBytes));
            _maximumDatagramBytes = maximumDatagramBytes;
            _fragmentBytes = maximumDatagramBytes - _headerBytes;
            if ((_maximumPacketBytes + _fragmentBytes - 1) / _fragmentBytes > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maximumDatagramBytes));
            _trySend = trySend ?? throw new ArgumentNullException(nameof(trySend));
            _clock = clock ?? MonotonicMilliseconds;
        }

        /// <summary>
        /// Attempts data and one XOR parity fragment per four-fragment group, dropping the remainder on backpressure.
        /// Concurrent attempts are replaceable and retain no pending work.
        /// </summary>
        /// <param name="packet">The complete replaceable tactical snapshot packet.</param>
        /// <returns>Whether the provider accepted every fragment.</returns>
        internal bool TrySend(MultiplayerPacket packet)
        {
            if (packet == null || packet.Type != MultiplayerMessageType.TacticalFrame)
                throw new ArgumentException(
                    "Only tactical snapshots use replaceable datagrams.",
                    nameof(packet)
                );
            if (
                packet.Payload.Length > MaximumPayloadBytes
                || _utf8.GetByteCount(packet.Metadata) > MaximumMetadataBytes
            )
                throw new InvalidDataException(
                    "The tactical snapshot packet exceeds its transport bound."
                );
            if (Interlocked.CompareExchange(ref _sending, 1, 0) != 0)
                return false;
            try
            {
                byte[] metadata = _utf8.GetBytes(packet.Metadata);
                byte[] bytes = new byte[4 + metadata.Length + packet.Payload.Length];
                WriteUInt32(bytes, 0, (uint)metadata.Length);
                Buffer.BlockCopy(metadata, 0, bytes, 4, metadata.Length);
                Buffer.BlockCopy(
                    packet.Payload,
                    0,
                    bytes,
                    4 + metadata.Length,
                    packet.Payload.Length
                );
                ulong sequence = checked(++_nextSequence);
                int fragments = (bytes.Length + _fragmentBytes - 1) / _fragmentBytes;
                byte[] datagram = new byte[_maximumDatagramBytes];
                byte[] parity = new byte[_maximumDatagramBytes];
                WriteUInt32(datagram, 0, _magic);
                datagram[4] = _version;
                WriteUInt64(datagram, 6, sequence);
                WriteUInt32(datagram, 14, (uint)bytes.Length);
                WriteUInt16(datagram, 20, (ushort)fragments);
                Buffer.BlockCopy(datagram, 0, parity, 0, _headerBytes);
                parity[5] = _parityFragment;
                for (int index = 0; index < fragments; index++)
                {
                    if (index % DataFragmentsPerParity == 0)
                        Array.Clear(parity, _headerBytes, _fragmentBytes);
                    int count = Math.Min(_fragmentBytes, bytes.Length - index * _fragmentBytes);
                    WriteUInt16(datagram, 18, (ushort)index);
                    Buffer.BlockCopy(bytes, index * _fragmentBytes, datagram, _headerBytes, count);
                    for (int item = 0; item < count; item++)
                        parity[_headerBytes + item] ^= datagram[_headerBytes + item];
                    if (!_trySend(new ArraySegment<byte>(datagram, 0, _headerBytes + count)))
                        return false;
                    if ((index + 1) % DataFragmentsPerParity == 0 || index + 1 == fragments)
                    {
                        int first = index - index % DataFragmentsPerParity;
                        int parityBytes = Math.Min(
                            _fragmentBytes,
                            bytes.Length - first * _fragmentBytes
                        );
                        WriteUInt16(parity, 18, (ushort)first);
                        if (
                            !_trySend(new ArraySegment<byte>(parity, 0, _headerBytes + parityBytes))
                        )
                            return false;
                    }
                }
                return true;
            }
            finally
            {
                Volatile.Write(ref _sending, 0);
            }
        }

        /// <summary>
        /// Receives a fragment while preserving bounded older assemblies across publication jitter.
        /// </summary>
        /// <param name="datagram">One complete admitted provider datagram.</param>
        /// <returns>The newest complete packet, or null while incomplete, duplicate, expired, or stale.</returns>
        internal MultiplayerPacket Receive(ArraySegment<byte> datagram)
        {
            Expire();
            if (
                datagram.Array == null
                || datagram.Count <= _headerBytes
                || datagram.Count > _maximumDatagramBytes
            )
                throw new InvalidDataException("The tactical snapshot datagram length is invalid.");
            byte[] data = datagram.Array;
            int start = datagram.Offset;
            ulong sequence = ReadUInt64(data, start + 6);
            uint length = ReadUInt32(data, start + 14);
            int index = ReadUInt16(data, start + 18);
            int fragments = ReadUInt16(data, start + 20);
            byte kind = data[start + 5];
            if (
                ReadUInt32(data, start) != _magic
                || data[start + 4] != _version
                || kind is not (_dataFragment or _parityFragment)
                || sequence == 0
                || length < 4
                || length > _maximumPacketBytes
                || fragments != (length + _fragmentBytes - 1) / _fragmentBytes
                || index >= fragments
                || kind == _parityFragment && index % DataFragmentsPerParity != 0
                || datagram.Count - _headerBytes
                    != Math.Min(_fragmentBytes, (int)length - index * _fragmentBytes)
            )
                throw new InvalidDataException(
                    "The tactical snapshot fragment geometry is invalid."
                );
            if (sequence <= _latestCompletedSequence)
                return null;
            if (!_assemblies.TryGetValue(sequence, out Assembly assembly))
            {
                if (sequence <= _retiredThrough)
                    return null;
                int groups = (fragments + DataFragmentsPerParity - 1) / DataFragmentsPerParity;
                int requiredBytes = checked(
                    (int)length + fragments + groups * _fragmentBytes + groups
                );
                while (
                    _assemblies.Count >= MaximumConcurrentAssemblies
                    || requiredBytes > MaximumBufferedBytes - BufferedBytes
                )
                {
                    ulong oldest = 0;
                    foreach (ulong candidate in _assemblies.Keys)
                    {
                        oldest = candidate;
                        break;
                    }
                    if (sequence < oldest)
                        return null;
                    RemoveAssembly(oldest);
                }
                assembly = new Assembly(checked((int)length), fragments, _fragmentBytes, _clock());
                _assemblies.Add(sequence, assembly);
                BufferedBytes += requiredBytes;
            }
            if (assembly.Bytes.Length != length || assembly.Received.Length != fragments)
                throw new InvalidDataException(
                    "The tactical snapshot fragments changed their bounds."
                );
            int count = datagram.Count - _headerBytes;
            int group = index / DataFragmentsPerParity;
            int offset = kind == _dataFragment ? index * _fragmentBytes : group * _fragmentBytes;
            byte[] destination = kind == _dataFragment ? assembly.Bytes : assembly.Parity;
            bool received =
                kind == _dataFragment ? assembly.Received[index] : assembly.ParityReceived[group];
            if (received)
            {
                for (int item = 0; item < count; item++)
                    if (destination[offset + item] != data[start + _headerBytes + item])
                        throw new InvalidDataException(
                            "A duplicate tactical fragment changed its bytes."
                        );
                return null;
            }
            Buffer.BlockCopy(data, start + _headerBytes, destination, offset, count);
            if (kind == _dataFragment)
            {
                assembly.Received[index] = true;
                assembly.ReceivedCount++;
            }
            else
                assembly.ParityReceived[group] = true;
            RecoverGroup(assembly, group);
            if (assembly.ReceivedCount != fragments)
                return null;
            byte[] complete = assembly.Bytes;
            _latestCompletedSequence = sequence;
            List<ulong> superseded = new();
            foreach (ulong candidate in _assemblies.Keys)
                if (candidate <= sequence)
                    superseded.Add(candidate);
            foreach (ulong candidate in superseded)
                RemoveAssembly(candidate);
            uint metadataLength = ReadUInt32(complete, 0);
            if (
                metadataLength > MaximumMetadataBytes
                || metadataLength > complete.Length - 4
                || complete.Length - 4 - metadataLength > MaximumPayloadBytes
            )
                throw new InvalidDataException("The tactical snapshot packet bounds are invalid.");
            string metadataText = _utf8.GetString(complete, 4, (int)metadataLength);
            byte[] payload = new byte[complete.Length - 4 - metadataLength];
            Buffer.BlockCopy(complete, 4 + (int)metadataLength, payload, 0, payload.Length);
            return new MultiplayerPacket(
                MultiplayerMessageType.TacticalFrame,
                metadataText,
                payload
            );
        }

        /// <summary>
        /// Releases incomplete work after its bounded assembly lifetime.
        /// </summary>
        internal void Expire()
        {
            double now = _clock();
            List<ulong> expired = null;
            foreach (var candidate in _assemblies)
                if (now - candidate.Value.Started >= AssemblyLifetimeMilliseconds)
                    (expired ??= new()).Add(candidate.Key);
            if (expired != null)
                foreach (ulong sequence in expired)
                    RemoveAssembly(sequence);
        }

        /// <summary>
        /// Releases bounded incomplete storage while retaining stale-sequence rejection.
        /// </summary>
        internal void ClearAssembly()
        {
            foreach (ulong sequence in _assemblies.Keys)
                _retiredThrough = Math.Max(_retiredThrough, sequence);
            _assemblies.Clear();
            BufferedBytes = 0;
        }

        /// <summary>
        /// Releases one incomplete or superseded snapshot without admitting its late fragments again.
        /// </summary>
        /// <param name="sequence">The retained snapshot identity.</param>
        private void RemoveAssembly(ulong sequence)
        {
            Assembly assembly = _assemblies[sequence];
            BufferedBytes -= assembly.MemoryBytes;
            _assemblies.Remove(sequence);
            _retiredThrough = Math.Max(_retiredThrough, sequence);
        }

        /// <summary>
        /// Retains one bounded in-flight snapshot independently of neighboring publication times.
        /// </summary>
        private sealed class Assembly
        {
            internal readonly byte[] Bytes;
            internal readonly bool[] Received;
            internal readonly byte[] Parity;
            internal readonly bool[] ParityReceived;
            internal readonly double Started;
            internal int ReceivedCount;
            internal int MemoryBytes =>
                Bytes.Length + Received.Length + Parity.Length + ParityReceived.Length;

            /// <summary>
            /// Allocates the exact validated fragment geometry.
            /// </summary>
            /// <param name="length">The complete snapshot byte count.</param>
            /// <param name="fragments">The number of provider datagrams.</param>
            /// <param name="fragmentBytes">The payload byte capacity of one provider datagram.</param>
            /// <param name="started">The monotonic admission time.</param>
            internal Assembly(int length, int fragments, int fragmentBytes, double started)
            {
                Bytes = new byte[length];
                Received = new bool[fragments];
                int groups = (fragments + DataFragmentsPerParity - 1) / DataFragmentsPerParity;
                Parity = new byte[checked(groups * fragmentBytes)];
                ParityReceived = new bool[groups];
                Started = started;
            }
        }

        /// <summary>
        /// Recovers one erased data fragment and checks a complete group's parity consistency.
        /// </summary>
        /// <param name="assembly">The bounded snapshot assembly.</param>
        /// <param name="group">The addressed four-data-fragment parity group.</param>
        private void RecoverGroup(Assembly assembly, int group)
        {
            if (!assembly.ParityReceived[group])
                return;
            int first = group * DataFragmentsPerParity;
            int end = Math.Min(first + DataFragmentsPerParity, assembly.Received.Length);
            int missing = -1;
            for (int index = first; index < end; index++)
                if (!assembly.Received[index])
                {
                    if (missing != -1)
                        return;
                    missing = index;
                }
            int parityOffset = group * _fragmentBytes;
            if (missing != -1)
            {
                int offset = missing * _fragmentBytes;
                int count = Math.Min(_fragmentBytes, assembly.Bytes.Length - offset);
                Buffer.BlockCopy(assembly.Parity, parityOffset, assembly.Bytes, offset, count);
                for (int index = first; index < end; index++)
                {
                    if (index == missing)
                        continue;
                    int knownOffset = index * _fragmentBytes;
                    int knownCount = Math.Min(count, assembly.Bytes.Length - knownOffset);
                    for (int item = 0; item < knownCount; item++)
                        assembly.Bytes[offset + item] ^= assembly.Bytes[knownOffset + item];
                }
                assembly.Received[missing] = true;
                assembly.ReceivedCount++;
            }
            int parityCount = Math.Min(
                _fragmentBytes,
                assembly.Bytes.Length - first * _fragmentBytes
            );
            for (int item = 0; item < parityCount; item++)
            {
                byte value = assembly.Parity[parityOffset + item];
                for (int index = first; index < end; index++)
                {
                    int offset = index * _fragmentBytes + item;
                    if (offset < assembly.Bytes.Length)
                        value ^= assembly.Bytes[offset];
                }
                if (value != 0)
                    throw new InvalidDataException(
                        "The tactical parity fragment conflicts with its data group."
                    );
            }
        }

        /// <summary>
        /// Reads the process monotonic clock in milliseconds.
        /// </summary>
        /// <returns>The monotonic millisecond timestamp.</returns>
        private static double MonotonicMilliseconds() =>
            Stopwatch.GetTimestamp() * (1000d / Stopwatch.Frequency);

        /// <summary>
        /// Writes an unsigned 16-bit network-order value.
        /// </summary>
        /// <param name="bytes">The destination bytes.</param>
        /// <param name="offset">The destination offset.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)(value >> 8);
            bytes[offset + 1] = (byte)value;
        }

        /// <summary>
        /// Writes an unsigned 32-bit network-order value.
        /// </summary>
        /// <param name="bytes">The destination bytes.</param>
        /// <param name="offset">The destination offset.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            for (int index = 3; index >= 0; index--)
            {
                bytes[offset + index] = (byte)value;
                value >>= 8;
            }
        }

        /// <summary>
        /// Writes an unsigned 64-bit network-order value.
        /// </summary>
        /// <param name="bytes">The destination bytes.</param>
        /// <param name="offset">The destination offset.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            for (int index = 7; index >= 0; index--)
            {
                bytes[offset + index] = (byte)value;
                value >>= 8;
            }
        }

        /// <summary>
        /// Reads an unsigned 16-bit network-order value.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The decoded value.</returns>
        private static ushort ReadUInt16(byte[] bytes, int offset) =>
            (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

        /// <summary>
        /// Reads an unsigned 32-bit network-order value.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The decoded value.</returns>
        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            uint value = 0;
            for (int index = 0; index < 4; index++)
                value = (value << 8) | bytes[offset + index];
            return value;
        }

        /// <summary>
        /// Reads an unsigned 64-bit network-order value.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The decoded value.</returns>
        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (int index = 0; index < 8; index++)
                value = (value << 8) | bytes[offset + index];
            return value;
        }
    }
}
