using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors shared menu frames and controls for application menu prefabs.
/// </summary>
public static class MenuPrefabBuilder
{
    private const string _windowPrefabPath = "Assets/Prefabs/UI/Common/MenuWindow.prefab";
    private const string _navigationPrefabPath =
        "Assets/Prefabs/UI/Common/MenuNavigationButton.prefab";
    private const string _actionPrefabPath = "Assets/Prefabs/UI/Common/MenuActionButton.prefab";
    private const int _navigationRowHeight = 28;
    internal const string _menuPanelAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_panel_background";
    internal const string _menuRowAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_row_background";
    internal const string _menuRowActiveAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_row_selected_background";
    internal const string _menuBadgeAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_badge_background";
    internal const string _menuFrameGlowAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_frame_overlay";
    internal const string _scrollAreaPrefabPath = "Assets/Prefabs/UI/Common/MenuScrollArea.prefab";
    internal const string _textInputPrefabPath = "Assets/Prefabs/UI/Common/TextInput.prefab";
    internal static readonly Vector4 _panelBorder = new Vector4(7f, 7f, 7f, 7f);
    internal static readonly Vector4 _badgeBorder = new Vector4(6f, 6f, 6f, 6f);
    internal static readonly Color32 _buttonSurfaceColor = new Color32(35, 56, 80, 255);

    /// <summary>
    /// Builds the reusable window shell and button prefabs consumed by application menus.
    /// </summary>
    internal static void RebuildSharedPrefabs()
    {
        BuildScrollAreaPrefab();
        BuildWindowPrefab();
        BuildButtonPrefab(_navigationPrefabPath, _menuRowAddress, _panelBorder, 163, 28, 13);
        BuildButtonPrefab(_actionPrefabPath, _menuBadgeAddress, _badgeBorder, 76, 22, 11);
    }

