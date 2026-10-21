using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace TND.FrameGen.DLSS
{
    public class DLSSFrameGenProvider: FrameGenProviderBase
    {
        private const int NvidiaVendorID = 0x10DE;

        public override string DisplayName => "DLSS 4.5";
        public override bool IsSupported { get { try { return SystemInfo.graphicsDeviceVendorID == NvidiaVendorID && FrameGenPlugin.IsAvailable && NativeD3D.IsFrameGenSupported() != 0; } catch (DllNotFoundException) { return false; } } }
        public override IntPtr NativeObject { get { try { return NativeD3D.GetFrameGenProvider(); } catch (DllNotFoundException) { return IntPtr.Zero; } } }
        public override ulong UniqueId => 0x4E56444C53534647u;      // "NVDLSSFG" in big-endian hex

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void RegisterFrameGenProvider()
        {
            RegisterFrameGenProvider<DLSSFrameGenProvider>();
        }

        private static class NativeD3D
        {
            private const string PluginName = "DLSSFrameGenPluginD3D";

            [DllImport(PluginName, EntryPoint = "FGUP_GetFrameGenProvider")]
            public static extern IntPtr GetFrameGenProvider();

            [DllImport(PluginName, EntryPoint = "FGUP_IsFrameGenSupported")]
            public static extern int IsFrameGenSupported();
        }
    }
}
