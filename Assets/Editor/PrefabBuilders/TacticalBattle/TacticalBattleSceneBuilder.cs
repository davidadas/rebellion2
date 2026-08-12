using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Generates the tactical space-battle scene from code-owned defaults.
/// </summary>
public static class TacticalBattleSceneBuilder
{
    internal const string ScenePath = "Assets/Scenes/TacticalBattle.unity";

    /// <summary>
    /// Rebuilds the tactical battle scene and enables it for player builds.
    /// </summary>
    public static void Rebuild()
    {
        UIAuthoringGuard.EnsureEditMode();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = TacticalBattleLaunchContext.SceneName;

        ConfigureEnvironment();
        GameObject root = CreateSceneController();
        CreateBattleSpace(root.transform);
        TacticalCameraRig cameraRig = CreateCamera(root.transform);
        CreateLight();
        CreateHud(root.transform, cameraRig);
        CreateEventSystem();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
        if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
            throw new IOException($"Could not generate scene: {ScenePath}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnableBuildScene();
    }

    /// <summary>
    /// Configures the scene's neutral space-lighting defaults.
    /// </summary>
    private static void ConfigureEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.12f, 1f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
    }

    /// <summary>
    /// Creates the component that owns tactical scene state.
    /// </summary>
    private static GameObject CreateSceneController()
    {
        GameObject root = new GameObject(TacticalBattleLaunchContext.SceneName);
        root.AddComponent<TacticalBattleController>();
        return root;
    }

    /// <summary>
    /// Creates the runtime-owned hierarchy for tactical unit presentation.
    /// </summary>
    /// <param name="parent">The tactical scene root.</param>
    private static void CreateBattleSpace(Transform parent)
    {
        GameObject battleSpace = new GameObject("BattleSpace");
        battleSpace.transform.SetParent(parent, false);
        battleSpace.AddComponent<TacticalBattleRenderer>();
    }

