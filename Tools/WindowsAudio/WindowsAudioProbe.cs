using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Rebellion.Diagnostics
{
    /// <summary>
    /// Compares Windows output formats without starting playback or changing endpoint settings.
    /// </summary>
    public static class WindowsAudioProbe
    {
        private const uint _eventCallback = 0x40000;
        private const long _bufferDuration = 1000000;
        private static readonly Guid _audioClient = new Guid(
            "1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"
        );

        /// <summary>
        /// Writes independent shared-mode checks for every active Windows render endpoint.
        /// </summary>
        /// <param name="reportPath">The absolute destination for the UTF-8 report.</param>
        public static void Collect(string reportPath)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("This diagnostic requires Windows.");

            using (
                StreamWriter report = new StreamWriter(reportPath, false, new UTF8Encoding(false))
            )
            {
                report.AutoFlush = true;
                report.WriteLine("Windows audio format check v2");
                report.WriteLine("UTC: " + DateTime.UtcNow.ToString("o"));
                report.WriteLine(
                    "OS: " + Environment.OSVersion + "; runtime: " + Environment.Version
                );
                report.WriteLine("64-bit process: " + Environment.Is64BitProcess);
                report.WriteLine("Independent checks; no Unity audio-client calls are captured.");
                report.WriteLine(
                    "Shared mode, 100 ms requested buffer; no playback or setting changes."
                );
                report.WriteLine(
                    "Matrix: 48 kHz, extensible format; 2/8 channels x float32/PCM32/PCM16."
                );
                report.WriteLine(
                    "Result 0 means success; result 1 means a closest format, not an exact match."
                );
                WriteGameLog(report);
                report.WriteLine("\nWindows format checks");
                int initialized = CoInitializeEx(IntPtr.Zero, 2);
                report.WriteLine("COM initialization: " + Result(initialized));
                Marshal.ThrowExceptionForHR(initialized);
                try
                {
                    CollectEndpoints(report);
                    report.WriteLine(
                        "Check complete. Compare this report with the failing game's Player.log."
                    );
                }
                catch (Exception error)
                {
                    report.WriteLine("Check failed: " + error);
                    throw;
                }
                finally
                {
                    CoUninitialize();
                }
            }
        }

        /// <summary>
        /// Includes the latest saved player log before any synchronous audio-driver checks.
        /// </summary>
        /// <param name="report">The report being written.</param>
        private static void WriteGameLog(StreamWriter report)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "LocalLow",
                "AdasGames",
                "Rebellion 2",
                "Player.log"
            );
            report.WriteLine("\nGame log from the last saved run");
            report.WriteLine("Path: " + path);
            try
            {
                using (
                    FileStream stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete
                    )
                )
                using (StreamReader reader = new StreamReader(stream))
                {
                    report.WriteLine(
                        "Last written UTC: " + File.GetLastWriteTimeUtc(path).ToString("o")
                    );
                    report.WriteLine("--- Begin Player.log ---");
                    char[] buffer = new char[4096];
                    int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                        report.Write(buffer, 0, count);
                    report.WriteLine("\n--- End Player.log ---");
                }
            }
            catch (IOException error)
            {
                report.WriteLine("Player.log unavailable: " + error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                report.WriteLine("Player.log unavailable: " + error.Message);
            }
        }

        /// <summary>
        /// Records default roles and inspects each active output on the same COM apartment.
        /// </summary>
        /// <param name="report">The report being written.</param>
        private static void CollectEndpoints(StreamWriter report)
        {
            Guid classId = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            Guid interfaceId = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
            IntPtr enumerator;
            Marshal.ThrowExceptionForHR(
                CoCreateInstance(ref classId, IntPtr.Zero, 23, ref interfaceId, out enumerator)
            );
            IntPtr devices = IntPtr.Zero;
            try
            {
                string[] roles = { "console", "multimedia", "communications" };
                for (int role = 0; role < roles.Length; role++)
                {
                    IntPtr device;
                    int result = Method<DefaultEndpoint>(enumerator, 4)(
                        enumerator,
                        0,
                        role,
                        out device
                    );
                    try
                    {
                        report.WriteLine("Default " + roles[role] + ": " + Result(result));
                        if (result >= 0)
                            report.WriteLine("  " + EndpointId(device));
                    }
                    finally
                    {
                        Release(device);
                    }
                }
                Marshal.ThrowExceptionForHR(
                    Method<Endpoints>(enumerator, 3)(enumerator, 0, 1, out devices)
                );
                uint count;
                Marshal.ThrowExceptionForHR(Method<Count>(devices, 3)(devices, out count));
                report.WriteLine("Active output endpoints: " + count);
                for (uint index = 0; index < count; index++)
                {
                    IntPtr device;
                    Marshal.ThrowExceptionForHR(
                        Method<Item>(devices, 4)(devices, index, out device)
                    );
                    try
                    {
                        InspectEndpoint(device, report);
                    }
                    catch (Exception error)
                    {
                        report.WriteLine("Endpoint check failed: " + error);
                    }
                    finally
                    {
                        Release(device);
                    }
                }
            }
            finally
            {
                Release(devices);
                Release(enumerator);
            }
        }

        /// <summary>
        /// Checks the untouched native format on its original client and on fresh clients.
        /// </summary>
        /// <param name="device">The endpoint used for all checks in this section.</param>
        /// <param name="report">The report being written.</param>
        private static void InspectEndpoint(IntPtr device, StreamWriter report)
        {
            report.WriteLine("\nEndpoint: " + EndpointId(device));
            WriteName(device, report);
            IntPtr client = Activate(device);
            IntPtr native = IntPtr.Zero;
            try
            {
                int result = Method<MixFormat>(client, 8)(client, out native);
                report.WriteLine("GetMixFormat: " + Result(result));
                if (result >= 0 && native != IntPtr.Zero)
                {
                    report.WriteLine("Native: " + Describe(native));
                    result = Method<Initialize>(client, 3)(
                        client,
                        0,
                        0,
                        _bufferDuration,
                        0,
                        native,
                        IntPtr.Zero
                    );
                    report.WriteLine("Initialize native on same client: " + Result(result));
                    Release(client);
                    client = IntPtr.Zero;
                    InspectFormat(
                        device,
                        native,
                        "Unchanged native format on fresh clients",
                        report
                    );
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(native);
                Release(client);
            }

            foreach (ushort channels in new ushort[] { 2, 8 })
            {
                foreach (string encoding in new string[] { "float32", "PCM32", "PCM16" })
                {
                    IntPtr format = CreateFormat(channels, encoding);
                    try
                    {
                        InspectFormat(
                            device,
                            format,
                            "48 kHz, " + channels + " channels, " + encoding,
                            report
                        );
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(format);
                    }
                }
            }
        }

        /// <summary>
        /// Records support, opens any closest alternative, and tests both native callback modes.
        /// </summary>
        /// <param name="device">The endpoint being inspected.</param>
        /// <param name="format">The caller-owned requested format.</param>
        /// <param name="label">The case description.</param>
        /// <param name="report">The report being written.</param>
        private static void InspectFormat(
            IntPtr device,
            IntPtr format,
            string label,
            StreamWriter report
        )
        {
            report.WriteLine("\nCase: " + label);
            report.WriteLine("  " + Describe(format));
            IntPtr client = Activate(device);
            IntPtr closest = IntPtr.Zero;
            try
            {
                int result = Method<FormatSupport>(client, 7)(client, 0, format, out closest);
                report.WriteLine("  IsFormatSupported: " + Result(result));
                if (closest != IntPtr.Zero)
                {
                    report.WriteLine("  Closest: " + Describe(closest));
                    Open(device, closest, 0, "closest", report);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(closest);
                Release(client);
            }
            Open(device, format, 0, "requested", report);
            Open(device, format, _eventCallback, "requested", report);
        }

        /// <summary>
        /// Opens and releases one fresh shared client without calling Start.
        /// </summary>
        /// <param name="device">The exact endpoint for this attempt.</param>
        /// <param name="format">The caller-owned format.</param>
        /// <param name="flags">Zero or the event callback flag.</param>
        /// <param name="label">Whether the request is exact or a closest alternative.</param>
        /// <param name="report">The report being written.</param>
        private static void Open(
            IntPtr device,
            IntPtr format,
            uint flags,
            string label,
            StreamWriter report
        )
        {
            IntPtr client = Activate(device);
            try
            {
                int result = Method<Initialize>(client, 3)(
                    client,
                    0,
                    flags,
                    _bufferDuration,
                    0,
                    format,
                    IntPtr.Zero
                );
                report.WriteLine(
                    "  Initialize "
                        + label
                        + " flags=0x"
                        + flags.ToString("X8")
                        + ": "
                        + Result(result)
                );
            }
            finally
            {
                Release(client);
            }
        }

        /// <summary>
        /// Creates one packed Windows extensible format for a controlled comparison.
        /// </summary>
        /// <param name="channels">Two stereo channels or eight surround channels.</param>
        /// <param name="encoding">Float32, PCM32 or PCM16 from the fixed diagnostic matrix.</param>
        /// <returns>A COM-task allocation owned by the caller.</returns>
        private static IntPtr CreateFormat(ushort channels, string encoding)
        {
            ushort bits = (ushort)(encoding == "PCM16" ? 16 : 32);
            ushort alignment = (ushort)(channels * bits / 8);
            byte[] bytes;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0xfffe);
                writer.Write(channels);
                writer.Write(48000);
                writer.Write(48000 * alignment);
                writer.Write(alignment);
                writer.Write(bits);
                writer.Write((ushort)22);
                writer.Write(bits);
                writer.Write(channels == 2 ? 3 : 0x63f);
                writer.Write(
                    new Guid(
                        encoding == "float32"
                            ? "00000003-0000-0010-8000-00AA00389B71"
                            : "00000001-0000-0010-8000-00AA00389B71"
                    ).ToByteArray()
                );
                bytes = stream.ToArray();
            }
            IntPtr format = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, format, bytes.Length);
            return format;
        }

        /// <summary>
        /// Records every format field and the exact bytes submitted to Windows.
        /// </summary>
        /// <param name="format">A live Windows format allocation.</param>
        /// <returns>The fields and hexadecimal representation.</returns>
        private static string Describe(IntPtr format)
        {
            ushort tag = (ushort)Marshal.ReadInt16(format, 0);
            ushort extra = (ushort)Marshal.ReadInt16(format, 16);
            string text =
                "rate="
                + Marshal.ReadInt32(format, 4)
                + " channels="
                + Marshal.ReadInt16(format, 2)
                + " bits="
                + Marshal.ReadInt16(format, 14)
                + " tag=0x"
                + tag.ToString("X4")
                + " blockAlign="
                + Marshal.ReadInt16(format, 12)
                + " bytesPerSecond="
                + Marshal.ReadInt32(format, 8)
                + " cbSize="
                + extra;
            if (tag == 0xfffe && extra >= 22)
            {
                byte[] guid = new byte[16];
                Marshal.Copy(IntPtr.Add(format, 24), guid, 0, guid.Length);
                text +=
                    " validBits="
                    + Marshal.ReadInt16(format, 18)
                    + " mask=0x"
                    + Marshal.ReadInt32(format, 20).ToString("X8")
                    + " subtype="
                    + new Guid(guid);
            }
            byte[] bytes = new byte[18 + extra];
            Marshal.Copy(format, bytes, 0, bytes.Length);
            return text + "\n  bytes=" + BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Copies an endpoint identifier before releasing its native string allocation.
        /// </summary>
        /// <param name="device">The render endpoint.</param>
        /// <returns>The Windows endpoint identifier.</returns>
        private static string EndpointId(IntPtr device)
        {
            IntPtr id;
            Marshal.ThrowExceptionForHR(Method<GetString>(device, 5)(device, out id));
            try
            {
                return Marshal.PtrToStringUni(id);
            }
            finally
            {
                Marshal.FreeCoTaskMem(id);
            }
        }

        /// <summary>
        /// Reads the endpoint display name from its property store without writing properties.
        /// </summary>
        /// <param name="device">The render endpoint.</param>
        /// <param name="report">The report being written.</param>
        private static void WriteName(IntPtr device, StreamWriter report)
        {
            IntPtr properties;
            int result = Method<PropertyStore>(device, 4)(device, 0, out properties);
            if (result < 0)
            {
                report.WriteLine("Name unavailable: " + Result(result));
                return;
            }
            try
            {
                PropertyKey key = new PropertyKey
                {
                    FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
                    Id = 14,
                };
                PropertyValue value = new PropertyValue();
                result = Method<GetProperty>(properties, 5)(properties, ref key, ref value);
                try
                {
                    report.WriteLine(
                        result >= 0 && value.Type == 31
                            ? "Name: " + Marshal.PtrToStringUni(value.Pointer)
                            : "Name unavailable: " + Result(result)
                    );
                }
                finally
                {
                    PropVariantClear(ref value);
                }
            }
            finally
            {
                Release(properties);
            }
        }

        /// <summary>
        /// Acquires a fresh audio client on an existing endpoint.
        /// </summary>
        /// <param name="device">The endpoint that owns the client.</param>
        /// <returns>The caller-owned COM interface pointer.</returns>
        private static IntPtr Activate(IntPtr device)
        {
            Guid interfaceId = _audioClient;
            IntPtr client;
            Marshal.ThrowExceptionForHR(
                Method<ActivateClient>(device, 3)(
                    device,
                    ref interfaceId,
                    23,
                    IntPtr.Zero,
                    out client
                )
            );
            return client;
        }

        /// <summary>
        /// Reads one Windows SDK interface slot without creating a managed COM wrapper.
        /// </summary>
        /// <typeparam name="T">The documented native method signature.</typeparam>
        /// <param name="instance">The live COM interface.</param>
        /// <param name="slot">The SDK method position, including IUnknown.</param>
        /// <returns>The native method delegate.</returns>
        private static T Method<T>(IntPtr instance, int slot)
            where T : class
        {
            return (T)
                (object)
                    Marshal.GetDelegateForFunctionPointer(
                        Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size),
                        typeof(T)
                    );
        }

        /// <summary>
        /// Preserves the raw Windows status without interpreting a generic audio error as a cause.
        /// </summary>
        /// <param name="status">The native HRESULT.</param>
        /// <returns>The hexadecimal status.</returns>
        private static string Result(int status)
        {
            return "0x" + unchecked((uint)status).ToString("X8");
        }

        /// <summary>
        /// Releases one acquired native interface reference.
        /// </summary>
        /// <param name="value">The owned interface pointer, or zero.</param>
        private static void Release(IntPtr value)
        {
            if (value != IntPtr.Zero)
                Marshal.Release(value);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public uint Id;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropertyValue
        {
            [FieldOffset(0)]
            public ushort Type;

            [FieldOffset(8)]
            public IntPtr Pointer;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int Endpoints(IntPtr self, int flow, uint states, out IntPtr collection);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DefaultEndpoint(IntPtr self, int flow, int role, out IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int Count(IntPtr self, out uint count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int Item(IntPtr self, uint index, out IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetString(IntPtr self, out IntPtr text);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PropertyStore(IntPtr self, uint access, out IntPtr properties);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetProperty(IntPtr self, ref PropertyKey key, ref PropertyValue value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ActivateClient(
            IntPtr self,
            ref Guid iid,
            uint context,
            IntPtr parameters,
            out IntPtr instance
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MixFormat(IntPtr self, out IntPtr format);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int FormatSupport(
            IntPtr self,
            int mode,
            IntPtr format,
            out IntPtr closest
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int Initialize(
            IntPtr self,
            int mode,
            uint flags,
            long duration,
            long period,
            IntPtr format,
            IntPtr session
        );

        /// <summary>
        /// Initializes the Windows COM apartment for synchronous device inspection.
        /// </summary>
        /// <param name="reserved">The required zero pointer.</param>
        /// <param name="flags">The apartment initialization mode.</param>
        /// <returns>The native HRESULT.</returns>
        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr reserved, uint flags);

        /// <summary>
        /// Balances this diagnostic's successful COM initialization.
        /// </summary>
        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        /// <summary>
        /// Creates the Windows endpoint enumerator using its documented class and interface.
        /// </summary>
        /// <param name="classId">The enumerator class identifier.</param>
        /// <param name="outer">The required zero outer object.</param>
        /// <param name="context">The COM activation context.</param>
        /// <param name="interfaceId">The requested enumerator interface.</param>
        /// <param name="instance">The owned native interface.</param>
        /// <returns>The native HRESULT.</returns>
        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(
            ref Guid classId,
            IntPtr outer,
            uint context,
            ref Guid interfaceId,
            out IntPtr instance
        );

        /// <summary>
        /// Releases the allocation held by a native endpoint property value.
        /// </summary>
        /// <param name="value">The property value to clear.</param>
        /// <returns>The native HRESULT.</returns>
        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropertyValue value);
    }
}
