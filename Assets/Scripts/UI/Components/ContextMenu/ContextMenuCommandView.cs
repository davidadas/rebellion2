using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ContextMenuCommandView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerClickHandler
{
    private const int _sourceIconPanelWidth = 25;

    private bool capturedTemplateLayout;
    private RectInt rootTemplateRect;
    private RectInt iconTemplateRect;
    private RectInt textTemplateRect;
    private float textFontSize;
    private TextAlignmentOptions textAlignment;
    private TextWrappingModes textWrappingMode;
    private TextOverflowModes textOverflowMode;
    private bool textMaskable;
    private bool bound;
    private ContextMenuCommandItem item;

    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI commandTextField;

    internal event System.Action<IContextMenuCommand> Selected;
    internal event System.Action StateChanged;

    public int RowHeight
    {
        get
        {
            CaptureTemplateLayout();
            return rootTemplateRect.height;
        }
    }

    public int CommandHitTopOffset
    {
        get
        {
            CaptureTemplateLayout();
            return textTemplateRect.y;
        }
    }

    public int CommandHitHeight
    {
        get
        {
            CaptureTemplateLayout();
            return rootTemplateRect.height;
        }
    }

    public int IconColumnWidth
    {
        get
        {
            CaptureTemplateLayout();
            return iconTemplateRect.x + iconTemplateRect.width;
        }
    }

    public int IconPanelWidth => _sourceIconPanelWidth;

    public void Render(
        ContextMenuCommandRenderData data,
        bool hasIconColumn,
        int panelWidth,
        int index
    )
    {
        EnsureBound();
        item = data.Item;

        int y = index * rootTemplateRect.height;
        UILayout.SetSourceRect(
            transform as RectTransform,
            0,
            y,
            panelWidth,
            rootTemplateRect.height
        );
        UILayout.SetSourceRect(
            hitAreaImage.rectTransform,
            0,
            0,
            panelWidth,
            rootTemplateRect.height
        );
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;

        Texture iconTexture = data.Item?.GetIconTexture();
        UILayout.SetSourceRect(
            iconImage.rectTransform,
            iconTemplateRect.x,
            iconTemplateRect.y,
            iconTemplateRect.width,
            iconTemplateRect.height
        );
        iconImage.texture = iconTexture;
        iconImage.enabled = iconTexture != null;

        int textX = textTemplateRect.x + (hasIconColumn ? IconColumnWidth : 0);
        int rightPadding = rootTemplateRect.width - textTemplateRect.x - textTemplateRect.width;
        int textWidth = Mathf.Max(0, panelWidth - textX - rightPadding);
        UILayout.SetSourceRect(
            commandTextField.rectTransform,
            textX,
            textTemplateRect.y,
            textWidth,
            textTemplateRect.height
        );
        commandTextField.text = data.Text ?? string.Empty;
        commandTextField.color = data.Color;
        commandTextField.fontSize = textFontSize;
        commandTextField.alignment = textAlignment;
        commandTextField.textWrappingMode = textWrappingMode;
        commandTextField.overflowMode = textOverflowMode;
        commandTextField.maskable = textMaskable;
        commandTextField.raycastTarget = false;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        item = null;
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            SetActive(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && item?.Enabled == true)
            Selected?.Invoke(item.Command);
    }

    public int GetMinimumPanelWidth(string text, bool hasIconColumn)
    {
        EnsureBound();

        int textX = textTemplateRect.x + (hasIconColumn ? IconColumnWidth : 0);
        int rightPadding = rootTemplateRect.width - textTemplateRect.x - textTemplateRect.width;
        Vector2 preferredSize = commandTextField.GetPreferredValues(text ?? string.Empty);
        return Mathf.CeilToInt(textX + preferredSize.x + rightPadding);
    }

    public void CaptureTemplateLayout()
    {
        if (capturedTemplateLayout)
            return;

        VerifyReferences();
        rootTemplateRect = UILayout.GetSourceRect(transform as RectTransform);
        iconTemplateRect = UILayout.GetSourceRect(iconImage.rectTransform);
        textTemplateRect = UILayout.GetSourceRect(commandTextField.rectTransform);
        textFontSize = commandTextField.fontSize;
        textAlignment = commandTextField.alignment;
        textWrappingMode = commandTextField.textWrappingMode;
        textOverflowMode = commandTextField.overflowMode;
        textMaskable = commandTextField.maskable;
        capturedTemplateLayout = true;
    }

    private void Awake()
    {
        TryBind();
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (commandTextField == null)
            throw new MissingReferenceException($"{name}/CommandTextField is missing.");
    }

    private void EnsureBound()
    {
        VerifyReferences();
        Bind();
    }

    private bool TryBind()
    {
        if (hitAreaImage == null || iconImage == null || commandTextField == null)
            return false;

        Bind();
        return true;
    }

    private void Bind()
    {
        if (bound)
            return;

        CaptureTemplateLayout();
        bound = true;
    }

    private void SetActive(bool active)
    {
        if (item == null || item.Active == active)
            return;

        item.Active = active;
        StateChanged?.Invoke();
    }
}