    /// <summary>
    /// Creates the original 640 by 480 tactical control surface from configured content.
    /// </summary>
    /// <param name="parent">The tactical scene root.</param>
    /// <param name="cameraRig">The tactical camera controlled by the HUD.</param>
    private static void CreateHud(Transform parent, TacticalCameraRig cameraRig)
    {
        TacticalBattleTheme theme = GetPreviewTheme();
        string root = theme.SharedUIRoot;

        GameObject canvasObject = new GameObject("TacticalHud", typeof(RectTransform));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.AddComponent<GraphicRaycaster>();

        TacticalBattleView view = canvasObject.AddComponent<TacticalBattleView>();
        Transform controlSurface = CreateControlSurface(canvasObject.transform);
        CreateControlPanelFrame(controlSurface, $"{root}/Hud/control-panel");
        RectTransform viewport = CreateViewport(controlSurface);
        cameraRig.ConfigureViewport(viewport);
        CreateBoundImage(
            "TaskForceHeader",
            controlSurface,
            theme.TaskForceHeaderImagePath,
            9,
            1,
            49,
            19
        );
        CreateBoundImage(
            "FighterGroupHeader",
            controlSurface,
            theme.FighterGroupHeaderImagePath,
            296,
            1,
            49,
            19
        );

        Button[] taskForceButtons = new Button[8];
        for (int index = 0; index < taskForceButtons.Length; index++)
        {
            taskForceButtons[index] = CreateBoundButton(
                $"TaskForce{index + 1}",
                controlSurface,
                $"{root}/TaskForces/group-up",
                $"{root}/TaskForces/group-down",
                60 + index * 29,
                2,
                25,
                17
            );
        }

        string[] fighterColors = { "red", "blue", "green", "gold" };
        Button[] fighterGroupButtons = new Button[fighterColors.Length];
        for (int index = 0; index < fighterGroupButtons.Length; index++)
        {
            string color = fighterColors[index];
            fighterGroupButtons[index] = CreateBoundButton(
                $"{char.ToUpperInvariant(color[0])}{color[1..]}FighterGroup",
                controlSurface,
                $"{root}/FighterGroups/{color}-up",
                $"{root}/FighterGroups/{color}-down",
                347 + index * 29,
                2,
                25,
                15
            );
        }

        Button[] navigationButtons = new Button[4];
        for (int index = 0; index < navigationButtons.Length; index++)
        {
            int set = index + 1;
            navigationButtons[index] = CreateBoundButton(
                $"NavigationSet{set}",
                controlSurface,
                $"{root}/Navigation/set-{set}-up",
                $"{root}/Navigation/set-{set}-down",
                485 + index * 39,
                272,
                27,
                27
            );
        }

        GameObject capitalShipStatusPanel = new GameObject(
            "CapitalShipStatus",
            typeof(RectTransform)
        );
        capitalShipStatusPanel.transform.SetParent(controlSurface, false);
        SetSourceRect(capitalShipStatusPanel.GetComponent<RectTransform>(), 482, 24, 149, 236);
        CreateBoundImage(
            "Background",
            capitalShipStatusPanel.transform,
            $"{root}/1302-1033-tactical-ui-right-panel-hull-integrity-shield-strength",
            0,
            0,
            149,
            236
        );
        Button previousCapitalShipButton = CreateBoundButton(
            "PreviousCapitalShip",
            capitalShipStatusPanel.transform,
            $"{root}/1101-1033-tactical-ui-previous-capital-ship-in-task-force-unpressed",
            $"{root}/1102-1033-tactical-ui-previous-capital-ship-in-task-force-pressed",
            7,
            10,
            11,
            53
        );
        Button nextCapitalShipButton = CreateBoundButton(
            "NextCapitalShip",
            capitalShipStatusPanel.transform,
            $"{root}/1103-1033-tactical-ui-next-capital-ship-in-task-force-unpressed",
            $"{root}/1104-1033-tactical-ui-next-capital-ship-in-task-force-pressed",
            132,
            10,
            11,
            53
        );
        Button capitalShipMissionsButton = CreateBoundButton(
            "Missions",
            capitalShipStatusPanel.transform,
            $"{root}/1105-1033-tactical-ui-maneuvers-tactics-unpressed",
            $"{root}/1106-1033-tactical-ui-maneuvers-tactics-pressed",
            80,
            210,
            58,
            22
        );
        Button capitalShipManeuversButton = CreateBoundButton(
            "Maneuvers",
            capitalShipStatusPanel.transform,
            $"{root}/1107-1033-tactical-ui-missions-unpressed",
            $"{root}/1108-1033-tactical-ui-missions-pressed",
            12,
            210,
            58,
            22
        );
        Image hullStatusBar = CreateStatusBar(
            "HullIntegrity",
            capitalShipStatusPanel.transform,
            new Color32(255, 0, 0, 255),
            34,
            81,
            37,
            10
        );
        Image shieldStatusBar = CreateStatusBar(
            "ShieldStrength",
            capitalShipStatusPanel.transform,
            new Color32(0, 128, 255, 255),
            103,
            81,
            37,
            10
        );
        string[] initialSystemImages =
        {
            "1205-1033-tactical-ui-shield-recharge-100%",
            "1210-1033-tactical-ui-weapon-recharge-100%",
            "1215-1033-tactical-ui-tractor-beam-100%",
            "1220-1033-tactical-ui-engines-100%",
            "1225-1033-tactical-ui-hyperspace-engines-100%",
        };
        RawImage[] systemStatusImages = new RawImage[initialSystemImages.Length];
        for (int index = 0; index < systemStatusImages.Length; index++)
        {
            systemStatusImages[index] = CreateBoundImage(
                $"SystemStatus{index + 1}",
                capitalShipStatusPanel.transform,
                $"{root}/{initialSystemImages[index]}",
                10 + index * 27,
                103,
                22,
                15
            );
        }
        TextMeshProUGUI capitalShipName = CreateStatusText(
            "ShipName",
            capitalShipStatusPanel.transform,
            26,
            7,
            97,
            9
        );
        RawImage capitalShipStatusImage = CreateImage(
            "ShipStatusImage",
            capitalShipStatusPanel.transform,
            26,
            17,
            96,
            39
        );
        TextMeshProUGUI capitalShipOrder = CreateStatusText(
            "Order",
            capitalShipStatusPanel.transform,
            15,
            182,
            121,
            9
        );
        TextMeshProUGUI capitalShipTaskForce = CreateStatusText(
            "TaskForce",
            capitalShipStatusPanel.transform,
            26,
            52,
            97,
            9
        );
        TextMeshProUGUI capitalShipFormation = CreateStatusText(
            "Formation",
            capitalShipStatusPanel.transform,
            15,
            193,
            121,
            9
        );

        GameObject superlaserPanel = new GameObject("Superlaser", typeof(RectTransform));
        superlaserPanel.transform.SetParent(controlSurface, false);
        SetSourceRect(superlaserPanel.GetComponent<RectTransform>(), 474, 0, 166, 25);
        CreateBoundImage(
            "Background",
            superlaserPanel.transform,
            $"{root}/1024-1033-tactical-ui-death-star-laser-gauge",
            0,
            0,
            166,
            25
        );
        Image superlaserChargeBar = CreateStatusBar(
            "Charge",
            superlaserPanel.transform,
            new Color32(64, 255, 64, 255),
            34,
            9,
            95,
            6
        );
        Button superlaserButton = CreateBoundButton(
            "Fire",
            superlaserPanel.transform,
            $"{root}/1021-1033-tactical-ui-death-star-laser-ready",
            $"{root}/1022-1033-tactical-ui-death-star-laser-fired",
            137,
            2,
            21,
            20
        );
        RawImage superlaserButtonImage = superlaserButton.GetComponent<RawImage>();

        GameObject missionOrderPanel = new GameObject("MissionOrders", typeof(RectTransform));
        missionOrderPanel.transform.SetParent(controlSurface, false);
        RectTransform missionOrderRect = missionOrderPanel.GetComponent<RectTransform>();
        missionOrderRect.anchorMin = Vector2.zero;
        missionOrderRect.anchorMax = Vector2.one;
        missionOrderRect.offsetMin = Vector2.zero;
        missionOrderRect.offsetMax = Vector2.zero;
        CreateBoundImage(
            "Background",
            missionOrderPanel.transform,
            $"{root}/FighterOrders/panel",
            482,
            24,
            149,
            236
        );
        string missionOrderVariant = theme.FighterOrderVariant;
        if (missionOrderVariant != "alliance" && missionOrderVariant != "empire")
            throw new InvalidOperationException(
                $"Unsupported tactical mission-order variant: '{missionOrderVariant}'."
            );

        Button[] missionOrderButtons =
        {
            CreatePreviewButton(
                "AttackCapitalShips",
                missionOrderPanel.transform,
                $"{root}/FighterOrders/{missionOrderVariant}-attack-capital-ships-up",
                $"{root}/FighterOrders/{missionOrderVariant}-attack-capital-ships-down",
                500,
                153,
                46,
                26
            ),
            CreatePreviewButton(
                "Recover",
                missionOrderPanel.transform,
                $"{root}/FighterOrders/{missionOrderVariant}-recover-up",
                $"{root}/FighterOrders/{missionOrderVariant}-recover-down",
                568,
                153,
                46,
                26
            ),
            CreatePreviewButton(
                "AttackDeathStar",
                missionOrderPanel.transform,
                $"{root}/FighterOrders/attack-death-star-up",
                $"{root}/FighterOrders/attack-death-star-down",
                568,
                183,
                46,
                26
            ),
            CreatePreviewButton(
                "AttackFighters",
                missionOrderPanel.transform,
                $"{root}/FighterOrders/{missionOrderVariant}-attack-fighters-up",
                $"{root}/FighterOrders/{missionOrderVariant}-attack-fighters-down",
                500,
                183,
                46,
                26
            ),
        };
        Button assignMissionOrderButton = CreateBoundButton(
            "Assign",
            missionOrderPanel.transform,
            $"{root}/Actions/assign-up",
            $"{root}/Actions/assign-down",
            548,
            226,
            27,
            25
        );
        Button cancelMissionOrderButton = CreateBoundButton(
            "Cancel",
            missionOrderPanel.transform,
            $"{root}/Actions/cancel-up",
            $"{root}/Actions/cancel-down",
            588,
            226,
            27,
            25
        );

        GameObject maneuverPanel = new GameObject("Maneuvers", typeof(RectTransform));
        maneuverPanel.transform.SetParent(controlSurface, false);
        RectTransform maneuverRect = maneuverPanel.GetComponent<RectTransform>();
        maneuverRect.anchorMin = Vector2.zero;
        maneuverRect.anchorMax = Vector2.one;
        maneuverRect.offsetMin = Vector2.zero;
        maneuverRect.offsetMax = Vector2.zero;
        CreateBoundImage(
            "Background",
            maneuverPanel.transform,
            $"{root}/Maneuvers/panel",
            482,
            24,
            149,
            236
        );
        Button[] maneuverButtons =
        {
            CreateBoundButton(
                "LeftHook",
                maneuverPanel.transform,
                $"{root}/Maneuvers/left-hook-up",
                $"{root}/Maneuvers/left-hook-down",
                495,
                183,
                28,
                28
            ),
            CreateBoundButton(
                "RightHook",
                maneuverPanel.transform,
                $"{root}/Maneuvers/right-hook-up",
                $"{root}/Maneuvers/right-hook-down",
                525,
                183,
                28,
                28
            ),
            CreateBoundButton(
                "Hammer",
                maneuverPanel.transform,
                $"{root}/Maneuvers/hammer-up",
                $"{root}/Maneuvers/hammer-down",
                495,
                153,
                28,
                28
            ),
            CreateBoundButton(
                "Anvil",
                maneuverPanel.transform,
                $"{root}/Maneuvers/anvil-up",
                $"{root}/Maneuvers/anvil-down",
                525,
                153,
                28,
                28
            ),
            CreateBoundButton(
                "Hold",
                maneuverPanel.transform,
                $"{root}/Maneuvers/hold-up",
                $"{root}/Maneuvers/hold-down",
                557,
                168,
                28,
                28
            ),
        };
        Button formationButton = CreateBoundButton(
            "Formation",
            maneuverPanel.transform,
            $"{root}/Maneuvers/stand-off",
            $"{root}/Maneuvers/surround",
            593,
            156,
            24,
            52
        );
        Button assignManeuverButton = CreateBoundButton(
            "Assign",
            maneuverPanel.transform,
            $"{root}/Actions/assign-up",
            $"{root}/Actions/assign-down",
            548,
            226,
            27,
            25
        );
        Button cancelManeuverButton = CreateBoundButton(
            "Cancel",
            maneuverPanel.transform,
            $"{root}/Actions/cancel-up",
            $"{root}/Actions/cancel-down",
            588,
            226,
            27,
            25
        );

        Button leftShipHighlightsButton = CreateBoundButton(
            "AllianceShipHighlights",
            controlSurface,
            $"{root}/Hud/alliance-highlight-up",
            $"{root}/Hud/alliance-highlight-down",
            483,
            305,
            30,
            26
        );
        Button rightShipHighlightsButton = CreateBoundButton(
            "EmpireShipHighlights",
            controlSurface,
            $"{root}/Hud/empire-highlight-up",
            $"{root}/Hud/empire-highlight-down",
            515,
            305,
            30,
            26
        );
        Button gameOptionsButton = CreateBoundButton(
            "GameOptions",
            controlSurface,
            $"{root}/Hud/options-up",
            $"{root}/Hud/options-down",
            608,
            309,
            20,
            20
        );

        RawImage pauseImage = CreateBoundImage(
            "Pause",
            controlSurface,
            $"{root}/Hud/pause",
            561,
            308,
            28,
            21
        );
        Button pauseButton = CreateButton(pauseImage);

        GameObject gameOptionsPanel = new GameObject("GameOptionsPanel", typeof(RectTransform));
        gameOptionsPanel.transform.SetParent(controlSurface, false);
        RectTransform gameOptionsRect = gameOptionsPanel.GetComponent<RectTransform>();
        SetSourceRect(gameOptionsRect, 482, 24, 149, 236);
        CreateBoundImage(
            "Background",
            gameOptionsPanel.transform,
            $"{root}/1306-1033-tactical-ui-game-options-hud",
            0,
            0,
            149,
            236
        );
        Button withdrawalButton = CreateBoundButton(
            "Withdraw",
            gameOptionsPanel.transform,
            $"{root}/Actions/withdraw-up",
            $"{root}/Actions/withdraw-down",
            18,
            19,
            48,
            34
        );
        Button immediateResultButton = CreateBoundButton(
            "ImmediateResult",
            gameOptionsPanel.transform,
            $"{root}/Actions/immediate-result-up",
            $"{root}/Actions/immediate-result-down",
            14,
            93,
            44,
            25
        );
        RawImage commandModeImage = CreateBoundImage(
            "CommandMode",
            gameOptionsPanel.transform,
            $"{root}/1154-1033-tactical-ui-observe-battle",
            87,
            93,
            45,
            25
        );
        Button commandModeButton = CreateButton(commandModeImage);
        Button settingsButton = CreateBoundButton(
            "Settings",
            gameOptionsPanel.transform,
            $"{root}/1155-1033-tactical-ui-game-options-unpressed",
            $"{root}/1156-1033-tactical-ui-game-options-pressed",
            23,
            184,
            52,
            33
        );
        Button closeGameOptionsButton = CreateBoundButton(
            "Close",
            gameOptionsPanel.transform,
            $"{root}/1157-1033-tactical-ui-close-unpressed",
            $"{root}/1158-1033-tactical-ui-close-pressed",
            97,
            184,
            29,
            33
        );
        GameObject withdrawalPanel = new GameObject(
            "WithdrawalConfirmation",
            typeof(RectTransform)
        );
        withdrawalPanel.transform.SetParent(controlSurface, false);
        RectTransform withdrawalRect = withdrawalPanel.GetComponent<RectTransform>();
        SetSourceRect(withdrawalRect, 482, 24, 149, 236);
        CreateBoundImage(
            "Background",
            withdrawalPanel.transform,
            $"{root}/Hud/withdrawal-confirmation",
            0,
            0,
            149,
            236
        );
        Button confirmWithdrawalButton = CreateBoundButton(
            "Confirm",
            withdrawalPanel.transform,
            $"{root}/Actions/assign-up",
            $"{root}/Actions/assign-down",
            66,
            202,
            27,
            25
        );
        Button cancelWithdrawalButton = CreateBoundButton(
            "Cancel",
            withdrawalPanel.transform,
            $"{root}/Actions/cancel-up",
            $"{root}/Actions/cancel-down",
            106,
            202,
            27,
            25
        );
        Button[] cameraControls = CreateCameraControls(controlSurface, root);
        cameraRig.Configure(cameraRig.GetComponent<Camera>(), cameraControls);
        view.Configure(
            taskForceButtons,
            fighterGroupButtons,
            navigationButtons,
            missionOrderPanel,
            missionOrderButtons,
            assignMissionOrderButton,
            cancelMissionOrderButton,
            maneuverPanel,
            maneuverButtons,
            formationButton,
            assignManeuverButton,
            cancelManeuverButton,
            pauseButton,
            pauseImage
        );
        view.ConfigureWithdrawal(
            withdrawalButton,
            withdrawalPanel,
            confirmWithdrawalButton,
            cancelWithdrawalButton
        );
        view.ConfigureShipHighlights(leftShipHighlightsButton, rightShipHighlightsButton);
        view.ConfigureGameOptions(
            gameOptionsButton,
            gameOptionsPanel,
            immediateResultButton,
            commandModeButton,
            commandModeImage,
            settingsButton,
            closeGameOptionsButton
        );
        view.ConfigureCapitalShipStatus(
            capitalShipStatusPanel,
            previousCapitalShipButton,
            nextCapitalShipButton,
            capitalShipMissionsButton,
            capitalShipManeuversButton,
            hullStatusBar,
            shieldStatusBar,
            systemStatusImages,
            capitalShipName,
            capitalShipStatusImage,
            capitalShipOrder,
            capitalShipTaskForce,
            capitalShipFormation
        );
        view.ConfigureSuperlaser(
            superlaserPanel,
            superlaserButton,
            superlaserButtonImage,
            superlaserChargeBar
        );
        capitalShipStatusPanel.SetActive(false);
        missionOrderPanel.SetActive(false);
        maneuverPanel.SetActive(false);
        gameOptionsPanel.SetActive(false);
        withdrawalPanel.SetActive(false);
        superlaserPanel.SetActive(false);
    }

