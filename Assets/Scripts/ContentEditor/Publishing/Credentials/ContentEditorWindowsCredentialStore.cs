using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

/// <summary>
/// Persists publishing credentials in Windows Credential Manager for the current user.
/// </summary>
internal sealed class ContentEditorWindowsCredentialStore : ContentEditorCredentialStore
{
    /// <summary>
    /// Identifies this application's generic credential.
    /// </summary>
    /// <param name="key">The application-owned target name.</param>
    internal ContentEditorWindowsCredentialStore(string key)
        : base(key) { }

    /// <summary>
    /// Reads and releases the system's credential buffer without retaining native copies.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The saved token, or null when no credential exists.</returns>
    internal override string Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CredRead(key, 1, 0, out IntPtr pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 1168)
                return null;
            throw Failure("read", error);
        }
        try
        {
            Credential entry = Marshal.PtrToStructure<Credential>(pointer);
            byte[] bytes = new byte[checked((int)entry.BlobSize)];
            try
            {
                Marshal.Copy(entry.Blob, bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
                ClearBuffer(entry.Blob, bytes.Length);
            }
        }
        finally
        {
            CredFree(pointer);
        }
    }

    /// <summary>
    /// Writes a non-roaming credential that survives logouts and toolkit restarts.
    /// </summary>
    /// <param name="credential">The validated token.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal override void Write(string credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = Encoding.UTF8.GetBytes(credential);
        IntPtr buffer = IntPtr.Zero;
        try
        {
            if (bytes.Length > 2560)
                throw new InvalidOperationException(
                    "This token exceeds the size supported by Windows Credential Manager."
                );
            buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Credential entry = new()
            {
                Type = 1,
                TargetName = key,
                Comment = _label,
                BlobSize = (uint)bytes.Length,
                Blob = buffer,
                Persist = 2,
                UserName = "publishing",
            };
            if (!CredWrite(ref entry, 0))
                throw Failure("save", Marshal.GetLastWin32Error());
        }
        finally
        {
            Array.Clear(bytes, 0, bytes.Length);
            if (buffer != IntPtr.Zero)
            {
                ClearBuffer(buffer, bytes.Length);
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    /// <summary>
    /// Removes only the application's named generic credential.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal override void Delete(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CredDelete(key, 1, 0))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 1168)
                throw Failure("remove", error);
        }
    }

    /// <summary>
    /// Clears a caller-owned native token buffer before releasing it.
    /// </summary>
    /// <param name="buffer">The allocated buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    private static void ClearBuffer(IntPtr buffer, int length)
    {
        for (int i = 0; i < length; i++)
            Marshal.WriteByte(buffer, i, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    /// <summary>
    /// Reads one generic credential from the current user's Windows vault.
    /// </summary>
    /// <param name="target">The credential name.</param>
    /// <param name="type">The credential category.</param>
    /// <param name="flags">Reserved flags.</param>
    /// <param name="credential">Receives the owned native buffer.</param>
    /// <returns>Whether the credential was read.</returns>
    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredReadW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credential
    );

    /// <summary>
    /// Writes a credential through Windows' protected per-user storage.
    /// </summary>
    /// <param name="credential">The credential metadata and data.</param>
    /// <param name="flags">The write options.</param>
    /// <returns>Whether the write succeeded.</returns>
    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredWriteW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    /// <summary>
    /// Removes a named credential from the Windows vault.
    /// </summary>
    /// <param name="target">The credential name.</param>
    /// <param name="type">The credential category.</param>
    /// <param name="flags">Reserved flags.</param>
    /// <returns>Whether the credential was removed.</returns>
    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredDeleteW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    /// <summary>
    /// Releases a buffer returned by Windows Credential Manager.
    /// </summary>
    /// <param name="buffer">The owned credential buffer.</param>
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
