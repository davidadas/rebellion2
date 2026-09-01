using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the MainMenuRoot prefab from scratch in one pass: the base hierarchy is authored with
/// <c>new GameObject(...)</c>, then the view bindings, the spinning-planet backdrop, and the
/// spinning 3D icon rigs are installed, and the result is saved once. No authored prefab is loaded.
/// </summary>
public static class MainMenuPrefabBuilder
{
    private const string _scenePath = "Assets/Scenes/MainMenu.unity";
    private const string _optionsMenuPrefabPath =
        "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";
    private const string _sceneInstanceName = "MainMenuRoot";

    // Prefab + authored-asset paths.
    private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";
    private const string _standardVictorySpriteAddress =
        "Application/MainMenu/UI/ui_mainmenu_hq_icon";
    private const string _victoryTextFontPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Faction identifiers bound to the two launch buttons.
    private const string _empireFactionId = "FNEMP1";
    private const string _rebelFactionId = "FNALL1";

    // Resource paths for the SFX cues authored on pointer event triggers.
    private const string _selectSfxPath = "Application/MainMenu/Audio/select";
    private const string _galaxySizeSelectSfxPath = "Application/MainMenu/Audio/galaxysize-select";
    private const string _factionSelectSfxPath = "Application/MainMenu/Audio/faction-select";
    private const string _exitSelectSfxPath = _factionSelectSfxPath;

    // Icon turntable speed: one full revolution per second (matched the original 2D flipbook loop).
    private const float _iconTurnDegreesPerSecond = 360f;

    // Spinning-planet backdrop.
    private const string _starfieldAddress = "Application/MainMenu/UI/starfield";
    private const string _cloudTextureAddress = "Application/MainMenu/UI/clouds";
    private const string _cloudShaderName = "Custom/PlanetClouds";
    private const string _atmosphereShaderName = "Custom/AtmosphereRim";
    private const string _planetDayNightShaderName = "Custom/PlanetDayNightShade";
    private const string _renderTexturePath = "Assets/Art/Models/MainMenu/Planet.renderTexture";
    private const string _citadelModelAddress = "Application/MainMenu/Models/citadel";
    private const string _citadelRenderTexturePath =
        "Assets/Art/Models/MainMenu/HqCitadel.renderTexture";
    private const string _rigName = "PlanetRig";
    private const string _backdropName = "SpaceBackdrop";
    private const string _foregroundName = "Cockpit";
    private const float _cloudSpinDegreesPerSecond = 1f / 3f;
    private static readonly Vector3 _planetSunDirection = new Vector3(
        0.80f,
        0.46f,
        -0.38f
    ).normalized;
    private static readonly Vector3 _planetRigOrigin = new Vector3(12000f, 12000f, 12000f);

    // Spinning 3D icon rigs.
    private const string _iconRigsName = "IconRigs"; // container for the lights and all icon rigs
    private static readonly Vector3 _rigOrigin = new Vector3(10000f, 10000f, 10000f);

    // Each rig sits this far from the next along Z so one icon's camera never captures the
    // other's model (camera far clip is 20, so 1000 units of separation is comfortably isolated).
    private const float _rigSpacing = 1000f;
    private const string _lightsName = "SharedLights";

    // The five icon slots -- two faction coins and three difficulty ships/sphere -- each carrying
    // its model + RenderTexture paths and the orientation its rig applies. (Face is defined below.)
    private static readonly Face[] _faces =
    {
        // Left button is bound to the Empire faction -> the gear medallion (coin, own material).
        // The coin imports facing +Y, so tilt it 90 deg to face the camera.
        new Face(
            "Faction1",
            "Assets/Art/Models/MainMenu/medallion_empire.fbx",
            "Assets/Art/Models/MainMenu/Medallion_Empire.renderTexture",
            "LeftFactionLaunchButton",
            -1f,
            Vector3.up,
            1f,
            new Vector3(90f, 0f, 0f)
        ),
        // Right button is bound to the Alliance/Rebel faction -> the starbird medallion (coin).
        // 270 deg (not 90) faces the camera the right way up: crest at top, not inverted.
        new Face(
            "Faction2",
            "Assets/Art/Models/MainMenu/medallion_rebel.fbx",
            "Assets/Art/Models/MainMenu/Medallion_Rebel.renderTexture",
            "RightFactionLaunchButton",
            1f,
            Vector3.up,
            1f,
            new Vector3(270f, 0f, 0f)
        ),
        // Easy difficulty toggle -> the 3D X-Wing, replacing its spinning flipbook icon.
        // Nose points left and slightly down; it barrel-rolls counter-clockwise on its long
        // (nose-to-tail, X) axis, matching the original flipbook. RT is landscape to fit the
        // toggle without squashing.
        new Face(
            "EasyDifficulty",
            "Assets/Art/Models/MainMenu/xwing.fbx",
            "Assets/Art/Models/MainMenu/XWing.renderTexture",
            "EasyDifficultyToggle",
            1f,
            Vector3.up, // vertical-axis turntable (basketball-on-a-finger), CCW
            1.05f,
            new Vector3(90f, 180f, 0f), // upright, nose to screen-left at rest
            new Vector3(-18f, 0f, 0f), // negative pitch looks down on it (birds-eye), not up
            512,
            360,
            true
        ),
        // Medium difficulty toggle -> 3D Star Destroyer (upright side profile, vertical turntable).
        new Face(
            "MediumDifficulty",
            "Assets/Art/Models/MainMenu/stardestroyer.fbx",
            "Assets/Art/Models/MainMenu/StarDestroyer.renderTexture",
            "MediumDifficultyToggle",
            1f,
            Vector3.up,
            1.55f,
            // The FBX imports tipped onto its back; -90 about X stands it deck-up and level.
            new Vector3(-90f, 0f, 0f),
            // Negative pitch looks down on the deck (from above), matching the original flipbook;
            // a positive pitch looked up at the belly (worm's-eye).
            new Vector3(-18f, 0f, 0f),
            512,
            360,
            true
        ),
        // Hard difficulty toggle -> 3D Death Star (sphere, vertical turntable).
        new Face(
            "HardDifficulty",
            "Assets/Art/Models/MainMenu/deathstar.fbx",
            "Assets/Art/Models/MainMenu/DeathStar.renderTexture",
            "HardDifficultyToggle",
            1f,
            Vector3.up,
            1.167f,
            // Same tipped-on-its-back import; -90 about X brings a pole up so the trench sits
            // horizontal and the superlaser dish faces front instead of looking down the pole.
            new Vector3(-90f, 0f, 0f),
            // Negative pitch views from slightly above like the ships (subtle on a sphere).
            new Vector3(-10f, 0f, 0f),
            512,
            360,
            true,
            underLightIntensity: 2.5f
        ),
    };

