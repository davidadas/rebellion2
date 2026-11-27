using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Displays and reorders a named item with a version and room for an optional trailing control.
/// </summary>
public sealed class SortableRowView : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const int RowHeight = 32;
    public const int Gap = 6;

    [SerializeField]
    private CardReorderGrip grip;

    [SerializeField]
    private TextMeshProUGUI title;

    [SerializeField]
    private TextMeshProUGUI version;

    [SerializeField]
    private TextMeshProUGUI details;

    [SerializeField]
    private RectTransform trailing;

    private Sprite idleArtwork;
    private Sprite activeArtwork;
    private bool lifted;

    public string ItemID { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    public CardReorderGrip Grip => grip;
    public TextMeshProUGUI Title => title;
    public TextMeshProUGUI Version => version;
    public TextMeshProUGUI Details => details;
    public RectTransform Trailing => trailing;

    public event Action<PointerEventData> DragStarted;
    public event Action<PointerEventData> Dragged;
    public event Action<PointerEventData> DragEnded;

    /// <summary>
    /// Displays an item in the shared bar and enables reordering when it can move.
    /// </summary>
    /// <param name="id">The item's stable identity.</param>
    /// <param name="name">The visible item name.</param>
    /// <param name="release">The displayed version.</param>
    /// <param name="description">Optional information shown below the name.</param>
    /// <param name="canReorder">Whether the bar can be moved.</param>
    public void Render(string id, string name, string release, string description, bool canReorder)
    {
        ItemID = id;
        title.text = name;
        version.text = release;
        details.text = description;
        details.gameObject.SetActive(!string.IsNullOrEmpty(description));
        grip.gameObject.SetActive(canReorder);
        interactable = canReorder;
    }

    /// <summary>
    /// Places the grip, name, version and optional control inside the same row surface.
    /// </summary>
    /// <param name="left">The row's left coordinate in its list.</param>
    /// <param name="top">The row's top coordinate in its list.</param>
    /// <param name="width">The row width.</param>
    /// <param name="trailingWidth">The width reserved for the host control.</param>
    /// <returns>The full height, including any details.</returns>
    public int Place(int left, int top, int width, int trailingWidth = 0)
    {
        const int padding = 12;
        const int gripWidth = 20;
        const int nameLeft = padding + gripWidth + 8;
        const int maxVersionWidth = 76;
        int controlRight = width - padding;
        int labelRight = controlRight - trailingWidth - (trailingWidth == 0 ? 0 : padding);
        int versionWidth = Mathf.Min(
            maxVersionWidth,
            Mathf.CeilToInt(version.GetPreferredValues(version.text).x)
        );
        int versionLeft = labelRight - versionWidth;
        int nameWidth = Mathf.Max(1, versionLeft - (versionWidth > 0 ? padding : 0) - nameLeft);
        UILayout.SetSourceRect(grip.rectTransform, padding, 0, gripWidth, RowHeight);
        UILayout.SetSourceRect(title.rectTransform, nameLeft, 0, nameWidth, RowHeight);
        UILayout.SetSourceRect(version.rectTransform, versionLeft, 0, versionWidth, RowHeight);
        UILayout.SetSourceRect(trailing, controlRight - trailingWidth, 0, trailingWidth, RowHeight);

        int height = RowHeight;
        if (details.gameObject.activeSelf)
        {
            int detailsWidth = Mathf.Max(1, controlRight - nameLeft);
            int detailsHeight = Mathf.CeilToInt(
                details.GetPreferredValues(details.text, detailsWidth, float.PositiveInfinity).y
            );
            UILayout.SetSourceRect(
                details.rectTransform,
                nameLeft,
                RowHeight,
                detailsWidth,
                detailsHeight
            );
            height += detailsHeight + padding;
        }

        UILayout.SetSourceRect(Rect, left, top, width, height);
        bool clipped = title.GetPreferredValues(title.text).x > nameWidth;
        title.raycastTarget = clipped;
        if (!title.TryGetComponent(out HelpTooltipView tooltip) && clipped)
            tooltip = title.gameObject.AddComponent<HelpTooltipView>();
        tooltip?.Configure(clipped ? title.text : string.Empty, captionFont: title.font);
        return height;
    }

    /// <summary>
    /// Applies optional theme artwork to the shared row surface.
    /// </summary>
    /// <param name="idle">The default surface sprite.</param>
    /// <param name="active">The pressed and lifted surface sprite.</param>
    internal void SetArtwork(Sprite idle, Sprite active)
    {
        idleArtwork = idle;
        activeArtwork = active;
        image.type = Image.Type.Sliced;
        image.sprite = idle;
    }

    /// <summary>
    /// Keeps pickup feedback visible while the drag preview follows the pointer.
    /// </summary>
    /// <param name="isLifted">Whether the row is held above the list.</param>
    internal void SetLifted(bool isLifted)
    {
        lifted = isLifted;
        DoStateTransition(currentSelectionState, true);
    }

    /// <summary>
    /// Renders the pointer or keyboard state using the row's theme.
    /// </summary>
    /// <param name="state">The current selectable state.</param>
    /// <param name="instant">Whether to change the tint immediately.</param>
    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(lifted ? SelectionState.Pressed : state, instant);
        if (activeArtwork != null)
            image.sprite = lifted || state == SelectionState.Pressed ? activeArtwork : idleArtwork;
    }

    /// <summary>
    /// Starts pickup from the bar while respecting a nested control's input.
    /// </summary>
    /// <param name="input">The initiating pointer event.</param>
    public void OnBeginDrag(PointerEventData input)
    {
        GameObject pressed = input.pointerPressRaycast.gameObject;
        if (
            !IsActive()
            || !IsInteractable()
            || input.button != PointerEventData.InputButton.Left
            || (pressed && pressed.transform.IsChildOf(trailing))
        )
            return;
        input.eligibleForClick = false;
        DragStarted?.Invoke(input);
    }

    /// <summary>
    /// Reports the pointer position to the owning list.
    /// </summary>
    /// <param name="input">The moving pointer.</param>
    public void OnDrag(PointerEventData input) => Dragged?.Invoke(input);

    /// <summary>
    /// Reports the release position to the owning list.
    /// </summary>
    /// <param name="input">The released pointer.</param>
    public void OnEndDrag(PointerEventData input) => DragEnded?.Invoke(input);
}
