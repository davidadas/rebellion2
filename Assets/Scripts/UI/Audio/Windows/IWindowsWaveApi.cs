using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Defines the Windows waveform operations used by the private stereo output stream.
/// </summary>
internal interface IWindowsWaveApi
{
    /// <summary>
    /// Opens the system default output using the requested PCM format and completion event.
    /// </summary>
    /// <param name="format">The explicitly requested PCM format.</param>
    /// <param name="completionEvent">The event signalled when a queued header is returned.</param>
    /// <param name="device">The opened device handle.</param>
    /// <returns>The Windows multimedia result code.</returns>
    uint Open(ref WindowsWaveFormat format, EventWaitHandle completionEvent, out IntPtr device);

    /// <summary>
    /// Prepares a buffer before its first submission.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <returns>The Windows multimedia result code.</returns>
    uint Prepare(IntPtr device, IntPtr header);

    /// <summary>
    /// Submits a prepared buffer for playback.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <returns>The Windows multimedia result code.</returns>
    uint Write(IntPtr device, IntPtr header);

    /// <summary>
    /// Stops playback and returns all submitted headers.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <returns>The Windows multimedia result code.</returns>
    uint Reset(IntPtr device);

    /// <summary>
    /// Releases the driver's ownership of a prepared header.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <param name="header">The waveform header.</param>
    /// <returns>The Windows multimedia result code.</returns>
    uint Unprepare(IntPtr device, IntPtr header);

    /// <summary>
    /// Closes the output device after its buffers have been released.
    /// </summary>
    /// <param name="device">The open output device.</param>
    /// <returns>The Windows multimedia result code.</returns>
    uint Close(IntPtr device);
}

/// <summary>
/// Describes the PCM format passed to Windows waveform output.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct WindowsWaveFormat
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SampleRate;
    public uint BytesPerSecond;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort ExtraSize;
}

/// <summary>
/// Describes one reusable Windows waveform buffer.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowsWaveHeader
{
    public IntPtr Data;
    public uint Length;
    public uint Recorded;
    public UIntPtr User;
    public uint Flags;
    public uint Loops;
    public IntPtr Next;
    public UIntPtr Reserved;
}
