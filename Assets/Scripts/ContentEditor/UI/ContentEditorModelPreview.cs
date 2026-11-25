using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Loads a selected GLB into the editor's isolated preview rig.
/// </summary>
public sealed class ContentEditorModelPreview : MonoBehaviour, IDragHandler, IScrollHandler
{
    private const int _maxTextureDimension = 1920;
    private static readonly Vector2 _initialViewAngles = new(25f, 65f);

    [SerializeField]
    private Camera camera;

    [SerializeField]
    private Shader previewShader;

    private readonly Dictionary<Material, Material> previewMaterials = new();
    private Vector3 modelCenter;
    private Vector3 modelCenterInMount;
    private float modelRadius;
    private Vector2Int viewport = new(960, 600);
    private Vector2 viewAngles;
    private float zoom = 1;
    private ContentAssets source;
    private string address;
    private CancellationTokenSource pending;
    private ContentModelCache cache;
    private ContentModelInstance instance;
    private GameObject modelMount;
    private RenderTexture texture;

    internal Texture Texture => texture;
    internal bool LoadFailed { get; private set; }
    internal float Zoom => zoom;
    internal event Action ZoomChanged;

    /// <summary>
    /// Rotates the loaded model around its center while dragging the preview.
    /// </summary>
    /// <param name="eventData">The left-button movement over the preview.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (modelMount == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        viewAngles.x = Mathf.Clamp(viewAngles.x - eventData.delta.y * 0.25f, -80f, 80f);
        viewAngles.y = Mathf.Repeat(viewAngles.y + eventData.delta.x * 0.25f, 360f);
        RotateModel(modelMount.transform);
    }

    /// <summary>
    /// Zooms the model while leaving its orientation and authored scale unchanged.
    /// </summary>
    /// <param name="eventData">The wheel movement over the preview.</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (modelMount == null)
            return;

        float steps = Mathf.Clamp(eventData.scrollDelta.y, -1f, 1f);
        SetZoom(zoom * Mathf.Pow(1.05f, steps));
    }

    /// <summary>
    /// Restores the fitted model and its starting pose.
    /// </summary>
    internal void ResetView()
    {
        if (modelMount == null)
            return;

        viewAngles = _initialViewAngles;
        RotateModel(modelMount.transform);
        SetZoom(1f);
    }

    /// <summary>
    /// Adjusts the fixed camera's magnification and updates the viewer readout.
    /// </summary>
    /// <param name="value">The requested magnification relative to fit.</param>
    private void SetZoom(float value)
    {
        zoom = Mathf.Clamp(value, 0.45f, 3f);
        UpdateCameraZoom();
        ZoomChanged?.Invoke();
    }

    /// <summary>
    /// Matches the preview's aspect ratio to its allocated space while retaining the current view.
    /// </summary>
    /// <param name="width">The viewport width.</param>
    /// <param name="height">The viewport height.</param>
    internal void ResizeViewport(int width, int height)
    {
        float scale = Mathf.Min(1f, (float)_maxTextureDimension / Mathf.Max(1, width, height));
        Vector2Int next = new(
            Mathf.Max(1, Mathf.RoundToInt(width * scale)),
            Mathf.Max(1, Mathf.RoundToInt(height * scale))
        );
        if (viewport == next)
            return;

        viewport = next;
        if (modelMount != null)
            CreateTexture();
    }

    /// <summary>
    /// Replaces the camera target for the current viewport.
    /// </summary>
    private void CreateTexture()
    {
        ReleaseTexture();
        texture = new RenderTexture(viewport.x, viewport.y, 24) { name = "Content model preview" };
        camera.targetTexture = texture;
        camera.aspect = (float)viewport.x / viewport.y;
        UpdateCameraZoom();
    }

    /// <summary>
    /// Fits the model on both screen axes at the current magnification.
    /// </summary>
    private void UpdateCameraZoom()
    {
        float fit = modelRadius * 1.05f / Mathf.Min(1f, camera.aspect);
        camera.orthographicSize = Mathf.Max(0.01f, fit / zoom);
    }

    /// <summary>
    /// Releases the render target before changing the viewport or closing the preview.
    /// </summary>
    private void ReleaseTexture()
    {
        if (camera != null)
            camera.targetTexture = null;
        if (texture == null)
            return;

        texture.Release();
        if (Application.isPlaying)
            Object.Destroy(texture);
        else
            Object.DestroyImmediate(texture);
        texture = null;
    }

    /// <summary>
    /// Displays the chosen model once it is loaded. An empty address clears the preview.
    /// </summary>
    /// <param name="assets">The working pack's asset resolver.</param>
    /// <param name="modelAddress">The resolved content address of the GLB.</param>
    /// <param name="ready">Reflows the form when the preview becomes visible.</param>
    /// <param name="reportError">Reports a load error while the selection is current.</param>
    internal void Show(
        ContentAssets assets,
        string modelAddress,
        Action ready,
        Action<string> reportError
    )
    {
        if (ReferenceEquals(source, assets) && address == modelAddress)
            return;

        Clear();
        if (assets == null || string.IsNullOrWhiteSpace(modelAddress))
            return;

        source = assets;
        address = modelAddress;
        pending = new CancellationTokenSource();
        _ = LoadAsync(assets, modelAddress, pending.Token, ready, reportError);
    }

    /// <summary>
    /// Cancels the current selection and releases its scene and render resources.
    /// </summary>
    internal void Clear()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
        LoadFailed = false;
        if (TryGetComponent(out RawImage preview))
            preview.raycastTarget = false;
        if (camera != null)
            camera.enabled = false;
        ReleaseTexture();
        instance?.Dispose();
        instance = null;
        DestroyMount(modelMount);
        modelMount = null;
        foreach (Material material in previewMaterials.Values)
        {
            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
        }
        previewMaterials.Clear();
        cache?.Dispose();
        cache = null;
        source = null;
        address = null;
    }

    /// <summary>
    /// Releases rendering and model resources when the preview control is destroyed.
    /// </summary>
    private void OnDestroy() => Clear();

    /// <summary>
    /// Frames the model and retains it only when this request is still current.
    /// </summary>
    /// <param name="assets">The selected pack's assets.</param>
    /// <param name="modelAddress">The GLB address.</param>
    /// <param name="cancellation">The token invalidated when the selection changes.</param>
    /// <param name="ready">The callback that reveals the rendered texture.</param>
    /// <param name="reportError">The callback that reports a GLB load failure.</param>
    /// <returns>The model load and framing task.</returns>
    private async Task LoadAsync(
        ContentAssets assets,
        string modelAddress,
        CancellationToken cancellation,
        Action ready,
        Action<string> reportError
    )
    {
        ContentModelCache loading = new ContentModelCache(assets);
        ContentModelInstance loaded = null;
        GameObject mount = null;
        try
        {
            // glTFast may use the supplied parent as the imported scene root.
            mount = new GameObject("Preview model");
            mount.transform.SetParent(camera.transform.parent, false);
            loaded = await loading.InstantiateAsync(modelAddress, mount.transform, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!Frame(loaded.ModelRoot, mount.transform))
                throw new InvalidOperationException("The GLB has no renderable model.");

            CreateTexture();
            camera.enabled = true;
            GetComponent<RawImage>().raycastTarget = true;
            cache = loading;
            instance = loaded;
            modelMount = mount;
            mount = null;
            loaded = null;
            loading = null;
            ready();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!cancellation.IsCancellationRequested)
            {
                LoadFailed = true;
                reportError($"Model preview could not load. {exception.Message}");
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            loaded?.Dispose();
            DestroyMount(mount);
            loading?.Dispose();
        }
    }

    /// <summary>
    /// Removes a request's scene parent and hides its geometry immediately.
    /// </summary>
    /// <param name="mount">The model's private scene parent.</param>
    private static void DestroyMount(GameObject mount)
    {
        if (mount == null)
            return;
        mount.SetActive(false);
        if (Application.isPlaying)
            Object.Destroy(mount);
        else
            Object.DestroyImmediate(mount);
    }

    /// <summary>
    /// Fits the entire model once and sets its initial three-quarter pose.
    /// </summary>
    /// <param name="model">The instantiated GLB hierarchy.</param>
    /// <param name="mount">The model's scene parent used for rotation.</param>
    /// <returns>True when there is an enabled renderer to preview.</returns>
    private bool Frame(Transform model, Transform mount)
    {
        foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
            node.gameObject.layer = camera.gameObject.layer;

        Bounds? combinedBounds = null;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;
            UsePreviewMaterials(renderer);
            Bounds bounds = combinedBounds ?? renderer.bounds;
            bounds.Encapsulate(renderer.bounds);
            combinedBounds = bounds;
        }
        if (combinedBounds == null)
            return false;

        Bounds modelBounds = combinedBounds.Value;
        modelCenter = modelBounds.center;
        modelCenterInMount = mount.InverseTransformPoint(modelCenter);
        modelRadius = Mathf.Max(modelBounds.extents.magnitude, 0.01f);
        camera.transform.SetPositionAndRotation(
            modelCenter - Vector3.forward * (modelRadius * 3f + 1f),
            Quaternion.identity
        );
        camera.farClipPlane = modelRadius * 5f + 2f;
        viewAngles = _initialViewAngles;
        zoom = 1;
        RotateModel(mount);
        return true;
    }

    /// <summary>
    /// Displays the imported base color with consistent visibility from every angle.
    /// </summary>
    /// <param name="renderer">The renderer owned by this preview's model cache.</param>
    private void UsePreviewMaterials(Renderer renderer)
    {
        if (previewShader == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material sourceMaterial = materials[i];
            if (sourceMaterial == null || sourceMaterial.shader == previewShader)
                continue;
            if (!previewMaterials.TryGetValue(sourceMaterial, out Material material))
            {
                material = new Material(sourceMaterial);
                previewMaterials.Add(sourceMaterial, material);
                material.shader = previewShader;
                if (sourceMaterial.HasProperty("diffuseFactor"))
                {
                    material.SetVector(
                        "baseColorFactor",
                        sourceMaterial.GetVector("diffuseFactor")
                    );
                    material.SetTexture(
                        "baseColorTexture",
                        sourceMaterial.GetTexture("diffuseTexture")
                    );
                    material.SetVector(
                        "baseColorTexture_ST",
                        sourceMaterial.GetVector("diffuseTexture_ST")
                    );
                    material.SetFloat(
                        "baseColorTexture_Rotation",
                        sourceMaterial.GetFloat("diffuseTexture_Rotation")
                    );
                    material.SetFloat(
                        "baseColorTexture_texCoord",
                        sourceMaterial.GetFloat("diffuseTexture_texCoord")
                    );
                }
            }
            materials[i] = material;
        }
        renderer.sharedMaterials = materials;
    }

    /// <summary>
    /// Applies the view angles to the model while holding its center in place.
    /// </summary>
    /// <param name="mount">The model's scene parent.</param>
    private void RotateModel(Transform mount)
    {
        mount.rotation = Quaternion.Inverse(Quaternion.Euler(viewAngles.x, viewAngles.y, 0));
        mount.position = modelCenter - mount.TransformVector(modelCenterInMount);
    }
}
