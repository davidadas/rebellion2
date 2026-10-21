using UnityEngine;
using UnityEngine.Rendering.Universal;
using TND.Common.URP;
#if UNITY_6000_4_OR_NEWER
using ObjectId = System.UInt64;
#else
using ObjectId = System.Int32;
#endif

namespace TND.FrameGen.URP
{
    [DisallowMultipleRendererFeature("TND Frame Generation")]
    internal class FrameGenRendererFeature : ScriptableRendererFeature
    {
        internal bool frameGenEnabled;
        internal ObjectId cameraInstanceID;

        private FrameGenPreparePass _preparePass;

        public override void Create()
        {
            name = "TND Frame Generation";

            _preparePass = new FrameGenPreparePass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
            _preparePass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying || Application.isEditor)
                return;

            // Only perform frame gen on the final camera in the stack that renders to the backbuffer
            ref CameraData cameraData = ref renderingData.cameraData;
            if (!cameraData.resolveFinalTarget || cameraData.targetTexture != null || cameraData.cameraType is not (CameraType.Game or CameraType.VR))
                return;

            // Ensure we only add passes to the exact camera that FG is associated with
#if UNITY_6000_4_OR_NEWER
            ObjectId currentCameraID = EntityId.ToULong(cameraData.camera.GetEntityId());
#else
            ObjectId currentCameraID = cameraData.camera.GetInstanceID();
#endif
            if (currentCameraID != cameraInstanceID)
                return;

            // Ensure stale resources don't stick around for too long
            UpscalingHelpers.PurgeUnusedRTHandles();

            if (frameGenEnabled)
            {
                renderer.EnqueuePass(_preparePass);
            }
        }
    }
}
