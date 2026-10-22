using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the loading-screen presentation prefab.
/// </summary>
public static class LoadingScreenPrefabBuilder
{
    private const string _prefabPath = "Assets/Resources/UI/LoadingScreen.prefab";
    private const int _presentationLayer = 31;
    private const float _builtInDirectionalLightIntensityToHdrp = Mathf.PI;
    private const float _builtInPunctualLightIntensityToHdrp = 600f / (4f * Mathf.PI);
    private static readonly Color _shuttleAmbientColor = new Color(0.212f, 0.227f, 0.259f).linear;

    /// <summary>
    /// Rebuilds the loading-screen prefab.
    /// </summary>
    public static void Rebuild()
    {
        GameObject root = new GameObject(
            "LoadingScreen",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(LoadingScreenView)
        );
        HdrpRenderTargetBuilder.AddIsolatedVolume(root.transform);
        HdrpRenderTargetBuilder.AddFlatAmbientVolume(root.transform, _shuttleAmbientColor);
        FillParent(root.GetComponent<RectTransform>());

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject displayCameraObject = new GameObject("DisplayCamera", typeof(Camera));
        displayCameraObject.transform.SetParent(root.transform, false);
        Camera displayCamera = displayCameraObject.GetComponent<Camera>();
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = new Color(0.005f, 0.012f, 0.022f, 1f);
        displayCamera.cullingMask = 0;
        displayCamera.depth = -100f;
        displayCamera.allowHDR = false;
        displayCamera.allowMSAA = false;

        GameObject backgroundObject = CreateRectObject("Background", root.transform);
        backgroundObject.AddComponent<CanvasRenderer>();
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.005f, 0.012f, 0.022f, 1f);
        background.raycastTarget = true;
        CanvasGroup presentationGroup = backgroundObject.AddComponent<CanvasGroup>();
        FillParent(background.rectTransform);

        GameObject starfieldObject = CreateRectObject(
            "HyperspaceField",
            backgroundObject.transform
        );
        starfieldObject.AddComponent<CanvasRenderer>();
        HyperspaceStarfieldGraphic starfield =
            starfieldObject.AddComponent<HyperspaceStarfieldGraphic>();
        starfield.raycastTarget = false;
        FillParent(starfield.rectTransform);

        GameObject bottomShadeObject = CreateRectObject(
            "BottomCommandShade",
            backgroundObject.transform
        );
        Image bottomShade = bottomShadeObject.AddComponent<Image>();
        bottomShade.color = new Color(0.002f, 0.008f, 0.02f, 0.58f);
        bottomShade.raycastTarget = false;
        SetSourceRect(bottomShade.rectTransform, 0, 822, 1920, 258);

        GameObject shuttleViewportObject = CreateRectObject(
            "ShuttleViewport",
            backgroundObject.transform
        );
        RawImage shuttleViewport = shuttleViewportObject.AddComponent<RawImage>();
        shuttleViewport.color = Color.white;
        shuttleViewport.raycastTarget = false;
        FillParent(shuttleViewport.rectTransform);

        TextMeshProUGUI status = CreateLoadingText(
            "StatusTextField",
            backgroundObject.transform,
            "LOADING, PLEASE WAIT...",
            28,
            FontStyles.Bold
        );
        status.characterSpacing = 5f;
        status.color = new Color(0.84f, 0.95f, 1f, 1f);
        SetSourceRect(status.rectTransform, 0, 890, 1920, 62);

        TextMeshProUGUI failureDetail = CreateLoadingText(
            "FailureDetailTextField",
            backgroundObject.transform,
            string.Empty,
            22,
            FontStyles.Normal
        );
        failureDetail.color = new Color(0.72f, 0.86f, 0.94f, 1f);
        failureDetail.textWrappingMode = TextWrappingModes.Normal;
        failureDetail.overflowMode = TextOverflowModes.Ellipsis;
        SetSourceRect(failureDetail.rectTransform, 460, 952, 1000, 92);
        failureDetail.gameObject.SetActive(false);

        Button continueButton = CreateContinueButton(backgroundObject.transform);
        continueButton.gameObject.SetActive(false);

        GameObject rig = new GameObject("ShuttleRig");
        rig.transform.SetParent(root.transform, false);
        rig.transform.position = new Vector3(12000f, 12000f, 12000f);

        GameObject modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(rig.transform, false);

        GameObject keyLightObject = new GameObject("KeyLight", typeof(Light));
        keyLightObject.transform.SetParent(rig.transform, false);
        keyLightObject.transform.localRotation = Quaternion.Euler(28f, -32f, 0f);
        Light keyLight = keyLightObject.GetComponent<Light>();
        keyLight.type = LightType.Directional;
        HdrpRenderTargetBuilder.ConfigureLight(
            keyLight,
            0.58f * _builtInDirectionalLightIntensityToHdrp
        );
        keyLight.color = Color.white;
        keyLight.cullingMask = 1 << _presentationLayer;
        keyLight.shadows = LightShadows.None;

