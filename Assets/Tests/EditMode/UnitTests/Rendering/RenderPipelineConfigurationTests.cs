using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Verifies the project-wide render pipeline configuration.
/// </summary>
[TestFixture]
public sealed class RenderPipelineConfigurationTests
{
    [Test]
    public void RenderPipeline_AllQualityLevels_UsesUniversalRenderPipeline()
    {
        Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline);
        Assert.AreSame(
            GraphicsSettings.defaultRenderPipeline,
            QualitySettings.GetRenderPipelineAssetAt(QualitySettings.GetQualityLevel())
        );

        for (int index = 0; index < QualitySettings.names.Length; index++)
        {
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(
                QualitySettings.GetRenderPipelineAssetAt(index),
                QualitySettings.names[index]
            );
        }
    }

    [TestCase(
        0,
        LightRenderingMode.Disabled,
        LightRenderingMode.PerVertex,
        4,
        false,
        1,
        1024,
        15f,
        1,
        false
    )]
    [TestCase(
        1,
        LightRenderingMode.Disabled,
        LightRenderingMode.PerVertex,
        4,
        false,
        1,
        1024,
        20f,
        1,
        false
    )]
    [TestCase(
        2,
        LightRenderingMode.PerPixel,
        LightRenderingMode.PerPixel,
        1,
        true,
        1,
        1024,
        20f,
        1,
        false
    )]
    [TestCase(
        3,
        LightRenderingMode.PerPixel,
        LightRenderingMode.PerPixel,
        2,
        true,
        1,
        2048,
        40f,
        2,
        true
    )]
    [TestCase(
        4,
        LightRenderingMode.PerPixel,
        LightRenderingMode.PerPixel,
        3,
        true,
        2,
        4096,
        70f,
        2,
        true
    )]
    [TestCase(
        5,
        LightRenderingMode.PerPixel,
        LightRenderingMode.PerPixel,
        4,
        true,
        2,
        4096,
        150f,
        4,
        true
    )]
    public void QualityPipeline_ConvertedTier_PreservesEquivalentSettings(
        int qualityIndex,
        LightRenderingMode mainLightMode,
        LightRenderingMode additionalLightMode,
        int additionalLightCount,
        bool shadowsEnabled,
        int msaaSamples,
        int shadowResolution,
        float shadowDistance,
        int shadowCascades,
        bool softShadows
    )
    {
        UniversalRenderPipelineAsset pipeline =
            QualitySettings.GetRenderPipelineAssetAt(qualityIndex) as UniversalRenderPipelineAsset;

        Assert.IsNotNull(pipeline);
        Assert.AreEqual(mainLightMode, pipeline.mainLightRenderingMode);
        Assert.AreEqual(additionalLightMode, pipeline.additionalLightsRenderingMode);
        Assert.AreEqual(additionalLightCount, pipeline.maxAdditionalLightsCount);
        Assert.AreEqual(shadowsEnabled, pipeline.supportsMainLightShadows);
        Assert.AreEqual(shadowsEnabled, pipeline.supportsAdditionalLightShadows);
        Assert.AreEqual(msaaSamples, pipeline.msaaSampleCount);
        Assert.AreEqual(shadowResolution, pipeline.mainLightShadowmapResolution);
        Assert.AreEqual(shadowResolution, pipeline.additionalLightsShadowmapResolution);
        Assert.AreEqual(shadowDistance, pipeline.shadowDistance);
        Assert.AreEqual(shadowCascades, pipeline.shadowCascadeCount);
        Assert.AreEqual(softShadows, pipeline.supportsSoftShadows);
    }

    [Test]
    public void ColorSpace_ProjectConfiguration_UsesLinearColor()
    {
        Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace);
    }

    [TestCase("Custom/AtmosphereRim")]
    [TestCase("Custom/PlanetClouds")]
    [TestCase("Custom/PlanetDayNightShade")]
    public void CustomShader_UniversalPipeline_IsSupported(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);

        Assert.IsNotNull(shader, shaderName);
        Assert.IsTrue(shader.isSupported, shaderName);
    }

    [TestCase("Shader Graphs/glTF-pbrMetallicRoughness")]
    [TestCase("Shader Graphs/glTF-pbrSpecularGlossiness")]
    [TestCase("Shader Graphs/glTF-unlit")]
    public void RuntimeGltfShader_ExternalModelMaterial_IsAlwaysIncluded(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        Assert.IsNotNull(shader, shaderName);

        SerializedObject graphicsSettings = new SerializedObject(
            GraphicsSettings.GetGraphicsSettings()
        );
        SerializedProperty includedShaders = graphicsSettings.FindProperty(
            "m_AlwaysIncludedShaders"
        );

        bool isIncluded = false;
        for (int index = 0; index < includedShaders.arraySize; index++)
        {
            if (includedShaders.GetArrayElementAtIndex(index).objectReferenceValue == shader)
            {
                isIncluded = true;
                break;
            }
        }

        Assert.IsTrue(isIncluded, shaderName);
    }
}
