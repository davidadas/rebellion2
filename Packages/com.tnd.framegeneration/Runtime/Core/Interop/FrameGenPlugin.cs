using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_VR && ENABLE_VR_MODULE
using UnityEngine.XR;
#endif
using TND.Common.Native;

namespace TND.FrameGen
{
    public static class FrameGenPlugin
    {
        private enum PluginEvent
        {
            PrepareFrameGen,
            ConfigureFrameGen,

            BeforeRendering,
            AfterRendering,
        }

        private enum TextureSlot
        {
            Depth,
            MotionVectors,
            HudLess,
            DistortionField,
        }

        [Flags]
        public enum RenderFlags
        {
            HighDynamicRange = 1 << 0,
            InvertedDepth = 1 << 1,
        }

        [Flags]
        public enum DebugFlags
        {
            DebugLines = 1 << 0,
            DebugView = 1 << 1,
            ShowOnlyGeneratedFrames = 1 << 2,
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct ConfigurationParams
        {
            public uint enable;
            public uint useAsync;
            public ulong frameNumber;
            public RenderFlags renderFlags;
            public DebugFlags debugFlags;
            public int viewportLeft;
            public int viewportTop;
            public int viewportWidth;
            public int viewportHeight;
            public uint numFramesToGenerate;
            public uint useDynamicMFG;
            public uint dynamicFramerateTarget;
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct PrepareParams
        {
            public ulong frameNumber;
            public uint renderWidth;
            public uint renderHeight;
            public float jitterOffsetX;
            public float jitterOffsetY;
            public float motionVectorScaleX;
            public float motionVectorScaleY;
            public float frameTimeDeltaMS;
            public float cameraNear;
            public float cameraFar;
            public float cameraFov;
            public Matrix4x4 cameraViewMatrix;
            public Matrix4x4 cameraProjMatrixNoJitter;
            public Vector3 cameraPosition;
            public uint reset;
        }

        public struct TextureTable
        {
            public Texture depth;
            public Texture motionVectors;
            public Texture hudLess;
            public Texture distortionField;
        }

        public struct FGVersion
        {
            public uint major;
            public uint minor;
            public uint patch;
        }

        public static bool IsAvailable => IsSupported && s_baseEventId >= 0;
        public static ulong ActiveProviderId => IsAvailable ? NativeD3D.GetActiveProviderId() : ulong.MaxValue;
        public static bool RequiresYFlip => IsAvailable && NativeD3D.GetRequiresYFlip() != 0;
        public static uint MaxInterpolatedFrames => IsAvailable ? NativeD3D.GetMaxInterpolatedFrames() : 1u;
        public static bool SupportsDynamicMFG => IsAvailable && NativeD3D.GetSupportsDynamicMFG() != 0;

        public static FGVersion TechVersion
        {
            get
            {
                if (IsAvailable)
                {
                    NativeD3D.GetTechVersion(out FGVersion version);
                    return version;
                }

                return default;
            }
        }

        private static bool IsSupported => Application.isPlaying && !Application.isEditor && !IsXRDeviceActive && SystemInfo.graphicsDeviceType is GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12;

        public static bool IsXRDeviceActive
        {
            get
            {
#if ENABLE_VR && ENABLE_VR_MODULE
                if (XRSettings.enabled)
                    return XRSettings.isDeviceActive;
#endif
                return false;
            }
        }

        private static int s_baseEventId;
        private static readonly NativeStructs<PrepareParams> NativePrepareParams = new();
        private static readonly NativeStructs<ConfigurationParams> NativeConfigParams = new();

        static FrameGenPlugin()
        {
            if (!IsSupported)
            {
                s_baseEventId = -1;
                return;
            }

            try
            {
                // This does an initial check whether the core FG plugin is present and loaded
                s_baseEventId = NativeD3D.GetBaseEventId();
            }
            catch (DllNotFoundException)
            {
                s_baseEventId = -1;
            }
        }

        public static ulong RegisterProvider(IntPtr providerPtr)
        {
            if (!IsAvailable || providerPtr == IntPtr.Zero)
            {
                return ulong.MaxValue;
            }

            return NativeD3D.RegisterFrameGenProvider(providerPtr);
        }

        public struct SwitchFrameGenProviderScope : IDisposable
        {
            public bool Result { get; private set; }

            public SwitchFrameGenProviderScope(ulong providerId)
            {
                if (!IsAvailable)
                {
                    Result = false;
                    return;
                }

                Result = NativeD3D.BeginSwitchFrameGenProvider(providerId) > 0;
            }

            public void Dispose()
            {
                if (!IsAvailable)
                {
                    Result = false;
                    return;
                }

                Result = NativeD3D.EndSwitchFrameGenProvider() > 0;
            }
        }

        public static void OnFrameStart()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeD3D.OnFrameStart((ulong)Time.frameCount);
            }
            catch (EntryPointNotFoundException)
            {
                // Older plugin versions may not have this function yet, and those do not need it to be called either, so just ignore
            }
        }

