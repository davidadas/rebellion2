using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using UnityEngine.XR;

#if TND_URP_RENDERGRAPH
using UnityEngine.Rendering.RenderGraphModule;
#pragma warning disable 0618    // Disable obsolete warnings
#endif

#if TND_URP_COMPATIBILITY && URP_COMPATIBILITY_MODE
using UnityEngine.Rendering.Universal.CompatibilityMode;
#endif

namespace TND.Common.URP
{
    /// <summary>
    /// Assorted helper methods to allow the upscaler render pass to access a number of URP-internal data structures and methods.
    /// </summary>
    public static class UpscalingHelpers
    {
        private static readonly RTHandlePool RTHandlePool = new();

        /// <summary>
        /// Apply a custom jitter matrix to a camera, while keeping the existing view and projection matrices.
        /// Normally the jitter matrix is only available internally and used only for TAA.
        /// Applying jitter this way integrates more elegantly into the URP render pipeline than modifying the projection matrix in-place.
        /// </summary>
        public static void SetCameraJitterMatrix(ref CameraData cameraData, in Matrix4x4 jitterMatrix, int viewIndex = 0)
        {
#if UNITY_2022_2_OR_NEWER
            cameraData.SetViewProjectionAndJitterMatrix(cameraData.GetViewMatrix(viewIndex), cameraData.GetProjectionMatrixNoJitter(viewIndex), jitterMatrix);
#endif
        }

#if TND_URP_RENDERGRAPH
        public static void SetCameraJitterMatrixRenderGraph(in CameraData cameraData, in Matrix4x4 jitterMatrix, int viewIndex = 0)
        {
            SetCameraJitterMatrixRenderGraph(cameraData.universalCameraData, jitterMatrix, viewIndex);
        }

        public static void SetCameraJitterMatrixRenderGraph(UniversalCameraData cameraData, in Matrix4x4 jitterMatrix, int viewIndex = 0)
        {
            cameraData.SetViewProjectionAndJitterMatrix(cameraData.GetViewMatrix(viewIndex), cameraData.GetProjectionMatrixNoJitter(viewIndex), jitterMatrix);
        }

        public static Matrix4x4 GetGPUProjectionMatrix(UniversalCameraData cameraData, bool yFlip, int viewIndex = 0)
        {
            return GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(viewIndex), yFlip);
        }

        public static Matrix4x4 GetGPUProjectionMatrixNoJitter(UniversalCameraData cameraData, bool yFlip, int viewIndex = 0)
        {
            return GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrixNoJitter(viewIndex), yFlip);
        }

        public static Matrix4x4 GetGPUProjectionMatrix(UniversalCameraData cameraData, in TextureHandle activeColorTexture, int viewIndex = 0)
        {
            return GetGPUProjectionMatrix(cameraData, SystemInfo.graphicsUVStartsAtTop && cameraData.IsRenderTargetProjectionMatrixFlipped(activeColorTexture));
        }

        public static Matrix4x4 GetGPUProjectionMatrixNoJitter(UniversalCameraData cameraData, in TextureHandle activeColorTexture, int viewIndex = 0)
        {
            return GetGPUProjectionMatrixNoJitter(cameraData, SystemInfo.graphicsUVStartsAtTop && cameraData.IsRenderTargetProjectionMatrixFlipped(activeColorTexture));
        }

        public static Vector2Int GetCameraPixelSize(UniversalCameraData cameraData)
        {
            return new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight);
        }

        public static void SetCameraDepthTexture(UniversalResourceData resourceData, TextureHandle textureHandle)
        {
            resourceData.cameraDepthTexture = textureHandle;
        }
