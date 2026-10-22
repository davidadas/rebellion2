using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace TND.FrameGen.HDRP
{
    [Serializable]
    class FrameGenPreparePass : CustomPass
    {
        private RTHandle _depthCopy;
        private RTHandle _motionVectorsCopy;

        protected override bool executeInSceneView => false;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            name = "FrameGen Prepare Pass";
            targetColorBuffer = TargetBuffer.None;
            targetDepthBuffer = TargetBuffer.None;
            clearFlags = ClearFlag.None;

            _depthCopy = RTHandles.Alloc(Vector2.one, colorFormat: GraphicsFormat.R32_SFloat, name: "FG Depth Copy");
            _motionVectorsCopy = RTHandles.Alloc(Vector2.one, colorFormat: GraphicsFormat.R16G16_SFloat, name: "FG Motion Vectors Copy");
        }

        protected override void Execute(CustomPassContext ctx)
        {
            RTHandle cameraColor = ctx.cameraColorBuffer;
            Vector2 viewportScale = cameraColor.useScaling ? new Vector2(cameraColor.rtHandleProperties.rtHandleScale.x, cameraColor.rtHandleProperties.rtHandleScale.y) : Vector2.one;
            Vector4 scaleBias = new Vector4(1, 1, 0, 0);
            if (FrameGenUtils.YFlipRequired)
            {
                scaleBias.w = viewportScale.y;
                scaleBias.y *= -1;
            }

            ctx.cmd.SetRenderTarget(_depthCopy, 0, CubemapFace.Unknown, -1);
            Blitter.BlitTexture(ctx.cmd, ctx.cameraDepthBuffer, scaleBias, 0, false);
            ctx.cmd.SetRenderTarget(_motionVectorsCopy, 0, CubemapFace.Unknown, -1);
            Blitter.BlitTexture(ctx.cmd, ctx.cameraMotionVectorsBuffer, scaleBias, 0, false);

            FrameGenPlugin.PrepareParams prepareParams = new()
            {
                frameNumber = (ulong)Time.frameCount,
                renderWidth = (uint)ctx.hdCamera.actualWidth,
                renderHeight = (uint)ctx.hdCamera.actualHeight,
                jitterOffsetX = ctx.hdCamera.taaJitter.x,
                jitterOffsetY = ctx.hdCamera.taaJitter.y,
                motionVectorScaleX = -ctx.hdCamera.actualWidth,
                motionVectorScaleY = -ctx.hdCamera.actualHeight * (SystemInfo.graphicsUVStartsAtTop ? -1 : 1),
                frameTimeDeltaMS = Time.unscaledDeltaTime * 1000f,
                cameraNear = ctx.hdCamera.camera.nearClipPlane,
                cameraFar = ctx.hdCamera.camera.farClipPlane,
                cameraFov = ctx.hdCamera.camera.fieldOfView * Mathf.Deg2Rad,
                cameraViewMatrix = ctx.hdCamera.mainViewConstants.viewMatrix,
                cameraProjMatrixNoJitter = GL.GetGPUProjectionMatrix(ctx.hdCamera.camera.projectionMatrix, true),
                cameraPosition = ctx.hdCamera.mainViewConstants.worldSpaceCameraPos,
                reset = 0u,
            };

            if (SystemInfo.usesReversedZBuffer)
            {
                (prepareParams.cameraNear, prepareParams.cameraFar) = (prepareParams.cameraFar, prepareParams.cameraNear);
            }

            FrameGenPlugin.TextureTable textureTable = new()
            {
                depth = _depthCopy,
                motionVectors = _motionVectorsCopy,
            };

            FrameGenPlugin.Prepare(ctx.cmd, ref prepareParams, textureTable);
        }

        protected override void Cleanup()
        {
            _depthCopy.Release();
            _motionVectorsCopy.Release();
        }
    }
}
