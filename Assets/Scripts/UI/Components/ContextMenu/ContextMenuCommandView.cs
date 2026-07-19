using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one authored context-menu command row and owns its pointer interaction.
/// </summary>
public sealed class ContextMenuCommandView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerClickHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RectTransform iconPanel;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI commandTextField;

    private bool initialized;
    private RectInt rootTemplateRect;
    private RectInt iconPanelTemplateRect;
    private RectInt iconTemplateRect;
    private RectInt textTemplateRect;
    private ContextMenuCommandItem item;

    /// <summary>
    /// Raised when the row invokes its bound command.
    /// </summary>
    internal event System.Action<IContextMenuCommand> Selected;

    /// <summary>
    /// Raised when the row changes its active presentation state.
    /// </summary>
    internal event System.Action<ContextMenuCommandItem> StateChanged;

    internal int RowHeight
    {
        get
        {
            InitializeView();
            return rootTemplateRect.height;
        }
    }

    internal int IconPanelWidth
    {
        get
        {
            InitializeView();
            return iconPanelTemplateRect.width;
        }
    }

    /// <summary>
    /// Renders one command using the current panel layout.
    /// </summary>
    /// <param name="commandItem">The command item to present.</param>
    /// <param name="color">The resolved command-state color.</param>
    /// <param name="hasIconColumn">Whether the panel reserves an icon column.</param>
    /// <param name="panelWidth">The current panel width in source units.</param>
    /// <param name="index">The zero-based row index.</param>
    internal void Render(
        ContextMenuCommandItem commandItem,
        Color32 color,
        bool hasIconColumn,
        int panelWidth,
        int index
    )
    {
        InitializeView();
        item = commandItem;

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

        Texture iconTexture = item?.GetIconTexture();
        RectInt iconRect = GetIconRect(iconTexture, item?.CenterNativeIcon == true);
        UILayout.SetSourceRect(
            iconImage.rectTransform,
            iconRect.x,
            iconRect.y,
            iconRect.width,
            iconRect.height
        );
        iconImage.texture = iconTexture;
        iconImage.enabled = iconTexture != null;
        iconImage.gameObject.SetActive(iconTexture != null);

        int textX = textTemplateRect.x + (hasIconColumn ? GetIconColumnWidth() : 0);
        int rightPadding = rootTemplateRect.width - textTemplateRect.x - textTemplateRect.width;
        int textWidth = Mathf.Max(0, panelWidth - textX - rightPadding);
        UILayout.SetSourceRect(
            commandTextField.rectTransform,
            textX,
            textTemplateRect.y,
            textWidth,
            textTemplateRect.height
        );
        commandTextField.text = item?.Text ?? string.Empty;
        commandTextField.color = color;

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Clears and hides the command row.
    /// </summary>
    internal void Hide()
    {
        item = null;
        if (iconImage != null)
            iconImage.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Calculates the minimum panel width required for command text and optional icon content.
    /// </summary>
    /// <param name="text">The command text.</param>
    /// <param name="hasIconColumn">Whether the panel reserves an icon column.</param>
    /// <returns>The required panel width in source units.</returns>
    internal int GetMinimumPanelWidth(string text, bool hasIconColumn)
    {
        InitializeView();

        int textX = textTemplateRect.x + (hasIconColumn ? GetIconColumnWidth() : 0);
        int rightPadding = rootTemplateRect.width - textTemplateRect.x - textTemplateRect.width;
        Vector2 preferredSize = commandTextField.GetPreferredValues(text ?? string.Empty);
        return Mathf.CeilToInt(textX + preferredSize.x + rightPadding);
    }

    /// <summary>
    /// Activates the row when the pointer enters it.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetActive(true);
    }

    /// <summary>
    /// Deactivates a row without an open submenu when the pointer exits it.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (item?.HasSubmenu != true)
            SetActive(false);
    }

    /// <summary>
    /// Activates the row when the primary pointer is pressed.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            SetActive(true);
    }

    /// <summary>
    /// Invokes an enabled leaf command when the primary pointer clicks it.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            eventData.button == PointerEventData.InputButton.Left
            && item?.Enabled == true
            && !item.HasSubmenu
        )
            Selected?.Invoke(item.Command);
    }

    /// <summary>
    /// Validates and captures the authored command template once.
    /// </summary>
    private void InitializeView()
    {
        if (initialized)
            return;

        VerifyReferences();
        rootTemplateRect = UILayout.GetSourceRect(transform as RectTransform);
        iconPanelTemplateRect = UILayout.GetSourceRect(iconPanel);
        iconTemplateRect = UILayout.GetSourceRect(iconImage.rectTransform);
        textTemplateRect = UILayout.GetSourceRect(commandTextField.rectTransform);
        initialized = true;
    }

    /// <summary>
    /// Verifies that every authored command-row reference is assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (iconPanel == null)
            throw new MissingReferenceException($"{name}/IconPanel is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (commandTextField == null)
            throw new MissingReferenceException($"{name}/CommandTextField is missing.");
    }

    /// <summary>
    /// Gets the horizontal space occupied by the authored icon image slot.
    /// </summary>
    /// <returns>The icon-column width in source units.</returns>
    private int GetIconColumnWidth()
    {
        return iconTemplateRect.x + iconTemplateRect.width;
    }

    /// <summary>
    /// Applies one active-state transition to the bound command item.
    /// </summary>
    /// <param name="active">The requested active state.</param>
    private void SetActive(bool active)
    {
        if (item == null || (active && !item.Enabled) || item.Active == active)
            return;

        item.Active = active;
        StateChanged?.Invoke(item);
    }

    /// <summary>
    /// Resolves the icon rectangle for authored or centered native-size presentation.
    /// </summary>
    /// <param name="texture">The icon texture.</param>
    /// <param name="centerNativeIcon">Whether to center the texture at its source size.</param>
    /// <returns>The resolved icon rectangle.</returns>
    private RectInt GetIconRect(Texture texture, bool centerNativeIcon)
    {
        if (!centerNativeIcon || texture == null)
            return iconTemplateRect;

        Vector2Int size = UILayout.GetTextureSourceSize(texture);
        if (size.x <= 0 || size.y <= 0)
            return iconTemplateRect;

        return new RectInt(
            iconTemplateRect.x,
            iconTemplateRect.y + Mathf.Max(0, (rootTemplateRect.height - size.y) / 2),
            size.x,
            size.y
        );
    }
}
