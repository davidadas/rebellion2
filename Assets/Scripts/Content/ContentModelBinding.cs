using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Loads a GLB model from installation content into this object at runtime and applies the authored
/// posing (rotation, unit normalization, scale, centering, render layer) the rig would have applied
/// to a baked model. The address and posing are authored values, so they survive the player build's
/// content stripping; the model file itself ships in the ownership-gated content pack. The load is
/// cancelled on destruction so it can never instantiate into a closed scene.
/// </summary>
public sealed class ContentModelBinding : MonoBehaviour
{
    [SerializeField]
    private string address;

    [SerializeField]
    private float modelScale = 1f;

    [SerializeField]
    private Vector3 rotationEuler;

    [SerializeField]
    private bool normalizeToUnitDiameter;

    [SerializeField]
    private bool centerOnPivot;

    [SerializeField]
    private int contentLayer = -1;

    [SerializeField]
    private bool overwriteRotation = true;

    private CancellationTokenSource cancellation;

    /// <summary>
    /// Gets the stable content address of the model this binding loads.
    /// </summary>
    public string Address => address;

    /// <summary>
    /// Assigns the model address and the posing applied to it after it loads.
    /// </summary>
    /// <param name="contentAddress">The application- or pack-relative model address.</param>
    /// <param name="scale">The uniform scale multiplier applied after loading.</param>
    /// <param name="rotation">The local rotation applied to the loaded model, in Euler degrees.</param>
    /// <param name="overwrite">Whether to overwrite the model's rotation; false keeps its imported rotation.</param>
    /// <param name="normalize">Whether to first scale the model to a unit diameter.</param>
    /// <param name="center">Whether to recenter the model's bounds on this object.</param>
    /// <param name="layer">The render layer applied to the loaded model, or a negative value to leave it.</param>
    public void SetModel(
        string contentAddress,
        float scale,
        Vector3 rotation,
        bool overwrite,
        bool normalize,
        bool center,
        int layer
    )
    {
        address = contentAddress;
        modelScale = scale;
        rotationEuler = rotation;
        overwriteRotation = overwrite;
        normalizeToUnitDiameter = normalize;
        centerOnPivot = center;
        contentLayer = layer;
    }

    /// <summary>
    /// Begins the asynchronous model load when the binding becomes active.
    /// </summary>
    private void Start()
    {
        cancellation = new CancellationTokenSource();
        LoadModelAsync(cancellation.Token);
    }

    /// <summary>
    /// Cancels any in-flight load so it cannot instantiate into a destroyed hierarchy.
    /// </summary>
    private void OnDestroy()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }

    /// <summary>
    /// Resolves the model file from installation content, instantiates it, and applies posing.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the load when the binding is destroyed.</param>
    private async void LoadModelAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address))
            throw new MissingReferenceException($"{name} content model address is missing.");

        string filePath =
            AppBootstrap.Instance.GetContentAssets().ResolveFile(address, ".glb")
            ?? throw new InvalidOperationException($"Content model is missing: {address}");

        try
        {
            Transform model = await ContentModelLoader.LoadAsync(
                filePath,
                transform,
                cancellationToken
            );
            if (model != null)
                ApplyPosing(model);
        }
        catch (OperationCanceledException)
        {
            // The binding was destroyed mid-load; there is nothing to instantiate.
        }
    }

    /// <summary>
    /// Applies the authored rotation, normalization, scale, centering, and layer to a loaded model.
    /// </summary>
    /// <param name="model">The instantiated model root.</param>
    private void ApplyPosing(Transform model)
    {
        model.localPosition = Vector3.zero;
        if (overwriteRotation)
            model.localRotation = Quaternion.Euler(rotationEuler);
        if (normalizeToUnitDiameter)
            NormalizeToUnitDiameter(model);
        if (!Mathf.Approximately(modelScale, 1f))
            model.localScale *= modelScale;
        if (centerOnPivot)
            CenterOnPivot(transform, model);
        if (contentLayer >= 0)
            SetLayerRecursively(model.gameObject, contentLayer);
    }

    /// <summary>
    /// Scales a model so its largest bounds dimension spans two units.
    /// </summary>
    /// <param name="model">The model to scale.</param>
    private static void NormalizeToUnitDiameter(Transform model)
    {
        if (!TryGetBounds(model, out Bounds bounds))
            return;

        float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxExtent > 0f)
            model.localScale *= 2f / maxExtent;
    }

    /// <summary>
    /// Offsets a model so its bounds center aligns with the pivot.
    /// </summary>
    /// <param name="pivot">The transform to center on.</param>
    /// <param name="model">The model to recenter.</param>
    private static void CenterOnPivot(Transform pivot, Transform model)
    {
        if (TryGetBounds(model, out Bounds bounds))
            model.position += pivot.position - bounds.center;
    }

    /// <summary>
    /// Computes the combined world bounds of a model's renderers.
    /// </summary>
    /// <param name="model">The model to measure.</param>
    /// <param name="bounds">The combined world bounds when renderers exist.</param>
    /// <returns>True when the model has at least one renderer.</returns>
    private static bool TryGetBounds(Transform model, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    /// <summary>
    /// Assigns a layer to a model and every descendant.
    /// </summary>
    /// <param name="model">The model root.</param>
    /// <param name="layer">The layer to assign.</param>
    private static void SetLayerRecursively(GameObject model, int layer)
    {
        model.layer = layer;
        foreach (Transform child in model.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
