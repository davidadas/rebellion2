using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the Save Menu window, reusable row controls, sliders, and scene root prefab.
/// </summary>
public static class SaveMenuPrefabBuilder
{
    private const string _saveMenuWindowPrefabPath = "Assets/Prefabs/UI/SaveMenuWindow.prefab";
    private const string _saveMenuRootPrefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuRoot.prefab";
    private const string _saveSlotRowPrefabPath = "Assets/Prefabs/UI/SaveMenu/SaveSlotRow.prefab";
    private const string _tacticalOptionRowPrefabPath =
        "Assets/Prefabs/UI/SaveMenu/SaveMenuTacticalOptionRow.prefab";
    private const string _sliderPrefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuSlider.prefab";
    private const string _commonTextInputPrefabPath = "Assets/Prefabs/UI/Common/TextInput.prefab";

    private const string _backgroundTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_background.png";
    private const string _cockpitButtonTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_cockpit_button.png";
    private const string _cockpitButtonPressedTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_cockpit_button_pressed.png";
    private const string _exitButtonTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_airlock_button.png";
    private const string _exitButtonPressedTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_airlock_button_pressed.png";
    private const string _musicButtonTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_music_button.png";
    private const string _musicButtonPressedTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_music_button_pressed.png";
    private const string _optionButtonTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_option_button.png";
    private const string _optionButtonPressedTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_option_button_pressed.png";
    private const string _saveButtonTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_save_button.png";
    private const string _saveButtonPressedTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_save_button_pressed.png";
    private const string _saveButtonDisabledTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_save_button_disabled.png";
    private const string _loadButtonTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_load_button.png";
    private const string _loadButtonPressedTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_load_button_pressed.png";
    private const string _loadButtonDisabledTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_load_button_disabled.png";
    private const string _sliderThumbTexturePath =
        "Assets/Resources/Art/HD/UI/SaveMenu/ui_savemenu_slider_thumb.png";
    private const string _confirmationDialogTexturePath =
        "Assets/Resources/Art/HD/UI/Common/ui_common_confirmation_dialog.png";
    private const string _confirmationYesTexturePath =
        "Assets/Resources/Art/HD/UI/Common/ui_common_confirmation_yes_button.png";
    private const string _confirmationYesPressedTexturePath =
        "Assets/Resources/Art/HD/UI/Common/ui_common_confirmation_yes_button_pressed.png";
    private const string _confirmationNoTexturePath =
        "Assets/Resources/Art/HD/UI/Common/ui_common_confirmation_no_button.png";
    private const string _confirmationNoPressedTexturePath =
        "Assets/Resources/Art/HD/UI/Common/ui_common_confirmation_no_button_pressed.png";

    private const int _windowWidth = 640;
    private const int _windowHeight = 480;
    private const int _saveSlotCount = 6;
    private const int _sliderWidth = 189;

    private static readonly Color32 _enabledTextColor = new Color32(117, 251, 76, 255);
    private static readonly Color32 _disabledTextColor = new Color32(62, 139, 38, 255);
    private static readonly Color32 _whiteTextColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 _versionTextColor = new Color32(0, 0, 0, 255);

