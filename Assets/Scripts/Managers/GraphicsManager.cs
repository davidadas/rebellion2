using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using TND.FrameGen;
#endif

/// <summary>
/// Applies persisted graphics quality to HDRP and game-facing cameras.
/// </summary>
public sealed class GraphicsManager : IDisposable
{
    public const int RuntimeVolumeLayer = 27;

    private const string _runtimeVolumeName = "Runtime Graphics Volume";

    private readonly GameObject _runtimeVolumeObject;
    private readonly VolumeProfile _runtimeVolumeProfile;
    private readonly ScreenSpaceAmbientOcclusion _ambientOcclusion;
    private readonly Bloom _bloom;
    private readonly MotionBlur _motionBlur;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private readonly FrameGenerationController _frameGeneration;
#endif
    private readonly HashSet<EntityId> _configuredCameraIds = new HashSet<EntityId>();
    private readonly Dictionary<
        EntityId,
        (
            bool CustomPassOverride,
            bool CustomPassEnabled,
            bool MotionOverride,
            bool MotionEnabled,
            bool ObjectOverride,
            bool ObjectEnabled
        )
    > _frameGenerationCameraSettings =
        new Dictionary<
            EntityId,
            (
                bool CustomPassOverride,
                bool CustomPassEnabled,
                bool MotionOverride,
                bool MotionEnabled,
                bool ObjectOverride,
                bool ObjectEnabled
            )
        >();
    private readonly Func<bool> _frameGenerationAvailability;
    private readonly Action<UserFrameGenerationMode> _setFrameGenerationMode;
    private readonly Action<bool> _setFrameGenerationPaused;

    private UserAntiAliasingMode _antiAliasingMode;
    private bool _frameGenerationEnabled;
    private bool _frameGenerationPresentationPaused;
    private bool _ambientOcclusionEnabled = true;
    private bool _bloomEnabled = true;
    private bool _motionBlurEnabled = true;
    private bool _rayTracingEnabled;
    private bool _hasAppliedSettings;
    private bool _disposed;

    /// <summary>
    /// Gets whether the active graphics device can execute HDRP ray tracing.
    /// </summary>
    public bool RayTracingAvailable =>
        SystemInfo.supportsRayTracing
        && GetActivePipelineAsset() is HDRenderPipelineAsset pipeline
        && pipeline.currentPlatformRenderPipelineSettings.supportRayTracing;

    /// <summary>
    /// Gets whether the primary display can enter HDR output mode.
    /// </summary>
    public bool HdrOutputAvailable => HDROutputSettings.main.available;

    /// <summary>
    /// Gets whether the Windows player can execute DLSS Frame Generation.
    /// </summary>
    public bool FrameGenerationAvailable => _frameGenerationAvailability();

    /// <summary>
    /// Gets the number of frames requested from the active presentation path for each rendered
    /// Unity frame.
    /// </summary>
    public uint PresentationFrameMultiplier => GetPresentationFrameMultiplier();

    /// <summary>
    /// Creates the application graphics owner and its runtime volume overrides.
    /// </summary>
    /// <param name="owner">The application lifetime transform.</param>
    /// <param name="frameGenerationAvailability">The runtime Frame Generation capability source.</param>
    /// <param name="setFrameGenerationMode">The native Frame Generation setting sink.</param>
    /// <param name="setFrameGenerationPaused">The native Frame Generation presentation pause sink.</param>
    public GraphicsManager(
        Transform owner,
        Func<bool> frameGenerationAvailability = null,
        Action<UserFrameGenerationMode> setFrameGenerationMode = null,
        Action<bool> setFrameGenerationPaused = null
    )
    {
        _runtimeVolumeObject = new GameObject(_runtimeVolumeName, typeof(Volume));
        _runtimeVolumeObject.layer = RuntimeVolumeLayer;
        if (owner != null)
            _runtimeVolumeObject.transform.SetParent(owner, false);
        else
            UnityEngine.Object.DontDestroyOnLoad(_runtimeVolumeObject);

        _runtimeVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _runtimeVolumeProfile.name = _runtimeVolumeName;
        _ambientOcclusion = _runtimeVolumeProfile.Add<ScreenSpaceAmbientOcclusion>();
        _ambientOcclusion.intensity.Override(1f);
        _ambientOcclusion.rayTracing.Override(false);
        _bloom = _runtimeVolumeProfile.Add<Bloom>();
        _bloom.threshold.Override(1f);
        _bloom.intensity.Override(0.25f);
        _motionBlur = _runtimeVolumeProfile.Add<MotionBlur>();
        _motionBlur.intensity.Override(0.25f);

        Volume volume = _runtimeVolumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = _runtimeVolumeProfile;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _frameGeneration = _runtimeVolumeObject.AddComponent<FrameGenerationController>();
#endif
        _frameGenerationAvailability =
            frameGenerationAvailability ?? GetFrameGenerationAvailability;
        _setFrameGenerationMode = setFrameGenerationMode ?? SetFrameGenerationMode;
        _setFrameGenerationPaused = setFrameGenerationPaused ?? SetFrameGenerationPaused;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    }

