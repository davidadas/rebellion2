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
    private const int _tacticalAmbientVolumeLayer = 24;
    private const int _planetPostProcessingVolumeLayer = 28;
    private const string _volumeProfilePath =
        "Assets/Settings/Rendering/RenderTargetVolumeProfile.asset";
    private const string _flatAmbientVolumeProfilePath =
        "Assets/Settings/Rendering/RenderTargetFlatAmbientVolumeProfile.asset";
    private const string _tacticalAmbientVolumeProfilePath =
        "Assets/Settings/Rendering/TacticalBattleAmbientVolumeProfile.asset";
    private const string _tacticalLitMaterialPath =
        "Assets/Settings/Rendering/TacticalBattleLitMaterial.mat";
    private const string _tacticalParticleMaterialPath =
        "Assets/Settings/Rendering/TacticalBattleParticleMaterial.mat";
    private const float _tacticalParticleAlphaCutoff = 0.5f;
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
        volume.sharedProfile = LoadOrCreateFlatAmbientVolumeProfile(
            _flatAmbientVolumeProfilePath,
            ambientColor
        );
    }

    /// <summary>
    /// Adds the flat ambient-lighting volume of the tactical battle rig. It has a layer and a
    /// profile of its own: the battle is lit far brighter than the menu and loading
    /// presentations that share the other flat ambient volume, and a loading screen can be on
    /// show while the strategy scene that holds the battle rig is loaded.
    /// </summary>
    /// <param name="parent">The generated prefab root that owns the volume.</param>
    /// <param name="ambientColor">The linear-space ambient color.</param>
    public static void AddTacticalAmbientVolume(Transform parent, Color ambientColor)
    {
        GameObject volumeObject = new GameObject("TacticalBattleAmbientVolume", typeof(Volume));
        volumeObject.layer = _tacticalAmbientVolumeLayer;
        volumeObject.transform.SetParent(parent, false);

        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1001f;
        volume.sharedProfile = LoadOrCreateFlatAmbientVolumeProfile(
            _tacticalAmbientVolumeProfilePath,
            ambientColor
        );
    }

    /// <summary>
    /// Loads the opaque, double-sided lit material that the tactical battle clones for its
    /// placeholders, markers, beams and repaired hulls, and restores its settings. The battle
    /// builds its materials while it runs; cloning an authored asset keeps the shader and its
    /// variants in a player build, where the pipeline's default material does not exist.
    /// </summary>
    /// <returns>The lit template material.</returns>
    public static Material LoadOrCreateTacticalLitMaterial()
    {
        return LoadOrCreateTacticalMaterial(
            _tacticalLitMaterialPath,
            doubleSided: true,
            alphaCutout: false
        );
    }

    /// <summary>
    /// Loads the alpha-cutout lit material that the tactical battle clones for its particle
    /// effects, and restores its settings.
    /// </summary>
    /// <returns>The particle template material.</returns>
    public static Material LoadOrCreateTacticalParticleMaterial()
    {
        return LoadOrCreateTacticalMaterial(
            _tacticalParticleMaterialPath,
            doubleSided: false,
            alphaCutout: true
        );
    }

    /// <summary>
    /// Adds the isolated post-processing volume used by the main-menu planet camera.
    /// </summary>
    /// <param name="parent">The generated prefab root that owns the volume.</param>
    public static void AddPlanetPostProcessingVolume(Transform parent)
    {
        GameObject volumeObject = new GameObject("PlanetRenderTargetVolume", typeof(Volume));
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
    /// <param name="includeTacticalAmbientLighting">
    /// Whether the camera includes the tactical battle's own ambient-lighting volume.
    /// </param>
    public static void ConfigureCamera(
        Camera camera,
        bool includeFlatAmbientLighting = false,
        bool includePlanetPostProcessing = false,
        HDAdditionalCameraData.AntialiasingMode antialiasing =
            HDAdditionalCameraData.AntialiasingMode.None,
        bool includeTacticalAmbientLighting = false
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
            | 1 << GraphicsManager.RuntimeVolumeLayer
            | (includeFlatAmbientLighting ? 1 << _flatAmbientVolumeLayer : 0)
            | (includeTacticalAmbientLighting ? 1 << _tacticalAmbientVolumeLayer : 0)
            | (includePlanetPostProcessing ? 1 << _planetPostProcessingVolumeLayer : 0);
        ConfigureFrameSetting(cameraData, FrameSettingsField.ExposureControl, false);
        ConfigureFrameSetting(
            cameraData,
            FrameSettingsField.Postprocess,
            antialiasing != HDAdditionalCameraData.AntialiasingMode.None
        );
        ConfigureFrameSetting(cameraData, FrameSettingsField.Bloom, includePlanetPostProcessing);
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
    /// Loads a tactical-battle template material, creating it from the pipeline's default
    /// material when it is missing, and restores the settings the battle relies on.
    /// </summary>
    /// <param name="path">The material asset path.</param>
    /// <param name="doubleSided">Whether both faces are drawn.</param>
    /// <param name="alphaCutout">Whether texels below the alpha cutoff are discarded.</param>
    /// <returns>The template material.</returns>
    private static Material LoadOrCreateTacticalMaterial(
        string path,
        bool doubleSided,
        bool alphaCutout
    )
    {
        Material source =
            GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial
                : null;
        if (source == null)
            throw new System.InvalidOperationException(
                "The render pipeline has no default material to author the tactical materials from."
            );

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = source.shader;
            material.CopyPropertiesFromMaterial(source);
        }

        CullMode culling = doubleSided ? CullMode.Off : CullMode.Back;
        material.SetFloat("_DoubleSidedEnable", doubleSided ? 1f : 0f);
        material.SetFloat("_CullMode", (float)culling);
        material.SetFloat("_CullModeForward", (float)culling);
        material.SetFloat("_AlphaCutoffEnable", alphaCutout ? 1f : 0f);
        if (alphaCutout)
            material.SetFloat("_AlphaCutoff", _tacticalParticleAlphaCutoff);
        // The keywords, passes and render queue follow from the settings above.
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Loads a flat ambient-lighting profile and restores its required overrides.
    /// </summary>
    /// <param name="path">The profile asset path.</param>
    /// <param name="ambientColor">The linear-space ambient color.</param>
    /// <returns>The flat ambient-lighting volume profile.</returns>
    private static VolumeProfile LoadOrCreateFlatAmbientVolumeProfile(
        string path,
        Color ambientColor
    )
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);
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
