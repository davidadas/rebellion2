using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the authored save-menu hierarchy and emits semantic UI requests.
/// </summary>
public sealed class SaveMenuWindowView : MonoBehaviour
{
    [SerializeField]
    private Color enabledTextColor;

    [SerializeField]
    private Color disabledTextColor;

    [SerializeField]
    private Color versionTextColor;

    [SerializeField]
    private Button cockpitButton;

    [SerializeField]
    private Button exitButton;

    [SerializeField]
    private Button returnStrategyButton;

    [SerializeField]
    private RawImagePressVisual returnStrategyButtonPressVisual;

    [SerializeField]
    private Texture2D returnStrategyButtonUpTexture;

    [SerializeField]
    private RawImagePressVisual musicButtonPressVisual;

    [SerializeField]
    private Button musicButton;

    [SerializeField]
    private Texture2D musicButtonUpTexture;

    [SerializeField]
    private Texture2D musicButtonDownTexture;

    [SerializeField]
    private SaveMenuSliderView musicSlider;

    [SerializeField]
    private SaveMenuSliderView sfxSlider;

    [SerializeField]
    private SaveMenuTacticalOptionRowView[] tacticalOptionRows =
        Array.Empty<SaveMenuTacticalOptionRowView>();

    [SerializeField]
    private SaveMenuSlotRowView[] saveSlotRows = Array.Empty<SaveMenuSlotRowView>();

    [SerializeField]
    private TextMeshProUGUI playMusicTextField;

    [SerializeField]
    private TextMeshProUGUI playMusicStateTextField;

    [SerializeField]
    private TextMeshProUGUI versionTextField;

    [SerializeField]
    private SaveMenuConfirmDialogView confirmDialog;

    private bool bound;

    /// <summary>
    /// Occurs when the player requests returning to the cockpit menu.
    /// </summary>
    public event Action ReturnCockpitRequested;

    /// <summary>
    /// Occurs when the player requests exiting the application.
    /// </summary>
    public event Action ExitRequested;

    /// <summary>
    /// Occurs when the player requests returning to strategy gameplay.
    /// </summary>
    public event Action ReturnStrategyRequested;

    /// <summary>
    /// Occurs when the player requests toggling music playback.
    /// </summary>
    public event Action MusicToggleRequested;

    /// <summary>
    /// Occurs when the player changes the normalized music volume.
    /// </summary>
    public event Action<float> MusicVolumeChanged;

    /// <summary>
    /// Occurs when the player changes the normalized sound-effect volume.
    /// </summary>
    public event Action<float> SfxVolumeChanged;

    /// <summary>
    /// Occurs when the player requests toggling a tactical option.
    /// </summary>
    public event Action<UserTacticalOption> TacticalOptionToggleRequested;

    /// <summary>
    /// Occurs when the player requests a named save in a slot.
    /// </summary>
    public event Action<int, string> SaveRequested;

    /// <summary>
    /// Occurs when the player requests loading a slot.
    /// </summary>
    public event Action<int> LoadRequested;

    /// <summary>
    /// Occurs when the player accepts the active confirmation.
    /// </summary>
    public event Action ConfirmationAccepted;

    /// <summary>
    /// Occurs when the player cancels the active confirmation.
    /// </summary>
    public event Action ConfirmationCanceled;

    /// <summary>
    /// Renders the complete menu from immutable presentation data.
    /// </summary>
    /// <param name="data">The current save-menu presentation data.</param>
    public void Render(SaveMenuWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        BindControls();
        RenderReturnStrategyButton(data);
        RenderAudioControls(data.MusicVolume, data.SfxVolume);
        SetTextContent(versionTextField, data.VersionText, versionTextColor);
        RenderTacticalOptions(data);
        RenderSaveSlots(data);
        RenderConfirmation(data.ConfirmationMessage);
    }

    /// <summary>
    /// Renders stateful audio controls without rebuilding unrelated save-slot presentation.
    /// </summary>
    /// <param name="musicVolume">The normalized music volume.</param>
    /// <param name="sfxVolume">The normalized sound-effect volume.</param>
    public void RenderAudioSettings(float musicVolume, float sfxVolume)
    {
        VerifySoundReferences();
        VerifyTextReferences();
        RenderAudioControls(musicVolume, sfxVolume);
    }

