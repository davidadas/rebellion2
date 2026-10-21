using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
#if TND_DEBUG && UNITY_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif
using TND.Common;

namespace TND.FrameGen
{
    public abstract class FrameGenManager
    {
        private static IEnumerator s_switchProcess;
        private static Camera[] s_allCameras = new Camera[1];
        protected static Camera s_finalBackbufferCamera;
        private static int s_lastFrameRendered = -1;

        protected static bool s_frameGenEnabled;
        protected static uint s_numFramesToGenerate = 1;
        protected static bool s_useDynamicMFG;
        protected static uint s_dynamicFramerateTarget;
        protected static Texture s_distortionField;
        protected static FrameGenPlugin.DebugFlags s_debugFlags;

        public static bool IsFrameGenEnabled => s_frameGenEnabled;
        public static IFrameGenProvider ActiveProvider => FrameGenProviderRegistry.FindFrameGenProvider(FrameGenPlugin.ActiveProviderId);
        public static uint MaxInterpolatedFrames => FrameGenPlugin.MaxInterpolatedFrames;
        public static bool SupportsDynamicMFG => FrameGenPlugin.SupportsDynamicMFG;
        public static FrameGenPlugin.FGVersion TechVersion => FrameGenPlugin.TechVersion;
        public static FrameGenPlugin.DebugFlags DebugFlags => s_debugFlags;

        public static bool IsFrameGenActiveAndEnabled => IsFrameGenEnabled && ActiveProvider != null;

        public static void SetFrameGenEnabled(bool enabled)
        {
            s_frameGenEnabled = enabled;
        }

        public static void SetNumFramesToGenerate(uint numFramesToGenerate)
        {
            s_numFramesToGenerate = numFramesToGenerate;
        }

        public static void SetUseDynamicMFG(bool useDynamicMFG)
        {
            s_useDynamicMFG = useDynamicMFG;
        }

        public static void SetDynamicFramerateTarget(uint dynamicFramerateTarget)
        {
            s_dynamicFramerateTarget = dynamicFramerateTarget;
        }

        public static void SetDistortionField(Texture distortionField)
        {
            s_distortionField = distortionField;
        }

        public static void SetDebugFlags(FrameGenPlugin.DebugFlags debugFlags)
        {
            s_debugFlags = debugFlags;
        }

        public static void SwitchFrameGenProvider(IFrameGenProvider frameGenProvider)
        {
            ulong providerId = frameGenProvider != null ? frameGenProvider.UniqueId : 0;
            if (providerId == FrameGenPlugin.ActiveProviderId)
            {
                // No need to switch if we're already on the requested provider
                return;
            }

            if (s_switchProcess != null)
            {
                // Warn if there are multiple overlapping provider switch requests
                // If this warning appears in a log, this may indicate a user error
                Debug.LogWarning("Another FG provider switch is already in progress!");
                return;
            }

            s_switchProcess = FrameGenUtils.SwitchFrameGenProvider(providerId);
        }

        protected static void ProgressProviderSwitch()
        {
            if (s_switchProcess != null)
            {
                // Progress the FG provider switching process by one frame
                // This is essentially the same as executing a coroutine
                if (!s_switchProcess.MoveNext())
                {
                    s_switchProcess = null;
                }
            }
        }

        protected virtual bool ShouldSkipCamera(Camera camera) => false;

        protected Camera FindFinalBackbufferCamera()
        {
            // Obtain a list of all cameras without unnecessary GC alloc
            int cameraCount = Camera.allCamerasCount;
            if (cameraCount > s_allCameras.Length)
            {
                Array.Resize(ref s_allCameras, cameraCount);
            }

            Camera.GetAllCameras(s_allCameras);

            Camera finalCamera = null;
            float highestDepth = float.MinValue;
            for (int i = 0; i < cameraCount; ++i)
            {
                Camera cam = s_allCameras[i];

                // Clear static references, so we do not accidentally cause leaks
                s_allCameras[i] = null;

                // Skip cameras that do not render to the backbuffer
                if (cam == null || !cam.isActiveAndEnabled || cam.targetTexture != null || cam.cameraType is not (CameraType.Game or CameraType.VR))
                    continue;

                // Allow for render pipeline-specific rules to be added
                if (ShouldSkipCamera(cam))
                    continue;

                // The final backbuffer camera is the one with the highest depth value
                // Stable sorting means that if two cameras have the same depth value, the later one in the list will be rendered last
                float camDepth = cam.depth;
                if (camDepth >= highestDepth)
                {
                    highestDepth = camDepth;
                    finalCamera = cam;
                }
            }

            return finalCamera;
        }

#if TND_SRP_CORE
        private static RenderTexture s_backbufferCopy;

