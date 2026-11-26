using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies persisted graphics quality to URP and game-facing cameras.
/// </summary>
public sealed class GraphicsManager : IDisposable
{
    public const int RuntimeVolumeLayer = 27;

    private const string _runtimeVolumeName = "Runtime Graphics Volume";

    private readonly GameObject _runtimeVolumeObject;
    private readonly VolumeProfile _runtimeVolumeProfile;
    private readonly VolumeProfile _disabledBloomProfile;
    private readonly Bloom _bloom;
    private readonly Bloom _disabledBloom;
    private readonly MotionBlur _motionBlur;
    private readonly HashSet<EntityId> _configuredCameraIds = new HashSet<EntityId>();

    private UserAntiAliasingMode _antiAliasingMode;
    private bool _ambientOcclusionEnabled = true;
    private bool _bloomEnabled = true;
    private bool _motionBlurEnabled = true;
    private bool _hasAppliedSettings;
    private bool _disposed;

    /// <summary>
    /// Gets whether the active rendering pipeline provides the ray-tracing option.
    /// </summary>
    public bool RayTracingAvailable => false;

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
        _bloom = _runtimeVolumeProfile.Add<Bloom>();
        _bloom.threshold.Override(1f);
        _bloom.intensity.Override(0.25f);
        _motionBlur = _runtimeVolumeProfile.Add<MotionBlur>();
        _motionBlur.intensity.Override(0.25f);

        Volume volume = _runtimeVolumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = _runtimeVolumeProfile;

        _disabledBloomProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _disabledBloom = _disabledBloomProfile.Add<Bloom>();
        _disabledBloom.intensity.Override(0f);
        _disabledBloom.active = false;
        GameObject disabledBloomObject = new GameObject("Disabled bloom", typeof(Volume));
        disabledBloomObject.layer = RuntimeVolumeLayer;
        disabledBloomObject.transform.SetParent(_runtimeVolumeObject.transform, false);
        Volume disabledBloomVolume = disabledBloomObject.GetComponent<Volume>();
        disabledBloomVolume.isGlobal = true;
        disabledBloomVolume.priority = float.MaxValue;
        disabledBloomVolume.sharedProfile = _disabledBloomProfile;

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
        _ambientOcclusionEnabled = settings.AmbientOcclusion;
        _bloomEnabled = settings.Bloom;
        _motionBlurEnabled = settings.MotionBlur;
        _bloom.active = _bloomEnabled;
        _disabledBloom.active = !_bloomEnabled;
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
        DestroyObject(_runtimeVolumeProfile);
        DestroyObject(_disabledBloomProfile);
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
    /// Applies anti-aliasing and effect gates to one scene camera.
    /// </summary>
    /// <param name="camera">The scene camera to configure.</param>
    private void ApplyToCamera(Camera camera)
    {
        UniversalAdditionalCameraData cameraData =
            camera.GetComponent<UniversalAdditionalCameraData>()
            ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.antialiasing =
            _antiAliasingMode == UserAntiAliasingMode.Taa && camera.orthographic
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : ConvertAntiAliasing(_antiAliasingMode);
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        camera.allowDynamicResolution = false;
        camera.allowHDR = true;
        cameraData.volumeLayerMask |= 1 << RuntimeVolumeLayer;
        cameraData.renderPostProcessing = true;
        cameraData.SetRenderer(GetRendererIndex(_ambientOcclusionEnabled));
        _configuredCameraIds.Add(camera.GetEntityId());
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
    /// Selects the renderer with the requested ambient-occlusion feature without changing shared assets.
    /// </summary>
    /// <param name="ambientOcclusion">Whether this camera should receive ambient occlusion.</param>
    /// <returns>The matching renderer index, or the pipeline default when none is available.</returns>
    private static int GetRendererIndex(bool ambientOcclusion)
    {
        if (GetActivePipelineAsset() is not UniversalRenderPipelineAsset pipeline)
            return -1;

        for (int index = 0; index < pipeline.rendererDataList.Length; index++)
        {
            ScriptableRendererData data = pipeline.rendererDataList[index];
            if (data == null)
                continue;
            bool hasAmbientOcclusion = data.rendererFeatures.Exists(feature =>
                feature is ScreenSpaceAmbientOcclusion && feature.isActive
            );
            if (hasAmbientOcclusion == ambientOcclusion)
                return index;
        }
        return -1;
    }

    /// <summary>
    /// Converts the persisted anti-aliasing value to URP's camera setting.
    /// </summary>
    /// <param name="mode">The persisted anti-aliasing mode.</param>
    /// <returns>The matching URP anti-aliasing mode.</returns>
    private static AntialiasingMode ConvertAntiAliasing(UserAntiAliasingMode mode)
    {
        return mode switch
        {
            UserAntiAliasingMode.Fxaa => AntialiasingMode.FastApproximateAntialiasing,
            UserAntiAliasingMode.Smaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            UserAntiAliasingMode.Taa => AntialiasingMode.TemporalAntiAliasing,
            _ => AntialiasingMode.None,
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

        UniversalAdditionalCameraData cameraData =
            camera.GetComponent<UniversalAdditionalCameraData>();
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
