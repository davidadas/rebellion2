using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private GameObject _previousSelection;

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
        ConfigureNavigation();
        FocusInitialControl();
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
        RestorePreviousSelection();
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
    /// Keeps keyboard and gamepad focus inside the active consent page.
    /// </summary>
    private void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != _allowButton.gameObject && selected != _declineButton.gameObject)
            eventSystem.SetSelectedGameObject(_allowButton.gameObject);
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

        Texture2D capture = null;
        RenderTexture halfResolution = null;
        RenderTexture eighthResolution = null;
        try
        {
            capture = ScreenCapture.CaptureScreenshotAsTexture();
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
                halfResolution = RenderTexture.GetTemporary(
                    Mathf.Max(1, capture.width / 2),
                    Mathf.Max(1, capture.height / 2),
                    0,
                    RenderTextureFormat.ARGB32
                );
                eighthResolution = RenderTexture.GetTemporary(
                    Mathf.Max(1, capture.width / 8),
                    Mathf.Max(1, capture.height / 8),
                    0,
                    RenderTextureFormat.ARGB32
                );
                halfResolution.filterMode = FilterMode.Bilinear;
                eighthResolution.filterMode = FilterMode.Bilinear;
                Graphics.Blit(capture, halfResolution);
                Graphics.Blit(halfResolution, _backdropTexture);
                Graphics.Blit(_backdropTexture, eighthResolution);
                Graphics.Blit(eighthResolution, _backdropTexture);
                _backdrop.texture = _backdropTexture;
                _backdrop.color = Color.white;
            }
        }
        catch (Exception exception)
        {
            ReleaseBackdrop();
            Debug.LogWarning($"Analytics consent backdrop capture failed: {exception.Message}");
        }
        finally
        {
            if (halfResolution != null)
                RenderTexture.ReleaseTemporary(halfResolution);
            if (eighthResolution != null)
                RenderTexture.ReleaseTemporary(eighthResolution);
            if (capture != null)
                Destroy(capture);

            _canvasGroup.alpha = 1f;
            _captureRoutine = null;
        }
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
    /// Restricts directional navigation to the two consent choices.
    /// </summary>
    private void ConfigureNavigation()
    {
        Navigation allowNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = _allowButton,
            selectOnRight = _declineButton,
            selectOnUp = _allowButton,
            selectOnDown = _allowButton,
        };
        Navigation declineNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = _allowButton,
            selectOnRight = _declineButton,
            selectOnUp = _declineButton,
            selectOnDown = _declineButton,
        };
        _allowButton.navigation = allowNavigation;
        _declineButton.navigation = declineNavigation;
    }

    /// <summary>
    /// Saves the prior selection and focuses the primary consent choice.
    /// </summary>
    private void FocusInitialControl()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        _previousSelection = eventSystem.currentSelectedGameObject;
        eventSystem.SetSelectedGameObject(_allowButton.gameObject);
    }

    /// <summary>
    /// Restores the selection that was active before the consent page opened.
    /// </summary>
    private void RestorePreviousSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selection =
                _previousSelection?.activeInHierarchy == true ? _previousSelection : null;
            eventSystem.SetSelectedGameObject(selection);
        }
        _previousSelection = null;
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
