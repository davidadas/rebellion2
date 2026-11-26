using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Isolates ambient lighting for a camera rendering artwork into a texture.
/// </summary>
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public sealed class RenderTargetLighting : MonoBehaviour
{
    [SerializeField]
    private Color ambientColor;

    private Camera targetCamera;
    private AmbientMode previousAmbientMode;
    private SphericalHarmonicsL2 previousAmbientProbe;
    private float previousReflectionIntensity;
    private bool applied;

    /// <summary>
    /// Sets the linear ambient color used only while this camera renders.
    /// </summary>
    /// <param name="color">The ambient color in linear space.</param>
    public void Configure(Color color) => ambientColor = color;

    /// <summary>
    /// Registers the camera's lighting scope with the rendering callbacks.
    /// </summary>
    private void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    /// <summary>
    /// Releases callbacks and restores scene lighting if rendering was interrupted.
    /// </summary>
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreLighting();
    }

    /// <summary>
    /// Applies the camera's ambient probe before culling and drawing its models.
    /// </summary>
    /// <param name="context">The active rendering context.</param>
    /// <param name="camera">The camera about to render.</param>
    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != targetCamera || applied)
            return;

        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientProbe = RenderSettings.ambientProbe;
        previousReflectionIntensity = RenderSettings.reflectionIntensity;
        SphericalHarmonicsL2 ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(ambientColor);
        RenderSettings.ambientMode = AmbientMode.Custom;
        RenderSettings.ambientProbe = ambient;
        RenderSettings.reflectionIntensity = 0f;
        applied = true;
    }

    /// <summary>
    /// Restores scene lighting when this camera has finished rendering.
    /// </summary>
    /// <param name="context">The active rendering context.</param>
    /// <param name="camera">The camera that finished rendering.</param>
    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
            RestoreLighting();
    }

    /// <summary>
    /// Restores the lighting state captured by the current camera scope.
    /// </summary>
    private void RestoreLighting()
    {
        if (!applied)
            return;

        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientProbe = previousAmbientProbe;
        RenderSettings.reflectionIntensity = previousReflectionIntensity;
        applied = false;
    }
}
