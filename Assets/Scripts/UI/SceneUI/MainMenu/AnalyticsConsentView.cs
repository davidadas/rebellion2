using System;
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

    private bool _bound;

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
    }

    /// <summary>
    /// Removes control listeners while the page is inactive.
    /// </summary>
    private void OnDisable()
    {
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
        if (_allowButton == null || _declineButton == null)
            throw new MissingReferenceException($"{name} has incomplete analytics controls.");
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
