using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the tactical battle screen, isolated HDRP rig, and semantic command controls.
/// </summary>
public static class TacticalCombatPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/StrategyView/TacticalCombat.prefab";

    /// <summary>
    /// Rebuilds the tactical view before the strategy root records its prefab reference.
    /// </summary>
    public static void Rebuild()
    {
        GameObject root = new GameObject("TacticalCombat", typeof(TacticalCombatView));
        TacticalCombatView view = root.GetComponent<TacticalCombatView>();
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640, 480);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        RectTransform frame = CreateRect("Frame", canvas.transform);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = new Vector2(640, 480);
        RawImage frameImage = frame.gameObject.AddComponent<RawImage>();
        frameImage.raycastTarget = false;
        frameImage.enabled = false;
        RawImage background = CreateImage("Starfield", frame, TacticalCombatHudLayout.Viewport);
        background.color = Color.black;
        RawImage viewport = CreateImage("Viewport", frame, TacticalCombatHudLayout.Viewport);
        viewport.raycastTarget = true;
        viewport.color = Color.clear;
        RawImage objectHeader = CreateImage("Object header", frame, new RectInt(9, 1, 49, 19));
        RawImage fighterHeader = CreateImage("Fighter header", frame, new RectInt(296, 1, 49, 19));
        objectHeader.enabled = false;
        fighterHeader.enabled = false;
        RawImage panel = CreateImage("Right panel", frame, TacticalCombatHudLayout.RightPanel);
        RawImage diagram = CreateImage(
            "Illustration",
            panel.transform,
            new RectInt(14, 14, 124, 106)
        );
        panel.enabled = false;
        diagram.enabled = false;
        RawImage objectPreview = CreateImage(
            "Object preview",
            panel.transform,
            new RectInt(26, 17, 96, 39)
        );
        RawImage fighterPreview = CreateImage(
            "Fighter preview",
            panel.transform,
            new RectInt(23, 59, 102, 107)
        );
        objectPreview.enabled = false;
        fighterPreview.enabled = false;
        TextMeshProUGUI selection = CreateLabel("Name", panel.transform, string.Empty, 12);
        UILayout.SetSourceRect(selection.rectTransform, 26, 7, 97, 13);
        selection.alignment = TextAlignmentOptions.TopLeft;
        selection.overflowMode = TextOverflowModes.Masking;
        selection.textWrappingMode = TextWrappingModes.NoWrap;
        selection.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI assignment = CreateInspectionLabel(
            "Assignment",
            panel.transform,
            new RectInt(26, 52, 97, 9)
        );
        TextMeshProUGUI orders = CreateInspectionLabel(
            "Orders",
            panel.transform,
            new RectInt(15, 182, 121, 9)
        );
        TextMeshProUGUI tactics = CreateInspectionLabel(
            "Tactics",
            panel.transform,
            new RectInt(15, 193, 121, 9)
        );
        TextMeshProUGUI fighterCounts = CreateInspectionLabel(
            "Fighter counts",
            panel.transform,
            new RectInt(23, 146, 102, 9)
        );
        fighterCounts.alignment = TextAlignmentOptions.Top;
        RawImage[] systemIcons = new RawImage[5];
        for (int index = 0; index < systemIcons.Length; index++)
        {
            RawImage icon = CreateImage(
                "System " + index,
                panel.transform,
                new RectInt(10 + index * 27, 103, 22, 15)
            );
            icon.enabled = false;
            icon.raycastTarget = true;
            icon.gameObject.AddComponent<HoverTooltipRegion>();
            systemIcons[index] = icon;
        }
        Image hull = CreateGauge("Hull", panel.transform, new RectInt(34, 81, 37, 10));
        Image shields = CreateGauge("Shields", panel.transform, new RectInt(103, 81, 37, 10));
        TextMeshProUGUI status = CreateLabel("Loading", viewport.transform, "Preparing battle…", 8);
        status.alignment = TextAlignmentOptions.Center;
        RawImage stationCharge = CreateImage("Station charge", frame, new RectInt(474, 0, 166, 25));
        stationCharge.enabled = false;
        Image stationChargeBar = CreateGauge(
            "Charge",
            stationCharge.transform,
            new RectInt(34, 9, 95, 6)
        );
        stationChargeBar.color = Color.green;
        stationChargeBar.transform.parent.GetComponent<Image>().color = Color.blue;
        BindControls(view, frame);
        TacticalCombatRenderer renderer = CreateRig(root.transform);
        Assign(view, "_frame", frame);
        Assign(view, "_frameImage", frameImage);
        Assign(view, "_objectHeader", objectHeader);
        Assign(view, "_fighterHeader", fighterHeader);
        Assign(view, "_panelImage", panel);
        Assign(view, "_diagram", diagram);
        Assign(view, "_objectPreview", objectPreview);
        Assign(view, "_fighterPreview", fighterPreview);
        Assign(view, "_assignment", assignment);
        Assign(view, "_orders", orders);
        Assign(view, "_tactics", tactics);
        Assign(view, "_fighterCounts", fighterCounts);
        SerializedObject iconOwner = new SerializedObject(view);
        SerializedProperty iconBindings = iconOwner.FindProperty("_systemIcons");
        iconBindings.arraySize = systemIcons.Length;
        for (int index = 0; index < systemIcons.Length; index++)
            iconBindings.GetArrayElementAtIndex(index).objectReferenceValue = systemIcons[index];
        iconOwner.ApplyModifiedPropertiesWithoutUndo();
        Assign(view, "_hullBar", hull);
        Assign(view, "_shieldBar", shields);
        Assign(view, "_stationCharge", stationCharge);
        Assign(view, "_stationChargeBar", stationChargeBar);
        Assign(view, "_viewport", viewport);
        Assign(view, "_background", background);
        Assign(view, "_status", status);
        Assign(view, "_selection", selection);
        Assign(view, "_battleRenderer", renderer);
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Authors the frame's fixed categories, camera controls, and contextual right-panel buttons.
    /// </summary>
    /// <param name="view">The generated view.</param>
    /// <param name="frame">The fixed-aspect source-space frame.</param>
    private static void BindControls(TacticalCombatView view, RectTransform frame)
    {
        System.Collections.Generic.Dictionary<TacticalCombatHudPanel, Transform> parents = new()
        {
            [TacticalCombatHudPanel.Frame] = frame,
        };
        SerializedObject serialized = new SerializedObject(view);
        SerializedProperty panels = serialized.FindProperty("_panels");
        panels.arraySize = 8;
        for (int index = 0; index < 8; index++)
        {
            TacticalCombatHudPanel kind = (TacticalCombatHudPanel)(index + 1);
            RectTransform root = CreateRect(kind.ToString(), frame);
            UILayout.SetSourceRect(root, 482, 28, 149, 236);
            parents[kind] = root;
            SerializedProperty binding = panels.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("Panel").enumValueIndex = (int)kind;
            binding.FindPropertyRelative("Root").objectReferenceValue = root.gameObject;
        }
        Transform confirmation = parents[TacticalCombatHudPanel.WithdrawalConfirmation];
        CreateWithdrawalLabel(
            "Withdrawal title",
            confirmation,
            "Withdraw Confirmation",
            new Vector2Int(20, 28)
        );
        CreateWithdrawalLabel(
            "Withdrawal question",
            confirmation,
            "Are you sure you wish to\norder a fleet withdraw?",
            new Vector2Int(14, 92)
        );
        SerializedProperty controls = serialized.FindProperty("_controls");
        controls.arraySize = TacticalCombatHudLayout.Controls.Count;
        for (int index = 0; index < controls.arraySize; index++)
        {
            TacticalCombatHudControl definition = TacticalCombatHudLayout.Controls[index];
            RectTransform rect = CreateRect(
                definition.Control.ToString(),
                parents[definition.Panel]
            );
            RectInt bounds = definition.Bounds;
            UILayout.SetSourceRect(rect, bounds.x, bounds.y, bounds.width, bounds.height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.enabled = false;
            TacticalCombatHudButton button =
                rect.gameObject.AddComponent<TacticalCombatHudButton>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.SetRepeatInterval(definition.RepeatMilliseconds);
            TextMeshProUGUI label = null;
            if (definition.Label != null)
            {
                label = CreateLabel("Label", rect, definition.Label, 8);
                label.alignment = TextAlignmentOptions.Center;
            }
            SerializedProperty binding = controls.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("Control").enumValueIndex = (int)definition.Control;
            binding.FindPropertyRelative("Panel").enumValueIndex = (int)definition.Panel;
            binding.FindPropertyRelative("Button").objectReferenceValue = button;
            binding.FindPropertyRelative("ArtworkKey").stringValue = definition.ArtworkKey;
            binding.FindPropertyRelative("BitmapHitTest").boolValue = definition.BitmapHitTest;
            binding.FindPropertyRelative("Label").objectReferenceValue = label;
            binding.FindPropertyRelative("Tooltip").stringValue = definition.Tooltip;
            if (definition.Panel != TacticalCombatHudPanel.Frame)
                bounds.position += new Vector2Int(482, 28);
            binding.FindPropertyRelative("SourceBounds").rectIntValue = bounds;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Authors one clipped, normal-weight inspection row at its fixed panel bounds.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The inspection panel.</param>
    /// <param name="bounds">The fixed panel-space rectangle.</param>
    /// <returns>The authored white status text.</returns>
    private static TextMeshProUGUI CreateInspectionLabel(
        string name,
        Transform parent,
        RectInt bounds
    )
    {
        TextMeshProUGUI label = CreateLabel(name, parent, string.Empty, 12);
        UILayout.SetSourceRect(
            label.rectTransform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        label.alignment = TextAlignmentOptions.TopLeft;
        label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Masking;
        label.gameObject.AddComponent<RectMask2D>();
        return label;
    }

    /// <summary>
    /// Authors an automatically measured withdrawal text block at its fixed panel origin.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The confirmation surface.</param>
    /// <param name="text">The authored title or explicit two-line question.</param>
    /// <param name="origin">The text block's panel-space top-left coordinate.</param>
    private static void CreateWithdrawalLabel(
        string name,
        Transform parent,
        string text,
        Vector2Int origin
    )
    {
        TextMeshProUGUI label = CreateLabel(name, parent, text, 14);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        UILayout.SetSourceRect(label.rectTransform, origin.x, origin.y, 0, 0);
        ContentSizeFitter sizing = label.gameObject.AddComponent<ContentSizeFitter>();
        sizing.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizing.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Authors an addressed image surface at a fixed source-space rectangle.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The source-space parent.</param>
    /// <param name="bounds">The authored pixel rectangle.</param>
    /// <returns>The noninteractive image surface.</returns>
    private static RawImage CreateImage(string name, Transform parent, RectInt bounds)
    {
        RectTransform rect = CreateRect(name, parent);
        UILayout.SetSourceRect(rect, bounds.x, bounds.y, bounds.width, bounds.height);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Authors one horizontal blue status gauge over its red remainder inside the inspection panel.
    /// </summary>
    /// <param name="name">The gauge's hierarchy name.</param>
    /// <param name="parent">The owning panel.</param>
    /// <param name="bounds">The gauge's pixel bounds.</param>
    /// <returns>The current-value fill image.</returns>
    private static Image CreateGauge(string name, Transform parent, RectInt bounds)
    {
        RectTransform rect = CreateRect(name, parent);
        UILayout.SetSourceRect(rect, bounds.x, bounds.y, bounds.width, bounds.height);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = Color.red;
        background.raycastTarget = false;
        rect.gameObject.AddComponent<RectMask2D>();
        RectTransform fill = CreateRect("Value", rect);
        Image image = fill.gameObject.AddComponent<Image>();
        image.color = Color.blue;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Authors world-space camera and lighting independently of the UI canvas scale.
    /// </summary>
    /// <param name="parent">The owned prefab root.</param>
    /// <returns>The configured battle renderer.</returns>
    private static TacticalCombatRenderer CreateRig(Transform parent)
    {
        GameObject rig = new GameObject("BattleRig", typeof(TacticalCombatRenderer));
        rig.transform.SetParent(parent, false);
        rig.transform.position = new Vector3(16000, 16000, 16000);
        GameObject models = new GameObject("Models");
        models.transform.SetParent(rig.transform, false);
        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.cullingMask = 1 << PresentationLayers.TacticalCombat;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 3000;
        camera.fieldOfView = 45;
        HdrpRenderTargetBuilder.ConfigureCamera(camera, includeFlatAmbientLighting: true);
        HdrpRenderTargetBuilder.AddIsolatedVolume(rig.transform);
        HdrpRenderTargetBuilder.AddFlatAmbientVolume(
            rig.transform,
            new Color(0.212f, 0.227f, 0.259f).linear
        );
        GameObject lightObject = new GameObject("KeyLight", typeof(Light));
        lightObject.transform.SetParent(rig.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(35, -30, 0);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.cullingMask = 1 << PresentationLayers.TacticalCombat;
        light.shadows = LightShadows.None;
        HdrpRenderTargetBuilder.ConfigureLight(light, Mathf.PI * 0.75f);
        TacticalCombatRenderer renderer = rig.GetComponent<TacticalCombatRenderer>();
        Assign(renderer, "_battleCamera", camera);
        Assign(renderer, "_modelRoot", models.transform);
        Assign(renderer, "_fighterShader", Shader.Find("Rebellion/Tactical fighter"));
        return renderer;
    }

    /// <summary>
    /// Creates a stretching UI rectangle.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning UI parent.</param>
    /// <returns>The authored rectangle.</returns>
    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    /// <summary>
    /// Creates a sentence-case tactical label.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning UI parent.</param>
    /// <param name="text">The authored text.</param>
    /// <param name="size">The reference font size.</param>
    /// <returns>The generated text component.</returns>
    private static TextMeshProUGUI CreateLabel(
        string name,
        Transform parent,
        string text,
        float size
    )
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// Writes an explicit generated component reference.
    /// </summary>
    /// <param name="target">The generated component.</param>
    /// <param name="property">The serialized field name.</param>
    /// <param name="value">The authored reference.</param>
    private static void Assign(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