#endif

        public static Vector2Int GetCameraPixelSize(ref CameraData cameraData)
        {
            return new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight);
        }

        public static void SetCameraPixelSize(ref CameraData cameraData, in Vector2Int pixelSize)
        {
            cameraData.pixelWidth = pixelSize.x;
            cameraData.pixelHeight = pixelSize.y;
        }

        public static void SetTaaMipBias(ref CameraData cameraData, float mipBias)
        {
            cameraData.taaSettings.mipBias = mipBias;
        }

        /// <summary>
        /// Reconfigure the color buffer system for after upscaling, such that any subsequent passes will render at full display resolution.
        /// This requires updating URP's internal color and depth buffer descriptors and reallocating these buffers to the new size.
        /// We then obtain the freshly reallocated front buffer for the upscaler to write its output to.
        /// </summary>
        public static bool PrepareUpscalerOutput(CommandBuffer cmd, ScriptableRenderer renderer, in RenderTextureDescriptor cameraTargetDescriptor,
#if UNITY_2022_1_OR_NEWER
            out RTHandle upscalerTarget)
#else
            out RenderTargetHandle upscalerTarget)
#endif
        {
            if (!TryGetColorBufferSystem(renderer, out var colorBufferSystem))
            {
                Debug.LogWarning("Could not obtain renderer's color buffer system. Upscaling is likely going to be broken.");
                upscalerTarget = default;
                return false;
            }

#if UNITY_2022_2_OR_NEWER
            // Reconfigure color buffer system to full display resolution
            colorBufferSystem.SetCameraSettings(cameraTargetDescriptor, FilterMode.Bilinear);
            RTHandle frontBuffer = colorBufferSystem.GetFrontBuffer(cmd); // This forces the color buffers to be reallocated at display resolution
#else
            // Reconfigure color buffer system to full display resolution
            colorBufferSystem.SetCameraSettings(cmd, cameraTargetDescriptor, FilterMode.Bilinear);
            var frontBuffer = colorBufferSystem.GetFrontBuffer(cmd);
#endif

            // Inject the new front buffer into the final blit pass
            // This is mostly required for when post-processing is disabled
            if (TryGetFinalBlitPass(renderer, out var finalBlitPass))
            {
                finalBlitPass.Setup(cameraTargetDescriptor, frontBuffer);
            }
            else
            {
                Debug.LogWarning("Could not obtain renderer's final blit pass. Upscaling output might be broken.");
            }

            upscalerTarget = frontBuffer;
            return true;
        }

        /// <summary>
        /// Set up the color buffer system for any render passes that come after upscaling, most notably post-processing.
        /// This ensures that the subsequent passes will pick up the upscaled image correctly.
        /// </summary>
        public static void InjectUpscalerOutput(CommandBuffer cmd, ScriptableRenderer renderer)
        {
#if TND_URP_COMPATIBILITY
            if (!TryGetColorBufferSystem(renderer, out var colorBufferSystem))
            {
                Debug.LogWarning("Could not obtain renderer's color buffer system. Upscaling is likely going to be broken.");
                return;
            }

            colorBufferSystem.Swap();

            // Prepare inputs for the next pass (post-processing)
            var cameraTarget = colorBufferSystem.GetBackBuffer(cmd);
#if UNITY_2022_1_OR_NEWER
            renderer.ConfigureCameraTarget(cameraTarget, cameraTarget);
    #if !UNITY_6000_3_OR_NEWER || URP_COMPATIBILITY_MODE
            if (renderer is UniversalRenderer universalRenderer)
            {
                universalRenderer.m_ActiveCameraDepthAttachment = cameraTarget;
            }
    #endif
#else
            renderer.ConfigureCameraTarget(cameraTarget.Identifier(), cameraTarget.Identifier());
            if (renderer is UniversalRenderer universalRenderer)
            {
                UniversalRendererMembers.ActiveCameraDepthAttachment.SetValue(universalRenderer, cameraTarget);
            }
#endif
#endif
        }

        /// <summary>
        /// Update the cached camera target descriptor both all post-processing passes.
        /// Normally the post-processing passes are set up with a camera target descriptor at scaled render resolution, and this descriptor is cached (copied) inside the passes themselves.
        /// For post-processing to behave correctly after upscaling, we need to update these values with the correct full display resolution size.
        /// </summary>
        public static void UpdatePostProcessDescriptors(ScriptableRenderer renderer, in RenderTextureDescriptor cameraTargetDescriptor)
        {
#if !UNITY_6000_3_OR_NEWER || (TND_URP_COMPATIBILITY && URP_COMPATIBILITY_MODE)
            if (PostProcessPassMembers.Descriptor == null || !TryGetPostProcessPasses(renderer, out var postProcessPass, out var finalPostProcessPass))
            {
                Debug.LogWarning("Could not find any post-processing passes to update. Post-processing might be broken after upscaling.");
                return;
            }

            if (postProcessPass != null || finalPostProcessPass != null)
            {
                // Box this struct only once to halve the amount of GC alloc when both post-process passes are active
                object boxedCameraTargetDescriptor = cameraTargetDescriptor;

                if (postProcessPass != null)
                    PostProcessPassMembers.Descriptor.SetValue(postProcessPass, boxedCameraTargetDescriptor);

                if (finalPostProcessPass != null)
                    PostProcessPassMembers.Descriptor.SetValue(finalPostProcessPass, boxedCameraTargetDescriptor);
            }
#endif
        }

        public static ScriptableRenderPassInput GetRequiredPostProcessInputs(ScriptableRenderer renderer, bool postProcessEnabled, RenderPassEvent upscalingRenderPassEvent)
        {
            var inputs = ScriptableRenderPassInput.None;

            if (postProcessEnabled)
            {
                // Check required inputs on built-in URP post-processing effects
                var volumeStack = VolumeManager.instance.stack;
                if (volumeStack.GetComponent<DepthOfField>()?.IsActive() ?? false)
                    inputs |= ScriptableRenderPassInput.Depth;

                if (volumeStack.GetComponent<MotionBlur>()?.IsActive() ?? false)
                    inputs |= ScriptableRenderPassInput.Depth;  // URP only applies camera motion blur based on the depth buffer, it does not use motion vectors!

                if (!LensFlareCommonSRP.Instance.IsEmpty())
                    inputs |= ScriptableRenderPassInput.Depth;  // Lens flare uses depth buffer to calculate occlusion
            }

            // Check required inputs on any render passes that come after upscaling
            if (TryGetActivePassQueue(renderer, out List<ScriptableRenderPass> passes))
            {
                int numPasses = passes.Count;
                for (int i = 0; i < numPasses; ++i)
                {
                    var pass = passes[i];

                    // Skip any pre-upscaling passes and upscaling itself
                    if (pass.renderPassEvent <= upscalingRenderPassEvent || pass.renderPassEvent > RenderPassEvent.AfterRendering)
                        continue;

#if !UNITY_6000_3_OR_NEWER || (TND_URP_COMPATIBILITY && URP_COMPATIBILITY_MODE)
                    // Skip URP built-in post-processing
                    if (pass is PostProcessPass)
                        continue;
#endif

                    // Some third-party assets do not accurately declare which inputs they require, so for safety assume they require depth
                    inputs |= pass.input | ScriptableRenderPassInput.Depth;
                }
            }

            return inputs;
        }

        public static int GetXRViewCount(this in CameraData cameraData)
        {
            // XRPass is internal in older URP versions, so we have to go through this helper method
            return cameraData.xr.enabled ? cameraData.xr.viewCount : 1;
        }

        public static int GetXRMultiPassId(this in CameraData cameraData)
        {
            // XRPass is internal in older URP versions, so we have to go through this helper method
            return cameraData.xr.multipassId;
        }

        public static bool TryGetXREyeDisplaySize(Camera camera, UniversalAdditionalCameraData additionalCameraData, out Vector2Int displaySize, out int viewCount, out bool isSinglePassXR)
        {
            displaySize = Vector2Int.zero;
            isSinglePassXR = false;
            viewCount = 1;

#if ENABLE_VR && ENABLE_XR_MODULE
            // Check if this camera actually draws to an XR display
            bool isGameCamera = camera.cameraType is CameraType.Game or CameraType.VR;
            if (!isGameCamera || camera.targetTexture != null || !additionalCameraData.allowXRRendering)
            {
                return false;
            }

            // Check if XR is active
            if (!XRSettings.enabled || !XRSettings.isDeviceActive)
            {
                return false;
            }

    #if UNITY_2022_2_OR_NEWER
            var xrDisplay = XRSystem.GetActiveDisplay();
    #else
            var xrDisplay = XRSystemMembers.Display.GetValue(UniversalRenderPipeline.m_XRSystem) as XRDisplaySubsystem;
    #endif
            if (xrDisplay == null || !xrDisplay.running)
            {
                return false;
            }

            // The number of views can be derived from how many render passes there are, and how many views per pass.
            // Multi-pass uses multiple render passes with one view per pass, while single-pass instanced uses one render pass with multiple views.
            int renderPassCount = xrDisplay.GetRenderPassCount();
            if (renderPassCount <= 0)
            {
                return false;
            }

            xrDisplay.GetRenderPass(0, out var xrRenderPass);
            int renderParamCount = xrRenderPass.GetRenderParameterCount();
            isSinglePassXR = renderParamCount == 2 && xrRenderPass.renderTargetDesc.volumeDepth == 2;

            displaySize = new(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
    #if !UNITY_2022_2_OR_NEWER
            // Calculate the original display resolution by reversing render scale
            float invEyeScale = 1.0f / XRSettings.eyeTextureResolutionScale;

            // Round up to an integer, then round down to the nearest even number.
            // Unfortunately due to rounding errors both in Unity, URP and here, we cannot reconstruct the original display size with 100% certainty here.
            // As display resolutions are rarely odd numbered, this is the most likely to give us the correct display size.
            displaySize = new Vector2Int(Mathf.CeilToInt(displaySize.x * invEyeScale) & ~1, Mathf.CeilToInt(displaySize.y * invEyeScale) & ~1);
    #endif
            viewCount = renderPassCount * renderParamCount;
            return true;
#else
            return false;
#endif
        }

#if UNITY_6000_6_OR_NEWER
        // Empty stub class to handle the removal of this class from Unity 6.6+
        private abstract class RenderTargetBufferSystem
        {
            public void SetCameraSettings(in RenderTextureDescriptor cameraTargetDescriptor, FilterMode filterMode)
            {
            }

            public RTHandle GetFrontBuffer(CommandBuffer cmd)
            {
                return null;
            }
        }
#endif

        private static bool TryGetColorBufferSystem(ScriptableRenderer renderer, out RenderTargetBufferSystem colorBufferSystem)
        {
            switch (renderer)
            {
#if !UNITY_6000_3_OR_NEWER || (TND_URP_COMPATIBILITY && URP_COMPATIBILITY_MODE)
                case UniversalRenderer universalRenderer:
                    colorBufferSystem = universalRenderer.m_ColorBufferSystem;
                    return true;
    #if UNITY_2022_2_OR_NEWER
                case Renderer2D renderer2D:
                    colorBufferSystem = renderer2D.m_ColorBufferSystem;
                    return true;
    #endif
#endif
                default:
                    colorBufferSystem = null;
                    return false;
            }
        }

        public static RTHandle GetMotionVectorTexture(ScriptableRenderer renderer)
        {
            switch (renderer)
            {
                case UniversalRenderer universalRenderer:
                    return UniversalRendererMembers.MotionVectorColor?.GetValue(universalRenderer) as RTHandle;
                default:
                    return null;
            }
        }

        public static bool TryGetRendererFeatures(ScriptableRenderer renderer, out List<ScriptableRendererFeature> rendererFeatures)
        {
            if (ScriptableRendererMembers.RendererFeatures == null)
            {
                rendererFeatures = null;
                return false;
            }

            rendererFeatures = ScriptableRendererMembers.RendererFeatures.GetValue(renderer) as List<ScriptableRendererFeature>;
            return rendererFeatures != null;
        }

        public static ScriptableRendererData GetRendererData(UniversalRenderPipelineAsset pipelineAsset)
        {
            return pipelineAsset.scriptableRendererData;
        }

        private static bool TryGetFinalBlitPass(ScriptableRenderer renderer, out FinalBlitPass finalBlitPass)
        {
            switch (renderer)
            {
                case UniversalRenderer universalRenderer:
                    finalBlitPass = UniversalRendererMembers.FinalBlitPass?.GetValue(universalRenderer) as FinalBlitPass;
                    return finalBlitPass != null;
                case Renderer2D renderer2D:
                    finalBlitPass = Renderer2DMembers.FinalBlitPass?.GetValue(renderer2D) as FinalBlitPass;
                    return finalBlitPass != null;
                default:
                    finalBlitPass = null;
                    return false;
            }
        }

#if !UNITY_6000_3_OR_NEWER || (TND_URP_COMPATIBILITY && URP_COMPATIBILITY_MODE)
        private static bool TryGetPostProcessPasses(ScriptableRenderer renderer, out PostProcessPass postProcessPass, out PostProcessPass finalPostProcessPass)
        {
            switch (renderer)
            {
                case UniversalRenderer universalRenderer:
                    postProcessPass = universalRenderer.postProcessPass;
                    finalPostProcessPass = universalRenderer.finalPostProcessPass;
                    return true;
                case Renderer2D renderer2D:
                    postProcessPass = renderer2D.postProcessPass;
                    finalPostProcessPass = renderer2D.finalPostProcessPass;
                    return true;
                default:
                    postProcessPass = null;
                    finalPostProcessPass = null;
                    return false;
            }
        }
#endif

        private static bool TryGetActivePassQueue(ScriptableRenderer renderer, out List<ScriptableRenderPass> activePassQueue)
        {
            if (ScriptableRendererMembers.ActiveRenderPassQueue == null)
            {
                activePassQueue = null;
                return false;
            }

            activePassQueue = ScriptableRendererMembers.ActiveRenderPassQueue.GetValue(renderer) as List<ScriptableRenderPass>;
            return activePassQueue != null;
        }

        /// <summary>
        /// Obtain a render texture handle, either by taking an existing texture from the pool or creating it anew.
        /// </summary>
        public static void AllocateRTHandle(ref RTHandle handle, in RenderTextureDescriptor descriptor, FilterMode filterMode, TextureWrapMode wrapMode, string name)
        {
            if (RTHandlePool.TryGetResource(descriptor, name, out handle))
                return;

            handle = RTHandles.Alloc(width: descriptor.width, height: descriptor.height, slices: descriptor.volumeDepth, depthBufferBits: (DepthBits)descriptor.depthBufferBits,
                colorFormat: descriptor.graphicsFormat, filterMode: filterMode, wrapMode: wrapMode, dimension: descriptor.dimension, enableRandomWrite: descriptor.enableRandomWrite,
                useMipMap: descriptor.useMipMap, autoGenerateMips: descriptor.autoGenerateMips, isShadowMap: false, anisoLevel: 1, mipMapBias: 0, msaaSamples: (MSAASamples)descriptor.msaaSamples,
                bindTextureMS: descriptor.bindMS, useDynamicScale: descriptor.useDynamicScale, memoryless: descriptor.memoryless, name: name);
        }

        /// <summary>
        /// Release a render texture handle by placing it into a pool for reuse.
        /// </summary>
        public static void ReleaseRTHandle(ref RTHandle handle)
        {
            if (handle == null || handle.rt == null || !handle.rt.IsCreated())
                return;

            if (!RTHandlePool.AddResourceToPool(handle, Time.frameCount))
            {
                // Texture was rejected by the pool, so instead just release it
                RTHandles.Release(handle);
            }

            handle = null;
        }

        /// <summary>
        /// Release render textures from the pool that haven't been used for several frames.
        /// </summary>
        public static void PurgeUnusedRTHandles()
        {
            //s_rtHandlePool.LogDebugInfo();
            RTHandlePool.PurgeUnusedResources(Time.frameCount);
        }

        /// <summary>
        /// Release all render textures in the pool.
        /// </summary>
        public static void CleanupRTHandles()
        {
            RTHandlePool.Cleanup();
        }

        public static bool ReAllocateHandleIfNeeded(ref RTHandle handle, in RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Repeat, string name = "")
        {
#if UNITY_2023_3_OR_NEWER
            return RenderingUtils.ReAllocateHandleIfNeeded(ref handle, in descriptor, filterMode, wrapMode, name: name);
#elif UNITY_2022_2_OR_NEWER
            return RenderingUtils.ReAllocateIfNeeded(ref handle, descriptor, filterMode, wrapMode, name: name);
#else
            if (handle == null || handle.rt == null || handle.rt.width != descriptor.width || handle.rt.height != descriptor.height || handle.rt.graphicsFormat != descriptor.graphicsFormat
                || handle.rt.depthStencilFormat != descriptor.depthStencilFormat || handle.rt.enableRandomWrite != descriptor.enableRandomWrite || handle.rt.useMipMap != descriptor.useMipMap
                || handle.rt.filterMode != filterMode || handle.rt.wrapMode != wrapMode || handle.name != name)
            {
                ReleaseRTHandle(ref handle);
                AllocateRTHandle(ref handle, in descriptor, filterMode, wrapMode, name);
                return true;
            }

            return false;
#endif
        }

        private static readonly int ScreenParamsId = Shader.PropertyToID("_ScreenParams");
        private static readonly int ScaledScreenParamsId = Shader.PropertyToID("_ScaledScreenParams");
        private static readonly int ScreenSizeId = Shader.PropertyToID("_ScreenSize");

        /// <summary>
        /// This logic was borrowed from ScriptableRenderer.SetPerCameraShaderVariables,
        /// keeping only the code required to update the screen size parameters.
        /// </summary>
        public static void UpdatePerCameraShaderVariables(CommandBuffer cmd, ref CameraData cameraData)
        {
            Camera camera = cameraData.camera;

            float scaledCameraWidth = cameraData.cameraTargetDescriptor.width;
            float scaledCameraHeight = cameraData.cameraTargetDescriptor.height;
            float cameraWidth = camera.pixelWidth;
            float cameraHeight = camera.pixelHeight;

            // Use eye texture's width and height as screen params when XR is enabled
            if (cameraData.xr.enabled)
            {
                cameraWidth = cameraData.cameraTargetDescriptor.width;
                cameraHeight = cameraData.cameraTargetDescriptor.height;
            }

            if (camera.allowDynamicResolution)
            {
                scaledCameraWidth *= ScalableBufferManager.widthScaleFactor;
                scaledCameraHeight *= ScalableBufferManager.heightScaleFactor;
            }

            cmd.SetGlobalVector(ScreenParamsId, new Vector4(cameraWidth, cameraHeight, 1.0f + 1.0f / cameraWidth, 1.0f + 1.0f / cameraHeight));
            cmd.SetGlobalVector(ScaledScreenParamsId, new Vector4(scaledCameraWidth, scaledCameraHeight, 1.0f + 1.0f / scaledCameraWidth, 1.0f + 1.0f / scaledCameraHeight));
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(scaledCameraWidth, scaledCameraHeight, 1.0f / scaledCameraWidth, 1.0f / scaledCameraHeight));
        }