    /// <summary>
    /// Authors the menu theme once on the shared scroll control without replacing its behavior.
    /// </summary>
    private static void BuildScrollAreaPrefab()
    {
        ScrollAreaView view = CommonUIPrefabBuilder.CreateScrollAreaView(
            "Assets/Prefabs/UI/Common/ScrollArea.prefab",
            null,
            "MenuScrollArea",
            new RectInt(0, 0, 1, 1),
            new RectInt(0, 0, 1, 1),
            new RectInt(0, 0, 12, 24),
            out _
        );
        Scrollbar scrollbar = view.GetComponentInChildren<Scrollbar>(true);
        Image thumb = CreateScrollControlSurface(scrollbar.handleRect);
        ApplyMenuSurfaceFeedback(scrollbar, thumb);
        CreateScrollThumbGrip(thumb.rectTransform);
        foreach (Button button in scrollbar.GetComponentsInChildren<Button>(true))
        {
            RectTransform rect = button.transform as RectTransform;
            SetSourceRect(rect, 0, 0, 12, 12);
            Image surface = CreateScrollControlSurface(rect);
            ApplyMenuSurfaceFeedback(button, surface);
            TextMeshProUGUI arrow = CreateTextLabel("Arrow", rect);
            arrow.text = button.name == "ScrollUpButtonImage" ? "\u25b2" : "\u25bc";
            arrow.fontSize = 8;
            arrow.color = new Color(0.373f, 0.659f, 0.925f);
            arrow.alignment = TextAlignmentOptions.Midline;
            FillParent(arrow.rectTransform);
        }

        Image track = FindRequiredChild<Image>(scrollbar.transform, "TrackBackgroundImage");
        track.color = new Color32(15, 23, 33, 255);
        track.raycastTarget = false;
        view.SetLayout(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12, 24));
        SaveGeneratedPrefabAsset(view.gameObject, _scrollAreaPrefabPath);
        UnityEngine.Object.DestroyImmediate(view.gameObject);
    }

    /// <summary>
    /// Replaces a texture-stretched scroll visual with the menu's sliced, content-bound surface.
    /// </summary>
    /// <param name="parent">The existing handle or arrow rectangle.</param>
    /// <returns>The surface receiving pointer and hover feedback.</returns>
    private static Image CreateScrollControlSurface(RectTransform parent)
    {
        UnityEngine.Object.DestroyImmediate(parent.GetComponent<ContentPressVisualBinding>());
        UnityEngine.Object.DestroyImmediate(parent.GetComponent<RawImagePressVisual>());
        UnityEngine.Object.DestroyImmediate(parent.GetComponent<ContentTextureBinding>());
        UnityEngine.Object.DestroyImmediate(parent.GetComponent<RawImage>());
        Image surface = CreateSlicedImage(
            "Surface",
            parent,
            _menuRowAddress,
            _panelBorder,
            0,
            0,
            12,
            12,
            Color.white
        );
        FillParent(surface.rectTransform);
        surface.raycastTarget = true;
        return CreateMenuControlSurface(surface);
    }

    /// <summary>
    /// Adds a centered grip that stays legible as the thumb changes height.
    /// </summary>
    /// <param name="handle">The proportional thumb rectangle.</param>
    private static void CreateScrollThumbGrip(RectTransform handle)
    {
        RectTransform grip = CreateLayer("Grip", handle).GetComponent<RectTransform>();
        grip.anchorMin = grip.anchorMax = grip.pivot = new Vector2(0.5f, 0.5f);
        grip.sizeDelta = new Vector2(6, 5);
        grip.anchoredPosition = Vector2.zero;
        for (int index = 0; index < 3; index++)
        {
            GameObject line = CreateLayer($"Line{index}", grip);
            SetSourceRect(line.GetComponent<RectTransform>(), 0, index * 2, 6, 1);
            Image image = line.AddComponent<Image>();
            image.color = new Color(0.373f, 0.659f, 0.925f);
            image.raycastTarget = false;
        }
    }

    /// <summary>
    /// Instantiates the shared menu shell with centered header fields.
    /// </summary>
    /// <param name="parent">The window that owns the content.</param>
    /// <param name="heading">The window caption.</param>
    /// <param name="header">Receives the centered window caption.</param>
    /// <param name="pageTitle">Receives the centered page caption.</param>
    /// <returns>The shared source-space content root.</returns>
    internal static RectTransform CreateMenuWindowContent(
        Transform parent,
        string heading,
        out TextMeshProUGUI header,
        out TextMeshProUGUI pageTitle
    )
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(_windowPrefabPath)
            ?? throw new MissingReferenceException(_windowPrefabPath);
        RectTransform root = (
            (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
        ).GetComponent<RectTransform>();
        root.name = "ContentRoot";
        root.localScale = new Vector3(0.719f, 0.719f, 1f);
        header = FindRequiredChild<TextMeshProUGUI>(root, "HeaderTextField");
        header.text = heading;
        pageTitle = FindRequiredChild<TextMeshProUGUI>(root, "PageTitleTextField");
        return root;
    }

    /// <summary>
    /// Authors the menu frame once instead of rebuilding its artwork in each feature.
    /// </summary>
    private static void BuildWindowPrefab()
    {
        GameObject root = CreateLayer("MenuWindow", null);
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, 632, 474);
        RawImage background = CreateRawImage(
            "BackgroundImage",
            root.transform,
            "Application/OptionsMenu/UI/ui_settingsmenu_background",
            0,
            0,
            632,
            474
        );
        background.raycastTarget = true;
        CreateSlicedImage(
            "FrameGlow",
            root.transform,
            _menuFrameGlowAddress,
            _panelBorder,
            2,
            2,
            628,
            470,
            Color.white
        );
        CreateSlicedImage(
            "NavPanel",
            root.transform,
            _menuPanelAddress,
            _panelBorder,
            23,
            70,
            192,
            364,
            Color.white
        );
        CreateSlicedImage(
            "ContentPanel",
            root.transform,
            _menuPanelAddress,
            _panelBorder,
            228,
            70,
            382,
            364,
            Color.white
        );
        TextMeshProUGUI header = CreateTextLabel("HeaderTextField", root.transform);
        header.color = new Color(0.875f, 0.910f, 0.941f);
        header.fontSize = 22;
        header.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(header.rectTransform, 41, 32, 155, 26);
        TextMeshProUGUI page = CreateTextLabel("PageTitleTextField", root.transform);
        page.text = string.Empty;
        page.color = new Color(0.373f, 0.659f, 0.925f);
        page.fontSize = 14;
        page.alignment = TextAlignmentOptions.Midline;
        SetSourceRect(page.rectTransform, 240, 36, 357, 22);
        SaveGeneratedPrefabAsset(root, _windowPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Authors one shared cut-corner button with a stretchable caption and pressed feedback.
    /// </summary>
    /// <param name="path">The generated prefab path.</param>
    /// <param name="address">The frame artwork address.</param>
    /// <param name="border">The nine-slice border.</param>
    /// <param name="width">The default source-space width.</param>
    /// <param name="height">The default source-space height.</param>
    /// <param name="fontSize">The default caption size.</param>
    private static void BuildButtonPrefab(
        string path,
        string address,
        Vector4 border,
        int width,
        int height,
        int fontSize
    )
    {
        Button button = CreateSlicedButton(
            "MenuButton",
            null,
            address,
            border,
            0,
            0,
            width,
            height,
            Color.white
        );
        Graphic frame = button.targetGraphic;
        Image surface = CreateMenuButtonSurface(button);
        TextMeshProUGUI caption = CreateTextLabel("Label", button.transform);
        caption.fontSize = fontSize;
        caption.alignment = TextAlignmentOptions.Midline;
        caption.text = string.Empty;
        caption.color = new Color(0.875f, 0.910f, 0.941f);
        FillParent(caption.rectTransform);
        ApplyMenuSurfaceFeedback(button, surface);
        AddMenuButtonBorder(frame);
        SaveGeneratedPrefabAsset(button.gameObject, path);
        UnityEngine.Object.DestroyImmediate(button.gameObject);
    }

    /// <summary>
    /// Adds pronounced hover feedback to a selectable control's visible cut-corner surface.
    /// </summary>
    /// <param name="selectable">The selectable control to restyle.</param>
    /// <param name="surface">The visible surface receiving the tint.</param>
    internal static void ApplyMenuSurfaceFeedback(Selectable selectable, Graphic surface)
    {
        if (selectable == null || surface == null)
            return;

        selectable.targetGraphic = surface;
        selectable.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.55f, 1.55f, 1.55f, 1f);
        colors.pressedColor = new Color(1.85f, 1.85f, 1.85f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        selectable.colors = colors;
    }

    /// <summary>
    /// Creates the framed restore control shared by application settings rows.
    /// </summary>
    /// <param name="name">The control name.</param>
    /// <param name="parent">The owning row.</param>
    /// <param name="x">The source-space left position.</param>
    /// <param name="y">The source-space top position.</param>
    /// <returns>The configured restore button.</returns>
    internal static Button CreateMenuRestoreButton(string name, Transform parent, int x, int y)
    {
        Image frame = CreateSlicedImage(
            name,
            parent,
            _menuBadgeAddress,
            _badgeBorder,
            x,
            y,
            30,
            16,
            Color.white
        );
        frame.raycastTarget = true;
        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        ApplyMenuFillButtonFeedback(button);
        RawImage icon = CreateRawImage(
            "Icon",
            frame.transform,
            "Application/OptionsMenu/UI/ui_settingsmenu_restore_default_icon",
            9,
            2,
            12,
            12
        );
        icon.raycastTarget = false;
        return button;
    }

    /// <summary>
    /// Applies the shared filled-control hover and press colors.
    /// </summary>
    /// <param name="button">The control receiving pointer feedback.</param>
    internal static void ApplyMenuFillButtonFeedback(Button button)
    {
        if (button == null)
            return;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.34f, 0.44f, 0.60f);
        colors.highlightedColor = new Color(0.49f, 0.61f, 0.80f);
        colors.pressedColor = new Color(0.66f, 0.79f, 0.98f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.22f, 0.24f, 0.28f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    /// <summary>
    /// Adds a border to a button.
    /// </summary>
    /// <param name="graphic">The button fill graphic to outline.</param>
    internal static void AddMenuButtonBorder(Graphic graphic)
    {
        if (graphic == null)
            return;

        Outline outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.60f, 0.69f, 0.85f, 0.9f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = false;
    }

    /// <summary>
    /// Instantiates a reusable menu navigation button with configurable caption alignment.
    /// </summary>
    /// <param name="name">The button object name.</param>
    /// <param name="parent">The content-root transform.</param>
    /// <param name="label">The caps row caption.</param>
    /// <param name="x">The source-space row left position.</param>
    /// <param name="y">The source-space row top.</param>
    /// <param name="color">The label color.</param>
    /// <param name="labelField">Receives the row label.</param>
    /// <param name="surface">Receives the row's tinted surface.</param>
    /// <param name="alignment">The caption's horizontal and vertical alignment.</param>
    /// <returns>The configured button.</returns>
    internal static Button CreateMenuNavigationRow(
        string name,
        Transform parent,
        string label,
        int x,
        int y,
        Color color,
        out TextMeshProUGUI labelField,
        out Image surface,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft
    )
    {
        Button button = InstantiatePrefabComponent<Button>(_navigationPrefabPath, parent);
        button.name = name;
        SetSourceRect((RectTransform)button.transform, x, y, 163, _navigationRowHeight);
        surface = button.transform.Find("SurfaceMask/Surface").GetComponent<Image>();
        labelField = FindRequiredChild<TextMeshProUGUI>(button.transform, "Label");
        labelField.name = $"{name}Label";
        labelField.text = label;
        labelField.color = color;
        labelField.fontSize = 13;
        labelField.enableAutoSizing = true;
        labelField.fontSizeMin = 11;
        labelField.fontSizeMax = 13;
        labelField.alignment = alignment;
        labelField.rectTransform.offsetMin = new Vector2(14f, 0f);
        labelField.rectTransform.offsetMax = new Vector2(-14f, 0f);
        return button;
    }

    /// <summary>
    /// Creates a settings button.
    /// </summary>
    /// <param name="parent">The settings-actions container.</param>
    /// <param name="name">The button object name.</param>
    /// <param name="label">The caps caption.</param>
    /// <param name="x">The source-space left position.</param>
    /// <param name="color">The label color.</param>
    /// <param name="width">The source-space button width.</param>
    /// <returns>The configured button.</returns>
    internal static Button CreateMenuActionButton(
        Transform parent,
        string name,
        string label,
        int x,
        Color color,
        int width = 76
    )
    {
        Button button = InstantiatePrefabComponent<Button>(_actionPrefabPath, parent);
        button.name = name;
        SetSourceRect((RectTransform)button.transform, x, 408, width, 22);
        TextMeshProUGUI text = FindRequiredChild<TextMeshProUGUI>(button.transform, "Label");
        text.text = label;
        text.color = color;
        text.fontSize = 11;
        text.alignment = TextAlignmentOptions.Midline;
        return button;
    }

    /// <summary>
    /// Adds a high-contrast surface clipped to the button's authored cut-corner shape.
    /// </summary>
    /// <param name="button">The button receiving the surface.</param>
    /// <returns>The clipped surface image.</returns>
    internal static Image CreateMenuButtonSurface(Button button)
    {
        return CreateMenuControlSurface(button.targetGraphic as Image);
    }

    /// <summary>
    /// Fills a menu control's sliced frame with the shared hoverable surface.
    /// </summary>
    /// <param name="frame">The content-bound frame defining the control's shape.</param>
    /// <returns>The clipped surface receiving interaction feedback.</returns>
    private static Image CreateMenuControlSurface(Image frame)
    {
        GameObject maskObject = new GameObject(
            "SurfaceMask",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Mask)
        );
        maskObject.transform.SetParent(frame.transform, false);
        maskObject.transform.SetAsFirstSibling();
        RectTransform maskRect = maskObject.GetComponent<RectTransform>();
        FillParent(maskRect);

        Image maskImage = maskObject.GetComponent<Image>();
        maskImage.sprite = frame.sprite;
        maskImage.type = frame.type;
        maskImage.pixelsPerUnitMultiplier = frame.pixelsPerUnitMultiplier;
        maskImage.raycastTarget = false;
        ContentSpriteBinding frameBinding = frame.GetComponent<ContentSpriteBinding>();
        maskObject
            .AddComponent<ContentSpriteBinding>()
            .SetAddress(frameBinding.Address, frameBinding.Border);
        Mask mask = maskObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject surface = new GameObject(
            "Surface",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        surface.transform.SetParent(maskObject.transform, false);
        RectTransform surfaceRect = surface.GetComponent<RectTransform>();
        FillParent(surfaceRect);
        Image surfaceImage = surface.GetComponent<Image>();
        surfaceImage.color = _buttonSurfaceColor;
        surfaceImage.raycastTarget = false;
        return surfaceImage;
    }

    /// <summary>
    /// Enables a generated runtime component and rejects a missing reference.
    /// </summary>
    /// <typeparam name="T">The runtime component type.</typeparam>
    /// <param name="component">The component to enable.</param>
    /// <returns>The enabled component.</returns>
    internal static T EnableRuntimeComponent<T>(T component)
        where T : MonoBehaviour
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        component.enabled = true;
        return component;
    }

    /// <summary>
    /// Enables a generated window and gives it an interactive canvas group.
    /// </summary>
    /// <param name="window">The window root being authored.</param>
    internal static void ConfigureWindowRoot(UIWindow window)
    {
        EnableRuntimeComponent(window);
        CanvasGroup inputGroup = window.GetComponent<CanvasGroup>();
        if (inputGroup == null)
            inputGroup = window.gameObject.AddComponent<CanvasGroup>();

        inputGroup.alpha = 1f;
        inputGroup.interactable = true;
        inputGroup.blocksRaycasts = true;
        inputGroup.ignoreParentGroups = false;
        AssignReference(window, "inputGroup", inputGroup);
    }

    /// <summary>
    /// Creates and positions a sliced sprite image.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The image's parent.</param>
    /// <param name="spriteAddress">The stable content address of the sliced sprite.</param>
    /// <param name="border">The sprite's explicit nine-slice border.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="color">The image tint.</param>
    /// <returns>The generated image.</returns>
    internal static Image CreateSlicedImage(
        string name,
        Transform parent,
        string spriteAddress,
        Vector4 border,
        int x,
        int y,
        int width,
        int height,
        Color color
    )
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = LoadSprite(spriteAddress, border);
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        ContentSpriteBinding binding = imageObject.AddComponent<ContentSpriteBinding>();
        binding.SetAddress(spriteAddress, border);
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Creates a sliced image configured as a non-transitioning button.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The button's parent.</param>
    /// <param name="spriteAddress">The stable content address of the sliced sprite.</param>
    /// <param name="border">The sprite's explicit nine-slice border.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="color">The image tint.</param>
    /// <returns>The generated button.</returns>
    internal static Button CreateSlicedButton(
        string name,
        Transform parent,
        string spriteAddress,
        Vector4 border,
        int x,
        int y,
        int width,
        int height,
        Color color
    )
    {
        Image image = CreateSlicedImage(
            name,
            parent,
            spriteAddress,
            border,
            x,
            y,
            width,
            height,
            color
        );
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    /// <summary>
    /// Creates a text label with the Options menu's baseline presentation.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The label's parent.</param>
    /// <returns>The generated text label.</returns>
    internal static TextMeshProUGUI CreateTextLabel(string name, Transform parent)
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
        label.text = "Corellian";
        label.color = Color.yellow;
        label.fontSize = 13;
        label.alignment = TextAlignmentOptions.Top;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        Shadow shadow = labelObject.GetComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
        return label;
    }

    /// <summary>
    /// Creates an optionally textured raw-image surface used by menu controls.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The image's parent.</param>
    /// <param name="texturePath">The optional content texture address.</param>
    /// <returns>The generated raw image.</returns>
    internal static RawImage CreateRawButton(
        string name,
        Transform parent,
        string texturePath = null
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
        image.texture = string.IsNullOrEmpty(texturePath) ? null : LoadTexture(texturePath);
        image.raycastTarget = false;
        if (!string.IsNullOrEmpty(texturePath))
            AttachTextureBinding(image, texturePath);
        if (image.texture != null)
        {
            Vector2Int size = UILayout.GetTextureSourceSize(image.texture);
            SetSourceRect(image.rectTransform, 0, 0, size.x, size.y);
        }
        return image;
    }

    /// <summary>
    /// Creates a textured raw image at a source-space rectangle.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The image's parent.</param>
    /// <param name="texturePath">The content texture address.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The generated raw image.</returns>
    internal static RawImage CreateRawImage(
        string name,
        Transform parent,
        string texturePath,
        int x,
        int y,
        int width,
        int height
    )
    {
        RawImage image = CreateRawButton(name, parent, texturePath);
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Instantiates and configures the shared text-input prefab for the Options layout.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The field's parent.</param>
    /// <param name="placeholderText">The empty-field prompt.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The configured input field.</returns>
    internal static TMP_InputField CreateTextInputField(
        string name,
        Transform parent,
        string placeholderText,
        int x,
        int y,
        int width,
        int height
    )
    {
        TMP_InputField input = InstantiatePrefabComponent<TMP_InputField>(
            _textInputPrefabPath,
            parent
        );
        input.gameObject.name = name;
        RectTransform rect = input.transform as RectTransform;
        SetSourceRect(rect, x, y, width, height);

        Image image = input.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        TextMeshProUGUI text = input.textComponent as TextMeshProUGUI;
        if (text == null)
            throw new MissingReferenceException($"{name}/Text is missing.");
        text.text = string.Empty;
        text.color = Color.white;
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(text.rectTransform, 2, 1, width - 2, height - 2);

        TextMeshProUGUI placeholder = input.placeholder as TextMeshProUGUI;
        if (placeholder == null)
            throw new MissingReferenceException($"{name}/Placeholder is missing.");
        placeholder.text = placeholderText;
        placeholder.color = Color.white;
        placeholder.fontSize = 12;
        placeholder.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(placeholder.rectTransform, 2, 1, width - 2, height - 2);

        input.enabled = true;
        input.targetGraphic = image;
        input.transition = Selectable.Transition.None;
        input.lineType = TMP_InputField.LineType.SingleLine;
        if (input.textViewport == null || input.textViewport == rect)
            throw new MissingReferenceException($"{name}/TextViewport is missing or invalid.");
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    /// <summary>
    /// Instantiates and lays out a shared scroll area for an Options list.
    /// </summary>
    /// <param name="parent">The scroll area's parent.</param>
    /// <param name="name">The generated object name.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="viewportX">The viewport left coordinate within the scroll area.</param>
    /// <param name="viewportY">The viewport top coordinate within the scroll area.</param>
    /// <param name="viewportWidth">The viewport width.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    /// <param name="scrollbarX">The scrollbar left coordinate within the scroll area.</param>
    /// <param name="scrollbarY">The scrollbar top coordinate within the scroll area.</param>
    /// <param name="scrollbarWidth">The scrollbar width.</param>
    /// <param name="scrollbarHeight">The scrollbar height.</param>
    /// <param name="contentRoot">The generated content container.</param>
    /// <returns>The configured scroll-area view.</returns>
    internal static ScrollAreaView CreateScrollAreaView(
        Transform parent,
        string name,
        int x,
        int y,
        int width,
        int height,
        int viewportX,
        int viewportY,
        int viewportWidth,
        int viewportHeight,
        int scrollbarX,
        int scrollbarY,
        int scrollbarWidth,
        int scrollbarHeight,
        out RectTransform contentRoot
    )
    {
        return CommonUIPrefabBuilder.CreateScrollAreaView(
            _scrollAreaPrefabPath,
            parent,
            name,
            new RectInt(x, y, width, height),
            new RectInt(viewportX, viewportY, viewportWidth, viewportHeight),
            new RectInt(scrollbarX, scrollbarY, scrollbarWidth, scrollbarHeight),
            out contentRoot
        );
    }

    /// <summary>
    /// Applies a top-left anchored source-pixel rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to place.</param>
    /// <param name="x">The source-space left coordinate.</param>
    /// <param name="y">The source-space top coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    internal static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Stretches a rectangle across its parent without offsets.
    /// </summary>
    /// <param name="rect">The rectangle to stretch.</param>
    internal static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Assigns an object reference to a serialized component field.
    /// </summary>
    /// <param name="target">The component containing the serialized field.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The object reference to assign.</param>
    internal static void AssignReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        FindRequiredProperty(target, serializedObject, propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Saves a generated hierarchy as a prefab and rejects an unsuccessful write.
    /// </summary>
    /// <param name="root">The generated hierarchy root.</param>
    /// <param name="path">The destination prefab path.</param>
    /// <returns>The saved prefab asset.</returns>
    internal static GameObject SaveGeneratedPrefabAsset(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success || saved == null)
            throw new InvalidOperationException($"Failed to save generated prefab at {path}.");
        return saved;
    }

    /// <summary>
    /// Creates an empty rectangular layer under a parent.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The layer's parent.</param>
    /// <returns>The generated layer.</returns>
    internal static GameObject CreateLayer(string name, Transform parent)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(parent, false);
        return layer;
    }

    /// <summary>
    /// Instantiates a shared nested prefab and returns its required component.
    /// </summary>
    /// <typeparam name="T">The required root component type.</typeparam>
    /// <param name="path">The shared prefab path.</param>
    /// <param name="parent">The nested instance's parent.</param>
    /// <returns>The enabled component on the nested instance.</returns>
    internal static T InstantiatePrefabComponent<T>(string path, Transform parent)
        where T : MonoBehaviour
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new MissingReferenceException($"Prefab asset is missing at {path}.");
        T prefabComponent = prefab.GetComponent<T>();
        if (prefabComponent == null)
            throw new MissingReferenceException(
                $"Prefab asset at {path} is missing {typeof(T).Name}."
            );

        GameObject instance = (GameObject)
            PrefabUtility.InstantiatePrefab(prefabComponent.gameObject, parent);
        T component = instance.GetComponent<T>();
        if (component == null)
            throw new MissingReferenceException(
                $"Nested prefab instance from {path} is missing {typeof(T).Name}."
            );
        component.enabled = true;
        return component;
    }

    /// <summary>
    /// Finds a required component on a named direct child.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="parent">The hierarchy parent to search.</param>
    /// <param name="childName">The direct child name.</param>
    /// <returns>The required child component.</returns>
    internal static T FindRequiredChild<T>(Transform parent, string childName)
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
    /// Loads a texture from the active development content pack.
    /// </summary>
    /// <param name="path">The content texture address.</param>
    /// <returns>The loaded texture.</returns>
    internal static Texture2D LoadTexture(string path)
    {
        return ContentPackEditor.Assets.GetTexture(path);
    }

    /// <summary>
    /// Loads a sprite with an explicit border from the active development content pack.
    /// </summary>
    /// <param name="address">The content sprite address.</param>
    /// <param name="border">The sprite's nine-slice border in pixels.</param>
    /// <returns>The loaded sprite.</returns>
    internal static Sprite LoadSprite(string address, Vector4 border)
    {
        return ContentPackEditor.Assets.GetSprite(address, border);
    }

    /// <summary>
    /// Replaces a raw image's runtime content binding with the requested address.
    /// </summary>
    /// <param name="image">The image receiving the runtime binding.</param>
    /// <param name="texturePath">The content texture address.</param>
    internal static void AttachTextureBinding(RawImage image, string texturePath)
    {
        string address = ToContentAddress(texturePath);
        if (image.TryGetComponent(out RawImagePressVisual _))
        {
            ContentTextureBinding textureBinding = image.GetComponent<ContentTextureBinding>();
            if (textureBinding != null)
                UnityEngine.Object.DestroyImmediate(textureBinding);

            ContentPressVisualBinding pressBinding =
                image.GetComponent<ContentPressVisualBinding>()
                ?? image.gameObject.AddComponent<ContentPressVisualBinding>();
            pressBinding.SetAddresses(address, null);
            return;
        }

        ContentTextureBinding existing = image.GetComponent<ContentTextureBinding>();
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);
        ContentTextureBinding binding = image.gameObject.AddComponent<ContentTextureBinding>();
        binding.SetAddress(address);
    }

    /// <summary>
    /// Removes a texture extension to produce a runtime content address.
    /// </summary>
    /// <param name="texturePath">The authored texture path.</param>
    /// <returns>The extensionless runtime address.</returns>
    internal static string ToContentAddress(string texturePath)
    {
        int separatorIndex = texturePath.LastIndexOf('/');
        int extensionIndex = texturePath.LastIndexOf('.');
        return extensionIndex > separatorIndex ? texturePath[..extensionIndex] : texturePath;
    }

    /// <summary>
    /// Finds a serialized field or fails the UI build when the field is missing.
    /// </summary>
    /// <param name="target">The component expected to contain the field.</param>
    /// <param name="serializedObject">The serialized representation of the component.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <returns>The required serialized field.</returns>
    internal static SerializedProperty FindRequiredProperty(
        UnityEngine.Object target,
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        return property;
    }
}
