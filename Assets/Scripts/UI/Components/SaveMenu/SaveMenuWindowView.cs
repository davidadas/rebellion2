using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SaveMenuWindowView : MonoBehaviour
{
    private const int _slotCount = 6;
    private const int _tacticalOptionCount = 5;
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 EnabledGreen = new Color32(117, 251, 76, 255);
    private static readonly Color32 DisabledGreen = new Color32(62, 139, 38, 255);
    private static readonly Color32 Black = new Color32(0, 0, 0, 255);

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage cockpitButtonImage;

    [SerializeField]
    private RawImage airlockButtonImage;

    [SerializeField]
    private RawImage returnStrategyButtonImage;

    [SerializeField]
    private RawImage musicButtonImage;

    [SerializeField]
    private RawImage musicSliderImage;

    [SerializeField]
    private RawImage sfxSliderImage;

    [SerializeField]
    private Button cockpitButton;

    [SerializeField]
    private Button airlockButton;

    [SerializeField]
    private Button returnStrategyButton;

    [SerializeField]
    private Button musicButton;

    [SerializeField]
    private Slider musicSlider;

    [SerializeField]
    private Slider sfxSlider;

    [SerializeField]
    private RawImage[] tacticalOptionButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] saveButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] loadButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] saveSlotFactionImages = Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tacticalOptionButtons = Array.Empty<Button>();

    [SerializeField]
    private Button[] saveButtons = Array.Empty<Button>();

    [SerializeField]
    private Button[] loadButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI savedGamesTitleTextField;

    [SerializeField]
    private TextMeshProUGUI soundOptionsTitleTextField;

    [SerializeField]
    private TextMeshProUGUI tacticalOptionsTitleTextField;

    [SerializeField]
    private TextMeshProUGUI playMusicTextField;

    [SerializeField]
    private TextMeshProUGUI playMusicStateTextField;

    [SerializeField]
    private TextMeshProUGUI[] tacticalOptionTextFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI[] tacticalOptionStateTextFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI[] saveSlotTextFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private TextMeshProUGUI versionTextField;

    [SerializeField]
    private Texture2D cockpitButtonUpTexture;

    [SerializeField]
    private Texture2D airlockButtonUpTexture;

    [SerializeField]
    private Texture2D musicButtonUpTexture;

    [SerializeField]
    private Texture2D musicButtonDownTexture;

    [SerializeField]
    private Texture2D optionButtonUpTexture;

    [SerializeField]
    private Texture2D optionButtonDownTexture;

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

    [SerializeField]
    private Texture2D sliderThumbTexture;

    private readonly bool[] tacticalOptions = { true, true, true, true, true };

    private SaveMenuWindowRenderData lastData;
    private bool musicEnabled;
    private int pressedSaveSlot = -1;
    private int pressedLoadSlot = -1;

    public event Action<SaveMenuWindowCommandRequest> CommandRequested;
    public event Action<SaveMenuWindowView> RenderRequested;

    public void Render(SaveMenuWindowRenderData data)
    {
        VerifyReferences();
        lastData = data;

        RectTransform rect = transform as RectTransform;
        UILayout.SetSourcePosition(rect, data.X, data.Y);
        musicEnabled = data.MusicVolume > 0f;

        SetImageAtTemplateOrigin(backgroundImage, backgroundImage.texture as Texture2D);
        UILayout.SetInteractiveImageTexture(cockpitButtonImage, cockpitButtonUpTexture);
        UILayout.SetInteractiveImageTexture(airlockButtonImage, airlockButtonUpTexture);
        UILayout.SetInteractiveImageTexture(
            returnStrategyButtonImage,
            data.ReturnStrategyButtonUpTexture ?? returnStrategyButtonImage.texture as Texture2D
        );
        UILayout.SetInteractiveImageTexture(
            musicButtonImage,
            musicEnabled ? musicButtonDownTexture : musicButtonUpTexture
        );
        UILayout.SetInteractiveImageTexture(musicSliderImage, sliderThumbTexture);
        UILayout.SetInteractiveImageTexture(sfxSliderImage, sliderThumbTexture);
        SetSliderValue(musicSlider, musicSliderImage, data.MusicVolume);
        SetSliderValue(sfxSlider, sfxSliderImage, data.SfxVolume);

        RenderStaticText(data);
        RenderTacticalOptions();
        RenderSaveSlots(data);
        gameObject.SetActive(true);
    }

    private void RenderStaticText(SaveMenuWindowRenderData data)
    {
        SetText(savedGamesTitleTextField, "Saved Games", EnabledGreen);
        SetText(soundOptionsTitleTextField, "Sound Options", EnabledGreen);
        SetText(tacticalOptionsTitleTextField, "Tactical Options", EnabledGreen);
        SetText(playMusicTextField, "Play Music", musicEnabled ? EnabledGreen : DisabledGreen);
        SetText(
            playMusicStateTextField,
            musicEnabled ? "ON" : "OFF",
            musicEnabled ? EnabledGreen : DisabledGreen
        );
        SetText(versionTextField, data.VersionText, Black);
    }

    private void RenderTacticalOptions()
    {
        string[] labels =
        {
            "Show Starfield",
            "Show Planet",
            "Show Pyro",
            "High Detail",
            "Show Holocube",
        };

        for (int i = 0; i < _tacticalOptionCount; i++)
        {
            bool enabled = tacticalOptions[i];
            UILayout.SetInteractiveImageTexture(
                tacticalOptionButtonImages[i],
                enabled ? optionButtonDownTexture : optionButtonUpTexture
            );
            SetText(tacticalOptionTextFields[i], labels[i], enabled ? EnabledGreen : DisabledGreen);
            SetText(
                tacticalOptionStateTextFields[i],
                enabled ? "ON" : "OFF",
                enabled ? EnabledGreen : DisabledGreen
            );
        }
    }

    private void RenderSaveSlots(SaveMenuWindowRenderData data)
    {
        IReadOnlyList<SaveSlotRenderData> slots = data.Slots ?? Array.Empty<SaveSlotRenderData>();
        for (int i = 0; i < _slotCount; i++)
        {
            SaveSlotRenderData slot = i < slots.Count ? slots[i] : SaveSlotRenderData.Empty(i);
            SetOptionalImage(saveSlotFactionImages[i], slot.FactionIconTexture);
            UILayout.SetInteractiveImageTexture(
                saveButtonImages[i],
                GetSaveButtonTexture(slot.CanSave, pressedSaveSlot == i)
            );
            UILayout.SetInteractiveImageTexture(
                loadButtonImages[i],
                GetLoadButtonTexture(slot.CanLoad, pressedLoadSlot == i)
            );
            saveButtons[i].interactable = slot.CanSave;
            loadButtons[i].interactable = slot.CanLoad;
            SetText(saveSlotTextFields[i], slot.Label, White);
        }
    }

    private Texture2D GetSaveButtonTexture(bool enabled, bool pressed)
    {
        if (!enabled)
            return saveButtonDisabledTexture;

        return pressed ? saveButtonDownTexture ?? saveButtonUpTexture : saveButtonUpTexture;
    }

    private Texture2D GetLoadButtonTexture(bool enabled, bool pressed)
    {
        if (!enabled)
            return loadButtonDisabledTexture;

        return pressed ? loadButtonDownTexture ?? loadButtonUpTexture : loadButtonUpTexture;
    }

    private void SendCommand(
        SaveMenuWindowCommand command,
        int slot = -1,
        int tacticalOption = -1,
        float value = 0f
    )
    {
        CommandRequested?.Invoke(
            new SaveMenuWindowCommandRequest(this, command, slot, tacticalOption, value)
        );
    }

    private void ReturnCockpit()
    {
        SendCommand(SaveMenuWindowCommand.ReturnCockpit);
    }

    private void Airlock()
    {
        SendCommand(SaveMenuWindowCommand.Airlock);
    }

    private void ReturnStrategy()
    {
        SendCommand(SaveMenuWindowCommand.ReturnStrategy);
    }

    private void ToggleMusic()
    {
        musicEnabled = !musicEnabled;
        SendCommand(SaveMenuWindowCommand.ToggleMusic);
        RequestRender();
    }

    private void ToggleTacticalOption(int option)
    {
        if (option < 0 || option >= tacticalOptions.Length)
            return;

        tacticalOptions[option] = !tacticalOptions[option];
        SendCommand(SaveMenuWindowCommand.ToggleTacticalOption, tacticalOption: option);
        RequestRender();
    }

    private void SaveSlot(int slot)
    {
        if (!IsSlotEnabled(slot, true))
            return;

        SendCommand(SaveMenuWindowCommand.SaveSlot, slot);
    }

    private void LoadSlot(int slot)
    {
        if (!IsSlotEnabled(slot, false))
            return;

        SendCommand(SaveMenuWindowCommand.LoadSlot, slot);
    }

    private void SetMusicVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        SetSliderValue(musicSlider, musicSliderImage, volume);
        SendCommand(SaveMenuWindowCommand.SetMusicVolume, value: volume);
    }

    private void SetSfxVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        SetSliderValue(sfxSlider, sfxSliderImage, volume);
        SendCommand(SaveMenuWindowCommand.SetSfxVolume, value: volume);
    }

    private bool IsSlotEnabled(int slot, bool save)
    {
        if (
            slot < 0
            || slot >= _slotCount
            || lastData?.Slots == null
            || slot >= lastData.Slots.Count
        )
            return false;

        SaveSlotRenderData data = lastData.Slots[slot];
        return save ? data.CanSave : data.CanLoad;
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture2D texture)
    {
        if (image == null)
            return;

        image.texture = texture;
    }

    private static void SetOptionalImage(RawImage image, Texture2D texture)
    {
        if (image == null)
            return;

        image.texture = texture;
        image.enabled = texture != null;
        image.raycastTarget = false;
    }

    private static void SetText(TextMeshProUGUI textField, string text, Color color)
    {
        textField.text = text ?? string.Empty;
        textField.color = color;
        textField.gameObject.SetActive(true);
    }

    private static void SetSliderValue(Slider slider, RawImage thumb, float value)
    {
        float normalizedValue = Mathf.Clamp01(value);
        slider.SetValueWithoutNotify(normalizedValue);
        SetSliderThumbPosition(slider, thumb, normalizedValue);
    }

    private static void SetSliderThumbPosition(Slider slider, RawImage thumb, float value)
    {
        RectTransform sliderRect = slider.transform as RectTransform;
        RectTransform thumbRect = thumb.rectTransform;
        int sliderWidth = GetRectWidth(sliderRect);
        int thumbWidth = GetRectWidth(thumbRect);
        int thumbHeight = GetRectHeight(thumbRect);
        int thumbX = Mathf.RoundToInt(
            Mathf.Clamp01(value) * Mathf.Max(0, sliderWidth - thumbWidth)
        );
        UILayout.SetSourceRect(thumbRect, thumbX, 0, thumbWidth, thumbHeight);
    }

    private static int GetRectWidth(RectTransform rect)
    {
        int width = Mathf.RoundToInt(rect.rect.width);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.sizeDelta.x);

        return Mathf.Max(0, width);
    }

    private static int GetRectHeight(RectTransform rect)
    {
        int height = Mathf.RoundToInt(rect.rect.height);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.sizeDelta.y);

        return Mathf.Max(0, height);
    }

    private void RequestRender()
    {
        RenderRequested?.Invoke(this);
    }

    private void Awake()
    {
        VerifyReferences();
        BindControls();
    }

    private void BindControls()
    {
        cockpitButton.onClick.AddListener(ReturnCockpit);
        airlockButton.onClick.AddListener(Airlock);
        returnStrategyButton.onClick.AddListener(ReturnStrategy);
        musicButton.onClick.AddListener(ToggleMusic);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);

        for (int i = 0; i < tacticalOptionButtons.Length; i++)
        {
            int option = i;
            tacticalOptionButtons[i].onClick.AddListener(() => ToggleTacticalOption(option));
        }

        for (int i = 0; i < saveButtons.Length; i++)
        {
            int slot = i;
            saveButtons[i].onClick.AddListener(() => SaveSlot(slot));
            BindButtonPress(saveButtons[i], () => PressSaveSlot(slot), () => ReleaseSaveSlot(slot));
        }

        for (int i = 0; i < loadButtons.Length; i++)
        {
            int slot = i;
            loadButtons[i].onClick.AddListener(() => LoadSlot(slot));
            BindButtonPress(loadButtons[i], () => PressLoadSlot(slot), () => ReleaseLoadSlot(slot));
        }
    }

    private void PressSaveSlot(int slot)
    {
        if (!IsSlotEnabled(slot, true))
            return;

        pressedSaveSlot = slot;
        RenderSaveSlots(lastData);
    }

    private void ReleaseSaveSlot(int slot)
    {
        if (pressedSaveSlot != slot)
            return;

        pressedSaveSlot = -1;
        RenderSaveSlots(lastData);
    }

    private void PressLoadSlot(int slot)
    {
        if (!IsSlotEnabled(slot, false))
            return;

        pressedLoadSlot = slot;
        RenderSaveSlots(lastData);
    }

    private void ReleaseLoadSlot(int slot)
    {
        if (pressedLoadSlot != slot)
            return;

        pressedLoadSlot = -1;
        RenderSaveSlots(lastData);
    }

    private static void BindButtonPress(Button button, Action onDown, Action onUp)
    {
        AddPointerEvent(button, EventTriggerType.PointerDown, onDown);
        AddPointerEvent(button, EventTriggerType.PointerUp, onUp);
        AddPointerEvent(button, EventTriggerType.PointerExit, onUp);
    }

    private static void AddPointerEvent(Button button, EventTriggerType eventType, Action callback)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new InvalidOperationException("BackgroundImage is not assigned.");
        if (cockpitButtonImage == null)
            throw new InvalidOperationException("CockpitButtonImage is not assigned.");
        if (airlockButtonImage == null)
            throw new InvalidOperationException("AirlockButtonImage is not assigned.");
        if (returnStrategyButtonImage == null)
            throw new InvalidOperationException("ReturnStrategyButtonImage is not assigned.");
        if (musicButtonImage == null)
            throw new InvalidOperationException("MusicButtonImage is not assigned.");
        if (musicSliderImage == null)
            throw new InvalidOperationException("MusicSliderImage is not assigned.");
        if (sfxSliderImage == null)
            throw new InvalidOperationException("SfxSliderImage is not assigned.");
        if (cockpitButton == null)
            throw new InvalidOperationException("CockpitButton is not assigned.");
        if (airlockButton == null)
            throw new InvalidOperationException("AirlockButton is not assigned.");
        if (returnStrategyButton == null)
            throw new InvalidOperationException("ReturnStrategyButton is not assigned.");
        if (musicButton == null)
            throw new InvalidOperationException("MusicButton is not assigned.");
        if (musicSlider == null)
            throw new InvalidOperationException("MusicSlider is not assigned.");
        if (sfxSlider == null)
            throw new InvalidOperationException("SfxSlider is not assigned.");
        if (
            tacticalOptionButtonImages == null
            || tacticalOptionButtonImages.Length != _tacticalOptionCount
        )
            throw new InvalidOperationException("TacticalOptionButtonImages are not assigned.");
        if (saveButtonImages == null || saveButtonImages.Length != _slotCount)
            throw new InvalidOperationException("SaveButtonImages are not assigned.");
        if (loadButtonImages == null || loadButtonImages.Length != _slotCount)
            throw new InvalidOperationException("LoadButtonImages are not assigned.");
        if (saveSlotFactionImages == null || saveSlotFactionImages.Length != _slotCount)
            throw new InvalidOperationException("SaveSlotFactionImages are not assigned.");
        if (tacticalOptionButtons == null || tacticalOptionButtons.Length != _tacticalOptionCount)
            throw new InvalidOperationException("TacticalOptionButtons are not assigned.");
        if (saveButtons == null || saveButtons.Length != _slotCount)
            throw new InvalidOperationException("SaveButtons are not assigned.");
        if (loadButtons == null || loadButtons.Length != _slotCount)
            throw new InvalidOperationException("LoadButtons are not assigned.");
        if (saveSlotTextFields == null || saveSlotTextFields.Length != _slotCount)
            throw new InvalidOperationException("SaveSlotTextFields are not assigned.");
        if (
            tacticalOptionTextFields == null
            || tacticalOptionTextFields.Length != _tacticalOptionCount
        )
            throw new InvalidOperationException("TacticalOptionTextFields are not assigned.");
        if (
            tacticalOptionStateTextFields == null
            || tacticalOptionStateTextFields.Length != _tacticalOptionCount
        )
            throw new InvalidOperationException("TacticalOptionStateTextFields are not assigned.");
    }
}

