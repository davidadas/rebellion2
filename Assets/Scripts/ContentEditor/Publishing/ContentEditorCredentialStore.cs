using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Keeps a publishing credential in the current user's operating-system secret store.
/// </summary>
internal abstract class ContentEditorCredentialStore
{
    protected const string _label = "Rebellion 2 toolkit publishing";
    protected readonly string key;

    /// <summary>
    /// Identifies the credential independently of any editable content pack.
    /// </summary>
    /// <param name="key">The application-owned credential identifier.</param>
    protected ContentEditorCredentialStore(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A credential identifier is required.", nameof(key));
        this.key = key;
    }

    /// <summary>
    /// Creates the system credential adapter without opening or unlocking its store.
    /// </summary>
    /// <param name="key">The application-owned credential identifier.</param>
    /// <returns>The adapter for the current desktop platform.</returns>
    internal static ContentEditorCredentialStore Create(string key)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new ContentEditorMacCredentialStore(key);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new ContentEditorWindowsCredentialStore(key);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new ContentEditorLinuxCredentialStore(key);
        throw new PlatformNotSupportedException(
            "Secure publishing credentials require a supported desktop platform."
        );
    }

    /// <summary>
    /// Reads the saved credential, allowing the system to request an unlock when necessary.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The credential, or null when it has not been saved.</returns>
    internal abstract string Read(CancellationToken cancellationToken);

    /// <summary>
    /// Saves a credential persistently for this user on this device.
    /// </summary>
    /// <param name="credential">The validated publishing credential.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal abstract void Write(string credential, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the saved credential, failing if the system cannot confirm its removal.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal abstract void Delete(CancellationToken cancellationToken);

    /// <summary>
    /// Reports native failures without including credentials or native error payloads.
    /// </summary>
    /// <param name="action">The operation that failed.</param>
    /// <param name="code">The system status code.</param>
    /// <returns>The actionable, credential-free error.</returns>
    protected static InvalidOperationException Failure(string action, int code) =>
        new(
            $"The system credential store could not {action} the Thunderstore connection (error {code}). Unlock it and try again."
        );
}
