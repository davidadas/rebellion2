using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a local working folder or a published package with explicit actions.
/// </summary>
public sealed class ContentEditorProjectRowView : MonoBehaviour
{
    internal const int Height = 112;
    internal const int Gap = 16;

    [SerializeField]
    private TMP_Text title;

    [SerializeField]
    private TMP_Text summary;

    [SerializeField]
    private TMP_Text detail;

    [SerializeField]
    private Button primary;

    [SerializeField]
    private Button secondary;

    private Action primaryAction;
    private Action secondaryAction;
    private bool bound;

    /// <summary>
    /// Reuses a row for its current metadata and available actions.
    /// </summary>
    /// <param name="name">The readable project or package name.</param>
    /// <param name="status">The authored or published version and state.</param>
    /// <param name="location">The local path or publishing description.</param>
    /// <param name="primaryLabel">The primary action label.</param>
    /// <param name="onPrimary">The primary action, or null when unavailable.</param>
    /// <param name="secondaryLabel">The optional second action label.</param>
    /// <param name="onSecondary">The optional second action.</param>
    internal void Render(
        string name,
        string status,
        string location,
        string primaryLabel,
        Action onPrimary,
        string secondaryLabel = null,
        Action onSecondary = null
    )
    {
        if (!bound)
        {
            primary.onClick.AddListener(InvokePrimary);
            secondary.onClick.AddListener(InvokeSecondary);
            bound = true;
        }
        title.text = name;
        summary.text = status;
        detail.text = location;
        primary.GetComponentInChildren<TMP_Text>().text = primaryLabel;
        secondary.GetComponentInChildren<TMP_Text>().text = secondaryLabel ?? string.Empty;
        primaryAction = onPrimary;
        secondaryAction = onSecondary;
        primary.interactable = onPrimary != null;
        secondary.gameObject.SetActive(onSecondary != null);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Fits reused controls to the current scroll viewport.
    /// </summary>
    /// <param name="top">The row's vertical position.</param>
    /// <param name="width">The available viewport width.</param>
    internal void SetLayout(int top, int width)
    {
        UILayout.SetSourceRect((RectTransform)transform, 0, top, width, Height);
        int actionWidth = width < 480 ? 88 : 112;
        int textWidth = Mathf.Max(
            1,
            width - (secondaryAction == null ? actionWidth + 48 : 2 * actionWidth + 64)
        );
        UILayout.SetSourceRect(title.rectTransform, 16, 16, textWidth, 24);
        UILayout.SetSourceRect(summary.rectTransform, 16, 44, textWidth, 20);
        UILayout.SetSourceRect(detail.rectTransform, 16, 76, width - 32, 20);
        UILayout.SetSourceRect(
            (RectTransform)primary.transform,
            width - 16 - actionWidth,
            20,
            actionWidth,
            40
        );
        UILayout.SetSourceRect(
            (RectTransform)secondary.transform,
            width - 32 - 2 * actionWidth,
            20,
            actionWidth,
            40
        );
    }

    /// <summary>
    /// Invokes the current primary action.
    /// </summary>
    private void InvokePrimary() => primaryAction?.Invoke();

    /// <summary>
    /// Invokes the current secondary action.
    /// </summary>
    private void InvokeSecondary() => secondaryAction?.Invoke();

    /// <summary>
    /// Releases the row's owned subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        primary.onClick.RemoveListener(InvokePrimary);
        secondary.onClick.RemoveListener(InvokeSecondary);
    }
}
