using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the reusable UI control prefabs consumed by generated Strategy and Save Menu prefabs.
/// </summary>
public static class CommonUIPrefabBuilder
{
    private const string _scrollAreaPrefabPath = "Assets/Prefabs/UI/Common/ScrollArea.prefab";
    private const string _textInputPrefabPath = "Assets/Prefabs/UI/Common/TextInput.prefab";
    private const string _scrollUpTexturePath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_scrollbar_arrow_up.png";
    private const string _scrollDownTexturePath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_scrollbar_arrow_pressed_2.png";
    private const string _scrollHandleTexturePath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_scrollbar_middle.png";
    private const int _defaultScrollbarWidth = 13;
    private const int _defaultArrowHeight = 9;
    private const int _defaultInputWidth = 3;
    private const int _defaultControlHeight = 1;

    /// <summary>
    /// Rebuilds every reusable generated control prefab.
    /// </summary>
    [MenuItem("Rebellion/UI/Rebuild Common Control Prefabs")]
    public static void RebuildSharedControlPrefabs()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildScrollAreaPrefab();
        BuildTextInputPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Authors the canonical scroll-area hierarchy and serialized control references.
    /// </summary>
    /// <returns>The saved scroll-area prefab root.</returns>
    private static GameObject BuildScrollAreaPrefab()
    {
        int scrollbarHeight = _defaultArrowHeight * 2;
        GameObject root = CreateRectObject("ScrollArea");
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, 1, 1);
        ScrollAreaView view = root.AddComponent<ScrollAreaView>();
        view.enabled = true;

        GameObject scrollObject = CreateRectObject("ScrollRect", root.transform);
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        ScrollAreaDragRelay dragRelay = scrollObject.AddComponent<ScrollAreaDragRelay>();
        dragRelay.enabled = true;
        SetSourceRect(scrollObject.GetComponent<RectTransform>(), 0, 0, 1, 1);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        GameObject viewport = CreateRectObject("Viewport", scrollObject.transform);
        viewport.AddComponent<RectMask2D>();
        Image viewportImage = viewport.AddComponent<Image>();
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetSourceRect(viewportRect, 0, 0, 1, 1);
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        GameObject content = CreateRectObject("Content", viewport.transform);
        RectTransform contentRoot = content.GetComponent<RectTransform>();
        SetSourceRect(contentRoot, 0, 0, 1, 1);
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;

        GameObject scrollbarObject = CreateRectObject("Scrollbar", root.transform);
        Image scrollbarBackground = scrollbarObject.AddComponent<Image>();
        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        SetSourceRect(
            scrollbarObject.GetComponent<RectTransform>(),
            0,
            0,
            _defaultScrollbarWidth,
            scrollbarHeight
        );
        scrollbarBackground.color = Color.clear;
        scrollbarBackground.raycastTarget = true;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;

        GameObject track = CreateRectObject("TrackBackgroundImage", scrollbarObject.transform);
        Image trackBackground = track.AddComponent<Image>();
        trackBackground.color = Color.black;
        SetSourceRect(
            track.GetComponent<RectTransform>(),
            0,
            _defaultArrowHeight,
            _defaultScrollbarWidth,
            0
        );

        RawImage scrollUpImage = CreateRawImage(
            "ScrollUpButtonImage",
            scrollbarObject.transform,
            _scrollUpTexturePath
        );
        SetSourceRect(
            scrollUpImage.rectTransform,
            0,
            0,
            _defaultScrollbarWidth,
            _defaultArrowHeight
        );
        scrollUpImage.raycastTarget = true;
        Button scrollUpButton = CreateButton(scrollUpImage);

        RawImage scrollDownImage = CreateRawImage(
            "ScrollDownButtonImage",
            scrollbarObject.transform,
            _scrollDownTexturePath
        );
        SetSourceRect(
            scrollDownImage.rectTransform,
            0,
            _defaultArrowHeight,
            _defaultScrollbarWidth,
            _defaultArrowHeight
        );
        scrollDownImage.raycastTarget = true;
        Button scrollDownButton = CreateButton(scrollDownImage);

        GameObject slidingArea = CreateRectObject("SlidingArea", scrollbarObject.transform);
        RectTransform slidingAreaRoot = slidingArea.GetComponent<RectTransform>();
        SetSourceRect(slidingAreaRoot, 0, _defaultArrowHeight, _defaultScrollbarWidth, 0);
        RawImage handleImage = CreateRawImage(
            "Handle",
            slidingArea.transform,
            _scrollHandleTexturePath
        );
        FillParent(handleImage.rectTransform);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = handleImage.rectTransform;
        scrollbar.targetGraphic = handleImage;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        AssignReference(view, "scrollRect", scrollRect);
        AssignReference(view, "contentRoot", contentRoot);
        AssignReference(view, "scrollbar", scrollbar);
        AssignReference(view, "trackBackgroundRoot", track.transform as RectTransform);
        AssignReference(view, "slidingAreaRoot", slidingAreaRoot);
        AssignReference(view, "scrollUpButton", scrollUpButton);
        AssignReference(view, "scrollDownButton", scrollDownButton);
        AssignReference(view, "dragRelay", dragRelay);

