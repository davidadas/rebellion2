using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

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
    private readonly HashSet<EntityId> _configuredCameraIds = new HashSet<EntityId>();

    private UserAntiAliasingMode _antiAliasingMode;
    private int _renderScalePercent = UserVideoSettings.DefaultRenderScalePercent;
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
    /// Creates the application graphics owner and its runtime volume overrides.
    /// </summary>
    /// <param name="owner">The application lifetime transform.</param>
    public GraphicsManager(Transform owner)
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
        Application.targetFrameRate = settings.FrameRateLimit == 0 ? -1 : settings.FrameRateLimit;

        _antiAliasingMode = (UserAntiAliasingMode)settings.AntiAliasingMode;
        _renderScalePercent = settings.RenderScalePercent;
        _ambientOcclusionEnabled = settings.AmbientOcclusion;
        _bloomEnabled = settings.Bloom;
        _motionBlurEnabled = settings.MotionBlur;
        _rayTracingEnabled =
            settings.RayTracing && settings.AmbientOcclusion && RayTracingAvailable;
        _ambientOcclusion.active = _ambientOcclusionEnabled;
        _ambientOcclusion.rayTracing.Override(_rayTracingEnabled);
        _bloom.active = _bloomEnabled;
        _motionBlur.active = _motionBlurEnabled;

        DynamicResolutionHandler.SetDynamicResScaler(
            GetRenderScale,
            DynamicResScalePolicyType.ReturnsPercentage
        );
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
        DynamicResolutionHandler.SetDynamicResScaler(
            GetFullRenderScale,
            DynamicResScalePolicyType.ReturnsPercentage
        );
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
        ApplyToManagedCameras();
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
    /// Applies anti-aliasing, dynamic resolution, and effect gates to one scene camera.
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
        cameraData.allowDynamicResolution = _renderScalePercent < 100;
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
    /// Resolves the active quality-level pipeline, falling back to the project default before the
    /// first pipeline instance has been created.
    /// </summary>
    /// <returns>The active render-pipeline asset, or null when the project has none.</returns>
    private static RenderPipelineAsset GetActivePipelineAsset()
    {
        return GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
    }

    /// <summary>
    /// Gets the selected dynamic-resolution scale as a percentage.
    /// </summary>
    /// <returns>The selected percentage.</returns>
    private float GetRenderScale()
    {
        return _renderScalePercent;
    }

    /// <summary>
    /// Gets the full-resolution fallback used after this owner is disposed.
    /// </summary>
    /// <returns>One hundred percent.</returns>
    private static float GetFullRenderScale()
    {
        return 100f;
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
