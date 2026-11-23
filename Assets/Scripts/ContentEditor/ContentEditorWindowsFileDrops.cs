using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using UnityEngine;

/// <summary>
/// Receives Explorer file drops on the content editor's own Windows window.
/// </summary>
internal sealed class ContentEditorWindowsFileDrops : ContentEditorFileDrops
{
    private const uint _dropFiles = 0x0233;
    private const uint _windowDestroyed = 0x0082;
    private static readonly WindowProcedure _procedure = ProcessWindowMessage;
    private static ContentEditorWindowsFileDrops _current;
    private IntPtr window;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr id,
        UIntPtr data
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    /// <summary>
    /// Installs a subclass on the editor's main window once it exists.
    /// </summary>
    protected override void Poll()
    {
        if (window != IntPtr.Zero)
            return;
        using Process process = Process.GetCurrentProcess();
        IntPtr handle = process.MainWindowHandle;
        if (
            handle == IntPtr.Zero
            || !SetWindowSubclass(handle, _procedure, UIntPtr.Zero, UIntPtr.Zero)
        )
            return;
        window = handle;
        _current = this;
        DragAcceptFiles(handle, true);
    }

    /// <summary>
    /// Removes this receiver while leaving the player's window procedure in place.
    /// </summary>
    public override void Dispose()
    {
        if (window == IntPtr.Zero)
            return;
        DragAcceptFiles(window, false);
        RemoveWindowSubclass(window, _procedure, UIntPtr.Zero);
        window = IntPtr.Zero;
        _current = null;
    }

    /// <summary>
    /// Queues dropped files and forwards unrelated messages to the existing procedure.
    /// </summary>
    /// <param name="hwnd">The receiving window.</param>
    /// <param name="message">The native message identifier.</param>
    /// <param name="wParam">The first native message argument.</param>
    /// <param name="lParam">The second native message argument.</param>
    /// <param name="id">The registered subclass identifier.</param>
    /// <param name="data">The subclass reference data.</param>
    /// <returns>The result returned to the window manager.</returns>
    [MonoPInvokeCallback(typeof(WindowProcedure))]
    private static IntPtr ProcessWindowMessage(
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr id,
        UIntPtr data
    )
    {
        if (message == _dropFiles)
        {
            try
            {
                _current?.QueueFiles(hwnd, wParam);
            }
            finally
            {
                DragFinish(wParam);
            }
            return IntPtr.Zero;
        }
        if (message == _windowDestroyed)
            _current?.Dispose();
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    /// <summary>
    /// Reads the file names and client coordinates from a completed shell drop.
    /// </summary>
    /// <param name="hwnd">The client window receiving the image.</param>
    /// <param name="drop">The shell's file drop handle.</param>
    private void QueueFiles(IntPtr hwnd, IntPtr drop)
    {
        if (!DragQueryPoint(drop, out Point point) || !GetClientRect(hwnd, out Rect bounds))
            return;
        int width = bounds.Right - bounds.Left;
        int height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0)
            return;
        uint count = DragQueryFile(drop, uint.MaxValue, null, 0);
        if (count == 0)
            return;
        string[] files = new string[count];
        for (uint index = 0; index < count; index++)
        {
            uint length = DragQueryFile(drop, index, null, 0);
            StringBuilder path = new((int)length + 1);
            DragQueryFile(drop, index, path, length + 1);
            files[index] = path.ToString();
        }
        Enqueue(
            files,
            new Vector2(
                (point.X - bounds.Left) / (float)width,
                (bounds.Bottom - point.Y) / (float)height
            )
        );
    }

    /// <summary>
    /// Registers a callback alongside the existing window procedure.
    /// </summary>
    /// <param name="hwnd">The target window.</param>
    /// <param name="callback">The new subclass callback.</param>
    /// <param name="id">The subclass identifier.</param>
    /// <param name="data">The subclass reference data.</param>
    /// <returns>Whether the callback was installed.</returns>
    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(
        IntPtr hwnd,
        WindowProcedure callback,
        UIntPtr id,
        UIntPtr data
    );

    /// <summary>
    /// Removes a registered subclass callback.
    /// </summary>
    /// <param name="hwnd">The target window.</param>
    /// <param name="callback">The installed callback.</param>
    /// <param name="id">The subclass identifier.</param>
    /// <returns>Whether the callback was removed.</returns>
    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(
        IntPtr hwnd,
        WindowProcedure callback,
        UIntPtr id
    );

    /// <summary>
    /// Forwards a message through the existing subclass chain.
    /// </summary>
    /// <param name="hwnd">The receiving window.</param>
    /// <param name="message">The message identifier.</param>
    /// <param name="wParam">The first native argument.</param>
    /// <param name="lParam">The second native argument.</param>
    /// <returns>The existing procedure's result.</returns>
    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam
    );

    /// <summary>
    /// Enables or disables shell file drops on the window.
    /// </summary>
    /// <param name="hwnd">The target window.</param>
    /// <param name="accept">Whether the window accepts drops.</param>
    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);

    /// <summary>
    /// Reads a path or the number of files in a shell drop.
    /// </summary>
    /// <param name="drop">The file drop handle.</param>
    /// <param name="index">The file index or the sentinel for file count.</param>
    /// <param name="path">The path buffer, when requesting a file name.</param>
    /// <param name="size">The path buffer length.</param>
    /// <returns>The file count or path length for the request.</returns>
    [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(
        IntPtr drop,
        uint index,
        StringBuilder path,
        uint size
    );

    /// <summary>
    /// Reads the client point for a shell drop.
    /// </summary>
    /// <param name="drop">The file drop handle.</param>
    /// <param name="point">The native client coordinates.</param>
    /// <returns>Whether the drop landed in the client area.</returns>
    [DllImport("shell32.dll")]
    private static extern bool DragQueryPoint(IntPtr drop, out Point point);

    /// <summary>
    /// Releases the shell drop after reading its data.
    /// </summary>
    /// <param name="drop">The file drop handle.</param>
    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr drop);

    /// <summary>
    /// Reads the client area used to normalize the drop coordinates.
    /// </summary>
    /// <param name="hwnd">The receiving window.</param>
    /// <param name="bounds">The client bounds in native screen units.</param>
    /// <returns>Whether the bounds were retrieved.</returns>
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out Rect bounds);
}
