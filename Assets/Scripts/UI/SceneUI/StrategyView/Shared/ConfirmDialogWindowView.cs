using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an authored strategy confirmation dialog and emits semantic choices.
/// </summary>
public sealed class ConfirmDialogWindowView : MonoBehaviour
{
    [Header("Frame")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage titleImage;

    [Header("Choices")]
    [SerializeField]
    private RawImage confirmButtonImage;

    [SerializeField]
    private Button confirmButton;

    [SerializeField]
    private RawImagePressVisual confirmButtonPressVisual;

    [SerializeField]
    private Texture2D confirmButtonUpTexture;

    [SerializeField]
    private Texture2D confirmButtonDownTexture;

    [SerializeField]
    private RawImage cancelButtonImage;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private RawImagePressVisual cancelButtonPressVisual;

    [SerializeField]
    private Texture2D cancelButtonUpTexture;

    [SerializeField]
    private Texture2D cancelButtonDownTexture;

    [Header("Content")]
    [SerializeField]
    private ScrollAreaView linesScrollArea;

    [SerializeField]
    private TextMeshProUGUI lineTemplate;

    private readonly List<TextMeshProUGUI> lineTextFields = new List<TextMeshProUGUI>();
    private readonly List<string> renderedLines = new List<string>();
    private bool renderedAnyLines;

    /// <summary>
    /// Occurs when a choice request is raised.
    /// </summary>
    public event Action<ConfirmDialogWindowView, bool> ChoiceRequested;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    public event Action<ConfirmDialogWindowView> Destroyed;

    /// <summary>
    /// Verifies authored references and binds the two choice buttons.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
    }

    /// <summary>
    /// Removes local listeners and notifies the owning feature controller.
    /// </summary>
    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(Cancel);

        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete confirmation-dialog presentation snapshot.
    /// </summary>
    /// <param name="data">The dialog presentation snapshot.</param>
    public void Render(ConfirmDialogWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetImageTexture(backgroundImage, data.BackgroundTexture);
        UILayout.SetInteractiveImageTexture(titleImage, data.TitleTexture);
        confirmButtonPressVisual.SetTextures(confirmButtonUpTexture, confirmButtonDownTexture);
        cancelButtonPressVisual.SetTextures(cancelButtonUpTexture, cancelButtonDownTexture);
        RenderLines(data.Lines);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits the accepted choice for this dialog.
    /// </summary>
    internal void Confirm()
    {
        ChoiceRequested?.Invoke(this, true);
    }

    /// <summary>
    /// Emits the rejected choice for this dialog.
    /// </summary>
    internal void Cancel()
    {
        ChoiceRequested?.Invoke(this, false);
    }

    /// <summary>
    /// Reconciles projected text lines against reusable authored text instances.
    /// </summary>
    /// <param name="lines">The projected dialog lines.</param>
    private void RenderLines(IReadOnlyList<string> lines)
    {
        IReadOnlyList<string> safeLines = lines ?? Array.Empty<string>();
        bool resetScroll = LinesChanged(safeLines);
        RectInt template = UILayout.GetSourceRect(lineTemplate.rectTransform);
        float contentHeight = template.y + safeLines.Count * template.height;
        linesScrollArea.SetContentHeight(contentHeight, template.height, resetScroll);

        for (int i = 0; i < safeLines.Count; i++)
        {
            TextMeshProUGUI textField = GetLineTextField(i);
            UILayout.SetTemplateText(
                textField,
                lineTemplate,
                safeLines[i],
                Color.white,
                new RectInt(
                    template.x,
                    template.y + i * template.height,
                    template.width,
                    template.height
                )
            );
        }

        for (int i = safeLines.Count; i < lineTextFields.Count; i++)
            lineTextFields[i].gameObject.SetActive(false);

        renderedAnyLines = true;
        renderedLines.Clear();
        for (int i = 0; i < safeLines.Count; i++)
            renderedLines.Add(safeLines[i]);
    }

    /// <summary>
    /// Gets or creates a reusable text field for one projected line.
    /// </summary>
    /// <param name="index">The required line index.</param>
    /// <returns>The reusable line text field.</returns>
    private TextMeshProUGUI GetLineTextField(int index)
    {
        while (lineTextFields.Count <= index)
        {
            TextMeshProUGUI textField = Instantiate(lineTemplate, linesScrollArea.ContentRoot);
            textField.name = $"LineTextField{lineTextFields.Count}";
            lineTextFields.Add(textField);
        }

        return lineTextFields[index];
    }

    /// <summary>
    /// Determines whether projected lines differ from the last rendered snapshot.
    /// </summary>
    /// <param name="lines">The projected dialog lines.</param>
    /// <returns>True when the scroll position should reset.</returns>
    private bool LinesChanged(IReadOnlyList<string> lines)
    {
        if (!renderedAnyLines || renderedLines.Count != lines.Count)
            return true;

        for (int i = 0; i < lines.Count; i++)
        {
            if (!string.Equals(renderedLines[i], lines[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Verifies every authored visual, control, and template reference.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (confirmButtonImage == null || confirmButton == null || confirmButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/ConfirmButton is incomplete.");
        if (cancelButtonImage == null || cancelButton == null || cancelButtonPressVisual == null)
            throw new MissingReferenceException($"{name}/CancelButton is incomplete.");
        if (confirmButtonUpTexture == null || confirmButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/ConfirmButton textures are missing.");
        if (cancelButtonUpTexture == null || cancelButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CancelButton textures are missing.");
        if (linesScrollArea == null)
            throw new MissingReferenceException($"{name}/LinesScrollArea is missing.");
        if (lineTemplate == null)
            throw new MissingReferenceException($"{name}/LineTemplate is missing.");

        lineTemplate.gameObject.SetActive(false);
    }
}