        public static void OnFrameEnd()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeD3D.OnFrameEnd((ulong)Time.frameCount);
            }
            catch (EntryPointNotFoundException)
            {
                // Older plugin versions may not have this function yet, and those do not need it to be called either, so just ignore
            }
        }

        public static void OnBeforeInputPolling()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeD3D.OnBeforeInputPolling((ulong)Time.frameCount);
            }
            catch (EntryPointNotFoundException)
            {
                // Older plugin versions may not have this function yet, and those do not need it to be called either, so just ignore
            }
        }

        public static void OnBeforeSimulation()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeD3D.OnBeforeSimulation((ulong)Time.frameCount);
            }
            catch (EntryPointNotFoundException)
            {
                // Older plugin versions may not have this function yet, and those do not need it to be called either, so just ignore
            }
        }

        public static void OnAfterSimulation()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeD3D.OnAfterSimulation((ulong)Time.frameCount);
            }
            catch (EntryPointNotFoundException)
            {
                // Older plugin versions may not have this function yet, and those do not need it to be called either, so just ignore
            }
        }

        public static void OnBeforeRendering(CommandBuffer cmd)
        {
            if (!IsAvailable)
            {
                return;
            }

            cmd.IssuePluginEventAndData(NativeD3D.GetRenderEventCallback(), GetEventId(PluginEvent.BeforeRendering), new IntPtr((long)Time.frameCount));
        }

        public static void OnAfterRendering(CommandBuffer cmd)
        {
            if (!IsAvailable)
            {
                return;
            }

            cmd.IssuePluginEventAndData(NativeD3D.GetRenderEventCallback(), GetEventId(PluginEvent.AfterRendering), new IntPtr((long)Time.frameCount));
        }

        public static void Prepare(CommandBuffer cmd, ref PrepareParams prepareParams, in TextureTable textures)
        {
            if (!IsAvailable)
            {
                return;
            }

            IntPtr textureCallback = NativeD3D.GetSetTextureEventCallback();
            SetTexture(cmd, textureCallback, TextureSlot.Depth, textures.depth, true);
            SetTexture(cmd, textureCallback, TextureSlot.MotionVectors, textures.motionVectors);

            NativePrepareParams.SetData(ref prepareParams);
            cmd.IssuePluginEventAndData(NativeD3D.GetRenderEventCallback(), GetEventId(PluginEvent.PrepareFrameGen), NativePrepareParams.GetPointer());
        }

        public static void Configure(CommandBuffer cmd, ref ConfigurationParams configParams, in TextureTable textures)
        {
            if (!IsAvailable)
            {
                return;
            }

            IntPtr textureCallback = NativeD3D.GetSetTextureEventCallback();
            SetTexture(cmd, textureCallback, TextureSlot.HudLess, textures.hudLess);
            SetTexture(cmd, textureCallback, TextureSlot.DistortionField, textures.distortionField);

            NativeConfigParams.SetData(ref configParams);
            cmd.IssuePluginEventAndData(NativeD3D.GetRenderEventCallback(), GetEventId(PluginEvent.ConfigureFrameGen), NativeConfigParams.GetPointer());
        }

        private static int GetEventId(PluginEvent evt)
        {
            if (s_baseEventId < 0)
                s_baseEventId = NativeD3D.GetBaseEventId();

            return s_baseEventId + (int)evt;
        }

        private static void SetTexture(CommandBuffer cmd, IntPtr callback, TextureSlot textureSlot, Texture texture, bool clearTextureTable = false)
        {
            if (texture == null)
                return;

            uint userData = (((uint)textureSlot & 0xFFFF) << 1) | (clearTextureTable ? 1u : 0u);
            cmd.IssuePluginCustomTextureUpdateV2(callback, texture, userData);
        }

        private static class NativeD3D
        {
            private const string PluginName = "FrameGenPluginD3D";

            [DllImport(PluginName, EntryPoint = "FGUP_GetBaseEventId")]
            public static extern int GetBaseEventId();

            [DllImport(PluginName, EntryPoint = "FGUP_RegisterFrameGenProvider")]
            public static extern ulong RegisterFrameGenProvider(IntPtr providerPtr);

            [DllImport(PluginName, EntryPoint = "FGUP_BeginSwitchFrameGenProvider")]
            public static extern int BeginSwitchFrameGenProvider(ulong providerId);

            [DllImport(PluginName, EntryPoint = "FGUP_EndSwitchFrameGenProvider")]
            public static extern int EndSwitchFrameGenProvider();

            [DllImport(PluginName, EntryPoint = "FGUP_GetActiveProviderId")]
            public static extern ulong GetActiveProviderId();

            [DllImport(PluginName, EntryPoint = "FGUP_GetRequiresYFlip")]
            public static extern int GetRequiresYFlip();

            [DllImport(PluginName, EntryPoint = "FGUP_GetMaxInterpolatedFrames")]
            public static extern uint GetMaxInterpolatedFrames();

            [DllImport(PluginName, EntryPoint = "FGUP_GetSupportsDynamicMFG")]
            public static extern int GetSupportsDynamicMFG();

            [DllImport(PluginName, EntryPoint = "FGUP_GetTechVersion")]
            public static extern void GetTechVersion(out FGVersion version);

            [DllImport(PluginName, EntryPoint = "FGUP_OnFrameStart")]
            public static extern void OnFrameStart(ulong frameNumber);

            [DllImport(PluginName, EntryPoint = "FGUP_OnFrameEnd")]
            public static extern void OnFrameEnd(ulong frameNumber);

            [DllImport(PluginName, EntryPoint = "FGUP_OnBeforeInputPolling")]
            public static extern void OnBeforeInputPolling(ulong frameNumber);

            [DllImport(PluginName, EntryPoint = "FGUP_OnBeforeSimulation")]
            public static extern void OnBeforeSimulation(ulong frameNumber);

            [DllImport(PluginName, EntryPoint = "FGUP_OnAfterSimulation")]
            public static extern void OnAfterSimulation(ulong frameNumber);

            [DllImport(PluginName, EntryPoint = "FGUP_GetRenderEventCallback")]
            public static extern IntPtr GetRenderEventCallback();

            [DllImport(PluginName, EntryPoint = "FGUP_GetSetTextureEventCallback")]
            public static extern IntPtr GetSetTextureEventCallback();
        }
    }
}
