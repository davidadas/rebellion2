using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Verifies the project-wide render pipeline configuration.
/// </summary>
[TestFixture]
public sealed class RenderPipelineConfigurationTests
{
    /// <summary>
    /// Verifies that every quality tier uses the same URP project configuration.
    /// </summary>
    [Test]
    public void RenderPipeline_AllQualityLevels_UsesUniversalRenderPipeline()
    {
        Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline);
        Assert.AreEqual(UserVideoSettings.DefaultQualityLevel + 1, QualitySettings.names.Length);
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

    [Test]
    public void RenderPipeline_ProjectConfiguration_PreservesTransparentPostProcessing()
    {
        UniversalRenderPipelineAsset pipeline =
            GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        Assert.IsNotNull(pipeline);
        Assert.IsTrue(pipeline.allowPostProcessAlphaOutput);
        Assert.IsTrue(pipeline.supportsHDR);
        Assert.IsTrue(pipeline.supportsCameraDepthTexture);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenderPipeline_AmbientOcclusion_HasDedicatedRenderer(bool ambientOcclusion)
    {
        UniversalRenderPipelineAsset pipeline =
            GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        bool found = false;
        foreach (ScriptableRendererData data in pipeline.rendererDataList)
        {
            Assert.IsInstanceOf<UniversalRendererData>(data);
            Assert.IsNotNull(((UniversalRendererData)data).postProcessData);
            bool hasAmbientOcclusion = data.rendererFeatures.Exists(feature =>
                feature is ScreenSpaceAmbientOcclusion && feature.isActive
            );
            found |= hasAmbientOcclusion == ambientOcclusion;
        }
        Assert.IsTrue(found);
    }

    [Test]
    public void RenderPipeline_TransparentRenderTarget_PreservesAlpha()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Reading rendered alpha requires a graphics device.");
        GameObject root = new GameObject("Transparent render test", typeof(Camera));
        Camera camera = root.GetComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 0;
        camera.allowHDR = true;
        root.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        RenderTexture target = new RenderTexture(16, 16, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(
                camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target }
            );
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            pixels.Apply();
            Assert.AreEqual(0f, pixels.GetPixel(8, 8).a, 1f / 255f);
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RenderPipeline_AmbientOcclusion_DarkensCustomOpaqueSurfaces()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Reading rendered occlusion requires a graphics device.");
        int originalQuality = QualitySettings.GetQualityLevel();
        int originalVSync = QualitySettings.vSyncCount;
        int originalFrameRate = Application.targetFrameRate;
        GameObject root = new GameObject("Occlusion test", typeof(Camera));
        Camera camera = root.GetComponent<Camera>();
        camera.enabled = false;
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 0.8f;
        camera.transform.position = new Vector3(0.8f, 0.7f, -1.3f);
        camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << 31;
        Material material = new Material(Shader.Find("Custom/PlanetSurface"));
        material.SetFloat("_CloudOpacity", 0f);
        material.SetFloat("_CityLightIntensity", 0f);
        material.SetFloat("_SurfaceRelief", 0f);
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.localScale = Vector3.one * 0.3f;
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0f, -0.2f, 0f);
        floor.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
        foreach (GameObject surface in new[] { cube, floor })
        {
            surface.layer = 31;
            surface.GetComponent<Renderer>().sharedMaterial = material;
        }
        RenderTexture target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        using GraphicsManager manager = new GraphicsManager(root.transform);
        try
        {
            Color[] unoccluded = null;
            float largestShadow = 0f;
            foreach (bool occlusion in new[] { false, true })
            {
                manager.Apply(
                    new UserVideoSettings
                    {
                        AntiAliasingMode = (int)UserAntiAliasingMode.Off,
                        AmbientOcclusion = occlusion,
                        Bloom = false,
                        MotionBlur = false,
                    }
                );
                RenderPipeline.SubmitRenderRequest(
                    camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target }
                );
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                pixels.Apply();
                Color[] rendered = pixels.GetPixels();
                if (!occlusion)
                    unoccluded = rendered;
                else
                    for (int index = 0; index < rendered.Length; index++)
                        largestShadow = Mathf.Max(
                            largestShadow,
                            unoccluded[index].grayscale - rendered[index].grayscale
                        );
            }
            Assert.Greater(largestShadow, 0.01f);
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(root);
            QualitySettings.SetQualityLevel(originalQuality, true);
            QualitySettings.vSyncCount = originalVSync;
            Application.targetFrameRate = originalFrameRate;
        }
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
    /// Verifies that Windows players start in the stable borderless fullscreen mode.
    /// </summary>
    [Test]
    public void WindowsFullscreen_ProjectConfiguration_StartsBorderless()
    {
        Assert.AreEqual(FullScreenMode.FullScreenWindow, PlayerSettings.fullScreenMode);
    }

    /// <summary>
    /// Verifies that Linux uses the graphics API supported by URP.
    /// </summary>
    [Test]
    public void LinuxGraphicsApi_ProjectConfiguration_UsesVulkan()
    {
        CollectionAssert.AreEqual(
            new[] { GraphicsDeviceType.Vulkan },
            PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneLinux64)
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
    /// Verifies shader support for the active backend without compiling URP code for OpenGL.
    /// </summary>
    /// <param name="shaderName">The shader name to resolve.</param>
    /// <param name="requiresUrpBackend">Whether the shader uses URP-specific shader libraries.</param>
    [TestCase("Custom/PlanetAtmosphere", true)]
    [TestCase("Custom/LoadingShuttleExhaust", true)]
    [TestCase("Custom/PlanetClouds", true)]
    [TestCase("Custom/PlanetDayNightShade", true)]
    [TestCase("Custom/PlanetSurface", true)]
    [TestCase("Custom/PremultipliedTexture", false)]
    [TestCase("UI/Crisp Sprite", false)]
    public void ProjectShader_ActiveBackend_HasExpectedSupport(
        string shaderName,
        bool requiresUrpBackend
    )
    {
        Shader shader = Shader.Find(shaderName);

        Assert.IsNotNull(shader, shaderName);
        bool isOpenGl =
            SystemInfo.graphicsDeviceType
            is GraphicsDeviceType.OpenGLCore
                or GraphicsDeviceType.OpenGLES3;
        Assert.AreEqual(!requiresUrpBackend || !isOpenGl, shader.isSupported, shaderName);
        AssertShaderHasNoCompilerErrors(shaderName, shader);
    }

    /// <summary>
    /// Verifies that the surface shader and the binding fixture expose the same typed contract.
    /// </summary>
    /// <param name="property">The material property consumed by the binding.</param>
    /// <param name="expectedType">The required shader-property type.</param>
    [TestCase("_BaseMap", ShaderPropertyType.Texture)]
    [TestCase("_BaseColor", ShaderPropertyType.Color)]
    [TestCase("_CloudMap", ShaderPropertyType.Texture)]
    [TestCase("_CloudRotationSpeed", ShaderPropertyType.Float)]
    public void PlanetSurfaceShader_BindingProperties_MatchesFixtureContract(
        string property,
        ShaderPropertyType expectedType
    )
    {
        foreach (
            string shaderName in new[]
            {
                "Custom/PlanetSurface",
                "Hidden/Tests/PlanetSurfaceBinding",
            }
        )
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, shaderName);
            int propertyIndex = shader.FindPropertyIndex(property);
            Assert.GreaterOrEqual(propertyIndex, 0, $"{shaderName}: {property}");
            Assert.AreEqual(expectedType, shader.GetPropertyType(propertyIndex), shaderName);
            AssertShaderHasNoCompilerErrors(shaderName, shader);
        }
    }

    [TestCase("Custom/PlanetSurface")]
    [TestCase("FORGE3D/Planets HD/URP/Terrestrial")]
    [TestCase("FORGE3D/Planets HD/URP/Thunderstorm")]
    [Category("RequiresMedia")]
    public void PlanetShader_OpaqueSurface_ProvidesMotionVectors(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        Assert.IsNotNull(shader);
        Material material = new Material(shader);
        try
        {
            Assert.GreaterOrEqual(material.FindPass("MotionVectors"), 0);
            Assert.IsTrue(material.GetShaderPassEnabled("MotionVectors"));
            AssertShaderHasNoCompilerErrors(shaderName, shader);
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }

    /// <summary>
    /// Verifies that each packaged shader is available and compiles for the active build target.
    /// </summary>
    /// <param name="shaderName">The shader name to resolve.</param>
    [TestCase("Space Graphics Toolkit/Flare")]
    [TestCase("FORGE3D/Planets HD/URP/Atmosphere")]
    [TestCase("FORGE3D/Planets HD/URP/Terrestrial")]
    [TestCase("FORGE3D/Planets HD/URP/Thunderstorm")]
    [Category("RequiresMedia")]
    public void PackagedShader_UniversalPipeline_IsAvailable(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);

        Assert.IsNotNull(shader, shaderName);
        AssertShaderHasNoCompilerErrors(shaderName, shader);
    }

    /// <summary>
    /// Verifies that Unity reports no compiler errors for a shader.
    /// </summary>
    /// <param name="shaderName">The shader name used in assertion messages.</param>
    /// <param name="shader">The shader to inspect.</param>
    private static void AssertShaderHasNoCompilerErrors(string shaderName, Shader shader)
    {
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
