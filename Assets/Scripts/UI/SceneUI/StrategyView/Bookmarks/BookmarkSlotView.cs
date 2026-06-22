using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BookmarkSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    public event Action<BookmarkSlotView> DoubleClicked;

    public int Index { get; private set; }

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            DoubleClicked?.Invoke(this);
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (labelTextField == null)
            throw new MissingReferenceException($"{name}/LabelTextField is missing.");
    }

    private static RectInt GetSlot(StrategyBookmarkLayout layout, int index)
    {
        return new RectInt(
            layout.StartX,
            layout.StartY + index * layout.ItemHeight,
            layout.Width,
            layout.ItemHeight
        );
    }

    private static int GetIconWidth(StrategyBookmarkLayout layout, Texture2D texture)
    {
        if (layout.IconWidth > 0)
            return layout.IconWidth;

        return texture == null ? 0 : texture.width;
    }

    private static int GetIconHeight(StrategyBookmarkLayout layout, Texture2D texture)
    {
        if (layout.IconHeight > 0)
            return layout.IconHeight;

        return texture == null ? 0 : texture.height;
    }

    private static int GetLabelOffsetX(StrategyBookmarkLayout layout, int iconWidth)
    {
        if (layout.LabelOffsetX > 0)
            return layout.LabelOffsetX;

        return iconWidth;
    }

    private static int GetCenteredOffset(int outerSize, int innerSize)
    {
        return Mathf.Max(0, (outerSize - innerSize) / 2);
    }
}