        protected static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!FrameGenPlugin.IsAvailable || ActiveProvider == null)
                return;

            if (s_frameGenEnabled)
            {
                // Defer UI rendering until after the scriptable render pipeline finishes its main Render method.
                // This means that during the endContextRendering callback, the backbuffer will contain a fully rendered image without UI.
                SupportedRenderingFeatures.active.rendersUIOverlay = false;
            }

            if (Time.frameCount > s_lastFrameRendered)  // Only do this once per frame, on the first context to get rendered
            {
                // Submit marker that render submission is about to start, on the render thread
                CommandBuffer cmd = CommandBufferPool.Get("FrameGen Before Rendering");
                FrameGenPlugin.OnBeforeRendering(cmd);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                s_lastFrameRendered = Time.frameCount;
            }
        }

        protected static void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!FrameGenPlugin.IsAvailable || ActiveProvider == null)
                return;

            // Skip rendering contexts that do not target the backbuffer
            if (cameras == null || cameras.IndexOf(s_finalBackbufferCamera) < 0)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("FrameGen Config Pass");

            if (cameras.Count > 0)
            {
                int screenWidth = Screen.width;
                int screenHeight = Screen.height;

                if (s_frameGenEnabled)
                {
                    GraphicsFormat backbufferFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    if (FrameGenUtils.HDRDisplayActive)
                    {
                        backbufferFormat = HDROutputSettings.main.graphicsFormat;
                    }

                    FrameGenUtils.AllocateRenderTexture(ref s_backbufferCopy, screenWidth, screenHeight, backbufferFormat, "HUD-less Backbuffer Copy");

                    cmd.SetRenderTarget(BuiltinRenderTextureType.None);
                    cmd.Blit(null, s_backbufferCopy);
                    cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                }

                FrameGenPlugin.ConfigurationParams configParams = new()
                {
                    enable = s_frameGenEnabled ? 1u : 0u,
                    useAsync = 0,
                    frameNumber = (ulong)Time.frameCount,
                    renderFlags = 0,
                    debugFlags = s_debugFlags,
                    viewportLeft = 0,
                    viewportTop = 0,
                    viewportWidth = screenWidth,
                    viewportHeight = screenHeight,
                    numFramesToGenerate = s_numFramesToGenerate,
                    useDynamicMFG = s_useDynamicMFG ? 1u : 0u,
                    dynamicFramerateTarget = s_dynamicFramerateTarget,
                };

                if (SystemInfo.usesReversedZBuffer)
                    configParams.renderFlags |= FrameGenPlugin.RenderFlags.InvertedDepth;
                if (FrameGenUtils.HDRDisplayActive)
                    configParams.renderFlags |= FrameGenPlugin.RenderFlags.HighDynamicRange;

                FrameGenPlugin.TextureTable textureTable = new()
                {
                    hudLess = s_frameGenEnabled ? s_backbufferCopy : null,
                    distortionField = s_distortionField,
                };

                FrameGenPlugin.Configure(cmd, ref configParams, textureTable);
            }
            else
            {
                // Ensure a Configure pass is always executed, even if we have no rendering cameras
                DispatchDisabledConfigPass(cmd);
            }

            // Submit marker that render submission is ending, on the render thread
            FrameGenPlugin.OnAfterRendering(cmd);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            context.Submit();
        }
