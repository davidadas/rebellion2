#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Calls the Windows waveform API without changing system device settings.
/// </summary>
internal sealed class WindowsWaveApi : IWindowsWaveApi
{
    private static readonly uint _headerSize = checked((uint)Marshal.SizeOf<WindowsWaveHeader>());

    /// <summary>
    /// Opens the default waveform mapper in 16-bit stereo with event-driven buffer completion.
    /// </summary>
    /// <param name="format">The requested PCM format.</param>
    /// <param name="completionEvent">The buffer completion event.</param>
    /// <param name="device">The opened device handle.</param>
    /// <returns>The Windows multimedia result code.</returns>
    public uint Open(
        ref WindowsWaveFormat format,
        EventWaitHandle completionEvent,
        out IntPtr device
    )
    {
        return waveOutOpen(
            out device,
            new UIntPtr(uint.MaxValue),
            ref format,
            completionEvent.SafeWaitHandle.DangerousGetHandle(),
            UIntPtr.Zero,
            0x50000
        );
    }

    /// <summary>
    /// Prepares a reusable output header.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <returns>The Windows multimedia result code.</returns>
    public uint Prepare(IntPtr device, IntPtr header) =>
        waveOutPrepareHeader(device, header, _headerSize);

    /// <summary>
    /// Submits a prepared PCM buffer.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <returns>The Windows multimedia result code.</returns>
    public uint Write(IntPtr device, IntPtr header) => waveOutWrite(device, header, _headerSize);

    /// <summary>
    /// Stops playback and returns outstanding buffers.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <returns>The Windows multimedia result code.</returns>
    public uint Reset(IntPtr device) => waveOutReset(device);

    /// <summary>
    /// Releases a returned buffer from the driver.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <returns>The Windows multimedia result code.</returns>
    public uint Unprepare(IntPtr device, IntPtr header) =>
        waveOutUnprepareHeader(device, header, _headerSize);

    /// <summary>
    /// Closes a waveform output device.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <returns>The Windows multimedia result code.</returns>
    public uint Close(IntPtr device) => waveOutClose(device);

    /// <summary>
    /// Creates a private waveform output stream.
    /// </summary>
    /// <param name="device">The opened device handle.</param>
    /// <param name="deviceId">The requested device or mapper.</param>
    /// <param name="format">The requested PCM format.</param>
    /// <param name="callback">The completion event handle.</param>
    /// <param name="instance">Unused callback state.</param>
    /// <param name="flags">The event callback mode.</param>
    /// <returns>The Windows multimedia result code.</returns>
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutOpen(
        out IntPtr device,
        UIntPtr deviceId,
        ref WindowsWaveFormat format,
        IntPtr callback,
        UIntPtr instance,
        uint flags
    );

    /// <summary>
    /// Prepares a waveform header for submission.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <param name="size">The native header size.</param>
    /// <returns>The Windows multimedia result code.</returns>
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);

    /// <summary>
    /// Submits a prepared waveform header.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <param name="size">The native header size.</param>
    /// <returns>The Windows multimedia result code.</returns>
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);

    /// <summary>
    /// Returns queued headers from a waveform output device.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <returns>The Windows multimedia result code.</returns>
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutReset(IntPtr device);

    /// <summary>
    /// Removes the driver's preparation of a returned header.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <param name="size">The native header size.</param>
    /// <returns>The Windows multimedia result code.</returns>
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);

    /// <summary>
    /// Closes a waveform output device with no outstanding buffers.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <returns>The Windows multimedia result code.</returns>
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutClose(IntPtr device);
}
#endif
