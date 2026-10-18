using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Verifies the project-wide render pipeline configuration.
/// </summary>
[TestFixture]
public sealed class RenderPipelineConfigurationTests
{
    /// <summary>
    /// Verifies that every quality tier uses the same HDRP project configuration.
    /// </summary>
    [Test]
    public void RenderPipeline_AllQualityLevels_UsesHighDefinitionRenderPipeline()
    {
        Assert.IsInstanceOf<HDRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline);
        Assert.AreEqual(UserVideoSettings.DefaultQualityLevel + 1, QualitySettings.names.Length);
        Assert.AreSame(
            GraphicsSettings.defaultRenderPipeline,
            QualitySettings.GetRenderPipelineAssetAt(QualitySettings.GetQualityLevel())
        );

        for (int index = 0; index < QualitySettings.names.Length; index++)
        {
            Assert.IsInstanceOf<HDRenderPipelineAsset>(
                QualitySettings.GetRenderPipelineAssetAt(index),
                QualitySettings.names[index]
            );
        }
    }

    /// <summary>
    /// Verifies that HDRP has the capabilities required by raster and ray-traced effects.
    /// </summary>
    [Test]
    public void RenderPipeline_ProjectConfiguration_EnablesRayTracingCapabilities()
    {
        HDRenderPipelineAsset pipeline =
            GraphicsSettings.defaultRenderPipeline as HDRenderPipelineAsset;

        Assert.IsNotNull(pipeline);
        RenderPipelineSettings settings = pipeline.currentPlatformRenderPipelineSettings;
        Assert.IsTrue(settings.supportRayTracing);
        Assert.IsTrue(settings.supportSSR);
        Assert.IsTrue(settings.supportSSRTransparent);
        Assert.IsTrue(settings.supportSSGI);
        Assert.IsTrue(settings.supportSSAO);
        Assert.IsTrue(settings.hdShadowInitParams.supportScreenSpaceShadows);
    }

    /// <summary>
    /// Verifies that Windows starts with DirectX 12 while retaining the raster DirectX 11 fallback.
    /// </summary>
    [Test]
    public void WindowsGraphicsApi_ProjectConfiguration_PrioritizesDirect3D12WithFallback()
    {
        CollectionAssert.AreEqual(
            new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 },
            PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64)
        );
    }

    /// <summary>
    /// Verifies that the project performs lighting calculations in linear color space.
    /// </summary>
    [Test]
    public void ColorSpace_ProjectConfiguration_UsesLinearColor()
    {
        Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace);
    }

    /// <summary>
    /// Verifies that project lighting uses linear intensity and color temperature.
    /// </summary>
    [Test]
    public void Lighting_ProjectConfiguration_UsesLinearIntensityAndColorTemperature()
    {
        SerializedObject graphicsSettings = new SerializedObject(
            GraphicsSettings.GetGraphicsSettings()
        );

        Assert.IsTrue(graphicsSettings.FindProperty("m_LightsUseLinearIntensity").boolValue);
        Assert.IsTrue(graphicsSettings.FindProperty("m_LightsUseColorTemperature").boolValue);
    }

    /// <summary>
    /// Verifies that each custom shader compiles for the active high-definition pipeline.
    /// </summary>
    /// <param name="shaderName">The shader name to resolve.</param>
    [TestCase("Custom/PlanetAtmosphere")]
    [TestCase("Custom/LoadingShuttleExhaust")]
    [TestCase("Custom/PlanetClouds")]
    [TestCase("Custom/PlanetDayNightShade")]
    [TestCase("Custom/PlanetSurface")]
    [TestCase("Custom/PremultipliedTexture")]
    [TestCase("FORGE3D/Planets HD/HDRP/Atmosphere")]
    [TestCase("FORGE3D/Planets HD/HDRP/Terrestrial")]
    [TestCase("FORGE3D/Planets HD/HDRP/Thunderstorm")]
    public void CustomShader_HighDefinitionPipeline_IsSupported(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);

        Assert.IsNotNull(shader, shaderName);
        Assert.IsTrue(shader.isSupported, shaderName);
        foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
        {
            Assert.AreNotEqual(
                ShaderCompilerMessageSeverity.Error,
                message.severity,
                $"{shaderName}: {message.message}"
            );
        }
    }

    /// <summary>
    /// Verifies that runtime glTF materials remain included in player builds.
    /// </summary>
    /// <param name="shaderName">The glTF shader name to resolve.</param>
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
