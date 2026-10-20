using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Loads GLB models from installation content into a scene hierarchy using glTFast. This is kept
/// separate from <see cref="ContentAssets"/>, which owns synchronous textures and preloaded audio.
/// </summary>
public static class ContentModelLoader
{
    /// <summary>
    /// Parses a GLB into a reusable model resource without instantiating its scene.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="cancellationToken">Cancels GLB loading.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task<ContentModelResource> LoadResourceAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A GLB file path is required.", nameof(filePath));

        GLTFast.IDeferAgent deferAgent = Application.isPlaying
            ? null
            : new GLTFast.UninterruptedDeferAgent();
        GLTFast.GltfImport gltf = new GLTFast.GltfImport(deferAgent: deferAgent);
        try
        {
            bool loaded = await gltf.LoadFile(filePath, null, null, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!loaded)
                throw new InvalidOperationException($"glTFast rejected the GLB file: {filePath}");

            return new ContentModelResource(gltf, filePath);
        }
        catch (Exception exception)
        {
            gltf.Dispose();
            if (exception is OperationCanceledException)
                throw;
            throw new InvalidOperationException($"Failed to load GLB model: {filePath}", exception);
        }
    }

    /// <summary>
    /// Parses an in-memory GLB into a reusable model resource without instantiating its scene.
    /// </summary>
    /// <param name="bytes">The complete GLB payload.</param>
    /// <param name="assetName">The diagnostic name of the packaged model.</param>
    /// <param name="cancellationToken">Cancels GLB loading.</param>
    /// <returns>A task containing the parsed model resource.</returns>
    internal static async Task<ContentModelResource> LoadResourceAsync(
        byte[] bytes,
        string assetName,
        CancellationToken cancellationToken
    )
    {
        if (bytes == null || bytes.Length == 0)
            throw new ArgumentException("A GLB payload is required.", nameof(bytes));
        if (string.IsNullOrWhiteSpace(assetName))
            throw new ArgumentException(
                "A packaged GLB asset name is required.",
                nameof(assetName)
            );

        GLTFast.IDeferAgent deferAgent = Application.isPlaying
            ? null
            : new GLTFast.UninterruptedDeferAgent();
        GLTFast.GltfImport gltf = new GLTFast.GltfImport(deferAgent: deferAgent);
        try
        {
            bool loaded = await gltf.Load(bytes, null, null, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!loaded)
                throw new InvalidOperationException($"glTFast rejected the GLB asset: {assetName}");

            return new ContentModelResource(gltf, assetName);
        }
        catch (Exception exception)
        {
            gltf.Dispose();
            if (exception is OperationCanceledException)
                throw;
            throw new InvalidOperationException(
                $"Failed to load packaged GLB model: {assetName}",
                exception
            );
        }
    }
}

/// <summary>
/// Owns one parsed GLB and can instantiate its main scene repeatedly.
/// </summary>
internal sealed class ContentModelResource : IDisposable
{
    private GLTFast.GltfImport importer;
    private readonly string filePath;

    /// <summary>
    /// Takes ownership of one successfully parsed glTFast resource.
    /// </summary>
    /// <param name="gltfImporter">The gltf importer.</param>
    /// <param name="loadedFilePath">The loaded file path.</param>
    internal ContentModelResource(GLTFast.GltfImport gltfImporter, string loadedFilePath)
    {
        importer = gltfImporter ?? throw new ArgumentNullException(nameof(gltfImporter));
        filePath = loadedFilePath;
    }

    /// <summary>
    /// Creates one scene hierarchy backed by this parsed model resource.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="cancellationToken">Cancels scene instantiation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<ContentModelInstance> InstantiateAsync(
        Transform parent,
        CancellationToken cancellationToken
    )
    {
        if (importer == null)
            throw new ObjectDisposedException(nameof(ContentModelResource));
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        GLTFast.GameObjectInstantiator instantiator = new GLTFast.GameObjectInstantiator(
            importer,
            parent
        );
        Transform sceneRoot = null;
        bool ownershipTransferred = false;
        try
        {
            bool instantiated = await importer.InstantiateMainSceneAsync(
                instantiator,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();
            if (!instantiated)
                throw new InvalidOperationException($"glTFast could not instantiate: {filePath}");

            sceneRoot = instantiator.SceneTransform;
            if (sceneRoot == null)
                throw new InvalidOperationException($"GLB scene root is missing: {filePath}");

            Transform modelRoot = sceneRoot.childCount == 1 ? sceneRoot.GetChild(0) : sceneRoot;
            ContentModelInstance result = new ContentModelInstance(sceneRoot, modelRoot);
            ownershipTransferred = true;
            return result;
        }
        finally
        {
            Transform failedSceneRoot = sceneRoot ?? instantiator.SceneTransform;
            if (!ownershipTransferred && failedSceneRoot != null)
                ContentModelInstance.DestroyHierarchy(failedSceneRoot);
        }
    }

    /// <summary>
    /// Releases all meshes, materials, and textures owned by the parsed GLB.
    /// </summary>
    public void Dispose()
    {
        importer?.Dispose();
        importer = null;
    }
}

