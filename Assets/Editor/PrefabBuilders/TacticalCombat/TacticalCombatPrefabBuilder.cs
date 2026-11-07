using System;
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
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform viewportRect = CreateRect("Viewport", canvas.transform);
        viewportRect.offsetMin = new Vector2(0, 320);
        viewportRect.offsetMax = new Vector2(0, -60);
        RectTransform backgroundRect = CreateRect("Starfield", canvas.transform);
        backgroundRect.offsetMin = viewportRect.offsetMin;
        backgroundRect.offsetMax = viewportRect.offsetMax;
        backgroundRect.SetAsFirstSibling();
        RawImage background = backgroundRect.gameObject.AddComponent<RawImage>();
        background.raycastTarget = false;
        background.color = Color.black;
        RawImage viewport = viewportRect.gameObject.AddComponent<RawImage>();
        viewport.color = Color.clear;
        RectTransform overlayRect = CreateRect("Overlay", viewport.transform);
        TacticalCombatOverlayGraphic overlay =
            overlayRect.gameObject.AddComponent<TacticalCombatOverlayGraphic>();
        overlay.raycastTarget = false;
        RectTransform header = CreatePanel("Header", canvas.transform);
        header.anchorMin = new Vector2(0, 1);
        header.offsetMin = new Vector2(0, -60);
        header.offsetMax = Vector2.zero;
        TextMeshProUGUI status = CreateLabel("Status", header, "Preparing battle…", 24);
        status.rectTransform.offsetMin = new Vector2(24, 0);
        RectTransform hud = CreatePanel("Orders", canvas.transform);
        hud.anchorMax = new Vector2(1, 0);
        hud.offsetMin = Vector2.zero;
        hud.offsetMax = new Vector2(0, 320);
        TextMeshProUGUI selection = CreateLabel("Selection", hud, string.Empty, 19);
        selection.rectTransform.offsetMin = new Vector2(24, 198);
        selection.rectTransform.offsetMax = new Vector2(-24, -12);
        selection.alignment = TextAlignmentOptions.TopLeft;
        selection.overflowMode = TextOverflowModes.Ellipsis;
        BindControls(view, hud);
        TacticalCombatRenderer renderer = CreateRig(root.transform);
        Assign(view, "_viewport", viewport);
        Assign(view, "_background", background);
        Assign(view, "_status", status);
        Assign(view, "_selection", selection);
        Assign(view, "_overlay", overlay);
        Assign(view, "_battleRenderer", renderer);
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Authors command rows and their explicit semantic bindings.
    /// </summary>
    /// <param name="view">The generated view.</param>
    /// <param name="parent">The command panel.</param>
    private static void BindControls(TacticalCombatView view, Transform parent)
    {
        string[] labels =
        {
            "Pause / resume",
            "Attack fighters",
            "Attack ships",
            "Hold fire",
            "Stop",
            "Recover fighters",
            "Withdraw",
            "Close attack",
            "Ranged attack",
            "Line formation",
            "Surround",
            "Left hook",
            "Right hook",
            "Hammer",
            "Anvil",
            "Attack station",
            "Superlaser",
            "Group selection",
            "Return to campaign",
            "Navigation ring 1",
            "Navigation ring 2",
            "Navigation ring 3",
            "Navigation ring 4",
            "Save camera",
            "Restore camera",
            "Track selection",
        };
        Array values = Enum.GetValues(typeof(TacticalCombatControl));
        SerializedObject serialized = new SerializedObject(view);
        SerializedProperty bindings = serialized.FindProperty("_controls");
        bindings.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            RectTransform rect = CreateRect("Command" + index, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(16 + index % 10 * 189, 132 - index / 10 * 60);
            rect.sizeDelta = new Vector2(180, 50);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.13f, 0.18f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.65f, 0.85f, 1);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f);
            button.colors = colors;
            TextMeshProUGUI label = CreateLabel("Label", rect, labels[index], 19);
            label.alignment = TextAlignmentOptions.Center;
            SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("Control").enumValueIndex = index;
            binding.FindPropertyRelative("Button").objectReferenceValue = button;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
    /// Creates an opaque HUD panel.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning UI parent.</param>
    /// <returns>The authored rectangle.</returns>
    private static RectTransform CreatePanel(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.04f, 1);
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
        label.color = new Color(0.8f, 0.9f, 1);
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
