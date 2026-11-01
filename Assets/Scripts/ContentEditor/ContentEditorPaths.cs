using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Resolves physical authoring locations before storage creates or replaces files.
/// </summary>
internal static class ContentEditorPaths
{
    /// <summary>
    /// Resolves existing links while retaining the relative suffix of a new destination.
    /// </summary>
    /// <param name="path">The existing or proposed filesystem path.</param>
    /// <returns>The absolute physical path.</returns>
    internal static string Resolve(string path)
    {
        string existing = Path.GetFullPath(path);
        Stack<string> suffix = new();
        while (!File.Exists(existing) && !Directory.Exists(existing))
        {
            suffix.Push(Path.GetFileName(existing));
            existing =
                Path.GetDirectoryName(existing)
                ?? throw new IOException("The path has no existing parent directory.");
        }
        string resolved = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ResolveWindows(existing)
            : ResolveUnix(existing);
        while (suffix.Count > 0)
            resolved = Path.Combine(resolved, suffix.Pop());
        return Path.GetFullPath(resolved);
    }

    /// <summary>
    /// Checks containment after resolving links in both locations.
    /// </summary>
    /// <param name="root">The owning directory.</param>
    /// <param name="path">The candidate location.</param>
    /// <returns>Whether the candidate is the root or one of its descendants.</returns>
    internal static bool Contains(string root, string path) =>
        IsWithin(Resolve(root), Resolve(path));

    /// <summary>
    /// Rejects authoring locations that resolve to installed application or pack data.
    /// </summary>
    /// <param name="contentRoot">The installed content directory.</param>
    /// <param name="path">The proposed writable location.</param>
    internal static void VerifyWritableLocation(string contentRoot, string path)
    {
        ContentPackEditorProject.VerifyWorkingFolder(contentRoot, path);
        string target = Resolve(path);
        if (string.Equals(target, Resolve(contentRoot), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Installed content is read-only. Choose a working folder.");
        foreach (string name in new[] { "Application", "Packs" })
        {
            string protectedRoot = Path.Combine(contentRoot, name);
            if (IsWithin(Resolve(protectedRoot), target))
                throw new IOException("Installed content is read-only. Choose a working folder.");
            if (name == "Packs" && Directory.Exists(protectedRoot))
                foreach (string pack in Directory.EnumerateDirectories(protectedRoot))
                    if (IsWithin(Resolve(pack), target))
                        throw new IOException(
                            "Installed content is read-only. Choose a working folder."
                        );
        }
    }

    /// <summary>
    /// Verifies that a write stays within the physical working pack and outside installed content.
    /// </summary>
    /// <param name="project">The owner of the write.</param>
    /// <param name="path">The proposed file or directory.</param>
    internal static void VerifyWritePath(ContentPackEditorProject project, string path)
    {
        VerifyWritableLocation(project.ContentRootPath, project.PackRootPath);
        VerifyWritableLocation(project.ContentRootPath, path);
        if (!Contains(project.PackRootPath, path))
            throw new IOException("The destination resolves outside your working content pack.");
    }

    /// <summary>
    /// Compares physical paths using an explicit directory boundary.
    /// </summary>
    /// <param name="root">The resolved owning directory.</param>
    /// <param name="path">The resolved candidate.</param>
    /// <returns>Whether the candidate stays inside the directory.</returns>
    private static bool IsWithin(string root, string path)
    {
        string parent = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parent, path, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(
                parent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );
    }

    /// <summary>
    /// Resolves a Windows file or directory through a metadata-only handle.
    /// </summary>
    /// <param name="path">The existing path.</param>
    /// <returns>The normalized drive or network path.</returns>
    private static string ResolveWindows(string path)
    {
        using SafeFileHandle handle = OpenPath(
            path,
            0,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            0x02000000,
            IntPtr.Zero
        );
        if (handle.IsInvalid)
            throw new IOException(
                "The path could not be resolved: " + path,
                new Win32Exception(Marshal.GetLastWin32Error())
            );
        StringBuilder buffer = new(512);
        uint length = GetFinalPath(handle, buffer, (uint)buffer.Capacity, 0);
        if (length >= buffer.Capacity)
        {
            buffer.Capacity = checked((int)length + 1);
            length = GetFinalPath(handle, buffer, (uint)buffer.Capacity, 0);
        }
        if (length == 0 || length >= buffer.Capacity)
            throw new IOException(
                "The path could not be resolved: " + path,
                new Win32Exception(Marshal.GetLastWin32Error())
            );
        string result = buffer.ToString();
        return result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)
                ? @"\\" + result[8..]
            : result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..]
            : result;
    }

    /// <summary>
    /// Resolves a Unix path and releases the filesystem library's allocated buffer.
    /// </summary>
    /// <param name="path">The existing path.</param>
    /// <returns>The resolved absolute path.</returns>
    private static string ResolveUnix(string path)
    {
        IntPtr resolved = RealPath(path, IntPtr.Zero);
        if (resolved == IntPtr.Zero)
            throw new IOException(
                "The path could not be resolved: " + path,
                new Win32Exception(Marshal.GetLastWin32Error())
            );
        try
        {
            return Marshal.PtrToStringUTF8(resolved);
        }
        finally
        {
            Free(resolved);
        }
    }

    /// <summary>
    /// Opens an existing Windows path without requesting access to its contents.
    /// </summary>
    /// <param name="path">The existing file or directory.</param>
    /// <param name="access">The requested access rights.</param>
    /// <param name="share">The permitted concurrent access.</param>
    /// <param name="security">The optional security attributes.</param>
    /// <param name="mode">The existing-file disposition.</param>
    /// <param name="flags">The directory-handle flags.</param>
    /// <param name="template">The optional template handle.</param>
    /// <returns>The owned path handle.</returns>
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    private static extern SafeFileHandle OpenPath(
        string path,
        uint access,
        FileShare share,
        IntPtr security,
        FileMode mode,
        uint flags,
        IntPtr template
    );

    /// <summary>
    /// Reads the fully resolved name associated with an existing Windows handle.
    /// </summary>
    /// <param name="handle">The path handle.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="capacity">The capacity including the terminator.</param>
    /// <param name="flags">The normalized path format.</param>
    /// <returns>The written length or required capacity.</returns>
    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    private static extern uint GetFinalPath(
        SafeFileHandle handle,
        StringBuilder buffer,
        uint capacity,
        uint flags
    );

    /// <summary>
    /// Allocates the canonical name of an existing Unix path.
    /// </summary>
    /// <param name="path">The UTF-8 filesystem path.</param>
    /// <param name="buffer">Null to request a library-owned buffer.</param>
    /// <returns>The owned buffer or null on failure.</returns>
    [DllImport(
        "libc",
        EntryPoint = "realpath",
        CallingConvention = CallingConvention.Cdecl,
        SetLastError = true
    )]
    private static extern IntPtr RealPath(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr buffer
    );

    /// <summary>
    /// Releases memory allocated by the Unix filesystem library.
    /// </summary>
    /// <param name="buffer">The owned native buffer.</param>
    [DllImport("libc", EntryPoint = "free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Free(IntPtr buffer);
}
