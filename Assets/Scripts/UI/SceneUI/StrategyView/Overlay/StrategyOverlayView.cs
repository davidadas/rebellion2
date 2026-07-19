using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders strategy drag and targeting feedback above the authored strategy surface.
/// </summary>
public sealed class StrategyOverlayView : MonoBehaviour, ITargetingCursor, ICancelHandler
{
    [SerializeField]
    private RawImage targetingInputImage;

    [SerializeField]
    private Image dragFrameTopImage;

    [SerializeField]
    private Image dragFrameBottomImage;

    [SerializeField]
    private Image dragFrameLeftImage;

    [SerializeField]
    private Image dragFrameRightImage;

    [SerializeField]
    private RawImage destinationCursorImage;

    [SerializeField]
    private int destinationCursorSize;

    [SerializeField]
    private int destinationCursorRadius;

    private Texture2D destinationCursorTexture;
    private int destinationCursorTextureSize;
    private int destinationCursorTextureRadius;
    private bool targetCursorVisible;
    private bool ownsUnityCursor;
    private bool previousUnityCursorVisible;

    /// <summary>
    /// Raised when the active targeting cursor receives a cancel command.
    /// </summary>
    public event System.Action TargetingCancelRequested;

    /// <summary>
    /// Applies one immutable drag-overlay presentation snapshot.
    /// </summary>
    /// <param name="data">The drag-overlay presentation, or null to hide drag feedback.</param>
    public void Render(StrategyOverlayRenderData data)
    {
        VerifyReferences();

        if (data == null)
        {
            HideDragFrame();
            HideSharedImageIfTargetCursorInactive();
            return;
        }

        RenderDragFrame(data);
        if (data.DragImageBounds.HasValue)
            RenderDragImage(data);
        else
            HideSharedImageIfTargetCursorInactive();
    }

    /// <summary>
    /// Shows the targeting cursor at one strategy source-space position.
    /// </summary>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    public void Show(int x, int y)
    {
        VerifyReferences();
        targetCursorVisible = true;
        DisableTargetingInput();
        HideUnityCursor();
        SelectForCancel();
        RenderDestinationCursor(x, y);
    }

    /// <summary>
    /// Moves a visible targeting cursor to one strategy source-space position.
    /// </summary>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    public void MoveTo(int x, int y)
    {
        if (targetCursorVisible)
            RenderDestinationCursor(x, y);
    }

    /// <summary>
    /// Hides the targeting cursor and restores the platform cursor state.
    /// </summary>
    public void Hide()
    {
        targetCursorVisible = false;
        DisableTargetingInput();
        RestoreUnityCursor();
        ClearCancelSelection();
        destinationCursorImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Emits a targeting cancellation request while the targeting cursor is active.
    /// </summary>
    /// <param name="eventData">The cancel event.</param>
    public void OnCancel(BaseEventData eventData)
    {
        if (targetCursorVisible)
            TargetingCancelRequested?.Invoke();
    }

    /// <summary>
    /// Verifies the authored hierarchy and initializes inactive overlay controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        DisableTargetingInput();
    }