    /// <summary>
    /// Rebuilds every Save Menu prefab in dependency order.
    /// </summary>
    [MenuItem("Rebellion/Save Menu/Rebuild Save Menu Prefabs")]
    public static void RebuildAllSaveMenuPrefabs()
    {
        UIAuthoringGuard.EnsureEditMode();
        CommonUIPrefabBuilder.RebuildSharedControlPrefabs();
        GameObject slotRowPrefab = BuildSaveSlotRowPrefab();
        GameObject tacticalRowPrefab = BuildTacticalOptionRowPrefab();
        GameObject sliderPrefab = BuildSliderPrefab();
        GameObject windowPrefab = BuildSaveMenuWindowPrefab(
            slotRowPrefab,
            tacticalRowPrefab,
            sliderPrefab
        );
        BuildSaveMenuRootPrefab(windowPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Authors one reusable save-slot row with nested shared text input.
    /// </summary>
    /// <returns>The generated save-slot row prefab asset.</returns>
    private static GameObject BuildSaveSlotRowPrefab()
    {
        GameObject root = CreateRectObject("SaveSlotRow");
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, 294, 42);
        SaveMenuSlotRowView view = root.AddComponent<SaveMenuSlotRowView>();
        view.enabled = true;

        RawImage saveButtonImage = CreateRawImage(
            "SaveButtonImage",
            root.transform,
            _saveButtonDisabledTexturePath,
            0,
            0
        );
        Button saveButton = CreatePressableButton(
            saveButtonImage,
            LoadRequiredTexture(_saveButtonTexturePath),
            LoadRequiredTexture(_saveButtonPressedTexturePath),
            out RawImagePressVisual saveButtonPressVisual
        );
        RawImage factionImage = CreateRawImage(
            "FactionImage",
            root.transform,
            ResolvePreviewTheme().SaveMenuSlotIconImagePath,
            51,
            1,
            26,
            19
        );
        factionImage.raycastTarget = false;

        TMP_InputField nameInput = CreateSaveNameInput(root.transform);

        RawImage loadButtonImage = CreateRawImage(
            "LoadButtonImage",
            root.transform,
            _loadButtonDisabledTexturePath,
            253,
            0
        );
        Button loadButton = CreatePressableButton(
            loadButtonImage,
            LoadRequiredTexture(_loadButtonTexturePath),
            LoadRequiredTexture(_loadButtonPressedTexturePath),
            out RawImagePressVisual loadButtonPressVisual
        );

        AssignReference(view, "factionImage", factionImage);
        AssignReference(view, "saveButton", saveButton);
        AssignReference(view, "loadButton", loadButton);
        AssignReference(view, "saveButtonPressVisual", saveButtonPressVisual);
        AssignReference(view, "loadButtonPressVisual", loadButtonPressVisual);
        AssignReference(view, "nameInputField", nameInput);
        AssignReference(view, "saveButtonUpTexture", LoadRequiredTexture(_saveButtonTexturePath));
        AssignReference(
            view,
            "saveButtonDownTexture",
            LoadRequiredTexture(_saveButtonPressedTexturePath)
        );
        AssignReference(
            view,
            "saveButtonDisabledTexture",
            LoadRequiredTexture(_saveButtonDisabledTexturePath)
        );
        AssignReference(view, "loadButtonUpTexture", LoadRequiredTexture(_loadButtonTexturePath));
        AssignReference(
            view,
            "loadButtonDownTexture",
            LoadRequiredTexture(_loadButtonPressedTexturePath)
        );
        AssignReference(
            view,
            "loadButtonDisabledTexture",
            LoadRequiredTexture(_loadButtonDisabledTexturePath)
        );
        saveButtonImage.texture = LoadRequiredTexture(_saveButtonDisabledTexturePath);
        loadButtonImage.texture = LoadRequiredTexture(_loadButtonDisabledTexturePath);

        return SavePrefab(root, _saveSlotRowPrefabPath);
    }

    /// <summary>
    /// Authors the reusable tactical-option row hierarchy.
    /// </summary>
    /// <returns>The generated tactical-option row prefab asset.</returns>
    private static GameObject BuildTacticalOptionRowPrefab()
    {
        GameObject root = CreateRectObject("SaveMenuTacticalOptionRow");
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, 243, 27);
        SaveMenuTacticalOptionRowView view = root.AddComponent<SaveMenuTacticalOptionRowView>();
        view.enabled = true;

        RawImage buttonImage = CreateRawImage(
            "ButtonImage",
            root.transform,
            _optionButtonPressedTexturePath,
            0,
            0
        );
        Texture2D initialButtonTexture = LoadRequiredTexture(_optionButtonPressedTexturePath);
        Button button = CreatePressableButton(
            buttonImage,
            initialButtonTexture,
            initialButtonTexture,
            out RawImagePressVisual buttonPressVisual
        );
        TextMeshProUGUI label = CreateSaveMenuText(
            "LabelTextField",
            root.transform,
            "Show Starfield",
            38,
            9,
            160,
            18,
            13,
            TextAlignmentOptions.BaselineLeft
        );
        TextMeshProUGUI state = CreateSaveMenuText(
            "StateTextField",
            root.transform,
            "ON",
            168,
            9,
            75,
            18,
            13,
            TextAlignmentOptions.BaselineRight
        );

        AssignInt(view, "option", (int)SaveMenuTacticalOption.Starfield);
        AssignColor(view, "enabledTextColor", _enabledTextColor);
        AssignColor(view, "disabledTextColor", _disabledTextColor);
        AssignReference(view, "buttonPressVisual", buttonPressVisual);
        AssignReference(view, "button", button);
        AssignReference(view, "labelTextField", label);
        AssignReference(view, "stateTextField", state);
        AssignReference(view, "disabledTexture", LoadRequiredTexture(_optionButtonTexturePath));
        AssignReference(
            view,
            "enabledTexture",
            LoadRequiredTexture(_optionButtonPressedTexturePath)
        );

        return SavePrefab(root, _tacticalOptionRowPrefabPath);
    }