    /// <summary>
    /// Creates the nine original camera controls at their exact source rectangles.
    /// </summary>
    /// <param name="parent">The tactical HUD transform.</param>
    /// <param name="root">The configured shared tactical UI root.</param>
    /// <returns>The nine controls in source command order.</returns>
    private static Button[] CreateCameraControls(Transform parent, string root)
    {
        return new[]
        {
            CreateCameraButton("ZoomIn", parent, root, "zoom-in", 491, 343, 24, 24),
            CreateCameraButton("ZoomOut", parent, root, "zoom-out", 607, 343, 24, 24),
            CreateCameraButton("RotateLeft", parent, root, "rotate-left", 497, 381, 42, 43),
            CreateCameraButton("RotateRight", parent, root, "rotate-right", 582, 381, 43, 43),
            CreateCameraButton("TiltUp", parent, root, "tilt-up", 539, 338, 43, 43),
            CreateCameraButton("TiltDown", parent, root, "tilt-down", 540, 424, 43, 43),
            CreateCameraButton(
                "RememberPosition",
                parent,
                root,
                "remember-position",
                594,
                444,
                43,
                25
            ),
            CreateCameraButton("ResetView", parent, root, "reset-view", 486, 444, 43, 25),
            CreateCameraButton("ResetSubject", parent, root, "reset-subject", 549, 391, 23, 23),
        };
    }

