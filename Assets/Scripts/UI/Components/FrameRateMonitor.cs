using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents an optional application-wide frame-rate counter above scene UI.
/// </summary>
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public sealed class FrameRateMonitor : MonoBehaviour
{
    private const float _sampleIntervalSeconds = 0.5f;
    private Func<bool> _isVisible;
    private TMP_Text _label;
    private float _elapsedSeconds;
    private int _sampledFrames;

    /// <summary>
    /// Returns the monitor beneath the supplied application root, creating it when absent.
    /// </summary>
    /// <param name="parent">The persistent application root.</param>
    /// <param name="isVisible">Returns whether the counter should currently be visible.</param>
    /// <returns>The initialized frame-rate monitor.</returns>
    public static FrameRateMonitor EnsureExists(Transform parent, Func<bool> isVisible)
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        FrameRateMonitor monitor = parent.GetComponentInChildren<FrameRateMonitor>(true);
        if (monitor == null)
        {
            GameObject root = new GameObject(
                "Frame Rate Monitor",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(FrameRateMonitor)
            );
            root.transform.SetParent(parent, false);
            monitor = root.GetComponent<FrameRateMonitor>();
        }

        monitor.Initialize(isVisible);
        return monitor;
    }

    /// <summary>
    /// Creates the presentation before the first rendered frame.
    /// </summary>
    private void Awake()
    {
        EnsurePresentation();
    }

    /// <summary>
    /// Samples unscaled frame time and refreshes the counter twice per second while visible.
    /// </summary>
    private void Update()
    {
        bool visible = _isVisible?.Invoke() == true;
        if (_label.gameObject.activeSelf != visible)
        {
            _label.gameObject.SetActive(visible);
            ResetSample();
        }

        if (!visible)
            return;

        _elapsedSeconds += Time.unscaledDeltaTime;
        _sampledFrames++;
        if (_elapsedSeconds < _sampleIntervalSeconds)
            return;

        _label.SetText("{0:0} FPS", _sampledFrames / _elapsedSeconds);
        _elapsedSeconds = 0f;
        _sampledFrames = 0;
    }

    /// <summary>
    /// Supplies the persisted visibility preference used by the monitor.
    /// </summary>
    /// <param name="isVisible">Returns whether the counter should currently be visible.</param>
    private void Initialize(Func<bool> isVisible)
    {
        _isVisible = isVisible ?? throw new ArgumentNullException(nameof(isVisible));
        EnsurePresentation();
        _label.gameObject.SetActive(_isVisible());
        ResetSample();
    }

    /// <summary>
    /// Creates and configures the overlay canvas and label when they do not yet exist.
    /// </summary>
    private void EnsurePresentation()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (_label != null)
            return;

        GameObject labelObject = new GameObject("Frame Rate", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.anchorMin = Vector2.one;
        labelTransform.anchorMax = Vector2.one;
        labelTransform.pivot = Vector2.one;
        labelTransform.anchoredPosition = new Vector2(-12f, -12f);
        labelTransform.sizeDelta = new Vector2(120f, 24f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "-- FPS";
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.TopRight;
        label.color = Color.white;
        label.raycastTarget = false;
        _label = label;
    }

    /// <summary>
    /// Clears the active averaging window and restores the placeholder label.
    /// </summary>
    private void ResetSample()
    {
        _elapsedSeconds = 0f;
        _sampledFrames = 0;
        _label.text = "-- FPS";
    }
}
