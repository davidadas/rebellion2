using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a focused confirmation with up to two actions and a cancel path.
/// </summary>
public sealed class ContentEditorConfirmationView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private TextMeshProUGUI message;

    [SerializeField]
    private TextMeshProUGUI heading;

    [SerializeField]
    private Button primaryButton;

    [SerializeField]
    private Button secondaryButton;

    [SerializeField]
    private Button cancelButton;
    private bool canCancel = true;

    /// <summary>
    /// Binds cancel without retaining project-specific callbacks after dismissal.
    /// </summary>
    private void Awake()
    {
        cancelButton.onClick.AddListener(Hide);
    }

    /// <summary>
    /// Opens the confirmation with explicit semantic actions.
    /// </summary>
    /// <param name="text">The decision to present.</param>
    /// <param name="primaryText">The main action label.</param>
    /// <param name="primary">The main action.</param>
    /// <param name="secondaryText">The optional alternate action label.</param>
    /// <param name="secondary">The optional alternate action.</param>
    /// <param name="allowCancel">Whether the prompt may be dismissed without choosing an action.</param>
    /// <param name="title">The optional attention banner above the message.</param>
    public void Show(
        string text,
        string primaryText,
        Action primary,
        string secondaryText = null,
        Action secondary = null,
        bool allowCancel = true,
        string title = null
    )
    {
        message.text = text;
        heading.text = title ?? string.Empty;
        heading.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(title));
        float headerHeight = string.IsNullOrEmpty(title) ? 0 : 64;
        canCancel = allowCancel;
        cancelButton.gameObject.SetActive(allowCancel);
        Bind(primaryButton, primaryText, primary);
        Bind(secondaryButton, secondaryText, secondary);
        RectTransform panel = (RectTransform)message.transform.parent;
        float height = Mathf.Max(
            44,
            message.GetPreferredValues(text, panel.rect.width - 40, float.PositiveInfinity).y
        );
        message.rectTransform.sizeDelta = new Vector2(panel.rect.width - 40, height);
        message.rectTransform.anchoredPosition = new Vector2(20, -18 - headerHeight);
        panel.sizeDelta = new Vector2(panel.rect.width, height + headerHeight + 92);
        float right = panel.rect.width - 20;
        foreach (Button button in new[] { primaryButton, secondaryButton, cancelButton })
        {
            if (!button.gameObject.activeSelf)
                continue;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(
                right - rect.rect.width,
                -height - headerHeight - 38
            );
            right -= rect.rect.width + 8;
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        primaryButton.Select();
    }

    /// <summary>
    /// Clears retained actions when the user dismisses the prompt.
    /// </summary>
    private void Hide()
    {
        primaryButton.onClick.RemoveAllListeners();
        secondaryButton.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Handles cancellation, dismissing only prompts that permit an unconfirmed exit.
    /// </summary>
    /// <returns>True when a visible prompt handled the request.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        if (!canCancel)
            return true;
        Hide();
        return true;
    }

    /// <summary>
    /// Rebinds one action without accumulating listeners from previous prompts.
    /// </summary>
    /// <param name="button">The authored action button.</param>
    /// <param name="text">The action label.</param>
    /// <param name="action">The selected action.</param>
    private void Bind(Button button, string text, Action action)
    {
        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(action != null);
        button.GetComponentInChildren<TextMeshProUGUI>(true).text = text ?? string.Empty;
        if (action != null)
            button.onClick.AddListener(() =>
            {
                Hide();
                action();
            });
    }
}