    /// <summary>
    /// Applies persisted graphics settings to Unity and all primary scene cameras.
    /// </summary>
    /// <param name="settings">The normalized graphics settings.</param>
    public void Apply(UserVideoSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        settings.Normalize();
        int qualityLevel = Mathf.Clamp(
            settings.QualityLevel,
            0,
            Math.Max(0, QualitySettings.names.Length - 1)
        );
        settings.QualityLevel = qualityLevel;
        QualitySettings.SetQualityLevel(qualityLevel, true);
        QualitySettings.vSyncCount = settings.VerticalSync ? 1 : 0;
        Application.targetFrameRate =
            settings.VerticalSync || settings.FrameRateLimit == 0 ? -1 : settings.FrameRateLimit;

        _antiAliasingMode = (UserAntiAliasingMode)settings.AntiAliasingMode;
        UserFrameGenerationMode frameGenerationMode = (UserFrameGenerationMode)
            settings.FrameGenerationMode;
        bool frameGenerationRuntimeAvailable = FrameGenerationAvailable;
        _frameGenerationEnabled =
            frameGenerationMode != UserFrameGenerationMode.Off
            && settings.SupportsFrameGeneration
            && frameGenerationRuntimeAvailable;
        _setFrameGenerationMode(
            _frameGenerationEnabled ? frameGenerationMode : UserFrameGenerationMode.Off
        );
        _setFrameGenerationPaused(!_frameGenerationEnabled || _frameGenerationPresentationPaused);
        _ambientOcclusionEnabled = settings.AmbientOcclusion;
        _bloomEnabled = settings.Bloom;
        _motionBlurEnabled = settings.MotionBlur;
        _rayTracingEnabled =
            settings.RayTracing && settings.AmbientOcclusion && RayTracingAvailable;
        _ambientOcclusion.active = _ambientOcclusionEnabled;
        _ambientOcclusion.rayTracing.Override(_rayTracingEnabled);
        _bloom.active = _bloomEnabled;
        _motionBlur.active = _motionBlurEnabled;

        _configuredCameraIds.Clear();
        _hasAppliedSettings = true;
        ApplyToManagedCameras();
        ApplyHdrOutput(settings);
    }

    /// <summary>
    /// Releases render callbacks and runtime volume objects.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        _setFrameGenerationMode(UserFrameGenerationMode.Off);
        _setFrameGenerationPaused(true);
        RestoreFrameGenerationFrameSettings();
        DestroyObject(_runtimeVolumeProfile);
        DestroyObject(_runtimeVolumeObject);
        _disposed = true;
    }

    /// <summary>
    /// Reconciles camera-specific settings after a Unity scene has finished loading.
    /// </summary>
    /// <param name="scene">The loaded scene.</param>
    /// <param name="mode">The scene loading mode.</param>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_disposed || !_hasAppliedSettings)
            return;

        _configuredCameraIds.Clear();
        if (mode == LoadSceneMode.Single)
            _frameGenerationCameraSettings.Clear();
        ApplyToManagedCameras();
    }

    /// <summary>
    /// Pauses native frame interpolation while Unity changes its display mode.
    /// </summary>
    internal void PrepareForDisplayChange()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_frameGenerationEnabled && _frameGeneration)
            _frameGeneration.PauseFrameGeneration();
