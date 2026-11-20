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
        IPointerClickHandler,
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
    private Button nextSizeButton;

    [SerializeField]
    private Button rerollButton;

    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private TMP_Text layerInstructions;

    [SerializeField]
    private RectTransform layerMarker;

    [SerializeField]
    private Button nextLayerButton;

    [SerializeField]
    private Button addLayerButton;

    [SerializeField]
    private Button removeLayerButton;

    [SerializeField]
    private Button duplicateLayerButton;

    private Action<int> selectLayer;
    private Action<int, Vector2> moveLayer;
    private Action removeLayer;
    private Action duplicateLayer;
    private Action undoLayerEdit;
    private Action redoLayerEdit;
    private Action closeDraft;
    private Vector2 dragStart;
    private RectTransform dragCanvas;
    private Vector2 cutOrigin;
    private Rect layerOrigin;
    private int draggedLayer = -1;
    private int selectedLayer = -1;
    private int draggedCut = -1;
    private Action<int, Vector2> moveCut;

    private bool bound;
    private IReadOnlyList<ArtworkEffectPreviewFrame> generatedPreviews;
    private readonly List<RawImage> effectImages = new();
    private RawImage subjectOverlay;
    private readonly List<(RectTransform Card, RawImage Image, RawImage Background)> cutCards =
        new();
    private string generatedTitle;
    private int previewIndex;

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
        ClearGenerated();
        Present(texture, address + "  /  " + texture.width + " x " + texture.height);
    }

    /// <summary>
    /// Shows a live effect draft whose layers can be selected and moved over the gameplay frame.
    /// </summary>
    /// <param name="title">The unit and state being edited.</param>
    /// <param name="previews">The borrowed frame textures and their positioned effects.</param>
    /// <param name="initialFrame">The gameplay size chosen on the artwork page.</param>
    /// <param name="selected">The layer selected in the draft.</param>
    /// <param name="select">Records the selected layer.</param>
    /// <param name="move">Commits a single layer translation.</param>
    /// <param name="add">Adds another layer when the effect supports it.</param>
    /// <param name="duplicate">Copies the selected layer.</param>
    /// <param name="remove">Removes the selected layer.</param>
    /// <param name="reroll">Replaces the draft with a complete generation.</param>
    /// <param name="addLabel">The label for adding the effect type.</param>
    /// <param name="generateLabel">The label for replacing the complete draft.</param>
    /// <param name="save">Writes the current draft.</param>
    /// <param name="undo">Reverts the last draft operation.</param>
    /// <param name="redo">Repeats the last reverted draft operation.</param>
    /// <param name="closed">Releases the draft after the preview is dismissed.</param>
    internal void ShowGenerated(
        string title,
        IReadOnlyList<ArtworkEffectPreviewFrame> previews,
        int initialFrame,
        int selected,
        Action<int> select,
        Action<int, Vector2> move,
        Action add,
        Action duplicate,
        Action remove,
        Action reroll,
        string addLabel,
        string generateLabel,
        Action save,
        Action undo,
        Action redo,
        Action closed
    )
    {
        VerifyReferences();
        int previousIndex = generatedPreviews == null ? initialFrame : previewIndex;
        ClearGenerated();
        generatedPreviews = previews;
        generatedTitle = title;
        previewIndex = Mathf.Min(previousIndex, previews.Count - 1);
        selectedLayer = selected;
        selectLayer = select;
        moveLayer = move;
        removeLayer = remove;
        duplicateLayer = duplicate;
        undoLayerEdit = undo;
        redoLayerEdit = redo;
        closeDraft = closed;
        SetAction(nextSizeButton, previews.Count > 1, SelectNextFrame);
        rerollButton.GetComponentInChildren<TMP_Text>().text = generateLabel;
        addLayerButton.GetComponentInChildren<TMP_Text>().text = addLabel;
        SetAction(rerollButton, true, () => reroll());
        applyButton.GetComponentInChildren<TMP_Text>().text = "Save";
        SetAction(applyButton, true, () => save());
        SetAction(nextLayerButton, true, SelectNextLayer);
        SetAction(addLayerButton, add != null, () => add());
        SetAction(duplicateLayerButton, duplicate != null, DuplicateSelectedLayer);
        SetAction(removeLayerButton, true, RemoveSelectedLayer);
        artworkSlot.offsetMin = new Vector2(artworkSlot.offsetMin.x, 108);
        image.raycastTarget = true;
        LayoutGeneratedPanel();
        PresentGeneratedSize();
    }

    /// <summary>
    /// Shows the next size of the same draft.
    /// </summary>
    private void SelectNextFrame()
    {
        previewIndex = (previewIndex + 1) % generatedPreviews.Count;
        PresentGeneratedSize();
    }

    /// <summary>
    /// Cycles through layers when their artwork overlaps.
    /// </summary>
    private void SelectNextLayer()
    {
        var layers = generatedPreviews[previewIndex].Layers;
        if (layers.Count == 0)
            return;
        int position = 0;
        for (int index = 0; index < layers.Count; index++)
            if (layers[index].Index == selectedLayer)
                position = (index + 1) % layers.Count;
        SelectLayer(layers[position].Index);
    }

    /// <summary>
    /// Removes the selected effect from the draft.
    /// </summary>
    private void RemoveSelectedLayer()
    {
        if (selectedLayer >= 0)
            removeLayer?.Invoke();
    }

    /// <summary>
    /// Duplicates the selected layer while keeping its current artwork and placement.
    /// </summary>
    private void DuplicateSelectedLayer()
    {
        if (selectedLayer >= 0)
            duplicateLayer?.Invoke();
    }

    /// <summary>
    /// Routes editor shortcuts to the active artwork draft.
    /// </summary>
    /// <param name="key">The incoming native key event.</param>
    /// <returns>Whether this preview handled the key.</returns>
    internal bool HandleKeyboardShortcut(Event key)
    {
        if (generatedPreviews == null || key?.type != EventType.KeyDown || key.alt)
            return false;
        bool command = key.control || key.command;
        Action action = key.keyCode switch
        {
            KeyCode.Backspace or KeyCode.Delete => RemoveSelectedLayer,
            KeyCode.Tab => SelectNextLayer,
            KeyCode.D when command => DuplicateSelectedLayer,
            KeyCode.Z when command && key.shift => redoLayerEdit,
            KeyCode.Z when command => undoLayerEdit,
            KeyCode.Y when command => redoLayerEdit,
            KeyCode.S when command => () => applyButton.onClick.Invoke(),
            _ => null,
        };
        if (action == null)
            return false;
        action();
        key.Use();
        return true;
    }

    /// <summary>
    /// Selects the layer under the pointer by its visible pixels.
    /// </summary>
    /// <param name="eventData">The completed click.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            generatedPreviews != null
            && dragCanvas == null
            && !eventData.dragging
            && eventData.button == PointerEventData.InputButton.Left
        )
            SelectLayer(PickLayer(eventData.position, eventData.pressEventCamera));
    }

    /// <summary>
    /// Finds the foremost visible effect at a screen position.
    /// </summary>
    /// <param name="position">The pointer position.</param>
    /// <param name="camera">The preview camera, when applicable.</param>
    /// <returns>The draft layer index, or minus one outside the effects.</returns>
    private int PickLayer(Vector2 position, Camera camera)
    {
        RectTransform rect = image.rectTransform;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                position,
                camera,
                out Vector2 local
            ) || !rect.rect.Contains(local)
        )
            return -1;
        Vector2 point = (local - rect.rect.min) / rect.rect.size;
        var layers = generatedPreviews[previewIndex].Layers;
        for (int index = layers.Count - 1; index >= 0; index--)
        {
            var layer = layers[index];
            if (!layer.Bounds.Contains(point))
                continue;
            Vector2 uv = (point - layer.Bounds.min) / layer.Bounds.size;
            if (layer.Texture.GetPixelBilinear(uv.x, uv.y).a > 1f / 255f)
                return layer.Index;
        }
        return -1;
    }

    /// <summary>
    /// Highlights the selected layer and updates the available commands.
    /// </summary>
    /// <param name="index">The draft layer index, or minus one to clear selection.</param>
    private void SelectLayer(int index)
    {
        selectedLayer = index;
        selectLayer?.Invoke(index);
        var layers = generatedPreviews[previewIndex].Layers;
        foreach (var layer in layers)
            if (layer.Index == index)
            {
                PositionLayerOutline(layer.Bounds);
                layerMarker.gameObject.SetActive(true);
                layerInstructions.text = $"{layer.Name}. Drag to move.";
                removeLayerButton.interactable = true;
                if (duplicateLayerButton != null)
                    duplicateLayerButton.interactable = duplicateLayer != null;
                return;
            }
        layerMarker.gameObject.SetActive(false);
        layerInstructions.text = "Select a layer on the artwork.";
        removeLayerButton.interactable = false;
        if (duplicateLayerButton != null)
            duplicateLayerButton.interactable = false;
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
        else if (moveLayer != null)
        {
            int picked = PickLayer(eventData.pressPosition, eventData.pressEventCamera);
            if (picked < 0)
                return;
            SelectLayer(picked);
            var layers = generatedPreviews[previewIndex].Layers;
            for (int index = 0; index < layers.Count; index++)
                if (layers[index].Index == picked)
                {
                    draggedLayer = index;
                    layerOrigin = layers[index].Bounds;
                    break;
                }
            dragCanvas = image.rectTransform;
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
        else if (draggedLayer >= 0)
        {
            Rect placed = new(layerOrigin.position + ClampLayerOffset(offset), layerOrigin.size);
            SetLayerRect(effectImages[draggedLayer].rectTransform, placed);
            PositionLayerOutline(placed);
        }
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
        else if (draggedLayer >= 0)
        {
            int layer = generatedPreviews[previewIndex].Layers[draggedLayer].Index;
            draggedLayer = -1;
            moveLayer?.Invoke(layer, ClampLayerOffset(offset));
        }
    }

    /// <summary>
    /// Keeps a layer's center within the editable canvas during a drag.
    /// </summary>
    /// <param name="offset">The proposed normalized movement.</param>
    /// <returns>The movement applied to the layer.</returns>
    private Vector2 ClampLayerOffset(Vector2 offset)
    {
        Vector2 center = layerOrigin.center + offset;
        return new Vector2(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y)) - layerOrigin.center;
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
    /// Draws the selection outline around the actual effect texture.
    /// </summary>
    /// <param name="bounds">The visible artwork bounds in normalized image coordinates.</param>
    private void PositionLayerOutline(Rect bounds)
    {
        SetLayerRect(layerMarker, bounds);
        layerMarker.SetAsLastSibling();
    }

    /// <summary>
    /// Places a live preview graphic in normalized canvas coordinates.
    /// </summary>
    /// <param name="target">The preview rectangle.</param>
    /// <param name="bounds">The rectangle relative to the image canvas.</param>
    private static void SetLayerRect(RectTransform target, Rect bounds)
    {
        target.anchorMin = bounds.min;
        target.anchorMax = bounds.max;
        target.offsetMin = target.offsetMax = Vector2.zero;
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
        ClearGenerated();
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
        LayoutGeneratedPanel();
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
    /// Presents the active size and the default dimensions of its generated asset.
    /// </summary>
    private void PresentGeneratedSize()
    {
        ClearEffectImages();
        var preview = generatedPreviews[previewIndex];
        Present(
            preview.BaseImage,
            $"{generatedTitle}  /  {preview.Caption} ({preview.BaseImage.width} \u00D7 {preview.BaseImage.height})"
        );
        foreach (var layer in preview.Layers)
        {
            RawImage graphic = CreateCutLayer(image.rectTransform, layer.Name, layer.Texture);
            SetLayerRect(graphic.rectTransform, layer.Bounds);
            effectImages.Add(graphic);
        }
        if (preview.SubjectOverlay != null)
        {
            subjectOverlay = CreateCutLayer(
                image.rectTransform,
                "Transit subject",
                preview.SubjectOverlay
            );
            SetLayerRect(subjectOverlay.rectTransform, new Rect(0, 0, 1, 1));
        }
        layerInstructions.gameObject.SetActive(true);
        nextLayerButton.interactable = preview.Layers.Count > 1;
        bool selectedIsVisible = false;
        foreach (var layer in preview.Layers)
            selectedIsVisible |= layer.Index == selectedLayer;
        SelectLayer(
            selectedIsVisible ? selectedLayer
            : preview.Layers.Count > 0 ? preview.Layers[0].Index
            : -1
        );
    }

    /// <summary>
    /// Clears the borrowed effect graphics when changing sizes or dismissing the preview.
    /// </summary>
    private void ClearEffectImages()
    {
        foreach (RawImage layer in effectImages)
        {
            layer.gameObject.SetActive(false);
            ReleasePreviewObject(layer.gameObject);
        }
        effectImages.Clear();
        if (subjectOverlay != null)
        {
            subjectOverlay.gameObject.SetActive(false);
            ReleasePreviewObject(subjectOverlay.gameObject);
            subjectOverlay = null;
        }
    }

    /// <summary>
    /// Keeps the variation viewer compact as the workspace window changes size.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (generatedPreviews != null || cutCards.Count > 0)
            LayoutGeneratedPanel();
    }

    /// <summary>
    /// Centers the generated image and controls in a panel sized for gameplay artwork.
    /// </summary>
    private void LayoutGeneratedPanel()
    {
        Rect available = ((RectTransform)transform).rect;
        previewPanel.anchorMin = previewPanel.anchorMax = new Vector2(0.5f, 0.5f);
        previewPanel.anchoredPosition = Vector2.zero;
        previewPanel.sizeDelta = new Vector2(
            Mathf.Min(cutCards.Count > 0 ? 1120 : 840, available.width - 48),
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
    private void ClearGenerated()
    {
        bool restorePanel = generatedPreviews != null || cutCards.Count > 0;
        ClearEffectImages();
        generatedPreviews = null;
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
        SetAction(nextSizeButton, false, null);
        SetAction(rerollButton, false, null);
        SetAction(applyButton, false, null);
        SetAction(addLayerButton, false, null);
        SetAction(duplicateLayerButton, false, null);
        SetAction(nextLayerButton, false, null);
        SetAction(removeLayerButton, false, null);
        if (layerMarker != null)
            layerMarker.gameObject.SetActive(false);
        if (layerInstructions != null)
            layerInstructions.gameObject.SetActive(false);
        if (image != null)
            image.raycastTarget = false;
        moveLayer = null;
        selectLayer = null;
        removeLayer = null;
        duplicateLayer = null;
        undoLayerEdit = null;
        redoLayerEdit = null;
        closeDraft = null;
        draggedLayer = -1;
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
        Action finished = closeDraft;
        ClearGenerated();
        gameObject.SetActive(false);
        finished?.Invoke();
        return true;
    }

    /// <summary>
    /// Releases any owned preview textures during workspace teardown.
    /// </summary>
    private void OnDestroy()
    {
        Action finished = closeDraft;
        ClearGenerated();
        finished?.Invoke();
    }
}

/// <summary>
/// Describes the visible texture and position of one editable effect.
/// </summary>
internal readonly struct ArtworkEffectLayerPreview
{
    internal int Index { get; }
    internal string Name { get; }
    internal Texture2D Texture { get; }
    internal Rect Bounds { get; }

    /// <summary>
    /// Captures a layer's position and artwork in one preview.
    /// </summary>
    /// <param name="index">The source draft index.</param>
    /// <param name="name">The displayed layer name.</param>
    /// <param name="texture">The borrowed cropped effect.</param>
    /// <param name="bounds">Its current normalized canvas rectangle.</param>
    internal ArtworkEffectLayerPreview(int index, string name, Texture2D texture, Rect bounds)
    {
        Index = index;
        Name = name;
        Texture = texture;
        Bounds = bounds;
    }
}

/// <summary>
/// Describes a layered preview for one output size.
/// </summary>
internal readonly struct ArtworkEffectPreviewFrame
{
    internal string Caption { get; }
    internal Texture2D BaseImage { get; }
    internal Texture2D SubjectOverlay { get; }
    internal IReadOnlyList<ArtworkEffectLayerPreview> Layers { get; }

    /// <summary>
    /// Captures the layer stack for one output size.
    /// </summary>
    /// <param name="caption">The output's label.</param>
    /// <param name="baseImage">The fixed gameplay background.</param>
    /// <param name="subjectOverlay">The optional unit drawn above the transit effects.</param>
    /// <param name="layers">The editable effects in back-to-front order.</param>
    internal ArtworkEffectPreviewFrame(
        string caption,
        Texture2D baseImage,
        Texture2D subjectOverlay,
        IReadOnlyList<ArtworkEffectLayerPreview> layers
    )
    {
        Caption = caption;
        BaseImage = baseImage;
        SubjectOverlay = subjectOverlay;
        Layers = layers;
    }
}
