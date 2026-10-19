using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one selected value with previous and next step controls.
/// </summary>
public sealed class OptionsChoiceFieldView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI valueField;

    [SerializeField]
    private Button previousButton;

    [SerializeField]
    private Button nextButton;

    private bool bound;

    /// <summary>
    /// Raised when the player requests the previous or next value.
    /// </summary>
    public event Action<int> StepRequested;

    /// <summary>
    /// Configures the reusable selector from authored controls.
    /// </summary>
    /// <param name="value">The selected-value label.</param>
    /// <param name="previous">The previous-value button.</param>
    /// <param name="next">The next-value button.</param>
    public void Configure(TextMeshProUGUI value, Button previous, Button next)
    {
        valueField = value ?? throw new ArgumentNullException(nameof(value));
        previousButton = previous ?? throw new ArgumentNullException(nameof(previous));
        nextButton = next ?? throw new ArgumentNullException(nameof(next));
        BindControls();
    }

    /// <summary>
    /// Displays the selected value.
    /// </summary>
    /// <param name="value">The player-facing value.</param>
    public void Render(string value)
    {
        UILayout.SetTextContent(valueField, value ?? string.Empty);
    }

    /// <summary>
    /// Clones an authored Options selector into another settings page.
    /// </summary>
    /// <param name="parent">The destination row.</param>
    /// <param name="name">The cloned selector name.</param>
    /// <param name="sourceBadge">The authored badge background.</param>
    /// <param name="sourceValue">The authored value label.</param>
    /// <param name="sourcePrevious">The authored previous button.</param>
    /// <param name="sourceNext">The authored next button.</param>
    /// <param name="x">The destination left coordinate.</param>
    /// <param name="y">The destination top coordinate.</param>
    /// <returns>The configured cloned selector.</returns>
    public static OptionsChoiceFieldView Clone(
        Transform parent,
        string name,
        Image sourceBadge,
        TextMeshProUGUI sourceValue,
        Button sourcePrevious,
        Button sourceNext,
        int x,
        int y
    )
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));
        if (sourceBadge == null)
            throw new ArgumentNullException(nameof(sourceBadge));

        RectInt badgeRect = UILayout.GetSourceRect(sourceBadge.rectTransform);
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        UILayout.SetSourceRect(
            (RectTransform)root.transform,
            x,
            y,
            badgeRect.width,
            badgeRect.height
        );

        Image badge = Instantiate(sourceBadge, root.transform, false);
        badge.name = "Badge";
        SetRelativeRect(badge.rectTransform, badgeRect, badgeRect);
        Button previous = Instantiate(sourcePrevious, root.transform, false);
        previous.name = "Previous";
        SetRelativeRect((RectTransform)previous.transform, badgeRect, sourcePrevious.transform);
        TextMeshProUGUI value = Instantiate(sourceValue, root.transform, false);
        value.name = "Value";
        SetRelativeRect(value.rectTransform, badgeRect, sourceValue.rectTransform);
        Button next = Instantiate(sourceNext, root.transform, false);
        next.name = "Next";
        SetRelativeRect((RectTransform)next.transform, badgeRect, sourceNext.transform);

        OverflowTextTooltip tooltip = value.GetComponent<OverflowTextTooltip>();
        tooltip.Configure(value);
        previous.GetComponent<OverflowTextTooltipTrigger>().Configure(tooltip);
        next.GetComponent<OverflowTextTooltipTrigger>().Configure(tooltip);

        OptionsChoiceFieldView choice = root.AddComponent<OptionsChoiceFieldView>();
        choice.Configure(value, previous, next);
        return choice;
    }

    /// <summary>
    /// Binds authored controls after deserialization.
    /// </summary>
    private void Awake()
    {
        if (valueField != null && previousButton != null && nextButton != null)
            BindControls();
    }

    /// <summary>
    /// Removes listeners installed by this component.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;

        previousButton.onClick.RemoveListener(RequestPrevious);
        nextButton.onClick.RemoveListener(RequestNext);
    }

    /// <summary>
    /// Installs previous and next button listeners once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        previousButton.onClick.AddListener(RequestPrevious);
        nextButton.onClick.AddListener(RequestNext);
        bound = true;
    }

    /// <summary>
    /// Requests the previous value.
    /// </summary>
    private void RequestPrevious() => StepRequested?.Invoke(-1);

    /// <summary>
    /// Requests the next value.
    /// </summary>
    private void RequestNext() => StepRequested?.Invoke(1);

    /// <summary>
    /// Repositions one cloned control relative to its source badge.
    /// </summary>
    /// <param name="target">The cloned control.</param>
    /// <param name="sourceBadgeRect">The source badge rectangle.</param>
    /// <param name="source">The source control.</param>
    private static void SetRelativeRect(
        RectTransform target,
        RectInt sourceBadgeRect,
        Transform source
    )
    {
        SetRelativeRect(target, sourceBadgeRect, UILayout.GetSourceRect((RectTransform)source));
    }

    /// <summary>
    /// Repositions one cloned control rectangle relative to its source badge.
    /// </summary>
    /// <param name="target">The cloned control.</param>
    /// <param name="sourceBadgeRect">The source badge rectangle.</param>
    /// <param name="sourceRect">The source control rectangle.</param>
    private static void SetRelativeRect(
        RectTransform target,
        RectInt sourceBadgeRect,
        RectInt sourceRect
    )
    {
        UILayout.SetSourceRect(
            target,
            sourceRect.x - sourceBadgeRect.x,
            sourceRect.y - sourceBadgeRect.y,
            sourceRect.width,
            sourceRect.height
        );
    }
}