#endif
    }

    /// <summary>
    /// Suspends interpolated presentation while full-screen video or equivalent UI owns the
    /// backbuffer.
    /// </summary>
    internal void PauseFrameGenerationPresentation()
    {
        _frameGenerationPresentationPaused = true;
        if (_frameGenerationEnabled)
            _setFrameGenerationPaused(true);
    }

    /// <summary>
    /// Restores interpolated presentation after full-screen video or equivalent UI releases the
    /// backbuffer.
    /// </summary>
    internal void ResumeFrameGenerationPresentation()
    {
        _frameGenerationPresentationPaused = false;
        if (_frameGenerationEnabled)
            _setFrameGenerationPaused(false);
    }

    /// <summary>
    /// Reconciles a managed camera created after the most recent settings or scene application.
    /// </summary>
    /// <param name="context">The current scriptable render context.</param>
    /// <param name="camera">The camera about to render.</param>
    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (
            _disposed
            || !_hasAppliedSettings
            || camera == null
            || _configuredCameraIds.Contains(camera.GetEntityId())
            || !IsManagedCamera(camera)
        )
        {
            return;
        }

        ApplyToCamera(camera);
    }

    /// <summary>
    /// Applies the active graphics settings to every loaded managed game camera.
    /// </summary>
    private void ApplyToManagedCameras()
    {
        foreach (
            Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include
            )
        )
        {
            if (IsManagedCamera(camera))
                ApplyToCamera(camera);
        }
    }

    /// <summary>
    /// Applies anti-aliasing and effect gates to one scene camera.
    /// </summary>
    /// <param name="camera">The scene camera to configure.</param>
    private void ApplyToCamera(Camera camera)
    {
        HDAdditionalCameraData cameraData = camera.GetComponent<HDAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            ApplyAuthoredClearMode(camera, cameraData);
        }
        cameraData.antialiasing = ConvertAntiAliasing(_antiAliasingMode);
        cameraData.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
        cameraData.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
        cameraData.allowDynamicResolution = false;
        cameraData.volumeLayerMask |= 1 << RuntimeVolumeLayer;
        cameraData.customRenderingSettings = true;
        SetFrameSetting(cameraData, FrameSettingsField.Postprocess, true);
        SetFrameSetting(
            cameraData,
            FrameSettingsField.Antialiasing,
            _antiAliasingMode != UserAntiAliasingMode.Off
        );
        SetFrameSetting(cameraData, FrameSettingsField.SSAO, _ambientOcclusionEnabled);
        SetFrameSetting(cameraData, FrameSettingsField.Bloom, _bloomEnabled);
        SetFrameSetting(cameraData, FrameSettingsField.MotionBlur, _motionBlurEnabled);
        ApplyFrameGenerationFrameSettings(camera, cameraData);
        SetFrameSetting(cameraData, FrameSettingsField.RayTracing, _rayTracingEnabled);
        _configuredCameraIds.Add(camera.GetEntityId());
    }

    /// <summary>
    /// Preserves the Unity camera's authored clear behavior when HDRP camera data is first added.
    /// </summary>
    /// <param name="camera">The camera whose authored clear settings are authoritative.</param>
    /// <param name="cameraData">The newly added HDRP camera data.</param>
    private static void ApplyAuthoredClearMode(Camera camera, HDAdditionalCameraData cameraData)
    {
        cameraData.clearColorMode =
            camera.clearFlags == CameraClearFlags.Skybox ? HDAdditionalCameraData.ClearColorMode.Sky
            : camera.clearFlags == CameraClearFlags.SolidColor
                ? HDAdditionalCameraData.ClearColorMode.Color
            : HDAdditionalCameraData.ClearColorMode.None;
        cameraData.backgroundColorHDR = camera.backgroundColor.linear;
    }

    /// <summary>
    /// Applies HDR output when the primary display supports it.
    /// </summary>
    /// <param name="settings">The persisted video settings to reconcile with hardware support.</param>
    private void ApplyHdrOutput(UserVideoSettings settings)
    {
        bool enabled = settings.HdrOutput && HdrOutputAvailable;
        if (HDROutputSettings.main.active != enabled)
            HDROutputSettings.main.RequestHDRModeChange(enabled);
    }

    /// <summary>
    /// Overrides one HDRP frame setting on a scene camera.
    /// </summary>
    /// <param name="cameraData">The HDRP camera data to update.</param>
    /// <param name="field">The frame setting to override.</param>
    /// <param name="enabled">Whether the frame setting is enabled.</param>
    private static void SetFrameSetting(
        HDAdditionalCameraData cameraData,
        FrameSettingsField field,
        bool enabled
    )
    {
        cameraData.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
        cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
    }

    /// <summary>
    /// Enables the HDRP submission pass and dense motion-vector inputs required by Frame
    /// Generation while preserving the authored camera policy when the feature is disabled.
    /// </summary>
    /// <param name="camera">The camera whose authored overrides must be preserved.</param>
    /// <param name="cameraData">The HDRP camera data to update.</param>
    private void ApplyFrameGenerationFrameSettings(Camera camera, HDAdditionalCameraData cameraData)
    {
        EntityId cameraId = camera.GetEntityId();
        bool isPresentationCamera = camera.targetTexture == null && camera.CompareTag("MainCamera");
        if (_frameGenerationEnabled && isPresentationCamera)
        {
            if (!_frameGenerationCameraSettings.ContainsKey(cameraId))
            {
                _frameGenerationCameraSettings[cameraId] = (
                    cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                        (uint)FrameSettingsField.CustomPass
                    ],
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(
                        FrameSettingsField.CustomPass
                    ),
                    cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                        (uint)FrameSettingsField.MotionVectors
                    ],
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(
                        FrameSettingsField.MotionVectors
                    ),
                    cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                        (uint)FrameSettingsField.ObjectMotionVectors
                    ],
                    cameraData.renderingPathCustomFrameSettings.IsEnabled(
                        FrameSettingsField.ObjectMotionVectors
                    )
                );
            }
            SetFrameSetting(cameraData, FrameSettingsField.CustomPass, true);
            SetFrameSetting(cameraData, FrameSettingsField.MotionVectors, true);
            SetFrameSetting(cameraData, FrameSettingsField.ObjectMotionVectors, true);
            return;
        }

        if (_frameGenerationCameraSettings.Remove(cameraId, out var authoredSettings))
        {
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.CustomPass,
                authoredSettings.CustomPassEnabled
            );
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.CustomPass
            ] = authoredSettings.CustomPassOverride;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.MotionVectors,
                authoredSettings.MotionEnabled
            );
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.MotionVectors
            ] = authoredSettings.MotionOverride;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.ObjectMotionVectors,
                authoredSettings.ObjectEnabled
            );
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.ObjectMotionVectors
            ] = authoredSettings.ObjectOverride;
        }
    }

    /// <summary>
    /// Restores authored custom-pass and motion-vector overrides before releasing this manager.
    /// </summary>
    private void RestoreFrameGenerationFrameSettings()
    {
        bool frameGenerationEnabled = _frameGenerationEnabled;
        _frameGenerationEnabled = false;
        foreach (
            Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include
            )
        )
        {
            HDAdditionalCameraData cameraData = camera.GetComponent<HDAdditionalCameraData>();
            if (cameraData != null)
                ApplyFrameGenerationFrameSettings(camera, cameraData);
        }
        _frameGenerationEnabled = frameGenerationEnabled;
        _frameGenerationCameraSettings.Clear();
    }

    /// <summary>
    /// Converts the persisted anti-aliasing value to HDRP's camera setting.
    /// </summary>
    /// <param name="mode">The persisted anti-aliasing mode.</param>
    /// <returns>The matching HDRP anti-aliasing mode.</returns>
    private static HDAdditionalCameraData.AntialiasingMode ConvertAntiAliasing(
        UserAntiAliasingMode mode
    )
    {
        return mode switch
        {
            UserAntiAliasingMode.Fxaa => HDAdditionalCameraData
                .AntialiasingMode
                .FastApproximateAntialiasing,
            UserAntiAliasingMode.Smaa => HDAdditionalCameraData
                .AntialiasingMode
                .SubpixelMorphologicalAntiAliasing,
            UserAntiAliasingMode.Taa => HDAdditionalCameraData
                .AntialiasingMode
                .TemporalAntialiasing,
            _ => HDAdditionalCameraData.AntialiasingMode.None,
        };
    }

    /// <summary>
    /// Gets whether the installed Frame Generation provider supports the active player.
    /// </summary>
    /// <returns>True when the DLSS Frame Generation provider is available.</returns>
    private static bool GetFrameGenerationAvailability()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        return TNDFrameGeneration.IsSupportedFrameGeneration(FrameGenName.DLSS);
