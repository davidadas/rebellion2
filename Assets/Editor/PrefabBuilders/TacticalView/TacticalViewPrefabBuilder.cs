using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the generated tactical-scene root and installs it in the TacticalView scene.
/// </summary>
public static class TacticalViewPrefabBuilder
{
    private const string _prefabPath = "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab";
    private const string _scenePath = "Assets/Scenes/TacticalView.unity";
    private const string _sceneInstanceName = "TacticalViewRoot";
    private const string _shieldPrefabPath =
        "Assets/FXV/ShieldEffect/Prefabs/Domes/ShieldDome1.prefab";

    /// <summary>
    /// Rebuilds the tactical root prefab and generated scene.
    /// </summary>
    public static void Rebuild()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_prefabPath));
        GameObject root = new GameObject(_sceneInstanceName);
        try
        {
            TacticalViewController controller = root.AddComponent<TacticalViewController>();

            GameObject mapRoot = new GameObject("MapRoot", typeof(TacticalMapRenderer));
            mapRoot.transform.SetParent(root.transform, false);

            GameObject battleRoot = new GameObject("BattleRoot");
            battleRoot.transform.SetParent(root.transform, false);

            GameObject shieldTemplate = BuildShieldTemplate(root.transform);

            GameObject cameraObject = new GameObject(
                "BattleCamera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(TacticalViewCameraController)
            );
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);

            TacticalSelectionController selection =
                root.AddComponent<TacticalSelectionController>();
            TacticalSelectionView selectionView = BuildSelectionView(
                root.transform,
                out TacticalTimeControlsView timeControls
            );
            SerializedObject serializedSelection = new SerializedObject(selection);
            serializedSelection.FindProperty("view").objectReferenceValue = selectionView;
            serializedSelection.ApplyModifiedPropertiesWithoutUndo();

            BuildOptionsOverlay(
                root,
                selection,
                cameraObject.GetComponent<TacticalViewCameraController>()
            );

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            eventSystem.transform.SetParent(root.transform, false);

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("battleCamera").objectReferenceValue =
                cameraObject.GetComponent<Camera>();
            serializedController.FindProperty("battleRoot").objectReferenceValue =
                battleRoot.transform;
            serializedController.FindProperty("mapRoot").objectReferenceValue = mapRoot.transform;
            serializedController.FindProperty("timeControls").objectReferenceValue = timeControls;
            serializedController.FindProperty("weaponShader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TacticalLaserBolt.shader")
                ?? throw new IOException("The tactical weapon shader is missing.");
            serializedController.FindProperty("shieldTemplate").objectReferenceValue =
                shieldTemplate;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
            if (prefab == null)
                throw new IOException($"Could not generate tactical prefab: {_prefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        SceneBuilder.Build(_scenePath, _prefabPath, _sceneInstanceName);
    }

    /// <summary>
    /// Authors an inactive battle-local shield template from the installed effect prefab.
    /// </summary>
    /// <param name="parent">The generated tactical root.</param>
    /// <returns>The stripped shield template retained by the tactical controller.</returns>
    private static GameObject BuildShieldTemplate(Transform parent)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(_shieldPrefabPath);
        if (!source)
            throw new FileNotFoundException("The tactical shield effect is not installed.");
        GameObject template = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        PrefabUtility.UnpackPrefabInstance(
            template,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction
        );
        template.name = "ShieldTemplate";
        foreach (Collider collider in template.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Light light in template.GetComponentsInChildren<Light>(true))
            Object.DestroyImmediate(light.gameObject);
        foreach (MonoBehaviour behaviour in template.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour && behaviour.GetType().Name == "fxvSinPos")
                Object.DestroyImmediate(behaviour);
        System.Type adapter = System.Type.GetType("TacticalShieldEffect, Assembly-CSharp");
        if (adapter == null || !typeof(MonoBehaviour).IsAssignableFrom(adapter))
            throw new MissingComponentException("The tactical shield adapter is not compiled.");
        template.AddComponent(adapter);
        template.SetActive(false);
        return template;
    }

    /// <summary>
    /// Authors the shared Options window's modal surface above the tactical HUD.
    /// </summary>
    /// <param name="root">The tactical scene root.</param>
    /// <param name="selection">The world-selection input owner suspended by the menu.</param>
    /// <param name="cameraInput">The camera input owner suspended by the menu.</param>
    private static void BuildOptionsOverlay(
        GameObject root,
        TacticalSelectionController selection,
        TacticalViewCameraController cameraInput
    )
    {
        TacticalMenuController menu = root.AddComponent<TacticalMenuController>();
        OptionsMenuView prefab = AssetDatabase.LoadAssetAtPath<OptionsMenuView>(
            "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab"
        );
        if (!prefab)
            throw new FileNotFoundException(
                "Build the shared Options menu before the tactical scene."
            );
        RectTransform overlay = RectObject("OptionsOverlayCanvas", root.transform, new Rect());
        Fill(overlay, Vector2.zero, Vector2.zero);
        Canvas canvas = overlay.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = overlay.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(853.33f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        UIWindowManager manager = overlay.gameObject.AddComponent<UIWindowManager>();
        RectTransform dimmer = RectObject("OptionsDimmer", overlay, new Rect());
        Fill(dimmer, Vector2.zero, Vector2.zero);
        Image image = dimmer.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.8f);
        image.raycastTarget = true;
        RectTransform layer = RectObject("OptionsModalLayer", overlay, new Rect());
        Fill(layer, Vector2.zero, Vector2.zero);
        SerializedObject serialized = new SerializedObject(menu);
        SetReference(serialized, "menuPrefab", prefab);
        SetReference(serialized, "overlay", overlay.gameObject);
        SetReference(serialized, "windowLayer", layer);
        SetReference(serialized, "windowManager", manager);
        SetReference(serialized, "selection", selection);
        SetReferences(serialized, "battleInput", new Object[] { selection, cameraInput });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Authors a compact bottom HUD and reusable selection presentation.
    /// </summary>
    /// <param name="parent">The tactical scene root.</param>
    /// <param name="timeControls">Receives the authored tactical time-control view.</param>
    /// <returns>The authored dock and selection view.</returns>
    private static TacticalSelectionView BuildSelectionView(
        Transform parent,
        out TacticalTimeControlsView timeControls
    )
    {
        GameObject overlay = new GameObject(
            "SelectionOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        overlay.transform.SetParent(parent, false);
        overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        TacticalSelectionView view = overlay.AddComponent<TacticalSelectionView>();

        TacticalHudGraphic frame = ProjectedUnitFrame(
            "SelectionFrame",
            overlay.transform,
            Color.white,
            out _,
            out _
        );
        TacticalHudGraphic targetFrame = Art(
            "AttackTargetFrame",
            overlay.transform,
            TacticalHudGraphic.Artwork.Brackets,
            new Color(1f, 0.22f, 0.17f),
            new Rect(0f, 0f, 24f, 24f)
        );
        targetFrame.rectTransform.anchorMin =
            targetFrame.rectTransform.anchorMax =
            targetFrame.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        targetFrame.gameObject.SetActive(false);
        TacticalHudGraphic hover = ProjectedUnitFrame(
            "HoverFrame",
            overlay.transform,
            new Color(1f, 0.22f, 0.17f),
            out RectTransform hoverShieldFill,
            out RectTransform hoverHullFill
        );
        TacticalHudGraphic marquee = Art(
            "Marquee",
            overlay.transform,
            TacticalHudGraphic.Artwork.SelectionRectangle,
            Color.white,
            new Rect(0f, 0f, 24f, 24f)
        );
        marquee.rectTransform.anchorMin =
            marquee.rectTransform.anchorMax =
            marquee.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        marquee.gameObject.SetActive(false);
        TacticalHudGraphic contextualCursor = Art(
            "ContextualCursor",
            overlay.transform,
            TacticalHudGraphic.Artwork.Dock,
            new Color(0.35f, 1f, 0.62f),
            new Rect(0f, 0f, 36f, 36f)
        );
        contextualCursor.rectTransform.anchorMin =
            contextualCursor.rectTransform.anchorMax =
            contextualCursor.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        contextualCursor.gameObject.SetActive(false);
        RectTransform officerMarkerFrame = RectObject(
            "OfficerMarkerTemplate",
            overlay.transform,
            new Rect(0f, 0f, 40f, 40f)
        );
        Image officerFrame = officerMarkerFrame.gameObject.AddComponent<Image>();
        officerFrame.color = Color.white;
        officerFrame.raycastTarget = true;
        Button officerButton = officerMarkerFrame.gameObject.AddComponent<Button>();
        officerButton.targetGraphic = officerFrame;
        officerButton.navigation = new Navigation { mode = Navigation.Mode.None };
        RectTransform officerViewport = RectObject(
            "PortraitViewport",
            officerMarkerFrame,
            new Rect(2f, 2f, 36f, 36f)
        );
        RawImage officerBackground = RectObject("PortraitBackground", officerViewport, new Rect())
            .gameObject.AddComponent<RawImage>();
        officerBackground.color = new Color(0.08f, 0.32f, 0.52f);
        officerBackground.raycastTarget = false;
        Fill(officerBackground.rectTransform, Vector2.zero, Vector2.zero);
        RawImage officerMarker = RectObject("PortraitImage", officerViewport, new Rect())
            .gameObject.AddComponent<RawImage>();
        officerMarker.color = Color.white;
        officerMarker.raycastTarget = false;
        Fill(officerMarker.rectTransform, Vector2.zero, Vector2.zero);
        officerMarkerFrame.anchorMin =
            officerMarkerFrame.anchorMax =
            officerMarkerFrame.pivot =
                new Vector2(0.5f, 0.5f);
        officerMarkerFrame.gameObject.SetActive(false);

        TMP_Text fps = Label(
            "FrameRate",
            overlay.transform,
            new Rect(0f, 0f, 120f, 24f),
            16f,
            "-- FPS",
            Color.white
        );
        RectTransform fpsRect = fps.rectTransform;
        fpsRect.anchorMin = fpsRect.anchorMax = fpsRect.pivot = Vector2.one;
        fpsRect.anchoredPosition = new Vector2(-12f, -12f);
        fps.alignment = TextAlignmentOptions.TopRight;
        fps.gameObject.AddComponent<TacticalFrameRateView>();

        Color steel = new Color(0.063f, 0.082f, 0.11f);
        TacticalHudGraphic dock = Art(
            "BattleDock",
            overlay.transform,
            TacticalHudGraphic.Artwork.Housing,
            steel,
            new Rect(0f, 0f, 0f, 288f)
        );
        StretchBottom(dock.rectTransform, 8f, 8f, 0f, 288f);
        dock.enabled = false;
        TacticalHudGraphic left = Art(
            "InstrumentsHousing",
            dock.transform,
            TacticalHudGraphic.Artwork.Housing,
            steel,
            new Rect(0f, 0f, 250f, 288f)
        );
        left.raycastTarget = true;
        TacticalHudGraphic radarWell = Art(
            "RadarWell",
            left.transform,
            TacticalHudGraphic.Artwork.Well,
            new Color(0.02f, 0.03f, 0.03f),
            new Rect(10f, 10f, 230f, 230f)
        );
        TacticalRadarView radar = RectObject(
            "Radar",
            radarWell.transform,
            new Rect(9f, 9f, 212f, 212f)
        )
            .gameObject.AddComponent<TacticalRadarView>();
        radar.raycastTarget = true;
        timeControls = BuildTimeControls(left.transform);

        TacticalHudGraphic rosterHousing = Art(
            "RosterHousing",
            dock.transform,
            TacticalHudGraphic.Artwork.Housing,
            steel,
            new Rect(0f, 0f, 0f, 226f)
        );
        StretchBottom(rosterHousing.rectTransform, 488f, 0f, 0f, 226f);
        rosterHousing.raycastTarget = true;
        TMP_Text rosterTitle = Label(
            "RosterTitle",
            rosterHousing.transform,
            new Rect(10f, 202f, 360f, 20f),
            11f,
            "SELECTED UNITS  //  000",
            new Color(0.6f, 0.67f, 0.75f)
        );
        TacticalHudGraphic rosterWell = Art(
            "RosterWell",
            rosterHousing.transform,
            TacticalHudGraphic.Artwork.Well,
            new Color(0.015f, 0.022f, 0.025f),
            new Rect()
        );
        Fill(rosterWell.rectTransform, new Vector2(6f, 6f), new Vector2(-6f, -30f));
        ScrollRect scroll = rosterWell.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = RectObject("Viewport", rosterWell.transform, new Rect());
        Fill(viewport, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = RectObject("Roster", viewport, new Rect(0f, 0f, 0f, 0f));
        content.anchorMin = Vector2.zero;
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        GridLayoutGroup layout = content.gameObject.AddComponent<GridLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.cellSize = new Vector2(130f, 88f);
        layout.spacing = new Vector2(5f, 5f);
        layout.startAxis = GridLayoutGroup.Axis.Vertical;
        layout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        layout.constraintCount = 2;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        TacticalUnitCardView card = BuildCard(overlay.transform);
        card.gameObject.SetActive(false);
        BuildControlGroups(
            rosterHousing.transform,
            out Button[] controlGroupButtons,
            out TMP_Text[] controlGroupLabels,
            out TacticalHudGraphic[] controlGroupFaces
        );

        TacticalHudGraphic commands = Art(
            "CommandsHousing",
            dock.transform,
            TacticalHudGraphic.Artwork.Housing,
            steel,
            new Rect(260f, 0f, 218f, 226f)
        );
        commands.raycastTarget = true;
        Label(
            "CommandsTitle",
            commands.transform,
            new Rect(9f, 202f, 200f, 20f),
            11f,
            "COMMANDS",
            new Color(0.6f, 0.67f, 0.75f)
        );
        TacticalHudGraphic[] keys = new TacticalHudGraphic[10 + controlGroupFaces.Length];
        string[] names =
        {
            "Move",
            "Attack",
            "Stop",
            "Guard",
            "Focus",
            "Retreat",
            "Launch",
            "Recall",
            "Dock",
        };
        TacticalHudGraphic.Artwork[] icons =
        {
            TacticalHudGraphic.Artwork.Move,
            TacticalHudGraphic.Artwork.Attack,
            TacticalHudGraphic.Artwork.Stop,
            TacticalHudGraphic.Artwork.Guard,
            TacticalHudGraphic.Artwork.Focus,
            TacticalHudGraphic.Artwork.Retreat,
        };
        for (int i = 0; i < names.Length; i++)
            keys[i] = BuildCommand(
                commands.transform,
                names[i],
                names[i],
                i < icons.Length ? icons[i] : null,
                new Vector2(7f + i % 3 * 70f, 133f - i / 3 * 63f)
            );

        keys[9] = BuildFollowButton(dock.transform, out TMP_Text followLabel);
        for (int index = 0; index < controlGroupFaces.Length; index++)
            keys[index + 10] = controlGroupFaces[index];
        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "selectionFrame", frame.rectTransform);
        SetReference(serialized, "attackTargetFrame", targetFrame.rectTransform);
        SetReference(serialized, "hoverFrame", hover.rectTransform);
        SetReference(serialized, "hoverHullFill", hoverHullFill);
        SetReference(serialized, "hoverShieldFill", hoverShieldFill);
        SetReference(serialized, "marquee", marquee.rectTransform);
        SetReference(serialized, "cardTemplate", card);
        SetReference(serialized, "rosterContent", content);
        SetReference(serialized, "rosterScroll", scroll);
        SetReference(serialized, "rosterTitle", rosterTitle);
        SetReference(serialized, "radar", radar);
        SetReference(
            serialized,
            "followButton",
            keys[9].GetComponentInParent<TacticalCommandButton>()
        );
        SetReference(serialized, "followLabel", followLabel);
        SetReferences(serialized, "controlGroupButtons", controlGroupButtons);
        SetReferences(serialized, "controlGroupLabels", controlGroupLabels);
        SetReference(serialized, "contextualCursor", contextualCursor);
        SetReference(serialized, "officerMarkerTemplate", officerMarkerFrame);
        SetReferences(serialized, "housings", new Object[] { left, rosterHousing, commands });
        SetReferences(serialized, "keys", keys);
        SetReference(
            serialized,
            "moveButton",
            keys[0].GetComponentInParent<TacticalCommandButton>()
        );
        SetReference(
            serialized,
            "attackButton",
            keys[1].GetComponentInParent<TacticalCommandButton>()
        );
        SetReference(
            serialized,
            "stopButton",
            keys[2].GetComponentInParent<TacticalCommandButton>()
        );
        SetReference(
            serialized,
            "launchButton",
            keys[6].GetComponentInParent<TacticalCommandButton>()
        );
        SetReference(
            serialized,
            "recallButton",
            keys[7].GetComponentInParent<TacticalCommandButton>()
        );
        SetReference(
            serialized,
            "dockButton",
            keys[8].GetComponentInParent<TacticalCommandButton>()
        );
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    /// <summary>
    /// Authors nine numbered control-group slots aligned above the selected-unit roster.
    /// </summary>
    /// <param name="parent">The roster housing that establishes responsive centering.</param>
    /// <param name="buttons">Receives the authored group buttons.</param>
    /// <param name="labels">Receives the visible group numbers.</param>
    /// <param name="faces">Receives the theme-tintable key faces.</param>
    private static void BuildControlGroups(
        Transform parent,
        out Button[] buttons,
        out TMP_Text[] labels,
        out TacticalHudGraphic[] faces
    )
    {
        const float keySize = 36f;
        const float gap = 6f;
        float width =
            TacticalSelectionController.ControlGroupCount * keySize
            + (TacticalSelectionController.ControlGroupCount - 1) * gap;
        RectTransform row = RectObject("ControlGroups", parent, new Rect(0f, 0f, width, keySize));
        row.anchorMin = row.anchorMax = Vector2.zero;
        row.pivot = Vector2.zero;
        row.anchoredPosition = new Vector2(10f, 236f);
        buttons = new Button[TacticalSelectionController.ControlGroupCount];
        labels = new TMP_Text[buttons.Length];
        faces = new TacticalHudGraphic[buttons.Length];
        for (int index = 0; index < buttons.Length; index++)
        {
            TacticalHudGraphic face = Art(
                $"ControlGroup{index + 1}",
                row,
                TacticalHudGraphic.Artwork.Key,
                new Color(0.125f, 0.165f, 0.212f),
                new Rect(index * (keySize + gap), 0f, keySize, keySize)
            );
            face.raycastTarget = true;
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            TMP_Text label = Label(
                "Label",
                face.transform,
                new Rect(0f, 0f, keySize, keySize),
                14f,
                (index + 1).ToString(),
                new Color(0.38f, 0.43f, 0.5f)
            );
            label.alignment = TextAlignmentOptions.Center;
            buttons[index] = button;
            labels[index] = label;
            faces[index] = face;
        }
    }

    /// <summary>
    /// Authors a dedicated camera key in the space above, and outside, the nine-command housing.
    /// </summary>
    /// <param name="parent">The centered bottom dock.</param>
    /// <param name="label">The label switching between follow and unfollow.</param>
    /// <returns>The tintable camera key face.</returns>
    private static TacticalHudGraphic BuildFollowButton(Transform parent, out TMP_Text label)
    {
        RectTransform root = RectObject("FollowSelected", parent, new Rect(260f, 238f, 218f, 42f));
        root.gameObject.SetActive(false);
        TacticalHudGraphic face = Art(
            "KeyFace",
            root,
            TacticalHudGraphic.Artwork.Key,
            new Color(0.125f, 0.165f, 0.212f),
            new Rect(0f, 2f, 218f, 40f)
        );
        face.raycastTarget = true;
        label = Label(
            "Label",
            face.transform,
            new Rect(4f, 4f, 210f, 32f),
            12f,
            "FOLLOW SELECTED",
            new Color(0.85f, 0.89f, 0.94f)
        );
        label.alignment = TextAlignmentOptions.Center;
        TacticalCommandButton button = root.gameObject.AddComponent<TacticalCommandButton>();
        button.targetGraphic = face;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.interactable = false;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        SerializedObject serialized = new SerializedObject(button);
        SetReference(serialized, "keyFace", face.rectTransform);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(true);
        return face;
    }

    /// <summary>
    /// Authors a reusable roster card with an independently bound portrait and bars.
    /// </summary>
    /// <param name="parent">The template owner.</param>
    /// <returns>The inactive card template.</returns>
    private static TacticalUnitCardView BuildCard(Transform parent)
    {
        TacticalHudGraphic face = Art(
            "UnitCardTemplate",
            parent,
            TacticalHudGraphic.Artwork.Well,
            new Color(0.025f, 0.03f, 0.032f),
            new Rect(0f, 0f, 130f, 88f)
        );
        face.raycastTarget = true;
        Button button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TacticalUnitCardView card = face.gameObject.AddComponent<TacticalUnitCardView>();
        TMP_Text count = Label(
            "UnitCount",
            face.transform,
            new Rect(5f, 66f, 120f, 19f),
            12f,
            "x01",
            Color.white
        );
        count.alignment = TextAlignmentOptions.Center;
        RectTransform portraitArea = RectObject(
            "PortraitArea",
            face.transform,
            new Rect(5f, 23f, 120f, 44f)
        );
        RawImage portrait = RectObject("Portrait", portraitArea, new Rect())
            .gameObject.AddComponent<RawImage>();
        Fill(portrait.rectTransform, Vector2.zero, Vector2.zero);
        portrait.raycastTarget = false;
        AspectRatioFitter aspect = portrait.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        Color shieldColor = new Color(0.2f, 0.66f, 0.89f);
        Color hullColor = new Color(0.35f, 0.85f, 0.58f);
        RectTransform shields = Bar("ShieldBar", face.transform, 19f, shieldColor);
        RectTransform hull = Bar("HullBar", face.transform, 11f, hullColor);
        Button[] members = BuildMemberButtons(face.transform);
        TacticalHudGraphic tooltip = Art(
            "DurabilityTooltip",
            face.transform,
            TacticalHudGraphic.Artwork.Well,
            new Color(0.018f, 0.024f, 0.026f),
            new Rect(4f, 27f, 120f, 42f)
        );
        TMP_Text shieldLabel = Label(
            "ShieldLabel",
            tooltip.transform,
            new Rect(6f, 21f, 108f, 18f),
            11f,
            string.Empty,
            Color.white
        );
        TMP_Text hullLabel = Label(
            "HullLabel",
            tooltip.transform,
            new Rect(6f, 3f, 108f, 18f),
            11f,
            string.Empty,
            Color.white
        );
        shieldLabel.enableAutoSizing = true;
        shieldLabel.fontSizeMin = 9f;
        hullLabel.enableAutoSizing = true;
        hullLabel.fontSizeMin = 9f;
        tooltip.gameObject.SetActive(false);
        SerializedObject serialized = new SerializedObject(card);
        SetReference(serialized, "unitCount", count);
        SetReferences(serialized, "memberButtons", members);
        SetReference(serialized, "portrait", portrait);
        SetReference(serialized, "portraitAspect", aspect);
        SetReference(serialized, "hullFill", hull);
        SetReference(serialized, "shieldFill", shields);
        SetReference(serialized, "hullLabel", hullLabel);
        SetReference(serialized, "shieldLabel", shieldLabel);
        SetReference(serialized, "button", button);
        SetReference(serialized, "durabilityTooltip", tooltip.gameObject);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return card;
    }

    /// <summary>
    /// Authors the grouped card's individual-member selection pips.
    /// </summary>
    /// <param name="parent">The roster card.</param>
    /// <returns>The reusable member buttons, limited to the card's fifteen visible positions.</returns>
    private static Button[] BuildMemberButtons(Transform parent)
    {
        Button[] buttons = new Button[15];
        for (int i = 0; i < buttons.Length; i++)
        {
            Image pip = RectObject($"Member{i + 1}", parent, new Rect(5f + i * 8f, 2f, 7f, 8f))
                .gameObject.AddComponent<Image>();
            Button button = pip.gameObject.AddComponent<Button>();
            button.targetGraphic = pip;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            buttons[i] = button;
        }
        return buttons;
    }

    /// <summary>
    /// Authors one tactile command key with standard pointer and keyboard-submit feedback.
    /// </summary>
    /// <param name="parent">The command housing.</param>
    /// <param name="name">The command object name.</param>
    /// <param name="text">The visible label, or empty for an unassigned slot.</param>
    /// <param name="icon">The command symbol, or null for an unassigned slot.</param>
    /// <param name="position">The key position.</param>
    /// <returns>The tintable key face.</returns>
    private static TacticalHudGraphic BuildCommand(
        Transform parent,
        string name,
        string text,
        TacticalHudGraphic.Artwork? icon,
        Vector2 position
    )
    {
        RectTransform root = RectObject(name, parent, new Rect(position, new Vector2(64f, 57f)));
        root.gameObject.SetActive(false);
        TacticalHudGraphic socket = Art(
            "Socket",
            root,
            TacticalHudGraphic.Artwork.Well,
            Color.black,
            new Rect(0f, 0f, 64f, 57f)
        );
        socket.raycastTarget = true;
        TacticalHudGraphic face = Art(
            "KeyFace",
            root,
            TacticalHudGraphic.Artwork.Key,
            new Color(0.125f, 0.165f, 0.212f),
            new Rect(0f, 2f, 64f, 55f)
        );
        if (icon.HasValue)
            Art(
                "Symbol",
                face.transform,
                icon.Value,
                new Color(0.85f, 0.89f, 0.94f),
                new Rect(20f, 23f, 24f, 24f)
            );
        TMP_Text label = Label(
            "Label",
            face.transform,
            new Rect(2f, 2f, 60f, 18f),
            8.5f,
            text,
            new Color(0.85f, 0.89f, 0.94f)
        );
        label.alignment = TextAlignmentOptions.Center;
        TacticalCommandButton button = root.gameObject.AddComponent<TacticalCommandButton>();
        button.targetGraphic = face;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        SerializedObject serialized = new SerializedObject(button);
        SetReference(serialized, "keyFace", face.rectTransform);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(true);
        return face;
    }

    /// <summary>
    /// Authors the dock's tactical-only time controls.
    /// </summary>
    /// <param name="parent">The instrument housing.</param>
    /// <returns>The view presenting and dispatching tactical speed selections.</returns>
    private static TacticalTimeControlsView BuildTimeControls(Transform parent)
    {
        string[] labels = { "||", "0.5", "1", "2", "4" };
        Button[] buttons = new Button[labels.Length];
        TacticalHudGraphic[] faces = new TacticalHudGraphic[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            TacticalHudGraphic face = Art(
                $"TimeControl{i}",
                parent,
                TacticalHudGraphic.Artwork.Key,
                new Color(0.015f, 0.085f, 0.12f),
                new Rect(10f + i * 47.5f, 240f, 40f, 40f)
            );
            face.raycastTarget = true;
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.72f, 0.96f, 1f);
            colors.pressedColor = new Color(0.38f, 0.82f, 0.9f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            TMP_Text label = Label(
                "Label",
                face.transform,
                new Rect(0f, 0f, 40f, 40f),
                13f,
                labels[i],
                new Color(0.76f, 0.94f, 1f)
            );
            label.alignment = TextAlignmentOptions.Center;
            buttons[i] = button;
            faces[i] = face;
        }

        TacticalTimeControlsView view = parent.gameObject.AddComponent<TacticalTimeControlsView>();
        SerializedObject serialized = new SerializedObject(view);
        SetReferences(serialized, "buttons", buttons);
        SetReferences(serialized, "faces", faces);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    /// <summary>
    /// Creates a compact durability fill inside its recessed track.
    /// </summary>
    /// <param name="name">The track name.</param>
    /// <param name="parent">The card owner.</param>
    /// <param name="y">The track position.</param>
    /// <param name="color">The fill color.</param>
    /// <returns>The fill rectangle.</returns>
    private static RectTransform Bar(string name, Transform parent, float y, Color color)
    {
        Image track = RectObject(name, parent, new Rect(5f, y, 120f, 6f))
            .gameObject.AddComponent<Image>();
        track.color = new Color(0.07f, 0.09f, 0.1f);
        track.raycastTarget = false;
        Image fill = RectObject("Fill", track.transform, new Rect())
            .gameObject.AddComponent<Image>();
        fill.color = color;
        fill.raycastTarget = false;
        Fill(fill.rectTransform, Vector2.zero, Vector2.zero);
        return fill.rectTransform;
    }

    /// <summary>
    /// Creates a durability bar that stretches with a projected hull frame.
    /// </summary>
    /// <param name="name">The track name.</param>
    /// <param name="parent">The projected frame.</param>
    /// <param name="y">The vertical offset beneath the frame.</param>
    /// <param name="color">The fill color.</param>
    /// <returns>The fill rectangle whose horizontal anchor represents current durability.</returns>
    private static RectTransform ProjectedBar(string name, Transform parent, float y, Color color)
    {
        Image track = RectObject(name, parent, new Rect()).gameObject.AddComponent<Image>();
        track.color = new Color(0.025f, 0.035f, 0.04f, 0.95f);
        track.raycastTarget = false;
        track.rectTransform.anchorMin = new Vector2(0f, 0f);
        track.rectTransform.anchorMax = new Vector2(1f, 0f);
        track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        track.rectTransform.offsetMin = new Vector2(4f, y);
        track.rectTransform.offsetMax = new Vector2(-4f, y + 4f);
        Image fill = RectObject("Fill", track.transform, new Rect())
            .gameObject.AddComponent<Image>();
        fill.color = color;
        fill.raycastTarget = false;
        Fill(fill.rectTransform, Vector2.zero, Vector2.zero);
        return fill.rectTransform;
    }

    /// <summary>
    /// Authors the shared projected hull brackets and durability bars used for selected and hovered ships.
    /// </summary>
    /// <param name="name">The frame object name.</param>
    /// <param name="parent">The screen-space overlay receiving the frame.</param>
    /// <param name="color">The bracket color identifying the contact state.</param>
    /// <param name="shieldFill">Receives the shared shield-bar fill.</param>
    /// <param name="hullFill">Receives the shared hull-bar fill.</param>
    /// <returns>The inactive projected frame.</returns>
    private static TacticalHudGraphic ProjectedUnitFrame(
        string name,
        Transform parent,
        Color color,
        out RectTransform shieldFill,
        out RectTransform hullFill
    )
    {
        TacticalHudGraphic frame = Art(
            name,
            parent,
            TacticalHudGraphic.Artwork.Brackets,
            color,
            new Rect(0f, 0f, 24f, 24f)
        );
        frame.rectTransform.anchorMin =
            frame.rectTransform.anchorMax =
            frame.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        shieldFill = ProjectedBar("ShieldBar", frame.transform, -9f, new Color(0.2f, 0.66f, 0.89f));
        hullFill = ProjectedBar("HullBar", frame.transform, -15f, new Color(0.35f, 0.85f, 0.58f));
        frame.gameObject.SetActive(false);
        return frame;
    }

    /// <summary>
    /// Creates an authored rectangle in bottom-left local coordinates.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owner.</param>
    /// <param name="bounds">The local position and size.</param>
    /// <returns>The authored rectangle.</returns>
    private static RectTransform RectObject(string name, Transform parent, Rect bounds)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = bounds.position;
        rect.sizeDelta = bounds.size;
        return rect;
    }

    /// <summary>
    /// Creates vector artwork without coupling the HUD to fixed-resolution bitmaps.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <param name="artwork">The vector artwork.</param>
    /// <param name="color">The face color.</param>
    /// <param name="bounds">The local rectangle.</param>
    /// <returns>The authored graphic.</returns>
    private static TacticalHudGraphic Art(
        string name,
        Transform parent,
        TacticalHudGraphic.Artwork artwork,
        Color color,
        Rect bounds
    )
    {
        TacticalHudGraphic graphic = RectObject(name, parent, bounds)
            .gameObject.AddComponent<TacticalHudGraphic>();
        graphic.SetArtwork(artwork);
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    /// <summary>
    /// Stretches a rectangle between its parent's edges.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="minimum">The bottom-left inset.</param>
    /// <param name="maximum">The top-right inset.</param>
    private static void Fill(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
    }

    /// <summary>
    /// Anchors a full-width region to the bottom without scaling it to ultrawide width.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="left">The left inset.</param>
    /// <param name="right">The right inset.</param>
    /// <param name="bottom">The bottom offset.</param>
    /// <param name="height">The fixed height.</param>
    private static void StretchBottom(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float height
    )
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    /// <summary>
    /// Authors a noninteractive label using the application font.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="parent">The owner.</param>
    /// <param name="bounds">The local rectangle.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The initial text.</param>
    /// <param name="color">The text color.</param>
    /// <returns>The authored label.</returns>
    private static TMP_Text Label(
        string name,
        Transform parent,
        Rect bounds,
        float size,
        string text,
        Color color
    )
    {
        TMP_Text label = RectObject(name, parent, bounds)
            .gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"
        );
        if (!label.font)
            throw new FileNotFoundException("The tactical dock font is missing.");
        label.fontSize = size;
        label.color = color;
        label.text = text;
        label.alignment = TextAlignmentOptions.Left;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// Assigns one authored component reference.
    /// </summary>
    /// <param name="serialized">The component being authored.</param>
    /// <param name="name">The serialized field.</param>
    /// <param name="value">The authored reference.</param>
    private static void SetReference(SerializedObject serialized, string name, Object value) =>
        serialized.FindProperty(name).objectReferenceValue = value;

    /// <summary>
    /// Assigns an authored component-reference array.
    /// </summary>
    /// <param name="serialized">The component being authored.</param>
    /// <param name="name">The serialized field.</param>
    /// <param name="values">The authored references.</param>
    private static void SetReferences(SerializedObject serialized, string name, Object[] values)
    {
        SerializedProperty array = serialized.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