public enum SaveMenuWindowCommand
{
    None,
    SaveSlot,
    LoadSlot,
    ReturnStrategy,
    ReturnCockpit,
    Airlock,
    ToggleMusic,
    ToggleTacticalOption,
    SetMusicVolume,
    SetSfxVolume,
}

public sealed class SaveMenuWindowRenderData
{
    public int X { get; set; }
    public int Y { get; set; }
    public Texture2D ReturnStrategyButtonUpTexture { get; set; }
    public float MusicVolume { get; set; }
    public float SfxVolume { get; set; }
    public string VersionText { get; set; }
    public IReadOnlyList<SaveSlotRenderData> Slots { get; set; }
}

public sealed class SaveMenuWindowCommandRequest
{
    public SaveMenuWindowCommandRequest(
        SaveMenuWindowView source,
        SaveMenuWindowCommand command,
        int slot,
        int tacticalOption,
        float value
    )
    {
        Source = source;
        Command = command;
        Slot = slot;
        TacticalOption = tacticalOption;
        Value = value;
    }

    public SaveMenuWindowView Source { get; }
    public SaveMenuWindowCommand Command { get; }
    public int Slot { get; }
    public int TacticalOption { get; }
    public float Value { get; }
}

public sealed class SaveSlotRenderData
{
    public int Slot { get; set; }
    public string Label { get; set; }
    public bool CanSave { get; set; }
    public bool CanLoad { get; set; }
    public Texture2D FactionIconTexture { get; set; }

    public static SaveSlotRenderData Empty(int slot)
    {
        return new SaveSlotRenderData
        {
            Slot = slot,
            Label = "Empty Save Slot",
            CanSave = false,
            CanLoad = false,
        };
    }
}