#else
        return false;
#endif
    }

    /// <summary>
    /// Gets the number of frames requested from the active Windows presentation provider.
    /// </summary>
    /// <returns>The active frame multiplier, or one when interpolation is inactive.</returns>
    private uint GetPresentationFrameMultiplier()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        return
            _frameGenerationEnabled
            && _frameGeneration
            && _frameGeneration.IsFrameGenerationEnabled()
            && !_frameGeneration.IsFrameGenerationPaused()
            ? _frameGeneration.GetFrameMultiplier()
            : 1u;
#else
        return 1u;
#endif
    }

    /// <summary>
    /// Applies the requested Frame Generation mode through the installed provider.
    /// </summary>
    /// <param name="mode">The persisted user mode.</param>
    private void SetFrameGenerationMode(UserFrameGenerationMode mode)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!_frameGeneration)
            return;

        _frameGeneration.ApplyMode(mode);
#endif
    }

    /// <summary>
    /// Applies the application presentation pause state to the native Frame Generation controller.
    /// </summary>
    /// <param name="paused">Whether interpolation must stop without switching providers.</param>
    private void SetFrameGenerationPaused(bool paused)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!_frameGeneration)
            return;

        _frameGeneration.ApplyPresentationPause(paused);
#endif
    }

    /// <summary>
    /// Resolves the active quality-level pipeline, falling back to the project default before the
    /// first pipeline instance has been created.
    /// </summary>
    /// <returns>The active render-pipeline asset, or null when the project has none.</returns>
    private static RenderPipelineAsset GetActivePipelineAsset()
    {
        return GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
    }

    /// <summary>
    /// Gets whether a camera is a primary scene camera or an explicitly opted-in render-target
    /// camera.
    /// </summary>
    /// <param name="camera">The camera to inspect.</param>
    /// <returns>True when the camera should receive global graphics settings.</returns>
    private static bool IsManagedCamera(Camera camera)
    {
        if (camera == null || camera.cameraType != CameraType.Game)
            return false;
        if (camera.targetTexture == null)
            return camera.CompareTag("MainCamera");

        HDAdditionalCameraData cameraData = camera.GetComponent<HDAdditionalCameraData>();
        return cameraData != null
            && (cameraData.volumeLayerMask.value & (1 << RuntimeVolumeLayer)) != 0;
    }

    /// <summary>
    /// Destroys a Unity object using the operation appropriate to the current runtime state.
    /// </summary>
    /// <param name="value">The Unity object to destroy.</param>
    private static void DestroyObject(UnityEngine.Object value)
    {
        if (value == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(value);
        else
            UnityEngine.Object.DestroyImmediate(value);
    }
}

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
/// <summary>
/// Adapts the package controller to application-owned settings initialization.
/// </summary>
internal sealed class FrameGenerationController : TNDFrameGeneration
{
    private UserFrameGenerationMode _applicationMode = UserFrameGenerationMode.Off;
    private bool _applicationPaused = true;
    private bool _started;

