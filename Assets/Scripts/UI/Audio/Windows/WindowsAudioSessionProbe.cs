#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Inspects the current process's existing render sessions without opening a stream.
/// </summary>
internal static class WindowsAudioSessionProbe
{
    /// <summary>
    /// Checks whether Windows has a non-expired audio session owned by this process.
    /// </summary>
    /// <returns>True for an existing session, false for none, or null when inspection fails.</returns>
    public static bool? HasOutputSession()
    {
        // Wine 10's session enumerator creates expired wrappers for default-session GUIDs.
        // An absent session there cannot establish that Unity's output failed.
        if (GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero)
            return null;
        int initialized = CoInitializeEx(IntPtr.Zero, 0);
        bool ownsInitialization = initialized >= 0;
        if (!ownsInitialization)
            return null;
        try
        {
            Guid clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            Guid iid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
            Check(CoCreateInstance(ref clsid, IntPtr.Zero, 23, ref iid, out IntPtr enumerator));
            using ComReference enumeratorReference = new ComReference(enumerator);
            Check(Method<Endpoints>(enumerator, 3)(enumerator, 0, 1, out IntPtr collection));
            using ComReference collectionReference = new ComReference(collection);
            Check(Method<Unsigned>(collection, 3)(collection, out uint count));
            uint processId = GetCurrentProcessId();
            for (uint index = 0; index < count; index++)
            {
                Check(Method<Item>(collection, 4)(collection, index, out IntPtr device));
                using ComReference deviceReference = new ComReference(device);
                if (HasProcessSession(device, processId))
                    return true;
            }
            return false;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (ownsInitialization)
                CoUninitialize();
        }
    }

    /// <summary>
    /// Checks existing sessions on one active render endpoint without creating a playback client.
    /// </summary>
    /// <param name="device">The active render endpoint.</param>
    /// <param name="processId">The game's Windows process identifier.</param>
    /// <returns>True when a non-expired session belongs to this process.</returns>
    private static bool HasProcessSession(IntPtr device, uint processId)
    {
        Guid managerId = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        Check(
            Method<Activate>(device, 3)(device, ref managerId, 23, IntPtr.Zero, out IntPtr manager)
        );
        using ComReference managerReference = new ComReference(manager);
        Check(Method<Interface>(manager, 5)(manager, out IntPtr sessions));
        using ComReference sessionsReference = new ComReference(sessions);
        Check(Method<Integer>(sessions, 3)(sessions, out int total));
        for (int position = 0; position < total; position++)
        {
            Check(Method<Session>(sessions, 4)(sessions, position, out IntPtr control));
            using ComReference controlReference = new ComReference(control);
            Check(Method<Integer>(control, 3)(control, out int state));
            if (state == 2)
                continue;
            Guid controlId = new Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
            Check(Method<Query>(control, 0)(control, ref controlId, out IntPtr extended));
            using ComReference extendedReference = new ComReference(extended);
            Check(Method<Unsigned>(extended, 14)(extended, out uint sessionProcess));
            if (sessionProcess == processId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reads one inspected Core Audio interface method.
    /// </summary>
    /// <typeparam name="T">The native method signature.</typeparam>
    /// <param name="instance">The native interface pointer.</param>
    /// <param name="slot">The method index from the Windows interface definition.</param>
    /// <returns>The callable native method.</returns>
    private static T Method<T>(IntPtr instance, int slot)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size)
        );

    /// <summary>
    /// Rejects a failed native HRESULT.
    /// </summary>
    /// <param name="result">The native HRESULT.</param>
    private static void Check(int result)
    {
        if (result < 0)
            Marshal.ThrowExceptionForHR(result);
    }

    /// <summary>
    /// Releases a Core Audio reference on the thread that acquired it.
    /// </summary>
    private sealed class ComReference : IDisposable
    {
        private readonly IntPtr value;

        /// <summary>
        /// Takes ownership of one acquired native reference.
        /// </summary>
        /// <param name="value">The acquired reference.</param>
        public ComReference(IntPtr value) => this.value = value;

        /// <summary>
        /// Releases the owned native reference.
        /// </summary>
        public void Dispose()
        {
            if (value != IntPtr.Zero)
                Method<Release>(value, 2)(value);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Endpoints(IntPtr self, int flow, uint states, out IntPtr collection);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Item(IntPtr self, uint index, out IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Session(IntPtr self, int index, out IntPtr control);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Activate(
        IntPtr self,
        ref Guid iid,
        uint context,
        IntPtr parameters,
        out IntPtr instance
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Query(IntPtr self, ref Guid iid, out IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Interface(IntPtr self, out IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Integer(IntPtr self, out int value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Unsigned(IntPtr self, out uint value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Release(IntPtr self);

    /// <summary>
    /// Finds an already loaded system module.
    /// </summary>
    /// <param name="name">The system module name.</param>
    /// <returns>The loaded module handle.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string name);

    /// <summary>
    /// Checks whether the host exposes the Wine identification export.
    /// </summary>
    /// <param name="module">The loaded system module.</param>
    /// <param name="name">The exported function name.</param>
    /// <returns>The export address or zero when absent.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    /// <summary>
    /// Initializes COM for native session inspection.
    /// </summary>
    /// <param name="reserved">The required null reserved pointer.</param>
    /// <param name="flags">The COM apartment mode.</param>
    /// <returns>The COM initialization result.</returns>
    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint flags);

    /// <summary>
    /// Releases the initialization owned by this query.
    /// </summary>
    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    /// <summary>
    /// Creates the Windows device enumerator.
    /// </summary>
    /// <param name="clsid">The device enumerator class.</param>
    /// <param name="outer">The required null outer reference.</param>
    /// <param name="context">The supported class context.</param>
    /// <param name="iid">The requested enumerator interface.</param>
    /// <param name="instance">The created interface pointer.</param>
    /// <returns>The COM activation result.</returns>
    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid clsid,
        IntPtr outer,
        uint context,
        ref Guid iid,
        out IntPtr instance
    );

    /// <summary>
    /// Identifies this Windows process without inspecting other processes.
    /// </summary>
    /// <returns>The current Windows process identifier.</returns>
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();
}
#endif
