using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the identity, inheritance source, and destination of a new content pack.
/// </summary>
public sealed class ContentEditorPackDialogView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private TMP_InputField newIDInput;

    [SerializeField]
    private TMP_InputField newNameInput;

    [SerializeField]
    private TMP_InputField newVersionInput;

    [SerializeField]
    private TMP_InputField newFolderInput;

    [SerializeField]
    private TextMeshProUGUI newFolderPathText;

    [SerializeField]
    private TextMeshProUGUI helpText;

    [SerializeField]
    private Button folderBrowseButton;

    [SerializeField]
    private TMP_Dropdown packChoiceDropdown;

    [SerializeField]
    private TextMeshProUGUI dialogError;

    [SerializeField]
    private Button dialogConfirmButton;

    [SerializeField]
    private Button dialogCancelButton;

    private IReadOnlyList<ContentPackDefinition> packs = Array.Empty<ContentPackDefinition>();
    private string chosenPackID;
    private string defaultLocation;
    private string automaticPackID = string.Empty;
    private bool bound;

    public event Action<string, string, string, string, string> CreateRequested;
    public event Action<string> BrowseRequested;
    public bool IsOpen => gameObject.activeSelf;
    public bool HasOpenChoice => packChoiceDropdown.IsExpanded;

    /// <summary>
    /// Binds the authored controls when the dialog first becomes active.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Connects each native control once without installing application operations in the view.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            newIDInput == null
            || newNameInput == null
            || newVersionInput == null
            || newFolderInput == null
            || newFolderPathText == null
            || helpText == null
            || folderBrowseButton == null
            || packChoiceDropdown == null
            || dialogError == null
            || dialogConfirmButton == null
            || dialogCancelButton == null
        )
            throw new MissingReferenceException(
                name + " has incomplete new-pack dialog references."
            );
        newNameInput.onValueChanged.AddListener(UpdatePackName);
        newIDInput.onValueChanged.AddListener(HandleRequiredFieldChanged);
        newVersionInput.onValueChanged.AddListener(HandleRequiredFieldChanged);
        newFolderInput.onValueChanged.AddListener(HandleRequiredFieldChanged);
        packChoiceDropdown.onValueChanged.AddListener(SelectBasePack);
        folderBrowseButton.onClick.AddListener(RequestBrowse);
        dialogConfirmButton.onClick.AddListener(Submit);
        dialogCancelButton.onClick.AddListener(Hide);
        bound = true;
    }

    /// <summary>
    /// Releases the listeners owned by this dialog.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        newNameInput.onValueChanged.RemoveListener(UpdatePackName);
        newIDInput.onValueChanged.RemoveListener(HandleRequiredFieldChanged);
        newVersionInput.onValueChanged.RemoveListener(HandleRequiredFieldChanged);
        newFolderInput.onValueChanged.RemoveListener(HandleRequiredFieldChanged);
        packChoiceDropdown.onValueChanged.RemoveListener(SelectBasePack);
        folderBrowseButton.onClick.RemoveListener(RequestBrowse);
        dialogConfirmButton.onClick.RemoveListener(Submit);
        dialogCancelButton.onClick.RemoveListener(Hide);
    }

    /// <summary>
    /// Configures the installed inheritance choices and initial working directory.
    /// </summary>
    /// <param name="definitions">The installed pack choices.</param>
    /// <param name="modsFolder">The default directory for authored packs.</param>
    public void Initialize(IReadOnlyList<ContentPackDefinition> definitions, string modsFolder)
    {
        BindControls();
        packs = definitions ?? Array.Empty<ContentPackDefinition>();
        chosenPackID = packs.FirstOrDefault()?.ID;
        defaultLocation = modsFolder;
    }

    /// <summary>
    /// Opens a fresh creation form while retaining the installed base-pack choices.
    /// </summary>
    public void Show()
    {
        BindControls();
        automaticPackID = string.Empty;
        newNameInput.SetTextWithoutNotify(string.Empty);
        newIDInput.SetTextWithoutNotify(string.Empty);
        newFolderInput.SetTextWithoutNotify(defaultLocation);
        newVersionInput.SetTextWithoutNotify("1.0.0");
        dialogError.text = string.Empty;
        packChoiceDropdown.ClearOptions();
        packChoiceDropdown.AddOptions(packs.Select(pack => pack.DisplayName).ToList());
        packChoiceDropdown.SetValueWithoutNotify(
            Math.Max(0, Array.FindIndex(packs.ToArray(), pack => pack.ID == chosenPackID))
        );
        packChoiceDropdown.RefreshShownValue();
        UpdateCreateAvailability();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the form without modifying its open working project.
    /// </summary>
    public void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Dismisses only this dialog when the application routes a cancel request to it.
    /// </summary>
    /// <returns>Whether the dialog handled the request.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        Hide();
        return true;
    }

    /// <summary>
    /// Displays an application error without discarding the entered creation values.
    /// </summary>
    /// <param name="message">The validation or file-operation failure.</param>
    public void ShowError(string message)
    {
        dialogError.text = message;
        LayoutContent();
    }

    /// <summary>
    /// Keeps complete destination paths and wrapped errors above the dialog's action row.
    /// </summary>
    private void LayoutContent()
    {
        RectTransform panel = (RectTransform)dialogError.transform.parent;
        float width = panel.rect.width - 56;
        float pathHeight = Mathf.Max(
            48,
            newFolderPathText.GetPreferredValues(newFolderPathText.text, width, 0).y
        );
        float helpHeight = Mathf.Max(48, helpText.GetPreferredValues(helpText.text, width, 0).y);
        float errorHeight = Mathf.Max(
            48,
            dialogError.GetPreferredValues(dialogError.text, width, 0).y
        );
        UILayout.SetSourceRect(
            newFolderPathText.rectTransform,
            28,
            370,
            Mathf.RoundToInt(width),
            Mathf.CeilToInt(pathHeight)
        );
        int helpTop = Mathf.CeilToInt(378 + pathHeight);
        UILayout.SetSourceRect(
            helpText.rectTransform,
            28,
            helpTop,
            Mathf.RoundToInt(width),
            Mathf.CeilToInt(helpHeight)
        );
        int errorTop = Mathf.CeilToInt(helpTop + helpHeight + 6);
        UILayout.SetSourceRect(
            dialogError.rectTransform,
            28,
            errorTop,
            Mathf.RoundToInt(width),
            Mathf.CeilToInt(errorHeight)
        );
        int buttonTop = Mathf.CeilToInt(errorTop + errorHeight + 8);
        UILayout.SetSourceRect(
            (RectTransform)dialogConfirmButton.transform,
            444,
            buttonTop,
            168,
            36
        );
        UILayout.SetSourceRect(
            (RectTransform)dialogCancelButton.transform,
            322,
            buttonTop,
            110,
            36
        );
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, buttonTop + 64);
    }

    /// <summary>
    /// Displays the parent folder chosen by the application's native file picker.
    /// </summary>
    /// <param name="folder">The selected parent directory.</param>
    public void SetLocation(string folder)
    {
        newFolderInput.SetTextWithoutNotify(folder);
        UpdateCreateAvailability();
    }

    /// <summary>
    /// Updates creation availability after a required value changes.
    /// </summary>
    /// <param name="value">The native control's current value.</param>
    private void HandleRequiredFieldChanged(string value) => UpdateCreateAvailability();

    /// <summary>
    /// Selects the inheritance source without making changes to the current project.
    /// </summary>
    /// <param name="index">The selected installed-pack index.</param>
    private void SelectBasePack(int index)
    {
        chosenPackID = packs.ElementAtOrDefault(index)?.ID;
        UpdateCreateAvailability();
    }

    /// <summary>
    /// Requests a native folder picker from the application controller.
    /// </summary>
    private void RequestBrowse() => BrowseRequested?.Invoke(newFolderInput.text);

    /// <summary>
    /// Shows the resulting destination and disables creation until required values are present.
    /// </summary>
    private void UpdateCreateAvailability()
    {
        newFolderPathText.text = string.IsNullOrWhiteSpace(newIDInput.text)
            ? "The pack folder is named automatically from its ID."
            : "Creates: "
                + newFolderInput.text.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                )
                + Path.DirectorySeparatorChar
                + newIDInput.text;
        dialogConfirmButton.interactable =
            packs.Any(pack => pack.ID == chosenPackID)
            && !string.IsNullOrWhiteSpace(newNameInput.text)
            && !string.IsNullOrWhiteSpace(newIDInput.text)
            && !string.IsNullOrWhiteSpace(newVersionInput.text)
            && !string.IsNullOrWhiteSpace(newFolderInput.text);
        LayoutContent();
    }

    /// <summary>
    /// Suggests an identifier until the author explicitly edits it.
    /// </summary>
    /// <param name="name">The entered display name.</param>
    private void UpdatePackName(string name)
    {
        string next = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (newIDInput.text == automaticPackID)
            newIDInput.SetTextWithoutNotify(next);
        automaticPackID = next;
        UpdateCreateAvailability();
    }

    /// <summary>
    /// Submits entered values while keeping the form available if creation fails.
    /// </summary>
    private void Submit()
    {
        dialogError.text = string.Empty;
        CreateRequested?.Invoke(
            chosenPackID,
            newIDInput.text,
            newNameInput.text,
            newVersionInput.text,
            newFolderInput.text
        );
    }
}
