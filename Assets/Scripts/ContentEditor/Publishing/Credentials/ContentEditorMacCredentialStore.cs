using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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
                status = SecItemUpdate(query.Handle, changes.Handle);
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
