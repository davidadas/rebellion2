using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Authors the shared HDRP configuration used by isolated UI render-target cameras.
/// </summary>
public static class HdrpRenderTargetBuilder
{
    private const int _volumeLayer = 30;
    private const int _flatAmbientVolumeLayer = 29;
    private const int _planetPostProcessingVolumeLayer = 28;
    private const string _volumeProfilePath =
        "Assets/Settings/Rendering/RenderTargetVolumeProfile.asset";
    private const string _flatAmbientVolumeProfilePath =
        "Assets/Settings/Rendering/RenderTargetFlatAmbientVolumeProfile.asset";
    private const string _planetPostProcessingVolumeProfilePath =
        "Assets/Settings/Rendering/PlanetRenderTargetVolumeProfile.asset";

    /// <summary>
    /// Adds the isolated indirect-lighting volume consumed by render-target cameras below a UI root.
    /// </summary>
    /// <param name="parent">The generated prefab root that owns the volume.</param>
    public static void AddIsolatedVolume(Transform parent)
    {
        GameObject volumeObject = new GameObject("RenderTargetVolume", typeof(Volume));
        volumeObject.layer = _volumeLayer;
        volumeObject.transform.SetParent(parent, false);

        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.sharedProfile = LoadOrCreateVolumeProfile();
    }

    /// <summary>
    /// Adds a flat ambient-lighting volume that selected render-target cameras can opt into.
    /// </summary>
    /// <param name="parent">The generated prefab root that owns the volume.</param>
    /// <param name="ambientColor">The linear-space ambient color.</param>
    public static void AddFlatAmbientVolume(Transform parent, Color ambientColor)
    {
        GameObject volumeObject = new GameObject("RenderTargetFlatAmbientVolume", typeof(Volume));
        volumeObject.layer = _flatAmbientVolumeLayer;
        volumeObject.transform.SetParent(parent, false);

        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1001f;
        volume.sharedProfile = LoadOrCreateFlatAmbientVolumeProfile(ambientColor);
    }

    /// <summary>
    /// Adds the isolated post-processing volume used by the main-menu planet camera.
    /// </summary>
    /// <param name="parent">The generated prefab root that owns the volume.</param>
    public static void AddPlanetPostProcessingVolume(Transform parent)
    {
        GameObject volumeObject = new GameObject(
            "PlanetRenderTargetVolume",
            typeof(Volume)
        );
        volumeObject.layer = _planetPostProcessingVolumeLayer;
        volumeObject.transform.SetParent(parent, false);

        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1002f;
        volume.sharedProfile = LoadOrCreatePlanetPostProcessingVolumeProfile();
    }

    /// <summary>
    /// Adds HDRP light data and applies an authored intensity in physical units.
    /// </summary>
    /// <param name="light">The light to configure.</param>
    /// <param name="intensity">The authored light intensity.</param>
    public static void ConfigureLight(Light light, float intensity)
    {
        HDAdditionalLightData lightData = light.GetComponent<HDAdditionalLightData>();
        if (lightData == null)
        {
            lightData = light.gameObject.AddComponent<HDAdditionalLightData>();
            HDAdditionalLightData.InitDefaultHDAdditionalLightData(lightData);
        }

        light.lightUnit = light.type == LightType.Directional ? LightUnit.Lux : LightUnit.Candela;
        light.lightShadowCasterMode = LightShadowCasterMode.Default;
        light.useColorTemperature = false;
        light.intensity = intensity;
    }

    /// <summary>
    /// Configures a transparent HDRP camera that is isolated from scene-wide lighting and effects.
    /// </summary>
    /// <param name="camera">The render-target camera to configure.</param>
    /// <param name="includeFlatAmbientLighting">
    /// Whether the camera includes the optional flat ambient-lighting volume.
    /// </param>
    /// <param name="includePlanetPostProcessing">
    /// Whether the camera includes the planet-specific color and bloom volume.
    /// </param>
    /// <param name="antialiasing">The HDRP post-process antialiasing mode.</param>
    public static void ConfigureCamera(
        Camera camera,
        bool includeFlatAmbientLighting = false,
        bool includePlanetPostProcessing = false,
        HDAdditionalCameraData.AntialiasingMode antialiasing =
            HDAdditionalCameraData.AntialiasingMode.None
    )
    {
        HDAdditionalCameraData cameraData =
            camera.GetComponent<HDAdditionalCameraData>()
            ?? camera.gameObject.AddComponent<HDAdditionalCameraData>();
        cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        cameraData.backgroundColorHDR = camera.backgroundColor.linear;
        cameraData.clearDepth = true;
        cameraData.customRenderingSettings = true;
        cameraData.volumeLayerMask =
            1 << _volumeLayer
            | (includeFlatAmbientLighting ? 1 << _flatAmbientVolumeLayer : 0)
            | (includePlanetPostProcessing ? 1 << _planetPostProcessingVolumeLayer : 0);
        ConfigureFrameSetting(cameraData, FrameSettingsField.ExposureControl, false);
        ConfigureFrameSetting(
            cameraData,
            FrameSettingsField.Postprocess,
            antialiasing != HDAdditionalCameraData.AntialiasingMode.None
        );
        ConfigureFrameSetting(cameraData, FrameSettingsField.ReflectionProbe, false);
        ConfigureFrameSetting(cameraData, FrameSettingsField.PlanarProbe, false);
        ConfigureFrameSetting(cameraData, FrameSettingsField.SkyReflection, false);
        cameraData.antialiasing = antialiasing;
        cameraData.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
    }