    /// <summary>
    /// Releases cursor ownership and hides transient overlay presentation.
    /// </summary>
    private void OnDisable()
    {
        targetCursorVisible = false;
        DisableTargetingInput();
        RestoreUnityCursor();
        ClearCancelSelection();
        if (destinationCursorImage != null)
            destinationCursorImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Renders or hides the window-move preview frame.
    /// </summary>
    /// <param name="data">The complete overlay presentation.</param>
    private void RenderDragFrame(StrategyOverlayRenderData data)
    {
        if (!data.DragFrameBounds.HasValue)
        {
            HideDragFrame();
            return;
        }

        RectInt bounds = data.DragFrameBounds.Value;
        SetLine(dragFrameTopImage, bounds.x, bounds.y, bounds.width, 1);
        SetLine(dragFrameBottomImage, bounds.x, bounds.y + bounds.height - 1, bounds.width, 1);
        SetLine(dragFrameLeftImage, bounds.x, bounds.y, 1, bounds.height);
        SetLine(dragFrameRightImage, bounds.x + bounds.width - 1, bounds.y, 1, bounds.height);
    }

    /// <summary>
    /// Renders the generated targeting cursor at one source-space position.
    /// </summary>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    private void RenderDestinationCursor(int x, int y)
    {
        destinationCursorImage.texture = GetDestinationCursorTexture(
            destinationCursorSize,
            destinationCursorRadius
        );
        destinationCursorImage.raycastTarget = false;
        UILayout.SetSourceRect(
            destinationCursorImage.rectTransform,
            x - destinationCursorSize / 2,
            y - destinationCursorSize / 2,
            destinationCursorSize,
            destinationCursorSize
        );
        destinationCursorImage.gameObject.SetActive(true);
        destinationCursorImage.enabled = true;
    }

    /// <summary>
    /// Renders the active item-drag image within its projected source-space bounds.
    /// </summary>
    /// <param name="data">The complete overlay presentation.</param>
    private void RenderDragImage(StrategyOverlayRenderData data)
    {
        if (data.DragImageTexture == null)
        {
            destinationCursorImage.gameObject.SetActive(false);
            return;
        }

        destinationCursorImage.texture = data.DragImageTexture;
        destinationCursorImage.raycastTarget = false;
        RectInt bounds = data.DragImageBounds.Value;
        UILayout.SetSourceRect(
            destinationCursorImage.rectTransform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        destinationCursorImage.gameObject.SetActive(true);
        destinationCursorImage.enabled = true;
    }

    /// <summary>
    /// Hides every edge of the window-move preview frame.
    /// </summary>
    private void HideDragFrame()
    {
        dragFrameTopImage.gameObject.SetActive(false);
        dragFrameBottomImage.gameObject.SetActive(false);
        dragFrameLeftImage.gameObject.SetActive(false);
        dragFrameRightImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides the shared cursor image when it is not serving the targeting controller.
    /// </summary>
    private void HideSharedImageIfTargetCursorInactive()
    {
        if (!targetCursorVisible)
            destinationCursorImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Captures and hides the platform cursor while this view owns targeting feedback.
    /// </summary>
    private void HideUnityCursor()
    {
        if (ownsUnityCursor)
            return;

        previousUnityCursorVisible = Cursor.visible;
        Cursor.visible = false;
        ownsUnityCursor = true;
    }

    /// <summary>
    /// Disables the authored targeting input image.
    /// </summary>
    private void DisableTargetingInput()
    {
        if (targetingInputImage == null)
            return;

        targetingInputImage.canvasRenderer.cullTransparentMesh = false;
        targetingInputImage.enabled = false;
        targetingInputImage.raycastTarget = false;
    }

    /// <summary>
    /// Selects the overlay so the event system can route cancellation.
    /// </summary>
    private void SelectForCancel()
    {
        EventSystem.current?.SetSelectedGameObject(gameObject);
    }

    /// <summary>
    /// Clears event-system selection when this overlay currently owns it.
    /// </summary>
    private void ClearCancelSelection()
    {
        if (EventSystem.current?.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Restores the platform cursor state captured by this view.
    /// </summary>
    private void RestoreUnityCursor()
    {
        if (!ownsUnityCursor)
            return;

        Cursor.visible = previousUnityCursorVisible;
        ownsUnityCursor = false;
    }

    /// <summary>
    /// Gets or creates the point-filtered targeting cursor texture for one authored geometry.
    /// </summary>
    /// <param name="size">The square texture size.</param>
    /// <param name="radius">The circular cursor radius.</param>
    /// <returns>The cached or newly generated cursor texture.</returns>
    private Texture2D GetDestinationCursorTexture(int size, int radius)
    {
        if (
            destinationCursorTexture != null
            && destinationCursorTextureSize == size
            && destinationCursorTextureRadius == radius
        )
            return destinationCursorTexture;

        Color32[] pixels = new Color32[size * size];
        Color32 white = new Color32(255, 255, 255, 255);
        int center = size / 2;
        for (int i = 0; i < size; i++)
        {
            SetCursorPixel(pixels, size, i, center, white);
            SetCursorPixel(pixels, size, center, i, white);
        }

        int x = radius;
        int y = 0;
        int error = 0;
        while (x >= y)
        {
            SetCursorCirclePixels(pixels, size, center, x, y, white);
            y++;

            if (error <= 0)
                error += 2 * y + 1;

            if (error > 0)
            {
                x--;
                error -= 2 * x + 1;
            }
        }

        destinationCursorTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        destinationCursorTexture.filterMode = FilterMode.Point;
        destinationCursorTexture.wrapMode = TextureWrapMode.Clamp;
        destinationCursorTexture.SetPixels32(pixels);
        destinationCursorTexture.Apply(false);
        destinationCursorTextureSize = size;
        destinationCursorTextureRadius = radius;
        return destinationCursorTexture;
    }

    /// <summary>
    /// Writes the eight symmetric points for one cursor-circle step.
    /// </summary>
    /// <param name="pixels">The destination pixel buffer.</param>
    /// <param name="size">The square buffer width.</param>
    /// <param name="center">The circle center coordinate.</param>
    /// <param name="x">The horizontal circle offset.</param>
    /// <param name="y">The vertical circle offset.</param>
    /// <param name="color">The pixel color.</param>
    private static void SetCursorCirclePixels(
        Color32[] pixels,
        int size,
        int center,
        int x,
        int y,
        Color32 color
    )
    {
        SetCursorPixel(pixels, size, center + x, center + y, color);
        SetCursorPixel(pixels, size, center + y, center + x, color);
        SetCursorPixel(pixels, size, center - y, center + x, color);
        SetCursorPixel(pixels, size, center - x, center + y, color);
        SetCursorPixel(pixels, size, center - x, center - y, color);
        SetCursorPixel(pixels, size, center - y, center - x, color);
        SetCursorPixel(pixels, size, center + y, center - x, color);
        SetCursorPixel(pixels, size, center + x, center - y, color);
    }

    /// <summary>
    /// Writes one cursor pixel when the coordinate lies inside the texture.
    /// </summary>
    /// <param name="pixels">The destination pixel buffer.</param>
    /// <param name="size">The square buffer width.</param>
    /// <param name="x">The horizontal pixel coordinate.</param>
    /// <param name="y">The vertical pixel coordinate.</param>
    /// <param name="color">The pixel color.</param>
    private static void SetCursorPixel(Color32[] pixels, int size, int x, int y, Color32 color)
    {
        if (x < 0 || x >= size || y < 0 || y >= size)
            return;

        pixels[(size - 1 - y) * size + x] = color;
    }

    /// <summary>
    /// Applies one source-space rectangle to a move-preview edge.
    /// </summary>
    /// <param name="image">The authored edge image.</param>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetLine(Image image, int x, int y, int width, int height)
    {
        image.color = Color.white;
        image.raycastTarget = false;
        UILayout.SetSourceRect(image.rectTransform, x, y, width, height);
        image.gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies every authored overlay reference before use.
    /// </summary>
    private void VerifyReferences()
    {
        if (targetingInputImage == null)
            throw new MissingReferenceException($"{name}/TargetingInputImage is missing.");
        if (dragFrameTopImage == null)
            throw new MissingReferenceException($"{name}/DragFrameTopImage is missing.");
        if (dragFrameBottomImage == null)
            throw new MissingReferenceException($"{name}/DragFrameBottomImage is missing.");
        if (dragFrameLeftImage == null)
            throw new MissingReferenceException($"{name}/DragFrameLeftImage is missing.");
        if (dragFrameRightImage == null)
            throw new MissingReferenceException($"{name}/DragFrameRightImage is missing.");
        if (destinationCursorImage == null)
            throw new MissingReferenceException($"{name}/DestinationCursorImage is missing.");
    }
}

/// <summary>
/// Contains one immutable strategy drag-overlay presentation snapshot.
/// </summary>
public sealed class StrategyOverlayRenderData
{
    /// <summary>
    /// Creates one complete drag-overlay presentation snapshot.
    /// </summary>
    /// <param name="dragFrameBounds">The optional window-move preview bounds.</param>
    /// <param name="dragImageTexture">The optional item-drag texture.</param>
    /// <param name="dragImageBounds">The optional item-drag bounds.</param>
    public StrategyOverlayRenderData(
        RectInt? dragFrameBounds,
        Texture dragImageTexture,
        RectInt? dragImageBounds
    )
    {
        bool hasDragImageTexture = dragImageTexture != null;
        bool hasDragImageBounds = dragImageBounds.HasValue;
        if (hasDragImageTexture != hasDragImageBounds)
            throw new System.ArgumentException(
                "Drag image texture and bounds must either both be supplied or both be absent."
            );

        DragFrameBounds = dragFrameBounds;
        DragImageTexture = dragImageTexture;
        DragImageBounds = dragImageBounds;
    }

    public RectInt? DragFrameBounds { get; }

    public Texture DragImageTexture { get; }

    public RectInt? DragImageBounds { get; }
}
