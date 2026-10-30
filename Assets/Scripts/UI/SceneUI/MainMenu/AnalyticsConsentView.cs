using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the first-run analytics choice and emits the player's explicit decision.
/// </summary>
public sealed class AnalyticsConsentView : MonoBehaviour
{
    [SerializeField]
    private Button _allowButton;

    [SerializeField]
    private Button _declineButton;

    [SerializeField]
    private RawImage _backdrop;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    private bool _bound;
    private Coroutine _captureRoutine;
    private RenderTexture _backdropTexture;

    /// <summary>
    /// Gets whether the analytics choice currently blocks the Main Menu.
    /// </summary>
    public bool IsVisible => gameObject.activeSelf;

    /// <summary>
    /// Occurs when the player allows analytics collection.
    /// </summary>
    public event Action Allowed;

    /// <summary>
    /// Occurs when the player declines analytics collection.
    /// </summary>
    public event Action Declined;

    /// <summary>
    /// Verifies and binds the authored controls when the page becomes active.
    /// </summary>
    private void OnEnable()
    {
        VerifyReferences();
        BindControls();
        _canvasGroup.alpha = 0f;
        _captureRoutine = StartCoroutine(CaptureBackdrop());
    }

    /// <summary>
    /// Removes control listeners while the page is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (_captureRoutine != null)
        {
            StopCoroutine(_captureRoutine);
            _captureRoutine = null;
        }

        ReleaseBackdrop();
        _canvasGroup.alpha = 1f;
        if (!_bound)
            return;

        _allowButton.onClick.RemoveListener(HandleAllowed);
        _declineButton.onClick.RemoveListener(HandleDeclined);
        _bound = false;
    }

    /// <summary>
    /// Shows or hides the analytics choice page.
    /// </summary>
    /// <param name="visible">Whether the page should block the Main Menu.</param>
    public void Render(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    /// <summary>
    /// Ensures every authored button reference is assigned.
    /// </summary>
    internal void VerifyReferences()
    {
        if (
            _allowButton == null
            || _declineButton == null
            || _backdrop == null
            || _canvasGroup == null
        )
            throw new MissingReferenceException($"{name} has incomplete analytics controls.");
    }

    /// <summary>
    /// Captures the unobscured Main Menu once, downsamples it for a soft-focus backdrop, and then
    /// reveals the consent page.
    /// </summary>
    /// <returns>An operation that completes after the current frame renders.</returns>
    private IEnumerator CaptureBackdrop()
    {
        yield return new WaitForEndOfFrame();

        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        if (capture != null)
        {
            capture.filterMode = FilterMode.Bilinear;
            int width = Mathf.Max(1, capture.width / 4);
            int height = Mathf.Max(1, capture.height / 4);
            _backdropTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "AnalyticsConsentBackdrop",
            };
            _backdropTexture.Create();
            RenderTexture blurTexture = RenderTexture.GetTemporary(
                Mathf.Max(1, capture.width / 12),
                Mathf.Max(1, capture.height / 12),
                0,
                RenderTextureFormat.ARGB32
            );
            blurTexture.filterMode = FilterMode.Bilinear;
            Graphics.Blit(capture, blurTexture);
            Graphics.Blit(blurTexture, _backdropTexture);
            RenderTexture.ReleaseTemporary(blurTexture);
            Destroy(capture);
            _backdrop.texture = _backdropTexture;
            _backdrop.color = Color.white;
        }

        _canvasGroup.alpha = 1f;
        _captureRoutine = null;
    }

    /// <summary>
    /// Releases the one-shot backdrop and clears it from the authored image.
    /// </summary>
    private void ReleaseBackdrop()
    {
        _backdrop.texture = null;
        _backdrop.color = Color.clear;
        if (_backdropTexture == null)
            return;

        _backdropTexture.Release();
        Destroy(_backdropTexture);
        _backdropTexture = null;
    }

    /// <summary>
    /// Adds button listeners once.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;

        _allowButton.onClick.AddListener(HandleAllowed);
        _declineButton.onClick.AddListener(HandleDeclined);
        _bound = true;
    }

    /// <summary>
    /// Emits the player's affirmative analytics decision.
    /// </summary>
    private void HandleAllowed()
    {
        Allowed?.Invoke();
    }

    /// <summary>
    /// Emits the player's negative analytics decision.
    /// </summary>
    private void HandleDeclined()
    {
        Declined?.Invoke();
    }
}
