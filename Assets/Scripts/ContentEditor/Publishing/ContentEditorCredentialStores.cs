using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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

/// <summary>
/// Stores publishing credentials as generic password items in macOS Keychain.
/// </summary>
internal sealed class ContentEditorMacCredentialStore : ContentEditorCredentialStore
{
    private const string _security = "/System/Library/Frameworks/Security.framework/Security";
    private const string _foundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private static readonly IntPtr _securityHandle = OpenFramework(_security);
    private static readonly IntPtr _foundationHandle = OpenFramework(_foundation);
    private static readonly IntPtr _keyCallbacks = Symbol(
        _foundationHandle,
        "kCFTypeDictionaryKeyCallBacks"
    );
    private static readonly IntPtr _valueCallbacks = Symbol(
        _foundationHandle,
        "kCFTypeDictionaryValueCallBacks"
    );

    /// <summary>
    /// Identifies this application's Keychain item.
    /// </summary>
    /// <param name="key">The application-owned service name.</param>
    internal ContentEditorMacCredentialStore(string key)
        : base(key) { }

    /// <summary>
    /// Reads a saved token, allowing Keychain to request access or an unlock.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The saved token, or null when no item exists.</returns>
    internal override string Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MacDictionary query = CreateQuery();
        query.Set("kSecReturnData", Constant(_foundationHandle, "kCFBooleanTrue"));
        int status = SecItemCopyMatching(query.Handle, out IntPtr data);
        if (status == -25300)
            return null;
        if (status != 0)
            throw Failure("read", status);
        try
        {
            int length = checked((int)CFDataGetLength(data).ToInt64());
            byte[] bytes = new byte[length];
            try
            {
                Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }
        finally
        {
            CFRelease(data);
        }
    }

