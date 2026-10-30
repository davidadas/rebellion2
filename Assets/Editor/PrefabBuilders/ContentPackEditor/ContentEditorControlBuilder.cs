using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adapts shared application controls to resizable content-authoring panes.
/// </summary>
internal static class ContentEditorControlBuilder
{
    internal static Color Background =>
        ContentEditorAppearance.ColorFor(
            ContentEditorColorMode.Dark,
            ContentEditorColorRole.Background
        );
    internal static Color InputColor =>
        ContentEditorAppearance.ColorFor(ContentEditorColorMode.Dark, ContentEditorColorRole.Input);
    internal static Color Text =>
        ContentEditorAppearance.ColorFor(ContentEditorColorMode.Dark, ContentEditorColorRole.Text);
    internal static Color Muted =>
        ContentEditorAppearance.ColorFor(ContentEditorColorMode.Dark, ContentEditorColorRole.Muted);
    internal static Color Divider =>
        ContentEditorAppearance.ColorFor(
            ContentEditorColorMode.Dark,
            ContentEditorColorRole.Divider
        );

    /// <summary>
    /// Creates a layout container with a top-left coordinate origin.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <returns>The container.</returns>
    internal static RectTransform Layer(string name, Transform parent)
    {
        RectTransform rect = new GameObject(
            name,
            typeof(RectTransform)
        ).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>
    /// Creates a plain authoring surface.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="color">The fill color.</param>
    /// <returns>The surface.</returns>
    internal static Image Surface(string name, Transform parent, Color color)
    {
        Image image = Layer(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Creates a surface whose color follows the editor's selected appearance.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="role">The surface's visual purpose.</param>
    /// <returns>The styled surface.</returns>
    internal static Image Surface(string name, Transform parent, ContentEditorColorRole role)
    {
        Image image = Surface(
            name,
            parent,
            ContentEditorAppearance.ColorFor(ContentEditorColorMode.Dark, role)
        );
        BindColor(image, role);
        return image;
    }

    /// <summary>
    /// Binds an existing shared primitive to an explicit editor palette role.
    /// </summary>
    /// <param name="graphic">The authored primitive.</param>
    /// <param name="role">Its visual purpose.</param>
    /// <param name="control">The optional selectable owning interaction colors.</param>
    internal static void BindColor(
        Graphic graphic,
        ContentEditorColorRole role,
        Selectable control = null
    )
    {
        ContentEditorColorBinding binding =
            graphic.GetComponent<ContentEditorColorBinding>()
            ?? graphic.gameObject.AddComponent<ContentEditorColorBinding>();
        binding.Bind(graphic, role, control);
    }

    /// <summary>
    /// Creates a label using the application's shared typography factory.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="text">The caption.</param>
    /// <param name="size">The font size.</param>
    /// <param name="muted">Whether to use secondary text color.</param>
    /// <param name="bindColor">Whether the editor palette owns the label color.</param>
    /// <returns>The label.</returns>
    internal static TextMeshProUGUI Label(
        string name,
        Transform parent,
        string text,
        int size = 15,
        bool muted = false,
        bool bindColor = true
    )
    {
        TextMeshProUGUI label = MenuPrefabBuilder.CreateTextLabel(
            name,
            parent,
            MenuControlStyle.Flat
        );
        label.text = text;
        label.parseCtrlCharacters = false;
        label.richText = false;
        label.fontSize = size;
        label.fontStyle = FontStyles.Normal;
        label.color = muted ? Muted : Text;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (bindColor)
            BindColor(label, muted ? ContentEditorColorRole.Muted : ContentEditorColorRole.Text);
        return label;
    }

    /// <summary>
    /// Creates a button with the shared menu feedback behavior.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="text">The caption.</param>
    /// <returns>The button.</returns>
    internal static Button Button(string name, Transform parent, string text)
    {
        Button button = MenuPrefabBuilder.CreateMenuActionButton(
            parent,
            name,
            text,
            0,
            Text,
            120,
            MenuControlStyle.Flat
        );
        Image frame = button.GetComponent<Image>();
        frame.color = new Color32(88, 88, 90, 255);
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
        BindColor(frame, ContentEditorColorRole.Button, button);
        BindColor(
            button.GetComponentInChildren<TextMeshProUGUI>(),
            ContentEditorColorRole.ButtonText
        );
        return button;
    }

    /// <summary>
    /// Creates a menu heading or tab without a permanent filled button surface.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="text">The caption.</param>
    /// <returns>The text command button.</returns>
    internal static Button PlainButton(string name, Transform parent, string text)
    {
        Image hit = Surface(name, parent, Color.clear);
        hit.raycastTarget = true;
        hit.canvasRenderer.cullTransparentMesh = false;
        Button button = hit.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = Label("Label", hit.transform, text);
        label.alignment = TextAlignmentOptions.Midline;
        Stretch(label.rectTransform, 8, 0, 8, 0);
        label.color = Color.white;
        button.targetGraphic = label;
        ColorBlock colors = button.colors;
        colors.normalColor = Text;
        colors.highlightedColor = new Color32(246, 207, 129, 255);
        colors.selectedColor = Text;
        colors.pressedColor = new Color32(198, 158, 95, 255);
        button.colors = colors;
        BindColor(label, ContentEditorColorRole.TextButton, button);
        return button;
    }

    /// <summary>
    /// Instantiates the canonical text-input prefab with authoring typography and focus feedback.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="placeholder">The empty-field prompt.</param>
    /// <returns>The shared input.</returns>
    internal static TMP_InputField Input(string name, Transform parent, string placeholder)
    {
        TMP_InputField input = MenuPrefabBuilder.CreateTextInputField(
            name,
            parent,
            placeholder,
            0,
            0,
            200,
            34
        );
        input.gameObject.AddComponent<TextInputHistory>();
        input.gameObject.AddComponent<TextInputOverflowView>();
        Image image = input.GetComponent<Image>();
        image.color = InputColor;
        Outline outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(88, 88, 90, 255);
        outline.effectDistance = new Vector2(2, -2);
        outline.useGraphicAlpha = false;
        input.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = input.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(220, 220, 220, 255);
        colors.selectedColor = colors.highlightedColor;
        input.colors = colors;
        Stretch(input.textViewport, 10, 6, 10, 6);
        foreach (TextMeshProUGUI label in input.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.parseCtrlCharacters = false;
            label.richText = false;
            label.fontSize = 15;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = label == input.textComponent ? new Color32(224, 202, 156, 255) : Muted;
            Stretch(label.rectTransform);
            BindColor(
                label,
                label == input.textComponent
                    ? ContentEditorColorRole.Text
                    : ContentEditorColorRole.Muted
            );
        }
        BindColor(image, ContentEditorColorRole.Input, input);
        return input;
    }

    /// <summary>
    /// Instantiates the canonical menu selector with neutral editor styling.
    /// </summary>
    /// <param name="name">The instance name.</param>
    /// <param name="parent">The owning form.</param>
    /// <returns>The shared selector.</returns>
    internal static TMP_Dropdown Dropdown(string name, Transform parent)
    {
        TMP_Dropdown dropdown = MenuControlPrefabBuilder.CreateDropdown(
            parent,
            name,
            MenuControlStyle.Flat
        );
        Toggle item = dropdown.template.GetComponentInChildren<Toggle>(true);
        foreach (Image image in dropdown.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Mask>() == null)
                BindColor(
                    image,
                    image == item.graphic
                        ? ContentEditorColorRole.Checkmark
                        : ContentEditorColorRole.Panel
                );
        }
        ((Image)item.graphic).sprite = LoadIcon("Checkmark");
        foreach (TMP_Text label in dropdown.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontSize = 15;
            BindColor(label, ContentEditorColorRole.Text);
        }
        BindColor(item.targetGraphic, ContentEditorColorRole.Panel, item);
        dropdown.transition = Selectable.Transition.ColorTint;
        BindColor(dropdown.targetGraphic, ContentEditorColorRole.Panel, dropdown);
        return dropdown;
    }

    /// <summary>
    /// Instantiates a shared scroll area and attaches responsive pane layout.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <returns>The responsive scroll wrapper.</returns>
    internal static ScrollAreaLayout Scroll(string name, Transform parent)
    {
        ScrollAreaView scroll = CommonUIPrefabBuilder.CreateScrollAreaView(
            MenuPrefabBuilder.FlatScrollAreaPath,
            parent,
            name,
            new RectInt(0, 0, 300, 500),
            new RectInt(0, 0, 284, 500),
            new RectInt(288, 0, 12, 500),
            out _
        );
        foreach (TMP_Text label in scroll.GetComponentsInChildren<TMP_Text>(true))
            BindColor(label, ContentEditorColorRole.Muted);
        foreach (Image image in scroll.GetComponentsInChildren<Image>(true))
            if (image.color.a > 0 && image.GetComponent<Mask>() == null)
                BindColor(
                    image,
                    image.name.Contains("Handle")
                        ? ContentEditorColorRole.Border
                        : ContentEditorColorRole.Divider
                );
        foreach (Scrollbar bar in scroll.GetComponentsInChildren<Scrollbar>(true))
            StyleScrollbar(bar);
        ScrollAreaLayout layout = scroll.gameObject.AddComponent<ScrollAreaLayout>();
        layout.Configure(scroll);
        return layout;
    }

    /// <summary>
    /// Connects Unity's standard scrollbar directly to a multiline input's native scrolling.
    /// </summary>
    /// <param name="input">The multiline editor.</param>
    internal static void AddInputScrollbar(TMP_InputField input)
    {
        GameObject root = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
        root.name = "Scrollbar";
        root.transform.SetParent(input.transform, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.right;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-18, 4);
        rect.offsetMax = new Vector2(-4, -4);
        Scrollbar bar = root.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.TopToBottom;
        StyleScrollbar(bar);
        Stretch(input.textViewport, 10, 6, 28, 6);
        input.verticalScrollbar = bar;
    }

    /// <summary>
    /// Applies the same opaque thumb and track styling to list and text scrollbars.
    /// </summary>
    /// <param name="bar">The standard scrollbar to style.</param>
    private static void StyleScrollbar(Scrollbar bar)
    {
        BindColor(bar.targetGraphic, ContentEditorColorRole.ScrollThumb, bar);
        if (bar.GetComponent<Image>() is Image track)
            BindColor(track, ContentEditorColorRole.ScrollTrack);
    }

    /// <summary>
    /// Creates a compact information target using the shared delayed tooltip component.
    /// </summary>
    /// <param name="parent">The field or caption containing the guidance.</param>
    /// <returns>The authored tooltip target.</returns>
    internal static HelpTooltipView HelpIcon(Transform parent)
    {
        Image target = Surface("Info", parent, Color.clear);
        target.raycastTarget = true;
        target.canvasRenderer.cullTransparentMesh = false;
        Rect(target.rectTransform, 0, 0, 24, 24);
        Image icon = Surface("Glyph", target.transform, ContentEditorColorRole.Muted);
        icon.sprite = LoadIcon("Information");
        icon.preserveAspect = true;
        Rect(icon.rectTransform, 4, 4, 16, 16);
        HelpTooltipView help = target.gameObject.AddComponent<HelpTooltipView>();
        target.gameObject.SetActive(false);
        return help;
    }

    /// <summary>
    /// Loads a monochrome editor icon for palette tinting at any display scale.
    /// </summary>
    /// <param name="name">The icon's asset name.</param>
    /// <returns>The imported UI sprite.</returns>
    internal static Sprite LoadIcon(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/ContentEditor/{name}.png")
        ?? throw new System.IO.FileNotFoundException($"Editor icon '{name}' is missing.");

    /// <summary>
    /// Authors the existing checkbox view using compact editor artwork.
    /// </summary>
    /// <param name="parent">The owning field.</param>
    /// <returns>The checkbox.</returns>
    internal static UICheckboxView Checkbox(Transform parent)
    {
        Image background = Surface("Checkbox", parent, Color.clear);
        background.raycastTarget = true;
        Toggle toggle = background.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = background;
        UICheckboxView view = background.gameObject.AddComponent<UICheckboxView>();
        RawImage frame = Layer("Frame", background.transform).gameObject.AddComponent<RawImage>();
        frame.color = new Color32(210, 210, 210, 255);
        BindColor(frame, ContentEditorColorRole.Border);
        frame.raycastTarget = false;
        Rect(frame.rectTransform, 0, 1, 22, 22);
        Image interior = Surface(
            "FrameInterior",
            frame.transform,
            ContentEditorColorRole.Background
        );
        Rect(interior.rectTransform, 2, 2, 18, 18);
        RectTransform mark = Layer("CheckMark", frame.transform);
        Rect(mark, 1, 1, 20, 20);
        RawImage tick = Layer("Tick", mark).gameObject.AddComponent<RawImage>();
        ConfigureCheckmark(tick);
        BindColor(tick, ContentEditorColorRole.Checkmark);
        Rect(tick.rectTransform, 0, 0, 20, 20);
        TextMeshProUGUI label = Label("Caption", background.transform, "Enabled");
        Rect(label.rectTransform, 34, 0, 120, 24);
        Assign(view, "toggle", toggle);
        Assign(view, "backgroundImage", background);
        Assign(view, "frameImage", frame);
        Assign(view, "checkMarkImage", tick);
        Assign(view, "checkMarkRoot", mark);
        Assign(view, "labelTextField", label);
        return view;
    }

    /// <summary>
    /// Configures a raw-image slot from the native checkmark sprite and its atlas bounds.
    /// </summary>
    /// <param name="image">The authored checkmark image.</param>
    private static void ConfigureCheckmark(RawImage image)
    {
        Sprite sprite = LoadIcon("Checkmark");
        Rect bounds = sprite.textureRect;
        image.texture = sprite.texture;
        image.uvRect = new Rect(
            bounds.x / sprite.texture.width,
            bounds.y / sprite.texture.height,
            bounds.width / sprite.texture.width,
            bounds.height / sprite.texture.height
        );
        image.raycastTarget = false;
    }

    /// <summary>
    /// Authors the shared menu view, including hover submenus and outside-click dismissal.
    /// </summary>
    /// <param name="parent">The canvas.</param>
    /// <returns>The menu view.</returns>
    internal static ContextMenuView Menu(Transform parent)
    {
        RectTransform root = Layer("MenuPopup", parent);
        Stretch(root);
        ContextMenuView menu = root.gameObject.AddComponent<ContextMenuView>();
        RawImage dismiss = Layer("Dismiss", root).gameObject.AddComponent<RawImage>();
        Stretch(dismiss.rectTransform, 0, 36);
        dismiss.color = Color.clear;
        dismiss.raycastTarget = true;
        dismiss.canvasRenderer.cullTransparentMesh = false;
        ContextMenuDismissBoundary boundary =
            dismiss.gameObject.AddComponent<ContextMenuDismissBoundary>();
        Image panel = Surface("PanelTemplate", root, ContentEditorColorRole.Panel);
        Rect(panel.rectTransform, 0, 0, 230, 72);
        ContextMenuPanelView panelView = panel.gameObject.AddComponent<ContextMenuPanelView>();
        string[] edges = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < edges.Length; i++)
        {
            Image border = Surface(edges[i], panel.transform, ContentEditorColorRole.Border);
            Rect(border.rectTransform, 0, 0, i < 2 ? 230 : 1, i < 2 ? 1 : 72);
            Assign(panelView, "border" + edges[i] + "Image", border);
        }
        RectTransform command = Layer("CommandTemplate", panel.transform);
        Rect(command, 0, 0, 230, 36);
        ContextMenuCommandView commandView =
            command.gameObject.AddComponent<ContextMenuCommandView>();
        SerializedObject commandStyle = new SerializedObject(commandView);
        commandStyle.FindProperty("iconVerticalOffset").intValue = 0;
        commandStyle.ApplyModifiedPropertiesWithoutUndo();
        RawImage hit = Layer("HitArea", command).gameObject.AddComponent<RawImage>();
        hit.color = Color.clear;
        Rect(hit.rectTransform, 0, 0, 230, 36);
        RectTransform iconPanel = Layer("IconPanel", command);
        Rect(iconPanel, 0, 0, 32, 36);
        RawImage icon = Layer("Icon", iconPanel).gameObject.AddComponent<RawImage>();
        ConfigureCheckmark(icon);
        BindColor(icon, ContentEditorColorRole.Checkmark);
        Rect(icon.rectTransform, 8, 10, 16, 16);
        TextMeshProUGUI caption = Label("Caption", command, "Command", bindColor: false);
        Rect(caption.rectTransform, 14, 0, 202, 36);
        Assign(commandView, "hitAreaImage", hit);
        Assign(commandView, "iconPanel", iconPanel);
        Assign(commandView, "iconImage", icon);
        Assign(commandView, "commandTextField", caption);
        TextMeshProUGUI shortcut = Label("Shortcut", command, string.Empty, 13, true, false);
        Rect(shortcut.rectTransform, 150, 0, 66, 36);
        shortcut.alignment = TextAlignmentOptions.MidlineRight;
        TextMeshProUGUI chevron = Label("Submenu", command, ">", 14);
        Rect(chevron.rectTransform, 204, 8, 12, 20);
        Assign(commandView, "shortcutTextField", shortcut);
        Assign(commandView, "submenuIndicator", chevron.rectTransform);
        Assign(panelView, "commandPrefab", commandView);
        Assign(menu, "panelPrefab", panelView);
        Assign(menu, "dismissHitAreaImage", dismiss);
        Assign(menu, "dismissBoundary", boundary);
        dismiss.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
        return menu;
    }

    /// <summary>
    /// Positions a fixed rectangle from its parent's top-left corner.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="x">The left offset.</param>
    /// <param name="y">The top offset.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    internal static void Rect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>
    /// Fits a rectangle to its parent's edges with insets.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="top">The top inset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="bottom">The bottom inset.</param>
    internal static void Stretch(
        RectTransform rect,
        float left = 0,
        float top = 0,
        float right = 0,
        float bottom = 0
    )
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>
    /// Assigns an authored component reference through Unity serialization.
    /// </summary>
    /// <param name="target">The receiving component.</param>
    /// <param name="field">The serialized field.</param>
    /// <param name="value">The assigned object.</param>
    internal static void Assign(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new System.MissingMemberException(target.GetType().Name, field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns an ordered collection of authored component references.
    /// </summary>
    /// <param name="target">The receiving component.</param>
    /// <param name="field">The serialized collection.</param>
    /// <param name="values">The components in display order.</param>
    internal static void Assign(
        Object target,
        string field,
        System.Collections.Generic.IReadOnlyList<Object> values
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(field)
            ?? throw new System.MissingMemberException(target.GetType().Name, field);
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
