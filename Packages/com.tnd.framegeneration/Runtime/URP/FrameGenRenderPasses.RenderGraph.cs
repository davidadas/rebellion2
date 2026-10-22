#if TND_URP_RENDERGRAPH
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using TND.Common.URP;

namespace TND.FrameGen.URP
{
    public partial class FrameGenPreparePass
    {
        private class PassData
        {
            public int frameNum;
            public TextureHandle inputDepth;
            public TextureHandle inputMotionVectors;
            public TextureHandle depthCopy;
            public TextureHandle motionVectorsCopy;
            public Vector3 cameraParams;
            public Vector2Int renderSize;
            public Vector2 jitterOffset;
            public Matrix4x4 cameraViewMatrix;
            public Matrix4x4 cameraProjMatrixNoJitter;
            public Vector3 cameraPosition;
            public bool reset;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData);

            builder.AllowPassCulling(false);

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            var depthDesc = cameraData.cameraTargetDescriptor;
            depthDesc.graphicsFormat = GraphicsFormat.R32_SFloat;
            depthDesc.depthBufferBits = 0;
            depthDesc.depthStencilFormat = GraphicsFormat.None;
            depthDesc.msaaSamples = 1;
            depthDesc.useMipMap = false;
            depthDesc.bindMS = false;

            var mvDesc = depthDesc;
            mvDesc.graphicsFormat = GraphicsFormat.R16G16_SFloat;

            // Derive jitter offset from the information that we have access to
            bool yFlip = FrameGenUtils.YFlipRequired;
            Matrix4x4 projMatrix = UpscalingHelpers.GetGPUProjectionMatrix(cameraData, yFlip);
            Matrix4x4 projMatrixNoJitter = UpscalingHelpers.GetGPUProjectionMatrixNoJitter(cameraData, yFlip);
            Vector2 jitterOffset = FrameGenUtils.DeriveJitterOffset(projMatrix, projMatrixNoJitter);

            passData.frameNum = Time.frameCount;
            passData.inputDepth = resourceData.cameraDepthTexture;
            passData.inputMotionVectors = resourceData.motionVectorColor;
            passData.depthCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDesc, "FrameGen Depth Copy", false);
            passData.motionVectorsCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, mvDesc, "FrameGen Motion Vectors Copy", false);
            passData.cameraParams = new Vector3(cameraData.camera.nearClipPlane, cameraData.camera.farClipPlane, cameraData.camera.fieldOfView * Mathf.Deg2Rad);
            passData.renderSize = new Vector2Int(cameraData.scaledWidth, cameraData.scaledHeight);
            passData.jitterOffset = jitterOffset;
            passData.cameraViewMatrix = cameraData.GetViewMatrix();
            passData.cameraProjMatrixNoJitter = projMatrixNoJitter;
            passData.cameraPosition = cameraData.worldSpaceCameraPos;
            passData.reset = false;

            builder.UseTexture(passData.inputDepth);
            builder.UseTexture(passData.inputMotionVectors);
            builder.UseTexture(passData.depthCopy, AccessFlags.Write);
            builder.UseTexture(passData.motionVectorsCopy, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
        }

        static void ExecutePass(in PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            Vector4 scaleBias = new Vector4(1, 1, 0, 0);
            if (FrameGenUtils.YFlipRequired)
            {
                scaleBias.w = scaleBias.y;
                scaleBias.y *= -1;
            }

            cmd.SetRenderTarget(data.depthCopy, 0, CubemapFace.Unknown, -1);
            Blitter.BlitTexture(cmd, data.inputDepth, scaleBias, 0, false);
            cmd.SetRenderTarget(data.motionVectorsCopy, 0, CubemapFace.Unknown, -1);
            Blitter.BlitTexture(cmd, data.inputMotionVectors, scaleBias, 0, false);

            FrameGenPlugin.PrepareParams prepareParams = new()
            {
                frameNumber = (ulong)data.frameNum,
                renderWidth = (uint)data.renderSize.x,
                renderHeight = (uint)data.renderSize.y,
                jitterOffsetX = data.jitterOffset.x,
                jitterOffsetY = data.jitterOffset.y,
                motionVectorScaleX = -data.renderSize.x,
                motionVectorScaleY = -data.renderSize.y * (SystemInfo.graphicsUVStartsAtTop ? -1 : 1),
                frameTimeDeltaMS = Time.unscaledDeltaTime * 1000f,
                cameraNear = data.cameraParams.x,
                cameraFar = data.cameraParams.y,
                cameraFov = data.cameraParams.z,
                cameraViewMatrix = data.cameraViewMatrix,
                cameraProjMatrixNoJitter = data.cameraProjMatrixNoJitter,
                cameraPosition = data.cameraPosition,
                reset = data.reset ? 1u : 0u,
            };

            if (SystemInfo.usesReversedZBuffer)
            {
                (prepareParams.cameraNear, prepareParams.cameraFar) = (prepareParams.cameraFar, prepareParams.cameraNear);
            }

            FrameGenPlugin.TextureTable textureTable = new()
            {
                depth = data.depthCopy,
                motionVectors = data.motionVectorsCopy,
            };

            FrameGenPlugin.Prepare(cmd, ref prepareParams, textureTable);
        }
    }
}
#endif  // TND_URP_RENDERGRAPH