        return SavePrefab(root, _scrollAreaPrefabPath);
    }

    /// <summary>
    /// Authors the canonical transparent single-line TMP input hierarchy.
    /// </summary>
    /// <returns>The saved text-input prefab root.</returns>
    private static GameObject BuildTextInputPrefab()
    {
        GameObject root = CreateRectObject("TextInput");
        root.AddComponent<CanvasRenderer>();
        Image image = root.AddComponent<Image>();
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.enabled = true;
        SetSourceRect(
            root.GetComponent<RectTransform>(),
            0,
            0,
            _defaultInputWidth,
            _defaultControlHeight
        );
        image.color = Color.clear;
        image.raycastTarget = true;

        TextMeshProUGUI text = CreateInputText("Text", root.transform, string.Empty);
        TextMeshProUGUI placeholder = CreateInputText("Placeholder", root.transform, string.Empty);
        SetSourceRect(text.rectTransform, 2, 0, 1, _defaultControlHeight);
        SetSourceRect(placeholder.rectTransform, 2, 0, 1, _defaultControlHeight);

        input.targetGraphic = image;
        input.transition = Selectable.Transition.None;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.textViewport = root.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = placeholder;

        return SavePrefab(root, _textInputPrefabPath);
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
    /// Creates a RawImage using one required texture asset.
    /// </summary>
    /// <param name="name">The image GameObject name.</param>
    /// <param name="parent">The image parent transform.</param>
    /// <param name="texturePath">The texture asset path.</param>
    /// <returns>The created RawImage.</returns>
    private static RawImage CreateRawImage(string name, Transform parent, string texturePath)
    {
        GameObject imageObject = CreateRectObject(name, parent);
        imageObject.AddComponent<CanvasRenderer>();
        RawImage image = imageObject.AddComponent<RawImage>();
        image.texture = LoadRequiredTexture(texturePath);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Authors a RawImage-backed button and its explicitly serialized pressed-state visual.
    /// </summary>
    /// <param name="image">The button's target image.</param>
    /// <returns>The configured button.</returns>
    private static Button CreateButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        RawImagePressVisual pressVisual = image.gameObject.AddComponent<RawImagePressVisual>();
        pressVisual.enabled = true;
        AssignReference(pressVisual, "image", image);
        AssignReference(pressVisual, "button", button);
        pressVisual.SetTextures(image.texture, null);
        return button;
    }

    /// <summary>
    /// Creates one input text child with the established TMP and shadow styling.
    /// </summary>
    /// <param name="name">The text GameObject name.</param>
    /// <param name="parent">The text parent transform.</param>
    /// <param name="content">The initial text content.</param>
    /// <returns>The created TMP component.</returns>
    private static TextMeshProUGUI CreateInputText(string name, Transform parent, string content)
    {
        GameObject textObject = CreateRectObject(name, parent);
        textObject.AddComponent<CanvasRenderer>();
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        Shadow shadow = textObject.AddComponent<Shadow>();

        text.text = content;
        text.color = Color.white;
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    /// <summary>
    /// Loads a required texture asset.
    /// </summary>
    /// <param name="path">The texture asset path.</param>
    /// <returns>The loaded texture.</returns>
    private static Texture2D LoadRequiredTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new FileNotFoundException(path);
        return texture;
    }

    /// <summary>
    /// Assigns one private serialized object reference without relying on runtime reflection.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The object reference value.</param>
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
    /// Applies a source-space rectangle to a generated RectTransform.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="x">The source x-coordinate.</param>
    /// <param name="y">The source y-coordinate.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
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
    /// Stretches a RectTransform across its parent without offsets.
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
    /// Saves one generated prefab and destroys its temporary authoring hierarchy.
    /// </summary>
    /// <param name="root">The temporary prefab root.</param>
    /// <param name="path">The destination prefab path.</param>
    /// <returns>The saved prefab asset root.</returns>
    private static GameObject SavePrefab(GameObject root, string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        UnityEngine.Object.DestroyImmediate(root);
        if (!success || saved == null)
            throw new InvalidOperationException($"Failed to save prefab at {path}.");
        return saved;
    }
}