    /// <summary>
    /// Stores and applies the Frame Generation mode selected by application settings.
    /// </summary>
    /// <param name="mode">The selected application mode.</param>
    internal void ApplyMode(UserFrameGenerationMode mode)
    {
        _applicationMode = mode;
        if (_started)
            ApplyApplicationMode();
    }

    /// <summary>
    /// Stores and applies whether application presentation currently permits generated frames.
    /// </summary>
    /// <param name="paused">Whether generated-frame presentation must remain paused.</param>
    internal void ApplyPresentationPause(bool paused)
    {
        _applicationPaused = paused;
        if (_started)
            ApplyApplicationPause();
    }

    /// <summary>
    /// Initializes the package controller before restoring application-owned settings.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        _started = true;
        ApplyApplicationMode();
        ApplyApplicationPause();
    }

    /// <summary>
    /// Applies the stored application mode to the initialized package controller.
    /// </summary>
    private void ApplyApplicationMode()
    {
        if (_applicationMode == UserFrameGenerationMode.On)
            SetFrameGeneration(FrameGenName.DLSS);
        else
            DisableFrameGeneration();
    }

    /// <summary>
    /// Applies the stored application presentation state to the initialized package controller.
    /// </summary>
    private void ApplyApplicationPause()
    {
        if (_applicationPaused)
            PauseFrameGeneration();
        else
            UnpauseFrameGeneration();
    }
}
#endif
