using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    public event System.Action TargetingCancelRequested;

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
        if (data.DragImageVisible)
            RenderDragImage(data);
        else
            HideSharedImageIfTargetCursorInactive();
    }

    public void Show(int x, int y)
    {
        VerifyReferences();
        targetCursorVisible = true;
        SetTargetingInputActive(false);
        HideUnityCursor();
        SelectForCancel();
        RenderDestinationCursor(x, y);
    }

    public void MoveTo(int x, int y)
    {
        if (targetCursorVisible)
            RenderDestinationCursor(x, y);
    }

    public void Hide()
    {
        targetCursorVisible = false;
        SetTargetingInputActive(false);
        RestoreUnityCursor();
        ClearCancelSelection();
        destinationCursorImage.gameObject.SetActive(false);
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (targetCursorVisible)
            TargetingCancelRequested?.Invoke();
    }

    private void Awake()
    {
        VerifyReferences();
        SetTargetingInputActive(false);
    }

    private void OnDisable()
    {
        targetCursorVisible = false;
        SetTargetingInputActive(false);
        RestoreUnityCursor();
        ClearCancelSelection();
        if (destinationCursorImage != null)
            destinationCursorImage.gameObject.SetActive(false);
    }

    private void RenderDragFrame(StrategyOverlayRenderData data)
    {
        if (!data.DragFrameVisible)
        {
            HideDragFrame();
            return;
        }

        SetLine(dragFrameTopImage, data.DragFrameX, data.DragFrameY, data.DragFrameWidth, 1);
        SetLine(
            dragFrameBottomImage,
            data.DragFrameX,
            data.DragFrameY + data.DragFrameHeight - 1,
            data.DragFrameWidth,
            1
        );
        SetLine(dragFrameLeftImage, data.DragFrameX, data.DragFrameY, 1, data.DragFrameHeight);
        SetLine(
            dragFrameRightImage,
            data.DragFrameX + data.DragFrameWidth - 1,
            data.DragFrameY,
            1,
            data.DragFrameHeight
        );
    }

    private void RenderDestinationCursor(int x, int y)
    {
        destinationCursorImage.texture = GetDestinationCursorTexture(
            destinationCursorSize,
            destinationCursorRadius
        );
        destinationCursorImage.raycastTarget = false;
        SetSourceRect(
            destinationCursorImage.rectTransform,
            x - destinationCursorSize / 2,
            y - destinationCursorSize / 2,
            destinationCursorSize,
            destinationCursorSize
        );
        destinationCursorImage.gameObject.SetActive(true);
        destinationCursorImage.enabled = true;
    }

    private void RenderDragImage(StrategyOverlayRenderData data)
    {
        if (data.DragImageTexture == null)
        {
            destinationCursorImage.gameObject.SetActive(false);
            return;
        }

        destinationCursorImage.texture = data.DragImageTexture;
        destinationCursorImage.raycastTarget = false;
        SetSourceRect(
            destinationCursorImage.rectTransform,
            data.DragImageX,
            data.DragImageY,
            data.DragImageWidth,
            data.DragImageHeight
        );
        destinationCursorImage.gameObject.SetActive(true);
        destinationCursorImage.enabled = true;
    }

    private void HideDragFrame()
    {
        dragFrameTopImage.gameObject.SetActive(false);
        dragFrameBottomImage.gameObject.SetActive(false);
        dragFrameLeftImage.gameObject.SetActive(false);
        dragFrameRightImage.gameObject.SetActive(false);
    }

    private void HideSharedImageIfTargetCursorInactive()
    {
        if (!targetCursorVisible)
            destinationCursorImage.gameObject.SetActive(false);
    }

    private void HideUnityCursor()
    {
        if (ownsUnityCursor)
            return;

        previousUnityCursorVisible = Cursor.visible;
        Cursor.visible = false;
        ownsUnityCursor = true;
    }

    private void SetTargetingInputActive(bool active)
    {
        if (targetingInputImage == null)
            return;

        targetingInputImage.canvasRenderer.cullTransparentMesh = false;
        targetingInputImage.enabled = false;
        targetingInputImage.raycastTarget = false;
    }

    private void SelectForCancel()
    {
        EventSystem.current?.SetSelectedGameObject(gameObject);
    }

    private void ClearCancelSelection()
    {
        if (EventSystem.current?.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void RestoreUnityCursor()
    {
        if (!ownsUnityCursor)
            return;

        Cursor.visible = previousUnityCursorVisible;
        ownsUnityCursor = false;
    }

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

    private static void SetCursorPixel(Color32[] pixels, int size, int x, int y, Color32 color)
    {
        if (x < 0 || x >= size || y < 0 || y >= size)
            return;

        pixels[(size - 1 - y) * size + x] = color;
    }

    private static void SetLine(Image image, int x, int y, int width, int height)
    {
        image.color = Color.white;
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
        image.gameObject.SetActive(true);
    }

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

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}

public sealed class StrategyOverlayRenderData
{
    public bool DragFrameVisible;
    public int DragFrameX;
    public int DragFrameY;
    public int DragFrameWidth;
    public int DragFrameHeight;
    public bool DragImageVisible;
    public Texture DragImageTexture;
    public int DragImageX;
    public int DragImageY;
    public int DragImageWidth;
    public int DragImageHeight;
}
