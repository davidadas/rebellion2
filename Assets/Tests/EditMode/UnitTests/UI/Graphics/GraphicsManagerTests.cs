using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class GraphicsManagerTests
    {
        private GameObject _owner;
        private GameObject _cameraObject;
        private GraphicsManager _manager;
        private int _originalQualityLevel;
        private int _originalVSyncCount;
        private int _originalTargetFrameRate;

        /// <summary>
        /// Creates an isolated graphics owner and primary game camera.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _originalQualityLevel = QualitySettings.GetQualityLevel();
            _originalVSyncCount = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;
            _owner = new GameObject("Graphics Test Owner");
            _cameraObject = new GameObject("Graphics Test Camera", typeof(Camera));
            _cameraObject.tag = "MainCamera";
            _manager = new GraphicsManager(_owner.transform);
        }

        /// <summary>
        /// Restores global Unity graphics state and destroys test objects.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            QualitySettings.SetQualityLevel(_originalQualityLevel, true);
            QualitySettings.vSyncCount = _originalVSyncCount;
            Application.targetFrameRate = _originalTargetFrameRate;
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_owner);
        }

        /// <summary>
        /// Verifies that applying runtime and camera settings updates their Unity owners.
        /// </summary>
        [Test]
        public void Apply_RuntimeAndCameraSettings_UpdatesUnityGraphicsState()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                QualityLevel = _originalQualityLevel,
                AntiAliasingMode = (int)UserAntiAliasingMode.Smaa,
                VerticalSync = false,
                FrameRateLimit = 120,
            };

            _manager.Apply(settings);

            UniversalAdditionalCameraData cameraData =
                _cameraObject.GetComponent<UniversalAdditionalCameraData>();
            Assert.AreEqual(_originalQualityLevel, QualitySettings.GetQualityLevel());
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Assert.AreEqual(120, Application.targetFrameRate);
            Assert.IsFalse(_cameraObject.GetComponent<Camera>().allowDynamicResolution);
            Assert.AreEqual(
                AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                cameraData.antialiasing
            );
        }

        /// <summary>
        /// Verifies that vertical synchronization owns frame pacing when both persisted controls
        /// have values.
        /// </summary>
        [Test]
        public void Apply_VSyncEnabled_DisablesSoftwareFrameLimit()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                QualityLevel = _originalQualityLevel,
                VerticalSync = true,
                FrameRateLimit = 30,
            };

            _manager.Apply(settings);

            Assert.AreEqual(1, QualitySettings.vSyncCount);
            Assert.AreEqual(-1, Application.targetFrameRate);
        }

        /// <summary>
        /// Verifies that adding URP camera data retains an authored solid-color clear rather than
        /// exposing the pipeline sky.
        /// </summary>
        [Test]
        public void Apply_PrimarySolidColorCamera_PreservesColorClearMode()
        {
            Camera camera = _cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.2f, 0.3f, 1f);

            _manager.Apply(new UserVideoSettings());

            UniversalAdditionalCameraData cameraData =
                _cameraObject.GetComponent<UniversalAdditionalCameraData>();
            Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 1f), camera.backgroundColor);
            Assert.IsNotNull(cameraData);
        }

        /// <summary>
        /// Verifies that auxiliary display cameras are outside the global graphics-camera policy.
        /// </summary>
        [Test]
        public void Apply_UntaggedDisplayCamera_DoesNotAddUrpCameraData()
        {
            _cameraObject.tag = "Untagged";

            _manager.Apply(new UserVideoSettings());

            Assert.IsNull(_cameraObject.GetComponent<UniversalAdditionalCameraData>());
        }

        /// <summary>
        /// Verifies that effect toggles update the owned URP volume components.
        /// </summary>
        [Test]
        public void Apply_EffectSettings_UpdatesRuntimeVolumeOverrides()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                AmbientOcclusion = true,
                Bloom = false,
                MotionBlur = false,
                RayTracing = true,
            };

            _manager.Apply(settings);

            Volume volume = _owner.GetComponentInChildren<Volume>();
            Assert.IsTrue(volume.sharedProfile.TryGet(out Bloom bloom));
            Assert.IsTrue(volume.sharedProfile.TryGet(out MotionBlur motionBlur));
            Assert.IsFalse(_manager.RayTracingAvailable);
            Assert.IsFalse(bloom.active);
            Assert.IsFalse(motionBlur.active);

            UniversalAdditionalCameraData cameraData =
                _cameraObject.GetComponent<UniversalAdditionalCameraData>();
            Assert.IsTrue(cameraData.renderPostProcessing);
        }

        [Test]
        public void Apply_OrthographicCamera_UsesSpatialAntialiasing()
        {
            _cameraObject.GetComponent<Camera>().orthographic = true;
            _manager.Apply(
                new UserVideoSettings { AntiAliasingMode = (int)UserAntiAliasingMode.Taa }
            );
            Assert.AreEqual(
                AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                _cameraObject.GetComponent<UniversalAdditionalCameraData>().antialiasing
            );
        }

        [Test]
        public void Apply_DisabledBloom_OverridesAuthoredVolumes()
        {
            _manager.Apply(new UserVideoSettings { Bloom = false });
            Volume[] volumes = _owner.GetComponentsInChildren<Volume>();
            Volume suppression = System.Array.Find(volumes, volume => volume.priority > 1002f);
            Assert.IsNotNull(suppression);
            Assert.IsTrue(suppression.sharedProfile.TryGet(out Bloom bloom));
            Assert.IsTrue(bloom.active);
            Assert.IsTrue(bloom.intensity.overrideState);
            Assert.AreEqual(0f, bloom.intensity.value);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Apply_AmbientOcclusion_SelectsMatchingRenderer(bool enabled)
        {
            _manager.Apply(new UserVideoSettings { AmbientOcclusion = enabled });
            UniversalRenderPipelineAsset pipeline =
                GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            UniversalAdditionalCameraData cameraData =
                _cameraObject.GetComponent<UniversalAdditionalCameraData>();
            for (int index = 0; index < pipeline.rendererDataList.Length; index++)
            {
                ScriptableRendererData data = pipeline.rendererDataList[index];
                bool hasAmbientOcclusion = data.rendererFeatures.Exists(feature =>
                    feature is ScreenSpaceAmbientOcclusion && feature.isActive
                );
                if (hasAmbientOcclusion == enabled)
                {
                    Assert.AreSame(pipeline.GetRenderer(index), cameraData.scriptableRenderer);
                    return;
                }
            }
            Assert.Fail(
                "The pipeline has no renderer for the requested ambient occlusion setting."
            );
        }

        /// <summary>
        /// Verifies an isolated render-target camera explicitly subscribed to the runtime volume
        /// receives the user-selected rendering settings.
        /// </summary>
        [Test]
        public void Apply_OptedInRenderTargetCamera_UpdatesCameraSettings()
        {
            _cameraObject.tag = "Untagged";
            Camera camera = _cameraObject.GetComponent<Camera>();
            RenderTexture target = new RenderTexture(64, 64, 0);
            camera.targetTexture = target;
            UniversalAdditionalCameraData cameraData =
                _cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.volumeLayerMask = 1 << GraphicsManager.RuntimeVolumeLayer;

            try
            {
                _manager.Apply(
                    new UserVideoSettings
                    {
                        AntiAliasingMode = (int)UserAntiAliasingMode.Fxaa,
                        AmbientOcclusion = true,
                        Bloom = true,
                        MotionBlur = true,
                    }
                );

                Assert.IsFalse(_cameraObject.GetComponent<Camera>().allowDynamicResolution);
                Assert.AreEqual(
                    AntialiasingMode.FastApproximateAntialiasing,
                    cameraData.antialiasing
                );
                Assert.IsTrue(cameraData.renderPostProcessing);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
            }
        }
    }
}
