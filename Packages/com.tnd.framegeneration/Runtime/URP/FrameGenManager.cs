using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting;
using TND.Common.URP;

[assembly: AlwaysLinkAssembly]

namespace TND.FrameGen.URP
{
    internal class FrameGenManager: FrameGenManagerBase<FrameGenManager>
    {
        private readonly WeakReference<ScriptableRenderer> _associatedRenderer = new(null);
        private FrameGenRendererFeature _rendererFeature;

        [Preserve]
        private FrameGenManager() { }

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            BaseInitialize();

            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
        }

        protected override bool ShouldSkipCamera(Camera camera)
        {
            // Skip overlay cameras; we do not need to inspect overlay cameras, since they share the same renderer as their base camera
            return camera.TryGetComponent(out UniversalAdditionalCameraData cameraData) && cameraData.renderType != CameraRenderType.Base;
        }

        protected override void UpdateCameraAssociation(Camera finalBackbufferCamera)
        {
            if (_rendererFeature == null)
            {
                _rendererFeature = ScriptableObject.CreateInstance<FrameGenRendererFeature>();
                _rendererFeature.Create();
            }

            // Associate the renderer feature with this specific camera
            _rendererFeature.cameraInstanceID = finalBackbufferCamera == null ? 0 :
#if UNITY_6000_4_OR_NEWER
                EntityId.ToULong(finalBackbufferCamera.GetEntityId());
#else
                finalBackbufferCamera.GetInstanceID();
#endif

            // Obtain the scriptable renderer that this camera is using
            ScriptableRenderer activeRenderer = null;
            if (finalBackbufferCamera != null && finalBackbufferCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                activeRenderer = cameraData.scriptableRenderer;
            }

            // If the scriptable renderer we're attached to has changed, make sure to remove the renderer feature from the old scriptable renderer
            if (_associatedRenderer.TryGetTarget(out ScriptableRenderer previousRenderer) && previousRenderer != activeRenderer)
            {
                if (ScriptableRendererMembers.RendererFeatures.GetValue(previousRenderer) is List<ScriptableRendererFeature> oldRendererFeatures)
                {
                    oldRendererFeatures.Remove(_rendererFeature);
                }
            }

            _associatedRenderer.SetTarget(activeRenderer);

            // Try to obtain the list of renderer features from the currently associated scriptable renderer
            if (activeRenderer == null || ScriptableRendererMembers.RendererFeatures.GetValue(activeRenderer) is not List<ScriptableRendererFeature> rendererFeatures)
            {
                _rendererFeature.frameGenEnabled = false;
                return;
            }

            // Add the FG renderer feature to the associated scriptable renderer's feature list
            if (rendererFeatures.IndexOf(_rendererFeature) < 0)
            {
                rendererFeatures.Add(_rendererFeature);
            }

            _rendererFeature.frameGenEnabled = IsFrameGenActiveAndEnabled;
        }
    }
}