        GameObject fillLightObject = new GameObject("FillLight", typeof(Light));
        fillLightObject.transform.SetParent(rig.transform, false);
        fillLightObject.transform.localPosition = new Vector3(-2.5f, -1f, -2.5f);
        Light fillLight = fillLightObject.GetComponent<Light>();
        fillLight.type = LightType.Point;
        HdrpRenderTargetBuilder.ConfigureLight(
            fillLight,
            0.3f * _builtInPunctualLightIntensityToHdrp
        );
        fillLight.color = new Color(0.82f, 0.87f, 0.95f, 1f);
        fillLight.range = 12f;
        fillLight.cullingMask = 1 << _presentationLayer;
        fillLight.shadows = LightShadows.None;

        GameObject rimLightObject = new GameObject("RimLight", typeof(Light));
        rimLightObject.transform.SetParent(rig.transform, false);
        rimLightObject.transform.localRotation = Quaternion.Euler(-18f, 154f, 0f);
        Light rimLight = rimLightObject.GetComponent<Light>();
        rimLight.type = LightType.Directional;
        HdrpRenderTargetBuilder.ConfigureLight(
            rimLight,
            0.25f * _builtInDirectionalLightIntensityToHdrp
        );
        rimLight.color = new Color(0.45f, 0.62f, 0.85f, 1f);
        rimLight.cullingMask = 1 << _presentationLayer;
        rimLight.shadows = LightShadows.None;

        GameObject cameraOrbitObject = new GameObject("CameraOrbit");
        cameraOrbitObject.transform.SetParent(rig.transform, false);

        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.transform.SetParent(cameraOrbitObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.08f, -8f);
        Camera shuttleCamera = cameraObject.GetComponent<Camera>();
        shuttleCamera.clearFlags = CameraClearFlags.SolidColor;
        shuttleCamera.backgroundColor = Color.clear;
        shuttleCamera.fieldOfView = 32f;
        shuttleCamera.nearClipPlane = 0.1f;
        shuttleCamera.farClipPlane = 50f;
        shuttleCamera.cullingMask = 1 << _presentationLayer;
        shuttleCamera.allowHDR = false;
        shuttleCamera.allowMSAA = false;
        HdrpRenderTargetBuilder.ConfigureCamera(shuttleCamera, includeFlatAmbientLighting: true);

        GameObject fadeOverlayObject = CreateRectObject("FadeOverlay", root.transform);
        Image fadeOverlay = fadeOverlayObject.AddComponent<Image>();
        fadeOverlay.color = Color.black;
        fadeOverlay.raycastTarget = true;
        CanvasGroup fadeOverlayGroup = fadeOverlayObject.AddComponent<CanvasGroup>();
        fadeOverlayGroup.alpha = 0f;
        fadeOverlayGroup.blocksRaycasts = false;
        FillParent(fadeOverlay.rectTransform);

