using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one authored selectable row in the messages index.
/// </summary>
public sealed class MessageWindowRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage selectionImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private Vector2Int normalIconOffset;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    private FontStyles headerTextTemplateFontStyle;
    private FontWeight headerTextTemplateFontWeight;
    private RectInt iconImageTemplateRect;
    private bool initialized;

    /// <summary>
    /// Gets the stable identifier represented by the current row presentation.
    /// </summary>
    public string MessageId { get; private set; } = string.Empty;

    /// <summary>
    /// Applies a complete row presentation snapshot.
    /// </summary>
    /// <param name="data">The projected row presentation.</param>
    /// <param name="displayIndex">The row's current visual index.</param>
    public void Render(MessageWindowRowRenderData data, int displayIndex)
    {
        EnsureInitialized();
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        MessageId = data.MessageId;
        ConfigureSelectableRow(displayIndex, hitAreaImage);
        UILayout.SetImageTexture(selectionImage, data.Selected ? data.SelectionTexture : null);
        SetIconImage(
            data.Selected ? data.SelectedIconTexture : data.NormalIconTexture,
            data.Selected
        );
        UILayout.SetTextContent(headerTextField, data.Header, data.HeaderColor);
        headerTextField.fontStyle = headerTextTemplateFontStyle;
        headerTextField.fontWeight = headerTextTemplateFontWeight;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies and captures authored row presentation state.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Captures the authored icon slot and typography exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        VerifyReferences();
        iconImageTemplateRect = UILayout.GetSourceRect(iconImage.rectTransform);
        headerTextTemplateFontStyle = headerTextField.fontStyle;
        headerTextTemplateFontWeight = headerTextField.fontWeight;
        initialized = true;
    }

    /// <summary>
    /// Applies the selected or normal icon layout from the authored slot and configured offset.
    /// </summary>
    /// <param name="texture">The displayed category icon.</param>
    /// <param name="selected">Whether the row is selected.</param>
    private void SetIconImage(Texture texture, bool selected)
    {
        Vector2Int offset = selected ? Vector2Int.zero : normalIconOffset;
        UILayout.SetImage(
            iconImage,
            texture,
            iconImageTemplateRect.x + offset.x,
            iconImageTemplateRect.y + offset.y,
            iconImageTemplateRect.width,
            iconImageTemplateRect.height
        );
    }

    /// <summary>
    /// Verifies every authored row reference before use.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (headerTextField == null)
            throw new MissingReferenceException($"{name}/HeaderTextField is missing.");
    }
}
