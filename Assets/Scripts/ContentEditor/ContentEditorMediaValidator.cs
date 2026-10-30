using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Checks referenced media with the runtime decoders without retaining a pack-sized asset cache.
/// </summary>
internal static class ContentEditorMediaValidator
{
    /// <summary>
    /// Loads one referenced asset and releases all temporary decoder resources.
    /// </summary>
    /// <param name="resolver">The project's content layers.</param>
    /// <param name="address">The scoped media address.</param>
    /// <param name="type">The field's declared media category.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of decoding or a descriptive validation failure.</returns>
    internal static async Task ValidateAsync(
        ContentFileResolver resolver,
        string address,
        ContentPackEditorMediaType type,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        using ContentAssets assets = new ContentAssets(resolver);
        try
        {
            switch (type)
            {
                case ContentPackEditorMediaType.Image:
                    if (assets.GetTexture(address) == null)
                        throw new InvalidDataException("Image could not be decoded.");
                    break;
                case ContentPackEditorMediaType.Audio:
                    await ValidateAudioAsync(assets, address, cancellationToken);
                    break;
                case ContentPackEditorMediaType.Model:
                    await ValidateModelAsync(
                        assets.ResolveFile(address, ".glb"),
                        cancellationToken
                    );
                    break;
                case ContentPackEditorMediaType.Video:
                    await ValidateVideoAsync(assets.GetVideoUrl(address), cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException(
                $"Media could not be loaded: {address}. {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Cancels an isolated asset store when its audio request is no longer needed.
    /// </summary>
    /// <param name="assets">The store owned exclusively by this validation check.</param>
    /// <param name="address">The scoped audio address.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of loading and request cleanup.</returns>
    private static async Task ValidateAudioAsync(
        ContentAssets assets,
        string address,
        CancellationToken cancellationToken
    )
    {
        Task<AudioClip> pending = assets.LoadAudioAsync(address);
        while (!pending.IsCompleted)
        {
            if (cancellationToken.IsCancellationRequested)
                assets.Dispose();
            await Task.Yield();
        }
        try
        {
            await pending;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Instantiates the model's main scene under an inactive temporary parent before releasing it.
    /// </summary>
    /// <param name="file">The resolved model file.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of parsing and scene validation.</returns>
    private static async Task ValidateModelAsync(string file, CancellationToken cancellationToken)
    {
        GameObject host = new GameObject("Content validation model")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        host.SetActive(false);
        try
        {
            using ContentModelResource resource = await ContentModelLoader.LoadResourceAsync(
                file,
                cancellationToken
            );
            using ContentModelInstance instance = await resource.InstantiateAsync(
                host.transform,
                cancellationToken
            );
        }
        finally
        {
            DestroyHost(host);
        }
    }

    /// <summary>
    /// Prepares a video with the platform decoder without showing frames or playing its audio.
    /// </summary>
    /// <param name="url">The resolved local video URL.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of decoder preparation.</returns>
    private static async Task ValidateVideoAsync(string url, CancellationToken cancellationToken)
    {
        GameObject host = new GameObject("Content validation video")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        try
        {
            VideoPlayer player = host.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.APIOnly;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.source = VideoSource.Url;
            player.url = url;
            string failure = null;
            player.errorReceived += (_, message) => failure = message;
            player.Prepare();
            while (!player.isPrepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (failure != null)
                    throw new InvalidDataException(failure);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
                await Task.Yield();
            }
            player.Stop();
        }
        finally
        {
            DestroyHost(host);
        }
    }

    /// <summary>
    /// Releases a temporary decoder host in either the running tool or Unity's edit-mode tests.
    /// </summary>
    /// <param name="host">The exclusively owned temporary hierarchy.</param>
    private static void DestroyHost(GameObject host)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(host);
        else
            UnityEngine.Object.DestroyImmediate(host);
    }
}
