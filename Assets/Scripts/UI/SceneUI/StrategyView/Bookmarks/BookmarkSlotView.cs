using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored bookmark slot and reports double-click activation.
/// </summary>
public sealed class BookmarkSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    /// <summary>
    /// Raised when the slot receives a left-button double click.
    /// </summary>
    public event Action<BookmarkSlotView> DoubleClicked;

    public int Index { get; private set; }

    /// <summary>
    /// Applies one bookmark presentation using the current faction's authored geometry.
    /// </summary>
    /// <param name="index">The zero-based bookmark slot index.</param>
    /// <param name="bookmark">The immutable bookmark presentation.</param>
    /// <param name="layout">The current faction's bookmark layout.</param>
    public void Render(int index, BookmarkRenderData bookmark, StrategyBookmarkLayout layout)
    {
        VerifyReferences();
        Index = index;
        RectInt slot = GetSlot(layout, index);
        UILayout.SetSourceRect(transform as RectTransform, slot.x, slot.y, slot.width, slot.height);
        UILayout.SetSourceRect(hitAreaImage.rectTransform, 0, 0, slot.width, slot.height);
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;

        int iconWidth = GetIconWidth(layout, bookmark.IconTexture);
        int iconHeight = GetIconHeight(layout, bookmark.IconTexture);
        UILayout.SetImage(
            iconImage,
            bookmark.IconTexture,
            0,
            GetCenteredOffset(slot.height, iconHeight),
            iconWidth,
            iconHeight
        );

        int labelOffsetX = GetLabelOffsetX(layout, iconWidth);
        int labelWidth = Mathf.Max(0, slot.width - labelOffsetX);
        UILayout.SetTemplateText(
            labelTextField,
            labelTextField,
            bookmark.Label,
            Color.yellow,
            new RectInt(labelOffsetX, 0, labelWidth, slot.height)
        );
        labelTextField.alignment = TextAlignmentOptions.MidlineLeft;
        labelTextField.textWrappingMode = TextWrappingModes.NoWrap;
        labelTextField.overflowMode = TextOverflowModes.Ellipsis;
        labelTextField.enabled = true;
        labelTextField.canvasRenderer.SetAlpha(1f);
        labelTextField.ForceMeshUpdate();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Reports a left-button double click to the owning bookmark bar.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            DoubleClicked?.Invoke(this);
    }

    /// <summary>
    /// Verifies authored slot references before first use.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Verifies every authored bookmark-slot reference.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (labelTextField == null)
            throw new MissingReferenceException($"{name}/LabelTextField is missing.");
    }

    /// <summary>
    /// Gets one slot's source-space bounds from the current faction layout.
    /// </summary>
    /// <param name="layout">The current faction's bookmark layout.</param>
    /// <param name="index">The zero-based slot index.</param>
    /// <returns>The slot bounds.</returns>
    private static RectInt GetSlot(StrategyBookmarkLayout layout, int index)
    {
        return new RectInt(
            layout.StartX,
            layout.StartY + index * layout.ItemHeight,
            layout.Width,
            layout.ItemHeight
        );
    }

    /// <summary>
    /// Gets the authored or texture-derived icon width.
    /// </summary>
    /// <param name="layout">The current faction's bookmark layout.</param>
    /// <param name="texture">The displayed icon texture.</param>
    /// <returns>The icon width in source-space units.</returns>
    private static int GetIconWidth(StrategyBookmarkLayout layout, Texture2D texture)
    {
        if (layout.IconWidth > 0)
            return layout.IconWidth;

        return UILayout.GetTextureSourceWidth(texture);
    }

    /// <summary>
    /// Gets the authored or texture-derived icon height.
    /// </summary>
    /// <param name="layout">The current faction's bookmark layout.</param>
    /// <param name="texture">The displayed icon texture.</param>
    /// <returns>The icon height in source-space units.</returns>
    private static int GetIconHeight(StrategyBookmarkLayout layout, Texture2D texture)
    {
        if (layout.IconHeight > 0)
            return layout.IconHeight;

        return UILayout.GetTextureSourceHeight(texture);
    }

    /// <summary>
    /// Gets the authored or icon-derived horizontal label offset.
    /// </summary>
    /// <param name="layout">The current faction's bookmark layout.</param>
    /// <param name="iconWidth">The resolved icon width.</param>
    /// <returns>The label offset in source-space units.</returns>
    private static int GetLabelOffsetX(StrategyBookmarkLayout layout, int iconWidth)
    {
        if (layout.LabelOffsetX > 0)
            return layout.LabelOffsetX;

        return iconWidth;
    }

    /// <summary>
    /// Centers one size within another without producing a negative offset.
    /// </summary>
    /// <param name="outerSize">The containing size.</param>
    /// <param name="innerSize">The contained size.</param>
    /// <returns>The centered non-negative offset.</returns>
    private static int GetCenteredOffset(int outerSize, int innerSize)
    {
        return Mathf.Max(0, (outerSize - innerSize) / 2);
    }
}
