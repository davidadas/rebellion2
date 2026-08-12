using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides scene-agnostic primitives for generated UI prefab builders.
/// </summary>
internal static class UIPrefabAuthoring
{
    private const string _scrollAreaPrefabPath = "Assets/Prefabs/UI/Common/ScrollArea.prefab";
    private const string _textInputPrefabPath = "Assets/Prefabs/UI/Common/TextInput.prefab";
    private const string _scrollUpAddress =
        "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_arrow_up.png";
    private const string _scrollDownAddress =
        "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_arrow_pressed_2.png";
    private const string _scrollHandleAddress =
        "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_middle.png";

    internal static T EnableRuntimeComponent<T>(T component)
        where T : MonoBehaviour
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        component.enabled = true;
        return component;
    }

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

    internal static RectTransform CreateChildLayer(string name, Transform parent)
    {
        RectTransform rect = CreateLayer(name, parent).GetComponent<RectTransform>();
        FillParent(rect);
        return rect;
    }

    internal static RectTransform CreateSourceRectLayer(
        string name,
        Transform parent,
        int width,
        int height
    )
    {
        RectTransform rect = CreateLayer(name, parent).GetComponent<RectTransform>();
        SetSourceRect(rect, 0, 0, width, height);
        return rect;
    }

    internal static Image CreateSlicedImage(
        string name,
        Transform parent,
        string spritePath,
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
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    internal static Button CreateSlicedButton(
        string name,
        Transform parent,
        string spritePath,
        int x,
        int y,
        int width,
        int height,
        Color color
    )
    {
        Image image = CreateSlicedImage(name, parent, spritePath, x, y, width, height, color);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

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

    internal static Button CreateButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        RawImagePressVisual pressVisual = EnableRuntimeComponent(
            image.gameObject.AddComponent<RawImagePressVisual>()
        );
        AssignReference(pressVisual, "image", image);
        AssignReference(pressVisual, "button", button);
        pressVisual.SetTextures(image.texture, null);

        ContentTextureBinding textureBinding = image.GetComponent<ContentTextureBinding>();
        if (textureBinding != null)
        {
            ContentPressVisualBinding pressBinding =
                image.gameObject.AddComponent<ContentPressVisualBinding>();
            pressBinding.SetAddresses(textureBinding.Address, null);
            UnityEngine.Object.DestroyImmediate(textureBinding);
        }
        return button;
    }

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
        SetSourceRect(text.rectTransform, 2, 0, width - 2, height);

        TextMeshProUGUI placeholder = input.placeholder as TextMeshProUGUI;
        if (placeholder == null)
            throw new MissingReferenceException($"{name}/Placeholder is missing.");
        placeholder.text = placeholderText;
        placeholder.color = Color.white;
        placeholder.fontSize = 12;
        placeholder.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(placeholder.rectTransform, 2, 0, width - 2, height);

        input.enabled = true;
        input.targetGraphic = image;
        input.transition = Selectable.Transition.None;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.textViewport = rect;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

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
        ScrollAreaView view = InstantiatePrefabComponent<ScrollAreaView>(
            _scrollAreaPrefabPath,
            parent
        );
        GameObject root = view.gameObject;
        root.name = name;
        root.SetActive(false);
        SetSourceRect(root.GetComponent<RectTransform>(), x, y, width, height);
        view.enabled = true;

        RectTransform scrollRoot = view.ScrollRoot;
        SetSourceRect(scrollRoot, viewportX, viewportY, viewportWidth, viewportHeight);
        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        RectTransform viewportRect = view.ViewportRoot;
        SetSourceRect(viewportRect, 0, 0, viewportWidth, viewportHeight);
        Image viewportImage = viewportRect.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        contentRoot = view.ContentRoot;
        SetSourceRect(contentRoot, 0, 0, viewportWidth, viewportHeight);
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;

        Scrollbar scrollbar = view.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar == null)
            throw new MissingReferenceException($"{name}/Scrollbar is missing.");
        scrollbar.handleRect = null;
        SetSourceRect(
            scrollbar.transform as RectTransform,
            scrollbarX,
            scrollbarY,
            scrollbarWidth,
            scrollbarHeight
        );

        Texture2D scrollUpTexture = LoadTexture(_scrollUpAddress);
        Texture2D scrollDownTexture = LoadTexture(_scrollDownAddress);
        int upArrowHeight = GetTextureHeight(scrollUpTexture, 9);
        int downArrowHeight = GetTextureHeight(scrollDownTexture, 9);
        int trackHeight = Mathf.Max(0, scrollbarHeight - upArrowHeight - downArrowHeight);

        Image scrollbarBackground = scrollbar.GetComponent<Image>();
        scrollbarBackground.color = Color.clear;
        scrollbarBackground.raycastTarget = true;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        Image trackBackground = FindRequiredChild<Image>(
            scrollbar.transform,
            "TrackBackgroundImage"
        );
        trackBackground.color = Color.black;
        SetSourceRect(trackBackground.rectTransform, 0, upArrowHeight, scrollbarWidth, trackHeight);

        RawImage scrollUpImage = FindRequiredChild<RawImage>(
            scrollbar.transform,
            "ScrollUpButtonImage"
        );
        scrollUpImage.texture = scrollUpTexture;
        AttachTextureBinding(scrollUpImage, _scrollUpAddress);
        SetSourceRect(scrollUpImage.rectTransform, 0, 0, scrollbarWidth, upArrowHeight);
        ConfigureScrollButton(scrollUpImage);

        RawImage scrollDownImage = FindRequiredChild<RawImage>(
            scrollbar.transform,
            "ScrollDownButtonImage"
        );
        scrollDownImage.texture = scrollDownTexture;
        AttachTextureBinding(scrollDownImage, _scrollDownAddress);
        SetSourceRect(
            scrollDownImage.rectTransform,
            0,
            scrollbarHeight - downArrowHeight,
            scrollbarWidth,
            downArrowHeight
        );
        ConfigureScrollButton(scrollDownImage);

        RectTransform slidingArea = FindRequiredChild<RectTransform>(
            scrollbar.transform,
            "SlidingArea"
        );
        SetSourceRect(slidingArea, 0, upArrowHeight, scrollbarWidth, trackHeight);
        RawImage handleImage = FindRequiredChild<RawImage>(slidingArea, "Handle");
        handleImage.texture = LoadTexture(_scrollHandleAddress);
        AttachTextureBinding(handleImage, _scrollHandleAddress);
        FillParent(handleImage.rectTransform);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = handleImage.rectTransform;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.None;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        root.SetActive(true);
        return view;
    }

    internal static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    internal static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

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

    internal static void AssignReferenceArray<T>(
        UnityEngine.Object target,
        string propertyName,
        IReadOnlyList<T> values
    )
        where T : UnityEngine.Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void AssignInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        FindRequiredProperty(target, serializedObject, propertyName).intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static GameObject SaveGeneratedPrefabAsset(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success || saved == null)
            throw new InvalidOperationException($"Failed to save generated prefab at {path}.");
        return saved;
    }

    private static GameObject CreateLayer(string name, Transform parent)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(parent, false);
        return layer;
    }

    private static T InstantiatePrefabComponent<T>(string path, Transform parent)
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

    private static Texture2D LoadTexture(string path)
    {
        return ContentPackEditor.Assets.GetTexture(path);
    }

    private static void AttachTextureBinding(RawImage image, string texturePath)
    {
        ContentTextureBinding existing = image.GetComponent<ContentTextureBinding>();
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);
        ContentTextureBinding binding = image.gameObject.AddComponent<ContentTextureBinding>();
        binding.SetAddress(ToContentAddress(texturePath));
    }

    private static string ToContentAddress(string texturePath)
    {
        int separatorIndex = texturePath.LastIndexOf('/');
        int extensionIndex = texturePath.LastIndexOf('.');
        return extensionIndex > separatorIndex ? texturePath[..extensionIndex] : texturePath;
    }

    private static int GetTextureHeight(Texture texture, int fallback)
    {
        int height = UILayout.GetTextureSourceHeight(texture);
        return height > 0 ? height : fallback;
    }

    private static void ConfigureScrollButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
    }

    private static SerializedProperty FindRequiredProperty(
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