    /// <summary>
    /// Rebuilds the main-menu prefab: view bindings, the spinning planet, and the 3D icon rigs.
    /// </summary>
    public static void RebuildMainMenuPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_prefabPath));
        GameObject root = new GameObject("MainMenuRoot");
        try
        {
            BuildBaseHierarchy(root);
            RebuildViewBindings(root);
            InstallPlanet(root);
            InstallSpinning3DIcons(root);
            InstallHqCitadel(root);
            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Authors the base MainMenuRoot hierarchy (services, controller, canvas, and every control)
    /// exactly as the prefab specifies, leaving the icon rigs and planet rig to their own phases.
    /// </summary>
    /// <param name="root">The freshly created prefab root.</param>
    private static void BuildBaseHierarchy(GameObject root)
    {
        BuildMainCamera(root);
        BuildEventSystem(root);
        BuildServices(root);
        MainMenuController controller = BuildController(root);
        BuildCanvas(root);
        PopulateViewBindings(root, controller.GetComponent<MainMenuView>());
    }

    /// <summary>
    /// Authors the camera and audio listener owned by the Main Menu scene root.
    /// </summary>
    /// <param name="root">The scene-root prefab receiving the infrastructure.</param>
    private static void BuildMainCamera(GameObject root)
    {
        GameObject cameraObject = NewChild("Main Camera", root.transform);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraObject.AddComponent<AudioListener>();
    }

    /// <summary>
    /// Authors the event system required by the self-contained Main Menu root.
    /// </summary>
    private static void BuildEventSystem(GameObject root)
    {
        GameObject eventSystem = NewChild("EventSystem", root.transform);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    /// <summary>
    /// Pre-populates the view's serialized bindings so the view-binding phase reads them back
    /// instead of discovering them from persistent calls (the authored prefab holds no such calls).
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <param name="view">The main-menu view.</param>
    private static void PopulateViewBindings(GameObject root, MainMenuView view)
    {
        List<(Toggle Toggle, int Value)> galaxySizes = DiscoverToggleBindings<GameSize>(
            root,
            "GalaxySizeGroup"
        );
        List<(Toggle Toggle, int Value)> difficulties = DiscoverToggleBindings<GameDifficulty>(
            root,
            "DifficultyGroup"
        );

        List<(Button Button, string FactionId)> factionLaunches = new List<(
            Button Button,
            string FactionId
        )>
        {
            (FindRequiredComponent<Button>(root, "LeftFactionLaunchButton"), _empireFactionId),
            (FindRequiredComponent<Button>(root, "RightFactionLaunchButton"), _rebelFactionId),
        };

        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> audioCues =
            CollectAudioCueBindings(root);

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("loadGameButton").objectReferenceValue =
            FindRequiredComponent<Button>(root, "LoadGameButton");
        serializedView.FindProperty("exitButton").objectReferenceValue =
            FindRequiredComponent<Button>(root, "ExitButton");
        serializedView.FindProperty("exitPressedImage").objectReferenceValue =
            FindRequiredComponent<Button>(root, "ExitButton")
                .transform.Find("PressedImage")
                .gameObject;
        serializedView.FindProperty("exitConfirmationDialog").objectReferenceValue =
            FindRequiredComponent<ConfirmationDialogView>(root, "ConfirmDialog");
        serializedView.FindProperty("creditsButton").objectReferenceValue =
            FindRequiredComponent<Button>(root, "CreditsButton");
        serializedView.FindProperty("victoryConditionButton").objectReferenceValue =
            FindRequiredComponent<Button>(root, "VictoryConditionButton");
        serializedView.FindProperty("victoryConditionIcon").objectReferenceValue =
            FindRequiredComponent<Image>(root, "VictoryConditionIcon");
        serializedView.FindProperty("standardVictoryConditionSprite").objectReferenceValue =
            LoadSprite("Application/MainMenu/UI/ui_mainmenu_hq_icon");
        serializedView.FindProperty("headquartersVictoryConditionSprite").objectReferenceValue =
            LoadSprite("Application/MainMenu/UI/ui_mainmenu_hqonly_icon");
        serializedView.FindProperty("victoryConditionText").objectReferenceValue =
            FindRequiredComponent<TMP_Text>(root, "VictoryConditionText");
        WriteToggleBindings(serializedView.FindProperty("galaxySizeBindings"), galaxySizes);
        WriteToggleBindings(serializedView.FindProperty("difficultyBindings"), difficulties);
        WriteFactionLaunchBindings(
            serializedView.FindProperty("factionLaunchBindings"),
            factionLaunches
        );
        WriteAudioCueBindings(serializedView.FindProperty("audioCueBindings"), audioCues);
        serializedView.FindProperty("optionsOverlay").objectReferenceValue =
            FindRequiredComponent<RectTransform>(root, "OptionsOverlayCanvas").gameObject;
        serializedView.FindProperty("optionsWindowLayer").objectReferenceValue =
            FindRequiredComponent<RectTransform>(root, "OptionsModalLayer");
        serializedView.FindProperty("optionsWindowManager").objectReferenceValue =
            FindRequiredComponent<UIWindowManager>(root, "OptionsOverlayCanvas");
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Collects the authored pointer audio-cue bindings in the same control order the prefab
    /// serializes: the difficulty toggles, galaxy toggles, then the command controls.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <returns>The ordered audio-cue bindings.</returns>
    private static List<(
        EventTrigger Trigger,
        EventTriggerType EventType,
        string ResourcePath
    )> CollectAudioCueBindings(GameObject root)
    {
        (string Name, EventTriggerType EventType, string ResourcePath)[] order =
        {
            ("EasyDifficultyToggle", EventTriggerType.PointerUp, _selectSfxPath),
            ("MediumDifficultyToggle", EventTriggerType.PointerUp, _selectSfxPath),
            ("HardDifficultyToggle", EventTriggerType.PointerUp, _selectSfxPath),
            ("SmallGalaxyToggle", EventTriggerType.PointerUp, _galaxySizeSelectSfxPath),
            ("MediumGalaxyToggle", EventTriggerType.PointerUp, _galaxySizeSelectSfxPath),
            ("LargeGalaxyToggle", EventTriggerType.PointerUp, _galaxySizeSelectSfxPath),
            ("ExitButton", EventTriggerType.PointerDown, _exitSelectSfxPath),
            ("CreditsButton", EventTriggerType.PointerUp, _selectSfxPath),
            ("LeftFactionLaunchButton", EventTriggerType.PointerUp, _factionSelectSfxPath),
            ("RightFactionLaunchButton", EventTriggerType.PointerUp, _factionSelectSfxPath),
            ("VictoryConditionButton", EventTriggerType.PointerUp, _selectSfxPath),
            ("VictoryConditionIcon", EventTriggerType.PointerUp, _selectSfxPath),
        };

        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> bindings =
            new List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)>();
        foreach ((string name, EventTriggerType eventType, string resourcePath) in order)
        {
            bindings.Add(
                (FindRequiredComponent<EventTrigger>(root, name), eventType, resourcePath)
            );
        }

        return bindings;
    }

    /// <summary>
    /// Authors the Services subtree: the bootstrap, audio manager, and cutscene manager.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    private static void BuildServices(GameObject root)
    {
        GameObject services = NewChild("Services", root.transform);

        GameObject bootstrap = NewChild("AppBootstrap", services.transform);
        bootstrap.AddComponent<AppBootstrap>();

        GameObject audioManagerObject = NewChild("AudioManager", services.transform);
        audioManagerObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        AudioSource musicSource = NewAudioSource(audioManagerObject);
        AudioSource sfxSource = NewAudioSource(audioManagerObject);
        AudioSource ambienceSource = NewAudioSource(audioManagerObject);
        AudioManager audioManager = audioManagerObject.AddComponent<AudioManager>();
        AssignReference(audioManager, "musicSource", musicSource);
        AssignReference(audioManager, "sfxSource", sfxSource);
        AssignReference(audioManager, "ambienceSource", ambienceSource);
    }

    /// <summary>
    /// Authors one always-play audio source with the serialized defaults the prefab uses.
    /// </summary>
    /// <param name="owner">The GameObject that receives the audio source.</param>
    /// <returns>The created audio source.</returns>
    private static AudioSource NewAudioSource(GameObject owner)
    {
        AudioSource source = owner.AddComponent<AudioSource>();
        source.playOnAwake = true;
        source.loop = false;
        source.volume = 1f;
        return source;
    }

    /// <summary>
    /// Authors the MainMenuController GameObject with its controller and view components.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <returns>The authored controller.</returns>
    private static MainMenuController BuildController(GameObject root)
    {
        GameObject controllerObject = NewChild("MainMenuController", root.transform);
        MainMenuController controller = controllerObject.AddComponent<MainMenuController>();
        MainMenuView view = controllerObject.AddComponent<MainMenuView>();
        AssignReference(controller, "view", view);
        AssignReference(
            controller,
            "_optionsMenuPrefab",
            AssetDatabase.LoadAssetAtPath<OptionsMenuView>(_optionsMenuPrefabPath)
        );
        AssignFloat(controller, "creditsMusicFadeDuration", 0.5f);
        return controller;
    }

    /// <summary>
    /// Authors the UI canvas subtree and every control below it.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    private static void BuildCanvas(GameObject root)
    {
        GameObject ui = NewChild("UI", root.transform);

        GameObject canvasObject = NewChild(
            "Canvas",
            ui.transform,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasRenderer)
        );
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.zero;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.zero;
        canvasRect.pivot = Vector2.zero;
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.sizeDelta = Vector2.zero;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        scaler.referencePixelsPerUnit = 100f;

        GameObject viewport = NewChild(
            "Viewport",
            canvasObject.transform,
            typeof(RectTransform),
            typeof(AspectRatioFitter),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        FillParent(viewportRect);
        AspectRatioFitter aspectRatio = viewport.GetComponent<AspectRatioFitter>();
        aspectRatio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectRatio.aspectRatio = 16f / 9f;

        Image cockpit = viewport.GetComponent<Image>();
        SetBoundSprite(cockpit, "Application/MainMenu/UI/ui_mainmenu_background");
        cockpit.raycastTarget = false;

        BuildControls(viewport.transform);
        BuildOptionsOverlay(ui.transform);
    }

    /// <summary>
    /// Authors the full-screen Options canvas, dimmer, modal layer, and window manager.
    /// </summary>
    /// <param name="parent">The Main Menu UI root.</param>
    private static void BuildOptionsOverlay(Transform parent)
    {
        GameObject overlay = NewChild(
            "OptionsOverlayCanvas",
            parent,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(UIWindowManager)
        );
        FillParent(overlay.GetComponent<RectTransform>());

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(853.33f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        GameObject dimmer = NewChild(
            "OptionsDimmer",
            overlay.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        FillParent(dimmer.GetComponent<RectTransform>());
        Image dimmerImage = dimmer.GetComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, 0.8f);
        dimmerImage.raycastTarget = true;

        GameObject modalLayer = NewChild(
            "OptionsModalLayer",
            overlay.transform,
            typeof(RectTransform)
        );
        FillParent(modalLayer.GetComponent<RectTransform>());
        overlay.SetActive(false);
    }

    /// <summary>
    /// Authors the MainMenuControls subtree with every control in authored sibling order.
    /// </summary>
    /// <param name="canvas">The canvas transform.</param>
    private static void BuildControls(Transform canvas)
    {
        GameObject controls = NewChild("MainMenuControls", canvas);
        FillParent(controls.GetComponent<RectTransform>());

        BuildDifficultyGroup(controls.transform);
        BuildGalaxySizeGroup(controls.transform);
        BuildExitButton(controls.transform);
        BuildCreditsButton(controls.transform);
        BuildLoadGameButton(controls.transform);
        BuildFactionLaunchButton(
            controls.transform,
            "LeftFactionLaunchButton",
            new Vector2(0.2785f, 0.22244444f),
            new Vector2(0.361f, 0.36911112f)
        );
        BuildFactionLaunchButton(
            controls.transform,
            "RightFactionLaunchButton",
            new Vector2(0.64024997f, 0.22244444f),
            new Vector2(0.72275f, 0.36911112f)
        );
        BuildVictoryConditionGroup(controls.transform);
        BuildExitConfirmationDialog(controls.transform);
    }

    /// <summary>
    /// Authors the shared modal exit confirmation.
    /// </summary>
    private static void BuildExitConfirmationDialog(Transform parent)
    {
        ConfirmationDialogView dialog = CommonUIPrefabBuilder.InstantiateConfirmationDialog(parent);
        dialog.gameObject.name = "ConfirmDialog";
        RectTransform rect = dialog.transform as RectTransform;
        FillParent(rect);
        RectTransform dialogSurface = dialog.transform.Find("DialogSurface") as RectTransform;
        if (dialogSurface == null)
            throw new MissingReferenceException("Confirmation dialog has no DialogSurface.");
        dialogSurface.localScale = Vector3.one * 3f;
    }

    /// <summary>
    /// Authors the difficulty toggle group and its three toggles.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    private static void BuildDifficultyGroup(Transform parent)
    {
        GameObject group = NewChild("DifficultyGroup", parent);
        SetAnchoredRect(
            group.GetComponent<RectTransform>(),
            new Vector2(0.16124997f, 0.8722376f),
            new Vector2(0.40061572f, 0.9800001f),
            new Vector2(-0.0000076293945f, 0f)
        );
        ToggleGroup toggleGroup = group.AddComponent<ToggleGroup>();
        toggleGroup.allowSwitchOff = false;

        BuildDifficultyToggle(
            group.transform,
            "EasyDifficultyToggle",
            new Vector2(0.06820129f, 0.10708914f),
            new Vector2(0.2561981f, 0.84946275f),
            true,
            "Application/MainMenu/UI/ui_mainmenu_toggle_easy_selected_icon",
            new Vector2(-0.276861f, -0.09802225f),
            new Vector2(1.2448332f, 1.0780834f),
            toggleGroup
        );
        BuildDifficultyToggle(
            group.transform,
            "MediumDifficultyToggle",
            new Vector2(0.38978013f, 0.10708914f),
            new Vector2(0.5777769f, 0.84946275f),
            false,
            "Application/MainMenu/UI/ui_mainmenu_toggle_medium_selected_icon",
            new Vector2(-0.28138146f, -0.31242868f),
            new Vector2(1.3778297f, 1.1104269f),
            toggleGroup
        );
        BuildDifficultyToggle(
            group.transform,
            "HardDifficultyToggle",
            new Vector2(0.7154325f, 0.10708914f),
            new Vector2(0.9034293f, 0.84946275f),
            false,
            "Application/MainMenu/UI/ui_mainmenu_toggle_hard_selected_icon",
            new Vector2(-0.3241689f, -0.25795534f),
            new Vector2(1.3976756f, 1.1844419f),
            toggleGroup
        );
    }

    /// <summary>
    /// Authors one difficulty toggle with its selection overlay. The spinning 3D icon is attached
    /// as a RawImage later, in the icon-rig install pass.
    /// </summary>
    /// <param name="parent">The difficulty group transform.</param>
    /// <param name="name">The toggle object name.</param>
    /// <param name="anchorMin">The toggle anchor minimum.</param>
    /// <param name="anchorMax">The toggle anchor maximum.</param>
    /// <param name="isOn">Whether the toggle starts selected.</param>
    /// <param name="overlayAddress">The selection overlay content address.</param>
    /// <param name="overlayAnchorMin">The selection overlay anchor minimum.</param>
    /// <param name="overlayAnchorMax">The selection overlay anchor maximum.</param>
    /// <param name="toggleGroup">The owning toggle group.</param>
    private static void BuildDifficultyToggle(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool isOn,
        string overlayAddress,
        Vector2 overlayAnchorMin,
        Vector2 overlayAnchorMax,
        ToggleGroup toggleGroup
    )
    {
        GameObject toggleObject = NewChild(
            name,
            parent,
            typeof(RectTransform),
            typeof(Toggle),
            typeof(EventTrigger)
        );
        toggleObject.layer = 5;
        SetAnchoredRect(toggleObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

        GameObject overlayObject = NewChild(
            "SelectedOverlay",
            toggleObject.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        SetAnchoredRect(
            overlayObject.GetComponent<RectTransform>(),
            overlayAnchorMin,
            overlayAnchorMax
        );
        Image overlay = overlayObject.GetComponent<Image>();
        SetBoundSprite(overlay, overlayAddress);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.graphic = overlay;
        toggle.group = toggleGroup;
        toggle.isOn = isOn;

        AddPointerUpTrigger(toggleObject.GetComponent<EventTrigger>());
    }

    /// <summary>
    /// Authors the galaxy-size toggle group and its three toggles.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    private static void BuildGalaxySizeGroup(Transform parent)
    {
        GameObject group = NewChild("GalaxySizeGroup", parent);
        SetAnchoredRect(
            group.GetComponent<RectTransform>(),
            new Vector2(0.44762507f, 0.3313065f),
            new Vector2(0.5857842f, 0.40584758f),
            new Vector2(0f, 0.0000038146973f)
        );
        ToggleGroup toggleGroup = group.AddComponent<ToggleGroup>();
        toggleGroup.allowSwitchOff = false;

        BuildGalaxySizeToggle(
            group.transform,
            "SmallGalaxyToggle",
            new Vector2(0.043455794f, 0.10742572f),
            new Vector2(0.29431152f, 0.889017f),
            new Vector2(-0.0000019073486f, 0f),
            true,
            "Application/MainMenu/UI/ui_mainmenu_toggle_small_map_selected_icon",
            new Vector2(-0.21818379f, -0.23880222f),
            new Vector2(1.1548722f, 1.171793f),
            toggleGroup
        );
        BuildGalaxySizeToggle(
            group.transform,
            "MediumGalaxyToggle",
            new Vector2(0.3790894f, 0.11568364f),
            new Vector2(0.62713313f, 0.86021256f),
            Vector2.zero,
            false,
            "Application/MainMenu/UI/ui_mainmenu_toggle_medium_map_selected_icon",
            new Vector2(-0.24899858f, -0.3004693f),
            new Vector2(1.1906377f, 1.2052346f),
            toggleGroup
        );
        BuildGalaxySizeToggle(
            group.transform,
            "LargeGalaxyToggle",
            new Vector2(0.7119703f, 0.1378822f),
            new Vector2(0.96001405f, 0.8824111f),
            Vector2.zero,
            false,
            "Application/MainMenu/UI/ui_mainmenu_toggle_large_map_selected_icon",
            new Vector2(-0.2263274f, -0.3004693f),
            new Vector2(1.230318f, 1.2052346f),
            toggleGroup
        );
    }

    /// <summary>
    /// Authors one galaxy-size toggle with its transparent hit graphic and selection overlay.
    /// </summary>
    /// <param name="parent">The galaxy-size group transform.</param>
    /// <param name="name">The toggle object name.</param>
    /// <param name="anchorMin">The toggle anchor minimum.</param>
    /// <param name="anchorMax">The toggle anchor maximum.</param>
    /// <param name="sizeDelta">The toggle size delta.</param>
    /// <param name="isOn">Whether the toggle starts selected.</param>
    /// <param name="overlayAddress">The selection overlay content address.</param>
    /// <param name="overlayAnchorMin">The selection overlay anchor minimum.</param>
    /// <param name="overlayAnchorMax">The selection overlay anchor maximum.</param>
    /// <param name="toggleGroup">The owning toggle group.</param>
    private static void BuildGalaxySizeToggle(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        bool isOn,
        string overlayAddress,
        Vector2 overlayAnchorMin,
        Vector2 overlayAnchorMax,
        ToggleGroup toggleGroup
    )
    {
        GameObject toggleObject = NewChild(
            name,
            parent,
            typeof(RectTransform),
            typeof(Toggle),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(EventTrigger)
        );
        SetAnchoredRect(
            toggleObject.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax,
            sizeDelta
        );

        Image hitGraphic = toggleObject.GetComponent<Image>();
        hitGraphic.color = new Color(1f, 1f, 1f, 0f);

        GameObject overlayObject = NewChild(
            "SelectedOverlay",
            toggleObject.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        SetAnchoredRect(
            overlayObject.GetComponent<RectTransform>(),
            overlayAnchorMin,
            overlayAnchorMax
        );
        Image overlay = overlayObject.GetComponent<Image>();
        SetBoundSprite(overlay, overlayAddress);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = hitGraphic;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        toggle.graphic = overlay;
        toggle.group = toggleGroup;
        toggle.isOn = isOn;

        AddPointerUpTrigger(toggleObject.GetComponent<EventTrigger>());
    }

    /// <summary>
    /// Authors the exit button, its animator, and the hidden pressed-state overlay image.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    private static void BuildExitButton(Transform parent)
    {
        GameObject buttonObject = NewChild(
            "ExitButton",
            parent,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Button),
            typeof(Image),
            typeof(EventTrigger)
        );
        buttonObject.layer = 5;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetAnchoredRect(
            rect,
            new Vector2(0.7641539f, 0.011382718f),
            new Vector2(0.84987485f, 0.16755562f)
        );
        rect.pivot = new Vector2(1f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = LoadSprite("Application/MainMenu/UI/ui_mainmenu_exit_01");
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = image;

        // The exit-button controller is assigned to the animator by RebuildViewBindings.

        GameObject pressed = BuildPressedImage(
            buttonObject.transform,
            "Application/MainMenu/UI/ui_mainmenu_exit_pressed"
        );
        pressed.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);

        AddPointerTrigger(
            buttonObject.GetComponent<EventTrigger>(),
            EventTriggerType.PointerDown,
            _exitSelectSfxPath
        );
    }

    /// <summary>
    /// Authors the credits button with its sprite-swap pressed state.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    private static void BuildCreditsButton(Transform parent)
    {
        GameObject buttonObject = NewChild(
            "CreditsButton",
            parent,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(EventTrigger)
        );
        buttonObject.layer = 5;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetAnchoredRect(
            rect,
            new Vector2(0.6708864f, 0.44184244f),
            new Vector2(0.71175f, 0.50244445f),
            new Vector2(0.000015258789f, 0f)
        );
        rect.pivot = new Vector2(1f, 1f);

        Image image = buttonObject.GetComponent<Image>();
        SetBoundSprite(image, "Application/MainMenu/UI/ui_mainmenu_credits_icon");

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        button.targetGraphic = image;
        SpriteState spriteState = button.spriteState;
        spriteState.pressedSprite = LoadSprite(
            "Application/MainMenu/UI/ui_mainmenu_credits_icon_pressed"
        );
        button.spriteState = spriteState;

        AddPointerUpTrigger(buttonObject.GetComponent<EventTrigger>());
    }

    /// <summary>
    /// Authors the load-game button, its animator, and the hidden pressed-state overlay image.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    private static void BuildLoadGameButton(Transform parent)
    {
        GameObject buttonObject = NewChild(
            "LoadGameButton",
            parent,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(EventTrigger)
        );
        buttonObject.layer = 5;
        SetAnchoredRect(
            buttonObject.GetComponent<RectTransform>(),
            new Vector2(0.6101744f, 0.45165005f),
            new Vector2(0.65932107f, 0.53413427f),
            new Vector2(0f, 0.000015258789f)
        );

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = LoadSprite("Application/MainMenu/UI/ui_mainmenu_load_01");

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = image;

        BuildPressedImage(
            buttonObject.transform,
            "Application/MainMenu/UI/ui_mainmenu_load_pressed"
        );

        AddPointerUpTrigger(buttonObject.GetComponent<EventTrigger>());
    }

    /// <summary>
    /// Authors one hidden pressed-state overlay image that fills its button.
    /// </summary>
    /// <param name="parent">The button transform.</param>
    /// <param name="spriteAddress">The pressed sprite content address.</param>
    /// <returns>The created pressed-state overlay GameObject.</returns>
    private static GameObject BuildPressedImage(Transform parent, string spriteAddress)
    {
        GameObject pressed = NewChild(
            "PressedImage",
            parent,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        pressed.layer = 5;
        pressed.SetActive(false);
        FillParent(pressed.GetComponent<RectTransform>());
        SetBoundSprite(pressed.GetComponent<Image>(), spriteAddress);
        return pressed;
    }

    /// <summary>
    /// Assigns an image's authored sprite and attaches a runtime binding that restores it from
    /// installation content.
    /// </summary>
    /// <param name="image">The image whose sprite is authored and bound.</param>
    /// <param name="address">The content address resolved at author time and runtime.</param>
    private static void SetBoundSprite(Image image, string address)
    {
        image.sprite = LoadSprite(address);
        ContentSpriteBinding binding = image.gameObject.AddComponent<ContentSpriteBinding>();
        binding.SetAddress(address);
    }

    /// <summary>
    /// Assigns a raw image its content texture for editor display and attaches the runtime binding
    /// that restores it from installation content after the player build strips the reference.
    /// </summary>
    /// <param name="image">The raw image to bind.</param>
    /// <param name="address">The stable content address of the texture.</param>
    private static void SetBoundTexture(RawImage image, string address)
    {
        image.texture = LoadTexture(address);
        ContentTextureBinding binding = image.gameObject.AddComponent<ContentTextureBinding>();
        binding.SetAddress(address);
    }

    /// <summary>
    /// Moves a sprite content binding from one image to another when a control is redrawn, so the
    /// runtime restore targets the image that actually renders.
    /// </summary>
    /// <param name="source">The image whose binding is moved.</param>
    /// <param name="destination">The image that receives the binding.</param>
    private static void MoveSpriteBinding(Image source, Image destination)
    {
        ContentSpriteBinding sourceBinding = source.GetComponent<ContentSpriteBinding>();
        if (sourceBinding == null)
            return;

        ContentSpriteBinding destinationBinding =
            destination.gameObject.AddComponent<ContentSpriteBinding>();
        destinationBinding.SetAddress(sourceBinding.Address, sourceBinding.Border);
        Object.DestroyImmediate(sourceBinding);
    }

    /// <summary>
    /// Converts a model asset path to its stable runtime content address under the models directory.
    /// </summary>
    /// <param name="modelPath">The authored model asset path.</param>
    /// <returns>The extension-free content address for the model.</returns>
    private static string ToModelAddress(string modelPath)
    {
        int separatorIndex = modelPath.LastIndexOf('/');
        int extensionIndex = modelPath.LastIndexOf('.');
        string modelName = modelPath.Substring(
            separatorIndex + 1,
            extensionIndex - separatorIndex - 1
        );
        return "Application/MainMenu/Models/" + modelName;
    }

    /// <summary>
    /// Authors one faction launch button, its animator, and its 3D-icon RawImage placeholder.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    /// <param name="name">The button object name.</param>
    /// <param name="anchorMin">The button anchor minimum.</param>
    /// <param name="anchorMax">The button anchor maximum.</param>
    private static void BuildFactionLaunchButton(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax
    )
    {
        GameObject buttonObject = NewChild(
            name,
            parent,
            typeof(RectTransform),
            typeof(Button),
            typeof(EventTrigger)
        );
        buttonObject.layer = 5;
        SetAnchoredRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        AddPointerUpTrigger(buttonObject.GetComponent<EventTrigger>());
    }

    /// <summary>
    /// Authors the victory-condition label, control, icon, and toggle button.
    /// </summary>
    /// <param name="parent">The MainMenuControls transform.</param>
    private static void BuildVictoryConditionGroup(Transform parent)
    {
        GameObject group = NewChild("VictoryConditionGroup", parent);
        SetCenteredRect(
            group.GetComponent<RectTransform>(),
            new Vector2(3.4168f, -118.24f),
            new Vector2(118.9644f, 62.717f)
        );

        GameObject textObject = NewChild(
            "VictoryConditionText",
            group.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        SetCenteredRect(
            textObject.GetComponent<RectTransform>(),
            new Vector2(-0.000037074f, -327.8f),
            new Vector2(356.4606f, 35.0948f)
        );
        TMP_Text victoryText = textObject.GetComponent<TextMeshProUGUI>();
        victoryText.text = "Headquarters Victory";
        victoryText.font = LoadRequiredAsset<TMP_FontAsset>(_victoryTextFontPath);
        victoryText.color = new Color(0f, 0.99607843f, 0f, 1f);
        victoryText.enableAutoSizing = true;
        victoryText.fontSizeMin = 8f;
        victoryText.fontSizeMax = 32.4f;
        victoryText.fontStyle = FontStyles.Bold;
        victoryText.alignment = TextAlignmentOptions.Center;

        GameObject control = NewChild("VictoryConditionControl", group.transform);
        SetCenteredRect(
            control.GetComponent<RectTransform>(),
            new Vector2(-0.000034094f, -222f),
            new Vector2(126.1f, 90.2372f)
        );

        GameObject buttonObject = NewChild(
            "VictoryConditionButton",
            control.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(EventTrigger)
        );
        FillParent(buttonObject.GetComponent<RectTransform>());
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0f);
        buttonImage.preserveAspect = true;
        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = buttonImage;
        AddPointerUpTrigger(buttonObject.GetComponent<EventTrigger>());

        GameObject iconObject = NewChild(
            "VictoryConditionIcon",
            control.transform,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(EventTrigger)
        );
        SetCenteredRect(
            iconObject.GetComponent<RectTransform>(),
            new Vector2(1.598999f, 1.1442986f),
            new Vector2(3.1981f, 2.2886f)
        );
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = LoadSprite("Application/MainMenu/UI/ui_mainmenu_hqonly_icon");
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        Button iconButton = iconObject.GetComponent<Button>();
        iconButton.transition = Selectable.Transition.ColorTint;
        iconButton.targetGraphic = icon;
        AddPointerUpTrigger(iconObject.GetComponent<EventTrigger>());
    }

    /// <summary>
    /// Adds one authored pointer-up event-trigger entry that plays a select cue.
    /// </summary>
    /// <param name="trigger">The event trigger to populate.</param>
    private static void AddPointerUpTrigger(EventTrigger trigger)
    {
        AddPointerUpTrigger(trigger, _selectSfxPath);
    }

    /// <summary>
    /// Adds one authored pointer-up event-trigger entry with an empty persistent callback.
    /// </summary>
    /// <param name="trigger">The event trigger to populate.</param>
    /// <param name="resourcePath">The cue resource path recorded on the view binding.</param>
    private static void AddPointerUpTrigger(EventTrigger trigger, string resourcePath)
    {
        AddPointerTrigger(trigger, EventTriggerType.PointerUp, resourcePath);
    }

    /// <summary>
    /// Adds one authored pointer event-trigger entry with an empty persistent callback.
    /// </summary>
    /// <param name="trigger">The event trigger to populate.</param>
    /// <param name="eventType">The pointer event that emits the cue.</param>
    /// <param name="resourcePath">The cue resource path recorded on the view binding.</param>
    private static void AddPointerTrigger(
        EventTrigger trigger,
        EventTriggerType eventType,
        string resourcePath
    )
    {
        _ = resourcePath;
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();
        trigger.triggers.Add(
            new EventTrigger.Entry
            {
                eventID = eventType,
                callback = new EventTrigger.TriggerEvent(),
            }
        );
    }

    /// <summary>
    /// Creates a child GameObject parented under a transform.
    /// </summary>
    /// <param name="name">The GameObject name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="components">Optional components to author with the GameObject.</param>
    /// <returns>The created child GameObject.</returns>
    private static GameObject NewChild(string name, Transform parent, params Type[] components)
    {
        // Any child of a RectTransform is a UI object and needs its own RectTransform. A container
        // with no graphic would otherwise get a plain Transform and break layout (FillParent etc.).
        if (
            parent is RectTransform
            && (components == null || Array.IndexOf(components, typeof(RectTransform)) < 0)
        )
        {
            Type[] uiComponents = new Type[(components?.Length ?? 0) + 1];
            uiComponents[0] = typeof(RectTransform);
            if (components != null)
                Array.Copy(components, 0, uiComponents, 1, components.Length);
            components = uiComponents;
        }

        GameObject child =
            components == null || components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    /// <summary>
    /// Applies anchors and a zero size delta to a RectTransform.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="anchorMin">The anchor minimum.</param>
    /// <param name="anchorMax">The anchor maximum.</param>
    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        SetAnchoredRect(rect, anchorMin, anchorMax, Vector2.zero);
    }

    /// <summary>
    /// Applies anchors and an explicit size delta to a RectTransform.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="anchorMin">The anchor minimum.</param>
    /// <param name="anchorMax">The anchor maximum.</param>
    /// <param name="sizeDelta">The size delta.</param>
    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta
    )
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;
    }

    /// <summary>
    /// Applies a centered anchor, position, and size delta to a RectTransform.
    /// </summary>
    /// <param name="rect">The target RectTransform.</param>
    /// <param name="anchoredPosition">The anchored position.</param>
    /// <param name="sizeDelta">The size delta.</param>
    private static void SetCenteredRect(
        RectTransform rect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    /// <summary>
    /// Loads one sprite from external application content.
    /// </summary>
    /// <param name="address">The external content address.</param>
    /// <returns>The sprite at the address.</returns>
    private static Sprite LoadSprite(string address)
    {
        return ContentPackEditor.Assets.GetSprite(address);
    }

    /// <summary>
    /// Loads a content texture for editor authoring by its stable content address.
    /// </summary>
    /// <param name="address">The stable content address of the texture.</param>
    /// <returns>The resolved texture from editor content.</returns>
    private static Texture2D LoadTexture(string address)
    {
        return ContentPackEditor.Assets.GetTexture(address);
    }

    /// <summary>
    /// Assigns one serialized float value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The float value.</param>
    private static void AssignFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Rebuilds the view bindings on one loaded main-menu prefab hierarchy.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    private static void RebuildViewBindings(GameObject root)
    {
        MainMenuController controller = FindRequiredComponent<MainMenuController>(root);
        MainMenuView view = controller.GetComponent<MainMenuView>();
        if (view == null)
            view = controller.gameObject.AddComponent<MainMenuView>();

        List<(Toggle Toggle, int Value)> galaxySizes = DiscoverToggleBindings<GameSize>(
            root,
            "GalaxySizeGroup"
        );
        List<(Toggle Toggle, int Value)> difficulties = DiscoverToggleBindings<GameDifficulty>(
            root,
            "DifficultyGroup"
        );

        List<(Button Button, string FactionId)> factionLaunches = ReadFactionLaunchBindings(view);
        if (factionLaunches.Count == 0)
            factionLaunches = DiscoverFactionLaunchBindings(root, controller);

        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> audioCues =
            ReadAudioCueBindings(view);
        if (audioCues.Count == 0)
            audioCues = DiscoverAudioCueBindings(root, controller);

        Button loadGameButton =
            ReadReference<Button>(view, "loadGameButton")
            ?? FindButtonByControllerMethod(root, controller, "OpenLoadGameMenu");
        Button exitButton =
            ReadReference<Button>(view, "exitButton")
            ?? FindRequiredComponent<Button>(root, "ExitButton");
        Button creditsButton =
            ReadReference<Button>(view, "creditsButton")
            ?? FindButtonByControllerMethod(root, controller, "ShowCredits");
        Button victoryConditionButton =
            ReadReference<Button>(view, "victoryConditionButton")
            ?? FindButtonByControllerMethod(root, controller, "ToggleVictoryCondition");
        Image victoryConditionIcon =
            ReadReference<Image>(view, "victoryConditionIcon")
            ?? FindRequiredComponent<Image>(root, "VictoryConditionIcon");
        TMP_Text victoryConditionText =
            ReadReference<TMP_Text>(view, "victoryConditionText")
            ?? FindRequiredComponent<TMP_Text>(root, "VictoryConditionText");
        Sprite standardVictoryConditionSprite =
            ReadReference<Sprite>(view, "standardVictoryConditionSprite")
            ?? ContentPackEditor.Assets.GetSprite(_standardVictorySpriteAddress);
        Sprite headquartersVictoryConditionSprite =
            ReadReference<Sprite>(view, "headquartersVictoryConditionSprite")
            ?? victoryConditionIcon.sprite;
        ConfigureView(
            view,
            loadGameButton,
            exitButton,
            creditsButton,
            victoryConditionButton,
            galaxySizes,
            difficulties,
            factionLaunches,
            victoryConditionIcon,
            standardVictoryConditionSprite,
            headquartersVictoryConditionSprite,
            victoryConditionText,
            audioCues
        );
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("exitPressedImage").objectReferenceValue = exitButton
            .transform.Find("PressedImage")
            .gameObject;
        serializedView.FindProperty("exitConfirmationDialog").objectReferenceValue =
            FindRequiredComponent<ConfirmationDialogView>(root, "ConfirmDialog");
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        AssignReference(controller, "view", view);
        RemoveControllerPersistentCalls(root, controller);
        RebuildEventTriggers(audioCues, exitButton.GetComponent<EventTrigger>());
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(view);
    }

    /// <summary>
    /// Reads an object reference from a serialized view field.
    /// </summary>
    /// <typeparam name="T">The referenced Unity object type.</typeparam>
    /// <param name="view">The main-menu view.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <returns>The referenced object, or <see langword="null"/>.</returns>
    private static T ReadReference<T>(MainMenuView view, string propertyName)
        where T : Object
    {
        SerializedProperty property = new SerializedObject(view).FindProperty(propertyName);
        return property?.objectReferenceValue as T;
    }

    /// <summary>
    /// Reads existing serialized faction-launch bindings from the view.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <returns>The configured launch buttons and faction identifiers.</returns>
    private static List<(Button Button, string FactionId)> ReadFactionLaunchBindings(
        MainMenuView view
    )
    {
        SerializedProperty bindings = new SerializedObject(view).FindProperty(
            "factionLaunchBindings"
        );
        List<(Button Button, string FactionId)> result =
            new List<(Button Button, string FactionId)>();
        if (bindings == null)
            return result;

        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            Button button = binding.FindPropertyRelative("button").objectReferenceValue as Button;
            string factionId = binding.FindPropertyRelative("factionId").stringValue;
            if (button == null || string.IsNullOrEmpty(factionId))
            {
                throw new InvalidOperationException($"Faction launch binding {i} is incomplete.");
            }

            result.Add((button, factionId));
        }

        return result;
    }

    /// <summary>
    /// Reads existing serialized audio-cue bindings from the view.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <returns>The configured event-trigger audio cues.</returns>
    private static List<(
        EventTrigger Trigger,
        EventTriggerType EventType,
        string ResourcePath
    )> ReadAudioCueBindings(MainMenuView view)
    {
        SerializedProperty bindings = new SerializedObject(view).FindProperty("audioCueBindings");
        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> result =
            new List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)>();
        if (bindings == null)
            return result;

        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            EventTrigger trigger =
                binding.FindPropertyRelative("trigger").objectReferenceValue as EventTrigger;
            string resourcePath = binding.FindPropertyRelative("resourcePath").stringValue;
            if (trigger == null || string.IsNullOrEmpty(resourcePath))
                throw new InvalidOperationException($"Audio cue binding {i} is incomplete.");

            result.Add(
                (
                    trigger,
                    (EventTriggerType)binding.FindPropertyRelative("eventType").intValue,
                    resourcePath
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Discovers enum-backed toggle values from one authored toggle group.
    /// </summary>
    /// <typeparam name="T">The enum represented by the toggle group.</typeparam>
    /// <param name="root">The prefab root.</param>
    /// <param name="groupName">The authored toggle-group object name.</param>
    /// <returns>The discovered toggle bindings.</returns>
    private static List<(Toggle Toggle, int Value)> DiscoverToggleBindings<T>(
        GameObject root,
        string groupName
    )
        where T : struct, Enum
    {
        ToggleGroup group = FindRequiredComponent<ToggleGroup>(root, groupName);
        Toggle[] toggles = group.GetComponentsInChildren<Toggle>(true);
        T[] values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        if (toggles.Length != values.Length)
        {
            throw new InvalidOperationException(
                $"The authored {groupName} toggles do not match the supported {typeof(T).Name} values."
            );
        }

        List<(Toggle Toggle, int Value)> bindings = new List<(Toggle Toggle, int Value)>();
        for (int i = 0; i < toggles.Length; i++)
            bindings.Add((toggles[i], Convert.ToInt32(values[i])));
        return bindings;
    }

    /// <summary>
    /// Discovers faction launch identifiers from the currently authored UnityEvents.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    /// <returns>The discovered faction launch bindings.</returns>
    private static List<(Button Button, string FactionId)> DiscoverFactionLaunchBindings(
        GameObject root,
        MainMenuController controller
    )
    {
        List<(Button Button, string FactionId)> bindings =
            new List<(Button Button, string FactionId)>();
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (
                TryReadPersistentStringArgument(
                    button,
                    "m_OnClick",
                    button.onClick,
                    controller,
                    "SelectFaction",
                    out string factionId
                )
            )
            {
                bindings.Add((button, factionId));
            }
        }

        if (bindings.Count == 0)
            throw new InvalidOperationException("No authored faction launch bindings were found.");
        return bindings;
    }

    /// <summary>
    /// Discovers UI audio cues from the currently authored event-trigger callbacks.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    /// <returns>The discovered audio cue bindings.</returns>
    private static List<(
        EventTrigger Trigger,
        EventTriggerType EventType,
        string ResourcePath
    )> DiscoverAudioCueBindings(GameObject root, MainMenuController controller)
    {
        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> bindings =
            new List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)>();
        foreach (EventTrigger trigger in root.GetComponentsInChildren<EventTrigger>(true))
        {
            SerializedObject serializedTrigger = new SerializedObject(trigger);
            SerializedProperty delegates = serializedTrigger.FindProperty("m_Delegates");
            for (int entryIndex = 0; entryIndex < trigger.triggers.Count; entryIndex++)
            {
                EventTrigger.Entry entry = trigger.triggers[entryIndex];
                SerializedProperty serializedEntry = delegates.GetArrayElementAtIndex(entryIndex);
                SerializedProperty callback = serializedEntry.FindPropertyRelative("callback");
                SerializedProperty calls = GetPersistentCalls(callback);
                for (
                    int callIndex = 0;
                    callIndex < entry.callback.GetPersistentEventCount();
                    callIndex++
                )
                {
                    if (
                        entry.callback.GetPersistentTarget(callIndex) != controller
                        || entry.callback.GetPersistentMethodName(callIndex) != "PlaySfx"
                    )
                    {
                        continue;
                    }

                    SerializedProperty call = calls.GetArrayElementAtIndex(callIndex);
                    SerializedProperty arguments = call.FindPropertyRelative("m_Arguments");
                    string resourcePath = arguments
                        .FindPropertyRelative("m_StringArgument")
                        .stringValue;
                    bindings.Add((trigger, entry.eventID, resourcePath));
                }
            }
        }

        if (bindings.Count == 0)
            throw new InvalidOperationException("No authored main-menu audio cues were found.");
        return bindings;
    }

    /// <summary>
    /// Finds a command button by its persistent controller method.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    /// <param name="methodName">The persistent controller method name.</param>
    /// <returns>The unique matching button.</returns>
    private static Button FindButtonByControllerMethod(
        GameObject root,
        MainMenuController controller,
        string methodName
    )
    {
        Button[] matches = root.GetComponentsInChildren<Button>(true)
            .Where(button => HasPersistentCall(button.onClick, controller, methodName))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one button bound to {methodName}, found {matches.Length}."
            );
        }

        return matches[0];
    }

    /// <summary>
    /// Determines whether a UnityEvent contains one controller method binding.
    /// </summary>
    /// <param name="unityEvent">The UnityEvent to inspect.</param>
    /// <param name="controller">The expected target controller.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <returns><see langword="true"/> when a matching call exists.</returns>
    private static bool HasPersistentCall(
        UnityEventBase unityEvent,
        MainMenuController controller,
        string methodName
    )
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (
                unityEvent.GetPersistentTarget(i) == controller
                && unityEvent.GetPersistentMethodName(i) == methodName
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a string argument from one matching persistent UnityEvent call.
    /// </summary>
    /// <param name="owner">The serialized UnityEvent owner.</param>
    /// <param name="eventPropertyName">The serialized UnityEvent property name.</param>
    /// <param name="unityEvent">The UnityEvent to inspect.</param>
    /// <param name="controller">The expected target controller.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <param name="value">Receives the serialized string argument.</param>
    /// <returns><see langword="true"/> when a matching call exists.</returns>
    private static bool TryReadPersistentStringArgument(
        Object owner,
        string eventPropertyName,
        UnityEventBase unityEvent,
        MainMenuController controller,
        string methodName,
        out string value
    )
    {
        int callIndex = FindPersistentCallIndex(unityEvent, controller, methodName);
        if (callIndex < 0)
        {
            value = null;
            return false;
        }

        SerializedProperty call = GetPersistentCalls(owner, eventPropertyName)
            .GetArrayElementAtIndex(callIndex);
        value = call.FindPropertyRelative("m_Arguments")
            .FindPropertyRelative("m_StringArgument")
            .stringValue;
        return true;
    }

    /// <summary>
    /// Finds one persistent controller call in a UnityEvent.
    /// </summary>
    /// <param name="unityEvent">The UnityEvent to inspect.</param>
    /// <param name="controller">The expected target controller.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <returns>The matching call index, or negative one.</returns>
    private static int FindPersistentCallIndex(
        UnityEventBase unityEvent,
        MainMenuController controller,
        string methodName
    )
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (
                unityEvent.GetPersistentTarget(i) == controller
                && unityEvent.GetPersistentMethodName(i) == methodName
            )
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the serialized persistent-call array for a UnityEvent field.
    /// </summary>
    /// <param name="owner">The serialized UnityEvent owner.</param>
    /// <param name="eventPropertyName">The serialized UnityEvent property name.</param>
    /// <returns>The serialized persistent-call array.</returns>
    private static SerializedProperty GetPersistentCalls(Object owner, string eventPropertyName)
    {
        SerializedProperty unityEvent = new SerializedObject(owner).FindProperty(eventPropertyName);
        return GetPersistentCalls(unityEvent);
    }

    /// <summary>
    /// Gets the serialized persistent-call array below a UnityEvent property.
    /// </summary>
    /// <param name="unityEvent">The serialized UnityEvent property.</param>
    /// <returns>The serialized persistent-call array.</returns>
    private static SerializedProperty GetPersistentCalls(SerializedProperty unityEvent)
    {
        return unityEvent.FindPropertyRelative("m_PersistentCalls").FindPropertyRelative("m_Calls");
    }

    /// <summary>
    /// Writes all serialized MainMenuView references and binding collections.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <param name="loadGameButton">The load-game button.</param>
    /// <param name="exitButton">The exit button.</param>
    /// <param name="creditsButton">The credits button.</param>
    /// <param name="victoryConditionButton">The victory-condition button.</param>
    /// <param name="galaxySizes">The galaxy-size bindings.</param>
    /// <param name="difficulties">The difficulty bindings.</param>
    /// <param name="factionLaunches">The faction launch bindings.</param>
    /// <param name="victoryConditionIcon">The victory-condition icon.</param>
    /// <param name="standardVictoryConditionSprite">The standard victory sprite.</param>
    /// <param name="headquartersVictoryConditionSprite">The headquarters victory sprite.</param>
    /// <param name="victoryConditionText">The victory-condition label.</param>
    /// <param name="audioCues">The audio-cue bindings.</param>
    private static void ConfigureView(
        MainMenuView view,
        Button loadGameButton,
        Button exitButton,
        Button creditsButton,
        Button victoryConditionButton,
        IReadOnlyList<(Toggle Toggle, int Value)> galaxySizes,
        IReadOnlyList<(Toggle Toggle, int Value)> difficulties,
        IReadOnlyList<(Button Button, string FactionId)> factionLaunches,
        Image victoryConditionIcon,
        Sprite standardVictoryConditionSprite,
        Sprite headquartersVictoryConditionSprite,
        TMP_Text victoryConditionText,
        IReadOnlyList<(
            EventTrigger Trigger,
            EventTriggerType EventType,
            string ResourcePath
        )> audioCues
    )
    {
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("loadGameButton").objectReferenceValue = loadGameButton;
        serializedView.FindProperty("exitButton").objectReferenceValue = exitButton;
        serializedView.FindProperty("creditsButton").objectReferenceValue = creditsButton;
        serializedView.FindProperty("victoryConditionButton").objectReferenceValue =
            victoryConditionButton;
        serializedView.FindProperty("victoryConditionIcon").objectReferenceValue =
            victoryConditionIcon;
        serializedView.FindProperty("standardVictoryConditionSprite").objectReferenceValue =
            standardVictoryConditionSprite;
        serializedView.FindProperty("headquartersVictoryConditionSprite").objectReferenceValue =
            headquartersVictoryConditionSprite;
        serializedView.FindProperty("victoryConditionText").objectReferenceValue =
            victoryConditionText;

        WriteToggleBindings(serializedView.FindProperty("galaxySizeBindings"), galaxySizes);
        WriteToggleBindings(serializedView.FindProperty("difficultyBindings"), difficulties);
        WriteFactionLaunchBindings(
            serializedView.FindProperty("factionLaunchBindings"),
            factionLaunches
        );
        WriteAudioCueBindings(serializedView.FindProperty("audioCueBindings"), audioCues);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Writes one serialized toggle-binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The toggle bindings.</param>
    private static void WriteToggleBindings(
        SerializedProperty property,
        IReadOnlyList<(Toggle Toggle, int Value)> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("toggle").objectReferenceValue = bindings[i].Toggle;
            binding.FindPropertyRelative("value").intValue = bindings[i].Value;
        }
    }

    /// <summary>
    /// Writes the serialized faction-launch binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The faction launch bindings.</param>
    private static void WriteFactionLaunchBindings(
        SerializedProperty property,
        IReadOnlyList<(Button Button, string FactionId)> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("button").objectReferenceValue = bindings[i].Button;
            binding.FindPropertyRelative("factionId").stringValue = bindings[i].FactionId;
        }
    }

    /// <summary>
    /// Writes the serialized audio-cue binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The audio-cue bindings.</param>
    private static void WriteAudioCueBindings(
        SerializedProperty property,
        IReadOnlyList<(
            EventTrigger Trigger,
            EventTriggerType EventType,
            string ResourcePath
        )> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("trigger").objectReferenceValue = bindings[i].Trigger;
            binding.FindPropertyRelative("eventType").intValue = (int)bindings[i].EventType;
            binding.FindPropertyRelative("resourcePath").stringValue = bindings[i].ResourcePath;
        }
    }

    /// <summary>
    /// Assigns one serialized Unity-object reference.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The referenced Unity object.</param>
    private static void AssignReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Removes persistent calls that target MainMenuController from authored controls.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    private static void RemoveControllerPersistentCalls(
        GameObject root,
        MainMenuController controller
    )
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            RemovePersistentCalls(button.onClick, controller);

        foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            RemovePersistentCalls(toggle.onValueChanged, controller);
    }

    /// <summary>
    /// Removes all calls to one target from a persistent UnityEvent.
    /// </summary>
    /// <param name="unityEvent">The UnityEvent to update.</param>
    /// <param name="target">The target whose calls should be removed.</param>
    private static void RemovePersistentCalls(UnityEventBase unityEvent, Object target)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (unityEvent.GetPersistentTarget(i) == target)
                UnityEventTools.RemovePersistentListener(unityEvent, i);
        }
    }

    /// <summary>
    /// Rebuilds event-trigger entries required by audio cues.
    /// </summary>
    /// <param name="audioCues">The audio-cue bindings.</param>
    /// <param name="exitTrigger">The exit lever trigger that also owns pressed-state events.</param>
    private static void RebuildEventTriggers(
        IReadOnlyList<(
            EventTrigger Trigger,
            EventTriggerType EventType,
            string ResourcePath
        )> audioCues,
        EventTrigger exitTrigger
    )
    {
        Dictionary<EventTrigger, HashSet<EventTriggerType>> eventTypes =
            new Dictionary<EventTrigger, HashSet<EventTriggerType>>();
        foreach ((EventTrigger trigger, EventTriggerType eventType, _) in audioCues)
            AddEventType(eventTypes, trigger, eventType);
        AddEventType(eventTypes, exitTrigger, EventTriggerType.PointerDown);
        AddEventType(eventTypes, exitTrigger, EventTriggerType.PointerUp);
        AddEventType(eventTypes, exitTrigger, EventTriggerType.PointerExit);

        EventTriggerType[] authoredOrder =
        {
            EventTriggerType.PointerDown,
            EventTriggerType.PointerUp,
            EventTriggerType.PointerExit,
        };
        foreach (KeyValuePair<EventTrigger, HashSet<EventTriggerType>> pair in eventTypes)
        {
            List<EventTriggerType> orderedTypes = authoredOrder
                .Where(pair.Value.Contains)
                .Concat(pair.Value.Except(authoredOrder).OrderBy(value => value))
                .ToList();
            pair.Key.triggers = orderedTypes.ConvertAll(eventType => new EventTrigger.Entry
            {
                eventID = eventType,
                callback = new EventTrigger.TriggerEvent(),
            });
            EditorUtility.SetDirty(pair.Key);
        }
    }

    /// <summary>
    /// Adds one required event type for an event trigger.
    /// </summary>
    /// <param name="eventTypes">The trigger event-type map.</param>
    /// <param name="trigger">The event trigger.</param>
    /// <param name="eventType">The required event type.</param>
    private static void AddEventType(
        IDictionary<EventTrigger, HashSet<EventTriggerType>> eventTypes,
        EventTrigger trigger,
        EventTriggerType eventType
    )
    {
        if (!eventTypes.TryGetValue(trigger, out HashSet<EventTriggerType> types))
        {
            types = new HashSet<EventTriggerType>();
            eventTypes.Add(trigger, types);
        }

        types.Add(eventType);
    }

    /// <summary>
    /// Finds one uniquely named component in the prefab hierarchy.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="root">The prefab root.</param>
    /// <param name="objectName">The required GameObject name.</param>
    /// <returns>The unique matching component.</returns>
    private static T FindRequiredComponent<T>(GameObject root, string objectName)
        where T : Component
    {
        T[] matches = root.GetComponentsInChildren<T>(true)
            .Where(component => component.gameObject.name == objectName)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name} named {objectName}, found {matches.Length}."
            );
        }

        return matches[0];
    }

    /// <summary>
    /// Finds the unique component of a type in the prefab hierarchy.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="root">The prefab root.</param>
    /// <returns>The unique matching component.</returns>
    private static T FindRequiredComponent<T>(GameObject root)
        where T : Component
    {
        T[] matches = root.GetComponentsInChildren<T>(true);
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name}, found {matches.Length}."
            );
        }

        return matches[0];
    }

    /// <summary>
    /// Loads one required Unity asset at an authored project path.
    /// </summary>
    /// <typeparam name="T">The required asset type.</typeparam>
    /// <param name="path">The project-relative asset path.</param>
    /// <returns>The loaded asset.</returns>
    private static T LoadRequiredAsset<T>(string path)
        where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
    }

    /// <summary>
    /// Builds the Planet rig and inserts the spinning-planet backdrop behind the cockpit
    /// windshield.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    private static void InstallPlanet(GameObject root)
    {
        RenderTexture rt = LoadOrCreateRenderTexture();

        BuildPlanetRig(root, rt);

        // The cockpit image is on the root Canvas GameObject itself -> the backmost layer of
        // that canvas, so nothing under it can render behind it (and a separate canvas did not
        // sort reliably behind it). So disable the root's own image and redraw the cockpit as a
        // CHILD image; the planet can then sit as an EARLIER sibling (behind the cockpit) in the
        // same canvas -- deterministic hierarchy order, no cross-canvas sorting.
        Image rootImage = FindBackgroundImage(root);
        Transform canvasT = rootImage.transform;
        // The current root image is the alpha-windowed cockpit; reuse its sprite as-is.
        Sprite cockpit = rootImage.sprite;
        rootImage.enabled = false;

        GameObject foreground = new GameObject(
            _foregroundName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform fgRect = foreground.GetComponent<RectTransform>();
        fgRect.SetParent(canvasT, false);
        FillParent(fgRect);
        Image fgImage = foreground.GetComponent<Image>();
        fgImage.sprite = cockpit;
        fgImage.raycastTarget = false;
        // The runtime content binding was authored on the now-disabled root image; move it to the
        // foreground cockpit that actually renders so the background is restored at runtime.
        MoveSpriteBinding(rootImage, fgImage);

        GameObject backdrop = new GameObject(
            _backdropName,
            typeof(RectTransform),
            typeof(CanvasRenderer)
        );
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.SetParent(canvasT, false);
        FillParent(backdropRect);

        // Render order within the canvas: space backdrop (behind) -> cockpit -> controls.
        backdrop.transform.SetSiblingIndex(0);
        foreground.transform.SetSiblingIndex(1);

        RawImage stars = NewRawImage(backdrop.transform, "Starfield", null);
        SetBoundTexture(stars, _starfieldAddress);
        FillParent(stars.rectTransform);

        RawImage planet = NewRawImage(backdrop.transform, "Planet", rt);
        // Tilted and positioned in the windshield canopy; values tuned in the editor.
        ApplyPlanetBackdropRect(planet.rectTransform);
    }

    /// <summary>
    /// Applies the shared placement of the planet backdrop layer (globe and its atmosphere) so both
    /// overlay the same region and stay aligned.
    /// </summary>
    /// <param name="rect">The RectTransform to place.</param>
    private static void ApplyPlanetBackdropRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.63f);
        rect.anchorMax = new Vector2(0.5f, 0.63f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(3770.957f, 3526.246f);
        rect.anchoredPosition = new Vector2(74f, -734f);
        rect.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Builds the off-screen planet model, lighting, and camera rig. The model is normalized and
    /// recentered before spinning because its exported scale and origin are arbitrary.
    /// </summary>
    /// <param name="root">The prefab root to parent the rig under.</param>
    /// <param name="renderTexture">The texture the rig camera renders into.</param>
    private static void BuildPlanetRig(GameObject root, RenderTexture renderTexture)
    {
        GameObject rig = new GameObject(_rigName);
        rig.transform.SetParent(root.transform, false);
        rig.transform.position = _planetRigOrigin;

        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(rig.transform, false);
        // The planet stays still; only the cloud layer drifts.

        // The planet ships as a pre-skinned GLB in the content pack. Load it at runtime and apply
        // the same pole-forward rotation, unit normalization, centering, and render layer the baked
        // model used. Posing stays here in code; only the model travels inside the GLB.
        const int planetLayer = 31;
        GameObject planetModelNode = new GameObject("Model");
        planetModelNode.transform.SetParent(pivot.transform, false);
        planetModelNode
            .AddComponent<ContentModelBinding>()
            .SetModel(
                "Application/MainMenu/Models/planet",
                1f,
                new Vector3(0f, 180f, 0f),
                overwrite: true,
                normalize: true,
                center: true,
                layer: planetLayer
            );

        // Cloud layer: a slightly larger sphere on its own pivot so the clouds drift in the
        // planet's spin direction while the planet itself stays still.
        GameObject cloudPivot = new GameObject("CloudPivot");
        cloudPivot.transform.SetParent(rig.transform, false);
        cloudPivot.AddComponent<AutoRotate>().Configure(-_cloudSpinDegreesPerSecond, Vector3.up);
        GameObject clouds = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        clouds.name = "Clouds";
        UnityEngine.Object.DestroyImmediate(clouds.GetComponent<Collider>());
        clouds.transform.SetParent(cloudPivot.transform, false);
        clouds.transform.localScale = Vector3.one * 2.020f; // primitive radius 0.5 -> ~1.010
        clouds.layer = planetLayer;
        clouds
            .AddComponent<RuntimeMaterialBinding>()
            .Configure(_cloudShaderName, _cloudTextureAddress);

        // Atmosphere: a static shell just outside the clouds with a Fresnel rim glow, so the limb
        // reads as a lit atmosphere. Built from a primitive with the custom rim shader assigned here.
        GameObject atmosphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atmosphere.name = "Atmosphere";
        UnityEngine.Object.DestroyImmediate(atmosphere.GetComponent<Collider>());
        atmosphere.transform.SetParent(rig.transform, false);
        atmosphere.transform.localScale = Vector3.one * 2.025f; // primitive radius 0.5 -> ~1.0125
        atmosphere.layer = planetLayer;
        atmosphere.AddComponent<RuntimeMaterialBinding>().Configure(_atmosphereShaderName);

        // A final multiply shell reproduces the prototype's world-space day/night terminator over
        // the complete composited globe, including the asynchronously loaded surface and clouds.
        GameObject dayNightShade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dayNightShade.name = "DayNightShade";
        UnityEngine.Object.DestroyImmediate(dayNightShade.GetComponent<Collider>());
        dayNightShade.transform.SetParent(rig.transform, false);
        dayNightShade.transform.localScale = Vector3.one * 2.040f;
        dayNightShade.layer = planetLayer;
        dayNightShade.AddComponent<RuntimeMaterialBinding>().Configure(_planetDayNightShaderName);

        // Dedicated sun for the planet, masked to their layer so it never touches the icons.
        // Gives the rocky surface real directional shading; the emission only lifts the night side.
        GameObject sunObject = new GameObject("PlanetSun", typeof(Light));
        sunObject.transform.SetParent(rig.transform, false);
        sunObject.transform.localRotation = Quaternion.LookRotation(-_planetSunDirection);
        Light sun = sunObject.GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.6f;
        sun.color = new Color(1f, 0.94f, 0.88f);
        sun.cullingMask = 1 << planetLayer;
        sun.shadows = LightShadows.None;

        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -6f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 1.75f; // frame the globe (radius 1) with margin
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
        camera.cullingMask = 1 << planetLayer;
        camera.targetTexture = renderTexture;
    }

    /// <summary>
    /// Loads the planet RenderTexture asset, creating it if it does not yet exist.
    /// </summary>
    /// <returns>The planet RenderTexture asset.</returns>
    private static RenderTexture LoadOrCreateRenderTexture()
    {
        const int size = 2048; // matches the ~1965px on-screen planet so the HD texture reads crisp
        RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(_renderTexturePath);
        if (existing != null)
        {
            if (existing.width != size || existing.height != size)
            {
                existing.Release();
                existing.width = size;
                existing.height = size;
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }

        RenderTexture created = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
        {
            name = "Planet",
            antiAliasing = 4,
        };
        EnsureAssetFolder(Path.GetDirectoryName(_renderTexturePath));
        AssetDatabase.CreateAsset(created, _renderTexturePath);
        return created;
    }

    /// <summary>
    /// Finds the full-screen cockpit background image in the prefab.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    /// <returns>The background image.</returns>
    private static Image FindBackgroundImage(GameObject root)
    {
        Canvas rootCanvas = root.GetComponentInChildren<Canvas>(true);
        Image onCanvas = rootCanvas != null ? rootCanvas.GetComponent<Image>() : null;
        if (onCanvas != null)
            return onCanvas;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image byName = images.FirstOrDefault(image =>
            image.sprite?.name.Contains("mainmenu_background") == true
        );
        if (byName != null)
            return byName;
        Image byObject = images.FirstOrDefault(image => image.gameObject.name == "Background");
        if (byObject != null)
            return byObject;
        return images.OrderByDescending(image => image.rectTransform.rect.width).First();
    }

    /// <summary>
    /// Creates a RawImage child that does not block pointer input.
    /// </summary>
    /// <param name="parent">The parent transform.</param>
    /// <param name="name">The GameObject name.</param>
    /// <param name="texture">The texture to display.</param>
    /// <returns>The created RawImage.</returns>
    private static RawImage NewRawImage(Transform parent, string name, Texture texture)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage rawImage = go.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    /// <summary>
    /// Stretches a RectTransform to fill its parent.
    /// </summary>
    /// <param name="rectTransform">The RectTransform to stretch.</param>
    private static void FillParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// One icon rig's data: which model, which RenderTexture asset, and which control shows it.
    /// </summary>
    private readonly struct Face
    {
        public readonly string FactionId;
        public readonly string ModelPath;
        public readonly string RenderTexturePath;
        public readonly string ButtonName;

        // Sign applied to the spin speed: +1 turns one way, -1 the other.
        public readonly float SpinDirection;

        // Local axis the model spins around: Vector3.up for a coin turntable, Vector3.right for a
        // ship barrel-rolling on its long (nose-to-tail) axis.
        public readonly Vector3 SpinAxis;

        // Presentation scale and a fixed tilt (Euler) so a ship can be shown banked at an angle.
        public readonly float ModelScale;
        public readonly Vector3 Tilt;

        // A static rotation applied to the spinning pivot itself (not the model). Because the spin
        // is incremental in the pivot's local space, this tilts the whole spin — e.g. a nose-down
        // attitude on a barrel-rolling ship — without making the roll wobble.
        public readonly Vector3 PivotTilt;

        // Target aspect of the RenderTexture. Coins are square; the ship icon matches the flipbook
        // it replaces (255x180 landscape) so it is not squashed when it fills the toggle.
        public readonly int RtWidth;
        public readonly int RtHeight;

        // When true the target is a Toggle (difficulty icon): only its own flipbook graphic is
        // replaced, leaving the selection overlay and toggle behaviour intact.
        public readonly bool IsToggle;

        // Adds a short-range point light beneath icons that need extra underside detail.
        public readonly float UnderLightIntensity;

        public Face(
            string factionId,
            string modelPath,
            string renderTexturePath,
            string buttonName,
            float spinDirection,
            Vector3 spinAxis,
            float modelScale,
            Vector3 tilt,
            Vector3 pivotTilt = default,
            int rtWidth = 512,
            int rtHeight = 512,
            bool isToggle = false,
            float underLightIntensity = 0f
        )
        {
            FactionId = factionId;
            ModelPath = modelPath;
            RenderTexturePath = renderTexturePath;
            ButtonName = buttonName;
            SpinDirection = spinDirection;
            SpinAxis = spinAxis;
            ModelScale = modelScale;
            Tilt = tilt;
            PivotTilt = pivotTilt;
            RtWidth = rtWidth;
            RtHeight = rtHeight;
            IsToggle = isToggle;
            UnderLightIntensity = underLightIntensity;
        }
    }

    /// <summary>
    /// Builds one icon rig per slot under a shared container and points each control at its
    /// RenderTexture.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    private static void InstallSpinning3DIcons(GameObject root)
    {
        GameObject iconRigs = new GameObject(_iconRigsName);
        iconRigs.transform.SetParent(root.transform, false);

        BuildSharedLighting(iconRigs);

        for (int i = 0; i < _faces.Length; i++)
        {
            Face face = _faces[i];

            RenderTexture renderTexture = LoadOrCreateRenderTexture(
                face.RenderTexturePath,
                face.FactionId,
                face.RtWidth,
                face.RtHeight
            );

            BuildRig(iconRigs, renderTexture, face, i);

            Transform button = FindDeep(root.transform, face.ButtonName);
            if (button == null)
                throw new InvalidOperationException(
                    $"Button '{face.ButtonName}' not found in prefab."
                );
            if (face.IsToggle)
                AttachToggleIcon(button.gameObject, renderTexture);
            else
                AttachButtonIcon(button.gameObject, renderTexture);
        }

        // Left faction icon nudged right to align (tuned in Play): Left 9, Right -9. Top/bottom kept.
        Transform leftButton = FindDeep(root.transform, "LeftFactionLaunchButton");
        Transform leftIcon = leftButton != null ? FindDeep(leftButton, "Icon3D") : null;
        if (leftIcon is RectTransform leftIconRect)
        {
            leftIconRect.offsetMin = new Vector2(9f, leftIconRect.offsetMin.y);
            leftIconRect.offsetMax = new Vector2(9f, leftIconRect.offsetMax.y);
        }
    }

    /// <summary>
    /// Replaces the victory-condition icon with a spinning citadel model and adds the HQ selection
    /// overlay.
    /// </summary>
    /// <param name="root">The prefab root.</param>
    private static void InstallHqCitadel(GameObject root)
    {
        Transform iconRigs = FindDeep(root.transform, _iconRigsName);
        if (iconRigs == null)
            throw new InvalidOperationException(
                "Icon rigs container not found; run InstallHqCitadel after InstallSpinning3DIcons."
            );

        RenderTexture renderTexture = LoadOrCreateRenderTexture(
            _citadelRenderTexturePath,
            "HqCitadel",
            256,
            256
        );
        AutoRotate citadelSpinner = BuildCitadelRig(
            iconRigs.gameObject,
            renderTexture,
            _faces.Length
        );

        Transform icon = FindDeep(root.transform, "VictoryConditionIcon");
        if (icon == null)
            throw new InvalidOperationException("VictoryConditionIcon not found in prefab.");

        // The Image is the view's target but renders nothing (transparent) so the citadel RawImage
        // shows through. It overlays a separate VictoryConditionButton sibling that owns the click, so
        // every graphic here stays raycast-off or it swallows the button's clicks.
        Image iconImage = icon.GetComponent<Image>();
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = new Color(1f, 1f, 1f, 0f);
            iconImage.raycastTarget = false;
        }
        RawImage citadelIcon = AddIconOverlay(icon, renderTexture, behindSiblings: false);
        // The 3D icon is decorative; it must not intercept clicks meant for the button underneath.
        citadelIcon.raycastTarget = false;
        // Placement tuned in Play and baked here: Left/Top/Right/Bottom offsets on the stretched rect.
        RectTransform citadelRect = citadelIcon.rectTransform;
        citadelRect.offsetMin = new Vector2(12.39164f, 12.1074f);
        citadelRect.offsetMax = new Vector2(-10.42626f, 2.519201f);

        // HQ-only crosshair, on top of the citadel and hidden by default. RenderVictoryCondition shows
        // it when Headquarters victory is selected.
        GameObject selectionOverlay = NewChild(
            "SelectionOverlay",
            icon,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        // Match the citadel's inset rect so the crosshair sits over the model, not the whole panel.
        RectTransform selectionRect = selectionOverlay.GetComponent<RectTransform>();
        selectionRect.anchorMin = Vector2.zero;
        selectionRect.anchorMax = Vector2.one;
        selectionRect.offsetMin = citadelRect.offsetMin;
        selectionRect.offsetMax = citadelRect.offsetMax;
        Image selectionImage = selectionOverlay.GetComponent<Image>();
        SetBoundSprite(selectionImage, "Application/MainMenu/UI/ui_mainmenu_hqonly_icon");
        selectionImage.preserveAspect = true;
        selectionImage.raycastTarget = false;
        selectionOverlay.SetActive(false);

        // Give the view the citadel spinner so it can pause the spin while the HQ-only crosshair shows.
        MainMenuView view = FindRequiredComponent<MainMenuView>(root);
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("victoryConditionSpinner").objectReferenceValue =
            citadelSpinner;
        serializedView.FindProperty("victoryConditionSelectionOverlay").objectReferenceValue =
            selectionOverlay;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Builds the off-screen citadel model + camera rig that renders the spinning HQ symbol to its
    /// texture on a solid black background.
    /// </summary>
    /// <param name="parent">The rig container.</param>
    /// <param name="renderTexture">The texture the rig camera renders into.</param>
    /// <param name="index">The rig slot index, offsetting it clear of the faction icon rigs.</param>
    /// <returns>The rig's spin component so the view can pause it for the HQ-only state.</returns>
    private static AutoRotate BuildCitadelRig(
        GameObject parent,
        RenderTexture renderTexture,
        int index
    )
    {
        GameObject rig = new GameObject("IconRig_HqCitadel");
        rig.transform.SetParent(parent.transform, false);
        rig.transform.position = _rigOrigin + new Vector3(0f, 0f, index * _rigSpacing);

        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(rig.transform, false);
        AutoRotate spinner = pivot.AddComponent<AutoRotate>();
        spinner.Configure(-_iconTurnDegreesPerSecond * 0.5f, Vector3.up);

        // Normalize to a consistent size and center on the pivot, then offset down so the dome sits in
        // frame and the tall stem drops below the camera crop. Offset/scale/size are eyeball -- tune.
        GameObject modelNode = new GameObject("Model");
        modelNode.transform.SetParent(pivot.transform, false);
        modelNode.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        // The model's up axis is Z (bounding box: X~Y~1.6, Z~1.9); +90 X maps that to Unity's +Y,
        // dome up, matching the original HQ icon.
        modelNode
            .AddComponent<ContentModelBinding>()
            .SetModel(
                _citadelModelAddress,
                1f,
                new Vector3(90f, 0f, 0f),
                overwrite: true,
                normalize: true,
                center: true,
                layer: -1
            );

        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        // Transparent clear so the citadel blends into the menu like the faction icons (no black box).
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.orthographic = true;
        // Smaller size = bigger on-screen icon. Tune this to grow/shrink the citadel.
        camera.orthographicSize = 0.7f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;
        camera.targetTexture = renderTexture;

        GameObject underLightObject = new GameObject("UnderLight", typeof(Light));
        underLightObject.transform.SetParent(rig.transform, false);
        underLightObject.transform.localPosition = new Vector3(0f, -2.5f, -1.5f);
        Light underLight = underLightObject.GetComponent<Light>();
        underLight.type = LightType.Point;
        underLight.range = 8f;
        underLight.intensity = 2f;
        underLight.color = Color.white;

        return spinner;
    }

    /// <summary>
    /// Loads an icon slot's RenderTexture asset, creating it if it does not yet exist.
    /// </summary>
    /// <param name="path">The RenderTexture asset path.</param>
    /// <param name="factionId">The slot identifier, used to name the asset.</param>
    /// <param name="width">The RenderTexture width.</param>
    /// <param name="height">The RenderTexture height.</param>
    /// <returns>The icon RenderTexture asset.</returns>
    private static RenderTexture LoadOrCreateRenderTexture(
        string path,
        string factionId,
        int width,
        int height
    )
    {
        RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (existing != null)
        {
            // Re-assert the intended dimensions in case an older square asset is on disk.
            if (existing.width != width || existing.height != height)
            {
                existing.Release();
                existing.width = width;
                existing.height = height;
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }

        RenderTexture created = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = $"Icon_{factionId}",
            antiAliasing = 4,
        };
        EnsureAssetFolder(Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    /// <summary>
    /// Creates each missing segment of a generated asset folder.
    /// </summary>
    /// <param name="folderPath">The project-relative folder path to create.</param>
    private static void EnsureAssetFolder(string folderPath)
    {
        string currentPath = "Assets";
        foreach (string segment in folderPath.Replace('\\', '/').Split('/').Skip(1))
        {
            string childPath = $"{currentPath}/{segment}";
            if (!AssetDatabase.IsValidFolder(childPath))
                AssetDatabase.CreateFolder(currentPath, segment);
            currentPath = childPath;
        }
    }

    /// <summary>
    /// Builds one off-screen model + camera rig that renders an icon to its texture.
    /// </summary>
    /// <param name="parent">The container to parent the rig under.</param>
    /// <param name="renderTexture">The texture the rig camera renders into.</param>
    /// <param name="face">The face describing spin, scale, and tilt.</param>
    /// <param name="index">The slot index, used to offset the rig so cameras stay isolated.</param>
    private static void BuildRig(
        GameObject parent,
        RenderTexture renderTexture,
        Face face,
        int index
    )
    {
        string rigName = $"IconRig_{face.FactionId}";
        GameObject rig = new GameObject(rigName);
        rig.transform.SetParent(parent.transform, false);
        rig.transform.position = _rigOrigin + new Vector3(0f, 0f, index * _rigSpacing);

        // AutoRotate goes on a clean, identity-rotation pivot so imported model transforms cannot
        // turn the vertical spin into a wobble.
        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(rig.transform, false);
        // Static presentation tilt on the pivot (e.g. nose-down); the incremental local-space spin
        // then rolls around this tilted frame without wobbling.
        pivot.transform.localRotation = Quaternion.Euler(face.PivotTilt);
        pivot
            .AddComponent<AutoRotate>()
            .Configure(_iconTurnDegreesPerSecond * face.SpinDirection, face.SpinAxis);

        // The icon ships as a pre-skinned GLB. Load it at runtime with its authored scale, tilt, and
        // centering. Rig spacing isolates each camera, so no dedicated render layer is needed.
        GameObject modelNode = new GameObject("Model");
        modelNode.transform.SetParent(pivot.transform, false);
        modelNode
            .AddComponent<ContentModelBinding>()
            .SetModel(
                ToModelAddress(face.ModelPath),
                face.ModelScale,
                face.Tilt,
                overwrite: true,
                normalize: false,
                center: true,
                layer: -1
            );

        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 1.3f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;
        camera.targetTexture = renderTexture;

        if (face.UnderLightIntensity > 0f)
        {
            GameObject underLightObject = new GameObject("UnderLight", typeof(Light));
            underLightObject.transform.SetParent(rig.transform, false);
            underLightObject.transform.localPosition = new Vector3(0f, -2.5f, -1.5f);
            Light underLight = underLightObject.GetComponent<Light>();
            underLight.type = LightType.Point;
            underLight.range = 8f;
            underLight.intensity = face.UnderLightIntensity;
            underLight.color = Color.white;
        }
    }

    /// <summary>
    /// Builds the shared key and fill lights used by the main-menu icon rigs.
    /// </summary>
    /// <param name="parent">The container that owns the lights.</param>
    private static void BuildSharedLighting(GameObject parent)
    {
        GameObject lights = new GameObject(_lightsName);
        lights.transform.SetParent(parent.transform, false);
        lights.transform.position = _rigOrigin;

        AddDirectionalLight(
            lights.transform,
            "Key",
            Quaternion.Euler(45f, 35f, 0f),
            0.6f,
            Color.white,
            LightShadows.Soft
        );
        AddDirectionalLight(
            lights.transform,
            "Fill",
            Quaternion.Euler(20f, 40f, 0f),
            0.35f,
            new Color(0.85f, 0.9f, 1f),
            LightShadows.None
        );
        AddDirectionalLight(
            lights.transform,
            "Rim",
            Quaternion.Euler(35f, 175f, 0f),
            1f,
            Color.white,
            LightShadows.None
        );
    }

    /// <summary>
    /// Adds one directional light to the shared icon lighting rig.
    /// </summary>
    /// <param name="parent">The lighting-rig parent.</param>
    /// <param name="name">The light name.</param>
    /// <param name="rotation">The light rotation.</param>
    /// <param name="intensity">The light intensity.</param>
    /// <param name="color">The light color.</param>
    /// <param name="shadows">The shadow mode.</param>
    private static void AddDirectionalLight(
        Transform parent,
        string name,
        Quaternion rotation,
        float intensity,
        Color color,
        LightShadows shadows
    )
    {
        GameObject lightObject = new GameObject($"IconLight_{name}", typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation = rotation;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = shadows;
        light.cullingMask = ~(1 << 31);
    }

    /// <summary>
    /// Attaches the spinning 3D icon (a RawImage of the RenderTexture) to a faction launch button
    /// and points the button's graphic target at it.
    /// </summary>
    /// <param name="button">The faction button GameObject.</param>
    /// <param name="renderTexture">The icon RenderTexture to display.</param>
    private static void AttachButtonIcon(GameObject button, RenderTexture renderTexture)
    {
        RawImage rawImage = AddIconOverlay(button.transform, renderTexture, false);
        foreach (Selectable selectable in button.GetComponentsInChildren<Selectable>(true))
        {
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = rawImage;
        }
    }

    /// <summary>
    /// Attaches the spinning 3D icon (a RawImage of the RenderTexture) behind a difficulty toggle's
    /// selection overlay and points the toggle's graphic target at it.
    /// </summary>
    /// <param name="toggle">The difficulty toggle GameObject.</param>
    /// <param name="renderTexture">The icon RenderTexture to display.</param>
    private static void AttachToggleIcon(GameObject toggle, RenderTexture renderTexture)
    {
        RawImage rawImage = AddIconOverlay(toggle.transform, renderTexture, true);
        foreach (Selectable selectable in toggle.GetComponents<Selectable>())
        {
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = rawImage;
        }
    }

    /// <summary>
    /// Adds an "Icon3D" RawImage that fills its parent and shows the given RenderTexture.
    /// </summary>
    /// <param name="parent">The control the overlay is parented to.</param>
    /// <param name="renderTexture">The texture the overlay displays.</param>
    /// <param name="behindSiblings">When true the overlay is placed first so siblings draw over it.</param>
    /// <returns>The created RawImage.</returns>
    private static RawImage AddIconOverlay(
        Transform parent,
        RenderTexture renderTexture,
        bool behindSiblings
    )
    {
        GameObject overlay = new GameObject("Icon3D", typeof(RectTransform), typeof(RawImage));
        overlay.transform.SetParent(parent, false);
        if (behindSiblings)
            overlay.transform.SetSiblingIndex(0);
        FillParent(overlay.GetComponent<RectTransform>());
        RawImage rawImage = overlay.GetComponent<RawImage>();
        rawImage.texture = renderTexture;
        rawImage.raycastTarget = true;
        return rawImage;
    }

    /// <summary>
    /// Rebuilds the complete Main Menu UI and installs it in its scene.
    /// </summary>
    public static void Rebuild()
    {
        RebuildMainMenuPrefab();
        SceneBuilder.Build(
            _scenePath,
            _prefabPath,
            _sceneInstanceName,
            () => RenderSettings.reflectionIntensity = 0f
        );
    }

    /// <summary>
    /// Finds a descendant transform by name, searching the whole subtree.
    /// </summary>
    /// <param name="parent">The transform whose subtree is searched.</param>
    /// <param name="targetName">The GameObject name to find.</param>
    /// <returns>The matching transform, or null when none is found.</returns>
    private static Transform FindDeep(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;
            Transform found = FindDeep(child, targetName);
            if (found != null)
                return found;
        }
        return null;
    }
}