    /// <summary>
    /// Fits the authored source window within its viewport without stretching it.
    /// </summary>
    /// <param name="contentHost">The authored transform that contains this window.</param>
    public void FitWithinViewport(RectTransform contentHost)
    {
        if (contentHost == null)
            throw new ArgumentNullException(nameof(contentHost));
        if (contentHost.parent is not RectTransform viewport)
            throw new MissingReferenceException("Save Menu content viewport is missing.");
        if (transform is not RectTransform sourceWindow)
            throw new MissingReferenceException("Save Menu source window transform is missing.");

        Vector2 sourceSize = sourceWindow.sizeDelta;
        if (sourceSize.x <= 0f)
            sourceSize.x = sourceWindow.rect.width;
        if (sourceSize.y <= 0f)
            sourceSize.y = sourceWindow.rect.height;
        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
            return;

        Rect viewportRect = viewport.rect;
        float scale =
            viewportRect.width <= 0f || viewportRect.height <= 0f
                ? 1f
                : Mathf.Min(viewportRect.width / sourceSize.x, viewportRect.height / sourceSize.y);

        contentHost.anchorMin = new Vector2(0.5f, 0.5f);
        contentHost.anchorMax = new Vector2(0.5f, 0.5f);
        contentHost.pivot = new Vector2(0.5f, 0.5f);
        contentHost.anchoredPosition = Vector2.zero;
        contentHost.sizeDelta = sourceSize;
        contentHost.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// Verifies every authored reference required by the window and its child components.
    /// </summary>
    public void VerifyReferences()
    {
        VerifyCommandReferences();
        VerifySoundReferences();
        VerifyTextReferences();

        if (confirmDialog == null)
            throw new MissingReferenceException("ConfirmDialog is missing.");
        confirmDialog.VerifyReferences();

        if (
            tacticalOptionRows == null
            || tacticalOptionRows.Length != Enum.GetValues(typeof(UserTacticalOption)).Length
        )
            throw new MissingReferenceException("TacticalOptionRows are incomplete.");
        for (int i = 0; i < tacticalOptionRows.Length; i++)
        {
            if (tacticalOptionRows[i] == null)
                throw new MissingReferenceException($"Tactical option row slot {i} is missing.");
            tacticalOptionRows[i].VerifyReferences();
        }

        if (saveSlotRows == null || saveSlotRows.Length == 0)
            throw new MissingReferenceException("SaveSlotRows are missing.");
        for (int i = 0; i < saveSlotRows.Length; i++)
        {
            if (saveSlotRows[i] == null)
                throw new MissingReferenceException($"SaveSlot{i + 1}Row is missing.");
            saveSlotRows[i].VerifyReferences();
        }
    }

    /// <summary>
    /// Binds the authored hierarchy while the window is active.
    /// </summary>
    private void OnEnable()
    {
        if (ReferencesAssigned())
            BindControls();
    }

    /// <summary>
    /// Removes all event subscriptions while the window is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (!bound)
            return;

        cockpitButton.onClick.RemoveListener(RequestReturnCockpit);
        exitButton.onClick.RemoveListener(RequestExit);
        returnStrategyButton.onClick.RemoveListener(RequestReturnStrategy);
        musicButton.onClick.RemoveListener(RequestMusicToggle);
        musicSlider.ValueChanged -= HandleMusicVolumeChanged;
        sfxSlider.ValueChanged -= HandleSfxVolumeChanged;
        confirmDialog.Confirmed -= HandleConfirmationAccepted;
        confirmDialog.Canceled -= HandleConfirmationCanceled;

        for (int i = 0; i < tacticalOptionRows.Length; i++)
            tacticalOptionRows[i].ToggleRequested -= HandleTacticalOptionToggleRequested;
        for (int i = 0; i < saveSlotRows.Length; i++)
        {
            saveSlotRows[i].SaveRequested -= HandleSaveRequested;
            saveSlotRows[i].LoadRequested -= HandleLoadRequested;
        }

        bound = false;
    }

    /// <summary>
    /// Applies the current faction textures to the return-to-strategy command.
    /// </summary>
    /// <param name="data">The current save-menu presentation data.</param>
    private void RenderReturnStrategyButton(SaveMenuWindowRenderData data)
    {
        Texture2D returnUpTexture =
            data.ReturnStrategyButtonUpTexture ?? returnStrategyButtonUpTexture;
        returnStrategyButtonPressVisual.SetTextures(
            returnUpTexture,
            data.ReturnStrategyButtonDownTexture ?? returnUpTexture
        );
    }

