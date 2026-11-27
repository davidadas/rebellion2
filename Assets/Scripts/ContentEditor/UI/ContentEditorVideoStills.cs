using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Decodes one still at a time and owns only the textures used by the visible cutscene page.
/// </summary>
internal sealed class ContentEditorVideoStills : MonoBehaviour
{
    internal readonly struct Tile
    {
        internal readonly (string Url, DateTime Modified, long Length) Source;
        internal readonly RawImage Image;
        internal readonly TMP_Text Status;
        internal readonly RectInt Bounds;

        /// <summary>
        /// Associates a local video revision with its currently visible thumbnail.
        /// </summary>
        /// <param name="source">The file URL and revision.</param>
        /// <param name="image">The thumbnail graphic.</param>
        /// <param name="status">The text shown while the thumbnail is unavailable.</param>
        /// <param name="bounds">The available room in the form.</param>
        internal Tile(
            (string Url, DateTime Modified, long Length) source,
            RawImage image,
            TMP_Text status,
            RectInt bounds
        )
        {
            Source = source;
            Image = image;
            Status = status;
            Bounds = bounds;
        }
    }

    private const int _stillWidth = 512;
    private const double _decodeTimeoutSeconds = 8;
    private readonly Dictionary<
        (string Url, DateTime Modified, long Length),
        RenderTexture
    > stills = new();
    private IReadOnlyList<Tile> tiles = Array.Empty<Tile>();
    private (string Url, DateTime Modified, long Length)[] sources = Array.Empty<(
        string,
        DateTime,
        long
    )>();
    private VideoPlayer decoder;
    private Coroutine loading;
    private bool decodeFailed;
    private long requestedFrame;
    private RenderTexture capturedStill;

    /// <summary>
    /// Refreshes bindings after a form reflow and decodes new or replaced clips.
    /// </summary>
    /// <param name="visibleTiles">The current set of resolved cutscene thumbnails.</param>
    internal void Present(IReadOnlyList<Tile> visibleTiles)
    {
        var next = visibleTiles.Select(tile => tile.Source).Distinct().ToArray();
        if (!next.SequenceEqual(sources))
        {
            StopDecoding();
            foreach (var obsolete in stills.Keys.Except(next).ToArray())
            {
                Destroy(stills[obsolete]);
                stills.Remove(obsolete);
            }
            sources = next;
        }
        tiles = visibleTiles;
        foreach (Tile tile in tiles)
            if (stills.TryGetValue(tile.Source, out RenderTexture still))
                ShowStill(tile, still);
        if (loading == null && sources.Any(source => !stills.ContainsKey(source)))
            loading = StartCoroutine(LoadStills());
    }

    /// <summary>
    /// Stops pending decodes and releases thumbnails when their page is closed.
    /// </summary>
    internal void Clear()
    {
        StopDecoding();
        foreach (RenderTexture still in stills.Values)
            Destroy(still);
        stills.Clear();
        tiles = Array.Empty<Tile>();
        sources = Array.Empty<(string, DateTime, long)>();
    }

    /// <summary>
    /// Releases the decoder when the owning form leaves the workspace.
    /// </summary>
    private void OnDisable() => Clear();

    /// <summary>
    /// Cancels the pending request and releases any frame that has yet to be presented.
    /// </summary>
    private void StopDecoding()
    {
        if (loading != null)
            StopCoroutine(loading);
        loading = null;
        if (decoder != null)
        {
            decoder.sendFrameReadyEvents = false;
            decoder.Stop();
        }
        if (capturedStill != null)
            Destroy(capturedStill);
        capturedStill = null;
    }

    /// <summary>
    /// Prepares and seeks each unresolved video using a single muted decoder.
    /// </summary>
    /// <returns>The frame-by-frame decode sequence.</returns>
    private IEnumerator LoadStills()
    {
        if (decoder == null)
        {
            decoder = gameObject.AddComponent<VideoPlayer>();
            decoder.playOnAwake = false;
            decoder.source = VideoSource.Url;
            decoder.renderMode = VideoRenderMode.APIOnly;
            decoder.audioOutputMode = VideoAudioOutputMode.None;
            decoder.frameReady += CaptureStill;
            decoder.errorReceived += HandleDecodeError;
        }
        foreach (var source in sources)
        {
            if (stills.ContainsKey(source))
                continue;
            decodeFailed = false;
            decoder.url = source.Url;
            decoder.Prepare();
            double deadline = Time.realtimeSinceStartupAsDouble + _decodeTimeoutSeconds;
            while (
                !decoder.isPrepared && !decodeFailed && Time.realtimeSinceStartupAsDouble < deadline
            )
                yield return null;
            if (decoder.isPrepared && !decodeFailed)
            {
                // A still past the opening is less likely to show a fade from black.
                requestedFrame = decoder.canSetTime ? (long)(decoder.frameCount / 4) : 0;
                decoder.sendFrameReadyEvents = true;
                if (requestedFrame > 0)
                    decoder.frame = requestedFrame;
                decoder.Pause();
                while (
                    capturedStill == null
                    && !decodeFailed
                    && Time.realtimeSinceStartupAsDouble < deadline
                )
                    yield return null;
            }
            decoder.sendFrameReadyEvents = false;
            decoder.Stop();
            stills.Add(source, capturedStill);
            foreach (Tile tile in tiles.Where(tile => tile.Source == source))
                ShowStill(tile, capturedStill);
            capturedStill = null;
        }
        loading = null;
    }

    /// <summary>
    /// Copies the requested decoder frame into a small GPU-owned thumbnail.
    /// </summary>
    /// <param name="player">The decoder that produced the frame.</param>
    /// <param name="frame">The decoded frame index.</param>
    private void CaptureStill(VideoPlayer player, long frame)
    {
        Texture texture = player.texture;
        if (
            capturedStill != null
            || frame < requestedFrame
            || texture == null
            || texture.width <= 0
            || texture.height <= 0
        )
            return;
        float scale = Mathf.Min(1f, _stillWidth / (float)Mathf.Max(texture.width, texture.height));
        int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
        capturedStill = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, capturedStill);
        player.sendFrameReadyEvents = false;
    }

    /// <summary>
    /// Ends the current thumbnail request when the decoder rejects its source.
    /// </summary>
    /// <param name="player">The active decoder.</param>
    /// <param name="message">The platform's decode failure.</param>
    private void HandleDecodeError(VideoPlayer player, string message) => decodeFailed = true;

    /// <summary>
    /// Shows the decoded frame at its own aspect ratio or retains the unavailable state.
    /// </summary>
    /// <param name="tile">The current form controls.</param>
    /// <param name="still">The captured frame, when decoding succeeded.</param>
    private static void ShowStill(Tile tile, RenderTexture still)
    {
        tile.Image.texture = still;
        if (still == null)
        {
            tile.Status.text = "Still unavailable";
            return;
        }
        tile.Status.gameObject.SetActive(false);
        tile.Image.color = Color.white;
        float scale = Mathf.Min(
            tile.Bounds.width / (float)still.width,
            tile.Bounds.height / (float)still.height
        );
        int width = Mathf.Max(1, Mathf.RoundToInt(still.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(still.height * scale));
        UILayout.SetSourceRect(
            tile.Image.rectTransform,
            tile.Bounds.x,
            tile.Bounds.y,
            width,
            height
        );
    }
}
