using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inspects artwork at its authored aspect ratio without covering the current workspace.
/// </summary>
public sealed class ImagePreviewView
    : MonoBehaviour,
        ICancelable,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    [SerializeField]
    private RawImage image;

    [SerializeField]
    private TextMeshProUGUI caption;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private RectTransform artworkSlot;

    [SerializeField]
    private RectTransform previewPanel;

    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private TMP_Text layerInstructions;

    private Vector2 dragStart;
    private RectTransform dragCanvas;
    private Vector2 cutOrigin;
    private int draggedCut = -1;
    private Action<int, Vector2> moveCut;
    private bool bound;
    private readonly List<(RectTransform Card, RawImage Image, RawImage Background)> cutCards =
        new();

    /// <summary>
    /// Shows one cached texture in the focused artwork viewer.
    /// </summary>
    /// <param name="texture">The resolved artwork.</param>
    /// <param name="address">Its content address.</param>
    public void Show(Texture2D texture, string address)
    {
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));
        VerifyReferences();
        ClearCuts();
        Present(texture, address + "  /  " + texture.width + " x " + texture.height);
    }

    /// <summary>
    /// Begins an artwork placement gesture inside the active image.
    /// </summary>
    /// <param name="eventData">The drag event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (moveCut != null)
        {
            draggedCut = cutCards.FindIndex(card =>
                RectTransformUtility.RectangleContainsScreenPoint(
                    card.Background.rectTransform,
                    eventData.pressPosition,
                    eventData.pressEventCamera
                )
            );
            if (draggedCut < 0)
                return;
            var cut = cutCards[draggedCut];
            dragCanvas = cut.Background.rectTransform;
            cutOrigin = cut.Image.rectTransform.anchoredPosition;
        }
        if (
            dragCanvas != null
            && !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragCanvas,
                eventData.pressPosition,
                eventData.pressEventCamera,
                out dragStart
            )
        )
            dragCanvas = null;
    }

    /// <summary>
    /// Tracks the artwork or effect marker while the author moves it.
    /// </summary>
    /// <param name="eventData">The drag event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (dragCanvas == null)
            return;
        Vector2 offset = GetDragOffset(eventData);
        if (draggedCut >= 0)
            cutCards[draggedCut].Image.rectTransform.anchoredPosition =
                cutOrigin + offset * dragCanvas.rect.size;
    }

    /// <summary>
    /// Rebuilds the preview at the selected placement when the gesture ends.
    /// </summary>
    /// <param name="eventData">The drag event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragCanvas == null)
            return;
        Vector2 offset = GetDragOffset(eventData);
        dragCanvas = null;
        if (draggedCut >= 0)
        {
            int index = draggedCut;
            draggedCut = -1;
            moveCut?.Invoke(index, offset);
        }
    }

    /// <summary>
    /// Converts a pointer movement to fractions of its artwork frame.
    /// </summary>
    /// <param name="eventData">The drag event.</param>
    /// <returns>The displacement in normalized image coordinates.</returns>
    private Vector2 GetDragOffset(PointerEventData eventData)
    {
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragCanvas,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 current
            )
        )
            return Vector2.zero;
        return (current - dragStart) / dragCanvas.rect.size;
    }

    /// <summary>
    /// Displays all standard artwork outputs together and lets the author apply those exact files.
    /// </summary>
    /// <param name="previews">The owned generated textures, their labels, and optional borrowed backgrounds.</param>
    /// <param name="apply">The action that saves the displayed output images.</param>
    /// <param name="move">Moves one preview by a normalized displacement.</param>
    internal void ShowCuts(
        IReadOnlyList<(
            string Caption,
            Texture2D Texture,
            Texture2D Background,
            Rect BackgroundUv
        )> previews,
        Action apply,
        Action<int, Vector2> move = null
    )
    {
        VerifyReferences();
        ClearCuts();
        image.gameObject.SetActive(false);
        moveCut = move;
        foreach (var preview in previews)
        {
            RectTransform card = new GameObject(
                "CutPreview",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            card.SetParent(artworkSlot, false);
            TextMeshProUGUI label = Instantiate(caption, card);
            label.text = preview.Caption;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.up;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(0, -34);
            label.rectTransform.offsetMax = Vector2.zero;

            RawImage backing = CreateCutLayer(card, "CutBacking", null);
            backing.color = new Color(0.035f, 0.04f, 0.05f, 1f);
            if (preview.Background != null)
            {
                RawImage background = CreateCutLayer(
                    backing.rectTransform,
                    "CutBackground",
                    preview.Background
                );
                background.uvRect = preview.BackgroundUv;
                background.rectTransform.anchorMin = Vector2.zero;
                background.rectTransform.anchorMax = Vector2.one;
                background.rectTransform.offsetMin = background.rectTransform.offsetMax =
                    Vector2.zero;
            }
            backing.raycastTarget = moveCut != null;
            backing.gameObject.AddComponent<RectMask2D>();
            RawImage cut = CreateCutLayer(backing.rectTransform, "CutArtwork", preview.Texture);
            cutCards.Add((card, cut, backing));
        }
        caption.text = "Generated artwork cuts";
        applyButton.GetComponentInChildren<TMP_Text>().text = "Save cuts";
        SetAction(
            applyButton,
            true,
            () =>
            {
                TryCancel();
                apply();
            }
        );
        if (moveCut != null)
        {
            layerInstructions.text = "Drag any image to adjust its placement.";
            layerInstructions.gameObject.SetActive(true);
        }
        artworkSlot.offsetMin = new Vector2(artworkSlot.offsetMin.x, moveCut == null ? 78 : 108);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        LayoutGallery();
    }

    /// <summary>
    /// Creates a noninteractive artwork layer within a generated output card.
    /// </summary>
    /// <param name="parent">The output card.</param>
    /// <param name="name">The semantic layer name.</param>
    /// <param name="texture">The optional artwork texture.</param>
    /// <returns>The created image layer.</returns>
    private static RawImage CreateCutLayer(RectTransform parent, string name, Texture2D texture)
    {
        RawImage layer = new GameObject(
            name,
            typeof(RectTransform),
            typeof(RawImage)
        ).GetComponent<RawImage>();
        layer.rectTransform.SetParent(parent, false);
        layer.raycastTarget = false;
        layer.texture = texture;
        return layer;
    }

    /// <summary>
    /// Validates and binds the shared viewer controls before displaying an image.
    /// </summary>
    private void VerifyReferences()
    {
        if (
            image == null
            || caption == null
            || closeButton == null
            || image.GetComponent<AspectRatioFitter>() == null
        )
            throw new MissingReferenceException(name + " has incomplete image-preview references.");
        if (!bound)
        {
            closeButton.onClick.AddListener(() => TryCancel());
            bound = true;
        }
    }

    /// <summary>
    /// Updates the texture and caption without taking ownership of the texture.
    /// </summary>
    /// <param name="texture">The ready-to-display image.</param>
    /// <param name="title">The visible image caption.</param>
    private void Present(Texture2D texture, string title)
    {
        image.texture = texture;
        image.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
        caption.text = title;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Keeps the variation viewer compact as the workspace window changes size.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (cutCards.Count > 0)
            LayoutGallery();
    }

    /// <summary>
    /// Centers the generated image and controls in a panel sized for gameplay artwork.
    /// </summary>
    private void LayoutGallery()
    {
        Rect available = ((RectTransform)transform).rect;
        previewPanel.anchorMin = previewPanel.anchorMax = new Vector2(0.5f, 0.5f);
        previewPanel.anchoredPosition = Vector2.zero;
        previewPanel.sizeDelta = new Vector2(
            Mathf.Min(1120, available.width - 48),
            Mathf.Min(440, available.height - 48)
        );
        if (cutCards.Count > 0)
            LayoutCutCards();
    }

    /// <summary>
    /// Fits every output at its full aspect ratio within the available gallery rows.
    /// </summary>
    private void LayoutCutCards()
    {
        const int gap = 18;
        const int labelHeight = 40;
        int width = Mathf.Max(1, Mathf.FloorToInt(artworkSlot.rect.width));
        int height = Mathf.Max(1, Mathf.FloorToInt(artworkSlot.rect.height));
        int columns = Mathf.Clamp((width + gap) / (212 + gap), 1, cutCards.Count);
        int rows = (cutCards.Count + columns - 1) / columns;
        int cardWidth = Mathf.Max(1, (width - gap * (columns - 1)) / columns);
        int cardHeight = Mathf.Max(1, (height - gap * (rows - 1)) / rows);
        for (int index = 0; index < cutCards.Count; index++)
        {
            var card = cutCards[index];
            int x = (index % columns) * (cardWidth + gap);
            int y = (index / columns) * (cardHeight + gap);
            UILayout.SetSourceRect(card.Card, x, y, cardWidth, cardHeight);
            float aspect = (float)card.Image.texture.width / card.Image.texture.height;
            int availableHeight = Mathf.Max(1, cardHeight - labelHeight);
            int imageWidth = Mathf.Max(
                1,
                Mathf.Min(cardWidth, Mathf.FloorToInt(availableHeight * aspect))
            );
            int imageHeight = Mathf.Max(
                1,
                Mathf.Min(availableHeight, Mathf.FloorToInt(imageWidth / aspect))
            );
            UILayout.SetSourceRect(card.Image.rectTransform, 0, 0, imageWidth, imageHeight);
            UILayout.SetSourceRect(
                card.Background.rectTransform,
                0,
                labelHeight,
                imageWidth,
                imageHeight
            );
        }
    }

    /// <summary>
    /// Rebinds one optional action while the generation controls are visible.
    /// </summary>
    /// <param name="button">The reused optional button.</param>
    /// <param name="visible">Whether to show the button.</param>
    /// <param name="action">The command to invoke when selected.</param>
    private static void SetAction(
        Button button,
        bool visible,
        UnityEngine.Events.UnityAction action
    )
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(action);
        button.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Discards all temporary canvases and restores the ordinary artwork viewer layout.
    /// </summary>
    private void ClearCuts()
    {
        bool restorePanel = cutCards.Count > 0;
        foreach (var card in cutCards)
        {
            card.Card.gameObject.SetActive(false);
            ReleasePreviewObject(card.Image.texture);
            ReleasePreviewObject(card.Card.gameObject);
        }
        cutCards.Clear();
        if (image != null)
            image.gameObject.SetActive(true);
        if (restorePanel && previewPanel != null)
        {
            previewPanel.anchorMin = Vector2.zero;
            previewPanel.anchorMax = Vector2.one;
            previewPanel.offsetMin = new Vector2(160, 100);
            previewPanel.offsetMax = new Vector2(-160, -100);
        }
        SetAction(applyButton, false, null);
        if (layerInstructions != null)
            layerInstructions.gameObject.SetActive(false);
        moveCut = null;
        dragCanvas = null;
        draggedCut = -1;
        if (artworkSlot != null)
            artworkSlot.offsetMin = new Vector2(artworkSlot.offsetMin.x, 28);
    }

    /// <summary>
    /// Releases temporary preview resources in the active Unity mode.
    /// </summary>
    /// <param name="resource">The texture or gallery object owned by the viewer.</param>
    private static void ReleasePreviewObject(UnityEngine.Object resource)
    {
        if (Application.isPlaying)
            Destroy(resource);
        else
            DestroyImmediate(resource);
    }

    /// <summary>
    /// Dismisses the artwork viewer without changing its source asset reference.
    /// </summary>
    /// <returns>True when the viewer was dismissed.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        image.texture = null;
        ClearCuts();
        gameObject.SetActive(false);
        return true;
    }

    /// <summary>
    /// Releases any owned preview textures during workspace teardown.
    /// </summary>
    private void OnDestroy() => ClearCuts();
}