/// <summary>
/// Owns one instantiated GLB hierarchy and the imported resources backing it.
/// </summary>
public sealed class ContentModelInstance : IDisposable
{
    private ContentModelResource ownedResource;
    private Transform sceneRoot;

    /// <summary>
    /// Gets the transform that receives the binding's authored pose.
    /// </summary>
    public Transform ModelRoot { get; private set; }

    /// <summary>
    /// Initializes ownership of one instantiated GLB scene.
    /// </summary>
    /// <param name="loadedSceneRoot">The complete instantiated scene hierarchy.</param>
    /// <param name="modelRoot">The model transform that receives authored posing.</param>
    internal ContentModelInstance(Transform loadedSceneRoot, Transform modelRoot)
    {
        sceneRoot = loadedSceneRoot
            ? loadedSceneRoot
            : throw new ArgumentNullException(nameof(loadedSceneRoot));
        ModelRoot = modelRoot ? modelRoot : throw new ArgumentNullException(nameof(modelRoot));
    }

    /// <summary>
    /// Transfers ownership of a one-off parsed resource to this model instance.
    /// </summary>
    /// <param name="resource">The resource.</param>
    internal void TakeOwnership(ContentModelResource resource)
    {
        ownedResource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    /// <summary>
    /// Applies authored posing to the instantiated model hierarchy.
    /// </summary>
    /// <param name="pivot">The transform whose origin receives the centered model.</param>
    /// <param name="modelScale">The uniform scale applied after optional normalization.</param>
    /// <param name="rotationEuler">The local Euler rotation applied when overwrite is enabled.</param>
    /// <param name="overwriteRotation">Whether the imported rotation is replaced.</param>
    /// <param name="normalizeToUnitDiameter">Whether the model is first normalized to a diameter of two.</param>
    /// <param name="centerOnPivot">Whether the rendered bounds are centered on the pivot.</param>
    /// <param name="contentLayer">The layer assigned recursively, or a negative value to preserve layers.</param>
    internal void ApplyPose(
        Transform pivot,
        float modelScale,
        Vector3 rotationEuler,
        bool overwriteRotation,
        bool normalizeToUnitDiameter,
        bool centerOnPivot,
        int contentLayer
    )
    {
        if (pivot == null)
            throw new ArgumentNullException(nameof(pivot));
        if (ModelRoot == null)
            throw new ObjectDisposedException(nameof(ContentModelInstance));

        ModelRoot.localPosition = Vector3.zero;
        if (overwriteRotation)
            ModelRoot.localRotation = Quaternion.Euler(rotationEuler);
        if (normalizeToUnitDiameter)
            NormalizeToUnitDiameter(ModelRoot, pivot);
        if (!Mathf.Approximately(modelScale, 1f))
            ModelRoot.localScale *= modelScale;
        if (centerOnPivot)
            CenterOnPivot(pivot, ModelRoot);
        if (contentLayer >= 0)
            SetLayerRecursively(ModelRoot.gameObject, contentLayer);
    }

    /// <summary>
    /// Destroys the instantiated hierarchy and releases all imported resources.
    /// </summary>
    public void Dispose()
    {
        if (sceneRoot != null)
            DestroyHierarchy(sceneRoot);
        sceneRoot = null;
        ModelRoot = null;

        ownedResource?.Dispose();
        ownedResource = null;
    }

    /// <summary>
    /// Destroys an instantiated model correctly in both player and editor test contexts.
    /// </summary>
    /// <param name="root">The instantiated scene hierarchy.</param>
    internal static void DestroyHierarchy(Transform root)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(root.gameObject);
        else
            UnityEngine.Object.DestroyImmediate(root.gameObject);
    }

    /// <summary>
    /// Scales a model so its largest pivot-local dimension spans two units, independent of heading.
    /// </summary>
    /// <param name="model">The model to scale.</param>
    /// <param name="pivot">The reference frame for authored model dimensions.</param>
    private static void NormalizeToUnitDiameter(Transform model, Transform pivot)
    {
        if (!TryGetBounds(model, pivot, out Bounds bounds))
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
        if (TryGetBounds(model, pivot, out Bounds bounds))
            model.position -= pivot.TransformVector(bounds.center);
    }

    /// <summary>
    /// Computes renderer envelopes in the reference frame before its world rotation is applied.
    /// </summary>
    /// <param name="model">The model to measure.</param>
    /// <param name="reference">The coordinate frame in which to measure the model.</param>
    /// <param name="bounds">The combined reference-local bounds when renderers exist.</param>
    /// <returns>True when the model has at least one renderer.</returns>
    internal static bool TryGetBounds(Transform model, Transform reference, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = reference.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(
                    local.center
                        + Vector3.Scale(
                            local.extents,
                            new Vector3(
                                (corner & 1) == 0 ? -1f : 1f,
                                (corner & 2) == 0 ? -1f : 1f,
                                (corner & 4) == 0 ? -1f : 1f
                            )
                        )
                );
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(point);
            }
        }
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
