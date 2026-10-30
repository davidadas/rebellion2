using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Adapts ordered message fragments to the existing bounded packet codec.
    /// </summary>
    public sealed class MultiplayerPacketStream : Stream
    {
        private readonly Queue<byte[]> _received = new();
        private readonly object _gate = new();
        private readonly Func<ArraySegment<byte>, CancellationToken, Task> _send;
        private readonly int _maximumBufferedBytes;
        private TaskCompletionSource<bool> _waiting;
        private int _offset;
        private int _bufferedBytes;
        private bool _completed;

        /// <summary>
        /// Gets whether the stream supports reads.
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Gets whether the stream supports seeking.
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Gets whether the stream supports writes.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Rejects querying a network stream length.
        /// </summary>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Rejects querying or changing a network stream position.
        /// </summary>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Creates an ordered fragment stream with a bounded receive queue.
        /// </summary>
        /// <param name="send">Sends one ordered byte range.</param>
        /// <param name="maximumBufferedBytes">The maximum unread byte count.</param>
        public MultiplayerPacketStream(
            Func<ArraySegment<byte>, CancellationToken, Task> send,
            int maximumBufferedBytes
        )
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            if (maximumBufferedBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBufferedBytes));
            _maximumBufferedBytes = maximumBufferedBytes;
        }

        /// <summary>
        /// Copies the next received fragment before its provider reuses the buffer.
        /// </summary>
        /// <param name="data">The received ordered bytes.</param>
        public void Append(ArraySegment<byte> data)
        {
            if (data.Count == 0)
                return;
            TaskCompletionSource<bool> waiting;
            lock (_gate)
            {
                if (_completed)
                    throw new ObjectDisposedException(nameof(MultiplayerPacketStream));
                if (data.Count > _maximumBufferedBytes - _bufferedBytes)
                    throw new InvalidDataException(
                        "The multiplayer receive buffer limit was exceeded."
                    );
                byte[] copy = new byte[data.Count];
                Buffer.BlockCopy(data.Array, data.Offset, copy, 0, copy.Length);
                _received.Enqueue(copy);
                _bufferedBytes += copy.Length;
                waiting = _waiting;
                _waiting = null;
            }
            waiting?.TrySetResult(true);
        }

        /// <summary>
        /// Ends the incoming stream after previously received bytes have been read.
        /// </summary>
        public void Complete()
        {
            TaskCompletionSource<bool> waiting;
            lock (_gate)
            {
                _completed = true;
                waiting = _waiting;
                _waiting = null;
            }
            waiting?.TrySetResult(true);
        }

        /// <summary>
        /// Reads bytes across message boundaries or waits for the next fragment.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The destination offset.</param>
        /// <param name="count">The maximum byte count.</param>
        /// <param name="cancellationToken">Cancels the pending read.</param>
        /// <returns>The number of bytes read, or zero after completion.</returns>
        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TaskCompletionSource<bool> waiting;
                lock (_gate)
                {
                    if (_received.Count > 0)
                    {
                        byte[] chunk = _received.Peek();
                        int length = Math.Min(count, chunk.Length - _offset);
                        Buffer.BlockCopy(chunk, _offset, buffer, offset, length);
                        _offset += length;
                        _bufferedBytes -= length;
                        if (_offset == chunk.Length)
                        {
                            _received.Dequeue();
                            _offset = 0;
                        }
                        return length;
                    }
                    if (_completed)
                        return 0;
                    waiting = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );
                    _waiting = waiting;
                }
                using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                    waiting.TrySetCanceled()
                );
                await waiting.Task;
            }
        }

        /// <summary>
        /// Sends a byte range through the ordered fragment provider.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">The source offset.</param>
        /// <param name="count">The byte count.</param>
        /// <param name="cancellationToken">Cancels the pending send.</param>
        /// <returns>The pending send.</returns>
        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            if (_completed)
                throw new ObjectDisposedException(nameof(MultiplayerPacketStream));
            return _send(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
        }

        /// <summary>
        /// Releases the queue and wakes any pending reader.
        /// </summary>
        /// <param name="disposing">Whether managed resources are being released.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Complete();
                lock (_gate)
                {
                    _received.Clear();
                    _bufferedBytes = 0;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Completes immediately because sends are accepted individually.
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// Completes immediately after the preceding ordered sends.
        /// </summary>
        /// <param name="cancellationToken">Cancels the flush.</param>
        /// <returns>The completed flush.</returns>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Rejects synchronous reads; the packet codec uses asynchronous reads.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The destination offset.</param>
        /// <param name="count">The requested count.</param>
        /// <returns>The read byte count.</returns>
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        /// <summary>
        /// Rejects seeking on an ordered network stream.
        /// </summary>
        /// <param name="offset">The requested offset.</param>
        /// <param name="origin">The seek origin.</param>
        /// <returns>The resulting position.</returns>
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        /// <summary>
        /// Rejects resizing an ordered network stream.
        /// </summary>
        /// <param name="value">The requested length.</param>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Rejects synchronous writes; the packet codec uses asynchronous writes.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">The source offset.</param>
        /// <param name="count">The requested count.</param>
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
