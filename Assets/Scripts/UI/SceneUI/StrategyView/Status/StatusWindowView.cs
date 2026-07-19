using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an authored status window and emits semantic window commands.
/// </summary>
public sealed class StatusWindowView : MonoBehaviour
{
    private readonly List<TextMeshProUGUI> labelTextFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> leftRowTextFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> rightRowTextFields = new List<TextMeshProUGUI>();
    private readonly List<StatusWindowRowRenderData> renderedRows =
        new List<StatusWindowRowRenderData>();
    private readonly List<RawImage> statusImageViews = new List<RawImage>();

    [Header("Frame")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    [Header("Images")]
    [SerializeField]
    private RectTransform imagesRoot;

    [SerializeField]
    private RawImage statusImageTemplate;

    [SerializeField]
    private RectTransform statusImageAreaTemplate;

    [SerializeField]
    private TextMeshProUGUI labelTextTemplate;

    [Header("Rows")]
    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private TextMeshProUGUI leftRowTextTemplate;

    [SerializeField]
    private TextMeshProUGUI rightRowTextTemplate;

    [Header("Commands")]
    [SerializeField]
    private RawImage infoButtonImage;

    [SerializeField]
    private RawImage closeButtonImage;

    [SerializeField]
    private RawImagePressVisual infoButtonPressVisual;

    [SerializeField]
    private RawImagePressVisual closeButtonPressVisual;

    [SerializeField]
    private Button infoButton;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Texture2D infoButtonUpTexture;

    [SerializeField]
    private Texture2D infoButtonDownTexture;

    [SerializeField]
    private Texture2D infoButtonDisabledTexture;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D closeButtonDownTexture;

    private bool renderedAnyRows;

    /// <summary>
    /// Occurs when a close request is raised.
    /// </summary>
    public event Action<StatusWindowView> CloseRequested;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    public event Action<StatusWindowView> Destroyed;

    /// <summary>
    /// Occurs when an info request is raised.
    /// </summary>
    public event Action<StatusWindowView> InfoRequested;

    /// <summary>
    /// Verifies the authored hierarchy and binds command listeners.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        infoButton.onClick.AddListener(RequestInfo);
        closeButton.onClick.AddListener(RequestClose);
    }

