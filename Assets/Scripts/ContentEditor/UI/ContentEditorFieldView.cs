using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one validated property with the shared input or checkbox control.
/// </summary>
public sealed class ContentEditorFieldView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI label;

    [SerializeField]
    private TMP_InputField input;

    [SerializeField]
    private UICheckboxView checkbox;

    [SerializeField]
    private TextMeshProUGUI error;

    [SerializeField]
    private Button importButton;

    [SerializeField]
    private Button revertButton;

    [SerializeField]
    private Button selectItemsButton;

    [SerializeField]
    private TMP_Dropdown choiceDropdown;

    [SerializeField]
    private Button previewButton;

    [SerializeField]
    private HelpTooltipView help;
    private ContentPackEditorField field;
    private ContentPackEditorFieldRenderData presentation;
    private Action changed;
    private Action<ContentPackEditorField> importRequested;
    private readonly List<UICheckboxView> choices = new List<UICheckboxView>();
    private Action<ContentPackEditorField, string> previewRequested;
    private bool bound;
    private bool rendering;
    private float lastLabelWidth;
    private IReadOnlyList<string> choiceValues = Array.Empty<string>();
    private IReadOnlyList<string> mediaFiles = Array.Empty<string>();

    public event Action<string> ValidationFailed;
    public event Action<ContentPackEditorField, IReadOnlyList<string>> Submitted;
    public event Action<ContentPackEditorField> ValidationRequested;
    public event Action<ContentPackEditorField> SelectionRequested;

    public int Height => PreferredHeight(((RectTransform)transform).rect.width, lastLabelWidth);

    public bool HasError => error != null && !string.IsNullOrEmpty(error.text);
    public bool HasOpenChoice => choiceDropdown.IsExpanded;
    public ContentPackEditorField Field => field;

    /// <summary>
    /// Applies the owning form's background palette to the editable value.
    /// </summary>
    /// <param name="role">The background's semantic palette role.</param>
    public void SetBackgroundRole(ContentEditorColorRole role) =>
        input.GetComponent<ContentEditorColorBinding>().SetRole(role);

    /// <summary>
    /// Centers a compact numeric control and its text within the owning table cell.
    /// </summary>
    public void SetCellLayout()
    {
        RectTransform value = (RectTransform)input.transform;
        value.anchorMin = value.anchorMax = value.pivot = new Vector2(0.5f, 0.5f);
        value.anchoredPosition = Vector2.zero;
        value.sizeDelta = new Vector2(((RectTransform)transform).rect.width - 8, 32);
        UILayout.ConfigureCenteredNumericInput(
            input,
            input.textComponent.color,
            input.textComponent.fontSize,
            10,
            6
        );
    }

    /// <summary>
    /// Fits one shared field into a compact unit form or a full-width configuration form.
    /// </summary>
    /// <param name="bounds">The content-space field bounds.</param>
    /// <param name="labelWidth">The space reserved for its label, or zero for a skill value cell.</param>
    /// <returns>The row height, including the complete wrapped caption.</returns>
    public int SetLayout(Rect bounds, float labelWidth)
    {
        bool hasHelp = labelWidth > 0 && !string.IsNullOrWhiteSpace(field.HelpText);
        RectTransform helpRect = (RectTransform)help.transform;
        int helpSize = Mathf.RoundToInt(helpRect.rect.width);
        float captionWidth = Mathf.Max(0, labelWidth - 12 - (hasHelp ? helpSize + 4 : 0));
        float valueLeft = Mathf.Max(4, labelWidth);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        int labelHeight =
            labelWidth > 0
                ? Mathf.Max(
                    32,
                    Mathf.CeilToInt(
                        label
                            .GetPreferredValues(
                                label.text,
                                Mathf.Max(1, captionWidth),
                                float.PositiveInfinity
                            )
                            .y
                    ) + 2
                )
                : 0;
        float labelExtra = Mathf.Max(0, labelHeight - 32) * 0.5f;
        lastLabelWidth = labelWidth;
        int height = Mathf.Max(
            Mathf.CeilToInt(
                (bounds.height > 0 ? bounds.height : PreferredHeight(bounds.width, labelWidth))
                    + labelExtra
            ),
            labelHeight + 24
        );
        bool multiline = field.IsMultiline || field.IsList && input.gameObject.activeSelf;
        if (multiline && input.gameObject.activeSelf && !field.IsMediaDirectory)
        {
            float textWidth = Mathf.Max(1, bounds.width - valueLeft - 28);
            float textHeight = input
                .textComponent.GetPreferredValues(input.text, textWidth, float.PositiveInfinity)
                .y;
            height = Mathf.Max(height, Mathf.CeilToInt(textHeight + labelExtra + 52));
        }
        int bodyHeight = height;
        int errorHeight = HasError
            ? Mathf.Max(
                32,
                Mathf.CeilToInt(
                    error
                        .GetPreferredValues(
                            error.text,
                            Mathf.Max(1, bounds.width - valueLeft - 88),
                            float.PositiveInfinity
                        )
                        .y
                )
            )
            : 0;
        bounds.height = height + (HasError ? errorHeight + 8 : 0);
        RectTransform root = (RectTransform)transform;
        UILayout.SetSourceRect(
            root,
            Mathf.RoundToInt(bounds.x),
            Mathf.RoundToInt(bounds.y),
            Mathf.RoundToInt(bounds.width),
            Mathf.RoundToInt(bounds.height)
        );
        label.gameObject.SetActive(labelWidth > 0);
        UILayout.SetSourceRect(
            label.rectTransform,
            4,
            4,
            Mathf.RoundToInt(captionWidth),
            labelHeight
        );
        help.gameObject.SetActive(hasHelp);
        if (hasHelp)
            UILayout.SetSourceRect(
                helpRect,
                Mathf.RoundToInt(8 + Mathf.Min(label.preferredWidth, captionWidth)),
                4 + (labelHeight - helpSize) / 2,
                helpSize,
                helpSize
            );
        bool stackedMedia = field.IsMedia && bounds.width < 600;
        float valueTop = stackedMedia ? 0 : 4 + labelExtra;
        float valueHeight =
            field.IsMediaDirectory ? (field.IsList ? 96 : 32)
            : multiline ? bodyHeight - valueTop - 20
            : 32;
        RectTransform value = (RectTransform)input.transform;
        value.anchorMin = new Vector2(0, 1);
        value.anchorMax = Vector2.one;
        value.pivot = Vector2.up;
        value.offsetMin = new Vector2(valueLeft, -valueTop - valueHeight);
        value.offsetMax = new Vector2(
            field.IsMedia && !field.IsMediaDirectory && !stackedMedia ? -190 : -8,
            -valueTop
        );
        if (field.IsNumber && !multiline)
        {
            value.anchorMin = value.anchorMax = value.pivot = Vector2.up;
            value.anchoredPosition = new Vector2(valueLeft, -valueTop);
            value.sizeDelta = new Vector2(
                Mathf.Min(120, Mathf.Max(1, bounds.width - valueLeft - 8)),
                valueHeight
            );
        }
        input.textComponent.alignment =
            multiline ? TextAlignmentOptions.TopLeft
            : field.IsNumber ? TextAlignmentOptions.MidlineRight
            : TextAlignmentOptions.MidlineLeft;
        RectTransform choice = (RectTransform)choiceDropdown.transform;
        choice.anchorMin = new Vector2(0, 1);
        choice.anchorMax = Vector2.one;
        float choiceTop = field.IsMediaDirectory ? (field.IsList ? 104 : 40) + valueTop : valueTop;
        choice.offsetMin = new Vector2(valueLeft, -choiceTop - 32);
        choice.offsetMax = new Vector2(field.IsMediaDirectory ? -104 : -8, -choiceTop);
        UILayout.SetSourceRect(
            (RectTransform)selectItemsButton.transform,
            Mathf.RoundToInt(valueLeft),
            Mathf.RoundToInt(valueTop),
            Mathf.Max(1, Mathf.RoundToInt(bounds.width - valueLeft - 8)),
            32
        );
        revertButton.gameObject.SetActive(HasError);
        if (HasError)
        {
            UILayout.SetSourceRect(
                error.rectTransform,
                Mathf.RoundToInt(valueLeft),
                bodyHeight - 8,
                Mathf.Max(1, Mathf.RoundToInt(bounds.width - valueLeft - 88)),
                errorHeight
            );
            UILayout.SetSourceRect(
                (RectTransform)revertButton.transform,
                Mathf.RoundToInt(bounds.width - 76),
                bodyHeight - 8,
                68,
                32
            );
        }
        UILayout.SetSourceRect(
            (RectTransform)checkbox.transform,
            Mathf.RoundToInt(valueLeft),
            Mathf.RoundToInt(valueTop + 4),
            Mathf.RoundToInt(bounds.width - labelWidth - 8),
            24
        );
        checkbox.GetComponentInChildren<TextMeshProUGUI>(true).text = string.Empty;
        int columns = ChoiceColumns(bounds.width, labelWidth);
        float choiceWidth = (bounds.width - valueLeft - 8) / columns;
        for (int i = 0; i < choices.Count; i++)
            UILayout.SetSourceRect(
                (RectTransform)choices[i].transform,
                Mathf.RoundToInt(valueLeft + i % columns * choiceWidth),
                Mathf.RoundToInt(valueTop + 4) + i / columns * 32,
                Mathf.RoundToInt(choiceWidth - 8),
                24
            );
        if (stackedMedia)
        {
            RectTransform preview = (RectTransform)previewButton.transform;
            RectTransform import = (RectTransform)importButton.transform;
            UILayout.SetSourceRect(preview, 0, bodyHeight - 54, 90, 32);
            UILayout.SetSourceRect(import, 100, bodyHeight - 54, 90, 32);
        }
        else
        {
            RectTransform preview = (RectTransform)previewButton.transform;
            RectTransform import = (RectTransform)importButton.transform;
            preview.anchorMin = preview.anchorMax = preview.pivot = new Vector2(1, 1);
            preview.anchoredPosition = field.IsMediaDirectory
                ? new Vector2(-4, -choiceTop)
                : new Vector2(-96, -valueTop);
            preview.sizeDelta = new Vector2(86, 32);
            import.anchorMin = import.anchorMax = import.pivot = new Vector2(1, 1);
            import.anchoredPosition = new Vector2(-4, -valueTop);
            import.sizeDelta = new Vector2(86, 32);
        }
        return Mathf.CeilToInt(bounds.height);
    }

    /// <summary>
    /// Measures the field's content at its actual available width before placing a row.
    /// </summary>
    /// <param name="width">The full row width.</param>
    /// <param name="labelWidth">The caption column width.</param>
    /// <returns>The unadorned field height.</returns>
    private int PreferredHeight(float width, float labelWidth) =>
        field == null || selectItemsButton.gameObject.activeSelf ? 56
        : field.IsMediaDirectory ? (field.IsList ? 160 : 96)
        : field.IsList
        && (choiceValues.Count > 0 || field.IsFactionReference || field.IsTraitReference)
            ? Math.Max(
                56,
                Mathf.CeilToInt((float)choiceValues.Count / ChoiceColumns(width, labelWidth)) * 32
                    + 20
            )
        : field.IsMultiline || field.IsList ? 152
        : 56;

    /// <summary>
    /// Packs set-valued choices into columns wide enough for their actual captions.
    /// </summary>
    /// <param name="width">The complete row width.</param>
    /// <param name="labelWidth">The reserved caption width.</param>
    /// <returns>The number of readable choice columns.</returns>
    private int ChoiceColumns(float width, float labelWidth)
    {
        float minimum = Mathf.Max(
            180,
            choices
                .Where(choice => choice.gameObject.activeSelf)
                .Select(choice => choice.GetComponentInChildren<TMP_Text>().preferredWidth + 40)
                .DefaultIfEmpty(180)
                .Max()
        );
        return Mathf.Clamp(
            Mathf.FloorToInt((width - Mathf.Max(4, labelWidth) - 8) / minimum),
            1,
            4
        );
    }

    /// <summary>
    /// Restores the last accepted value without undoing other project changes.
    /// </summary>
    public void RevertDraft()
    {
        if (field == null)
            return;
        input.SetTextWithoutNotify(field.IsList ? string.Join("\n", field.Values) : field.Value);
        input.GetComponent<TextInputHistory>()?.ClearHistory();
        error.text = string.Empty;
        revertButton.gameObject.SetActive(false);
        changed?.Invoke();
    }

    /// <summary>
    /// Binds the authored controls once.
    /// </summary>
    private void Awake()
    {
        BindControls();
    }

    /// <summary>
    /// Connects authored controls once for runtime and native preview rendering.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            label == null
            || input == null
            || checkbox == null
            || error == null
            || importButton == null
            || revertButton == null
            || selectItemsButton == null
            || choiceDropdown == null
            || previewButton == null
            || help == null
        )
            throw new MissingReferenceException(name + " has incomplete field references.");
        revertButton.onClick.AddListener(RevertDraft);
        selectItemsButton.onClick.AddListener(RequestSelection);
        bound = true;
        input.onEndEdit.AddListener(ApplyText);
        importButton.onClick.AddListener(() => importRequested?.Invoke(field));
        choiceDropdown.onValueChanged.AddListener(index =>
        {
            if (!rendering && field?.IsMediaDirectory == false && index < choiceValues.Count)
                ApplyText(choiceValues[index]);
        });
        previewButton.onClick.AddListener(PreviewSelectedMedia);
        checkbox.ValueChanged += ApplyBoolean;
    }

    /// <summary>
    /// Releases the shared checkbox listener.
    /// </summary>
    private void OnDestroy()
    {
        if (revertButton != null)
            revertButton.onClick.RemoveListener(RevertDraft);
        if (selectItemsButton != null)
            selectItemsButton.onClick.RemoveListener(RequestSelection);
        if (bound)
            checkbox.ValueChanged -= ApplyBoolean;
    }

    /// <summary>
    /// Displays the selected member and configures its input type.
    /// </summary>
    /// <param name="value">The member and its display choices.</param>
    /// <param name="onChanged">The notification after a valid edit.</param>
    /// <param name="onImport">The media-file selection request.</param>
    /// <param name="onPreview">The media preview request.</param>
    public void Render(
        ContentPackEditorFieldRenderData value,
        Action onChanged,
        Action<ContentPackEditorField> onImport,
        Action<ContentPackEditorField, string> onPreview
    )
    {
        rendering = true;
        try
        {
            BindControls();
            SetBackgroundRole(ContentEditorColorRole.Input);
            presentation = value;
            field = value.Field;
            help.Configure(field.HelpText, input);
            IReadOnlyDictionary<string, string> referenceLabels = value.ReferenceLabels;
            choiceValues =
                referenceLabels != null
                    ? referenceLabels
                        .Keys.Concat(field.Values.Where(item => !string.IsNullOrWhiteSpace(item)))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                : field.IsList && field.Choices.Count > 0
                    ? field.Choices.Concat(field.Values).Distinct(StringComparer.Ordinal).ToArray()
                : field.Choices;
            changed = onChanged;
            importRequested = onImport;
            previewRequested = onPreview;
            previewButton.GetComponentInChildren<TextMeshProUGUI>().text =
                field.MediaType == ContentPackEditorMediaType.Audio ? "Play/stop" : "Preview";
            label.text = field.Caption;
            error.text = string.Empty;
            revertButton.gameObject.SetActive(false);
            importButton.gameObject.SetActive(field.IsMedia && !field.IsMediaDirectory);
            previewButton.gameObject.SetActive(
                field.MediaType
                    is ContentPackEditorMediaType.Image
                        or ContentPackEditorMediaType.Audio
            );
            previewButton.interactable = field.Values.Any(address =>
                !string.IsNullOrWhiteSpace(address)
            );
            bool isChoice = !field.IsList && choiceValues.Count > 0;
            if (isChoice && !choiceValues.Contains(field.Value))
                choiceValues = new[] { field.Value }.Concat(choiceValues).ToArray();
            bool isPicker =
                field.IsList
                && referenceLabels != null
                && !field.IsFactionReference
                && !field.IsTraitReference;
            bool isChecklist =
                !isPicker && field.IsList && (choiceValues.Count > 0 || referenceLabels != null);
            bool booleanValue = false;
            bool showBoolean = field.IsBoolean && bool.TryParse(field.Value, out booleanValue);
            bool emptyReference = referenceLabels != null && choiceValues.Count == 0;
            choiceDropdown.gameObject.SetActive(
                isChoice || field.IsMediaDirectory || emptyReference
            );
            choiceDropdown.interactable = true;
            if (emptyReference)
            {
                choiceDropdown.ClearOptions();
                choiceDropdown.AddOptions(
                    new List<string>
                    {
                        field.IsTraitReference ? "No traits defined" : "No factions defined",
                    }
                );
                choiceDropdown.SetValueWithoutNotify(0);
                choiceDropdown.RefreshShownValue();
                choiceDropdown.interactable = false;
            }
            if (isChoice)
            {
                choiceDropdown.ClearOptions();
                choiceDropdown.AddOptions(
                    choiceValues
                        .Select(choice =>
                            referenceLabels == null ? ContentPackEditorField.FormatName(choice)
                            : referenceLabels.TryGetValue(choice, out string caption) ? caption
                            : "Missing: " + choice
                        )
                        .ToList()
                );
                choiceDropdown.SetValueWithoutNotify(
                    Array.IndexOf(choiceValues.ToArray(), field.Value)
                );
                choiceDropdown.RefreshShownValue();
            }
            input.gameObject.SetActive(!showBoolean && !isChoice && !isChecklist && !isPicker);
            selectItemsButton.gameObject.SetActive(isPicker);
            if (isPicker)
                selectItemsButton.GetComponentInChildren<TMP_Text>().text =
                    field.Values.Count == 0
                        ? "Choose..."
                        : field.Values.Distinct().Count() + " selected — choose...";
            checkbox.gameObject.SetActive(showBoolean);
            foreach (UICheckboxView choice in choices)
                choice.gameObject.SetActive(false);
            if (isChecklist)
            {
                for (int i = 0; i < choiceValues.Count; i++)
                {
                    int index = i;
                    if (choices.Count <= i)
                    {
                        UICheckboxView choice = Instantiate(checkbox, transform);
                        choices.Add(choice);
                        choice.ValueChanged += enabled => ApplyChoice(index, enabled);
                    }
                    UICheckboxView control = choices[i];
                    control.GetComponentInChildren<TextMeshProUGUI>(true).text =
                        referenceLabels != null
                            ? (
                                referenceLabels.TryGetValue(
                                    choiceValues[i],
                                    out string referenceName
                                )
                                    ? referenceName
                                    : (
                                        field.IsTraitReference
                                            ? "Unknown trait: "
                                            : "Unknown faction: "
                                    ) + choiceValues[i]
                            )
                            : ContentPackEditorField.FormatName(choiceValues[i]);
                    UILayout.SetSourceRect(
                        (RectTransform)control.transform,
                        244,
                        9 + i * 32,
                        300,
                        26
                    );
                    control.SetIsCheckedWithoutNotify(field.Values.Contains(choiceValues[i]));
                    control.gameObject.SetActive(true);
                }
            }
            if (showBoolean)
                checkbox.SetIsCheckedWithoutNotify(booleanValue);
            else
            {
                input.contentType = TMP_InputField.ContentType.Standard;
                input.characterValidation = TMP_InputField.CharacterValidation.None;
                input.lineType =
                    field.IsMultiline || field.IsList
                        ? TMP_InputField.LineType.MultiLineNewline
                        : TMP_InputField.LineType.SingleLine;
                input.textComponent.textWrappingMode =
                    field.IsMultiline || field.IsList
                        ? TextWrappingModes.Normal
                        : TextWrappingModes.NoWrap;
                input.textComponent.alignment =
                    field.IsMultiline || field.IsList
                        ? TextAlignmentOptions.TopLeft
                        : TextAlignmentOptions.MidlineLeft;
                input.SetTextWithoutNotify(
                    field.IsList ? string.Join("\n", field.Values) : field.Value
                );
                RectTransform rect = (RectTransform)input.transform;
                rect.offsetMin = new Vector2(244, 22);
                rect.offsetMax = new Vector2(field.IsMedia ? -190 : -14, -8);
            }
            if (field.IsMediaDirectory)
                RenderMediaFiles();
            input.GetComponent<TextInputHistory>()?.ClearHistory();
            gameObject.SetActive(true);
        }
        finally
        {
            rendering = false;
        }
    }

    /// <summary>
    /// Releases bindings before a pooled field is hidden or assigned to another project.
    /// </summary>
    public void Clear()
    {
        field = null;
        presentation = null;
        changed = null;
        importRequested = null;
        previewRequested = null;
        choiceValues = mediaFiles = Array.Empty<string>();
        help.Configure(null, input);
        help.gameObject.SetActive(false);
        input.GetComponent<TextInputHistory>()?.ClearHistory();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Commits a pending text edit before an operation changes selection.
    /// </summary>
    public void Commit()
    {
        if (field != null && input.gameObject.activeSelf)
            ApplyText(input.text);
    }

    /// <summary>
    /// Validates the accepted value for an explicit validation or save operation.
    /// </summary>
    public void ValidateCurrent()
    {
        try
        {
            ValidationRequested?.Invoke(field);
            error.text = string.Empty;
            revertButton.gameObject.SetActive(false);
        }
        catch (ArgumentException exception)
        {
            ShowError(exception);
        }
    }

    /// <summary>
    /// Validates a text edit and keeps any error beside the responsible field.
    /// </summary>
    /// <param name="text">The proposed value.</param>
    private void ApplyText(string text)
    {
        if (field == null || rendering)
            return;
        try
        {
            bool modified = text != (field.IsList ? string.Join("\n", field.Values) : field.Value);
            if (!modified)
            {
                bool hadError = HasError;
                error.text = string.Empty;
                revertButton.gameObject.SetActive(false);
                if (hadError)
                    changed?.Invoke();
                return;
            }
            if (field.IsList)
                Submitted?.Invoke(
                    field,
                    text.Split('\n')
                        .Select(value => value.TrimEnd('\r'))
                        .Where(value => value.Length > 0)
                        .ToArray()
                );
            else
                Submitted?.Invoke(field, new[] { text });
            input.GetComponent<TextInputHistory>()?.ClearHistory();
            error.text = string.Empty;
            if (field.IsMediaDirectory)
                RenderMediaFiles();
            if (modified)
                changed?.Invoke();
        }
        catch (ArgumentException exception)
        {
            ShowError(exception);
        }
    }

    /// <summary>
    /// Applies a checkbox state through the same validated field contract.
    /// </summary>
    /// <param name="value">The checked state.</param>
    private void ApplyBoolean(bool value)
    {
        ApplyText(value ? "true" : "false");
    }

    /// <summary>
    /// Applies one enum checklist choice while preserving declaration order.
    /// </summary>
    /// <param name="index">The changed option.</param>
    /// <param name="enabled">Whether the option is enabled.</param>
    private void ApplyChoice(int index, bool enabled)
    {
        if (field == null || rendering)
            return;
        HashSet<string> selected = new HashSet<string>(field.Values);
        if (enabled)
            selected.Add(choiceValues[index]);
        else
            selected.Remove(choiceValues[index]);
        try
        {
            Submitted?.Invoke(field, choiceValues.Where(selected.Contains).ToArray());
            error.text = string.Empty;
            changed?.Invoke();
        }
        catch (ArgumentException exception)
        {
            choices[index].SetIsCheckedWithoutNotify(field.Values.Contains(choiceValues[index]));
            ShowError(exception);
        }
    }

    /// <summary>
    /// Lists files inside an authored media directory using the shared selector.
    /// </summary>
    private void RenderMediaFiles()
    {
        try
        {
            mediaFiles = presentation.GetMediaFiles();
        }
        catch (ArgumentException)
        {
            mediaFiles = Array.Empty<string>();
            error.text = "Check the folder path.";
        }
        choiceDropdown.ClearOptions();
        string root = field.Value.TrimEnd('/') + "/";
        choiceDropdown.AddOptions(
            mediaFiles
                .Select(address =>
                    address.StartsWith(root, StringComparison.Ordinal)
                        ? address[root.Length..]
                        : address
                )
                .ToList()
        );
        choiceDropdown.SetValueWithoutNotify(0);
        choiceDropdown.RefreshShownValue();
        previewButton.interactable = mediaFiles.Count > 0;
        choiceDropdown.interactable = mediaFiles.Count > 0;
    }

    /// <summary>
    /// Presents a concise field error and forwards the complete notice to the workspace.
    /// </summary>
    /// <param name="exception">The rejected edit.</param>
    private void ShowError(ArgumentException exception)
    {
        string message = exception.Message.Split('\n')[0];
        if (error.text != message)
            ValidationFailed?.Invoke(field.Name + ": " + message);
        error.text = message;
        revertButton.gameObject.SetActive(true);
        changed?.Invoke();
    }

    /// <summary>
    /// Previews the media line containing the caret, retaining punctuation within file names.
    /// </summary>
    private void PreviewSelectedMedia()
    {
        if (field?.IsMedia != true)
            return;
        Commit();
        if (HasError)
            return;
        try
        {
            ValidationRequested?.Invoke(field);
        }
        catch (ArgumentException exception)
        {
            ShowError(exception);
            return;
        }
        if (field.IsMediaDirectory)
        {
            string selected = mediaFiles.ElementAtOrDefault(choiceDropdown.value);
            if (selected != null)
                previewRequested?.Invoke(field, selected);
            return;
        }
        int line = field.IsList
            ? input
                .text.Take(Math.Min(input.caretPosition, input.text.Length))
                .Count(character => character == '\n')
            : 0;
        string address = field.Values.ElementAtOrDefault(line) ?? field.Values.FirstOrDefault();
        previewRequested?.Invoke(field, address);
    }

    /// <summary>
    /// Requests named selection without exposing reference identifiers as free text.
    /// </summary>
    private void RequestSelection() => SelectionRequested?.Invoke(field);
}
