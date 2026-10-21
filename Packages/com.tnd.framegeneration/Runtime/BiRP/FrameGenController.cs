using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TND.FrameGen.BiRP
{
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("")]
    public class FrameGenController : MonoBehaviour
    {
        internal bool frameGenEnabled;
        internal uint numFramesToGenerate;
        internal bool useDynamicMFG;
        internal uint dynamicFramerateTarget;
        internal Texture distortionField;
        internal FrameGenPlugin.DebugFlags debugFlags;

        private Camera _camera;
        internal Camera Camera => _camera;

        private CommandBuffer _frameStartCommandBuffer;
        private CommandBuffer _prepareCommandBuffer;
        private CommandBuffer _configCommandBuffer;

        private RenderTexture _depthCopy;
        private RenderTexture _motionVectorsCopy;
        private RenderTexture _hudlessBackbufferCopy;

        private Material _copyDepthMaterial;
        private MaterialPropertyBlock _propertyBlock;

        private static readonly int DepthTexPropertyID = Shader.PropertyToID("_DepthTex");
        private static readonly int BlitScaleBiasPropertyID = Shader.PropertyToID("_BlitScaleBias");

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            _frameStartCommandBuffer = new CommandBuffer { name = "FG Before Rendering" };
            _prepareCommandBuffer = new CommandBuffer { name = "FG Prepare Pass" };
            _configCommandBuffer = new CommandBuffer { name = "FG Config Pass" };
        }

        private void OnEnable()
        {
            _camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

            _camera.AddCommandBuffer(CameraEvent.BeforeGBuffer, _frameStartCommandBuffer);  // Deferred rendering
            _camera.AddCommandBuffer(CameraEvent.BeforeDepthTexture, _frameStartCommandBuffer); // Forward rendering
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, _prepareCommandBuffer);
            _camera.AddCommandBuffer(CameraEvent.AfterEverything, _configCommandBuffer);

            _copyDepthMaterial = new Material(Shader.Find("Hidden/TND/Common/Blit"));
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            _camera.RemoveCommandBuffer(CameraEvent.BeforeGBuffer, _frameStartCommandBuffer);
            _camera.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, _frameStartCommandBuffer);
            _camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, _prepareCommandBuffer);
            _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _configCommandBuffer);

            if (_depthCopy != null)
            {
                _depthCopy.Release();
                _depthCopy = null;
            }

            if (_motionVectorsCopy != null)
            {
                _motionVectorsCopy.Release();
                _motionVectorsCopy = null;
            }

            if (_hudlessBackbufferCopy != null)
            {
                _hudlessBackbufferCopy.Release();
                _hudlessBackbufferCopy = null;
            }

            if (_copyDepthMaterial != null)
            {
                Destroy(_copyDepthMaterial);
                _copyDepthMaterial = null;
            }

            _propertyBlock = null;
        }

        private void Update()
        {
            // Setting up the Config pass in Update ensures we get the camera's display resolution, before upscaling has any effect.
            AddConfigCommands();

            // Submit marker that render submission is about to start, on the render thread
            _frameStartCommandBuffer.Clear();
            FrameGenPlugin.OnBeforeRendering(_frameStartCommandBuffer);

            // Submit marker that render submission is ending, on the render thread
            FrameGenPlugin.OnAfterRendering(_configCommandBuffer);
        }

        private void OnPreRender()
        {
            // Setting up the Prepare pass in OnPreRender allows Frame Gen to work nicely with BiRP upscalers.
            // Upscaling will modify the camera's viewport rect in OnPreCull to render at a lower resolution.
            // OnPreRender executes after OnPreCull, but before the camera starts rendering. This is the right moment for us to grab the camera's actual rendering resolution.
            AddPrepareCommands();
        }

        private void AddPrepareCommands()
        {
            if (!frameGenEnabled)
            {
                _prepareCommandBuffer.Clear();
                return;
            }

            Vector2Int renderSize = new Vector2Int(_camera.pixelWidth, _camera.pixelHeight);

            FrameGenUtils.AllocateRenderTexture(ref _depthCopy, renderSize.x, renderSize.y, GraphicsFormat.R32_SFloat, "FrameGen Depth Copy");
            FrameGenUtils.AllocateRenderTexture(ref _motionVectorsCopy, renderSize.x, renderSize.y, GraphicsFormat.R16G16_SFloat, "FrameGen Motion Vectors Copy");

            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;
            if (FrameGenUtils.YFlipRequired)
            {
                offset.y = scale.y;
                scale.y *= -1;
            }

            _prepareCommandBuffer.Clear();
            _prepareCommandBuffer.SetGlobalTexture(DepthTexPropertyID, GetDepthTexture(), RenderTextureSubElement.Depth);
            _propertyBlock.SetVector(BlitScaleBiasPropertyID, new Vector4(scale.x, scale.y, offset.x, offset.y));
            _prepareCommandBuffer.SetRenderTarget(_depthCopy);
            _prepareCommandBuffer.DrawProcedural(Matrix4x4.identity, _copyDepthMaterial, 3, MeshTopology.Triangles, 3, 1, _propertyBlock);
            _prepareCommandBuffer.Blit(BuiltinRenderTextureType.MotionVectors, _motionVectorsCopy, scale, offset);

            Vector2 jitterOffset = FrameGenUtils.DeriveJitterOffset(_camera.projectionMatrix, _camera.nonJitteredProjectionMatrix);

            FrameGenPlugin.PrepareParams prepareParams = new()
            {
                frameNumber = (ulong)Time.frameCount,
                renderWidth = (uint)renderSize.x,
                renderHeight = (uint)renderSize.y,
                jitterOffsetX = jitterOffset.x,
                jitterOffsetY = jitterOffset.y,
                motionVectorScaleX = -renderSize.x,
                motionVectorScaleY = -renderSize.y * (SystemInfo.graphicsUVStartsAtTop ? -1 : 1),
                frameTimeDeltaMS = Time.unscaledDeltaTime * 1000f,
                cameraNear = _camera.nearClipPlane,
                cameraFar = _camera.farClipPlane,
                cameraFov = _camera.fieldOfView * Mathf.Deg2Rad,
                cameraViewMatrix = _camera.worldToCameraMatrix,
                cameraProjMatrixNoJitter = _camera.nonJitteredProjectionMatrix,
                cameraPosition = _camera.transform.position,
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

            FrameGenPlugin.Prepare(_prepareCommandBuffer, ref prepareParams, textureTable);
        }

        private void AddConfigCommands()
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            FrameGenUtils.AllocateRenderTexture(ref _hudlessBackbufferCopy, screenWidth, screenHeight, GetBackbufferFormat(), "FrameGen Backbuffer Copy");

            _configCommandBuffer.Clear();

            if (frameGenEnabled)
            {
                _configCommandBuffer.SetRenderTarget(BuiltinRenderTextureType.None);
                _configCommandBuffer.Blit(null, _hudlessBackbufferCopy);
                _configCommandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            }

            FrameGenPlugin.ConfigurationParams configParams = new()
            {
                enable = frameGenEnabled ? 1u : 0u,
                useAsync = 0,
                frameNumber = (ulong)Time.frameCount,
                renderFlags = 0,
                debugFlags = debugFlags,
                viewportLeft = 0,
                viewportTop = 0,
                viewportWidth = screenWidth,
                viewportHeight = screenHeight,
                numFramesToGenerate = numFramesToGenerate,
                useDynamicMFG = useDynamicMFG ? 1u : 0u,
                dynamicFramerateTarget = dynamicFramerateTarget,
            };

            if (SystemInfo.usesReversedZBuffer)
                configParams.renderFlags |= FrameGenPlugin.RenderFlags.InvertedDepth;
            if (FrameGenUtils.HDRDisplayActive)
                configParams.renderFlags |= FrameGenPlugin.RenderFlags.HighDynamicRange;

            FrameGenPlugin.TextureTable textureTable = new()
            {
                hudLess = _hudlessBackbufferCopy,
                distortionField = distortionField,
            };

            FrameGenPlugin.Configure(_configCommandBuffer, ref configParams, textureTable);
        }

        private GraphicsFormat GetBackbufferFormat()
        {
            if (FrameGenUtils.HDRDisplayActive)
            {
                return HDROutputSettings.main.graphicsFormat;
            }

            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        private BuiltinRenderTextureType GetDepthTexture()
        {
            return _camera.actualRenderingPath is RenderingPath.Forward or RenderingPath.VertexLit ? BuiltinRenderTextureType.Depth : BuiltinRenderTextureType.CameraTarget;
        }
    }
}