    /// <summary>
    /// Applies audio state to the authored controls and labels.
    /// </summary>
    /// <param name="musicVolume">The normalized music volume.</param>
    /// <param name="sfxVolume">The normalized sound-effect volume.</param>
    private void RenderAudioControls(float musicVolume, float sfxVolume)
    {
        bool musicEnabled = musicVolume > 0f;
        Texture2D musicButtonTexture = musicEnabled ? musicButtonDownTexture : musicButtonUpTexture;
        musicButtonPressVisual.SetTextures(musicButtonTexture, musicButtonTexture);
        musicSlider.Render(musicVolume);
        sfxSlider.Render(sfxVolume);
        SetTextColor(playMusicTextField, musicEnabled ? enabledTextColor : disabledTextColor);
        SetTextContent(
            playMusicStateTextField,
            musicEnabled ? "ON" : "OFF",
            musicEnabled ? enabledTextColor : disabledTextColor
        );
    }

    /// <summary>
    /// Renders each authored tactical-option row from typed state.
    /// </summary>
    /// <param name="data">The current save-menu presentation data.</param>
    private void RenderTacticalOptions(SaveMenuWindowRenderData data)
    {
        for (int i = 0; i < tacticalOptionRows.Length; i++)
        {
            UserTacticalOption option = tacticalOptionRows[i].Option;
            if (!data.TacticalOptions.TryGetValue(option, out bool enabled))
                throw new InvalidOperationException(
                    $"Tactical option {option} has no render state."
                );

            tacticalOptionRows[i].Render(enabled);
        }
    }

    /// <summary>
    /// Renders every authored save row without introducing runtime hierarchy changes.
    /// </summary>
    /// <param name="data">The current save-menu presentation data.</param>
    private void RenderSaveSlots(SaveMenuWindowRenderData data)
    {
        for (int i = 0; i < saveSlotRows.Length; i++)
        {
            SaveSlotRenderData slot =
                i < data.Slots.Count ? data.Slots[i] : SaveSlotRenderData.Empty(i);
            saveSlotRows[i].Render(slot);
        }
    }

    /// <summary>
    /// Shows or hides the authored modal confirmation dialog.
    /// </summary>
    /// <param name="message">The active confirmation message, or null.</param>
    private void RenderConfirmation(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            confirmDialog.Hide();
            return;
        }

