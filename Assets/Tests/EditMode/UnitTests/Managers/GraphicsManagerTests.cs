using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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
            _manager = new GraphicsManager(_owner.transform, () => true);
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

            HDAdditionalCameraData cameraData =
                _cameraObject.GetComponent<HDAdditionalCameraData>();
            Assert.AreEqual(_originalQualityLevel, QualitySettings.GetQualityLevel());
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Assert.AreEqual(120, Application.targetFrameRate);
            Assert.IsFalse(cameraData.allowDynamicResolution);
            Assert.AreEqual(
                HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                cameraData.antialiasing
            );
        }

        /// <summary>
        /// Verifies Frame Generation enables its native mode, HDRP pass, and motion inputs while
        /// preserving the camera's authored overrides when disabled.
        /// </summary>
        [Test]
        public void Apply_FrameGenerationSelected_EnablesAndRestoresRequiredFrameSettings()
        {
            UserFrameGenerationMode appliedMode = UserFrameGenerationMode.Off;
            _manager.Dispose();
            _manager = new GraphicsManager(
                _owner.transform,
                () => true,
                mode => appliedMode = mode
            );

            _manager.Apply(
                new UserVideoSettings
                {
                    QualityLevel = _originalQualityLevel,
                    FullScreenMode = (int)FullScreenMode.FullScreenWindow,
                    FrameGenerationMode = (int)UserFrameGenerationMode.On,
                }
            );

            HDAdditionalCameraData cameraData =
                _cameraObject.GetComponent<HDAdditionalCameraData>();
            Assert.AreEqual(UserFrameGenerationMode.On, appliedMode);
            Assert.IsTrue(
                cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.CustomPass)
            );
            Assert.IsTrue(
                cameraData.renderingPathCustomFrameSettings.IsEnabled(
                    FrameSettingsField.MotionVectors
                )
            );
            Assert.IsTrue(
                cameraData.renderingPathCustomFrameSettings.IsEnabled(
                    FrameSettingsField.ObjectMotionVectors
                )
            );

            _manager.Apply(new UserVideoSettings { QualityLevel = _originalQualityLevel });

            Assert.AreEqual(UserFrameGenerationMode.Off, appliedMode);
            Assert.IsFalse(
                cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                    (uint)FrameSettingsField.CustomPass
                ]
            );
            Assert.IsFalse(
                cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                    (uint)FrameSettingsField.MotionVectors
                ]
            );
            Assert.IsFalse(
                cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                    (uint)FrameSettingsField.ObjectMotionVectors
                ]
            );
        }

        [Test]
        public void Apply_FrameGenerationSelectedInExclusiveFullscreen_DisablesEffectiveMode()
        {
            UserFrameGenerationMode appliedMode = UserFrameGenerationMode.On;
            _manager.Dispose();
            _manager = new GraphicsManager(
                _owner.transform,
                () => true,
                mode => appliedMode = mode
            );
            UserVideoSettings settings = new UserVideoSettings
            {
                QualityLevel = _originalQualityLevel,
                FullScreenMode = (int)FullScreenMode.ExclusiveFullScreen,
                FrameGenerationMode = (int)UserFrameGenerationMode.On,
            };

            _manager.Apply(settings);

            Assert.AreEqual(UserFrameGenerationMode.Off, appliedMode);
            Assert.AreEqual((int)UserFrameGenerationMode.On, settings.FrameGenerationMode);
        }

        [Test]
        public void PauseFrameGenerationPresentation_ActiveMode_PausesAndResumesNativePresentation()
        {
            bool? presentationPaused = null;
            _manager.Dispose();
            _manager = new GraphicsManager(
                _owner.transform,
                () => true,
                _ => { },
                paused => presentationPaused = paused
            );
            _manager.Apply(
                new UserVideoSettings
                {
                    QualityLevel = _originalQualityLevel,
                    FullScreenMode = (int)FullScreenMode.FullScreenWindow,
                    FrameGenerationMode = (int)UserFrameGenerationMode.On,
                }
            );

            _manager.PauseFrameGenerationPresentation();

            Assert.IsTrue(presentationPaused);

            _manager.ResumeFrameGenerationPresentation();

            Assert.IsFalse(presentationPaused);
        }

        [Test]
        public void Apply_FrameGenerationSelectedWhilePresentationPaused_KeepsNativePresentationPaused()
        {
            bool? presentationPaused = null;
            _manager.Dispose();
            _manager = new GraphicsManager(
                _owner.transform,
                () => true,
                _ => { },
                paused => presentationPaused = paused
            );
            _manager.PauseFrameGenerationPresentation();

            _manager.Apply(
                new UserVideoSettings
                {
                    QualityLevel = _originalQualityLevel,
                    FullScreenMode = (int)FullScreenMode.FullScreenWindow,
                    FrameGenerationMode = (int)UserFrameGenerationMode.On,
                }
            );

            Assert.IsTrue(presentationPaused);
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
        /// Verifies that adding HDRP camera data retains an authored solid-color clear rather than
        /// exposing the pipeline sky.
        /// </summary>
        [Test]
        public void Apply_PrimarySolidColorCamera_PreservesColorClearMode()
        {
            Camera camera = _cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.2f, 0.3f, 1f);

            _manager.Apply(new UserVideoSettings());

            HDAdditionalCameraData cameraData =
                _cameraObject.GetComponent<HDAdditionalCameraData>();
            Assert.AreEqual(HDAdditionalCameraData.ClearColorMode.Color, cameraData.clearColorMode);
            Assert.AreEqual(camera.backgroundColor.linear, cameraData.backgroundColorHDR);
        }

        /// <summary>
        /// Verifies that auxiliary display cameras are outside the global graphics-camera policy.
        /// </summary>
        [Test]
        public void Apply_UntaggedDisplayCamera_DoesNotAddHdrpCameraData()
        {
            _cameraObject.tag = "Untagged";

            _manager.Apply(new UserVideoSettings());

            Assert.IsNull(_cameraObject.GetComponent<HDAdditionalCameraData>());
        }

        /// <summary>
        /// Verifies that effect toggles update the owned HDRP volume components.
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
            Assert.IsTrue(
                volume.sharedProfile.TryGet(out ScreenSpaceAmbientOcclusion ambientOcclusion)
            );
            Assert.IsTrue(volume.sharedProfile.TryGet(out Bloom bloom));
            Assert.IsTrue(volume.sharedProfile.TryGet(out MotionBlur motionBlur));
            Assert.IsTrue(ambientOcclusion.active);
            Assert.AreEqual(_manager.RayTracingAvailable, ambientOcclusion.rayTracing.value);
            Assert.IsFalse(bloom.active);
            Assert.IsFalse(motionBlur.active);

            HDAdditionalCameraData cameraData =
                _cameraObject.GetComponent<HDAdditionalCameraData>();
            Assert.IsTrue(
                cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.SSAO)
            );
            Assert.IsFalse(
                cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.Bloom)
            );
            Assert.IsFalse(
                cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.MotionBlur)
            );
            Assert.AreEqual(
                _manager.RayTracingAvailable,
                cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.RayTracing)
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
            HDAdditionalCameraData cameraData =
                _cameraObject.AddComponent<HDAdditionalCameraData>();
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

                Assert.IsFalse(cameraData.allowDynamicResolution);
                Assert.AreEqual(
                    HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing,
                    cameraData.antialiasing
                );
                Assert.IsTrue(
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(
                        FrameSettingsField.Postprocess
                    )
                );
                Assert.IsTrue(
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.SSAO)
                );
                Assert.IsTrue(
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.Bloom)
                );
                Assert.IsTrue(
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(
                        FrameSettingsField.MotionBlur
                    )
                );
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
            }
        }
    }
}
