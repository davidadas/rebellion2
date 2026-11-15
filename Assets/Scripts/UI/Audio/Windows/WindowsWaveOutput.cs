using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Sends Unity's existing stereo mix to a private PCM output when its normal device cannot open.
/// </summary>
internal sealed class WindowsWaveOutput : IDisposable
{
    private const int _completionTimeoutMilliseconds = 5000;
    private readonly object sync = new object();
    private readonly IWindowsWaveApi api;
    private readonly AutoResetEvent completion = new AutoResetEvent(false);
    private readonly PcmBuffer[] buffers;
    private readonly int sampleCount;
    private IntPtr device;
    private bool eventRetained;
    private bool disposed;
    private int nextBuffer;
    private string failure;

    public string Failure => Volatile.Read(ref failure);

    /// <summary>
    /// Opens an explicitly formatted stereo stream using the engine's existing sample rate and buffers.
    /// </summary>
    /// <param name="api">The native output operations.</param>
    /// <param name="sampleRate">The current Unity output sample rate.</param>
    /// <param name="frames">The current Unity DSP buffer frame count.</param>
    /// <param name="bufferCount">The current Unity DSP buffer count.</param>
    public WindowsWaveOutput(IWindowsWaveApi api, int sampleRate, int frames, int bufferCount)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
        if (sampleRate <= 0 || frames <= 0 || bufferCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        sampleCount = checked(frames * 2);
        buffers = new PcmBuffer[bufferCount];
        try
        {
            WindowsWaveFormat format = new WindowsWaveFormat
            {
                FormatTag = 1,
                Channels = 2,
                SampleRate = checked((uint)sampleRate),
                BytesPerSecond = checked((uint)sampleRate * 4),
                BlockAlign = 4,
                BitsPerSample = 16,
            };
            // The driver retains this event until the device closes, including failed cleanup.
            completion.SafeWaitHandle.DangerousAddRef(ref eventRetained);
            Check(api.Open(ref format, completion, out device), "open");
            for (int index = 0; index < buffers.Length; index++)
            {
                PcmBuffer buffer = new PcmBuffer(sampleCount);
                buffers[index] = buffer;
                Check(api.Prepare(device, buffer.Header), "prepare");
                buffer.Prepared = true;
            }
            // Start the device clock before handing off the emulated mixer.
            foreach (PcmBuffer buffer in buffers)
            {
                Check(api.Write(device, buffer.Header), "prime");
                buffer.Queued = true;
            }
            while (!buffers[0].IsDone)
            {
                if (!completion.WaitOne(_completionTimeoutMilliseconds))
                    throw new TimeoutException(
                        "Windows stereo output did not start returning buffers."
                    );
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Queues a mixed block and silences Unity's route only after successful handoff.
    /// Device completions pace the emulated mixer without dropping mixed blocks.
    /// </summary>
    /// <param name="samples">The final interleaved Unity mix.</param>
    /// <param name="channels">The mix channel count.</param>
    /// <returns>True when this stream accepted the block.</returns>
    public bool TryWrite(float[] samples, int channels)
    {
        while (true)
        {
            lock (sync)
            {
                if (disposed || failure != null)
                    return false;
                if (samples == null || channels != 2 || samples.Length != sampleCount)
                {
                    failure = "The Unity audio configuration changed.";
                    return false;
                }
                PcmBuffer buffer = buffers[nextBuffer];
                if (!buffer.Queued || buffer.IsDone)
                {
                    buffer.Copy(samples);
                    uint result = api.Write(device, buffer.Header);
                    if (result != 0)
                    {
                        failure = $"Windows stereo write failed ({result}).";
                        return false;
                    }
                    buffer.Queued = true;
                    nextBuffer = (nextBuffer + 1) % buffers.Length;
                    Array.Clear(samples, 0, samples.Length);
                    return true;
                }
            }
            try
            {
                if (completion.WaitOne(_completionTimeoutMilliseconds))
                    continue;
                Interlocked.CompareExchange(
                    ref failure,
                    "Windows stereo output stopped returning buffers.",
                    null
                );
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Wakes a waiting writer and releases only memory that the driver has returned.
    /// </summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;
            disposed = true;
            completion.Set();
            if (device != IntPtr.Zero)
            {
                uint reset = api.Reset(device);
                if (reset != 0)
                    failure ??= $"Windows stereo reset failed ({reset}).";
                foreach (PcmBuffer buffer in buffers)
                {
                    if (buffer == null)
                        continue;
                    if (buffer.Prepared)
                    {
                        uint result = api.Unprepare(device, buffer.Header);
                        if (result != 0)
                        {
                            // Retain memory still owned by the driver instead of freeing a live header.
                            failure ??= $"Windows stereo buffer release failed ({result}).";
                            continue;
                        }
                    }
                    buffer.Dispose();
                }
                uint close = api.Close(device);
                if (close != 0)
                    failure ??= $"Windows stereo close failed ({close}).";
                else
                    device = IntPtr.Zero;
            }
            else
            {
                foreach (PcmBuffer buffer in buffers)
                    buffer?.Dispose();
            }
            if (device == IntPtr.Zero && eventRetained)
            {
                completion.SafeWaitHandle.DangerousRelease();
                eventRetained = false;
            }
            completion.Dispose();
        }
    }

    /// <summary>
    /// Rejects an initialization failure before any mix is redirected.
    /// </summary>
    /// <param name="result">The Windows multimedia result code.</param>
    /// <param name="operation">The failed initialization operation.</param>
    private static void Check(uint result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"Windows stereo {operation} failed ({result}).");
    }

    /// <summary>
    /// Owns a prepared header and its reusable managed and native sample storage.
    /// </summary>
    private sealed class PcmBuffer : IDisposable
    {
        private static readonly int _flagsOffset = Marshal
            .OffsetOf<WindowsWaveHeader>(nameof(WindowsWaveHeader.Flags))
            .ToInt32();
        private readonly short[] samples;
        private IntPtr data;
        public IntPtr Header { get; private set; }
        public bool Prepared { get; set; }
        public bool Queued { get; set; }
        public bool IsDone => (Marshal.ReadInt32(Header, _flagsOffset) & 1) != 0;

        /// <summary>
        /// Allocates zeroed sample storage and a native waveform header.
        /// </summary>
        /// <param name="count">The interleaved sample count.</param>
        public PcmBuffer(int count)
        {
            samples = new short[count];
            try
            {
                data = Marshal.AllocHGlobal(checked(count * 2));
                Marshal.Copy(samples, 0, data, count);
                Header = Marshal.AllocHGlobal(Marshal.SizeOf<WindowsWaveHeader>());
                Marshal.StructureToPtr(
                    new WindowsWaveHeader { Data = data, Length = checked((uint)count * 2) },
                    Header,
                    false
                );
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts the mixed stereo samples without changing their channel order or gain.
        /// </summary>
        /// <param name="source">The interleaved mixed samples.</param>
        public void Copy(float[] source)
        {
            for (int index = 0; index < source.Length; index++)
            {
                float value = source[index];
                samples[index] =
                    float.IsNaN(value) ? (short)0
                    : value >= 1 ? short.MaxValue
                    : value <= -1 ? short.MinValue
                    : (short)(value * (value < 0 ? 32768f : 32767f));
            }
            Marshal.Copy(samples, 0, data, samples.Length);
        }

        /// <summary>
        /// Releases sample memory after native ownership has ended.
        /// </summary>
        public void Dispose()
        {
            if (Header != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Header);
                Header = IntPtr.Zero;
            }
            if (data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(data);
                data = IntPtr.Zero;
            }
        }
    }
}
