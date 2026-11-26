using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Persists publishing credentials in the user's Linux Secret Service collection.
/// </summary>
internal sealed class ContentEditorLinuxCredentialStore : ContentEditorCredentialStore
{
    private const string _secret = "libsecret-1.so.0";
    private const string _glib = "libglib-2.0.so.0";
    private const string _gio = "libgio-2.0.so.0";
    private const string _gobject = "libgobject-2.0.so.0";

    /// <summary>
    /// Identifies the application's secret with its own matching schema name.
    /// </summary>
    /// <param name="key">The application-owned schema name.</param>
    internal ContentEditorLinuxCredentialStore(string key)
        : base(key) { }

    /// <summary>
    /// Reads the saved token without accessing other applications' secrets.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The saved token, or null when none exists.</returns>
    internal override string Read(CancellationToken cancellationToken) =>
        WithStore(
            query =>
            {
                IntPtr password = Lookup(query, cancellationToken);
                if (password == IntPtr.Zero)
                    return null;
                try
                {
                    return Marshal.PtrToStringUTF8(password);
                }
                finally
                {
                    PasswordFree(password);
                }
            },
            cancellationToken
        );

    /// <summary>
    /// Writes to the persistent default collection rather than a login-session collection.
    /// </summary>
    /// <param name="credential">The validated token.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal override void Write(string credential, CancellationToken cancellationToken) =>
        WithStore(
            query =>
            {
                bool stored = PasswordStore(
                    query.Schema,
                    query.Attributes,
                    null,
                    _label,
                    credential,
                    query.Cancellable,
                    out IntPtr error
                );
                CheckError(error, "save", cancellationToken);
                if (!stored)
                    throw Failure("save", -1);
                return true;
            },
            cancellationToken
        );

    /// <summary>
    /// Unlocks and removes the matching credential, confirming that none remains.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal override void Delete(CancellationToken cancellationToken) =>
        WithStore(
            query =>
            {
                IntPtr password = Lookup(query, cancellationToken);
                if (password == IntPtr.Zero)
                    return true;
                PasswordFree(password);
                _ = PasswordClear(
                    query.Schema,
                    query.Attributes,
                    query.Cancellable,
                    out IntPtr error
                );
                CheckError(error, "remove", cancellationToken);
                if (Contains(query, cancellationToken))
                    throw Failure("remove", -1);
                return true;
            },
            cancellationToken
        );

