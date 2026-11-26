using System;
using System.Collections;
using Rebellion.Util.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed partial class ImagePreviewView
{
    private const double _displayTransitionTimeout = 3;

    private VideoPlayer video;
    private Action<string, bool> reportVideoStatus;
    private (
        Vector2Int Size,
        FullScreenMode Mode,
        DisplayInfo Display,
        Vector2Int Position,
        bool CursorVisible
    )? editorWindow;
    private Color overlayColor;
    private Coroutine videoTransition;
    private bool restoringVideo;

    /// <summary>
    /// Enters the selected fullscreen mode and plays the cutscene.
    /// </summary>
    /// <param name="url">The resolved local video URL.</param>
    /// <param name="resolution">The selected fullscreen resolution.</param>
    /// <param name="reportStatus">Reports a resolution substitution or playback error to the workspace.</param>
    public void ShowVideo(string url, Vector2Int resolution, Action<string, bool> reportStatus)
    {
        VerifyReferences();
        StopVideo();
        ClearCuts();
        reportVideoStatus = reportStatus;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (video == null)
        {
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.source = VideoSource.Url;
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.frameReady += ShowVideoFrame;
            video.errorReceived += HandleVideoFailure;
            video.loopPointReached += HandleVideoCompletion;
        }

        editorWindow = (
            new Vector2Int(Screen.width, Screen.height),
            Screen.fullScreenMode,
            Screen.mainWindowDisplayInfo,
            Screen.mainWindowPosition,
            Cursor.visible
        );
        Image overlay = GetComponent<Image>();
        overlayColor = overlay.color;
        overlay.color = Color.black;
        image.transform.SetParent(transform, false);
        image.transform.SetAsLastSibling();
        image.texture = null;
        image.enabled = false;
        previewPanel.gameObject.SetActive(false);
        Cursor.visible = false;
        videoTransition = StartCoroutine(PlayAfterDisplayChange(url, resolution));
    }

    /// <summary>
    /// Waits for fullscreen, then plays using the resolution applied by the display.
    /// </summary>
    /// <param name="url">The video to play after switching modes.</param>
    /// <param name="resolution">The selected resolution.</param>
    /// <returns>The asynchronous display change.</returns>
    private IEnumerator PlayAfterDisplayChange(string url, Vector2Int resolution)
    {
        const FullScreenMode mode = FullScreenMode.FullScreenWindow;
        Screen.SetResolution(resolution.x, resolution.y, mode);
        yield return null;
        yield return WaitForDisplay(mode);
        videoTransition = null;
        if (!Screen.fullScreen)
        {
            const string failure = "The display did not enter fullscreen.";
            GameLogger.Warning(failure);
            reportVideoStatus?.Invoke(failure, true);
            EndFullscreenVideo();
            yield break;
        }

        if (Screen.width != resolution.x || Screen.height != resolution.y)
            reportVideoStatus?.Invoke(
                $"Requested {resolution.x} × {resolution.y}. Fullscreen preview used {Screen.width} × {Screen.height}.",
                false
            );
        GameLogger.Log($"Cutscene preview started at {Screen.width} × {Screen.height} fullscreen.");
        video.url = url;
        video.sendFrameReadyEvents = true;
        video.Play();
    }

    /// <summary>
    /// Shows the uncropped frame at the same aspect ratio as gameplay cutscenes.
    /// </summary>
    /// <param name="player">The player supplying the frame.</param>
    /// <param name="frame">The first available frame index.</param>
    private void ShowVideoFrame(VideoPlayer player, long frame)
    {
        Texture texture = player.texture;
        if (texture == null || texture.height <= 0)
            return;
        image.texture = texture;
        image.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
        image.enabled = true;
        player.sendFrameReadyEvents = false;
    }

    /// <summary>
    /// Preserves the editor's video error reporting while restoring its window.
    /// </summary>
    /// <param name="player">The decoder that reported the failure.</param>
    /// <param name="message">The platform decoder's error message.</param>
    private void HandleVideoFailure(VideoPlayer player, string message)
    {
        if (restoringVideo || !editorWindow.HasValue)
            return;
        reportVideoStatus?.Invoke(message, true);
        EndFullscreenVideo();
    }

    /// <summary>
    /// Restores the editor when playback reaches the end.
    /// </summary>
    /// <param name="player">The player that completed its clip.</param>
    private void HandleVideoCompletion(VideoPlayer player) => EndFullscreenVideo();

    /// <summary>
    /// Ends fullscreen playback or consumes another dismissal while the window is being restored.
    /// </summary>
    /// <returns>Whether fullscreen playback owned the dismissal.</returns>
    private bool EndFullscreenVideo()
    {
        if (!editorWindow.HasValue)
            return false;
        if (restoringVideo)
            return true;
        restoringVideo = true;
        if (videoTransition != null)
            StopCoroutine(videoTransition);
        video.sendFrameReadyEvents = false;
        video.Stop();
        image.texture = null;
        image.enabled = false;
        videoTransition = StartCoroutine(RestoreEditorWindow());
        return true;
    }

    /// <summary>
    /// Returns to the editor's original size, display mode and window position.
    /// </summary>
    /// <returns>The asynchronous restoration and overlay close.</returns>
    private IEnumerator RestoreEditorWindow()
    {
        var window = editorWindow.Value;
        Screen.SetResolution(window.Size.x, window.Size.y, window.Mode);
        yield return null;
        yield return WaitForDisplay(window.Mode, window.Size);
        if (
            window.Mode == FullScreenMode.Windowed
            && Screen.fullScreenMode == window.Mode
            && Screen.mainWindowPosition != window.Position
        )
        {
            yield return Screen.MoveMainWindowTo(window.Display, window.Position);
        }
        ResetVideoOverlay(window.CursorVisible);
        editorWindow = null;
        videoTransition = null;
        restoringVideo = false;
        TryCancel();
    }

    /// <summary>
    /// Waits for a fullscreen or windowed resolution change to settle.
    /// </summary>
    /// <param name="mode">The requested window mode.</param>
    /// <param name="resolution">The original backbuffer size when restoring the editor.</param>
    /// <returns>The bounded wait for Unity's asynchronous resize.</returns>
    private static IEnumerator WaitForDisplay(FullScreenMode mode, Vector2Int? resolution = null)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + _displayTransitionTimeout;
        while (
            Time.realtimeSinceStartupAsDouble < deadline
            && (
                Screen.fullScreenMode != mode
                || resolution is { } size && (Screen.width != size.x || Screen.height != size.y)
            )
        )
            yield return null;
    }

    /// <summary>
    /// Moves the shared image back into its normal modal and restores the cursor.
    /// </summary>
    /// <param name="cursorVisible">Cursor visibility before playback.</param>
    private void ResetVideoOverlay(bool cursorVisible)
    {
        Cursor.visible = cursorVisible;
        GetComponent<Image>().color = overlayColor;
        if (image != null && artworkSlot != null)
        {
            image.transform.SetParent(artworkSlot, false);
            image.texture = null;
            image.enabled = true;
        }
        if (previewPanel != null)
            previewPanel.gameObject.SetActive(true);
    }

    /// <summary>
    /// Stops any interrupted playback during workspace teardown.
    /// </summary>
    private void StopVideo()
    {
        if (videoTransition != null)
            StopCoroutine(videoTransition);
        videoTransition = null;
        if (editorWindow is { } window)
        {
            Screen.SetResolution(window.Size.x, window.Size.y, window.Mode);
            ResetVideoOverlay(window.CursorVisible);
        }
        if (video != null)
        {
            video.sendFrameReadyEvents = false;
            video.Stop();
        }
        editorWindow = null;
        restoringVideo = false;
        reportVideoStatus = null;
    }
}