    /// <summary>
    /// Removes local listeners and notifies the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        if (infoButton != null)
            infoButton.onClick.RemoveListener(RequestInfo);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(RequestClose);
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete status-window presentation to the authored hierarchy.
    /// </summary>
    /// <param name="data">The immutable presentation snapshot.</param>
    public void Render(StatusWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetImageTexture(backgroundImage, data.BackgroundTexture);
        RenderHeader(data.Header);
        RenderImages(data.ImageTextures, data.CenterImage);
        RenderLabel(data.Label);
        RenderRows(data.Rows);
        UILayout.SetInteractiveImageTexture(
            infoButtonImage,
            data.InfoDisabled ? infoButtonDisabledTexture : infoButtonUpTexture
        );
        infoButton.interactable = !data.InfoDisabled;
        infoButtonPressVisual.SetTextures(
            data.InfoDisabled ? infoButtonDisabledTexture : infoButtonUpTexture,
            data.InfoDisabled ? null : infoButtonDownTexture
        );
        closeButtonPressVisual.SetInteractiveTextures(closeButtonUpTexture, closeButtonDownTexture);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits a semantic request to open Encyclopedia information for this status target.
    /// </summary>
    internal void RequestInfo()
    {
        InfoRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a semantic request to close this status window.
    /// </summary>
    internal void RequestClose()
    {
        CloseRequested?.Invoke(this);
    }

    /// <summary>
    /// Applies the projected status title.
    /// </summary>
    /// <param name="header">The displayed title.</param>
    private void RenderHeader(string header)
    {
        bool visible = !string.IsNullOrEmpty(header);
        headerTextField.gameObject.SetActive(visible);
        if (visible)
            UILayout.SetTextContent(headerTextField, header);
    }

    /// <summary>
    /// Fits projected images into the authored status-image slot.
    /// </summary>
    /// <param name="textures">The images to render in stacking order.</param>
    /// <param name="centerImage">Whether each image is centered within the slot.</param>
    private void RenderImages(IReadOnlyList<Texture2D> textures, bool centerImage)
    {
        IReadOnlyList<Texture2D> safeTextures = textures ?? Array.Empty<Texture2D>();
        RectInt imageArea = UILayout.GetSourceRect(statusImageAreaTemplate);
        int visibleIndex = 0;
        foreach (Texture2D texture in safeTextures)
        {
            if (texture == null)
                continue;

            RawImage image = GetStatusImage(visibleIndex++);
            Vector2Int size = UILayout.GetFittedImageSize(texture, imageArea);
            int x = imageArea.x + (centerImage ? (imageArea.width - size.x) / 2 : 0);
            int y = imageArea.y + (centerImage ? (imageArea.height - size.y) / 2 : 0);
            UILayout.SetImage(image, texture, x, y, size.x, size.y);
        }

        HideImagesFrom(visibleIndex);
    }

    /// <summary>
    /// Applies one label using the authored field and TextMesh Pro font metrics.
    /// </summary>
    /// <param name="label">The displayed label.</param>
    private void RenderLabel(string label)
    {
        RectInt template = UILayout.GetSourceRect(labelTextTemplate.rectTransform);
        if (string.IsNullOrEmpty(label))
        {
            HideTextFieldsFrom(labelTextFields, 0);
            return;
        }

        TextMeshProUGUI textField = GetLabelTextField(0);
        int height = GetPreferredTextHeight(labelTextTemplate, label, template);
        UILayout.SetTemplateText(
            textField,
            labelTextTemplate,
            label,
            Color.white,
            new RectInt(template.x, template.y, template.width, height)
        );
        HideTextFieldsFrom(labelTextFields, 1);
    }

    /// <summary>
    /// Applies projected detail rows and reconciles the local scroll position.
    /// </summary>
    /// <param name="rows">The projected visual rows.</param>
    private void RenderRows(IReadOnlyList<StatusWindowRowRenderData> rows)
    {
        IReadOnlyList<StatusWindowRowRenderData> safeRows =
            rows ?? Array.Empty<StatusWindowRowRenderData>();
        RectInt leftTemplate = UILayout.GetSourceRect(leftRowTextTemplate.rectTransform);
        RectInt rightTemplate = UILayout.GetSourceRect(rightRowTextTemplate.rectTransform);
        int contentHeight = 0;
        for (int i = 0; i < safeRows.Count; i++)
        {
            StatusWindowRowRenderData row = safeRows[i];
            int leftHeight = GetPreferredTextHeight(leftRowTextTemplate, row.Left, leftTemplate);
            int rightHeight = GetPreferredTextHeight(
                rightRowTextTemplate,
                row.Right,
                rightTemplate
            );
            int rowHeight = Mathf.Max(leftHeight, rightHeight);
            UILayout.SetTemplateText(
                GetLeftRowTextField(i),
                leftRowTextTemplate,
                row.Left,
                Color.white,
                new RectInt(
                    leftTemplate.x,
                    leftTemplate.y + contentHeight + rowHeight - leftHeight,
                    leftTemplate.width,
                    leftHeight
                )
            );
            UILayout.SetTemplateText(
                GetRightRowTextField(i),
                rightRowTextTemplate,
                row.Right,
                Color.white,
                new RectInt(
                    rightTemplate.x,
                    rightTemplate.y + contentHeight + rowHeight - rightHeight,
                    rightTemplate.width,
                    rightHeight
                )
            );
            contentHeight += rowHeight;
        }

        rowsScrollArea.SetContentHeight(
            contentHeight,
            Mathf.Max(leftTemplate.height, rightTemplate.height),
            RowsChanged(safeRows)
        );
        HideTextFieldsFrom(leftRowTextFields, safeRows.Count);
        HideTextFieldsFrom(rightRowTextFields, safeRows.Count);
        StoreRenderedRows(safeRows);
    }

    /// <summary>
    /// Gets or creates a reusable status-image instance.
    /// </summary>
    /// <param name="index">The required image index.</param>
    /// <returns>The reusable image.</returns>
    private RawImage GetStatusImage(int index)
    {
        while (statusImageViews.Count <= index)
        {
            RawImage image = Instantiate(statusImageTemplate, imagesRoot);
            image.name = $"StatusImage{statusImageViews.Count}";
            statusImageViews.Add(image);
        }

        return statusImageViews[index];
    }

    /// <summary>
    /// Gets or creates a reusable label field.
    /// </summary>
    /// <param name="index">The required label-line index.</param>
    /// <returns>The reusable label field.</returns>
    private TextMeshProUGUI GetLabelTextField(int index)
    {
        while (labelTextFields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(labelTextTemplate, transform);
            textField.name = $"LabelTextField{labelTextFields.Count}";
            labelTextFields.Add(textField);
        }

        return labelTextFields[index];
    }

    /// <summary>
    /// Gets or creates a reusable left-column row field.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable left-column field.</returns>
    private TextMeshProUGUI GetLeftRowTextField(int index)
    {
        return GetRowTextField(leftRowTextFields, leftRowTextTemplate, "LeftRowTextField", index);
    }

    /// <summary>
    /// Gets or creates a reusable right-column row field.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable right-column field.</returns>
    private TextMeshProUGUI GetRightRowTextField(int index)
    {
        return GetRowTextField(
            rightRowTextFields,
            rightRowTextTemplate,
            "RightRowTextField",
            index
        );
    }

    /// <summary>
    /// Gets or creates a reusable field in one row-column cache.
    /// </summary>
    /// <param name="fields">The row-column cache.</param>
    /// <param name="template">The authored row-column template.</param>
    /// <param name="namePrefix">The instance-name prefix.</param>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable row field.</returns>
    private TextMeshProUGUI GetRowTextField(
        List<TextMeshProUGUI> fields,
        TextMeshProUGUI template,
        string namePrefix,
        int index
    )
    {
        while (fields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(template, rowsScrollArea.ContentRoot);
            textField.name = $"{namePrefix}{fields.Count}";
            fields.Add(textField);
        }

        return fields[index];
    }

    /// <summary>
    /// Determines whether a new row snapshot requires scroll reconciliation.
    /// </summary>
    /// <param name="rows">The next visual row snapshot.</param>
    /// <returns>True when row content or count changed.</returns>
    private bool RowsChanged(IReadOnlyList<StatusWindowRowRenderData> rows)
    {
        if (!renderedAnyRows || renderedRows.Count != rows.Count)
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedRows[i].Left != rows[i].Left || renderedRows[i].Right != rows[i].Right)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Stores the most recently rendered rows for scroll reconciliation.
    /// </summary>
    /// <param name="rows">The rendered visual rows.</param>
    private void StoreRenderedRows(IReadOnlyList<StatusWindowRowRenderData> rows)
    {
        renderedAnyRows = true;
        renderedRows.Clear();
        for (int i = 0; i < rows.Count; i++)
            renderedRows.Add(rows[i]);
    }

    /// <summary>
    /// Hides cached images beginning at the supplied index.
    /// </summary>
    /// <param name="firstHiddenIndex">The first image to hide.</param>
    private void HideImagesFrom(int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < statusImageViews.Count; i++)
            statusImageViews[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides cached text fields beginning at the supplied index.
    /// </summary>
    /// <param name="fields">The field cache to update.</param>
    /// <param name="firstHiddenIndex">The first field to hide.</param>
    private static void HideTextFieldsFrom(
        IReadOnlyList<TextMeshProUGUI> fields,
        int firstHiddenIndex
    )
    {
        for (int i = firstHiddenIndex; i < fields.Count; i++)
            fields[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Measures wrapped text using the authored TextMesh Pro field and width.
    /// </summary>
    /// <param name="template">The authored text field.</param>
    /// <param name="text">The displayed text.</param>
    /// <param name="rect">The authored field bounds.</param>
    /// <returns>The source-space height required by the wrapped text.</returns>
    private static int GetPreferredTextHeight(TextMeshProUGUI template, string text, RectInt rect)
    {
        float preferredHeight = template.GetPreferredValues(text ?? string.Empty, rect.width, 0f).y;
        return Mathf.Max(rect.height, Mathf.CeilToInt(preferredHeight));
    }

    /// <summary>
    /// Verifies every authored reference required by the status presentation.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (headerTextField == null)
            throw new MissingReferenceException($"{name}/HeaderTextField is missing.");
        if (imagesRoot == null)
            throw new MissingReferenceException($"{name}/Images is missing.");
        if (statusImageTemplate == null)
            throw new MissingReferenceException($"{name}/StatusImageTemplate is missing.");
        if (statusImageAreaTemplate == null)
            throw new MissingReferenceException($"{name}/StatusImageAreaTemplate is missing.");
        if (labelTextTemplate == null)
            throw new MissingReferenceException($"{name}/LabelTextTemplate is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (leftRowTextTemplate == null)
            throw new MissingReferenceException($"{name}/LeftRowTextTemplate is missing.");
        if (rightRowTextTemplate == null)
            throw new MissingReferenceException($"{name}/RightRowTextTemplate is missing.");
        if (infoButtonImage == null)
            throw new MissingReferenceException($"{name}/InfoButtonImage is missing.");
        if (infoButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/InfoButtonPressVisual is missing.");
        if (closeButtonImage == null)
            throw new MissingReferenceException($"{name}/CloseButtonImage is missing.");
        if (closeButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/CloseButtonPressVisual is missing.");
        if (infoButton == null)
            throw new MissingReferenceException($"{name}/InfoButton is missing.");
        if (closeButton == null)
            throw new MissingReferenceException($"{name}/CloseButton is missing.");
        if (infoButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonUpTexture is missing.");
        if (infoButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonDownTexture is missing.");
        if (infoButtonDisabledTexture == null)
            throw new MissingReferenceException($"{name}/InfoButtonDisabledTexture is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (closeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonDownTexture is missing.");

        statusImageTemplate.gameObject.SetActive(false);
        statusImageAreaTemplate.gameObject.SetActive(false);
        labelTextTemplate.gameObject.SetActive(false);
        leftRowTextTemplate.gameObject.SetActive(false);
        rightRowTextTemplate.gameObject.SetActive(false);
    }
}
