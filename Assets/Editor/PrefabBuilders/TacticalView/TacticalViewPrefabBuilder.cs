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
            TacticalSelectionView selectionView = BuildSelectionView(root.transform);
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

    /// <summary>Authors the shared Options window's modal surface above the tactical HUD.</summary>
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
        SetReferences(serialized, "battleInput", new Object[] { selection, cameraInput });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
    }

    /// <summary>Authors a compact bottom HUD and reusable selection presentation.</summary>
    /// <param name="parent">The tactical scene root.</param>
    /// <returns>The authored dock and selection view.</returns>
    private static TacticalSelectionView BuildSelectionView(Transform parent)
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

        TacticalHudGraphic frame = Art(
            "SelectionFrame",
            overlay.transform,
            TacticalHudGraphic.Artwork.Brackets,
            Color.white,
            new Rect(0f, 0f, 24f, 24f)
        );
        frame.rectTransform.anchorMin =
            frame.rectTransform.anchorMax =
            frame.rectTransform.pivot =
                new Vector2(0.5f, 0.5f);
        frame.gameObject.SetActive(false);
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
        BuildTimeControls(left.transform);
        BuildLights(left.transform, new Vector2(207f, 17f), 0.7f);

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
        layout.childAlignment = TextAnchor.MiddleLeft;
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
        TacticalHudGraphic[] keys = new TacticalHudGraphic[9];
        string[] names = { "Move", "Attack", "Stop", "Guard", "Focus", "Retreat", "", "", "" };
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
                i < icons.Length ? names[i] : $"CommandSlot{i + 1}",
                names[i],
                i < icons.Length ? icons[i] : null,
                new Vector2(7f + i % 3 * 70f, 133f - i / 3 * 63f)
            );

        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "selectionFrame", frame.rectTransform);
        SetReference(serialized, "marquee", marquee.rectTransform);
        SetReference(serialized, "cardTemplate", card);
        SetReference(serialized, "rosterContent", content);
        SetReference(serialized, "rosterScroll", scroll);
        SetReference(serialized, "rosterTitle", rosterTitle);
        SetReference(serialized, "radar", radar);
        SetReferences(serialized, "housings", new Object[] { left, rosterHousing, commands });
        SetReferences(serialized, "keys", keys);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    /// <summary>Authors a reusable roster card with an independently bound portrait and bars.</summary>
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
        TMP_Text name = Label(
            "UnitName",
            face.transform,
            new Rect(5f, 66f, 88f, 19f),
            9.5f,
            string.Empty,
            Color.white
        );
        name.enableAutoSizing = true;
        name.fontSizeMin = 8.5f;
        TMP_Text count = Label(
            "UnitCount",
            face.transform,
            new Rect(96f, 66f, 29f, 19f),
            11f,
            "x01",
            Color.white
        );
        count.alignment = TextAlignmentOptions.TopRight;
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
        SetReference(serialized, "unitName", name);
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

    /// <summary>Authors the grouped card's individual-member selection pips.</summary>
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

    /// <summary>Authors one tactile command key with standard pointer and keyboard-submit feedback.</summary>
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

    /// <summary>Authors the dock's time-control placeholders without changing global simulation time.</summary>
    /// <param name="parent">The instrument housing.</param>
    private static void BuildTimeControls(Transform parent)
    {
        string[] labels = { "||", "0.5", "1", "2", "4" };
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
        }
    }

    /// <summary>Authors three small status lights with staggered presentation-only blinking.</summary>
    /// <param name="parent">The instrument housing.</param>
    /// <param name="position">The lamp bank position.</param>
    /// <param name="phase">The independent blink offset.</param>
    private static void BuildLights(Transform parent, Vector2 position, float phase)
    {
        TacticalHudGraphic bank = Art(
            "LampBank",
            parent,
            TacticalHudGraphic.Artwork.Well,
            new Color(0.015f, 0.018f, 0.02f),
            new Rect(position, new Vector2(24f, 3f))
        );
        Image[] lamps = new Image[3];
        for (int i = 0; i < lamps.Length; i++)
        {
            Image lamp = RectObject($"Lamp{i}", bank.transform, new Rect(i * 9f, 0f, 3f, 3f))
                .gameObject.AddComponent<Image>();
            lamp.color = i == 0 ? new Color(0.35f, 0.65f, 0.55f) : new Color(0.25f, 0.32f, 0.39f);
            lamp.raycastTarget = false;
            lamps[i] = lamp;
        }
        TacticalPanelLights lights = bank.gameObject.AddComponent<TacticalPanelLights>();
        SerializedObject serialized = new SerializedObject(lights);
        SetReferences(serialized, "lamps", lamps);
        serialized.FindProperty("phaseOffset").floatValue = phase;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Creates a compact durability fill inside its recessed track.</summary>
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

    /// <summary>Creates an authored rectangle in bottom-left local coordinates.</summary>
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

    /// <summary>Creates vector artwork without coupling the HUD to fixed-resolution bitmaps.</summary>
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

    /// <summary>Stretches a rectangle between its parent's edges.</summary>
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

    /// <summary>Anchors a full-width region to the bottom without scaling it to ultrawide width.</summary>
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

    /// <summary>Authors a noninteractive label using the application font.</summary>
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

    /// <summary>Assigns one authored component reference.</summary>
    /// <param name="serialized">The component being authored.</param>
    /// <param name="name">The serialized field.</param>
    /// <param name="value">The authored reference.</param>
    private static void SetReference(SerializedObject serialized, string name, Object value) =>
        serialized.FindProperty(name).objectReferenceValue = value;

    /// <summary>Assigns an authored component-reference array.</summary>
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
