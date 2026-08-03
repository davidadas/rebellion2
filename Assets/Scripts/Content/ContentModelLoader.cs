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
    /// Loads a GLB file and instantiates its main scene under the supplied parent.
    /// </summary>
    /// <param name="filePath">The absolute path to the .glb file.</param>
    /// <param name="parent">The transform the instantiated model is parented to.</param>
    /// <param name="cancellationToken">Token that cancels the load and instantiation.</param>
    /// <returns>The loaded model and the resources that must remain alive with it.</returns>
    public static async Task<ContentModelInstance> LoadAsync(
        string filePath,
        Transform parent,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("A GLB file path is required.", nameof(filePath));
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        GLTFast.IDeferAgent deferAgent = Application.isPlaying
            ? null
            : new GLTFast.UninterruptedDeferAgent();
        GLTFast.GltfImport gltf = new GLTFast.GltfImport(deferAgent: deferAgent);
        GLTFast.GameObjectInstantiator instantiator = null;
        Transform sceneRoot = null;
        bool ownershipTransferred = false;
        try
        {
            bool loaded = await gltf.LoadFile(filePath, null, null, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (parent == null)
                throw new OperationCanceledException(cancellationToken);
            if (!loaded)
                throw new InvalidOperationException($"glTFast rejected the GLB file: {filePath}");

            instantiator = new GLTFast.GameObjectInstantiator(gltf, parent);
            bool instantiated = await gltf.InstantiateMainSceneAsync(
                instantiator,
                cancellationToken
            );
            if (!instantiated)
                throw new InvalidOperationException($"glTFast could not instantiate: {filePath}");

            cancellationToken.ThrowIfCancellationRequested();
            sceneRoot = instantiator.SceneTransform;
            if (sceneRoot == null)
                throw new InvalidOperationException($"GLB scene root is missing: {filePath}");

            // glTFast wraps the model in a "Scene" node. Pose the actual model node while retaining
            // ownership of the wrapper and importer for deterministic cleanup.
            Transform modelRoot = sceneRoot.childCount == 1 ? sceneRoot.GetChild(0) : sceneRoot;
            ContentModelInstance result = new ContentModelInstance(gltf, sceneRoot, modelRoot);
            ownershipTransferred = true;
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to load GLB model: {filePath}", exception);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                Transform failedSceneRoot = sceneRoot ?? instantiator?.SceneTransform;
                if (failedSceneRoot != null)
                    ContentModelInstance.DestroyHierarchy(failedSceneRoot);
                gltf.Dispose();
            }
        }
    }
}

/// <summary>
/// Owns one instantiated GLB hierarchy and the imported resources backing it.
/// </summary>
public sealed class ContentModelInstance : IDisposable
{
    private GLTFast.GltfImport importer;
    private Transform sceneRoot;

    /// <summary>
    /// Initializes ownership of one instantiated GLB scene.
    /// </summary>
    /// <param name="gltfImporter">The importer owning generated meshes, materials, and textures.</param>
    /// <param name="loadedSceneRoot">The complete instantiated scene hierarchy.</param>
    /// <param name="modelRoot">The model transform that receives authored posing.</param>
    internal ContentModelInstance(
        GLTFast.GltfImport gltfImporter,
        Transform loadedSceneRoot,
        Transform modelRoot
    )
    {
        importer = gltfImporter ?? throw new ArgumentNullException(nameof(gltfImporter));
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
    /// Destroys the instantiated hierarchy and releases all imported resources.
    /// </summary>
    public void Dispose()
    {
        if (sceneRoot != null)
            DestroyHierarchy(sceneRoot);
        sceneRoot = null;
        ModelRoot = null;

        importer?.Dispose();
        importer = null;
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