    /// <summary>
    /// Overrides one HDRP frame setting for an isolated render-target camera.
    /// </summary>
    /// <param name="cameraData">The HDRP camera data to configure.</param>
    /// <param name="field">The frame setting to override.</param>
    /// <param name="enabled">Whether the frame setting is enabled.</param>
    private static void ConfigureFrameSetting(
        HDAdditionalCameraData cameraData,
        FrameSettingsField field,
        bool enabled
    )
    {
        cameraData.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
        cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
    }

    /// <summary>
    /// Loads the shared render-target volume profile and restores its required overrides.
    /// </summary>
    /// <returns>The shared render-target volume profile.</returns>
    private static VolumeProfile LoadOrCreateVolumeProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(_volumeProfilePath);
        if (profile == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_volumeProfilePath));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RenderTargetVolumeProfile";
            AssetDatabase.CreateAsset(profile, _volumeProfilePath);
        }

        profile.components.RemoveAll(component => component == null);
        if (!profile.TryGet(out IndirectLightingController indirectLighting))
        {
            indirectLighting = profile.Add<IndirectLightingController>();
            AssetDatabase.AddObjectToAsset(indirectLighting, profile);
        }

        indirectLighting.indirectDiffuseLightingMultiplier.Override(0f);
        indirectLighting.reflectionLightingMultiplier.Override(0f);
        indirectLighting.reflectionProbeIntensityMultiplier.Override(0f);
        EditorUtility.SetDirty(indirectLighting);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>
    /// Loads the optional flat ambient-lighting profile and restores its required overrides.
    /// </summary>
    /// <param name="ambientColor">The linear-space ambient color.</param>
    /// <returns>The flat ambient-lighting volume profile.</returns>
    private static VolumeProfile LoadOrCreateFlatAmbientVolumeProfile(Color ambientColor)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            _flatAmbientVolumeProfilePath
        );
        if (profile == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_flatAmbientVolumeProfilePath));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RenderTargetFlatAmbientVolumeProfile";
            AssetDatabase.CreateAsset(profile, _flatAmbientVolumeProfilePath);
        }

        profile.components.RemoveAll(component => component == null);
        if (!profile.TryGet(out VisualEnvironment visualEnvironment))
        {
            visualEnvironment = profile.Add<VisualEnvironment>();
            AssetDatabase.AddObjectToAsset(visualEnvironment, profile);
        }

        if (!profile.TryGet(out GradientSky gradientSky))
        {
            gradientSky = profile.Add<GradientSky>();
            AssetDatabase.AddObjectToAsset(gradientSky, profile);
        }

        if (!profile.TryGet(out IndirectLightingController indirectLighting))
        {
            indirectLighting = profile.Add<IndirectLightingController>();
            AssetDatabase.AddObjectToAsset(indirectLighting, profile);
        }

        visualEnvironment.skyType.Override((int)SkyType.Gradient);
        visualEnvironment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        gradientSky.top.Override(ambientColor);
        gradientSky.middle.Override(ambientColor);
        gradientSky.bottom.Override(ambientColor);
        gradientSky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);
        gradientSky.multiplier.Override(1f);
        indirectLighting.indirectDiffuseLightingMultiplier.Override(1f);
        indirectLighting.reflectionLightingMultiplier.Override(0f);
        indirectLighting.reflectionProbeIntensityMultiplier.Override(0f);

        EditorUtility.SetDirty(visualEnvironment);
        EditorUtility.SetDirty(gradientSky);
        EditorUtility.SetDirty(indirectLighting);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>
    /// Loads the planet render profile and restores its cinematic output overrides.
    /// </summary>
    /// <returns>The planet camera's isolated post-processing profile.</returns>
    private static VolumeProfile LoadOrCreatePlanetPostProcessingVolumeProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            _planetPostProcessingVolumeProfilePath
        );
        if (profile == null)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(_planetPostProcessingVolumeProfilePath)
            );
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PlanetRenderTargetVolumeProfile";
            AssetDatabase.CreateAsset(profile, _planetPostProcessingVolumeProfilePath);
        }

        profile.components.RemoveAll(component => component == null);
        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>();
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }

        if (!profile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>();
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
        }

        if (!profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>();
            AssetDatabase.AddObjectToAsset(colorAdjustments, profile);
        }

        bloom.threshold.Override(0.7f);
        bloom.intensity.Override(0.22f);
        bloom.scatter.Override(0.55f);
        bloom.tint.Override(Color.white);
        bloom.resolution = BloomResolution.Half;
        bloom.highQualityPrefiltering = true;
        bloom.highQualityFiltering = true;
        tonemapping.mode.Override(TonemappingMode.ACES);
        tonemapping.useFullACES.Override(true);
        colorAdjustments.postExposure.Override(0f);
        colorAdjustments.contrast.Override(4f);
        colorAdjustments.saturation.Override(3f);

        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(tonemapping);
        EditorUtility.SetDirty(colorAdjustments);
        EditorUtility.SetDirty(profile);
        return profile;
    }
}