    /// <summary>
    /// Creates one camera button using the shared paired-artwork convention.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The tactical HUD transform.</param>
    /// <param name="root">The configured shared tactical UI root.</param>
    /// <param name="assetName">The shared camera asset stem.</param>
    /// <param name="x">The source-space left edge.</param>
    /// <param name="y">The source-space top edge.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static Button CreateCameraButton(
        string name,
        Transform parent,
        string root,
        string assetName,
        int x,
        int y,
        int width,
        int height
    )
    {
        return CreateBoundButton(
            name,
            parent,
            $"{root}/Camera/{assetName}-up",
            $"{root}/Camera/{assetName}-down",
            x,
            y,
            width,
            height
        );
    }

    /// <summary>
    /// Creates the UI event dispatcher required by tactical controls.
    /// </summary>
    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    /// <summary>
    /// Creates the centered, aspect-preserving surface used by the original tactical layout.
    /// </summary>
    /// <param name="parent">The full-screen tactical canvas.</param>
    /// <returns>The 640 by 480 control surface.</returns>
    private static Transform CreateControlSurface(Transform parent)
    {
        GameObject surface = new GameObject("ControlSurface", typeof(RectTransform));
        surface.transform.SetParent(parent, false);
        RectTransform rect = surface.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(640f, 480f);
        return surface.transform;
    }

    /// <summary>
    /// Creates the four control-panel pieces surrounding the unobstructed battle viewport.
    /// </summary>
    /// <param name="parent">The tactical control surface.</param>
    /// <param name="address">The complete control-panel texture address.</param>
    private static void CreateControlPanelFrame(Transform parent, string address)
    {
        CreateBoundImageSegment("ControlPanelTop", parent, address, 0, 0, 640, 30);
        CreateBoundImageSegment("ControlPanelLeft", parent, address, 0, 30, 17, 438);
        CreateBoundImageSegment("ControlPanelRight", parent, address, 457, 30, 183, 438);
        CreateBoundImageSegment("ControlPanelBottom", parent, address, 0, 468, 640, 12);
    }

    /// <summary>
    /// Creates the source-sized rectangle into which the tactical camera renders.
    /// </summary>
    /// <param name="parent">The tactical control surface.</param>
    /// <returns>The camera viewport rectangle.</returns>
    private static RectTransform CreateViewport(Transform parent)
    {
        GameObject target = new GameObject("BattleViewport", typeof(RectTransform));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        SetSourceRect(rect, 17, 30, 440, 438);
        return rect;
    }

    /// <summary>
    /// Creates one cropped piece of the complete tactical control-panel texture.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The tactical control surface.</param>
    /// <param name="address">The complete control-panel texture address.</param>
    /// <param name="x">The source-space left edge.</param>
    /// <param name="y">The source-space top edge.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void CreateBoundImageSegment(
        string name,
        Transform parent,
        string address,
        int x,
        int y,
        int width,
        int height
    )
    {
        RawImage image = CreateBoundImage(name, parent, address, x, y, width, height);
        image.uvRect = new Rect(x / 640f, 1f - (y + height) / 480f, width / 640f, height / 480f);
    }

    /// <summary>
    /// Resolves the active scenario's default player-faction theme for scene authoring.
    /// </summary>
    /// <returns>The configured tactical battle theme.</returns>
    private static TacticalBattleTheme GetPreviewTheme()
    {
        ContentPack pack = ContentPackEditor.LoadActivePack();
        string factionId = pack.Scenario.DefaultPlayerFactionID;
        FactionTheme theme = pack.GameData.FactionThemes.FirstOrDefault(candidate =>
            string.Equals(
                candidate?.FactionInstanceID,
                factionId,
                StringComparison.OrdinalIgnoreCase
            )
        );
        return theme?.TacticalBattle
            ?? throw new InvalidOperationException(
                $"Faction '{factionId}' requires a tactical battle theme."
            );
    }

    /// <summary>
    /// Creates one content-bound image in top-left source coordinates.
    /// </summary>
    private static RawImage CreateBoundImage(
        string name,
        Transform parent,
        string address,
        int x,
        int y,
        int width,
        int height
    )
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        target.transform.SetParent(parent, false);
        RawImage image = target.AddComponent<RawImage>();
        image.texture = ContentPackEditor.Assets.GetTexture(address);
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
        target.AddComponent<ContentTextureBinding>().SetAddress(address);
        return image;
    }

    /// <summary>
    /// Creates one runtime-populated image in top-left source coordinates.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="x">The source-space left edge.</param>
    /// <param name="y">The source-space top edge.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The generated raw image.</returns>
    private static RawImage CreateImage(
        string name,
        Transform parent,
        int x,
        int y,
        int width,
        int height
    )
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        target.transform.SetParent(parent, false);
        RawImage image = target.AddComponent<RawImage>();
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Creates one content-bound tactical button with its original released and pressed artwork.
    /// </summary>
    private static Button CreateBoundButton(
        string name,
        Transform parent,
        string upAddress,
        string downAddress,
        int x,
        int y,
        int width,
        int height
    )
    {
        RawImage image = CreateBoundImage(name, parent, upAddress, x, y, width, height);
        ContentTextureBinding textureBinding = image.GetComponent<ContentTextureBinding>();
        UnityEngine.Object.DestroyImmediate(textureBinding);
        Button button = CreateButton(image);
        RawImagePressVisual visual = image.gameObject.AddComponent<RawImagePressVisual>();
        AssignReference(visual, "image", image);
        AssignReference(visual, "button", button);
        visual.SetTextures(image.texture, ContentPackEditor.Assets.GetTexture(downAddress));
        image
            .gameObject.AddComponent<ContentPressVisualBinding>()
            .SetAddresses(upAddress, downAddress);
        return button;
    }

    /// <summary>
    /// Creates a faction-specific tactical button whose runtime artwork is resolved by the view.
    /// </summary>
    private static Button CreatePreviewButton(
        string name,
        Transform parent,
        string upAddress,
        string downAddress,
        int x,
        int y,
        int width,
        int height
    )
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        target.transform.SetParent(parent, false);
        RawImage image = target.AddComponent<RawImage>();
        image.texture = ContentPackEditor.Assets.GetTexture(upAddress);
        image.raycastTarget = true;
        SetSourceRect(image.rectTransform, x, y, width, height);
        Button button = CreateButton(image);
        RawImagePressVisual visual = target.AddComponent<RawImagePressVisual>();
        AssignReference(visual, "image", image);
        AssignReference(visual, "button", button);
        visual.SetTextures(image.texture, ContentPackEditor.Assets.GetTexture(downAddress));
        return button;
    }

    /// <summary>
    /// Adds a non-transitioning button to an authored raw image.
    /// </summary>
    private static Button CreateButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    /// <summary>
    /// Creates one left-to-right status bar in source coordinates.
    /// </summary>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="parent">The parent status panel.</param>
    /// <param name="color">The filled bar color.</param>
    /// <param name="x">The source-space left edge.</param>
    /// <param name="y">The source-space top edge.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The generated filled image.</returns>
    private static Image CreateStatusBar(
        string name,
        Transform parent,
        Color color,
        int x,
        int y,
        int width,
        int height
    )
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        target.transform.SetParent(parent, false);
        Image image = target.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Creates one source-positioned status line for the selected capital ship.
    /// </summary>
    /// <param name="name">The generated object name.</param>
    /// <param name="parent">The selected-ship panel transform.</param>
    /// <param name="x">The source-space left edge.</param>
    /// <param name="y">The source-space top edge.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The generated text field.</returns>
    private static TextMeshProUGUI CreateStatusText(
        string name,
        Transform parent,
        int x,
        int y,
        int width,
        int height
    )
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 8;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        SetSourceRect(text.rectTransform, x, y, width, height);
        return text;
    }

    /// <summary>
    /// Applies one original top-left rectangle without resampling its coordinates.
    /// </summary>
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
    /// Assigns one private serialized component reference during scene generation.
    /// </summary>
    private static void AssignReference(
        Component component,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(component.GetType().Name, propertyName);

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Creates the tactical presentation camera.
    /// </summary>
    /// <param name="parent">The tactical scene root.</param>
    /// <returns>The generated camera rig.</returns>
    private static TacticalCameraRig CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("TacticalCamera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 10000f;
        camera.fieldOfView = 45f;
        cameraObject.AddComponent<AudioListener>();
        return cameraObject.AddComponent<TacticalCameraRig>();
    }

    /// <summary>
    /// Creates the primary tactical scene light.
    /// </summary>
    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("TacticalKeyLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
    }

    /// <summary>
    /// Adds the generated scene to the enabled player-build scene list once.
    /// </summary>
    private static void EnableBuildScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => string.Equals(scene.path, ScenePath, StringComparison.Ordinal)))
            return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
            .ToArray();
    }
}
