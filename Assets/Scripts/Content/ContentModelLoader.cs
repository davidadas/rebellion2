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
    internal ContentModelResource(GLTFast.GltfImport gltfImporter, string loadedFilePath)
    {
        importer = gltfImporter ?? throw new ArgumentNullException(nameof(gltfImporter));
        filePath = loadedFilePath;
    }

    /// <summary>
    /// Creates one scene hierarchy backed by this parsed model resource.
    /// </summary>
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
    /// Gets the transform that receives the binding's authored pose.
    /// </summary>
    public Transform ModelRoot { get; private set; }

    /// <summary>
    /// Transfers ownership of a one-off parsed resource to this model instance.
    /// </summary>
    internal void TakeOwnership(ContentModelResource resource)
    {
        ownedResource = resource ?? throw new ArgumentNullException(nameof(resource));
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
}
