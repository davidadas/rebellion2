using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;

/// <summary>
/// Authors the new-campaign difficulty settings with the existing Options menu frame and controls.
/// </summary>
public static class NewGameSettingsPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/MainMenu/NewGameSettings.prefab";
    private const string _artRoot = "Application/OptionsMenu/UI/ui_settingsmenu_";
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
            typeof(UIWindow),
            typeof(NewGameSettingsView)
        );
        ConfigureWindowRoot(root.GetComponent<UIWindow>());
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(454f, 341f);
        RectTransform contentRoot = CreateMenuWindowContent(
            rect,
            "GAME SETUP",
            out _,
            out TextMeshProUGUI title
        );
        title.text = "DIFFICULTY MODIFIERS";
        string[] names = { "DIFFICULTY MODIFIERS" };
        Button[] tabs = new Button[names.Length];
        for (int i = 0; i < tabs.Length; i++)
            tabs[i] = CreateMenuNavigationRow(
                "Tab" + i,
                contentRoot,
                names[i],
                38,
                82 + i * 32,
                _textColor,
                out _,
                out _,
                TextAlignmentOptions.Midline
            );
        TMP_Text subtitle = Label(
            "Subtitle",
            contentRoot,
            new Rect(38, 122, 163, 86),
            string.Empty,
            11,
            _mutedColor
        );
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text scope = Label(
            "Scope",
            contentRoot,
            new Rect(248, 82, 132, 16),
            string.Empty,
            10,
            _accentColor
        );
        Label(
            "PlayerHeading",
            contentRoot,
            new Rect(408, 103, 76, 16),
            "PLAYER",
            10,
            _accentColor
        ).alignment = TextAlignmentOptions.Midline;
        Label(
            "AIHeading",
            contentRoot,
            new Rect(493, 103, 76, 16),
            "AI",
            10,
            _accentColor
        ).alignment = TextAlignmentOptions.Midline;
        ScrollAreaView scrollArea = CreateScrollAreaView(
            contentRoot,
            "SettingsScrollArea",
            248,
            122,
            342,
            241,
            0,
            0,
            326,
            241,
            333,
            0,
            9,
            241,
            out RectTransform content
        );
        GameObject row = BuildRow(content);
        row.SetActive(false);
        Image tooltip = CreateSlicedImage(
            "SettingTooltip",
            contentRoot,
            _menuBadgeAddress,
            _badgeBorder,
            248,
            122,
            326,
            40,
            new Color(0.08f, 0.12f, 0.18f)
        );
        tooltip.raycastTarget = false;
        TMP_Text tooltipText = Label(
            "Text",
            tooltip.transform,
            new Rect(7, 6, 312, 28),
            string.Empty,
            10,
            _textColor
        );
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        tooltip.gameObject.SetActive(false);
        TMP_Text changes = Label(
            "ChangeCount",
            contentRoot,
            new Rect(38, 296, 163, 16),
            "0 CHANGES",
            10,
            _mutedColor
        );
        Button defaults = CreateMenuActionButton(
            contentRoot,
            "Defaults",
            "RESTORE DEFAULTS",
            248,
            _textColor,
            132
        );
        Button apply = CreateMenuActionButton(contentRoot, "Apply", "APPLY", 390, _textColor, 76);
        Button cancel = CreateMenuActionButton(
            contentRoot,
            "Cancel",
            "CANCEL",
            476,
            _textColor,
            114
        );
        Button close = CreateMenuNavigationRow(
            "Close",
            contentRoot,
            "BACK TO MENU",
            38,
            392,
            _textColor,
            out _,
            out _,
            TextAlignmentOptions.Midline
        );
        SerializedObject serialized = new SerializedObject(
            root.GetComponent<NewGameSettingsView>()
        );
        SerializedProperty tabArray = serialized.FindProperty("tabs");
        tabArray.arraySize = tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
            tabArray.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
        serialized.FindProperty("title").objectReferenceValue = title;
        serialized.FindProperty("subtitle").objectReferenceValue = subtitle;
        serialized.FindProperty("scope").objectReferenceValue = scope;
        serialized.FindProperty("changedCount").objectReferenceValue = changes;
        serialized.FindProperty("content").objectReferenceValue = content;
        serialized.FindProperty("rowTemplate").objectReferenceValue = row;
        serialized.FindProperty("tooltipRoot").objectReferenceValue = tooltip.rectTransform;
        serialized.FindProperty("tooltipText").objectReferenceValue = tooltipText;
        serialized.FindProperty("scroll").objectReferenceValue =
            scrollArea.ScrollRoot.GetComponent<ScrollRect>();
        serialized.FindProperty("scrollArea").objectReferenceValue = scrollArea;
        serialized.FindProperty("cancelButton").objectReferenceValue = cancel;
        serialized.FindProperty("closeButton").objectReferenceValue = close;
        serialized.FindProperty("defaultsButton").objectReferenceValue = defaults;
        serialized.FindProperty("applyButton").objectReferenceValue = apply;
        serialized.FindProperty("rowIdleSprite").objectReferenceValue =
            ContentPackEditor.Assets.GetSprite(_artRoot + "row_background", _panelBorder);
        serialized.FindProperty("rowActiveSprite").objectReferenceValue =
            ContentPackEditor.Assets.GetSprite(_artRoot + "row_selected_background", _panelBorder);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Authors a compact setting row with an info control and independent player and AI values.
    /// </summary>
    /// <param name="parent">The scroll content.</param>
    /// <returns>The inactive row template.</returns>
    private static GameObject BuildRow(Transform parent)
    {
        RectTransform row = Rect("RowTemplate", parent, new Rect(0, 0, 326, 23));
        TMP_Text label = Label("Label", row, new Rect(0, 4, 136, 15), string.Empty, 10, _textColor);
        label.enableAutoSizing = true;
        label.fontSizeMin = 7;
        label.fontSizeMax = 10;
        Image infoFrame = CreateSlicedImage(
            "Info",
            row,
            _menuBadgeAddress,
            _badgeBorder,
            138,
            3,
            16,
            16,
            Color.white
        );
        infoFrame.raycastTarget = true;
        Button infoButton = infoFrame.gameObject.AddComponent<Button>();
        infoButton.targetGraphic = infoFrame;
        ApplyMenuFillButtonFeedback(infoButton);
        TMP_Text infoText = Label(
            "Label",
            infoFrame.transform,
            new Rect(0, 0, 16, 16),
            "i",
            10,
            _textColor
        );
        infoText.alignment = TextAlignmentOptions.Midline;
        infoText.raycastTarget = false;
        BuildValueControl(row, "Player", 160, 211);
        BuildValueControl(row, "AI", 245, 296);
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
            3,
            47,
            16,
            Color.white
        );
        TMP_InputField input = CreateTextInputField(
            prefix + "Value",
            row,
            string.Empty,
            valueX,
            3,
            47,
            16
        );
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.textComponent.fontSize = 10;
        input.textComponent.alignment = TextAlignmentOptions.Center;
        RectTransform viewport = input.textViewport;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        RectTransform text = input.textComponent.rectTransform;
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(4, 1);
        text.offsetMax = new Vector2(-4, -1);
        CreateMenuRestoreButton(prefix + "Reset", row, resetX, 3);
    }

    /// <summary>
    /// Creates a top-left source-space rectangle.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="bounds">The source-space bounds.</param>
    /// <returns>The authored rectangle.</returns>
    private static RectTransform Rect(string name, Transform parent, Rect bounds)
    {
        RectTransform rect = new GameObject(
            name,
            typeof(RectTransform)
        ).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
        rect.sizeDelta = bounds.size;
        return rect;
    }

    /// <summary>
    /// Positions a shadowed label using the existing Options typography.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="bounds">The source-space bounds.</param>
    /// <param name="text">The label text.</param>
    /// <param name="size">The source-space font size.</param>
    /// <param name="color">The text color.</param>
    /// <returns>The authored label.</returns>
    private static TMP_Text Label(
        string name,
        Transform parent,
        Rect bounds,
        string text,
        float size,
        Color color
    )
    {
        TMP_Text label = CreateTextLabel(name, parent);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
        rect.sizeDelta = bounds.size;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
}