#endif

        // Dispatch a fallback Configure call to the plugin with FG disabled.
        // Used when there is no camera to attach to or obtain parameters from, e.g. when on a UI-only loading screen.
        protected static void DispatchDisabledConfigPass(CommandBuffer cmd)
        {
            FrameGenPlugin.ConfigurationParams configParams = new()
            {
                enable = 0u,
                useAsync = 0,
                frameNumber = (ulong)Time.frameCount,
                renderFlags = 0,
                debugFlags = s_debugFlags,
                viewportLeft = 0,
                viewportTop = 0,
                viewportWidth = Screen.width,
                viewportHeight = Screen.height,
                numFramesToGenerate = 0,
            };

            if (SystemInfo.usesReversedZBuffer)
                configParams.renderFlags |= FrameGenPlugin.RenderFlags.InvertedDepth;
            if (FrameGenUtils.HDRDisplayActive)
                configParams.renderFlags |= FrameGenPlugin.RenderFlags.HighDynamicRange;

            FrameGenPlugin.Configure(cmd, ref configParams, new FrameGenPlugin.TextureTable());
        }
    }

    internal abstract class FrameGenManagerBase<TFrameGenManager>: FrameGenManager
        where TFrameGenManager: FrameGenManagerBase<TFrameGenManager>
    {
        private static TFrameGenManager s_instance;

        protected abstract void UpdateCameraAssociation(Camera finalBackbufferCamera);

        protected static void BaseInitialize()
        {
            s_instance = (TFrameGenManager)Activator.CreateInstance(typeof(TFrameGenManager), true);

            PlayerLoopHelper.InsertPlayerLoopSystem<FrameGenUpdate.AfterTimeUpdate, TimeUpdate>(OnAfterTimeUpdate);
            PlayerLoopHelper.InsertPlayerLoopSystem<FrameGenUpdate.BeforeInput, EarlyUpdate.UpdateInputManager>(OnBeforeInputUpdate, true);
            PlayerLoopHelper.InsertPlayerLoopSystem<FrameGenUpdate.BeforeSimulation, PreUpdate>(OnBeforeSimulation, true);
            PlayerLoopHelper.InsertPlayerLoopSystem<FrameGenUpdate.AfterSimulation, PreLateUpdate>(OnAfterSimulation);
            PlayerLoopHelper.InsertPlayerLoopSystem<FrameGenUpdate.AfterEndOfFrame, PostLateUpdate>(OnAfterEndOfFrame);
        }

        private static void OnAfterTimeUpdate()
        {
            FrameGenPlugin.OnFrameStart();
        }

        private static void OnBeforeInputUpdate()
        {
            FrameGenPlugin.OnBeforeInputPolling();
        }

        private static void OnBeforeSimulation()
        {
            FrameGenPlugin.OnBeforeSimulation();

            // Update the FG provider switching process regardless of whether FG is actually available.
            // This is because the switch may be started from user code, and we don't want the process to get stuck.
            ProgressProviderSwitch();

            if (!FrameGenPlugin.IsAvailable)
                return;

#if TND_DEBUG
            HandleDebugKeys();
#endif

            s_finalBackbufferCamera = s_instance.FindFinalBackbufferCamera();
            s_instance.UpdateCameraAssociation(s_finalBackbufferCamera);
        }

        private static void OnAfterSimulation()
        {
            FrameGenPlugin.OnAfterSimulation();
        }

        private static void OnAfterEndOfFrame()
        {
            FrameGenPlugin.OnFrameEnd();
        }

#if TND_DEBUG
        // ReSharper disable once StaticMemberInGenericType
        private static IFrameGenProvider[] s_frameGenProviders;
        // ReSharper disable once StaticMemberInGenericType
        private static int s_currentProviderIdIndex;

        private static void HandleDebugKeys()
        {
            if (s_frameGenProviders == null)
            {
                int idx = 0;
                var providers = FrameGenProviderRegistry.GetFrameGenProviders();
                s_frameGenProviders = new IFrameGenProvider[providers.Count + 1];
                s_frameGenProviders[idx++] = null;
                foreach (IFrameGenProvider provider in providers)
                {
                    s_frameGenProviders[idx++] = provider;
                }

                s_currentProviderIdIndex = Math.Max(0, Array.IndexOf(s_frameGenProviders, ActiveProvider));
            }

            bool toggleFrameGen = false, switchProvider = false, toggleDebugView = false;
    #if UNITY_INPUTSYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                toggleFrameGen = kb.fKey.wasPressedThisFrame;
                switchProvider = kb.gKey.wasPressedThisFrame;
                toggleDebugView = kb.bKey.wasPressedThisFrame;
            }
    #else
            toggleFrameGen = Input.GetKeyDown(KeyCode.F);
            switchProvider = Input.GetKeyDown(KeyCode.G);
            toggleDebugView = Input.GetKeyDown(KeyCode.B);
    #endif

            if (toggleFrameGen)
            {
                SetFrameGenEnabled(!s_frameGenEnabled);
                Debug.Log($"Set FG enabled to {s_frameGenEnabled}");
            }

            if (switchProvider)
            {
                Debug.Log("Switching FG provider");
                s_currentProviderIdIndex = (s_currentProviderIdIndex + 1) % s_frameGenProviders.Length;
                SwitchFrameGenProvider(s_frameGenProviders[s_currentProviderIdIndex]);
            }

            if (toggleDebugView)
            {
                SetDebugFlags(s_debugFlags ^ FrameGenPlugin.DebugFlags.DebugView);
                Debug.Log($"Set FG debug flags to {s_debugFlags}");
            }
        }
#endif

        struct FrameGenUpdate
        {
            public struct AfterTimeUpdate
            {
            }

            public struct BeforeInput
            {
            }

            public struct BeforeSimulation
            {
            }

            public struct AfterSimulation
            {
            }

            public struct AfterEndOfFrame
            {
            }
        }
    }
}