        confirmDialog.Show(message);
    }

    /// <summary>
    /// Attaches semantic callbacks to all authored controls exactly once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        cockpitButton.onClick.AddListener(RequestReturnCockpit);
        exitButton.onClick.AddListener(RequestExit);
        returnStrategyButton.onClick.AddListener(RequestReturnStrategy);
        musicButton.onClick.AddListener(RequestMusicToggle);
        musicSlider.ValueChanged += HandleMusicVolumeChanged;
        sfxSlider.ValueChanged += HandleSfxVolumeChanged;
        confirmDialog.Confirmed += HandleConfirmationAccepted;
        confirmDialog.Canceled += HandleConfirmationCanceled;

        for (int i = 0; i < tacticalOptionRows.Length; i++)
            tacticalOptionRows[i].ToggleRequested += HandleTacticalOptionToggleRequested;
        for (int i = 0; i < saveSlotRows.Length; i++)
        {
            saveSlotRows[i].SaveRequested += HandleSaveRequested;
            saveSlotRows[i].LoadRequested += HandleLoadRequested;
        }

        bound = true;
    }

    /// <summary>
    /// Emits the cockpit navigation request.
    /// </summary>
    private void RequestReturnCockpit()
    {
        ReturnCockpitRequested?.Invoke();
    }

    /// <summary>
    /// Emits the request to begin the exit-confirmation flow.
    /// </summary>
    private void RequestExit()
    {
        ExitRequested?.Invoke();
    }

    /// <summary>
    /// Emits the strategy-view navigation request.
    /// </summary>
    private void RequestReturnStrategy()
    {
        ReturnStrategyRequested?.Invoke();
    }

    /// <summary>
    /// Emits the music toggle request.
    /// </summary>
    private void RequestMusicToggle()
    {
        MusicToggleRequested?.Invoke();
    }

    /// <summary>
    /// Forwards a normalized music-volume change.
    /// </summary>
    /// <param name="value">The normalized music volume.</param>
    private void HandleMusicVolumeChanged(float value)
    {
        MusicVolumeChanged?.Invoke(value);
    }

    /// <summary>
    /// Forwards a normalized sound-effect-volume change.
    /// </summary>
    /// <param name="value">The normalized sound-effect volume.</param>
    private void HandleSfxVolumeChanged(float value)
    {
        SfxVolumeChanged?.Invoke(value);
    }

    /// <summary>
    /// Forwards a typed tactical-option request.
    /// </summary>
    /// <param name="option">The requested tactical option.</param>
    private void HandleTacticalOptionToggleRequested(UserTacticalOption option)
    {
        TacticalOptionToggleRequested?.Invoke(option);
    }

    /// <summary>
    /// Forwards a named save request.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    /// <param name="name">The requested save display name.</param>
    private void HandleSaveRequested(int slot, string name)
    {
        SaveRequested?.Invoke(slot, name);
    }

    /// <summary>
    /// Forwards a load request.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    private void HandleLoadRequested(int slot)
    {
        LoadRequested?.Invoke(slot);
    }

    /// <summary>
    /// Forwards confirmation acceptance to the owning controller.
    /// </summary>
    private void HandleConfirmationAccepted()
    {
        ConfirmationAccepted?.Invoke();
    }

    /// <summary>
    /// Forwards confirmation cancellation to the owning controller.
    /// </summary>
    private void HandleConfirmationCanceled()
    {
        ConfirmationCanceled?.Invoke();
    }

    /// <summary>
    /// Applies dynamic text content and color without changing authored typography or geometry.
    /// </summary>
    /// <param name="textField">The authored text field.</param>
    /// <param name="text">The displayed text.</param>
    /// <param name="color">The displayed text color.</param>
    private static void SetTextContent(TextMeshProUGUI textField, string text, Color color)
    {
        textField.text = text ?? string.Empty;
        SetTextColor(textField, color);
    }

    /// <summary>
    /// Applies dynamic color without changing authored text or geometry.
    /// </summary>
    /// <param name="textField">The authored text field.</param>
    /// <param name="color">The displayed text color.</param>
    private static void SetTextColor(TextMeshProUGUI textField, Color color)
    {
        textField.color = color;
    }

    /// <summary>
    /// Verifies navigation-command references required by this coordinating view.
    /// </summary>
    private void VerifyCommandReferences()
    {
        if (cockpitButton == null)
            throw new MissingReferenceException("CockpitButton is missing.");
        if (exitButton == null)
            throw new MissingReferenceException("ExitButton is missing.");
        if (returnStrategyButton == null || returnStrategyButtonPressVisual == null)
            throw new MissingReferenceException(
                "Return strategy button references are incomplete."
            );
        if (returnStrategyButtonUpTexture == null)
            throw new MissingReferenceException("Return strategy button texture is missing.");
    }

    /// <summary>
    /// Verifies sound-control references and state textures.
    /// </summary>
    private void VerifySoundReferences()
    {
        if (musicButtonPressVisual == null || musicButton == null)
            throw new MissingReferenceException("Music button references are incomplete.");
        if (musicButtonUpTexture == null || musicButtonDownTexture == null)
            throw new MissingReferenceException("Music button textures are incomplete.");
        if (musicSlider == null || sfxSlider == null)
            throw new MissingReferenceException("Volume sliders are incomplete.");
        musicSlider.VerifyReferences();
        sfxSlider.VerifyReferences();
    }

    /// <summary>
    /// Verifies every stateful authored text field.
    /// </summary>
    private void VerifyTextReferences()
    {
        if (playMusicTextField == null)
            throw new MissingReferenceException("PlayMusicTextField is missing.");
        if (playMusicStateTextField == null)
            throw new MissingReferenceException("PlayMusicStateTextField is missing.");
        if (versionTextField == null)
            throw new MissingReferenceException("VersionTextField is missing.");
    }

    /// <summary>
    /// Checks whether the prefab has all references needed for early binding.
    /// </summary>
    /// <returns>True when every required authored reference is assigned.</returns>
    private bool ReferencesAssigned()
    {
        if (
            cockpitButton == null
            || exitButton == null
            || returnStrategyButton == null
            || returnStrategyButtonPressVisual == null
            || returnStrategyButtonUpTexture == null
            || musicButtonPressVisual == null
            || musicButton == null
            || musicButtonUpTexture == null
            || musicButtonDownTexture == null
            || musicSlider == null
            || sfxSlider == null
            || confirmDialog == null
            || playMusicTextField == null
            || playMusicStateTextField == null
            || versionTextField == null
        )
        {
            return false;
        }

        if (
            tacticalOptionRows == null
            || tacticalOptionRows.Length != Enum.GetValues(typeof(UserTacticalOption)).Length
            || saveSlotRows == null
            || saveSlotRows.Length == 0
        )
        {
            return false;
        }

        for (int i = 0; i < tacticalOptionRows.Length; i++)
        {
            if (tacticalOptionRows[i] == null)
                return false;
        }

        for (int i = 0; i < saveSlotRows.Length; i++)
        {
            if (saveSlotRows[i] == null)
                return false;
        }

        return true;
    }
}