#if TND_URP_RENDERGRAPH
        // Pre-boxed value to avoid GC alloc at run-time
        private static readonly object BoxedTrue = true;

        public static void SetupUpscaledColorHandles(ScriptableRenderer renderer, in RenderTextureDescriptor upscaledTargetDesc)
        {
            switch (renderer)
            {
                case UniversalRenderer:
                    var upscaledCameraColorHandles = UniversalRendererMembers.UpscaledCameraColorHandles?.GetValue(null) as RTHandle[];
                    if (upscaledCameraColorHandles != null)
                    {
                        RenderingUtils.ReAllocateHandleIfNeeded(ref upscaledCameraColorHandles[0], upscaledTargetDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_CameraUpscaledTargetAttachmentA");
                        RenderingUtils.ReAllocateHandleIfNeeded(ref upscaledCameraColorHandles[1], upscaledTargetDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_CameraUpscaledTargetAttachmentB");
                    }

                    UniversalRendererMembers.UseUpscaledColorHandle?.SetValue(null, BoxedTrue);
                    break;
            }
        }
#endif

        public static unsafe IntPtr GetPtr<T>(ref T value)
            where T: struct
        {
            return new IntPtr(UnsafeUtility.AddressOf(ref value));
        }

        public static unsafe ref T AsRef<T>(IntPtr ptr)
            where T: struct
        {
            return ref UnsafeUtility.AsRef<T>(ptr.ToPointer());
        }
    }
}
