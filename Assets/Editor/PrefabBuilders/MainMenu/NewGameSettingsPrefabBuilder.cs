using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;

/// <summary>
/// Authors the custom-game settings with the existing Options menu frame and controls.
/// </summary>
public static class NewGameSettingsPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/MainMenu/NewGameSettings.prefab";
    private const string _artRoot = "Application/OptionsMenu/UI/ui_settingsmenu_";
    private const int _controlTop = 1;
    private static readonly Color _textColor = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color _mutedColor = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color _accentColor = new Color(0.373f, 0.659f, 0.925f);

    /// <summary>
    /// Creates a modal editor using the same source-space layout and artwork as Options.
    /// </summary>
    public static void Rebuild()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        GameObject root = new GameObject(
            "NewGameSettings",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        root.SetActive(false);
        NewGameSettingsView view = root.AddComponent<NewGameSettingsView>();
        ConfigureWindowRoot(root.GetComponent<UIWindow>());
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(454f, 341f);
        RectTransform contentRoot = CreateMenuWindowContent(
            rect,
            "CUSTOM GAME",
            out TextMeshProUGUI header,
            out TextMeshProUGUI title
        );
        header.enableAutoSizing = true;
        header.fontSizeMin = 16f;
        header.fontSizeMax = 22f;
        title.text = "GAME SETUP";
        RectTransform difficultyPage = CreateLayer("DifficultyPage", contentRoot)
            .GetComponent<RectTransform>();
        SetSourceRect(difficultyPage, 228, 69, 382, 365);
        string[] names =
        {
            "GAME SETUP",
            "DIFFICULTY MODIFIERS",
            "WORLD GENERATION",
            "FACTION MODIFIERS",
            "SAVE / LOAD",
        };
        Button[] tabs = new Button[names.Length];
        for (int i = 0; i < tabs.Length; i++)
            tabs[i] = CreateMenuNavigationRow(
                "Tab" + i,
                contentRoot,
                names[i],
                38,
                i == names.Length - 1 ? 360 : 82 + i * 32,
                _textColor,
                out _,
                out _
            );
        TextMeshProUGUI playerHeading = CreateTextLabel("PlayerHeading", difficultyPage);
        playerHeading.text = "PLAYER";
        playerHeading.fontSize = 9;
        playerHeading.color = _accentColor;
        playerHeading.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(playerHeading.rectTransform, 174, 16, 47, 16);
        TextMeshProUGUI aiHeading = CreateTextLabel("AIHeading", difficultyPage);
        aiHeading.text = "AI";
        aiHeading.fontSize = 9;
        aiHeading.color = _accentColor;
        aiHeading.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(aiHeading.rectTransform, 261, 16, 47, 16);
        ScrollAreaView scrollArea = CreateScrollAreaView(
            difficultyPage,
            "SettingsScrollArea",
            20,
            36,
            342,
            290,
            0,
            0,
            326,
            290,
            333,
            0,
            9,
            290,
            out RectTransform content
        );
        GameObject row = BuildRow(content);
        row.SetActive(false);
        Button defaults = CreateMenuActionButton(
            contentRoot,
            "Defaults",
            "APPLY DEFAULTS",
            300,
            _textColor,
            114
        );
        Button apply = CreateMenuActionButton(
            contentRoot,
            "Apply",
            "START GAME",
            424,
            _textColor,
            114
        );
        TextMeshProUGUI applyLabel = apply.GetComponentInChildren<TextMeshProUGUI>();
        applyLabel.color = _accentColor;
        applyLabel.fontStyle = FontStyles.Bold;
        Button close = CreateMenuNavigationRow(
            "Close",
            contentRoot,
            "BACK TO MENU",
            38,
            392,
            _mutedColor,
            out _,
            out _
        );
        SerializedObject serialized = new SerializedObject(view);
        SerializedProperty tabArray = serialized.FindProperty("tabs");
        tabArray.arraySize = tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
            tabArray.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
        serialized.FindProperty("title").objectReferenceValue = title;
        serialized.FindProperty("playerHeading").objectReferenceValue = playerHeading;
        serialized.FindProperty("aiHeading").objectReferenceValue = aiHeading;
        serialized.FindProperty("content").objectReferenceValue = content;
        serialized.FindProperty("rowTemplate").objectReferenceValue = row;
        serialized.FindProperty("scrollArea").objectReferenceValue = scrollArea;
        serialized.FindProperty("closeButton").objectReferenceValue = close;
        serialized.FindProperty("defaultsButton").objectReferenceValue = defaults;
        serialized.FindProperty("applyButton").objectReferenceValue = apply;
        serialized.FindProperty("rowIdleSprite").objectReferenceValue =
            ContentPackEditor.Assets.GetSprite(_artRoot + "row_background", _panelBorder);
        serialized.FindProperty("rowActiveSprite").objectReferenceValue =
            ContentPackEditor.Assets.GetSprite(_artRoot + "row_selected_background", _panelBorder);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Authors a setting row with an inline description and independent player and AI values.
    /// </summary>
    /// <param name="parent">The scroll content.</param>
    /// <returns>The inactive row template.</returns>
    private static GameObject BuildRow(Transform parent)
    {
        RectTransform row = CreateLayer("RowTemplate", parent).GetComponent<RectTransform>();
        SetSourceRect(row, 0, 0, 326, 44);
        TextMeshProUGUI label = CreateTextLabel("Label", row);
        label.fontSize = 9;
        label.color = _textColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(label.rectTransform, 0, _controlTop, 150, 16);
        label.enableAutoSizing = false;
        TextMeshProUGUI description = CreateTextLabel("Description", row);
        description.fontSize = 9;
        description.color = _mutedColor;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(description.rectTransform, 0, 17, 150, 28);
        BuildValueControl(row, "Player", 154, 203);
        BuildValueControl(row, "AI", 241, 290);
        BuildValueControl(row, "World", 241, 290);
        row.Find("WorldValueBadge").gameObject.SetActive(false);
        row.Find("WorldValue").gameObject.SetActive(false);
        row.Find("WorldReset").gameObject.SetActive(false);
        return row.gameObject;
    }

    /// <summary>
    /// Authors one recipient's numeric field and restore button.
    /// </summary>
    /// <param name="row">The owning settings row.</param>
    /// <param name="prefix">The control-name prefix.</param>
    /// <param name="valueX">The numeric field's source-space left coordinate.</param>
    /// <param name="resetX">The restore button's source-space left coordinate.</param>
    private static void BuildValueControl(Transform row, string prefix, int valueX, int resetX)
    {
        CreateSlicedImage(
            prefix + "ValueBadge",
            row,
            _artRoot + "badge_background",
            new Vector4(6, 6, 6, 6),
            valueX,
            _controlTop,
            47,
            16,
            Color.white
        );
        TMP_InputField input = CreateTextInputField(
            prefix + "Value",
            row,
            string.Empty,
            valueX,
            _controlTop,
            47,
            16
        );
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        UILayout.ConfigureCenteredNumericInput(input, _textColor, 10f, 4, 1);
        CreateMenuRestoreButton(prefix + "Reset", row, resetX, _controlTop);
    }
}
