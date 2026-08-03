using System;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
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
    /// <returns>The instantiated model root, or null when cancelled after the parent was destroyed.</returns>
    public static async Task<Transform> LoadAsync(
        string filePath,
        Transform parent,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("A GLB file path is required.", nameof(filePath));
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        GltfImport gltf = new GltfImport();
        bool loaded;
        try
        {
            loaded = await gltf.LoadFile(filePath, null, null, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to load GLB model: {filePath}", exception);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (parent == null)
            return null;
        if (!loaded)
            throw new InvalidOperationException($"Failed to load GLB model: {filePath}");

        GameObjectInstantiator instantiator = new GameObjectInstantiator(gltf, parent);
        bool instantiated = await gltf.InstantiateMainSceneAsync(instantiator, cancellationToken);
        if (!instantiated)
            throw new InvalidOperationException($"Failed to instantiate GLB model: {filePath}");

        // glTFast wraps the model in a "Scene" node; return the actual model node beneath it so the
        // caller poses the model root directly (the model node carries the FBX axis-conversion
        // rotation the rig overwrites).
        Transform sceneRoot = instantiator.SceneTransform;
        if (sceneRoot == null)
            return null;
        return sceneRoot.childCount == 1 ? sceneRoot.GetChild(0) : sceneRoot;
    }
}