    /// <summary>
    /// Updates an existing password atomically, or creates its first Keychain item.
    /// </summary>
    /// <param name="credential">The validated token.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal override void Write(string credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MacDictionary query = CreateQuery();
        using MacDictionary changes = new();
        changes.SetData(credential);
        int status = SecItemUpdate(query.Handle, changes.Handle);
        if (status == -25300)
        {
            query.SetData(credential);
            query.SetText("kSecAttrLabel", _label);
            status = SecItemAdd(query.Handle, IntPtr.Zero);
            if (status == -25299)
            {
                using MacDictionary match = CreateQuery();
                status = SecItemUpdate(match.Handle, changes.Handle);
            }
        }
        if (status != 0)
            throw Failure("save", status);
    }

    /// <summary>
    /// Removes only this application's saved publishing password.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    internal override void Delete(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MacDictionary query = CreateQuery();
        int status = SecItemDelete(query.Handle);
        if (status != 0 && status != -25300)
            throw Failure("remove", status);
    }

    /// <summary>
    /// Restricts all lookups and mutations to this service and publishing account.
    /// </summary>
    /// <returns>The owned matching dictionary.</returns>
    private MacDictionary CreateQuery()
    {
        MacDictionary query = new();
        try
        {
            query.Set("kSecClass", Constant(_securityHandle, "kSecClassGenericPassword"));
            query.SetText("kSecAttrService", key);
            query.SetText("kSecAttrAccount", "publishing");
            return query;
        }
        catch
        {
            query.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a system framework for access to its exported attribute constants.
    /// </summary>
    /// <param name="path">The system framework path.</param>
    /// <returns>The process-lifetime framework handle.</returns>
    private static IntPtr OpenFramework(string path)
    {
        IntPtr handle = dlopen(path, 1);
        return handle != IntPtr.Zero ? handle : throw Failure("open", -1);
    }

    /// <summary>
    /// Reads an exported Core Foundation object pointer without copying or owning it.
    /// </summary>
    /// <param name="library">The framework containing the constant.</param>
    /// <param name="name">The exported constant name.</param>
    /// <returns>The borrowed object pointer.</returns>
    private static IntPtr Constant(IntPtr library, string name)
    {
        return Marshal.ReadIntPtr(Symbol(library, name));
    }

    /// <summary>
    /// Finds a required exported constant or callback table in a system framework.
    /// </summary>
    /// <param name="library">The containing framework.</param>
    /// <param name="name">The symbol name.</param>
    /// <returns>The borrowed symbol address.</returns>
    private static IntPtr Symbol(IntPtr library, string name)
    {
        IntPtr symbol = dlsym(library, name);
        return symbol != IntPtr.Zero ? symbol : throw Failure("open", -1);
    }

    /// <summary>
    /// Owns the dictionary and temporary values passed to a single Keychain operation.
    /// </summary>
    private sealed class MacDictionary : IDisposable
    {
        private readonly List<IntPtr> values = new();
        internal IntPtr Handle { get; private set; }

        /// <summary>
        /// Creates a dictionary whose temporary objects remain owned by this scope.
        /// </summary>
        internal MacDictionary()
        {
            Handle = CFDictionaryCreateMutable(
                IntPtr.Zero,
                IntPtr.Zero,
                _keyCallbacks,
                _valueCallbacks
            );
            if (Handle == IntPtr.Zero)
                throw Failure("open", -1);
        }

        /// <summary>
        /// Adds a borrowed Keychain attribute value.
        /// </summary>
        /// <param name="name">The exported attribute name.</param>
        /// <param name="value">The value retained by the current operation.</param>
        internal void Set(string name, IntPtr value) =>
            CFDictionarySetValue(Handle, Constant(_securityHandle, name), value);

        /// <summary>
        /// Adds an owned textual attribute to this operation.
        /// </summary>
        /// <param name="name">The exported attribute name.</param>
        /// <param name="text">The attribute text.</param>
        internal void SetText(string name, string text)
        {
            IntPtr value = CFStringCreateWithCString(IntPtr.Zero, text, 0x08000100);
            if (value == IntPtr.Zero)
                throw Failure("prepare", -1);
            values.Add(value);
            Set(name, value);
        }

        /// <summary>
        /// Adds password data and clears its temporary managed encoding.
        /// </summary>
        /// <param name="credential">The token to pass to Keychain.</param>
        internal void SetData(string credential)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(credential);
            try
            {
                IntPtr value = CFDataCreate(IntPtr.Zero, bytes, new IntPtr(bytes.Length));
                if (value == IntPtr.Zero)
                    throw Failure("prepare", -1);
                values.Add(value);
                Set("kSecValueData", value);
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Releases all native objects created for this operation.
        /// </summary>
        public void Dispose()
        {
            if (Handle == IntPtr.Zero)
                return;
            CFRelease(Handle);
            Handle = IntPtr.Zero;
            foreach (IntPtr value in values)
                CFRelease(value);
            values.Clear();
        }
    }

    /// <summary>
    /// Loads a system framework into this process.
    /// </summary>
    /// <param name="path">The framework path.</param>
    /// <param name="flags">The loader options.</param>
    /// <returns>The framework handle.</returns>
    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlopen(string path, int flags);

    /// <summary>
    /// Finds one exported framework symbol.
    /// </summary>
    /// <param name="handle">The framework handle.</param>
    /// <param name="name">The symbol name.</param>
    /// <returns>The symbol address.</returns>
    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlsym(IntPtr handle, string name);

    /// <summary>
    /// Creates a dictionary with explicit ownership of its keys and values.
    /// </summary>
    /// <param name="allocator">The allocation policy.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <param name="keys">The key ownership callbacks.</param>
    /// <param name="values">The value ownership callbacks.</param>
    /// <returns>The owned dictionary.</returns>
    [DllImport(_foundation)]
    private static extern IntPtr CFDictionaryCreateMutable(
        IntPtr allocator,
        IntPtr capacity,
        IntPtr keys,
        IntPtr values
    );

    /// <summary>
    /// Adds an attribute to a mutable dictionary.
    /// </summary>
    /// <param name="dictionary">The destination dictionary.</param>
    /// <param name="key">The borrowed attribute key.</param>
    /// <param name="value">The borrowed attribute value.</param>
    [DllImport(_foundation)]
    private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);

    /// <summary>
    /// Copies text into a Core Foundation string.
    /// </summary>
    /// <param name="allocator">The allocation policy.</param>
    /// <param name="text">The UTF-8 text.</param>
    /// <param name="encoding">The text encoding identifier.</param>
    /// <returns>The owned string.</returns>
    [DllImport(_foundation)]
    private static extern IntPtr CFStringCreateWithCString(
        IntPtr allocator,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        uint encoding
    );

    /// <summary>
    /// Copies password bytes into a Core Foundation data object.
    /// </summary>
    /// <param name="allocator">The allocation policy.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="length">The byte count.</param>
    /// <returns>The owned data object.</returns>
    [DllImport(_foundation)]
    private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, IntPtr length);

    /// <summary>
    /// Reads a data object's byte count.
    /// </summary>
    /// <param name="data">The data object.</param>
    /// <returns>The byte count.</returns>
    [DllImport(_foundation)]
    private static extern IntPtr CFDataGetLength(IntPtr data);

    /// <summary>
    /// Borrows the bytes of a data object until that object is released.
    /// </summary>
    /// <param name="data">The data object.</param>
    /// <returns>The borrowed byte pointer.</returns>
    [DllImport(_foundation)]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    /// <summary>
    /// Releases one owned Core Foundation object.
    /// </summary>
    /// <param name="value">The owned object.</param>
    [DllImport(_foundation)]
    private static extern void CFRelease(IntPtr value);

    /// <summary>
    /// Finds a Keychain item matching the supplied attributes.
    /// </summary>
    /// <param name="query">The matching attributes.</param>
    /// <param name="result">Receives the requested data object.</param>
    /// <returns>The system status code.</returns>
    [DllImport(_security)]
    private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

    /// <summary>
    /// Updates only the matching Keychain item's supplied attributes.
    /// </summary>
    /// <param name="query">The matching attributes.</param>
    /// <param name="changes">The replacement attributes.</param>
    /// <returns>The system status code.</returns>
    [DllImport(_security)]
    private static extern int SecItemUpdate(IntPtr query, IntPtr changes);

    /// <summary>
    /// Creates a Keychain item using the supplied attributes.
    /// </summary>
    /// <param name="attributes">The initial item attributes.</param>
    /// <param name="result">The optional result pointer.</param>
    /// <returns>The system status code.</returns>
    [DllImport(_security)]
    private static extern int SecItemAdd(IntPtr attributes, IntPtr result);

    /// <summary>
    /// Removes a Keychain item matching the supplied attributes.
    /// </summary>
    /// <param name="query">The matching attributes.</param>
    /// <returns>The system status code.</returns>
    [DllImport(_security)]
    private static extern int SecItemDelete(IntPtr query);
}

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
