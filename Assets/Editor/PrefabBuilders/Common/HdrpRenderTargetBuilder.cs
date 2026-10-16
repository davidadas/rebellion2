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
    private const string _volumeProfilePath =
        "Assets/Settings/Rendering/RenderTargetVolumeProfile.asset";

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
        light.intensity = intensity;
    }

    /// <summary>
    /// Configures a transparent HDRP camera that is isolated from scene-wide lighting and effects.
    /// </summary>
    /// <param name="camera">The render-target camera to configure.</param>
    public static void ConfigureCamera(Camera camera)
    {
        HDAdditionalCameraData cameraData =
            camera.GetComponent<HDAdditionalCameraData>()
            ?? camera.gameObject.AddComponent<HDAdditionalCameraData>();
        cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        cameraData.backgroundColorHDR = camera.backgroundColor.linear;
        cameraData.clearDepth = true;
        cameraData.customRenderingSettings = true;
        cameraData.volumeLayerMask = 1 << _volumeLayer;
        ConfigureFrameSetting(cameraData, FrameSettingsField.ExposureControl, false);
        ConfigureFrameSetting(cameraData, FrameSettingsField.Postprocess, false);
        ConfigureFrameSetting(cameraData, FrameSettingsField.ReflectionProbe, false);
        ConfigureFrameSetting(cameraData, FrameSettingsField.PlanarProbe, false);
        ConfigureFrameSetting(cameraData, FrameSettingsField.SkyReflection, false);
        cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
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
}
