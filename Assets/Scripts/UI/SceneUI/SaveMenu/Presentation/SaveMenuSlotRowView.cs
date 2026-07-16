using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one save slot and emits save or load requests for that slot.
/// </summary>
public sealed class SaveMenuSlotRowView : MonoBehaviour
{
    [SerializeField]
    private RawImage factionImage;

    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Button loadButton;

    [SerializeField]
    private RawImagePressVisual saveButtonPressVisual;

    [SerializeField]
    private RawImagePressVisual loadButtonPressVisual;

    [SerializeField]
    private TMP_InputField nameInputField;

    [SerializeField]
    private Texture2D saveButtonUpTexture;

    [SerializeField]
    private Texture2D saveButtonDownTexture;

    [SerializeField]
    private Texture2D saveButtonDisabledTexture;

    [SerializeField]
    private Texture2D loadButtonUpTexture;

    [SerializeField]
    private Texture2D loadButtonDownTexture;

    [SerializeField]
    private Texture2D loadButtonDisabledTexture;

    private int slot = -1;
    private string renderedName = string.Empty;
    private string draftName = string.Empty;
    private bool draftChanged;
    private bool bound;

    /// <summary>
    /// Occurs when the player requests a named save in this slot.
    /// </summary>
    public event Action<int, string> SaveRequested;

    /// <summary>
    /// Occurs when the player requests loading this slot.
    /// </summary>
    public event Action<int> LoadRequested;

    /// <summary>
    /// Renders one slot while preserving an in-progress name edit.
    /// </summary>
    /// <param name="data">The slot presentation data.</param>
    public void Render(SaveSlotRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        BindControls();
        bool slotChanged = slot != data.Slot;
        slot = data.Slot;
        RenderFaction(data.FactionIconTexture);
        RenderButtons(data.CanSave, data.CanLoad);
        RenderName(data.Label, data.CanSave, slotChanged);
    }

    /// <summary>
    /// Verifies every authored reference required to render and interact with the row.
    /// </summary>
    public void VerifyReferences()
    {
        if (factionImage == null)
            throw new MissingReferenceException("FactionImage is missing.");
        if (saveButton == null)
            throw new MissingReferenceException("SaveButton is missing.");
        if (loadButton == null)
            throw new MissingReferenceException("LoadButton is missing.");
        if (saveButtonPressVisual == null)
            throw new MissingReferenceException("SaveButtonPressVisual is missing.");
        if (loadButtonPressVisual == null)
            throw new MissingReferenceException("LoadButtonPressVisual is missing.");
        if (nameInputField == null || nameInputField.textComponent == null)
            throw new MissingReferenceException("NameInputField is missing.");
        if (saveButtonUpTexture == null)
            throw new MissingReferenceException("SaveButtonUpTexture is missing.");
        if (saveButtonDownTexture == null)
            throw new MissingReferenceException("SaveButtonDownTexture is missing.");
        if (saveButtonDisabledTexture == null)
            throw new MissingReferenceException("SaveButtonDisabledTexture is missing.");
        if (loadButtonUpTexture == null)
            throw new MissingReferenceException("LoadButtonUpTexture is missing.");
        if (loadButtonDownTexture == null)
            throw new MissingReferenceException("LoadButtonDownTexture is missing.");
        if (loadButtonDisabledTexture == null)
            throw new MissingReferenceException("LoadButtonDisabledTexture is missing.");
    }

    /// <summary>
    /// Binds authored controls while the row is active.
    /// </summary>
    private void OnEnable()
    {
        if (ReferencesAssigned())
            BindControls();
    }

    /// <summary>
    /// Removes runtime control listeners while the row is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (!bound)
            return;

