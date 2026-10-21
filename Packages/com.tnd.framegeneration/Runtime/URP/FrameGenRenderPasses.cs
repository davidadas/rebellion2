using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TND.Common.URP;

#if TND_URP_RENDERGRAPH
#pragma warning disable 0672    // Disable obsolete warnings
#pragma warning disable 0618    // Disable obsolete warnings
#endif

namespace TND.FrameGen.URP
{
    public partial class FrameGenPreparePass: ScriptableRenderPass
    {
        private const string PassName = "FrameGen Prepare Pass";

#if TND_URP_COMPATIBILITY
        private RTHandle _depthCopy;
        private RTHandle _motionVectorCopy;

        private static readonly int MotionTexturePropertyID = Shader.PropertyToID("_MotionVectorTexture");

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var depthDesc = cameraTextureDescriptor;
            depthDesc.graphicsFormat = GraphicsFormat.R32_SFloat;
            depthDesc.depthBufferBits = 0;
            depthDesc.depthStencilFormat = GraphicsFormat.None;
            depthDesc.msaaSamples = 1;
            depthDesc.useMipMap = false;
            depthDesc.bindMS = false;

            var mvDesc = depthDesc;
            mvDesc.graphicsFormat = GraphicsFormat.R16G16_SFloat;

            UpscalingHelpers.ReAllocateHandleIfNeeded(ref _depthCopy, depthDesc, FilterMode.Point, TextureWrapMode.Clamp, "FrameGen Depth Copy");
            UpscalingHelpers.ReAllocateHandleIfNeeded(ref _motionVectorCopy, mvDesc, FilterMode.Point, TextureWrapMode.Clamp, "FrameGen Motion Vectors Copy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(PassName);
            ref var cameraData = ref renderingData.cameraData;

            Vector4 scaleBias = new Vector4(1, 1, 0, 0);
            if (FrameGenUtils.YFlipRequired)
            {
                scaleBias.w = scaleBias.y;
                scaleBias.y *= -1;
            }

#if UNITY_2022_2_OR_NEWER
            cmd.SetRenderTarget(_depthCopy, 0, CubemapFace.Unknown, -1);
            Blitter.BlitTexture(cmd, cameraData.renderer.cameraDepthTargetHandle, scaleBias, 0, false);
            cmd.SetRenderTarget(_motionVectorCopy, 0, CubemapFace.Unknown, -1);
            Blitter.BlitTexture(cmd, Shader.GetGlobalTexture(MotionTexturePropertyID), scaleBias, Blitter.GetBlitMaterial(TextureXR.dimension), 0);
#else
            Vector2 scale = new Vector2(scaleBias.x, scaleBias.y);
            Vector2 offset = new Vector2(scaleBias.z, scaleBias.w);
            cmd.Blit(cameraData.renderer.cameraDepthTarget, _depthCopy, scale, offset);
            cmd.Blit(Shader.GetGlobalTexture(MotionTexturePropertyID), _motionVectorCopy, scale, offset);
#endif

            // Derive jitter offset from the information that we have access to
#if UNITY_2022_2_OR_NEWER
            Matrix4x4 projMatrix = cameraData.GetGPUProjectionMatrix();
            Matrix4x4 projMatrixNoJitter = cameraData.GetGPUProjectionMatrixNoJitter();
            Vector2 jitterOffset = FrameGenUtils.DeriveJitterOffset(projMatrix, projMatrixNoJitter);
#else
            bool yFlip = FrameGenUtils.YFlipRequired;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cameraData.camera.projectionMatrix, yFlip);
            Matrix4x4 projMatrixNoJitter = GL.GetGPUProjectionMatrix(cameraData.camera.nonJitteredProjectionMatrix, yFlip);
            Vector2 jitterOffset = FrameGenUtils.DeriveJitterOffset(projMatrix, projMatrixNoJitter);
#endif

            FrameGenPlugin.PrepareParams prepareParams = new()
            {
                frameNumber = (ulong)Time.frameCount,
                renderWidth = (uint)cameraData.cameraTargetDescriptor.width,
                renderHeight = (uint)cameraData.cameraTargetDescriptor.height,
                jitterOffsetX = jitterOffset.x,
                jitterOffsetY = jitterOffset.y,
                motionVectorScaleX = -cameraData.cameraTargetDescriptor.width,
                motionVectorScaleY = -cameraData.cameraTargetDescriptor.height * (SystemInfo.graphicsUVStartsAtTop ? -1 : 1),
                frameTimeDeltaMS = Time.unscaledDeltaTime * 1000f,
                cameraNear = cameraData.camera.nearClipPlane,
                cameraFar = cameraData.camera.farClipPlane,
                cameraFov = cameraData.camera.fieldOfView * Mathf.Deg2Rad,
                cameraViewMatrix = cameraData.GetViewMatrix(),
                cameraProjMatrixNoJitter = projMatrixNoJitter,
                cameraPosition = cameraData.worldSpaceCameraPos,
                reset = 0u,
            };

            if (SystemInfo.usesReversedZBuffer)
            {
                (prepareParams.cameraNear, prepareParams.cameraFar) = (prepareParams.cameraFar, prepareParams.cameraNear);
            }

            FrameGenPlugin.TextureTable textureTable = new()
            {
                depth = _depthCopy,
                motionVectors = _motionVectorCopy,
            };

            FrameGenPlugin.Prepare(cmd, ref prepareParams, textureTable);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
    }
}
