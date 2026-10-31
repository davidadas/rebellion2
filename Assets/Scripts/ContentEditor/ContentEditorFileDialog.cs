using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

/// <summary>
/// Opens the operating system's file and folder dialogs through Native File Dialog Extended.
/// </summary>
internal static class ContentEditorFileDialog
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFilter
    {
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Name;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string Specification;
    }

    /// <summary>
    /// Invokes a native selection operation after initialization.
    /// </summary>
    /// <param name="path">The library-owned UTF-8 selection.</param>
    /// <returns>The native result code.</returns>
    private delegate int SelectPath(out IntPtr path);

    /// <summary>
    /// Chooses one existing file using the supplied extension filter.
    /// </summary>
    /// <param name="directory">The suggested starting directory.</param>
    /// <param name="extensions">The supported extensions.</param>
    /// <returns>The selected path, or null when canceled.</returns>
    internal static string OpenFile(string directory, IEnumerable<string> extensions)
    {
        NativeFilter[] filters = CreateFilters(extensions);
        return Select(
            (out IntPtr path) => OpenDialog(out path, filters, (uint)filters.Length, directory)
        );
    }

    /// <summary>
    /// Chooses a directory using the operating system's folder picker.
    /// </summary>
    /// <param name="directory">The suggested starting directory.</param>
    /// <returns>The selected directory, or null when canceled.</returns>
    internal static string OpenFolder(string directory)
    {
        return Select((out IntPtr path) => PickFolder(out path, directory));
    }

    /// <summary>
    /// Chooses an output file, including the operating system's replacement confirmation.
    /// </summary>
    /// <param name="directory">The suggested starting directory.</param>
    /// <param name="name">The suggested filename.</param>
    /// <param name="extensions">The supported extensions.</param>
    /// <returns>The selected output path, or null when canceled.</returns>
    internal static string SaveFile(string directory, string name, IEnumerable<string> extensions)
    {
        NativeFilter[] filters = CreateFilters(extensions);
        return Select(
            (out IntPtr path) =>
                SaveDialog(out path, filters, (uint)filters.Length, directory, name)
        );
    }

    /// <summary>
    /// Converts authored extensions into the native library's filter format.
    /// </summary>
    /// <param name="extensions">The supported extensions.</param>
    /// <returns>The native filter list.</returns>
    private static NativeFilter[] CreateFilters(IEnumerable<string> extensions)
    {
        string specification = string.Join(
            ",",
            (extensions ?? Array.Empty<string>())
                .Select(extension => extension.TrimStart('.'))
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        );
        return specification.Length == 0
            ? Array.Empty<NativeFilter>()
            : new[]
            {
                new NativeFilter { Name = "Supported files", Specification = specification },
            };
    }

    /// <summary>
    /// Runs one dialog on the calling UI thread and releases all library-owned state.
    /// </summary>
    /// <param name="choose">The native selection operation.</param>
    /// <returns>The selected UTF-8 path, or null when canceled.</returns>
    private static string Select(SelectPath choose)
    {
        if (Initialize() != 1)
            throw new IOException(Marshal.PtrToStringUTF8(GetError()));
        IntPtr path = IntPtr.Zero;
        try
        {
            int result = choose(out path);
            if (result == 2)
                return null;
            if (result != 1)
                throw new IOException(Marshal.PtrToStringUTF8(GetError()));
            return Marshal.PtrToStringUTF8(path);
        }
        finally
        {
            if (path != IntPtr.Zero)
                FreePath(path);
            Quit();
        }
    }

    /// <summary>
    /// Initializes the native dialog backend on this thread.
    /// </summary>
    /// <returns>The native result code.</returns>
    [DllImport("nfd", EntryPoint = "NFD_Init", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Initialize();

    /// <summary>
    /// Releases the thread's initialized dialog backend.
    /// </summary>
    [DllImport("nfd", EntryPoint = "NFD_Quit", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Quit();

    /// <summary>
    /// Reads the native backend's last error message.
    /// </summary>
    /// <returns>The borrowed UTF-8 error string.</returns>
    [DllImport("nfd", EntryPoint = "NFD_GetError", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetError();

    /// <summary>
    /// Releases a selected UTF-8 path.
    /// </summary>
    /// <param name="path">The library-owned path.</param>
    [DllImport("nfd", EntryPoint = "NFD_FreePathU8", CallingConvention = CallingConvention.Cdecl)]
    private static extern void FreePath(IntPtr path);

    /// <summary>
    /// Opens the native single-file picker.
    /// </summary>
    /// <param name="path">The library-owned selected path.</param>
    /// <param name="filters">The allowed file filters.</param>
    /// <param name="filterCount">The number of filters.</param>
    /// <param name="directory">The initial directory.</param>
    /// <returns>The native result code.</returns>
    [DllImport("nfd", EntryPoint = "NFD_OpenDialogU8", CallingConvention = CallingConvention.Cdecl)]
    private static extern int OpenDialog(
        out IntPtr path,
        [In] NativeFilter[] filters,
        uint filterCount,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string directory
    );

    /// <summary>
    /// Opens the native folder picker.
    /// </summary>
    /// <param name="path">The library-owned selected directory.</param>
    /// <param name="directory">The initial directory.</param>
    /// <returns>The native result code.</returns>
    [DllImport("nfd", EntryPoint = "NFD_PickFolderU8", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PickFolder(
        out IntPtr path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string directory
    );

    /// <summary>
    /// Opens the native save-file picker.
    /// </summary>
    /// <param name="path">The library-owned selected path.</param>
    /// <param name="filters">The allowed file filters.</param>
    /// <param name="filterCount">The number of filters.</param>
    /// <param name="directory">The initial directory.</param>
    /// <param name="name">The suggested filename.</param>
    /// <returns>The native result code.</returns>
    [DllImport("nfd", EntryPoint = "NFD_SaveDialogU8", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SaveDialog(
        out IntPtr path,
        [In] NativeFilter[] filters,
        uint filterCount,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string directory,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name
    );
}
