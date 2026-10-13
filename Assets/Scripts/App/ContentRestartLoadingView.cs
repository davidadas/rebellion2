using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents an application-owned hyperspace scene while a saved game loads.
/// </summary>
[DisallowMultipleComponent]
public sealed class ContentRestartLoadingView : MonoBehaviour
{
    private const string _loadingText = "Loading ...";
    private const string _shuttleRelativePath = "Application/Loading/Models/lambda-shuttle.glb";
    private const int _presentationLayer = 31;
    private const int _renderTextureWidth = 1280;
    private const int _renderTextureHeight = 720;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private RawImage shuttleViewport;

    [SerializeField]
    private Camera shuttleCamera;

    [SerializeField]
    private Transform shuttleModelRoot;

    private CancellationTokenSource cancellation;
    private ContentModelInstance modelInstance;
    private RenderTexture shuttleRenderTexture;
    private Quaternion shuttleRestRotation;
    private bool modelReady;

    /// <summary>
    /// Verifies the generated presentation, creates its render target, and seeds the starfield.
    /// </summary>
    private void Awake()
    {
        if (
            statusText == null
            || shuttleViewport == null
            || shuttleCamera == null
            || shuttleModelRoot == null
        )
        {
            throw new MissingReferenceException(
                $"{name} requires loading text, viewport, camera, and model-root references."
            );
        }

        statusText.text = _loadingText;
        cancellation = new CancellationTokenSource();
        CreateRenderTarget();
    }

    /// <summary>
    /// Starts loading the shuttle from application content before the active content pack is
    /// replaced.
    /// </summary>
    /// <param name="contentRootPath">The absolute external content root.</param>
    public void Initialize(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
            throw new ArgumentException(
                "A content root path is required.",
                nameof(contentRootPath)
            );

        cancellation ??= new CancellationTokenSource();
        _ = LoadShuttleAsync(
            Path.Combine(
                contentRootPath,
                _shuttleRelativePath.Replace('/', Path.DirectorySeparatorChar)
            ),
            cancellation.Token
        );
    }

    /// <summary>
    /// Advances the looping hyperspace streaks and gives the shuttle a subtle in-flight drift.
    /// </summary>
    private void Update()
    {
        if (!modelReady)
            return;

        float time = Time.unscaledTime;
        shuttleModelRoot.localRotation =
            shuttleRestRotation
            * Quaternion.Euler(
                Mathf.Sin(time * 0.55f) * 1.2f,
                Mathf.Sin(time * 0.31f) * 1.8f,
                Mathf.Sin(time * 0.42f) * 0.8f
            );
    }

    /// <summary>
    /// Cancels the model load and releases all runtime rendering resources.
    /// </summary>
    private void OnDestroy()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;

        modelInstance?.Dispose();
        modelInstance = null;

        if (shuttleCamera != null)
            shuttleCamera.targetTexture = null;
        if (shuttleViewport != null)
            shuttleViewport.texture = null;
        if (shuttleRenderTexture != null)
        {
            shuttleRenderTexture.Release();
            if (Application.isPlaying)
                Destroy(shuttleRenderTexture);
            else
                DestroyImmediate(shuttleRenderTexture);
            shuttleRenderTexture = null;
        }
    }

    /// <summary>
    /// Creates the transparent render target composited over the hyperspace starfield.
    /// </summary>
    private void CreateRenderTarget()
    {
        shuttleRenderTexture = new RenderTexture(
            _renderTextureWidth,
            _renderTextureHeight,
            24,
            RenderTextureFormat.ARGB32
        )
        {
            name = "Loading Shuttle",
            antiAliasing = 4,
            useMipMap = false,
        };
        shuttleRenderTexture.Create();
        shuttleCamera.targetTexture = shuttleRenderTexture;
        shuttleViewport.texture = shuttleRenderTexture;
    }

    /// <summary>
    /// Loads, instantiates, and frames the shuttle independently from the active content services.
    /// </summary>
    /// <param name="filePath">The absolute shuttle GLB path.</param>
    /// <param name="cancellationToken">Cancels loading when the presentation closes.</param>
    /// <returns>A task representing the asynchronous model load.</returns>
    private async Task LoadShuttleAsync(string filePath, CancellationToken cancellationToken)
    {
        ContentModelResource resource = null;
        ContentModelInstance loadedInstance = null;
        try
        {
            resource = await ContentModelLoader.LoadResourceAsync(filePath, cancellationToken);
            loadedInstance = await resource.InstantiateAsync(shuttleModelRoot, cancellationToken);
            loadedInstance.TakeOwnership(resource);
            resource = null;
            cancellationToken.ThrowIfCancellationRequested();

            modelInstance = loadedInstance;
            loadedInstance = null;
            PoseShuttle(modelInstance.ModelRoot);
            modelReady = true;
        }
        catch (OperationCanceledException)
        {
            // The loading page completed before its decorative model finished loading.
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Loading shuttle unavailable; continuing with hyperspace.\n{exception}"
            );
        }
        finally
        {
            loadedInstance?.Dispose();
            resource?.Dispose();
        }
    }

    /// <summary>
    /// Normalizes, centers, lights, and angles the loaded shuttle for the loading composition.
    /// </summary>
    /// <param name="model">The instantiated shuttle model.</param>
    private void PoseShuttle(Transform model)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        PoseWingsForFlight(model);
        if (TryGetBounds(model, out Bounds bounds))
        {
            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension > 0f)
                model.localScale *= 1.65f / largestDimension;

            if (TryGetBounds(model, out bounds))
                model.position += shuttleModelRoot.position - bounds.center;
        }

        SetLayerRecursively(model.gameObject, _presentationLayer);
        shuttleRestRotation = shuttleModelRoot.localRotation;
    }

    /// <summary>
    /// Samples the shuttle's authored wing motion at its fully deployed in-flight pose.
    /// </summary>
    /// <param name="model">The instantiated shuttle model.</param>
    private static void PoseWingsForFlight(Transform model)
    {
        Animation animation = model.GetComponentInParent<Animation>();
        if (animation == null || animation.clip == null)
            return;

        AnimationState state = animation[animation.clip.name];
        if (state == null)
            return;

        state.enabled = true;
        state.weight = 1f;
        state.time = state.length * 0.5f;
        animation.Sample();
        animation.enabled = false;
    }

    /// <summary>
    /// Computes the combined renderer bounds beneath a model root.
    /// </summary>
    /// <param name="model">The model to measure.</param>
    /// <param name="bounds">The combined bounds when renderers exist.</param>
    /// <returns>True when the hierarchy contains at least one renderer.</returns>
    private static bool TryGetBounds(Transform model, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return true;
    }

    /// <summary>
    /// Assigns the dedicated presentation layer to a hierarchy.
    /// </summary>
    /// <param name="model">The hierarchy root.</param>
    /// <param name="layer">The layer to assign.</param>
    private static void SetLayerRecursively(GameObject model, int layer)
    {
        model.layer = layer;
        foreach (Transform child in model.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
