using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Authors isolated URP cameras, lights and post-processing for UI render targets.
/// </summary>
public static class UrpRenderTargetBuilder
{
    private const int _planetPostProcessingVolumeLayer = 28;
    private const string _planetPostProcessingVolumeProfilePath =
        "Assets/Settings/Rendering/PlanetRenderTargetVolumeProfile.asset";

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
    /// Applies the light intensity used by the universal rendering pipeline.
    /// </summary>
    /// <param name="light">The light to configure.</param>
    /// <param name="intensity">The authored light intensity.</param>
    public static void ConfigureLight(Light light, float intensity) => light.intensity = intensity;

    /// <summary>
    /// Configures a transparent camera isolated from scene lighting and unrelated volumes.
    /// </summary>
    /// <param name="camera">The render-target camera to configure.</param>
    /// <param name="ambientColor">The linear ambient color applied only to this camera.</param>
    /// <param name="includePlanetPostProcessing">Whether to include the planet color and bloom profile.</param>
    /// <param name="antialiasing">The post-process antialiasing mode.</param>
    public static void ConfigureCamera(
        Camera camera,
        Color ambientColor = default,
        bool includePlanetPostProcessing = false,
        AntialiasingMode antialiasing = AntialiasingMode.None
    )
    {
        UniversalAdditionalCameraData cameraData =
            camera.GetComponent<UniversalAdditionalCameraData>()
            ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = true;
        cameraData.volumeLayerMask =
            1 << GraphicsManager.RuntimeVolumeLayer
            | (includePlanetPostProcessing ? 1 << _planetPostProcessingVolumeLayer : 0);
        cameraData.renderPostProcessing =
            includePlanetPostProcessing || antialiasing != AntialiasingMode.None;
        cameraData.antialiasing = antialiasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        cameraData.SetRenderer(0);
        RenderTargetLighting lighting =
            camera.GetComponent<RenderTargetLighting>()
            ?? camera.gameObject.AddComponent<RenderTargetLighting>();
        lighting.Configure(ambientColor);
    }

    /// <summary>
    /// Loads the planet profile and restores its authored color and bloom overrides.
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
        bloom.highQualityFiltering.Override(true);
        tonemapping.mode.Override(TonemappingMode.ACES);
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