    /// <summary>
    /// Authors the reusable Save Menu volume slider hierarchy.
    /// </summary>
    /// <returns>The generated slider prefab asset.</returns>
    private static GameObject BuildSliderPrefab()
    {
        Texture2D thumbTexture = LoadRequiredTexture(_sliderThumbTexturePath);
        Vector2Int thumbSize = UILayout.GetTextureSourceSize(thumbTexture);
        GameObject root = new GameObject(
            "SaveMenuSlider",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider),
            typeof(SaveMenuSliderView)
        );
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, _sliderWidth, thumbSize.y);

        Image hitArea = root.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        RectTransform fillArea = CreateRectObject("FillArea", root.transform)
            .GetComponent<RectTransform>();
        FillParent(fillArea);
        RawImage thumbImage = CreateRawImage(
            "ThumbImage",
            root.transform,
            _sliderThumbTexturePath,
            0,
            0,
            thumbSize.x,
            thumbSize.y
        );
        thumbImage.raycastTarget = true;

        Slider slider = root.GetComponent<Slider>();
        slider.enabled = true;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillArea;
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.transition = Selectable.Transition.None;

        SaveMenuSliderView view = root.GetComponent<SaveMenuSliderView>();
        view.enabled = true;
        AssignReference(view, "slider", slider);
        AssignReference(view, "thumbImage", thumbImage);

        return SavePrefab(root, _sliderPrefabPath);
    }

    /// <summary>
    /// Authors the complete Save Menu window using nested reusable controls.
    /// </summary>
    /// <param name="slotRowPrefab">The generated save-slot row prefab.</param>
    /// <param name="tacticalRowPrefab">The generated tactical-option row prefab.</param>
    /// <param name="sliderPrefab">The generated slider prefab.</param>
    /// <returns>The generated Save Menu window prefab asset.</returns>
    private static GameObject BuildSaveMenuWindowPrefab(
        GameObject slotRowPrefab,
        GameObject tacticalRowPrefab,
        GameObject sliderPrefab
    )
    {
        GameObject window = new GameObject(
            "SaveMenuWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        UIWindow uiWindow = window.GetComponent<UIWindow>();
        ConfigureWindow(uiWindow);
        SaveMenuWindowView view = window.AddComponent<SaveMenuWindowView>();
        view.enabled = true;
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, _windowWidth, _windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _backgroundTexturePath,
            0,
            0,
            _windowWidth,
            _windowHeight
        );
        background.raycastTarget = true;

        Button cockpitButton = CreateWindowCommandButton(
            "CockpitButtonImage",
            window.transform,
            _cockpitButtonTexturePath,
            _cockpitButtonPressedTexturePath,
            76,
            381,
            out _,
            out _
        );
        Button exitButton = CreateWindowCommandButton(
            "ExitButtonImage",
            window.transform,
            _exitButtonTexturePath,
            _exitButtonPressedTexturePath,
            248,
            381,
            out _,
            out _
        );

        FactionTheme previewTheme = ResolvePreviewTheme();
        Button returnStrategyButton = CreateWindowCommandButton(
            "ReturnStrategyButtonImage",
            window.transform,
            previewTheme.SaveMenuReturnStrategyButtonImagePath,
            previewTheme.SaveMenuReturnStrategyButtonPressedImagePath,
            162,
            382,
            out RawImage returnStrategyButtonImage,
            out RawImagePressVisual returnStrategyButtonPressVisual
        );
        SetSourceRect(returnStrategyButtonImage.rectTransform, 162, 382, 42, 42);

        RawImage musicButtonImage = CreateRawImage(
            "MusicButtonImage",
            window.transform,
            _musicButtonPressedTexturePath,
            352,
            76
        );
        Texture2D initialMusicButtonTexture = LoadRequiredTexture(_musicButtonPressedTexturePath);
        Button musicButton = CreatePressableButton(
            musicButtonImage,
            initialMusicButtonTexture,
            initialMusicButtonTexture,
            out RawImagePressVisual musicButtonPressVisual
        );

        SaveMenuSliderView musicSlider = InstantiateNested<SaveMenuSliderView>(
            sliderPrefab,
            window.transform,
            "MusicSlider"
        );
        SetSourcePosition(musicSlider.transform as RectTransform, 393, 134);
        SaveMenuSliderView sfxSlider = InstantiateNested<SaveMenuSliderView>(
            sliderPrefab,
            window.transform,
            "SfxSlider"
        );
        SetSourcePosition(sfxSlider.transform as RectTransform, 393, 194);

        SaveMenuSlotRowView[] saveSlotRows = CreateSaveSlotRows(slotRowPrefab, window.transform);
        SaveMenuTacticalOptionRowView[] tacticalRows = CreateTacticalRows(
            tacticalRowPrefab,
            window.transform
        );

        CreateSaveMenuText(
            "SavedGamesTitleTextField",
            window.transform,
            "Saved Games",
            90,
            46,
            180,
            18,
            14,
            TextAlignmentOptions.Baseline
        );
        CreateSaveMenuText(
            "SoundOptionsTitleTextField",
            window.transform,
            "Sound Options",
            400,
            46,
            180,
            18,
            14,
            TextAlignmentOptions.Baseline
        );
        CreateSaveMenuText(
            "TacticalOptionsTitleTextField",
            window.transform,
            "Tactical Options",
            400,
            281,
            180,
            18,
            14,
            TextAlignmentOptions.Baseline
        );
        TextMeshProUGUI playMusic = CreateSaveMenuText(
            "PlayMusicTextField",
            window.transform,
            "Play Music",
            377,
            96,
            160,
            18,
            16,
            TextAlignmentOptions.BaselineLeft
        );
        TextMeshProUGUI playMusicState = CreateSaveMenuText(
            "PlayMusicStateTextField",
            window.transform,
            "ON",
            532,
            96,
            75,
            18,
            16,
            TextAlignmentOptions.BaselineRight
        );
        TextMeshProUGUI version = CreateSaveMenuText(
            "VersionTextField",
            window.transform,
            "Version: Development",
            90,
            445,
            180,
            12,
            9,
            TextAlignmentOptions.Baseline
        );
        version.color = _versionTextColor;

        ConfirmationDialogView confirmationDialog = CreateConfirmationDialog(window.transform);

        AssignColor(view, "enabledTextColor", _enabledTextColor);
        AssignColor(view, "disabledTextColor", _disabledTextColor);
        AssignColor(view, "versionTextColor", _versionTextColor);
        AssignReference(view, "cockpitButton", cockpitButton);
        AssignReference(view, "exitButton", exitButton);
        AssignReference(view, "returnStrategyButton", returnStrategyButton);
        AssignReference(view, "returnStrategyButtonPressVisual", returnStrategyButtonPressVisual);
        AssignReference(
            view,
            "returnStrategyButtonUpTexture",
            LoadRequiredTexture(previewTheme.SaveMenuReturnStrategyButtonImagePath)
        );
        AssignReference(view, "musicButtonPressVisual", musicButtonPressVisual);
        AssignReference(view, "musicButton", musicButton);
        AssignReference(view, "musicButtonUpTexture", LoadRequiredTexture(_musicButtonTexturePath));
        AssignReference(
            view,
            "musicButtonDownTexture",
            LoadRequiredTexture(_musicButtonPressedTexturePath)
        );
        AssignReference(view, "musicSlider", musicSlider);
        AssignReference(view, "sfxSlider", sfxSlider);
        AssignReferenceArray(view, "tacticalOptionRows", tacticalRows);
        AssignReferenceArray(view, "saveSlotRows", saveSlotRows);
        AssignReference(view, "playMusicTextField", playMusic);
        AssignReference(view, "playMusicStateTextField", playMusicState);
        AssignReference(view, "versionTextField", version);
        AssignReference(view, "confirmationDialog", confirmationDialog);

        return SavePrefab(window, _saveMenuWindowPrefabPath);
    }

    /// <summary>
    /// Authors the full-screen Save Menu scene root around the nested window prefab.
    /// </summary>
    /// <param name="windowPrefab">The generated Save Menu window prefab.</param>
    /// <returns>The generated Save Menu root prefab asset.</returns>
    private static GameObject BuildSaveMenuRootPrefab(GameObject windowPrefab)
    {
        GameObject root = CreateRectObject("SaveMenuRoot");
        FillParent(root.GetComponent<RectTransform>());
        SaveMenuSceneController controller = root.AddComponent<SaveMenuSceneController>();
        controller.enabled = true;

        GameObject blockerObject = CreateRectObject("BackgroundBlocker", root.transform);
        blockerObject.AddComponent<CanvasRenderer>();
        RawImage blocker = blockerObject.AddComponent<RawImage>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;
        FillParent(blocker.rectTransform);

        RectTransform contentHost = CreateRectObject("ContentHost", root.transform)
            .GetComponent<RectTransform>();
        SetCenteredRect(contentHost, _windowWidth, _windowHeight);

        SaveMenuWindowView saveMenuWindow = InstantiateNested<SaveMenuWindowView>(
            windowPrefab,
            contentHost,
            "SaveMenuWindow"
        );
        SetSourceRect(saveMenuWindow.transform as RectTransform, 0, 0, _windowWidth, _windowHeight);

        AssignReference(controller, "contentHost", contentHost);
        AssignReference(controller, "saveMenuWindow", saveMenuWindow);
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        return SavePrefab(root, _saveMenuRootPrefabPath);
    }

    /// <summary>
    /// Creates all nested save-slot row instances at their authored positions.
    /// </summary>
    /// <param name="rowPrefab">The generated save-slot row prefab.</param>
    /// <param name="parent">The Save Menu window transform.</param>
    /// <returns>The ordered save-slot row instances.</returns>
    private static SaveMenuSlotRowView[] CreateSaveSlotRows(GameObject rowPrefab, Transform parent)
    {
        SaveMenuSlotRowView[] rows = new SaveMenuSlotRowView[_saveSlotCount];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = InstantiateNested<SaveMenuSlotRowView>(
                rowPrefab,
                parent,
                $"SaveSlotRow{index}"
            );
            SetSourcePosition(rows[index].transform as RectTransform, 34, 81 + 42 * index);
        }

        return rows;
    }

    /// <summary>
    /// Creates and configures all typed tactical-option row instances.
    /// </summary>
    /// <param name="rowPrefab">The generated tactical-option row prefab.</param>
    /// <param name="parent">The Save Menu window transform.</param>
    /// <returns>The ordered tactical-option row instances.</returns>
    private static SaveMenuTacticalOptionRowView[] CreateTacticalRows(
        GameObject rowPrefab,
        Transform parent
    )
    {
        SaveMenuTacticalOption[] options =
        {
            SaveMenuTacticalOption.Starfield,
            SaveMenuTacticalOption.Planet,
            SaveMenuTacticalOption.Pyro,
            SaveMenuTacticalOption.HighDetail,
            SaveMenuTacticalOption.Holocube,
        };
        string[] labels =
        {
            "Show Starfield",
            "Show Planet",
            "Show Pyro",
            "High Detail",
            "Show Holocube",
        };

        SaveMenuTacticalOptionRowView[] rows = new SaveMenuTacticalOptionRowView[options.Length];
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index] = InstantiateNested<SaveMenuTacticalOptionRowView>(
                rowPrefab,
                parent,
                $"TacticalOptionRow{index}"
            );
            SetSourcePosition(rows[index].transform as RectTransform, 357, 311 + 27 * index);
            AssignInt(rows[index], "option", (int)options[index]);
            TextMeshProUGUI label = FindRequiredChild<TextMeshProUGUI>(
                rows[index].transform,
                "LabelTextField"
            );
            label.text = labels[index];
        }

        return rows;
    }

    /// <summary>
    /// Creates the nested shared input used by a save-slot row.
    /// </summary>
    /// <param name="parent">The save-slot row transform.</param>
    /// <returns>The configured save-name input field.</returns>
    private static TMP_InputField CreateSaveNameInput(Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_commonTextInputPrefabPath);
        if (prefab == null)
            throw new FileNotFoundException(_commonTextInputPrefabPath);

        TMP_InputField input = InstantiateNested<TMP_InputField>(prefab, parent, "NameInputField");
        SetSourceRect(input.transform as RectTransform, 86, 8, 150, 18);
        input.textComponent.alignment = TextAlignmentOptions.BaselineLeft;
        input.textComponent.color = _whiteTextColor;
        SetSourceRect(input.textComponent.rectTransform, 2, 0, 148, 18);

        if (input.placeholder is not TextMeshProUGUI placeholder)
            throw new MissingReferenceException("Save-slot text input placeholder is missing.");
        placeholder.text = "Empty Save Slot";
        placeholder.alignment = TextAlignmentOptions.BaselineLeft;
        placeholder.color = _whiteTextColor;
        SetSourceRect(placeholder.rectTransform, 2, 0, 148, 18);
        return input;
    }

    /// <summary>
    /// Creates the inline modal confirmation dialog hosted by the Save Menu window.
    /// </summary>
    /// <param name="parent">The Save Menu window transform.</param>
    /// <returns>The configured confirmation dialog view.</returns>
    private static ConfirmationDialogView CreateConfirmationDialog(Transform parent)
    {
        GameObject root = new GameObject(
            "ConfirmationDialog",
            typeof(RectTransform),
            typeof(ConfirmationDialogView)
        );
        root.transform.SetParent(parent, false);
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, _windowWidth, _windowHeight);
        ConfirmationDialogView view = root.GetComponent<ConfirmationDialogView>();
        view.enabled = true;

        GameObject blockerObject = CreateRectObject("InputBlocker", root.transform);
        blockerObject.AddComponent<CanvasRenderer>();
        Image blocker = blockerObject.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;
        SetSourceRect(blocker.rectTransform, 0, 0, _windowWidth, _windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            root.transform,
            _confirmationDialogTexturePath,
            114,
            150,
            412,
            176
        );
        RawImage confirmImage = CreateRawImage(
            "ConfirmButtonImage",
            root.transform,
            _confirmationYesTexturePath,
            252,
            285,
            57,
            28
        );
        Button confirmButton = CreatePressableButton(
            confirmImage,
            LoadRequiredTexture(_confirmationYesTexturePath),
            LoadRequiredTexture(_confirmationYesPressedTexturePath),
            out RawImagePressVisual confirmPressVisual
        );
        RawImage cancelImage = CreateRawImage(
            "CancelButtonImage",
            root.transform,
            _confirmationNoTexturePath,
            343,
            285,
            57,
            28
        );
        Button cancelButton = CreatePressableButton(
            cancelImage,
            LoadRequiredTexture(_confirmationNoTexturePath),
            LoadRequiredTexture(_confirmationNoPressedTexturePath),
            out RawImagePressVisual cancelPressVisual
        );
        TextMeshProUGUI message = CreateSaveMenuText(
            "MessageTextField",
            root.transform,
            "Are you sure you want to quit?",
            114,
            218,
            412,
            12,
            12,
            TextAlignmentOptions.Baseline
        );
        message.color = _whiteTextColor;

        AssignReference(view, "backgroundImage", background);
        AssignColor(view, "messageTextColor", _whiteTextColor);
        AssignReference(view, "confirmButtonImage", confirmImage);
        AssignReference(view, "confirmButton", confirmButton);
        AssignReference(view, "confirmButtonPressVisual", confirmPressVisual);
        AssignReference(
            view,
            "confirmButtonUpTexture",
            LoadRequiredTexture(_confirmationYesTexturePath)
        );
        AssignReference(
            view,
            "confirmButtonDownTexture",
            LoadRequiredTexture(_confirmationYesPressedTexturePath)
        );
        AssignReference(view, "cancelButtonImage", cancelImage);
        AssignReference(view, "cancelButton", cancelButton);
        AssignReference(view, "cancelButtonPressVisual", cancelPressVisual);
        AssignReference(
            view,
            "cancelButtonUpTexture",
            LoadRequiredTexture(_confirmationNoTexturePath)
        );
        AssignReference(
            view,
            "cancelButtonDownTexture",
            LoadRequiredTexture(_confirmationNoPressedTexturePath)
        );
        AssignReference(view, "messageTextField", message);
        root.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates a pressable window command at an authored source-space position.
    /// </summary>
    /// <param name="name">The command image object name.</param>
    /// <param name="parent">The command parent transform.</param>
    /// <param name="upTexturePath">The normal texture path.</param>
    /// <param name="downTexturePath">The pressed texture path.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="image">The created command image.</param>
    /// <param name="pressVisual">The created pressed-state visual.</param>
    /// <returns>The created command button.</returns>
    private static Button CreateWindowCommandButton(
        string name,
        Transform parent,
        string upTexturePath,
        string downTexturePath,
        int x,
        int y,
        out RawImage image,
        out RawImagePressVisual pressVisual
    )
    {
        Texture2D upTexture = LoadRequiredTexture(upTexturePath);
        Texture2D downTexture = LoadRequiredTexture(downTexturePath);
        image = CreateRawImage(name, parent, upTexturePath, x, y);
        return CreatePressableButton(image, upTexture, downTexture, out pressVisual);
    }

    /// <summary>
    /// Adds an explicitly wired pressed-state visual to an authored image button.
    /// </summary>
    /// <param name="image">The button's target image.</param>
    /// <param name="upTexture">The normal texture.</param>
    /// <param name="downTexture">The pressed texture.</param>
    /// <param name="pressVisual">The created pressed-state visual.</param>
    /// <returns>The configured Button.</returns>
    private static Button CreatePressableButton(
        RawImage image,
        Texture upTexture,
        Texture downTexture,
        out RawImagePressVisual pressVisual
    )
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.enabled = true;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        pressVisual = image.gameObject.AddComponent<RawImagePressVisual>();
        pressVisual.enabled = true;
        AssignReference(pressVisual, "image", image);
        AssignReference(pressVisual, "button", button);
        pressVisual.SetTextures(upTexture, downTexture);
        return button;
    }

    /// <summary>
    /// Creates a styled Save Menu TMP label.
    /// </summary>
    /// <param name="name">The label object name.</param>
    /// <param name="parent">The label parent transform.</param>
    /// <param name="text">The initial label content.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="fontSize">The TMP font size.</param>
    /// <param name="alignment">The TMP alignment.</param>
    /// <returns>The configured label.</returns>
    private static TextMeshProUGUI CreateSaveMenuText(
        string name,
        Transform parent,
        string text,
        int x,
        int y,
        int width,
        int height,
        int fontSize,
        TextAlignmentOptions alignment
    )
    {
        GameObject labelObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(Shadow)
        );
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = _enabledTextColor;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        Shadow shadow = labelObject.GetComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
        SetSourceRect(label.rectTransform, x, y, width, height);
        return label;
    }

    /// <summary>
    /// Creates a top-left anchored RectTransform GameObject.
    /// </summary>
    /// <param name="name">The GameObject name.</param>
    /// <param name="parent">The optional parent transform.</param>
    /// <returns>The created GameObject.</returns>
    private static GameObject CreateRectObject(string name, Transform parent = null)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    /// <summary>
    /// Creates an authored RawImage using the texture's source-space dimensions.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The image parent transform.</param>
    /// <param name="texturePath">The required texture path.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <returns>The configured RawImage.</returns>
    private static RawImage CreateRawImage(
        string name,
        Transform parent,
        string texturePath,
        int x,
        int y
    )
    {
        Texture2D texture = LoadRequiredTexture(texturePath);
        Vector2Int size = UILayout.GetTextureSourceSize(texture);
        return CreateRawImage(name, parent, texturePath, x, y, size.x, size.y);
    }

    /// <summary>
    /// Creates an authored RawImage with explicit source-space dimensions.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The image parent transform.</param>
    /// <param name="texturePath">The required texture path.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The configured RawImage.</returns>
    private static RawImage CreateRawImage(
        string name,
        Transform parent,
        string texturePath,
        int x,
        int y,
        int width,
        int height
    )
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        imageObject.transform.SetParent(parent, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = LoadRequiredTexture(texturePath);
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Instantiates one nested prefab component under an authored parent.
    /// </summary>
    /// <typeparam name="T">The required nested component type.</typeparam>
    /// <param name="prefab">The prefab asset to instantiate.</param>
    /// <param name="parent">The instance parent transform.</param>
    /// <param name="name">The instance object name.</param>
    /// <returns>The required nested component.</returns>
    private static T InstantiateNested<T>(GameObject prefab, Transform parent, string name)
        where T : MonoBehaviour
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        T component = instance.GetComponent<T>();
        if (component == null)
            throw new MissingReferenceException($"{prefab.name} is missing {typeof(T).Name}.");

        component.enabled = true;
        return component;
    }

    /// <summary>
    /// Finds a required direct child component.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="parent">The direct parent transform.</param>
    /// <param name="childName">The required child object name.</param>
    /// <returns>The matching child component.</returns>
    private static T FindRequiredChild<T>(Transform parent, string childName)
        where T : Component
    {
        Transform child = parent.Find(childName);
        T component = child == null ? null : child.GetComponent<T>();
        if (component == null)
            throw new MissingReferenceException(
                $"{parent.name}/{childName} is missing {typeof(T).Name}."
            );
        return component;
    }

    /// <summary>
    /// Selects a deterministic configured theme that supplies Save Menu preview art.
    /// </summary>
    /// <returns>The first semantically eligible theme ordered by stable identifier.</returns>
    private static FactionTheme ResolvePreviewTheme()
    {
        FactionThemes themes = ResourceManager.GetConfig<FactionThemes>();
        FactionTheme theme = themes
            .Where(candidate => candidate != null)
            .Where(candidate => !string.IsNullOrEmpty(candidate.FactionInstanceID))
            .Where(candidate =>
                !string.IsNullOrEmpty(candidate.SaveMenuReturnStrategyButtonImagePath)
            )
            .Where(candidate =>
                !string.IsNullOrEmpty(candidate.SaveMenuReturnStrategyButtonPressedImagePath)
            )
            .Where(candidate => !string.IsNullOrEmpty(candidate.SaveMenuSlotIconImagePath))
            .OrderBy(candidate => candidate.FactionInstanceID, StringComparer.Ordinal)
            .FirstOrDefault();
        if (theme == null)
            throw new InvalidOperationException(
                "No configured theme supplies complete Save Menu art."
            );
        return theme;
    }

    /// <summary>
    /// Loads one required texture from either an asset or Resources-style path.
    /// </summary>
    /// <param name="path">The configured texture path.</param>
    /// <returns>The loaded texture asset.</returns>
    private static Texture2D LoadRequiredTexture(string path)
    {
        string assetPath = ResolveTextureAssetPath(path);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
            throw new FileNotFoundException(assetPath);
        return texture;
    }

    /// <summary>
    /// Resolves a configured Resources path to its concrete texture asset path.
    /// </summary>
    /// <param name="path">The asset or Resources-style path.</param>
    /// <returns>The concrete asset path including an image extension.</returns>
    private static string ResolveTextureAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("A texture path is required.", nameof(path));

        string assetPath = path.StartsWith("Assets/", StringComparison.Ordinal)
            ? path
            : Path.Combine("Assets/Resources", path).Replace("\\", "/");
        if (File.Exists(assetPath))
            return assetPath;
        if (File.Exists(assetPath + ".png"))
            return assetPath + ".png";
        if (File.Exists(assetPath + ".jpg"))
            return assetPath + ".jpg";
        if (File.Exists(assetPath + ".jpeg"))
            return assetPath + ".jpeg";
        return assetPath;
    }

    /// <summary>
    /// Assigns the authored CanvasGroup required by a UIWindow.
    /// </summary>
    /// <param name="window">The generated window component.</param>
    private static void ConfigureWindow(UIWindow window)
    {
        window.enabled = true;
        CanvasGroup group = window.GetComponent<CanvasGroup>();
        if (group == null)
            group = window.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = false;
        AssignReference(window, "inputGroup", group);
    }

    /// <summary>
    /// Assigns one serialized object reference.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The object-reference value.</param>
    private static void AssignReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns an ordered serialized object-reference array.
    /// </summary>
    /// <typeparam name="T">The referenced Unity object type.</typeparam>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized array property name.</param>
    /// <param name="values">The ordered reference values.</param>
    private static void AssignReferenceArray<T>(
        UnityEngine.Object target,
        string propertyName,
        IReadOnlyList<T> values
    )
        where T : UnityEngine.Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one serialized color value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized color property name.</param>
    /// <param name="value">The color value.</param>
    private static void AssignColor(UnityEngine.Object target, string propertyName, Color value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.colorValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one serialized integer or enum value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized integer property name.</param>
    /// <param name="value">The integer value.</param>
    private static void AssignInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Applies a source-space top-left rectangle.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Moves a top-left authored RectTransform without changing its dimensions.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    private static void SetSourcePosition(RectTransform rect, int x, int y)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Centers a fixed-size RectTransform within its parent.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="width">The authored width.</param>
    /// <param name="height">The authored height.</param>
    private static void SetCenteredRect(RectTransform rect, int width, int height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Stretches a RectTransform across its parent.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Applies one layer recursively to an authored prefab hierarchy.
    /// </summary>
    /// <param name="gameObject">The root GameObject.</param>
    /// <param name="layer">The Unity layer index.</param>
    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        if (gameObject == null || layer < 0)
            return;
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>
    /// Saves one generated prefab and destroys its temporary authoring hierarchy.
    /// </summary>
    /// <param name="root">The temporary prefab root.</param>
    /// <param name="path">The destination prefab asset path.</param>
    /// <returns>The saved prefab asset root.</returns>
    private static GameObject SavePrefab(GameObject root, string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        UnityEngine.Object.DestroyImmediate(root);
        if (!success || prefab == null)
            throw new InvalidOperationException($"Failed to save prefab at {path}.");
        return prefab;
    }
}
