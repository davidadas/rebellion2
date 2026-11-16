using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Presents an optional HTTPS thumbnail with bounded transport and explicit texture ownership.
/// </summary>
public sealed class RemoteImageView : MonoBehaviour
{
    private const ulong _maximumBytes = 4 * 1024 * 1024;

    [SerializeField]
    private RawImage image;

    [SerializeField]
    private GameObject placeholder;
    private UnityWebRequest request;
    private Texture2D texture;
    private string address;
    private Coroutine loading;

    /// <summary>
    /// Loads a thumbnail only when its address changes, retaining a fallback for missing or failed images.
    /// </summary>
    /// <param name="url">The optional public HTTPS image address.</param>
    public void Show(string url)
    {
        if (image == null || placeholder == null)
            throw new MissingReferenceException(name + " has incomplete remote-image references.");
        if (address == url)
            return;
        Release();
        address = url;
        if (
            !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && isActiveAndEnabled
        )
            loading = StartCoroutine(Load(url));
    }

    /// <summary>
    /// Restarts a thumbnail requested while its containing panel was hidden.
    /// </summary>
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(address) || texture != null)
            return;
        string pending = address;
        address = null;
        Show(pending);
    }

    /// <summary>
    /// Downloads a bounded thumbnail and releases stale requests before assigning a texture.
    /// </summary>
    /// <param name="url">The validated HTTPS address.</param>
    /// <returns>The asynchronous transport steps.</returns>
    private IEnumerator Load(string url)
    {
        request = UnityWebRequestTexture.GetTexture(url, true);
        request.timeout = 15;
        request.redirectLimit = 3;
        request.SetRequestHeader("User-Agent", "Rebellion2");
        UnityWebRequestAsyncOperation pending = request.SendWebRequest();
        while (!pending.isDone)
        {
            if (request.downloadedBytes > _maximumBytes)
            {
                request.Abort();
                break;
            }
            yield return null;
        }
        if (
            request.result == UnityWebRequest.Result.Success
            && request.downloadedBytes <= _maximumBytes
        )
        {
            texture = DownloadHandlerTexture.GetContent(request);
            image.texture = texture;
            image.enabled = texture != null;
            placeholder.SetActive(texture == null);
        }
        request.Dispose();
        request = null;
        loading = null;
    }

    /// <summary>
    /// Cancels hidden thumbnail work and releases the view-owned texture.
    /// </summary>
    private void OnDisable()
    {
        Release();
        address = null;
    }

    /// <summary>
    /// Releases only this component's request, coroutine and decoded texture.
    /// </summary>
    private void Release()
    {
        if (loading != null)
            StopCoroutine(loading);
        loading = null;
        request?.Abort();
        request?.Dispose();
        request = null;
        if (image != null)
        {
            image.texture = null;
            image.enabled = false;
        }
        if (placeholder)
            placeholder.SetActive(true);
        if (texture != null)
            Destroy(texture);
        texture = null;
    }
}
