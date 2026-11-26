using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[TestFixture]
public sealed class RenderTargetLightingTests
{
    [Test]
    public void Configure_RenderingCamera_RestoresSceneLighting()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Rendering callbacks require a graphics device.");
        GameObject root = new GameObject("Lighting test", typeof(Camera));
        Camera camera = root.GetComponent<Camera>();
        camera.enabled = false;
        RenderTexture target = new RenderTexture(32, 32, 24);
        AmbientMode originalMode = RenderSettings.ambientMode;
        SphericalHarmonicsL2 originalProbe = RenderSettings.ambientProbe;
        float originalReflection = RenderSettings.reflectionIntensity;
        Color ambientColor = new Color(0.12f, 0.21f, 0.34f);
        RenderTargetLighting lighting = root.AddComponent<RenderTargetLighting>();
        lighting.Configure(ambientColor);
        SphericalHarmonicsL2 expectedProbe = new SphericalHarmonicsL2();
        expectedProbe.AddAmbientLight(ambientColor);
        bool observed = false;
        System.Action<ScriptableRenderContext, Camera> observe = (_, renderedCamera) =>
        {
            if (renderedCamera != camera)
                return;
            observed = true;
            Assert.AreEqual(AmbientMode.Custom, RenderSettings.ambientMode);
            Assert.AreEqual(expectedProbe, RenderSettings.ambientProbe);
            Assert.AreEqual(0f, RenderSettings.reflectionIntensity);
        };
        RenderPipelineManager.beginCameraRendering += observe;
        try
        {
            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.IsTrue(observed);
            Assert.AreEqual(originalMode, RenderSettings.ambientMode);
            Assert.AreEqual(originalProbe, RenderSettings.ambientProbe);
            Assert.AreEqual(originalReflection, RenderSettings.reflectionIntensity);
        }
        finally
        {
            RenderPipelineManager.beginCameraRendering -= observe;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(target);
        }
    }
}
