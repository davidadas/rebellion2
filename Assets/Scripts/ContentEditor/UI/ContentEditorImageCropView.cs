using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Frames an imported image at a required aspect ratio before producing a separate PNG.
/// </summary>
public sealed class ContentEditorImageCropView
    : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
{
    [SerializeField]
    private RawImage artwork;

    [SerializeField]
    private RawImage preview;

    [SerializeField]
    private Slider cropSize;

    [SerializeField]
    private TMP_Text cropSizeValue;

    [SerializeField]
    private RectTransform selection;

    [SerializeField]
    private RectTransform[] shades;

    [SerializeField]
    private TMP_Text description;

    [SerializeField]
    private TMP_Text dimensions;

    [SerializeField]
    private TMP_Text quality;

    [SerializeField]
    private TMP_Text error;

    [SerializeField]
    private Button reset;

    [SerializeField]
    private Button cancel;

    [SerializeField]
    private Button apply;

    private Texture2D source;
    private Vector2Int outputSize;
    private Vector2 center;
    private Vector2 dragStart;
    private Vector2 dragCenter;
    private Vector2 resizeAnchor;
    private Vector2 resizeDirection;
    private bool resizing;
    private Action<byte[]> submitted;
    private bool bound;
    private bool dragging;
    internal bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// Loads an image for framing without writing to its source or the current project.
    /// </summary>
    /// <param name="path">The chosen PNG or JPEG.</param>
    /// <param name="size">The required output dimensions.</param>
    /// <param name="onSubmitted">The callback receiving a confirmed PNG.</param>
    internal void Show(string path, Vector2Int size, Action<byte[]> onSubmitted)
    {
        if (size.x <= 0 || size.y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (onSubmitted == null)
            throw new ArgumentNullException(nameof(onSubmitted));
        BindControls();
        Cancel();
        Texture2D loaded = new(2, 2, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
        };
        try
        {
            if (!ImageConversion.LoadImage(loaded, File.ReadAllBytes(path), false))
                throw new InvalidDataException("Choose a PNG or JPEG image that can be opened.");
        }
        catch
        {
            ReleaseTexture(loaded);
            throw;
        }
        source = loaded;
        outputSize = size;
        submitted = onSubmitted;
        artwork.texture = preview.texture = source;
        artwork.uvRect = new Rect(0, 0, 1, 1);
        artwork.GetComponent<AspectRatioFitter>().aspectRatio = (float)source.width / source.height;
        preview.GetComponent<AspectRatioFitter>().aspectRatio = (float)size.x / size.y;
        description.text = Path.GetFileName(path) + " — the original image stays unchanged.";
        dimensions.text = $"{size.x} × {size.y} preview";
        error.text = string.Empty;
        ResetCrop();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Binds the authored controls once, including the matching teardown ownership.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            artwork == null
            || preview == null
            || cropSize == null
            || cropSizeValue == null
            || selection == null
            || shades == null
            || shades.Length != 4
            || description == null
            || dimensions == null
            || quality == null
            || error == null
            || reset == null
            || cancel == null
            || apply == null
        )
            throw new MissingReferenceException(name + " has incomplete crop controls.");
        cropSize.onValueChanged.AddListener(ChangeSize);
        reset.onClick.AddListener(ResetCrop);
        cancel.onClick.AddListener(Cancel);
        apply.onClick.AddListener(Apply);
        bound = true;
    }

    /// <summary>
    /// Starts moving the selection or resizing it from one of its corners.
    /// </summary>
    /// <param name="eventData">The pointer gesture.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = false;
        if (
            source == null
            || eventData.button != PointerEventData.InputButton.Left
            || !TryGetImagePoint(eventData, eventData.pressPosition, out dragStart)
        )
            return;
        Rect crop = preview.uvRect;
        resizeDirection = new Vector2(
            dragStart.x < crop.center.x ? -1 : 1,
            dragStart.y < crop.center.y ? -1 : 1
        );
        Vector2 corner = crop.center + crop.size * resizeDirection * 0.5f;
        Vector2 distance = (dragStart - corner) * artwork.rectTransform.rect.size;
        resizing = Mathf.Abs(distance.x) <= 12 && Mathf.Abs(distance.y) <= 12;
        dragging = resizing || crop.Contains(dragStart);
        resizeAnchor = crop.center - crop.size * resizeDirection * 0.5f;
        dragCenter = center;
    }

    /// <summary>
    /// Moves or resizes the crop box over the stationary, fully visible source image.
    /// </summary>
    /// <param name="eventData">The current pointer position.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (
            !dragging
            || source == null
            || !TryGetImagePoint(eventData, eventData.position, out Vector2 point)
        )
            return;
        if (resizing)
        {
            Vector2 largest = ContentEditorArtworkCrop
                .GetCenteredUvRect(new Vector2Int(source.width, source.height), outputSize)
                .size;
            Vector2 extent = (point - resizeAnchor) * resizeDirection / largest;
            Vector2 available =
                new Vector2(
                    resizeDirection.x > 0 ? 1 - resizeAnchor.x : resizeAnchor.x,
                    resizeDirection.y > 0 ? 1 - resizeAnchor.y : resizeAnchor.y
                ) / largest;
            float fraction = Mathf.Clamp(
                Mathf.Max(extent.x, extent.y),
                cropSize.minValue,
                Mathf.Min(1, available.x, available.y)
            );
            cropSize.SetValueWithoutNotify(fraction);
            center = resizeAnchor + largest * resizeDirection * fraction * 0.5f;
        }
        else
            center = dragCenter + point - dragStart;
        RenderCrop();
    }

    /// <summary>
    /// Finishes the current selection gesture without applying it to the project.
    /// </summary>
    /// <param name="eventData">The completed pointer gesture.</param>
    public void OnEndDrag(PointerEventData eventData) => dragging = false;

    /// <summary>
    /// Converts a pointer position to normalized coordinates on the complete source image.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="screenPoint">The pointer position to project onto the image.</param>
    /// <param name="point">Receives the normalized image position.</param>
    /// <returns>Whether the image plane could be resolved.</returns>
    private bool TryGetImagePoint(
        PointerEventData eventData,
        Vector2 screenPoint,
        out Vector2 point
    )
    {
        point = Vector2.zero;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                artwork.rectTransform,
                screenPoint,
                eventData.pressEventCamera,
                out Vector2 local
            )
        )
            return false;
        Rect bounds = artwork.rectTransform.rect;
        point = (local - bounds.min) / bounds.size;
        return true;
    }

    /// <summary>
    /// Adjusts selection size with the pointer wheel while leaving the source image fixed.
    /// </summary>
    /// <param name="eventData">The wheel gesture.</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (
            source != null
            && RectTransformUtility.RectangleContainsScreenPoint(
                artwork.rectTransform,
                eventData.position,
                eventData.enterEventCamera
            )
        )
            cropSize.value -= eventData.scrollDelta.y * 0.05f;
    }

    /// <summary>
    /// Returns to the largest centered crop with the required aspect ratio.
    /// </summary>
    private void ResetCrop()
    {
        center = new Vector2(0.5f, 0.5f);
        cropSize.SetValueWithoutNotify(1);
        RenderCrop();
    }

    /// <summary>
    /// Refreshes the selection after pointer or keyboard size changes.
    /// </summary>
    /// <param name="value">The fraction of the largest fitting selection.</param>
    private void ChangeSize(float value) => RenderCrop();

    /// <summary>
    /// Positions the crop box and outside shading, and previews the selected source region.
    /// </summary>
    private void RenderCrop()
    {
        if (source == null)
            return;
        Rect crop = ContentEditorArtworkCrop.GetUvRect(
            new Vector2Int(source.width, source.height),
            outputSize,
            center,
            1 / cropSize.value
        );
        center = crop.center;
        preview.uvRect = crop;
        SetRegion(selection, crop);
        SetRegion(shades[0], Rect.MinMaxRect(0, 0, crop.xMin, 1));
        SetRegion(shades[1], Rect.MinMaxRect(crop.xMax, 0, 1, 1));
        SetRegion(shades[2], Rect.MinMaxRect(crop.xMin, 0, crop.xMax, crop.yMin));
        SetRegion(shades[3], Rect.MinMaxRect(crop.xMin, crop.yMax, crop.xMax, 1));
        cropSizeValue.text = cropSize.value.ToString("P0");
        quality.text =
            source.width * crop.width < outputSize.x || source.height * crop.height < outputSize.y
                ? "This crop is smaller than the output and may look soft."
                : $"Source: {source.width} × {source.height}";
    }

    /// <summary>
    /// Anchors one overlay region to normalized coordinates on the displayed source.
    /// </summary>
    /// <param name="rect">The overlay rectangle.</param>
    /// <param name="region">The normalized source bounds.</param>
    private static void SetRegion(RectTransform rect, Rect region)
    {
        rect.anchorMin = region.min;
        rect.anchorMax = region.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Produces the selected PNG and closes only after its owner accepts it.
    /// </summary>
    private void Apply()
    {
        if (source == null)
            return;
        try
        {
            submitted(
                ContentEditorArtworkCutGenerator.GeneratePngCrop(source, preview.uvRect, outputSize)
            );
            Cancel();
        }
        catch (Exception exception)
        {
            error.text = exception.Message;
        }
    }

    /// <summary>
    /// Discards the draft crop and releases its decoded source without submitting it.
    /// </summary>
    internal void Cancel()
    {
        ReleaseDraft();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Drops the decoded source and its submission callback when a draft ends.
    /// </summary>
    private void ReleaseDraft()
    {
        submitted = null;
        dragging = false;
        if (artwork != null)
            artwork.texture = null;
        if (preview != null)
            preview.texture = null;
        ReleaseTexture(source);
        source = null;
    }

    /// <summary>
    /// Releases a decoded image in both authoring previews and standalone players.
    /// </summary>
    /// <param name="texture">The owned image.</param>
    private static void ReleaseTexture(Texture2D texture)
    {
        if (texture == null)
            return;
        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);
    }

    /// <summary>
    /// Drops draft pixels if the publishing workflow is hidden by its owner.
    /// </summary>
    private void OnDisable() => ReleaseDraft();

    /// <summary>
    /// Removes control listeners and releases any outstanding draft image.
    /// </summary>
    private void OnDestroy()
    {
        if (bound)
        {
            cropSize.onValueChanged.RemoveListener(ChangeSize);
            reset.onClick.RemoveListener(ResetCrop);
            cancel.onClick.RemoveListener(Cancel);
            apply.onClick.RemoveListener(Apply);
        }
        ReleaseDraft();
    }
}