        saveButton.onClick.RemoveListener(RequestSave);
        loadButton.onClick.RemoveListener(RequestLoad);
        nameInputField.onValueChanged.RemoveListener(SetDraftName);
        nameInputField.onSubmit.RemoveListener(SubmitName);
        bound = false;
    }

    /// <summary>
    /// Renders the optional faction icon without changing its authored bounds.
    /// </summary>
    /// <param name="texture">The faction icon texture.</param>
    private void RenderFaction(Texture2D texture)
    {
        factionImage.texture = texture;
        factionImage.enabled = texture != null;
    }

    /// <summary>
    /// Applies enabled and pressed visuals to the row commands.
    /// </summary>
    /// <param name="canSave">Whether saving is enabled.</param>
    /// <param name="canLoad">Whether loading is enabled.</param>
    private void RenderButtons(bool canSave, bool canLoad)
    {
        saveButton.interactable = canSave;
        loadButton.interactable = canLoad;
        saveButtonPressVisual.SetTextures(
            canSave ? saveButtonUpTexture : saveButtonDisabledTexture,
            canSave ? saveButtonDownTexture : saveButtonDisabledTexture
        );
        loadButtonPressVisual.SetTextures(
            canLoad ? loadButtonUpTexture : loadButtonDisabledTexture,
            canLoad ? loadButtonDownTexture : loadButtonDisabledTexture
        );
    }

    /// <summary>
    /// Applies the saved name unless the player is actively editing a draft.
    /// </summary>
    /// <param name="savedName">The persisted display name.</param>
    /// <param name="canSave">Whether the field accepts edits.</param>
    /// <param name="slotChanged">Whether the row now represents a different slot.</param>
    private void RenderName(string savedName, bool canSave, bool slotChanged)
    {
        string safeName = savedName ?? string.Empty;
        if (slotChanged || !draftChanged)
        {
            renderedName = safeName;
            draftName = safeName;
            draftChanged = false;
        }

        nameInputField.SetTextWithoutNotify(draftName);
        nameInputField.readOnly = !canSave;
    }

    /// <summary>
    /// Attaches semantic callbacks to the authored controls exactly once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        saveButton.onClick.AddListener(RequestSave);
        loadButton.onClick.AddListener(RequestLoad);
        nameInputField.onValueChanged.AddListener(SetDraftName);
        nameInputField.onSubmit.AddListener(SubmitName);
        bound = true;
    }

    /// <summary>
    /// Emits a save request using the current draft name.
    /// </summary>
    private void RequestSave()
    {
        if (!saveButton.interactable || slot < 0)
            return;

        draftChanged = false;
        renderedName = draftName;
        SaveRequested?.Invoke(slot, draftName);
    }

    /// <summary>
    /// Emits a load request for the rendered slot.
    /// </summary>
    private void RequestLoad()
    {
        if (!loadButton.interactable || slot < 0)
            return;

        LoadRequested?.Invoke(slot);
    }

    /// <summary>
    /// Tracks the player's current save-name draft.
    /// </summary>
    /// <param name="value">The current input value.</param>
    private void SetDraftName(string value)
    {
        draftName = value ?? string.Empty;
        draftChanged = !string.Equals(draftName, renderedName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Submits the current draft when the input field receives Enter.
    /// </summary>
    /// <param name="value">The submitted input value.</param>
    private void SubmitName(string value)
    {
        SetDraftName(value);
        RequestSave();
    }

    /// <summary>
    /// Checks whether the prefab has all references needed for early binding.
    /// </summary>
    /// <returns>True when every required authored reference is assigned.</returns>
    private bool ReferencesAssigned()
    {
        return factionImage != null
            && saveButton != null
            && loadButton != null
            && saveButtonPressVisual != null
            && loadButtonPressVisual != null
            && nameInputField != null
            && nameInputField.textComponent != null
            && saveButtonUpTexture != null
            && saveButtonDownTexture != null
            && saveButtonDisabledTexture != null
            && loadButtonUpTexture != null
            && loadButtonDownTexture != null
            && loadButtonDisabledTexture != null;
    }
}
