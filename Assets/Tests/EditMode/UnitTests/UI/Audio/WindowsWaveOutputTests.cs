using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public sealed class WindowsWaveOutputTests
{
    [Test]
    public void Constructor_EngineConfiguration_OpensExplicitStereoPcm()
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);

        Assert.AreEqual(1, api.Format.FormatTag);
        Assert.AreEqual(2, api.Format.Channels);
        Assert.AreEqual(48000, api.Format.SampleRate);
        Assert.AreEqual(192000, api.Format.BytesPerSecond);
        Assert.AreEqual(4, api.Format.BlockAlign);
        Assert.AreEqual(16, api.Format.BitsPerSample);
        Assert.AreEqual(2, api.Prepared.Count);
    }

    [Test]
    public void TryWrite_StereoMix_PreservesChannelsAndGain()
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);
        float[] mix = { 0.25f, -0.5f, 1f, -1f };

        Assert.IsTrue(output.TryWrite(mix, 2));
        CollectionAssert.AreEqual(new short[] { 8191, -16384, 32767, -32768 }, api.Played[0]);
    }

    [Test]
    public void TryWrite_AcceptedMix_SilencesUnityRoute()
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);
        float[] mix = { 0.5f, -0.5f, 0.25f, -0.25f };

        Assert.IsTrue(output.TryWrite(mix, 2));
        CollectionAssert.AreEqual(new float[4], mix);
    }

    [Test]
    public void TryWrite_ClippedOrNonfiniteSamples_ProducesBoundedPcm()
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);
        float[] mix = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 2f };

        Assert.IsTrue(output.TryWrite(mix, 2));
        CollectionAssert.AreEqual(new short[] { 0, 32767, -32768, 32767 }, api.Played[0]);
    }

    [Test]
    public void TryWrite_DeviceRejectsBuffer_PreservesUnityMix()
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);
        api.WriteResult = 5;
        float[] mix = { 0.5f, -0.5f, 0.25f, -0.25f };
        float[] expected = (float[])mix.Clone();

        Assert.IsFalse(output.TryWrite(mix, 2));
        CollectionAssert.AreEqual(expected, mix);
        Assert.That(output.Failure, Does.Contain("write failed (5)"));
        Assert.IsFalse(output.TryWrite(mix, 2));
        Assert.AreEqual(1, api.WriteCalls);
    }

    [TestCase(1, 4)]
    [TestCase(6, 4)]
    [TestCase(2, 8)]
    public void TryWrite_ConfigurationMismatch_PreservesUnityMix(int channels, int count)
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);
        float[] mix = new float[count];
        mix[0] = 0.5f;

        Assert.IsFalse(output.TryWrite(mix, channels));
        Assert.AreEqual(0.5f, mix[0]);
        Assert.AreEqual(0, api.WriteCalls);
    }

    [Test]
    public void TryWrite_QueuedHeader_WaitsForDriverOwnershipToEnd()
    {
        FakeWaveApi api = new FakeWaveApi();
        using WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 1);
        Assert.IsTrue(output.TryWrite(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, 2));
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        Task<bool> pending = Task.Run(() =>
        {
            entered.Set();
            return output.TryWrite(new float[] { 0.4f, 0.3f, 0.2f, 0.1f }, 2);
        });
        Assert.IsTrue(entered.Wait(1000));
        Assert.IsFalse(pending.Wait(50));
        Assert.AreEqual(1, api.WriteCalls);
        api.ReturnBuffer();

        Assert.IsTrue(pending.Wait(1000));
        Assert.IsTrue(pending.Result);
        Assert.AreEqual(2, api.WriteCalls);
    }

    [Test]
    public void Dispose_WriterWaitingForBuffer_ReleasesWriterWithoutMutingMix()
    {
        FakeWaveApi api = new FakeWaveApi();
        WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 1);
        output.TryWrite(new float[4], 2);
        float[] mix = { 0.5f, 0.5f, 0.5f, 0.5f };
        using ManualResetEventSlim entered = new ManualResetEventSlim();
        Task<bool> pending = Task.Run(() =>
        {
            entered.Set();
            return output.TryWrite(mix, 2);
        });
        Assert.IsTrue(entered.Wait(1000));
        output.Dispose();

        Assert.IsTrue(pending.Wait(1000));
        Assert.IsFalse(pending.Result);
        Assert.AreEqual(0.5f, mix[0]);
    }

    [Test]
    public void Constructor_DeviceOpenFails_DoesNotPrepareOrCloseInvalidHandle()
    {
        FakeWaveApi api = new FakeWaveApi { OpenResult = 5 };

        Assert.Throws<InvalidOperationException>(() => new WindowsWaveOutput(api, 48000, 2, 2));
        CollectionAssert.AreEqual(new[] { "open" }, api.Operations);
    }

    [Test]
    public void Dispose_ResetFails_RecordsFailureAndStillReturnsPreparedHeaders()
    {
        FakeWaveApi api = new FakeWaveApi { ResetResult = 5 };
        WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 1);
        api.Operations.Clear();

        Assert.DoesNotThrow(output.Dispose);

        Assert.That(output.Failure, Does.Contain("reset failed (5)"));
        CollectionAssert.AreEqual(new[] { "reset", "unprepare", "close" }, api.Operations);
    }

    [Test]
    public void Constructor_PrimingFails_ReleasesEveryPreparedHeader()
    {
        FakeWaveApi api = new FakeWaveApi { WriteResult = 5 };

        Assert.Throws<InvalidOperationException>(() => new WindowsWaveOutput(api, 48000, 2, 2));
        CollectionAssert.AreEqual(
            new[] { "open", "prepare", "prepare", "reset", "unprepare", "unprepare", "close" },
            api.Operations
        );
    }

    [Test]
    public void Constructor_PreparationFailure_StopsAndReleasesPreparedHeaders()
    {
        FakeWaveApi api = new FakeWaveApi { FailPrepareAt = 2 };

        Assert.Throws<InvalidOperationException>(() => new WindowsWaveOutput(api, 48000, 2, 3));
        CollectionAssert.AreEqual(
            new[] { "open", "prepare", "prepare", "reset", "unprepare", "close" },
            api.Operations
        );
    }

    [Test]
    public void Dispose_QueuedHeaders_ResetsBeforeUnpreparingAndClosing()
    {
        FakeWaveApi api = new FakeWaveApi();
        WindowsWaveOutput output = new WindowsWaveOutput(api, 48000, 2, 2);
        output.TryWrite(new float[4], 2);
        api.Operations.Clear();

        output.Dispose();
        output.Dispose();

        CollectionAssert.AreEqual(
            new[] { "reset", "unprepare", "unprepare", "close" },
            api.Operations
        );
        Assert.IsFalse(output.TryWrite(new float[4], 2));
    }

    /// <summary>
    /// Models native header ownership and records the PCM handed to the operating system.
    /// </summary>
    private sealed class FakeWaveApi : IWindowsWaveApi
    {
        public readonly List<IntPtr> Prepared = new List<IntPtr>();
        public readonly List<short[]> Played = new List<short[]>();
        public readonly List<string> Operations = new List<string>();
        public WindowsWaveFormat Format;
        public uint WriteResult;
        public uint OpenResult;
        public uint ResetResult;
        public int FailPrepareAt;
        public int WriteCalls;
        private EventWaitHandle completion;
        private int primed;

        /// <summary>
        /// Records the explicit format and the output completion event.
        /// </summary>
        /// <param name="format">The requested format.</param>
        /// <param name="completionEvent">The completion event handle.</param>
        /// <param name="device">The fake device handle.</param>
        /// <returns>A successful native result.</returns>
        public uint Open(
            ref WindowsWaveFormat format,
            EventWaitHandle completionEvent,
            out IntPtr device
        )
        {
            Operations.Add("open");
            Format = format;
            completion = completionEvent;
            device = OpenResult == 0 ? new IntPtr(1) : IntPtr.Zero;
            return OpenResult;
        }

        /// <summary>
        /// Takes ownership of a prepared header unless the requested failure is reached.
        /// </summary>
        /// <param name="device">The fake device handle.</param>
        /// <param name="header">The native header.</param>
        /// <returns>The configured native result.</returns>
        public uint Prepare(IntPtr device, IntPtr header)
        {
            Operations.Add("prepare");
            if (FailPrepareAt == Prepared.Count + 1)
                return 1;
            Prepared.Add(header);
            return 0;
        }

        /// <summary>
        /// Completes initial silence immediately, then records PCM and retains it until completion.
        /// </summary>
        /// <param name="device">The fake device handle.</param>
        /// <param name="header">The prepared native header.</param>
        /// <returns>The configured native result.</returns>
        public uint Write(IntPtr device, IntPtr header)
        {
            if (primed < Prepared.Count)
            {
                primed++;
                if (WriteResult != 0)
                    return WriteResult;
                WindowsWaveHeader silence = Marshal.PtrToStructure<WindowsWaveHeader>(header);
                silence.Flags = 1;
                Marshal.StructureToPtr(silence, header, false);
                return 0;
            }
            WriteCalls++;
            if (WriteResult != 0)
                return WriteResult;
            WindowsWaveHeader value = Marshal.PtrToStructure<WindowsWaveHeader>(header);
            short[] samples = new short[value.Length / 2];
            Marshal.Copy(value.Data, samples, 0, samples.Length);
            Played.Add(samples);
            value.Flags = 16;
            Marshal.StructureToPtr(value, header, false);
            return 0;
        }

        /// <summary>
        /// Returns a submitted header and signals the actual completion event.
        /// </summary>
        public void ReturnBuffer()
        {
            WindowsWaveHeader value = Marshal.PtrToStructure<WindowsWaveHeader>(Prepared[0]);
            value.Flags = 1;
            Marshal.StructureToPtr(value, Prepared[0], false);
            completion.Set();
        }

        /// <summary>
        /// Records cancellation of outstanding playback.
        /// </summary>
        /// <param name="device">The fake device handle.</param>
        /// <returns>A successful native result.</returns>
        public uint Reset(IntPtr device)
        {
            Operations.Add("reset");
            return ResetResult;
        }

        /// <summary>
        /// Records the return of native header ownership.
        /// </summary>
        /// <param name="device">The fake device handle.</param>
        /// <param name="header">The prepared native header.</param>
        /// <returns>A successful native result.</returns>
        public uint Unprepare(IntPtr device, IntPtr header)
        {
            Operations.Add("unprepare");
            return 0;
        }

        /// <summary>
        /// Records closing the private output handle.
        /// </summary>
        /// <param name="device">The fake device handle.</param>
        /// <returns>A successful native result.</returns>
        public uint Close(IntPtr device)
        {
            Operations.Add("close");
            return 0;
        }
    }
}