        LoadingScreenView view = root.GetComponent<LoadingScreenView>();
        AudioSource shuttleFlightAudio = root.AddComponent<AudioSource>();
        shuttleFlightAudio.playOnAwake = false;
        shuttleFlightAudio.loop = true;
        shuttleFlightAudio.spatialBlend = 0f;
        AssignReference(view, "statusText", status);
        AssignReference(view, "failureDetailText", failureDetail);
        AssignReference(view, "continueButton", continueButton);
        AssignReference(
            view,
            "continueLabel",
            continueButton.transform.Find("Label").GetComponent<TextMeshProUGUI>()
        );
        AssignReference(
            view,
            "continueKeyboardHint",
            continueButton.transform.Find("KeyboardHint").GetComponent<TextMeshProUGUI>()
        );
        AssignReference(view, "shuttleViewport", shuttleViewport);
        AssignReference(view, "starfield", starfield);
        AssignReference(view, "presentationGroup", presentationGroup);
        AssignReference(view, "fadeOverlayGroup", fadeOverlayGroup);
        AssignReference(view, "shuttleCameraOrbit", cameraOrbitObject.transform);
        AssignReference(view, "shuttleCamera", shuttleCamera);
        AssignReference(view, "shuttleModelRoot", modelRoot.transform);
        AssignReference(view, "shuttleFlightAudio", shuttleFlightAudio);
        SavePrefab(root, _prefabPath);
    }

    /// <summary>
    /// Creates the centered button used to enter a fully loaded game.
    /// </summary>
    /// <param name="parent">The loading-presentation parent.</param>
    /// <returns>The configured continue button.</returns>
    private static Button CreateContinueButton(Transform parent)
    {
        GameObject buttonObject = CreateRectObject("ContinueButton", parent);
        buttonObject.AddComponent<CanvasRenderer>();
        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.09f, 0.15f, 0.98f);

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        SetSourceRect(background.rectTransform, 735, 946, 450, 86);

        Image outerGlow = CreateButtonImage(
            "OuterGlow",
            buttonObject.transform,
            new Color(0.08f, 0.64f, 1f, 0.1f)
        );
        SetInsetRect(outerGlow.rectTransform, -7f, -7f, -7f, -7f);

        Image frame = CreateButtonImage(
            "Frame",
            buttonObject.transform,
            new Color(0.12f, 0.55f, 0.75f, 0.75f)
        );
        SetInsetRect(frame.rectTransform, 0f, 0f, 0f, 0f);

        Image panel = CreateButtonImage(
            "Panel",
            buttonObject.transform,
            new Color(0.012f, 0.055f, 0.1f, 0.98f)
        );
        SetInsetRect(panel.rectTransform, 3f, 3f, 3f, 3f);

        Image upperRail = CreateButtonImage(
            "UpperRail",
            buttonObject.transform,
            new Color(0.5f, 0.9f, 1f, 0.9f)
        );
        SetCenteredRect(upperRail.rectTransform, 0f, 40f, 258f, 2f);

        Image lowerRail = CreateButtonImage(
            "LowerRail",
            buttonObject.transform,
            new Color(0.12f, 0.5f, 0.8f, 0.74f)
        );
        SetCenteredRect(lowerRail.rectTransform, 0f, -40f, 258f, 2f);

        Image leftAccent = CreateButtonImage(
            "LeftAccent",
            buttonObject.transform,
            new Color(0.36f, 0.88f, 1f, 1f)
        );
        SetCenteredRect(leftAccent.rectTransform, -216f, 0f, 6f, 42f);

        Image rightAccent = CreateButtonImage(
            "RightAccent",
            buttonObject.transform,
            new Color(0.36f, 0.88f, 1f, 1f)
        );
        SetCenteredRect(rightAccent.rectTransform, 216f, 0f, 6f, 42f);

        TextMeshProUGUI label = CreateLoadingText(
            "Label",
            buttonObject.transform,
            "CONTINUE",
            24,
            FontStyles.Bold
        );
        label.characterSpacing = 8f;
        label.color = new Color(0.78f, 0.91f, 0.96f, 1f);
        SetCenteredRect(label.rectTransform, 0f, 8f, 420f, 42f);

        TextMeshProUGUI keyboardHint = CreateLoadingText(
            "KeyboardHint",
            buttonObject.transform,
            "PRESS ENTER",
            12,
            FontStyles.Normal
        );
        keyboardHint.characterSpacing = 4f;
        keyboardHint.color = new Color(0.4f, 0.74f, 0.9f, 1f);
        SetCenteredRect(keyboardHint.rectTransform, 0f, -25f, 420f, 22f);

        LoadingContinueButtonFeedback feedback =
            buttonObject.AddComponent<LoadingContinueButtonFeedback>();
        AssignReference(feedback, "outerGlow", outerGlow);
        AssignReference(feedback, "frame", frame);
        AssignReference(feedback, "panel", panel);
        AssignReference(feedback, "label", label);
        return button;
    }

    /// <summary>
    /// Creates a non-interactive image used to compose the continue-button frame.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The button transform.</param>
    /// <param name="color">The authored image color.</param>
    /// <returns>The configured image.</returns>
    private static Image CreateButtonImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = CreateRectObject(name, parent);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Insets a rectangle from every edge of its parent.
    /// </summary>
    /// <param name="rect">The target rectangle.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="bottom">The bottom inset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="top">The top inset.</param>
    private static void SetInsetRect(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top
    )
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Positions a fixed-size rectangle relative to the center of its parent.
    /// </summary>
    /// <param name="rect">The target rectangle.</param>
    /// <param name="x">The horizontal center offset.</param>
    /// <param name="y">The vertical center offset.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    private static void SetCenteredRect(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height
    )
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Creates one centered text field for the loading screen.
    /// </summary>
    /// <param name="name">The text GameObject name.</param>
    /// <param name="parent">The text parent transform.</param>
    /// <param name="content">The initial text.</param>
    /// <param name="fontSize">The authored font size.</param>
    /// <param name="fontStyle">The authored font style.</param>
    /// <returns>The configured text component.</returns>
    private static TextMeshProUGUI CreateLoadingText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        FontStyles fontStyle
    )
    {
        GameObject textObject = CreateRectObject(name, parent);
        textObject.AddComponent<CanvasRenderer>();
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// Creates a RectTransform GameObject beneath an optional parent.
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
    /// Assigns one serialized object reference.
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
    /// Saves the generated prefab and destroys its temporary authoring hierarchy.
    /// </summary>
    /// <param name="root">The temporary prefab root.</param>
    /// <param name="path">The destination prefab path.</param>
    private static void SavePrefab(GameObject root, string path)
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
    }
}