    /// <summary>
    /// Owns native lookup resources and reports an unavailable desktop secret service clearly.
    /// </summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="operation">The operation using the matching schema.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The operation result.</returns>
    private T WithStore<T>(Func<LinuxQuery, T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using LinuxQuery query = new(key, cancellationToken);
            return operation(query);
        }
        catch (Exception exception)
            when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new InvalidOperationException(
                "Secure storage requires libsecret and a running Secret Service, such as GNOME Keyring or KWallet. Enable them to access saved connections."
            );
        }
    }

    /// <summary>
    /// Looks up and unlocks a matching secret, retaining native ownership until it is freed.
    /// </summary>
    /// <param name="query">The operation's owned matching resources.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The owned password pointer, or zero when no match exists.</returns>
    private static IntPtr Lookup(LinuxQuery query, CancellationToken cancellationToken)
    {
        IntPtr password = PasswordLookup(
            query.Schema,
            query.Attributes,
            query.Cancellable,
            out IntPtr error
        );
        if (error != IntPtr.Zero)
        {
            if (password != IntPtr.Zero)
                PasswordFree(password);
            CheckError(error, "read", cancellationToken);
        }
        if (password == IntPtr.Zero && Contains(query, cancellationToken))
            throw Failure("unlock", -1);
        return password;
    }

    /// <summary>
    /// Detects matching items even when their collection remains locked.
    /// </summary>
    /// <param name="query">The operation's matching resources.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>Whether any matching item still exists.</returns>
    private static bool Contains(LinuxQuery query, CancellationToken cancellationToken)
    {
        IntPtr items = ServiceSearch(
            IntPtr.Zero,
            query.Schema,
            query.Attributes,
            0,
            query.Cancellable,
            out IntPtr error
        );
        try
        {
            CheckError(error, "read", cancellationToken);
            return items != IntPtr.Zero;
        }
        finally
        {
            for (
                IntPtr item = items;
                item != IntPtr.Zero;
                item = Marshal.ReadIntPtr(item, IntPtr.Size)
            )
                ObjectUnref(Marshal.ReadIntPtr(item));
            if (items != IntPtr.Zero)
                ListFree(items);
        }
    }

    /// <summary>
    /// Frees native error details without copying possible credential data into logs.
    /// </summary>
    /// <param name="error">The owned native error, or zero.</param>
    /// <param name="action">The failed operation.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    private static void CheckError(IntPtr error, string action, CancellationToken cancellationToken)
    {
        if (error == IntPtr.Zero)
            return;
        int code = Marshal.ReadInt32(error, 4);
        ErrorFree(error);
        cancellationToken.ThrowIfCancellationRequested();
        throw Failure(action, code);
    }

    /// <summary>
    /// Owns the matching schema, attribute table, and cancellable native operation.
    /// </summary>
    private sealed class LinuxQuery : IDisposable
    {
        private CancellationTokenRegistration registration;
        internal IntPtr Schema { get; private set; }
        internal IntPtr Attributes { get; private set; }
        internal IntPtr Cancellable { get; private set; }

        /// <summary>
        /// Creates a schema whose name alone restricts matches to this application's credential.
        /// </summary>
        /// <param name="key">The unique matching schema name.</param>
        /// <param name="cancellationToken">The requested cancellation.</param>
        internal LinuxQuery(string key, CancellationToken cancellationToken)
        {
            try
            {
                Attributes = HashTableNew(IntPtr.Zero, IntPtr.Zero);
                Schema = SchemaNew(key, 0, Attributes);
                Cancellable = CancellableNew();
                if (
                    Attributes == IntPtr.Zero
                    || Schema == IntPtr.Zero
                    || Cancellable == IntPtr.Zero
                )
                    throw Failure("open", -1);
                registration = cancellationToken.Register(Cancel);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Cancels an outstanding Secret Service request from the caller's cancellation thread.
        /// </summary>
        private void Cancel() => CancellableCancel(Cancellable);

        /// <summary>
        /// Releases the cancellation registration before destroying the native request resources.
        /// </summary>
        public void Dispose()
        {
            registration.Dispose();
            if (Cancellable != IntPtr.Zero)
                ObjectUnref(Cancellable);
            if (Schema != IntPtr.Zero)
                SchemaUnref(Schema);
            if (Attributes != IntPtr.Zero)
                HashTableUnref(Attributes);
            Cancellable = Schema = Attributes = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Creates a schema from an explicitly owned attribute table.
    /// </summary>
    /// <param name="name">The matching schema name.</param>
    /// <param name="flags">The matching options.</param>
    /// <param name="attributes">The schema's attribute definitions.</param>
    /// <returns>The owned schema.</returns>
    [DllImport(_secret, EntryPoint = "secret_schema_newv")]
    private static extern IntPtr SchemaNew(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int flags,
        IntPtr attributes
    );

    /// <summary>
    /// Releases a dynamically created matching schema.
    /// </summary>
    /// <param name="schema">The owned schema.</param>
    [DllImport(_secret, EntryPoint = "secret_schema_unref")]
    private static extern void SchemaUnref(IntPtr schema);

    /// <summary>
    /// Reads an owned UTF-8 password from the user's Secret Service.
    /// </summary>
    /// <param name="schema">The matching schema.</param>
    /// <param name="attributes">The matching attributes.</param>
    /// <param name="cancellable">The cancellation object.</param>
    /// <param name="error">Receives an owned error.</param>
    /// <returns>The owned password pointer.</returns>
    [DllImport(_secret, EntryPoint = "secret_password_lookupv_sync")]
    private static extern IntPtr PasswordLookup(
        IntPtr schema,
        IntPtr attributes,
        IntPtr cancellable,
        out IntPtr error
    );

    /// <summary>
    /// Stores a UTF-8 password in a persistent collection.
    /// </summary>
    /// <param name="schema">The matching schema.</param>
    /// <param name="attributes">The matching attributes.</param>
    /// <param name="collection">The collection alias, or null for the default.</param>
    /// <param name="label">The system-visible description.</param>
    /// <param name="password">The password to protect.</param>
    /// <param name="cancellable">The cancellation object.</param>
    /// <param name="error">Receives an owned error.</param>
    /// <returns>Whether the password was saved.</returns>
    [DllImport(_secret, EntryPoint = "secret_password_storev_sync")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PasswordStore(
        IntPtr schema,
        IntPtr attributes,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        IntPtr cancellable,
        out IntPtr error
    );

    /// <summary>
    /// Removes the matching unlocked secrets.
    /// </summary>
    /// <param name="schema">The matching schema.</param>
    /// <param name="attributes">The matching attributes.</param>
    /// <param name="cancellable">The cancellation object.</param>
    /// <param name="error">Receives an owned error.</param>
    /// <returns>Whether any secrets were removed.</returns>
    [DllImport(_secret, EntryPoint = "secret_password_clearv_sync")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PasswordClear(
        IntPtr schema,
        IntPtr attributes,
        IntPtr cancellable,
        out IntPtr error
    );

    /// <summary>
    /// Searches item metadata without unlocking items or loading their secret values.
    /// </summary>
    /// <param name="service">The default service when null.</param>
    /// <param name="schema">The matching schema.</param>
    /// <param name="attributes">The matching attributes.</param>
    /// <param name="flags">The search options.</param>
    /// <param name="cancellable">The cancellation object.</param>
    /// <param name="error">Receives an owned error.</param>
    /// <returns>The owned list of matching item objects.</returns>
    [DllImport(_secret, EntryPoint = "secret_service_search_sync")]
    private static extern IntPtr ServiceSearch(
        IntPtr service,
        IntPtr schema,
        IntPtr attributes,
        int flags,
        IntPtr cancellable,
        out IntPtr error
    );

    /// <summary>
    /// Releases list nodes after their owned item objects have been released.
    /// </summary>
    /// <param name="items">The owned item list.</param>
    [DllImport(_glib, EntryPoint = "g_list_free")]
    private static extern void ListFree(IntPtr items);

    /// <summary>
    /// Clears and frees an owned password buffer.
    /// </summary>
    /// <param name="password">The owned password buffer.</param>
    [DllImport(_secret, EntryPoint = "secret_password_free")]
    private static extern void PasswordFree(IntPtr password);

    /// <summary>
    /// Creates an empty attribute table used with a named schema.
    /// </summary>
    /// <param name="hash">The optional key hash function.</param>
    /// <param name="equal">The optional key comparison function.</param>
    /// <returns>The owned table.</returns>
    [DllImport(_glib, EntryPoint = "g_hash_table_new")]
    private static extern IntPtr HashTableNew(IntPtr hash, IntPtr equal);

    /// <summary>
    /// Releases an owned attribute table.
    /// </summary>
    /// <param name="table">The owned table.</param>
    [DllImport(_glib, EntryPoint = "g_hash_table_unref")]
    private static extern void HashTableUnref(IntPtr table);

    /// <summary>
    /// Frees a native error without logging its payload.
    /// </summary>
    /// <param name="error">The owned error.</param>
    [DllImport(_glib, EntryPoint = "g_error_free")]
    private static extern void ErrorFree(IntPtr error);

    /// <summary>
    /// Creates a cancellable native request.
    /// </summary>
    /// <returns>The owned cancellation object.</returns>
    [DllImport(_gio, EntryPoint = "g_cancellable_new")]
    private static extern IntPtr CancellableNew();

    /// <summary>
    /// Requests cancellation of a native operation.
    /// </summary>
    /// <param name="cancellable">The cancellation object.</param>
    [DllImport(_gio, EntryPoint = "g_cancellable_cancel")]
    private static extern void CancellableCancel(IntPtr cancellable);

    /// <summary>
    /// Releases a native object reference.
    /// </summary>
    /// <param name="value">The owned object.</param>
    [DllImport(_gobject, EntryPoint = "g_object_unref")]
    private static extern void ObjectUnref(IntPtr value);
}
