using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class StrategyViewPrefabBuilder
{
    private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";
    private const string _legacyArtAssetRoot = "Assets/Resources/Art/UI/";
    private const string _originalArtAssetRoot = "Assets/Resources/Art/Original/UI/";
    private const string _legacyArtResourceRoot = "Art/UI/";
    private const string _originalArtResourceRoot = "Art/Original/UI/";
    private const string _planetSystemWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/PlanetSystemWindow.prefab";
    private const string _planetSystemPlanetPrefabPath =
        "Assets/Prefabs/UI/StrategyView/PlanetSystemPlanet.prefab";
    private const string _confirmDialogWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/ConfirmDialogWindow.prefab";
    private const string _facilityWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/FacilityWindow.prefab";
    private const string _constructionWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/ConstructionWindow.prefab";
    private const string _defenseWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/DefenseWindow.prefab";
    private const string _fleetWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/FleetWindow.prefab";
    private const string _missionsWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/MissionsWindow.prefab";
    private const string _missionCreateWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/MissionCreateWindow.prefab";
    private const string _saveMenuWindowPrefabPath = "Assets/Prefabs/UI/SaveMenuWindow.prefab";
    private const string _statusWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/StatusWindow.prefab";
    private const string _finderWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/FinderWindow.prefab";
    private const string _messagesWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";
    private const string _encyclopediaWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/EncyclopediaWindow.prefab";
    private const string _planetSystemClusterPrefabPath =
        "Assets/Prefabs/UI/StrategyView/PlanetSystemCluster.prefab";
    private const string _strategyScenePath = "Assets/Scenes/StrategyView.unity";
    private const string _strategySceneRootParentPath = "GameRoot/UI/Canvas";
    private const string _sceneInstanceName = "StrategyViewRoot";
    private const string _surfaceName = "Viewport";
    private const string _galaxyMapName = "GalaxyMap";
    private const string _hudName = "StrategyHud";
    private const string _hudBackgroundImageName = "BackgroundImage";
    private const string _hudTextFieldsName = "HudTextFields";
    private const string _hudTickTextFieldName = "TickTextField";
    private const string _hudRawMaterialsTextFieldName = "RawMaterialsTextField";
    private const string _hudRefinedMaterialsTextFieldName = "RefinedMaterialsTextField";
    private const string _hudMaintenanceTextFieldName = "MaintenanceTextField";
    private const string _windowLayerName = "Windows";
    private const string _normalWindowLayerName = "NormalWindows";
    private const string _modalWindowLayerName = "ModalWindows";
    private const string _modalInputBlockerName = "ModalInputBlocker";
    private const string _overlayLayerName = "Overlay";
    private const string _contextMenuName = "ContextMenu";
    private const string _contextMenuPanelTemplateName = "PanelTemplate";
    private const string _contextMenuCommandTemplateName = "CommandTemplate";
    private const string _galaxyBackgroundImageName = "BackgroundImage";
    private const string _planetSystemClustersName = "PlanetSystemClusters";
    private const string _galaxyStarPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_galaxy_star_preview.png";
    private const string _planetPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_planetsystem_planet_preview.png";
    private const string _windowOpenSectorPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_open_sector_button.png";
    private const string _windowOpenSectorDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_open_sector_button_pressed.png";
    private const string _windowSwapPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_swap_button.png";
    private const string _windowClosePreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_close_button.png";
    private const string _windowCloseDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_close_button_pressed.png";
    private const string _windowMinimizePreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_minimize_button.png";
    private const string _windowMinimizeDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_window_minimize_button_pressed.png";
    private const string _confirmButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_confirm_ok_button.png";
    private const string _cancelButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_confirm_cancel_button.png";
    private const string _facilityWindowBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_window_background.png";
    private const string _facilityWindowTabPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_tab.png";
    private const string _facilityManufacturingStripPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_manufacturing_strip.png";
    private const string _facilityManufacturingCardPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_manufacturing_lane_card.png";
    private const string _facilityManufacturingCardStatePreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_manufacturing_lane_state.png";
    private const string _facilityManufacturingSelectPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_manufacturing_selection.png";
    private const string _facilityInventoryItemPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_facility_inventory_item.png";
    private const string _facilityCardEntityPreviewPath =
        "Assets/Resources/Art/UI/Units/ent_building_ship_yard.png";
    private const string _facilityCardEntitySmallPreviewPath =
        "Assets/Resources/Art/UI/Units/ent_building_ship_yard_small.png";
    private const string _constructionWindowBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_window_background.png";
    private const string _constructionOpenButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_open_button.png";
    private const string _constructionOpenButtonDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_open_button_pressed.png";
    private const string _constructionInfoButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_info_button.png";
    private const string _constructionInfoButtonDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_info_button_pressed.png";
    private const string _constructionOkButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_ok_button.png";
    private const string _constructionOkButtonDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_ok_button_pressed.png";
    private const string _constructionOkButtonDisabledPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_ok_button_disabled.png";
    private const string _constructionCancelButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_cancel_button.png";
    private const string _constructionCancelButtonDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_cancel_button_pressed.png";
    private const string _constructionIncrementButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_increment_button.png";
    private const string _constructionDecrementButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_decrement_button.png";
    private const string _constructionDropdownBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_construction_dropdown_background.png";
    private const string _defenseWindowBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_defense_window_background.png";
    private const string _defenseWindowTabPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_defense_tab.png";
    private const string _defenseSelectionPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_defense_selection.png";
    private const string _defensePersonnelBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_defense_personnel_background.png";
    private const string _defenseEnrouteBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_defense_window_enroute_background.png";
    private const string _fleetWindowBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_fleet_window_background.png";
    private const string _fleetIconPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_fleet_window_icon.png";
    private const string _fleetSelectionPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_fleet_selection.png";
    private const string _fleetShipSelectionPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_fleet_ship_selection.png";
    private const string _fleetPersonnelEnrouteBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_defense_window_enroute_background.png";
    private const string _fleetDetailBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_fleet_detail_background.png";
    private const string _fleetTabPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_fleet_tab.png";
    private const string _missionsWindowBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missions_window_background.png";
    private const string _missionsTabPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missions_tab.png";
    private const string _missionCreateMissionBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missioncreate_mission_background.png";
    private const string _missionCreatePersonnelBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missioncreate_personnel_background.png";
    private const string _missionCreateMoveRightButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missioncreate_move_right_button.png";
    private const string _missionCreateMoveRightButtonDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missioncreate_move_right_button_pressed.png";
    private const string _missionCreateMoveLeftButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missioncreate_move_left_button.png";
    private const string _missionCreateMoveLeftButtonDownPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_missioncreate_move_left_button_pressed.png";
    private const string _saveMenuBackgroundPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_background.png";
    private const string _saveMenuCockpitButtonPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_cockpit_button.png";
    private const string _saveMenuAirlockButtonPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_airlock_button.png";
    private const string _saveMenuMusicButtonPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_music_button.png";
    private const string _saveMenuMusicButtonDownPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_music_button_pressed.png";
    private const string _saveMenuOptionButtonPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_option_button.png";
    private const string _saveMenuOptionButtonDownPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_option_button_pressed.png";
    private const string _saveMenuSaveButtonPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_save_button.png";
    private const string _saveMenuSaveButtonDownPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_save_button_pressed.png";
    private const string _saveMenuSaveButtonDisabledPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_save_button_disabled.png";
    private const string _saveMenuLoadButtonPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_load_button.png";
    private const string _saveMenuLoadButtonDownPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_load_button_pressed.png";
    private const string _saveMenuLoadButtonDisabledPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_load_button_disabled.png";
    private const string _saveMenuSliderPreviewPath =
        "Assets/Resources/Art/UI/SaveMenu/ui_savemenu_slider_thumb.png";
    private const int _saveMenuSliderWidth = 189;
    private const string _statusWindowBackgroundPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_status_window_background.png";
    private const string _statusInfoButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_status_info_button.png";
    private const string _statusInfoButtonDisabledPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_status_info_button_disabled.png";
    private const string _statusCloseButtonPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_status_close_button.png";
    private const string _scrollUpArrowPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_scrollbar_arrow_up.png";
    private const string _scrollDownArrowPreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_scrollbar_arrow_pressed_2.png";
    private const string _scrollBarMiddlePreviewPath =
        "Assets/Resources/Art/UI/StrategyView/ui_strategyview_scrollbar_middle.png";
    private const int _screenWidth = 640;
    private const int _screenHeight = 481;
    private const int _contextMenuIconPreviewSize = 14;
    private const int _speedContextMenuWidth = 60;
    private const int _facilityContextMenuWidth = 89;
    private const int _planetSystemContextMenuWidth = 130;
    private const int _defenseContextMenuWidth = 110;
    private const int _missionsContextMenuWidth = 100;
    private const int _fallbackContextMenuWidth = 100;
    private const int _fleetContextMenuWidth = 100;
    private const int _fleetBombardmentContextMenuWidth = 140;
    private const int _destinationCursorSize = 22;
    private const int _destinationCursorRadius = 8;
    private const int _defaultGalaxyBackgroundX = 52;
    private const int _defaultGalaxyBackgroundY = 25;
    private const int _sectorLeftOpenThresholdOffset = -15;
    private const int _sectorRightOpenThresholdOffset = -3;
    private const int _constructionWindowOffsetX = 15;
    private const int _constructionWindowOffsetY = 25;
    private const float _sectorCoordinateRange = 1024f;
    private const float _sectorCoordinateScaleX = 13f;
    private const float _sectorCoordinateScaleY = 10f;
    private const int _planetPreviewWidth = 37;
    private static readonly Color32 _sectorWindowBackgroundOverlay = new Color32(57, 57, 57, 230);

    private static FactionThemeLibrary _previewThemeLibrary;

    private static FactionTheme PreviewTheme => GetPreviewTheme(0);

    private static IReadOnlyList<FactionTheme> PreviewThemes
    {
        get
        {
            _previewThemeLibrary ??= new FactionThemeLibrary();
            return _previewThemeLibrary.GetAllThemes();
        }
    }

    private static FactionTheme GetPreviewTheme(int index)
    {
        IReadOnlyList<FactionTheme> themes = PreviewThemes;
        if (themes.Count == 0)
            return null;

        return themes[Mathf.Clamp(index, 0, themes.Count - 1)];
    }

    private static string GetPreviewHudButtonImagePath(int action)
    {
        List<StrategyHudButtonTheme> buttons = PreviewTheme?.TacticalHUDLayout?.Buttons;
        if (buttons == null)
            return null;

        foreach (StrategyHudButtonTheme button in buttons)
        {
            if (button?.Action == action)
                return button.PressedImagePath;
        }

        return null;
    }

    private static List<StrategyHudButtonView> CreateHudButtonViews(Transform parent)
    {
        List<StrategyHudButtonView> views = new List<StrategyHudButtonView>();
        List<StrategyHudButtonTheme> buttons = PreviewTheme?.TacticalHUDLayout?.Buttons;
        if (buttons == null)
            return views;

        for (int i = 0; i < buttons.Count; i++)
            views.Add(CreateHudButtonView($"HudButton{i}", parent, buttons[i]?.HitArea));

        return views;
    }

    private static List<RawImage> CreateHudMessageNotificationImages(Transform parent)
    {
        List<RawImage> images = new List<RawImage>();
        List<StrategyHudMessageNotificationTheme> notifications = PreviewTheme
            ?.TacticalHUDLayout
            ?.MessageNotifications;
        if (notifications == null)
            return images;

        for (int i = 0; i < notifications.Count; i++)
        {
            StrategyHudMessageNotificationTheme notification = notifications[i];
            SourceRectLayout layout = notification?.SourceLayout;
            RawImage image = CreateRawButton(
                $"MessageNotification{i}",
                parent,
                notification?.DefaultImagePath
            );
            SetSourceRect(
                image.rectTransform,
                layout?.X ?? 0,
                layout?.Y ?? 0,
                layout?.Width ?? 1,
                layout?.Height ?? 1
            );
            images.Add(image);
        }

        return images;
    }

    private static StrategyHudButtonView CreateHudButtonView(
        string name,
        Transform parent,
        SourceRectLayout hitArea
    )
    {
        GameObject button = new GameObject(
            name,
            typeof(RectTransform),
            typeof(StrategyHudButtonView)
        );
        button.transform.SetParent(parent, false);
        StrategyHudButtonView view = button.GetComponent<StrategyHudButtonView>();
        view.enabled = true;
        SetSourceRect(
            button.GetComponent<RectTransform>(),
            hitArea?.X ?? 0,
            hitArea?.Y ?? 0,
            hitArea?.Width ?? 1,
            hitArea?.Height ?? 1
        );

        RawImage hitAreaImage = CreatePanelImage(
            "HitAreaImage",
            button.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitAreaImage.rectTransform, 0, 0, hitArea?.Width ?? 1, hitArea?.Height ?? 1);
        AssignReference(view, "hitAreaImage", hitAreaImage);
        return view;
    }

    private static BookmarkSlotView CreateBookmarkSlotTemplate(
        Transform parent,
        StrategyBookmarkLayout layout
    )
    {
        GameObject slot = new GameObject(
            "BookmarkSlotTemplate",
            typeof(RectTransform),
            typeof(BookmarkSlotView)
        );
        slot.transform.SetParent(parent, false);
        BookmarkSlotView view = slot.GetComponent<BookmarkSlotView>();
        SetSourceRect(
            slot.GetComponent<RectTransform>(),
            layout.StartX,
            layout.StartY,
            layout.Width,
            layout.ItemHeight
        );

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            slot.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitArea.rectTransform, 0, 0, layout.Width, layout.ItemHeight);
        RawImage icon = CreateRawImage(
            "IconImage",
            slot.transform,
            PreviewTheme?.StrategyBookmarkIcons?.FacilityImagePath,
            0,
            0
        );
        SetSourceRect(
            icon.rectTransform,
            0,
            Mathf.Max(0, (layout.ItemHeight - layout.IconHeight) / 2),
            layout.IconWidth,
            layout.IconHeight
        );
        TextMeshProUGUI label = CreateTextLabel("LabelTextField", slot.transform);
        label.text = "Corellia";
        label.color = Color.yellow;
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(
            label.rectTransform,
            layout.LabelOffsetX,
            0,
            layout.Width - layout.LabelOffsetX,
            layout.ItemHeight
        );

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "iconImage", icon);
        AssignReference(view, "labelTextField", label);
        slot.SetActive(false);
        return view;
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Strategy View Root Prefab")]
    public static void BuildStrategyViewRootPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_prefabPath));
        PlanetSystemWindowView planetSystemWindowPrefab = LoadWindowPrefab<PlanetSystemWindowView>(
            _planetSystemWindowPrefabPath
        );
        ConfirmDialogWindowView confirmDialogWindowPrefab =
            LoadWindowPrefab<ConfirmDialogWindowView>(_confirmDialogWindowPrefabPath);
        FacilityWindowView facilityWindowPrefab = LoadWindowPrefab<FacilityWindowView>(
            _facilityWindowPrefabPath
        );
        ConstructionWindowView constructionWindowPrefab = LoadWindowPrefab<ConstructionWindowView>(
            _constructionWindowPrefabPath
        );
        DefenseWindowView defenseWindowPrefab = LoadWindowPrefab<DefenseWindowView>(
            _defenseWindowPrefabPath
        );
        FleetWindowView fleetWindowPrefab = LoadWindowPrefab<FleetWindowView>(
            _fleetWindowPrefabPath
        );
        MissionsWindowView missionsWindowPrefab = LoadWindowPrefab<MissionsWindowView>(
            _missionsWindowPrefabPath
        );
        MissionCreateWindowView missionCreateWindowPrefab =
            LoadWindowPrefab<MissionCreateWindowView>(_missionCreateWindowPrefabPath);
        StatusWindowView statusWindowPrefab = LoadWindowPrefab<StatusWindowView>(
            _statusWindowPrefabPath
        );
        FinderWindowView finderWindowPrefab = LoadWindowPrefab<FinderWindowView>(
            _finderWindowPrefabPath
        );
        MessagesWindowView messagesWindowPrefab = LoadWindowPrefab<MessagesWindowView>(
            _messagesWindowPrefabPath
        );
        EncyclopediaWindowView encyclopediaWindowPrefab = LoadWindowPrefab<EncyclopediaWindowView>(
            _encyclopediaWindowPrefabPath
        );
        PlanetSystemClusterView planetSystemClusterPrefab =
            LoadPrefabComponent<PlanetSystemClusterView>(_planetSystemClusterPrefabPath);

        GameObject root = new GameObject(_sceneInstanceName, typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        FillParent(rootRect);

        StrategyController controller = root.AddComponent<StrategyController>();
        root.AddComponent<GameFlowController>();

        GameObject surface = CreateLayer(_surfaceName, root.transform);
        RectTransform surfaceRect = surface.GetComponent<RectTransform>();
        SetStrategySurfaceRect(surfaceRect);
        RawImage surfaceImage = surface.AddComponent<RawImage>();
        surfaceImage.enabled = true;
        surfaceImage.color = Color.clear;
        surfaceImage.raycastTarget = true;

        GameObject galaxyMap = CreateLayer(_galaxyMapName, root.transform);
        RectTransform galaxyMapRect = galaxyMap.GetComponent<RectTransform>();
        SetStrategySurfaceRect(galaxyMapRect);
        GalaxyMapView galaxyMapView = galaxyMap.AddComponent<GalaxyMapView>();

        RectTransform background = CreateChildLayer(
            _galaxyBackgroundImageName,
            galaxyMap.transform
        );
        RawImage backgroundImage = background.gameObject.AddComponent<RawImage>();
        backgroundImage.raycastTarget = false;
        Texture2D backgroundTexture = LoadTexture(PreviewTheme?.GalaxyBackground?.ImagePath);
        backgroundImage.texture = backgroundTexture;
        if (backgroundTexture != null)
            SetSourceRect(
                background,
                _defaultGalaxyBackgroundX,
                _defaultGalaxyBackgroundY,
                backgroundTexture.width,
                backgroundTexture.height
            );

        RectTransform planetSystemClusters = CreateChildLayer(
            _planetSystemClustersName,
            galaxyMap.transform
        );

        GameObject bookmarks = CreateLayer("Bookmarks", root.transform);
        RectTransform bookmarksRect = bookmarks.GetComponent<RectTransform>();
        SetStrategySurfaceRect(bookmarksRect);
        BookmarkBarView bookmarkBarView = bookmarks.AddComponent<BookmarkBarView>();
        StrategyBookmarkLayout bookmarkLayout =
            PreviewTheme?.StrategyBookmarkLayout
            ?? throw new MissingReferenceException("Preview StrategyBookmarkLayout is missing.");
        BookmarkSlotView bookmarkSlotTemplate = CreateBookmarkSlotTemplate(
            bookmarks.transform,
            bookmarkLayout
        );

        GameObject hud = CreateLayer(_hudName, root.transform);
        bookmarks.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);
        RectTransform hudRect = hud.GetComponent<RectTransform>();
        SetStrategySurfaceRect(hudRect);
        StrategyHudView hudView = hud.AddComponent<StrategyHudView>();

        RectTransform hudBackground = CreateChildLayer(_hudBackgroundImageName, hud.transform);
        RawImage hudBackgroundImage = hudBackground.gameObject.AddComponent<RawImage>();
        hudBackgroundImage.texture = LoadTexture(PreviewTheme?.TacticalHUDLayout?.ImagePath);
        hudBackgroundImage.enabled = hudBackgroundImage.texture != null;
        hudBackgroundImage.raycastTarget = false;

        RectTransform hudTextFields = CreateChildLayer(_hudTextFieldsName, hud.transform);
        TextMeshProUGUI tickLabel = CreateHudLabel(
            _hudTickTextFieldName,
            "0",
            hudTextFields,
            96,
            20,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI rawMaterialsLabel = CreateHudLabel(
            _hudRawMaterialsTextFieldName,
            "0",
            hudTextFields,
            246,
            20,
            TextAlignmentOptions.TopRight
        );
        TextMeshProUGUI refinedMaterialsLabel = CreateHudLabel(
            _hudRefinedMaterialsTextFieldName,
            "0",
            hudTextFields,
            340,
            20,
            TextAlignmentOptions.TopRight
        );
        TextMeshProUGUI maintenanceLabel = CreateHudLabel(
            _hudMaintenanceTextFieldName,
            "0",
            hudTextFields,
            440,
            20,
            TextAlignmentOptions.TopRight
        );
        RawImage speedIndicatorImage = CreateRawImage(
            "SpeedIndicatorImage",
            hud.transform,
            PreviewTheme?.TacticalHUDLayout?.SpeedIndicators?.MediumImagePath,
            167,
            20
        );
        RawImage pressedMainButtonImage = CreateRawImage(
            "PressedMainButtonImage",
            hud.transform,
            GetPreviewHudButtonImagePath(StrategyHudActions.Options),
            3,
            355
        );
        pressedMainButtonImage.gameObject.SetActive(false);
        List<RawImage> messageNotificationImages = CreateHudMessageNotificationImages(
            hud.transform
        );
        List<Button> messageNotificationButtons = CreateButtons(messageNotificationImages);
        List<StrategyHudButtonView> hudButtonViews = CreateHudButtonViews(hud.transform);
        StrategyHudButtonView speedContextView = CreateHudButtonView(
            "SpeedContextView",
            hud.transform,
            PreviewTheme?.TacticalHUDLayout?.SpeedContextSourceLayout
        );

        GameObject windows = CreateLayer(_windowLayerName, root.transform);
        RectTransform windowsRect = windows.GetComponent<RectTransform>();
        SetStrategySurfaceRect(windowsRect);
        UIWindowManager windowManager = windows.AddComponent<UIWindowManager>();
        StrategyWindowLayerView windowsView = windows.AddComponent<StrategyWindowLayerView>();
        RectTransform normalWindowLayer = CreateChildLayer(
            _normalWindowLayerName,
            windows.transform
        );
        RectTransform modalWindowLayer = CreateChildLayer(_modalWindowLayerName, windows.transform);
        RawImage modalInputBlocker = CreatePanelImage(
            _modalInputBlockerName,
            modalWindowLayer,
            Color.clear
        );
        FillParent(modalInputBlocker.rectTransform);
        modalInputBlocker.raycastTarget = true;
        modalInputBlocker.gameObject.SetActive(false);
        StrategyOverlayView overlayView = CreateStrategyOverlayView(root.transform);

        StrategyContextMenuPresenter contextMenu = CreateContextMenu(root.transform);

        AssignReference(controller, "strategySurface", surfaceRect);
        AssignReference(controller, "strategySurfaceImage", surfaceImage);
        AssignReference(controller, "strategyHud", hudView);
        AssignReference(controller, "strategyContextMenu", contextMenu);
        AssignReference(controller, "strategyOverlay", overlayView);
        AssignReference(controller, "strategyWindowLayer", windowsRect);
        AssignReference(controller, "strategyWindowLayerView", windowsView);
        AssignReference(windowsView, "windowManager", windowManager);
        AssignReference(windowsView, "normalWindowLayer", normalWindowLayer);
        AssignReference(windowsView, "modalWindowLayer", modalWindowLayer);
        AssignReference(windowsView, "modalInputBlockerImage", modalInputBlocker);
        AssignReference(controller, "galaxyMap", galaxyMapView);
        AssignReference(controller, "bookmarkBar", bookmarkBarView);
        AssignReference(bookmarkBarView, "slotTemplate", bookmarkSlotTemplate);
        AssignWindowPrefabs(
            windowsView,
            planetSystemWindowPrefab,
            facilityWindowPrefab,
            defenseWindowPrefab,
            fleetWindowPrefab,
            missionsWindowPrefab,
            constructionWindowPrefab,
            missionCreateWindowPrefab,
            statusWindowPrefab,
            messagesWindowPrefab,
            confirmDialogWindowPrefab,
            finderWindowPrefab,
            encyclopediaWindowPrefab
        );
        AssignWindowLayerLayout(windowsView);
        AssignReference(hudView, "backgroundImage", hudBackgroundImage);
        AssignReference(hudView, "tickTextField", tickLabel);
        AssignReference(hudView, "rawMaterialsTextField", rawMaterialsLabel);
        AssignReference(hudView, "refinedMaterialsTextField", refinedMaterialsLabel);
        AssignReference(hudView, "maintenanceTextField", maintenanceLabel);
        AssignReference(hudView, "speedIndicatorImage", speedIndicatorImage);
        AssignReference(hudView, "pressedMainButtonImage", pressedMainButtonImage);
        AssignReferenceArray(hudView, "messageNotificationImages", messageNotificationImages);
        AssignReferenceArray(hudView, "messageNotificationButtons", messageNotificationButtons);
        AssignReferenceArray(hudView, "buttonViews", hudButtonViews);
        AssignReference(hudView, "speedContextView", speedContextView);
        AssignReference(galaxyMapView, "background", background);
        AssignReference(galaxyMapView, "backgroundImage", backgroundImage);
        AssignReference(galaxyMapView, "planetSystemClusters", planetSystemClusters);
        AssignReference(galaxyMapView, "planetSystemClusterPrefab", planetSystemClusterPrefab);

        SaveGeneratedPrefabAsset(root, _prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Planet System Prefabs")]
    public static void BuildPlanetSystemPrefabs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_planetSystemPlanetPrefabPath));

        PlanetSystemPlanetView planetPrefab = BuildPlanetSystemPlanetPrefab();
        BuildPlanetSystemWindowPrefab(planetPrefab);
        RegisterWindowPrefabsInRoot();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Planet System Cluster Prefab")]
    public static void RebuildPlanetSystemClusterPrefab()
    {
        BuildPlanetSystemClusterPrefab();
    }

    [MenuItem("Rebellion/Strategy View/Refresh Strategy View Root Window Prefabs")]
    public static void RefreshStrategyViewRootWindowPrefabs()
    {
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Confirm Dialog Window Prefab")]
    public static void RebuildConfirmDialogWindowPrefab()
    {
        BuildConfirmDialogWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Facility Window Prefab")]
    public static void RebuildFacilityWindowPrefab()
    {
        BuildFacilityWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Construction Window Prefab")]
    public static void RebuildConstructionWindowPrefab()
    {
        BuildConstructionWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Defense Window Prefab")]
    public static void RebuildDefenseWindowPrefab()
    {
        BuildDefenseWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Fleet Window Prefab")]
    public static void RebuildFleetWindowPrefab()
    {
        BuildFleetWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Missions Window Prefab")]
    public static void RebuildMissionsWindowPrefab()
    {
        BuildMissionsWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Mission Create Window Prefab")]
    public static void RebuildMissionCreateWindowPrefab()
    {
        BuildMissionCreateWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Save Menu Window Prefab")]
    public static void RebuildSaveMenuWindowPrefab()
    {
        BuildSaveMenuWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Status Window Prefab")]
    public static void RebuildStatusWindowPrefab()
    {
        BuildStatusWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Finder Window Prefab")]
    public static void RebuildFinderWindowPrefab()
    {
        BuildFinderWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Messages Window Prefab")]
    public static void RebuildMessagesWindowPrefab()
    {
        BuildMessagesWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    [MenuItem("Rebellion/Strategy View/Rebuild Encyclopedia Window Prefab")]
    public static void RebuildEncyclopediaWindowPrefab()
    {
        BuildEncyclopediaWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    private static FacilityWindowView BuildFacilityWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_facilityWindowPrefabPath));
        const int windowWidth = 226;
        const int windowHeight = 304;

        GameObject window = new GameObject(
            "FacilityWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(FacilityWindowView)
        );
        FacilityWindowView view = window.GetComponent<FacilityWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _facilityWindowBackgroundPreviewPath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.transform, windowWidth);
        TextMeshProUGUI caption = CreateTextLabel("CaptionTextField", window.transform);
        caption.text = "Corellia";
        caption.color = Color.black;
        caption.fontSize = 12;
        caption.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(caption.rectTransform, 19, 4, 150, 16);

        RectTransform buttons = CreateSourceRectLayer(
            "Buttons",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> buttonImages = new List<RawImage>
        {
            CreateRawImage("ButtonImage0", buttons, _windowOpenSectorPreviewPath, 3, 3),
            CreateRawImage(
                "ButtonImage1",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3
            ),
            CreateRawImage("ButtonImage2", buttons, _windowClosePreviewPath, windowWidth - 17, 3),
        };
        int[] buttonActions =
        {
            StrategyWindowButtonActions.OpenSector,
            StrategyWindowButtonActions.MinimizeWindow,
            StrategyWindowButtonActions.CloseWindow,
        };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);

        RectTransform tabs = CreateSourceRectLayer(
            "Tabs",
            window.transform,
            windowWidth,
            windowHeight
        );
        tabs.gameObject.AddComponent<RectMask2D>();
        List<RawImage> tabImages = new List<RawImage>();
        for (int i = 0; i < 6; i++)
        {
            RawImage tabImage = CreateRawButton(
                $"TabImage{i}",
                tabs,
                _facilityWindowTabPreviewPath
            );
            SetSourceRect(tabImage.rectTransform, i * 38, 20, 38, 33);
            tabImages.Add(tabImage);
        }
        List<Button> tabButtons = CreateButtons(tabImages);

        RawImage strip = CreateRawImage(
            "ManufacturingStripImage",
            window.transform,
            _facilityManufacturingStripPreviewPath,
            6,
            72
        );

        List<ManufacturingLaneCardView> manufacturingCards = new List<ManufacturingLaneCardView>
        {
            CreateManufacturingLaneCardView(window.transform, "ManufacturingLaneCard0", 56, 120),
            CreateManufacturingLaneCardView(window.transform, "ManufacturingLaneCard1", 138, 201),
            CreateManufacturingLaneCardView(window.transform, "ManufacturingLaneCard2", 220, 281),
        };

        RectTransform inventory = CreateSourceRectLayer(
            "Inventory",
            window.transform,
            windowWidth,
            windowHeight
        );
        TextMeshProUGUI inventoryTitle = CreateTextLabel("InventoryTitleTextField", inventory);
        inventoryTitle.text = "Shipyards";
        inventoryTitle.color = Color.white;
        inventoryTitle.fontSize = 13;
        inventoryTitle.alignment = TextAlignmentOptions.Top;
        SetSourceRect(inventoryTitle.rectTransform, 10, 58, 206, 20);

        FacilityInventoryItemView inventoryItemTemplate = CreateFacilityInventoryItemTemplate(
            inventory
        );

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(
            view,
            "backgroundTexture",
            LoadTexture(_facilityWindowBackgroundPreviewPath)
        );
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(
            view,
            "openSectorButtonUpTexture",
            LoadTexture(_windowOpenSectorPreviewPath)
        );
        AssignReference(
            view,
            "openSectorButtonDownTexture",
            LoadTexture(_windowOpenSectorDownPreviewPath)
        );
        AssignReference(view, "minimizeButtonUpTexture", LoadTexture(_windowMinimizePreviewPath));
        AssignReference(
            view,
            "minimizeButtonDownTexture",
            LoadTexture(_windowMinimizeDownPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(view, "closeButtonDownTexture", LoadTexture(_windowCloseDownPreviewPath));
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignFacilityWindowTabTextures(view);
        AssignReference(view, "manufacturingStripImage", strip);
        AssignReference(
            view,
            "manufacturingStripTexture",
            LoadTexture(_facilityManufacturingStripPreviewPath)
        );
        AssignReferenceArray(view, "manufacturingCardViews", manufacturingCards);
        AssignReference(view, "inventoryRoot", inventory);
        AssignReference(view, "inventoryTitleTextField", inventoryTitle);
        AssignReference(view, "inventoryItemTemplate", inventoryItemTemplate);
        GameObject saved = SaveGeneratedPrefabAsset(window, _facilityWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<FacilityWindowView>();
    }

    private static void AssignFacilityWindowTabTextures(FacilityWindowView view)
    {
        AssignReference(
            view,
            "shipyardTabActiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_shipyard_tab_active")
        );
        AssignReference(
            view,
            "shipyardTabInactiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_shipyard_tab_inactive")
        );
        AssignReference(
            view,
            "shipyardTabDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_shipyard_tab_disabled")
        );
        AssignReference(
            view,
            "troopTabActiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_troop_tab_active")
        );
        AssignReference(
            view,
            "troopTabInactiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_troop_tab_inactive")
        );
        AssignReference(
            view,
            "troopTabDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_troop_tab_disabled")
        );
        AssignReference(
            view,
            "constructionTabActiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_construction_tab_active")
        );
        AssignReference(
            view,
            "constructionTabInactiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_construction_tab_inactive")
        );
        AssignReference(
            view,
            "constructionTabDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_construction_tab_disabled")
        );
        AssignReference(
            view,
            "refineryTabActiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_refinery_tab_active")
        );
        AssignReference(
            view,
            "refineryTabInactiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_refinery_tab_inactive")
        );
        AssignReference(
            view,
            "refineryTabDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_refinery_tab_disabled")
        );
        AssignReference(
            view,
            "mineTabActiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_mine_tab_active")
        );
        AssignReference(
            view,
            "mineTabInactiveTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_mine_tab_inactive")
        );
        AssignReference(
            view,
            "mineTabDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_facility_window_tab_mine_tab_disabled")
        );
    }

    private static ConstructionWindowView BuildConstructionWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_constructionWindowPrefabPath));
        const int windowWidth = 210;
        const int windowHeight = 260;

        GameObject window = new GameObject(
            "ConstructionWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(ConstructionWindowView)
        );
        ConstructionWindowView view = window.GetComponent<ConstructionWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _constructionWindowBackgroundPreviewPath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.transform, windowWidth);
        TextMeshProUGUI caption = CreateTextLabel("CaptionTextField", window.transform);
        caption.text = "Build Selection";
        caption.color = Color.black;
        caption.fontSize = 12;
        caption.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(caption.rectTransform, 65, 4, 120, 16);

        RectTransform buttons = CreateSourceRectLayer(
            "Buttons",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> buttonImages = new List<RawImage>
        {
            CreateRawImage("ButtonImage0", buttons, _windowClosePreviewPath, windowWidth - 17, 3),
        };
        int[] buttonActions = { StrategyWindowButtonActions.CloseWindow };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);

        RawImage selectedItem = CreateRawImage(
            "SelectedItemImage",
            window.transform,
            _facilityCardEntityPreviewPath,
            0,
            22
        );
        SetSourceRect(selectedItem.rectTransform, 0, 22, 210, 50);
        TextMeshProUGUI selectedName = CreateTextLabel("SelectedNameTextField", window.transform);
        selectedName.text = "Nebulon-B Frigate";
        selectedName.color = Color.white;
        selectedName.fontSize = 12;
        selectedName.alignment = TextAlignmentOptions.Top;
        SetSourceRect(selectedName.rectTransform, 0, 72, 210, 18);

        TextMeshProUGUI buildCountLabel = CreateTextLabel(
            "BuildCountLabelTextField",
            window.transform
        );
        buildCountLabel.text = "Number to build";
        buildCountLabel.color = Color.white;
        buildCountLabel.fontSize = 12;
        buildCountLabel.alignment = TextAlignmentOptions.TopRight;
        SetSourceRect(buildCountLabel.rectTransform, 6, 200, 130, 18);

        TextMeshProUGUI buildCount = CreateTextLabel("BuildCountTextField", window.transform);
        buildCount.text = "1";
        buildCount.color = Color.white;
        buildCount.fontSize = 13;
        buildCount.alignment = TextAlignmentOptions.BottomLeft;
        SetSourceRect(buildCount.rectTransform, 143, 196, 47, 17);

        RawImage increment = CreateRawImage(
            "IncrementButtonImage",
            window.transform,
            _constructionIncrementButtonPreviewPath,
            192,
            196
        );
        Button incrementButton = CreateButton(increment);
        RawImage decrement = CreateRawImage(
            "DecrementButtonImage",
            window.transform,
            _constructionDecrementButtonPreviewPath,
            192,
            205
        );
        Button decrementButton = CreateButton(decrement);

        TextMeshProUGUI constructionCost = CreateTextLabel(
            "ConstructionCostTextField",
            window.transform
        );
        constructionCost.text = "100";
        constructionCost.color = Color.white;
        constructionCost.fontSize = 14;
        constructionCost.alignment = TextAlignmentOptions.Top;
        SetSourceRect(constructionCost.rectTransform, 42, 117, 55, 18);

        TextMeshProUGUI maintenanceCost = CreateTextLabel(
            "MaintenanceCostTextField",
            window.transform
        );
        maintenanceCost.text = "10";
        maintenanceCost.color = Color.white;
        maintenanceCost.fontSize = 14;
        maintenanceCost.alignment = TextAlignmentOptions.Top;
        SetSourceRect(maintenanceCost.rectTransform, 142, 117, 55, 18);

        TextMeshProUGUI completionLabel = CreateTextLabel(
            "CompletionLabelTextField",
            window.transform
        );
        completionLabel.text = "Best Time to Completion";
        completionLabel.color = Color.white;
        completionLabel.fontSize = 11;
        completionLabel.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(completionLabel.rectTransform, 9, 148, 150, 18);

        TextMeshProUGUI completionValue = CreateTextLabel(
            "CompletionValueTextField",
            window.transform
        );
        completionValue.text = "N/A";
        completionValue.color = Color.white;
        completionValue.fontSize = 11;
        completionValue.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(completionValue.rectTransform, 180, 148, 32, 18);

        TextMeshProUGUI deploymentLabel = CreateTextLabel(
            "DeploymentLabelTextField",
            window.transform
        );
        deploymentLabel.text = "Best Time to Deployment";
        deploymentLabel.color = Color.white;
        deploymentLabel.fontSize = 11;
        deploymentLabel.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(deploymentLabel.rectTransform, 9, 169, 150, 18);

        TextMeshProUGUI deploymentValue = CreateTextLabel(
            "DeploymentValueTextField",
            window.transform
        );
        deploymentValue.text = "N/A";
        deploymentValue.color = Color.white;
        deploymentValue.fontSize = 11;
        deploymentValue.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(deploymentValue.rectTransform, 180, 169, 32, 18);

        RawImage dropdownButton = CreateRawImage(
            "DropdownButtonImage",
            window.transform,
            _constructionOpenButtonPreviewPath,
            79,
            90
        );
        Button dropdownButtonComponent = CreateButton(dropdownButton);
        RawImage infoButton = CreateRawImage(
            "InfoButtonImage",
            window.transform,
            _constructionInfoButtonPreviewPath,
            5,
            224
        );
        Button infoButtonComponent = CreateButton(infoButton);
        RawImage okButton = CreateRawImage(
            "OkButtonImage",
            window.transform,
            _constructionOkButtonPreviewPath,
            73,
            224
        );
        Button okButtonComponent = CreateButton(okButton);
        RawImage cancelButton = CreateRawImage(
            "CancelButtonImage",
            window.transform,
            _constructionCancelButtonPreviewPath,
            141,
            224
        );
        Button cancelButtonComponent = CreateButton(cancelButton);

        RectTransform dropdown = CreateChildLayer("Dropdown", window.transform);
        SetSourceRect(dropdown, 8, 111, 195, 142);
        Image dropdownFill = CreateImage("DropdownFrameFillImage", dropdown);
        dropdownFill.color = new Color32(12, 15, 18, 255);
        SetSourceRect(dropdownFill.rectTransform, 1, 1, 193, 140);
        Image dropdownTop = CreateImage("DropdownFrameTopImage", dropdown);
        dropdownTop.color = Color.white;
        SetSourceRect(dropdownTop.rectTransform, 0, 0, 195, 1);
        Image dropdownBottom = CreateImage("DropdownFrameBottomImage", dropdown);
        dropdownBottom.color = Color.white;
        SetSourceRect(dropdownBottom.rectTransform, 0, 141, 195, 1);
        Image dropdownLeft = CreateImage("DropdownFrameLeftImage", dropdown);
        dropdownLeft.color = Color.white;
        SetSourceRect(dropdownLeft.rectTransform, 0, 0, 1, 142);
        Image dropdownRight = CreateImage("DropdownFrameRightImage", dropdown);
        dropdownRight.color = Color.white;
        SetSourceRect(dropdownRight.rectTransform, 194, 0, 1, 142);

        List<RawImage> dropdownBackgrounds = new List<RawImage>();
        for (int i = 0; i < 3; i++)
        {
            RawImage dropdownBackground = CreateRawImage(
                $"DropdownBackground{i}Image",
                dropdown,
                _constructionDropdownBackgroundPreviewPath,
                0,
                i * 61
            );
            SetSourceRect(dropdownBackground.rectTransform, 0, i * 61, 195, i == 2 ? 20 : 61);
            dropdownBackgrounds.Add(dropdownBackground);
        }

        ScrollAreaView dropdownScrollArea = CreateScrollAreaView(
            dropdown,
            "DropdownScrollArea",
            0,
            0,
            195,
            142,
            0,
            1,
            180,
            140,
            182,
            0,
            13,
            142,
            out RectTransform dropdownContent
        );
        ConfigureVerticalContent(dropdownContent);

        RawImage dropdownItemImageTemplate = CreateRawButton(
            "DropdownItemImageTemplate",
            dropdownContent,
            _facilityCardEntityPreviewPath
        );
        SetSourceRect(dropdownItemImageTemplate.rectTransform, 0, 4, 180, 48);
        dropdownItemImageTemplate.gameObject.SetActive(false);

        TextMeshProUGUI dropdownItemTextTemplate = CreateTextLabel(
            "DropdownItemTextTemplate",
            dropdownContent
        );
        dropdownItemTextTemplate.text = "Nebulon-B Frigate";
        dropdownItemTextTemplate.color = Color.white;
        dropdownItemTextTemplate.fontSize = 12;
        dropdownItemTextTemplate.alignment = TextAlignmentOptions.Top;
        SetSourceRect(dropdownItemTextTemplate.rectTransform, 0, 52, 180, 18);
        dropdownItemTextTemplate.gameObject.SetActive(false);

        RectTransform layoutTemplates = CreateChildLayer("LayoutTemplates", window.transform);
        layoutTemplates.gameObject.SetActive(false);
        GameObject dropdownItemRowObject = new GameObject(
            "DropdownItemRowTemplate",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        dropdownItemRowObject.transform.SetParent(layoutTemplates, false);
        RectTransform dropdownItemRowTemplate = dropdownItemRowObject.GetComponent<RectTransform>();
        SetSourceRect(dropdownItemRowTemplate, 0, 4, 180, 70);
        Image dropdownItemRowHitArea = dropdownItemRowObject.GetComponent<Image>();
        dropdownItemRowHitArea.color = Color.clear;
        dropdownItemRowHitArea.raycastTarget = true;
        Button dropdownItemRowButton = dropdownItemRowObject.GetComponent<Button>();
        dropdownItemRowButton.targetGraphic = dropdownItemRowHitArea;
        dropdownItemRowButton.transition = Selectable.Transition.None;
        AddTemplateLayoutElement(dropdownItemRowTemplate);
        RectTransform dropdownItemImageAreaTemplate = CreateChildLayer(
            "DropdownItemImageAreaTemplate",
            layoutTemplates
        );
        SetSourceRect(dropdownItemImageAreaTemplate, 0, 0, 180, 48);
        RectTransform dropdownItemTextAreaTemplate = CreateChildLayer(
            "DropdownItemTextAreaTemplate",
            layoutTemplates
        );
        SetSourceRect(dropdownItemTextAreaTemplate, 0, 48, 180, 18);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(view, "selectedItemImage", selectedItem);
        AssignReference(view, "selectedNameTextField", selectedName);
        AssignReference(view, "buildCountLabelTextField", buildCountLabel);
        AssignReference(view, "buildCountTextField", buildCount);
        AssignReference(view, "incrementButtonImage", increment);
        AssignReference(view, "decrementButtonImage", decrement);
        AssignReference(view, "constructionCostTextField", constructionCost);
        AssignReference(view, "maintenanceCostTextField", maintenanceCost);
        AssignReference(view, "completionLabelTextField", completionLabel);
        AssignReference(view, "completionValueTextField", completionValue);
        AssignReference(view, "deploymentLabelTextField", deploymentLabel);
        AssignReference(view, "deploymentValueTextField", deploymentValue);
        AssignReference(view, "dropdownButtonImage", dropdownButton);
        AssignReference(view, "infoButtonImage", infoButton);
        AssignReference(view, "okButtonImage", okButton);
        AssignReference(view, "cancelButtonImage", cancelButton);
        AssignReference(view, "incrementButton", incrementButton);
        AssignReference(view, "decrementButton", decrementButton);
        AssignReference(view, "dropdownButton", dropdownButtonComponent);
        AssignReference(view, "infoButton", infoButtonComponent);
        AssignReference(view, "okButton", okButtonComponent);
        AssignReference(view, "cancelButton", cancelButtonComponent);
        AssignReference(view, "dropdownRoot", dropdown);
        AssignReference(view, "dropdownFrameFillImage", dropdownFill);
        AssignReference(view, "dropdownFrameTopImage", dropdownTop);
        AssignReference(view, "dropdownFrameBottomImage", dropdownBottom);
        AssignReference(view, "dropdownFrameLeftImage", dropdownLeft);
        AssignReference(view, "dropdownFrameRightImage", dropdownRight);
        AssignReferenceArray(view, "dropdownBackgroundImages", dropdownBackgrounds);
        AssignReference(view, "dropdownScrollArea", dropdownScrollArea);
        AssignReference(view, "dropdownItemImageTemplate", dropdownItemImageTemplate);
        AssignReference(view, "dropdownItemTextTemplate", dropdownItemTextTemplate);
        AssignReference(view, "dropdownItemRowTemplate", dropdownItemRowTemplate);
        AssignReference(view, "dropdownItemImageAreaTemplate", dropdownItemImageAreaTemplate);
        AssignReference(view, "dropdownItemTextAreaTemplate", dropdownItemTextAreaTemplate);
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(
            view,
            "incrementButtonUpTexture",
            LoadTexture(_constructionIncrementButtonPreviewPath)
        );
        AssignReference(
            view,
            "decrementButtonUpTexture",
            LoadTexture(_constructionDecrementButtonPreviewPath)
        );
        AssignReference(
            view,
            "dropdownButtonUpTexture",
            LoadTexture(_constructionOpenButtonPreviewPath)
        );
        AssignReference(
            view,
            "dropdownButtonDownTexture",
            LoadTexture(_constructionOpenButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "infoButtonUpTexture",
            LoadTexture(_constructionInfoButtonPreviewPath)
        );
        AssignReference(view, "okButtonUpTexture", LoadTexture(_constructionOkButtonPreviewPath));
        AssignReference(
            view,
            "okButtonDisabledTexture",
            LoadTexture(_constructionOkButtonDisabledPreviewPath)
        );
        AssignReference(
            view,
            "cancelButtonUpTexture",
            LoadTexture(_constructionCancelButtonPreviewPath)
        );

        dropdown.gameObject.SetActive(false);
        GameObject saved = SaveGeneratedPrefabAsset(window, _constructionWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<ConstructionWindowView>();
    }

    private static DefenseWindowView BuildDefenseWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_defenseWindowPrefabPath));
        const int windowWidth = 235;
        const int windowHeight = 304;

        GameObject window = new GameObject(
            "DefenseWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(DefenseWindowView)
        );
        DefenseWindowView view = window.GetComponent<DefenseWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _defenseWindowBackgroundPreviewPath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.transform, windowWidth);
        TextMeshProUGUI caption = CreateTextLabel("CaptionTextField", window.transform);
        caption.text = "Corellia";
        caption.color = Color.black;
        caption.fontSize = 12;
        caption.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(caption.rectTransform, 19, 4, 150, 16);

        RectTransform buttons = CreateSourceRectLayer(
            "Buttons",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> buttonImages = new List<RawImage>
        {
            CreateRawImage("ButtonImage0", buttons, _windowOpenSectorPreviewPath, 3, 3),
            CreateRawImage(
                "ButtonImage1",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3
            ),
            CreateRawImage("ButtonImage2", buttons, _windowClosePreviewPath, windowWidth - 17, 3),
        };
        int[] buttonActions =
        {
            StrategyWindowButtonActions.OpenSector,
            StrategyWindowButtonActions.MinimizeWindow,
            StrategyWindowButtonActions.CloseWindow,
        };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);

        RectTransform tabs = CreateSourceRectLayer(
            "Tabs",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> tabImages = new List<RawImage>();
        for (int i = 0; i < 5; i++)
        {
            RawImage tabImage = CreateRawButton($"TabImage{i}", tabs, _defenseWindowTabPreviewPath);
            SetSourceRect(tabImage.rectTransform, 27 + i * 36, 20, 36, 33);
            tabImages.Add(tabImage);
        }
        List<Button> tabButtons = CreateButtons(tabImages);

        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", window.transform);
        tabTitle.text = "Troops/Regiments";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 13;
        tabTitle.alignment = TextAlignmentOptions.Top;
        SetSourceRect(tabTitle.rectTransform, 6, 56, 222, 16);

        ScrollAreaView itemsScrollArea = CreateScrollAreaView(
            window.transform,
            "ItemsScrollArea",
            6,
            79,
            222,
            213,
            0,
            3,
            211,
            210,
            209,
            0,
            13,
            212,
            out RectTransform itemsContent
        );
        GridLayoutGroup itemsGridLayout = ConfigureGridContent(itemsContent, 71, 70, 3);

        StrategyUnitCardView itemCardTemplate = CreateDefenseUnitCardTemplate(itemsContent);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(
            view,
            "backgroundTexture",
            LoadTexture(_defenseWindowBackgroundPreviewPath)
        );
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(
            view,
            "openSectorButtonUpTexture",
            LoadTexture(_windowOpenSectorPreviewPath)
        );
        AssignReference(
            view,
            "openSectorButtonDownTexture",
            LoadTexture(_windowOpenSectorDownPreviewPath)
        );
        AssignReference(view, "minimizeButtonUpTexture", LoadTexture(_windowMinimizePreviewPath));
        AssignReference(
            view,
            "minimizeButtonDownTexture",
            LoadTexture(_windowMinimizeDownPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(view, "closeButtonDownTexture", LoadTexture(_windowCloseDownPreviewPath));
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignDefenseWindowTabTextures(view);
        AssignReference(view, "tabTitleTextField", tabTitle);
        AssignReference(view, "itemsScrollArea", itemsScrollArea);
        AssignReference(view, "itemsGridLayout", itemsGridLayout);
        AssignReference(view, "itemCardTemplate", itemCardTemplate);
        AssignReference(
            view,
            "personnelBackgroundTexture",
            LoadTexture(_defensePersonnelBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "enrouteBackgroundTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_enroute_background")
        );

        GameObject saved = SaveGeneratedPrefabAsset(window, _defenseWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<DefenseWindowView>();
    }

    private static void AssignDefenseWindowTabTextures(DefenseWindowView view)
    {
        AssignReference(
            view,
            "shieldAvailableTabTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_tab_shield_available_tab")
        );
        AssignReference(
            view,
            "shieldActiveTabTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_tab_shield_active_tab")
        );
        AssignReference(
            view,
            "shieldDisabledTabTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_tab_shield_disabled_tab")
        );
        AssignReference(
            view,
            "batteryAvailableTabTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_tab_battery_available_tab")
        );
        AssignReference(
            view,
            "batteryActiveTabTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_tab_battery_active_tab")
        );
        AssignReference(
            view,
            "batteryDisabledTabTexture",
            LoadStrategyViewTexture("ui_strategyview_defense_window_tab_battery_disabled_tab")
        );
    }

    private static StrategyUnitCardView CreateDefenseUnitCardTemplate(Transform parent)
    {
        GameObject item = new GameObject(
            "ItemCardTemplate",
            typeof(RectTransform),
            typeof(StrategyUnitCardView)
        );
        item.transform.SetParent(parent, false);
        StrategyUnitCardView view = item.GetComponent<StrategyUnitCardView>();
        SetSourceRect(item.GetComponent<RectTransform>(), 0, 0, 71, 70);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            item.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;
        SetSourceRect(hitArea.rectTransform, 0, 0, 71, 70);
        RawImage background = CreateRawButton(
            "BackgroundImage",
            item.transform,
            _defensePersonnelBackgroundPreviewPath
        );
        SetSourceRect(background.rectTransform, 0, 0, 61, 25);
        RawImage entity = CreateRawButton(
            "EntityImage",
            item.transform,
            _facilityCardEntityPreviewPath
        );
        SetSourceRect(entity.rectTransform, 0, 0, 61, 25);
        RawImage constructionOverlay = CreateRawButton(
            "ConstructionOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetConstructionSmallImagePath
        );
        SetSourceRect(constructionOverlay.rectTransform, 0, 0, 61, 25);
        RawImage enrouteOverlay = CreateRawButton(
            "EnrouteOverlayImage",
            item.transform,
            _defenseEnrouteBackgroundPreviewPath
        );
        SetSourceRect(enrouteOverlay.rectTransform, 0, 0, 61, 25);
        RawImage damagedOverlay = CreateRawButton(
            "DamagedOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListDamagedIconImagePath
        );
        SetSourceRect(damagedOverlay.rectTransform, 0, 0, 61, 25);
        RawImage capturedOverlay = CreateRawButton("CapturedOverlayImage", item.transform);
        SetSourceRect(capturedOverlay.rectTransform, 0, 0, 61, 25);
        RawImage selection = CreateRawButton(
            "SelectionImage",
            item.transform,
            _defenseSelectionPreviewPath
        );
        SetSourceRect(selection.rectTransform, 0, 0, 61, 25);

        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", item.transform);
        nameText.text = "Mon Calamari Regiment";
        nameText.color = Color.white;
        nameText.fontSize = 10;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.maxVisibleLines = 2;
        SetSourceRect(nameText.rectTransform, 0, 28, 68, 30);

        AssignUnitCardReferences(
            view,
            hitArea,
            background,
            constructionOverlay,
            enrouteOverlay,
            damagedOverlay,
            entity,
            capturedOverlay,
            selection,
            null,
            null,
            null,
            nameText,
            null
        );
        AddTemplateLayoutElement(item.GetComponent<RectTransform>());
        item.SetActive(false);
        return view;
    }

    private static FleetWindowView BuildFleetWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_fleetWindowPrefabPath));
        const int windowWidth = 235;
        const int windowHeight = 304;

        GameObject window = new GameObject(
            "FleetWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(FleetWindowView)
        );
        FleetWindowView view = window.GetComponent<FleetWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _fleetWindowBackgroundPreviewPath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.transform, windowWidth);
        TextMeshProUGUI caption = CreateTextLabel("CaptionTextField", window.transform);
        caption.text = "Corellia";
        caption.color = Color.black;
        caption.fontSize = 12;
        caption.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(caption.rectTransform, 19, 4, 150, 16);

        RectTransform buttons = CreateSourceRectLayer(
            "Buttons",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> buttonImages = new List<RawImage>
        {
            CreateRawImage("ButtonImage0", buttons, _windowOpenSectorPreviewPath, 3, 3),
            CreateRawImage(
                "ButtonImage1",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3
            ),
            CreateRawImage("ButtonImage2", buttons, _windowClosePreviewPath, windowWidth - 17, 3),
        };
        int[] buttonActions =
        {
            StrategyWindowButtonActions.OpenSector,
            StrategyWindowButtonActions.MinimizeWindow,
            StrategyWindowButtonActions.CloseWindow,
        };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);

        ScrollAreaView fleetListScrollArea = CreateScrollAreaView(
            window.transform,
            "FleetListScrollArea",
            5,
            29,
            91,
            267,
            0,
            0,
            90,
            267,
            78,
            0,
            13,
            267,
            out RectTransform fleetListContent
        );
        ConfigureVerticalContent(fleetListContent);
        FleetListRowView listRowTemplate = CreateFleetListRowTemplate(fleetListContent);

        RectTransform layoutTemplates = CreateChildLayer("LayoutTemplates", window.transform);
        layoutTemplates.gameObject.SetActive(false);

        RawImage detailBackground = CreateRawImage(
            "DetailBackgroundImage",
            window.transform,
            _fleetDetailBackgroundPreviewPath,
            97,
            29
        );
        RawImage banner = CreateRawImage(
            "BannerImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Fleet?.BannerImagePath,
            103,
            40
        );
        RawImage bannerEnrouteOverlay = CreateRawImage(
            "BannerEnrouteOverlayImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Status?.FleetBannerEnrouteImagePath,
            103,
            40
        );
        RawImage bannerDamagedOverlay = CreateRawImage(
            "BannerDamagedOverlayImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Status?.FleetBannerDamagedImagePath,
            103,
            40
        );
        TextMeshProUGUI fleetName = CreateTextLabel("FleetNameTextField", window.transform);
        fleetName.text = "Fleet";
        fleetName.color = Color.yellow;
        fleetName.fontSize = 12;
        fleetName.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(fleetName.rectTransform, 100, 32, 126, 16);

        TextMeshProUGUI capacityLeft = CreateTextLabel("CapacityLeftTextField", window.transform);
        capacityLeft.text = "0";
        capacityLeft.color = Color.white;
        capacityLeft.fontSize = 12;
        capacityLeft.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(capacityLeft.rectTransform, 100, 45, 30, 16);

        TextMeshProUGUI capacityRight = CreateTextLabel("CapacityRightTextField", window.transform);
        capacityRight.text = "0";
        capacityRight.color = Color.white;
        capacityRight.fontSize = 12;
        capacityRight.alignment = TextAlignmentOptions.TopRight;
        SetSourceRect(capacityRight.rectTransform, 195, 45, 30, 16);

        RectTransform tabs = CreateSourceRectLayer(
            "Tabs",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> tabImages = new List<RawImage>();
        string[] defaultTabTexturePaths =
        {
            _fleetTabPreviewPath,
            PreviewTheme?.StrategyWindows?.Fleet?.Tabs?.Starfighters?.InactiveImagePath,
            PreviewTheme?.StrategyWindows?.Fleet?.Tabs?.Regiments?.InactiveImagePath,
            PreviewTheme?.StrategyWindows?.Fleet?.Tabs?.Officers?.InactiveImagePath,
        };
        for (int i = 0; i < 4; i++)
        {
            RawImage tabImage = CreateRawButton($"TabImage{i}", tabs, defaultTabTexturePaths[i]);
            SetSourceRect(tabImage.rectTransform, 100 + i * 32, 96, 32, 33);
            tabImages.Add(tabImage);
        }
        List<Button> tabButtons = CreateButtons(tabImages);

        ScrollAreaView detailItemsScrollArea = CreateScrollAreaView(
            window.transform,
            "DetailItemsScrollArea",
            104,
            125,
            131,
            166,
            0,
            0,
            125,
            166,
            118,
            2,
            13,
            164,
            out RectTransform detailItemsContent
        );
        ConfigureVerticalContent(detailItemsContent);
        StrategyUnitCardView detailItemTemplate = CreateFleetDetailUnitCardTemplate(
            detailItemsContent
        );
        RectTransform detailItemsScrollPaddingTemplate = CreateChildLayer(
            "DetailItemsScrollPaddingTemplate",
            layoutTemplates
        );
        SetSourceRect(detailItemsScrollPaddingTemplate, 0, 0, 125, 10);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(view, "backgroundTexture", LoadTexture(_fleetWindowBackgroundPreviewPath));
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(
            view,
            "openSectorButtonUpTexture",
            LoadTexture(_windowOpenSectorPreviewPath)
        );
        AssignReference(
            view,
            "openSectorButtonDownTexture",
            LoadTexture(_windowOpenSectorDownPreviewPath)
        );
        AssignReference(view, "minimizeButtonUpTexture", LoadTexture(_windowMinimizePreviewPath));
        AssignReference(
            view,
            "minimizeButtonDownTexture",
            LoadTexture(_windowMinimizeDownPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(view, "closeButtonDownTexture", LoadTexture(_windowCloseDownPreviewPath));
        AssignInt(view, "contextMenuWidth", _fleetContextMenuWidth);
        AssignInt(view, "bombardmentContextMenuWidth", _fleetBombardmentContextMenuWidth);
        AssignReference(view, "fleetListScrollArea", fleetListScrollArea);
        AssignReference(view, "fleetListRowTemplate", listRowTemplate);
        AssignReference(view, "detailBackgroundImage", detailBackground);
        AssignReference(view, "bannerImage", banner);
        AssignReference(view, "bannerEnrouteOverlayImage", bannerEnrouteOverlay);
        AssignReference(view, "bannerDamagedOverlayImage", bannerDamagedOverlay);
        AssignReference(view, "fleetNameTextField", fleetName);
        AssignReference(view, "capacityLeftTextField", capacityLeft);
        AssignReference(view, "capacityRightTextField", capacityRight);
        AssignReference(view, "tabsRoot", tabs);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "detailItemsScrollArea", detailItemsScrollArea);
        AssignReference(view, "detailItemsScrollPaddingTemplate", detailItemsScrollPaddingTemplate);
        AssignReference(view, "detailItemTemplate", detailItemTemplate);
        AssignReference(
            view,
            "personnelBackgroundTexture",
            LoadTexture(_defensePersonnelBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "personnelEnrouteBackgroundTexture",
            LoadTexture(_fleetPersonnelEnrouteBackgroundPreviewPath)
        );

        GameObject saved = SaveGeneratedPrefabAsset(window, _fleetWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<FleetWindowView>();
    }

    private static FleetListRowView CreateFleetListRowTemplate(Transform parent)
    {
        GameObject row = new GameObject(
            "FleetListRowTemplate",
            typeof(RectTransform),
            typeof(FleetListRowView)
        );
        row.transform.SetParent(parent, false);
        FleetListRowView view = row.GetComponent<FleetListRowView>();
        SetSourceRect(row.GetComponent<RectTransform>(), 0, 0, 90, 52);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            row.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitArea.rectTransform, 0, 0, 90, 52);
        RawImage selection = CreateRawImage(
            "SelectionImage",
            row.transform,
            _fleetSelectionPreviewPath,
            0,
            0
        );
        RawImage icon = CreateRawImage("IconImage", row.transform, _fleetIconPreviewPath, 5, 5);
        RawImage enrouteOverlay = CreateRawImage(
            "EnrouteOverlayImage",
            row.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListEnrouteIconImagePath,
            5,
            5
        );
        SetSourceRect(enrouteOverlay.rectTransform, 5, 5, 66, 25);
        RawImage damagedOverlay = CreateRawImage(
            "DamagedOverlayImage",
            row.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListDamagedIconImagePath,
            5,
            5
        );
        SetSourceRect(damagedOverlay.rectTransform, 5, 5, 66, 25);
        RawImage starfighterBadge = CreateRawImage(
            "StarfighterBadgeImage",
            row.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetStarfightersBadgeImagePath,
            24,
            33
        );
        SetSourceRect(starfighterBadge.rectTransform, 24, 33, 15, 11);
        RawImage troopBadge = CreateRawImage(
            "TroopBadgeImage",
            row.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetTroopsBadgeImagePath,
            40,
            33
        );
        SetSourceRect(troopBadge.rectTransform, 40, 33, 15, 11);
        RawImage personnelBadge = CreateRawImage(
            "PersonnelBadgeImage",
            row.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetPersonnelBadgeImagePath,
            56,
            33
        );
        SetSourceRect(personnelBadge.rectTransform, 56, 33, 15, 11);
        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", row.transform);
        nameText.text = "Fleet";
        nameText.color = Color.white;
        nameText.fontSize = 11;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(nameText.rectTransform, 7, 5, 78, 14);

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "iconImage", icon);
        AssignReference(view, "enrouteOverlayImage", enrouteOverlay);
        AssignReference(view, "damagedOverlayImage", damagedOverlay);
        AssignReference(view, "starfighterBadgeImage", starfighterBadge);
        AssignReference(view, "troopBadgeImage", troopBadge);
        AssignReference(view, "personnelBadgeImage", personnelBadge);
        AssignReference(view, "nameTextField", nameText);
        AssignReference(view, "selectionImage", selection);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);
        return view;
    }

    private static StrategyUnitCardView CreateFleetDetailUnitCardTemplate(Transform parent)
    {
        GameObject item = new GameObject(
            "FleetDetailItemTemplate",
            typeof(RectTransform),
            typeof(StrategyUnitCardView)
        );
        item.transform.SetParent(parent, false);
        StrategyUnitCardView view = item.GetComponent<StrategyUnitCardView>();
        SetSourceRect(item.GetComponent<RectTransform>(), 0, 3, 125, 50);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            item.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;
        SetSourceRect(hitArea.rectTransform, 0, 0, 125, 50);
        RawImage background = CreateRawImage(
            "BackgroundImage",
            item.transform,
            _defensePersonnelBackgroundPreviewPath,
            25,
            5
        );
        SetSourceRect(background.rectTransform, 25, 5, 61, 25);
        RawImage constructionOverlay = CreateRawImage(
            "ConstructionOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetConstructionSmallImagePath,
            25,
            5
        );
        SetSourceRect(constructionOverlay.rectTransform, 25, 5, 61, 25);
        RawImage enrouteOverlay = CreateRawImage(
            "EnrouteOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListEnrouteIconImagePath,
            25,
            5
        );
        SetSourceRect(enrouteOverlay.rectTransform, 25, 5, 61, 25);
        RawImage damagedOverlay = CreateRawImage(
            "DamagedOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListDamagedIconImagePath,
            25,
            5
        );
        SetSourceRect(damagedOverlay.rectTransform, 25, 5, 61, 25);
        RawImage entity = CreateRawImage(
            "EntityImage",
            item.transform,
            _facilityCardEntityPreviewPath,
            25,
            5
        );
        SetSourceRect(entity.rectTransform, 25, 5, 61, 25);
        RawImage capturedOverlay = CreateRawImage(
            "CapturedOverlayImage",
            item.transform,
            null,
            25,
            5
        );
        SetSourceRect(capturedOverlay.rectTransform, 25, 5, 61, 25);
        RawImage starfighterBadge = CreateRawImage(
            "StarfighterBadgeImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetStarfightersBadgeImagePath,
            23,
            23
        );
        SetSourceRect(starfighterBadge.rectTransform, 23, 23, 15, 11);
        RawImage troopBadge = CreateRawImage(
            "TroopBadgeImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetTroopsBadgeImagePath,
            39,
            23
        );
        SetSourceRect(troopBadge.rectTransform, 39, 23, 15, 11);
        RawImage personnelBadge = CreateRawImage(
            "PersonnelBadgeImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetPersonnelBadgeImagePath,
            55,
            23
        );
        SetSourceRect(personnelBadge.rectTransform, 55, 23, 15, 11);
        RawImage selection = CreateRawImage(
            "SelectionImage",
            item.transform,
            _fleetShipSelectionPreviewPath,
            2,
            0
        );
        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", item.transform);
        nameText.text = "Capital Ship";
        nameText.color = Color.white;
        nameText.fontSize = 10;
        nameText.alignment = TextAlignmentOptions.Top;
        SetSourceRect(nameText.rectTransform, 2, 38, 116, 15);

        TextMeshProUGUI personnelNameText = CreateTextLabel(
            "PersonnelNameTextTemplate",
            item.transform
        );
        personnelNameText.text = "Personnel";
        personnelNameText.color = Color.white;
        personnelNameText.fontSize = 10;
        personnelNameText.alignment = TextAlignmentOptions.Top;
        SetSourceRect(personnelNameText.rectTransform, -2, 38, 116, 15);
        personnelNameText.gameObject.SetActive(false);

        AssignUnitCardReferences(
            view,
            hitArea,
            background,
            constructionOverlay,
            enrouteOverlay,
            damagedOverlay,
            entity,
            capturedOverlay,
            selection,
            starfighterBadge,
            troopBadge,
            personnelBadge,
            nameText,
            personnelNameText
        );
        AddTemplateLayoutElement(item.GetComponent<RectTransform>());
        item.SetActive(false);
        return view;
    }

    private static MissionsWindowView BuildMissionsWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_missionsWindowPrefabPath));
        const int windowWidth = 235;
        const int windowHeight = 304;

        GameObject window = new GameObject(
            "MissionsWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(MissionsWindowView)
        );
        MissionsWindowView view = window.GetComponent<MissionsWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _missionsWindowBackgroundPreviewPath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.transform, windowWidth);
        TextMeshProUGUI caption = CreateTextLabel("CaptionTextField", window.transform);
        caption.text = "Corellia";
        caption.color = Color.black;
        caption.fontSize = 12;
        caption.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(caption.rectTransform, 19, 4, 150, 16);

        RectTransform buttons = CreateSourceRectLayer(
            "Buttons",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> buttonImages = new List<RawImage>
        {
            CreateRawImage("ButtonImage0", buttons, _windowOpenSectorPreviewPath, 3, 3),
            CreateRawImage(
                "ButtonImage1",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3
            ),
            CreateRawImage("ButtonImage2", buttons, _windowClosePreviewPath, windowWidth - 17, 3),
        };
        int[] buttonActions =
        {
            StrategyWindowButtonActions.OpenSector,
            StrategyWindowButtonActions.MinimizeWindow,
            StrategyWindowButtonActions.CloseWindow,
        };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);

        ScrollAreaView missionListScrollArea = CreateScrollAreaView(
            window.transform,
            "MissionListScrollArea",
            5,
            22,
            95,
            276,
            0,
            0,
            95,
            276,
            81,
            2,
            13,
            274,
            out RectTransform missionListContent
        );
        ConfigureVerticalContent(missionListContent);
        MissionListRowView listRowTemplate = CreateMissionListRowTemplate(missionListContent);

        RectTransform layoutTemplates = CreateChildLayer("LayoutTemplates", window.transform);
        layoutTemplates.gameObject.SetActive(false);
        RectTransform missionListContentPaddingTemplate = CreateChildLayer(
            "MissionListContentPaddingTemplate",
            layoutTemplates
        );
        SetSourceRect(missionListContentPaddingTemplate, 0, 0, 95, 5);

        TextMeshProUGUI targetTitle = CreateTextLabel("TargetTitleTextField", window.transform);
        targetTitle.text = "Target";
        targetTitle.color = Color.white;
        targetTitle.fontSize = 13;
        targetTitle.alignment = TextAlignmentOptions.Top;
        SetSourceRect(targetTitle.rectTransform, 116, 27, 100, 16);

        RawImage targetImage = CreateRawImage(
            "TargetImage",
            window.transform,
            _facilityCardEntityPreviewPath,
            135,
            44
        );
        SetSourceRect(targetImage.rectTransform, 135, 44, 73, 48);

        TextMeshProUGUI targetName = CreateTextLabel("TargetNameTextField", window.transform);
        targetName.text = "Corellia";
        targetName.color = Color.white;
        targetName.fontSize = 13;
        targetName.alignment = TextAlignmentOptions.Top;
        SetSourceRect(targetName.rectTransform, 109, 94, 115, 16);

        RectTransform tabs = CreateSourceRectLayer(
            "Tabs",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> tabImages = new List<RawImage>();
        for (int i = 0; i < 2; i++)
        {
            RawImage tab = CreateRawButton($"TabImage{i}", tabs, _missionsTabPreviewPath);
            SetSourceRect(tab.rectTransform, 105 + i * 61, 127, 61, 16);
            tabImages.Add(tab);
        }
        List<Button> tabButtons = CreateButtons(tabImages);

        ScrollAreaView participantsScrollArea = CreateScrollAreaView(
            window.transform,
            "ParticipantsScrollArea",
            108,
            146,
            117,
            146,
            0,
            0,
            116,
            146,
            104,
            0,
            13,
            146,
            out RectTransform participantsContent
        );
        ConfigureVerticalContent(participantsContent);
        MissionParticipantRowView participantTemplate = CreateMissionParticipantRowTemplate(
            participantsContent,
            "ParticipantRowTemplate",
            116,
            42,
            5,
            27,
            0,
            61,
            25,
            4,
            28,
            108,
            14
        );

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(view, "missionListScrollArea", missionListScrollArea);
        AssignReference(view, "missionListRowTemplate", listRowTemplate);
        AssignReference(
            view,
            "missionListContentPaddingTemplate",
            missionListContentPaddingTemplate
        );
        AssignReference(view, "targetTitleTextField", targetTitle);
        AssignReference(view, "targetImage", targetImage);
        AssignReference(view, "targetNameTextField", targetName);
        AssignReference(view, "tabsRoot", tabs);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "participantsScrollArea", participantsScrollArea);
        AssignReference(view, "participantRowTemplate", participantTemplate);
        AssignReference(
            view,
            "backgroundTexture",
            LoadTexture(_missionsWindowBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "openSectorButtonUpTexture",
            LoadTexture(_windowOpenSectorPreviewPath)
        );
        AssignReference(
            view,
            "openSectorButtonDownTexture",
            LoadTexture(_windowOpenSectorDownPreviewPath)
        );
        AssignReference(view, "minimizeButtonUpTexture", LoadTexture(_windowMinimizePreviewPath));
        AssignReference(
            view,
            "minimizeButtonDownTexture",
            LoadTexture(_windowMinimizeDownPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(view, "closeButtonDownTexture", LoadTexture(_windowCloseDownPreviewPath));
        AssignReference(
            view,
            "participantEnrouteBackgroundTexture",
            LoadTexture(_fleetPersonnelEnrouteBackgroundPreviewPath)
        );

        GameObject saved = SaveGeneratedPrefabAsset(window, _missionsWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<MissionsWindowView>();
    }

    private static MissionCreateWindowView BuildMissionCreateWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_missionCreateWindowPrefabPath));
        const int windowWidth = 259;
        const int windowHeight = 355;

        GameObject window = new GameObject(
            "MissionCreateWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        window.SetActive(false);
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _missionCreateMissionBackgroundPreviewPath,
            0,
            0
        );
        const int titleWidth = 240;
        const int titleHeight = 17;
        background.raycastTarget = true;

        List<RawImage> titleImages = new List<RawImage>
        {
            CreateRawImage(
                "TitleImage0",
                window.transform,
                PreviewTheme?.StrategyWindows?.MissionCreate?.TitleImagePath,
                2,
                2
            ),
            CreateRawImage(
                "TitleImage1",
                window.transform,
                PreviewTheme?.StrategyWindows?.MissionCreate?.TitleImagePath,
                windowWidth - titleWidth - 2,
                2
            ),
        };
        for (int i = 0; i < titleImages.Count; i++)
        {
            titleImages[i].raycastTarget = true;
            titleImages[i].gameObject.AddComponent<UIWindowDragHandle>();
        }

        for (int i = 0; i < titleImages.Count; i++)
            SetSourceRect(
                titleImages[i].rectTransform,
                i == 0 ? 2 : windowWidth - titleWidth - 2,
                2,
                titleWidth,
                titleHeight
            );

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", window.transform);
        title.text = "Create Mission";
        title.color = Color.black;
        title.fontSize = 12;
        title.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(title.rectTransform, 4, 4, 160, 16);

        RectTransform buttons = CreateSourceRectLayer(
            "Buttons",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> buttonImages = new List<RawImage>
        {
            CreateRawImage("ButtonImage0", buttons, _windowClosePreviewPath, windowWidth - 17, 3),
        };
        int[] buttonActions = { StrategyWindowButtonActions.CloseWindow };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);

        RectTransform tabs = CreateSourceRectLayer(
            "Tabs",
            window.transform,
            windowWidth,
            windowHeight
        );
        List<RawImage> tabImages = new List<RawImage>();
        List<Texture2D> tabActiveTextures = new List<Texture2D>
        {
            LoadTexture(PreviewTheme?.StrategyWindows?.MissionCreate?.MissionTab?.ActiveImagePath),
            LoadTexture(
                PreviewTheme?.StrategyWindows?.MissionCreate?.PersonnelTab?.ActiveImagePath
            ),
        };
        List<Texture2D> tabInactiveTextures = new List<Texture2D>
        {
            LoadTexture(
                PreviewTheme?.StrategyWindows?.MissionCreate?.MissionTab?.InactiveImagePath
            ),
            LoadTexture(
                PreviewTheme?.StrategyWindows?.MissionCreate?.PersonnelTab?.InactiveImagePath
            ),
        };
        RawImage firstTab = CreateRawButton(
            "Tab0Image",
            tabs,
            PreviewTheme?.StrategyWindows?.MissionCreate?.MissionTab?.InactiveImagePath
        );
        SetSourceRect(firstTab.rectTransform, 8, 20, 116, 33);
        Button firstTabButton = CreateButton(firstTab);
        tabImages.Add(firstTab);
        RawImage secondTab = CreateRawButton(
            "Tab1Image",
            tabs,
            PreviewTheme?.StrategyWindows?.MissionCreate?.PersonnelTab?.InactiveImagePath
        );
        SetSourceRect(secondTab.rectTransform, 136, 20, 116, 33);
        Button secondTabButton = CreateButton(secondTab);
        tabImages.Add(secondTab);
        List<Button> tabButtons = new List<Button> { firstTabButton, secondTabButton };

        RawImage infoButton = CreateRawImage(
            "InfoButtonImage",
            window.transform,
            _constructionInfoButtonPreviewPath,
            33,
            320
        );
        Button infoButtonComponent = CreateButton(infoButton);
        RawImage okButton = CreateRawImage(
            "OkButtonImage",
            window.transform,
            _constructionOkButtonPreviewPath,
            102,
            320
        );
        Button okButtonComponent = CreateButton(okButton);
        RawImage cancelButton = CreateRawImage(
            "CancelButtonImage",
            window.transform,
            _constructionCancelButtonPreviewPath,
            170,
            320
        );
        Button cancelButtonComponent = CreateButton(cancelButton);

        RectTransform missionSelection = CreateChildLayer("MissionSelection", window.transform);
        SetSourceRect(missionSelection, 0, 0, 259, 355);
        RawImage dropdownButton = CreateRawImage(
            "DropdownButtonImage",
            missionSelection,
            _constructionOpenButtonPreviewPath,
            101,
            174
        );
        Button dropdownButtonComponent = CreateButton(dropdownButton);
        TextMeshProUGUI targetLabel = CreateTextLabel("TargetLabelTextField", missionSelection);
        targetLabel.text = "Target";
        targetLabel.color = Color.white;
        targetLabel.fontSize = 13;
        targetLabel.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(targetLabel.rectTransform, 36, 197, 80, 16);
        RawImage selectedMission = CreateRawImage(
            "SelectedMissionImage",
            missionSelection,
            _facilityCardEntityPreviewPath,
            70,
            86
        );
        SetSourceRect(selectedMission.rectTransform, 70, 86, 130, 65);
        TextMeshProUGUI selectedMissionName = CreateTextLabel(
            "SelectedMissionNameTextField",
            missionSelection
        );
        selectedMissionName.text = "Espionage";
        selectedMissionName.color = Color.white;
        selectedMissionName.fontSize = 13;
        selectedMissionName.alignment = TextAlignmentOptions.Top;
        SetSourceRect(selectedMissionName.rectTransform, 60, 64, 150, 16);
        RawImage targetPreview = CreateRawImage(
            "TargetPreviewImage",
            missionSelection,
            _planetPreviewPath,
            50,
            211
        );
        SetSourceRect(targetPreview.rectTransform, 50, 211, 166, 78);
        TextMeshProUGUI targetPreviewName = CreateTextLabel(
            "TargetPreviewNameTextField",
            missionSelection
        );
        targetPreviewName.text = "Corellia";
        targetPreviewName.color = Color.white;
        targetPreviewName.fontSize = 13;
        targetPreviewName.alignment = TextAlignmentOptions.Top;
        SetSourceRect(targetPreviewName.rectTransform, 50, 296, 166, 16);

        RectTransform dropdown = CreateChildLayer("Dropdown", missionSelection);
        SetSourceRect(dropdown, 36, 176, 197, 114);
        Image dropdownFill = CreateImage("DropdownFrameFillImage", dropdown);
        dropdownFill.color = new Color32(12, 15, 18, 255);
        SetSourceRect(dropdownFill.rectTransform, 1, 1, 195, 112);
        Image dropdownTop = CreateImage("DropdownFrameTopImage", dropdown);
        dropdownTop.color = Color.white;
        SetSourceRect(dropdownTop.rectTransform, 0, 0, 197, 1);
        Image dropdownBottom = CreateImage("DropdownFrameBottomImage", dropdown);
        dropdownBottom.color = Color.white;
        SetSourceRect(dropdownBottom.rectTransform, 0, 113, 197, 1);
        Image dropdownLeft = CreateImage("DropdownFrameLeftImage", dropdown);
        dropdownLeft.color = Color.white;
        SetSourceRect(dropdownLeft.rectTransform, 0, 0, 1, 114);
        Image dropdownRight = CreateImage("DropdownFrameRightImage", dropdown);
        dropdownRight.color = Color.white;
        SetSourceRect(dropdownRight.rectTransform, 196, 0, 1, 114);

        List<RawImage> dropdownBackgrounds = new List<RawImage>();
        for (int i = 0; i < 2; i++)
        {
            RawImage dropdownBackground = CreateRawImage(
                $"DropdownBackground{i}Image",
                dropdown,
                _constructionDropdownBackgroundPreviewPath,
                0,
                i * 61
            );
            SetSourceRect(dropdownBackground.rectTransform, 0, i * 61, 197, i == 0 ? 61 : 53);
            dropdownBackgrounds.Add(dropdownBackground);
        }

        ScrollAreaView dropdownScrollArea = CreateScrollAreaView(
            dropdown,
            "DropdownScrollArea",
            0,
            0,
            198,
            114,
            0,
            0,
            197,
            114,
            185,
            0,
            13,
            114,
            out RectTransform dropdownContent
        );
        ConfigureVerticalContent(dropdownContent);
        RawImage dropdownItemImageTemplate = CreateRawButton(
            "DropdownItemImageTemplate",
            dropdownContent,
            _facilityCardEntityPreviewPath
        );
        SetSourceRect(dropdownItemImageTemplate.rectTransform, 2, 25, 197, 85);
        dropdownItemImageTemplate.gameObject.SetActive(false);
        TextMeshProUGUI dropdownItemTextTemplate = CreateTextLabel(
            "DropdownItemTextTemplate",
            dropdownContent
        );
        dropdownItemTextTemplate.text = "Espionage";
        dropdownItemTextTemplate.color = Color.white;
        dropdownItemTextTemplate.fontSize = 13;
        dropdownItemTextTemplate.alignment = TextAlignmentOptions.Top;
        SetSourceRect(dropdownItemTextTemplate.rectTransform, 3, 3, 190, 16);
        dropdownItemTextTemplate.gameObject.SetActive(false);

        RectTransform personnel = CreateChildLayer("Personnel", window.transform);
        SetSourceRect(personnel, 0, 0, 259, 355);
        RawImage agentsHeader = CreateRawImage(
            "AgentsHeaderImage",
            personnel,
            PreviewTheme?.StrategyWindows?.MissionCreate?.AgentsHeaderImagePath,
            8,
            65
        );
        RawImage decoysHeader = CreateRawImage(
            "DecoysHeaderImage",
            personnel,
            PreviewTheme?.StrategyWindows?.MissionCreate?.DecoysHeaderImagePath,
            136,
            65
        );
        RawImage moveRight = CreateRawImage(
            "MoveRightButtonImage",
            personnel,
            _missionCreateMoveRightButtonPreviewPath,
            120,
            136
        );
        Button moveRightButton = CreateButton(moveRight);
        RawImage moveLeft = CreateRawImage(
            "MoveLeftButtonImage",
            personnel,
            _missionCreateMoveLeftButtonPreviewPath,
            120,
            221
        );
        Button moveLeftButton = CreateButton(moveLeft);

        ScrollAreaView agentsScrollArea = CreateScrollAreaView(
            personnel,
            "AgentsScrollArea",
            9,
            93,
            108,
            214,
            0,
            0,
            107,
            214,
            95,
            0,
            13,
            214,
            out RectTransform agentsContent
        );
        ConfigureVerticalContent(agentsContent);
        RectTransform missionCreateLayoutTemplates = CreateChildLayer(
            "LayoutTemplates",
            window.transform
        );
        missionCreateLayoutTemplates.gameObject.SetActive(false);
        GameObject dropdownItemRowObject = new GameObject(
            "DropdownItemRowTemplate",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        dropdownItemRowObject.transform.SetParent(missionCreateLayoutTemplates, false);
        RectTransform dropdownItemRowTemplate = dropdownItemRowObject.GetComponent<RectTransform>();
        SetSourceRect(dropdownItemRowTemplate, 0, 0, 197, 110);
        Image dropdownItemRowHitArea = dropdownItemRowObject.GetComponent<Image>();
        dropdownItemRowHitArea.color = Color.clear;
        dropdownItemRowHitArea.raycastTarget = true;
        Button dropdownItemRowButton = dropdownItemRowObject.GetComponent<Button>();
        dropdownItemRowButton.targetGraphic = dropdownItemRowHitArea;
        dropdownItemRowButton.transition = Selectable.Transition.None;
        AddTemplateLayoutElement(dropdownItemRowTemplate);
        RectTransform dropdownItemImageAreaTemplate = CreateChildLayer(
            "DropdownItemImageAreaTemplate",
            missionCreateLayoutTemplates
        );
        SetSourceRect(dropdownItemImageAreaTemplate, 2, 25, 197, 85);
        RectTransform dropdownContentPaddingTemplate = CreateChildLayer(
            "DropdownContentPaddingTemplate",
            missionCreateLayoutTemplates
        );
        SetSourceRect(dropdownContentPaddingTemplate, 0, 0, 197, 5);
        MissionParticipantRowView agentTemplate = CreateMissionParticipantRowTemplate(
            agentsContent,
            "AgentRowTemplate",
            107,
            60,
            16,
            23,
            2,
            61,
            25,
            0,
            29,
            95,
            30
        );

        ScrollAreaView decoysScrollArea = CreateScrollAreaView(
            personnel,
            "DecoysScrollArea",
            137,
            93,
            108,
            214,
            0,
            0,
            107,
            214,
            95,
            0,
            13,
            214,
            out RectTransform decoysContent
        );
        ConfigureVerticalContent(decoysContent);
        MissionParticipantRowView decoyTemplate = CreateMissionParticipantRowTemplate(
            decoysContent,
            "DecoyRowTemplate",
            107,
            60,
            16,
            23,
            2,
            61,
            25,
            0,
            29,
            95,
            30
        );

        MissionCreateWindowView view = window.AddComponent<MissionCreateWindowView>();
        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleTextField", title);
        AssignReferenceArray(view, "titleImages", titleImages);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(view, "tabsRoot", tabs);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReferenceArray(view, "tabActiveTextures", tabActiveTextures);
        AssignReferenceArray(view, "tabInactiveTextures", tabInactiveTextures);
        AssignReference(view, "infoButtonImage", infoButton);
        AssignReference(view, "okButtonImage", okButton);
        AssignReference(view, "cancelButtonImage", cancelButton);
        AssignReference(view, "infoButton", infoButtonComponent);
        AssignReference(view, "okButton", okButtonComponent);
        AssignReference(view, "cancelButton", cancelButtonComponent);
        AssignReference(view, "missionSelectionRoot", missionSelection);
        AssignReference(view, "dropdownButtonImage", dropdownButton);
        AssignReference(view, "dropdownButton", dropdownButtonComponent);
        AssignReference(view, "targetLabelTextField", targetLabel);
        AssignReference(view, "selectedMissionImage", selectedMission);
        AssignReference(view, "selectedMissionNameTextField", selectedMissionName);
        AssignReference(view, "targetPreviewImage", targetPreview);
        AssignReference(view, "targetPreviewNameTextField", targetPreviewName);
        AssignReference(view, "dropdownRoot", dropdown);
        AssignReference(view, "dropdownFrameFillImage", dropdownFill);
        AssignReference(view, "dropdownFrameTopImage", dropdownTop);
        AssignReference(view, "dropdownFrameBottomImage", dropdownBottom);
        AssignReference(view, "dropdownFrameLeftImage", dropdownLeft);
        AssignReference(view, "dropdownFrameRightImage", dropdownRight);
        AssignReferenceArray(view, "dropdownBackgroundImages", dropdownBackgrounds);
        AssignReference(view, "dropdownScrollArea", dropdownScrollArea);
        AssignReference(view, "dropdownItemImageTemplate", dropdownItemImageTemplate);
        AssignReference(view, "dropdownItemTextTemplate", dropdownItemTextTemplate);
        AssignReference(view, "dropdownItemRowTemplate", dropdownItemRowTemplate);
        AssignReference(view, "dropdownItemImageAreaTemplate", dropdownItemImageAreaTemplate);
        AssignReference(view, "dropdownContentPaddingTemplate", dropdownContentPaddingTemplate);
        AssignReference(view, "personnelRoot", personnel);
        AssignReference(view, "agentsHeaderImage", agentsHeader);
        AssignReference(view, "decoysHeaderImage", decoysHeader);
        AssignReference(view, "moveRightButtonImage", moveRight);
        AssignReference(view, "moveLeftButtonImage", moveLeft);
        AssignReference(view, "moveRightButton", moveRightButton);
        AssignReference(view, "moveLeftButton", moveLeftButton);
        AssignReference(view, "agentsScrollArea", agentsScrollArea);
        AssignReference(view, "agentRowTemplate", agentTemplate);
        AssignReference(view, "decoysScrollArea", decoysScrollArea);
        AssignReference(view, "decoyRowTemplate", decoyTemplate);
        AssignReference(
            view,
            "missionBackgroundTexture",
            LoadTexture(_missionCreateMissionBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "personnelBackgroundTexture",
            LoadTexture(_missionCreatePersonnelBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "titleTexture",
            LoadTexture(PreviewTheme?.StrategyWindows?.MissionCreate?.TitleImagePath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(
            view,
            "infoButtonUpTexture",
            LoadTexture(_constructionInfoButtonPreviewPath)
        );
        AssignReference(
            view,
            "infoButtonDownTexture",
            LoadTexture(_constructionInfoButtonDownPreviewPath)
        );
        AssignReference(view, "okButtonUpTexture", LoadTexture(_constructionOkButtonPreviewPath));
        AssignReference(
            view,
            "okButtonDownTexture",
            LoadTexture(_constructionOkButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "cancelButtonUpTexture",
            LoadTexture(_constructionCancelButtonPreviewPath)
        );
        AssignReference(
            view,
            "cancelButtonDownTexture",
            LoadTexture(_constructionCancelButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "dropdownButtonUpTexture",
            LoadTexture(_constructionOpenButtonPreviewPath)
        );
        AssignReference(
            view,
            "dropdownButtonDownTexture",
            LoadTexture(_constructionOpenButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "dropdownBackgroundTexture",
            LoadTexture(_constructionDropdownBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "moveRightButtonUpTexture",
            LoadTexture(_missionCreateMoveRightButtonPreviewPath)
        );
        AssignReference(
            view,
            "moveRightButtonDownTexture",
            LoadTexture(_missionCreateMoveRightButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "moveLeftButtonUpTexture",
            LoadTexture(_missionCreateMoveLeftButtonPreviewPath)
        );
        AssignReference(
            view,
            "moveLeftButtonDownTexture",
            LoadTexture(_missionCreateMoveLeftButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "participantEnrouteBackgroundTexture",
            LoadTexture(_fleetPersonnelEnrouteBackgroundPreviewPath)
        );

        dropdown.gameObject.SetActive(false);
        personnel.gameObject.SetActive(false);
        view.enabled = true;
        window.SetActive(true);
        GameObject saved = SaveGeneratedPrefabAsset(window, _missionCreateWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<MissionCreateWindowView>();
    }

    private static MissionListRowView CreateMissionListRowTemplate(Transform parent)
    {
        GameObject row = new GameObject(
            "MissionListRowTemplate",
            typeof(RectTransform),
            typeof(MissionListRowView)
        );
        row.transform.SetParent(parent, false);
        MissionListRowView view = row.GetComponent<MissionListRowView>();
        SetSourceRect(row.GetComponent<RectTransform>(), 0, 2, 95, 50);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            row.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitArea.rectTransform, 0, 0, 95, 50);
        RawImage icon = CreateRawImage(
            "IconImage",
            row.transform,
            _facilityCardEntityPreviewPath,
            0,
            0
        );
        SetSourceRect(icon.rectTransform, 0, 0, 73, 48);
        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", row.transform);
        nameText.text = "Espionage";
        nameText.color = Color.white;
        nameText.fontSize = 10;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(nameText.rectTransform, 0, 0, 90, 42);
        RawImage selection = CreateRawImage(
            "SelectionImage",
            row.transform,
            PreviewTheme?.StrategyWindows?.Missions?.SelectionImagePath,
            0,
            0
        );

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "iconImage", icon);
        AssignReference(view, "nameTextField", nameText);
        AssignReference(view, "selectionImage", selection);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);
        return view;
    }

    private static MissionParticipantRowView CreateMissionParticipantRowTemplate(
        Transform parent,
        string name,
        int width,
        int height,
        int rowY,
        int entityX,
        int entityY,
        int entityWidth,
        int entityHeight,
        int nameX,
        int nameY,
        int nameWidth,
        int nameHeight
    )
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        MissionParticipantRowView view = row.AddComponent<MissionParticipantRowView>();
        SetSourceRect(row.GetComponent<RectTransform>(), 0, rowY, width, height);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            row.transform,
            _defensePersonnelBackgroundPreviewPath,
            entityX,
            entityY
        );
        SetSourceRect(background.rectTransform, entityX, entityY, entityWidth, entityHeight);
        RawImage entity = CreateRawImage(
            "EntityImage",
            row.transform,
            _facilityCardEntityPreviewPath,
            entityX,
            entityY
        );
        SetSourceRect(entity.rectTransform, entityX, entityY, entityWidth, entityHeight);
        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", row.transform);
        nameText.text = "Leia Organa";
        nameText.color = Color.white;
        nameText.fontSize = 10;
        nameText.alignment = TextAlignmentOptions.Top;
        SetSourceRect(nameText.rectTransform, nameX, nameY, nameWidth, nameHeight);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "entityImage", entity);
        AssignReference(view, "nameTextField", nameText);
        AssignReference(
            view,
            "backgroundTexture",
            LoadTexture(_defensePersonnelBackgroundPreviewPath)
        );
        view.enabled = true;
        return view;
    }

    private static SaveMenuWindowView BuildSaveMenuWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_saveMenuWindowPrefabPath));
        AssetDatabase.Refresh();

        GameObject window = new GameObject(
            "SaveMenuWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        SaveMenuWindowView view = window.AddComponent<SaveMenuWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 640, 480);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _saveMenuBackgroundPreviewPath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage cockpitButtonImage = CreateRawImage(
            "CockpitButtonImage",
            window.transform,
            _saveMenuCockpitButtonPreviewPath,
            76,
            381
        );
        Button cockpitButton = CreateButton(cockpitButtonImage);
        RawImage airlockButtonImage = CreateRawImage(
            "AirlockButtonImage",
            window.transform,
            _saveMenuAirlockButtonPreviewPath,
            248,
            381
        );
        Button airlockButton = CreateButton(airlockButtonImage);
        RawImage returnStrategyButtonImage = CreateRawImage(
            "ReturnStrategyButtonImage",
            window.transform,
            PreviewTheme?.SaveMenuReturnStrategyButtonImagePath,
            162,
            382
        );
        Button returnStrategyButton = CreateButton(returnStrategyButtonImage);
        RawImage musicButtonImage = CreateRawImage(
            "MusicButtonImage",
            window.transform,
            _saveMenuMusicButtonPreviewPath,
            352,
            76
        );
        Button musicButton = CreateButton(musicButtonImage);
        Slider musicSlider = CreateSaveMenuSlider(
            "MusicSlider",
            window.transform,
            393,
            134,
            out RawImage musicSliderImage
        );
        Slider sfxSlider = CreateSaveMenuSlider(
            "SfxSlider",
            window.transform,
            393,
            194,
            out RawImage sfxSliderImage
        );

        RawImage[] tacticalButtonImages = new RawImage[5];
        Button[] tacticalButtons = new Button[5];
        int[] tacticalButtonY = { 311, 338, 365, 392, 419 };
        for (int i = 0; i < tacticalButtons.Length; i++)
        {
            tacticalButtonImages[i] = CreateRawImage(
                $"TacticalOptionButtonImage{i}",
                window.transform,
                _saveMenuOptionButtonPreviewPath,
                357,
                tacticalButtonY[i]
            );
            tacticalButtons[i] = CreateButton(tacticalButtonImages[i]);
        }

        RawImage[] saveButtonImages = new RawImage[6];
        RawImage[] loadButtonImages = new RawImage[6];
        RawImage[] saveSlotFactionImages = new RawImage[6];
        Button[] saveButtons = new Button[6];
        Button[] loadButtons = new Button[6];
        TextMeshProUGUI[] saveSlotTexts = new TextMeshProUGUI[6];
        for (int i = 0; i < saveButtons.Length; i++)
        {
            int y = 81 + 42 * i;
            saveButtonImages[i] = CreateRawImage(
                $"SaveButtonImage{i}",
                window.transform,
                _saveMenuSaveButtonDisabledPreviewPath,
                34,
                y
            );
            saveButtons[i] = CreateButton(saveButtonImages[i]);
            loadButtonImages[i] = CreateRawImage(
                $"LoadButtonImage{i}",
                window.transform,
                _saveMenuLoadButtonDisabledPreviewPath,
                287,
                y
            );
            loadButtons[i] = CreateButton(loadButtonImages[i]);
            saveSlotFactionImages[i] = CreateRawImage(
                $"SaveSlotFactionImage{i}",
                window.transform,
                PreviewTheme?.SaveMenuSlotIconImagePath,
                85,
                82 + 42 * i
            );
            saveSlotFactionImages[i].raycastTarget = false;
            saveSlotTexts[i] = CreateSaveMenuText(
                $"SaveSlotTextField{i}",
                window.transform,
                "Empty Save Slot",
                120,
                82 + 42 * i,
                150,
                18,
                12,
                TextAlignmentOptions.TopLeft
            );
        }

        TextMeshProUGUI savedGamesTitle = CreateSaveMenuText(
            "SavedGamesTitleTextField",
            window.transform,
            "Saved Games",
            90,
            38,
            180,
            18,
            14,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI soundOptionsTitle = CreateSaveMenuText(
            "SoundOptionsTitleTextField",
            window.transform,
            "Sound Options",
            400,
            38,
            180,
            18,
            14,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI tacticalOptionsTitle = CreateSaveMenuText(
            "TacticalOptionsTitleTextField",
            window.transform,
            "Tactical Options",
            400,
            273,
            180,
            18,
            14,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI playMusic = CreateSaveMenuText(
            "PlayMusicTextField",
            window.transform,
            "Play Music",
            377,
            87,
            160,
            18,
            16,
            TextAlignmentOptions.TopLeft
        );
        TextMeshProUGUI playMusicState = CreateSaveMenuText(
            "PlayMusicStateTextField",
            window.transform,
            "ON",
            535,
            87,
            75,
            18,
            16,
            TextAlignmentOptions.TopRight
        );

        TextMeshProUGUI[] tacticalLabels = new TextMeshProUGUI[5];
        TextMeshProUGUI[] tacticalStates = new TextMeshProUGUI[5];
        string[] tacticalPreviewTexts =
        {
            "Show Starfield",
            "Show Planet",
            "Show Pyro",
            "High Detail",
            "Show Holocube",
        };
        int[] tacticalTextY = { 311, 338, 365, 392, 419 };
        for (int i = 0; i < tacticalLabels.Length; i++)
        {
            tacticalLabels[i] = CreateSaveMenuText(
                $"TacticalOptionTextField{i}",
                window.transform,
                tacticalPreviewTexts[i],
                395,
                tacticalTextY[i],
                160,
                18,
                13,
                TextAlignmentOptions.TopLeft
            );
            tacticalStates[i] = CreateSaveMenuText(
                $"TacticalOptionStateTextField{i}",
                window.transform,
                "ON",
                535,
                tacticalTextY[i],
                75,
                18,
                13,
                TextAlignmentOptions.TopRight
            );
        }

        TextMeshProUGUI version = CreateSaveMenuText(
            "VersionTextField",
            window.transform,
            "Version: Development",
            90,
            438,
            180,
            12,
            9,
            TextAlignmentOptions.Top
        );
        version.color = Color.black;

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "cockpitButtonImage", cockpitButtonImage);
        AssignReference(view, "airlockButtonImage", airlockButtonImage);
        AssignReference(view, "returnStrategyButtonImage", returnStrategyButtonImage);
        AssignReference(view, "musicButtonImage", musicButtonImage);
        AssignReference(view, "musicSliderImage", musicSliderImage);
        AssignReference(view, "sfxSliderImage", sfxSliderImage);
        AssignReference(view, "cockpitButton", cockpitButton);
        AssignReference(view, "airlockButton", airlockButton);
        AssignReference(view, "returnStrategyButton", returnStrategyButton);
        AssignReference(view, "musicButton", musicButton);
        AssignReference(view, "musicSlider", musicSlider);
        AssignReference(view, "sfxSlider", sfxSlider);
        AssignReferenceArray(view, "tacticalOptionButtonImages", tacticalButtonImages);
        AssignReferenceArray(view, "saveButtonImages", saveButtonImages);
        AssignReferenceArray(view, "loadButtonImages", loadButtonImages);
        AssignReferenceArray(view, "saveSlotFactionImages", saveSlotFactionImages);
        AssignReferenceArray(view, "tacticalOptionButtons", tacticalButtons);
        AssignReferenceArray(view, "saveButtons", saveButtons);
        AssignReferenceArray(view, "loadButtons", loadButtons);
        AssignReference(view, "savedGamesTitleTextField", savedGamesTitle);
        AssignReference(view, "soundOptionsTitleTextField", soundOptionsTitle);
        AssignReference(view, "tacticalOptionsTitleTextField", tacticalOptionsTitle);
        AssignReference(view, "playMusicTextField", playMusic);
        AssignReference(view, "playMusicStateTextField", playMusicState);
        AssignReferenceArray(view, "tacticalOptionTextFields", tacticalLabels);
        AssignReferenceArray(view, "tacticalOptionStateTextFields", tacticalStates);
        AssignReferenceArray(view, "saveSlotTextFields", saveSlotTexts);
        AssignReference(view, "versionTextField", version);
        AssignReference(
            view,
            "cockpitButtonUpTexture",
            LoadTexture(_saveMenuCockpitButtonPreviewPath)
        );
        AssignReference(
            view,
            "airlockButtonUpTexture",
            LoadTexture(_saveMenuAirlockButtonPreviewPath)
        );
        AssignReference(view, "musicButtonUpTexture", LoadTexture(_saveMenuMusicButtonPreviewPath));
        AssignReference(
            view,
            "musicButtonDownTexture",
            LoadTexture(_saveMenuMusicButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "optionButtonUpTexture",
            LoadTexture(_saveMenuOptionButtonPreviewPath)
        );
        AssignReference(
            view,
            "optionButtonDownTexture",
            LoadTexture(_saveMenuOptionButtonDownPreviewPath)
        );
        AssignReference(view, "saveButtonUpTexture", LoadTexture(_saveMenuSaveButtonPreviewPath));
        AssignReference(
            view,
            "saveButtonDownTexture",
            LoadTexture(_saveMenuSaveButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "saveButtonDisabledTexture",
            LoadTexture(_saveMenuSaveButtonDisabledPreviewPath)
        );
        AssignReference(view, "loadButtonUpTexture", LoadTexture(_saveMenuLoadButtonPreviewPath));
        AssignReference(
            view,
            "loadButtonDownTexture",
            LoadTexture(_saveMenuLoadButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "loadButtonDisabledTexture",
            LoadTexture(_saveMenuLoadButtonDisabledPreviewPath)
        );
        AssignReference(view, "sliderThumbTexture", LoadTexture(_saveMenuSliderPreviewPath));

        GameObject saved = SaveGeneratedPrefabAsset(window, _saveMenuWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<SaveMenuWindowView>();
    }

    private static void RegisterWindowPrefabsInRoot()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        if (root == null)
            throw new System.InvalidOperationException("Strategy view root prefab was not found.");

        StrategyWindowLayerView windowsView = root.GetComponentInChildren<StrategyWindowLayerView>(
            true
        );
        if (windowsView == null)
            throw new System.InvalidOperationException(
                "Strategy view root prefab has no window layer."
            );

        AssignWindowPrefabs(
            windowsView,
            LoadWindowPrefab<PlanetSystemWindowView>(_planetSystemWindowPrefabPath),
            LoadWindowPrefab<FacilityWindowView>(_facilityWindowPrefabPath),
            LoadWindowPrefab<DefenseWindowView>(_defenseWindowPrefabPath),
            LoadWindowPrefab<FleetWindowView>(_fleetWindowPrefabPath),
            LoadWindowPrefab<MissionsWindowView>(_missionsWindowPrefabPath),
            LoadWindowPrefab<ConstructionWindowView>(_constructionWindowPrefabPath),
            LoadWindowPrefab<MissionCreateWindowView>(_missionCreateWindowPrefabPath),
            LoadWindowPrefab<StatusWindowView>(_statusWindowPrefabPath),
            LoadWindowPrefab<MessagesWindowView>(_messagesWindowPrefabPath),
            LoadWindowPrefab<ConfirmDialogWindowView>(_confirmDialogWindowPrefabPath),
            LoadWindowPrefab<FinderWindowView>(_finderWindowPrefabPath),
            LoadWindowPrefab<EncyclopediaWindowView>(_encyclopediaWindowPrefabPath)
        );

        EditorUtility.SetDirty(windowsView);
        EditorUtility.SetDirty(root);
        PrefabUtility.SavePrefabAsset(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static T LoadWindowPrefab<T>(string path)
        where T : MonoBehaviour
    {
        return LoadPrefabComponent<T>(path);
    }

    private static T LoadPrefabComponent<T>(string path)
        where T : MonoBehaviour
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new MissingReferenceException($"Prefab asset is missing at {path}.");

        T component = prefab.GetComponent<T>();
        if (component == null)
            throw new MissingReferenceException(
                $"Prefab asset at {path} is missing {typeof(T).Name}."
            );

        return component;
    }

    private static StatusWindowView BuildStatusWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statusWindowPrefabPath));

        GameObject window = new GameObject(
            "StatusWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(StatusWindowView)
        );
        StatusWindowView view = window.GetComponent<StatusWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 379, 272);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _statusWindowBackgroundPreviewPath,
            0,
            0
        );

        TextMeshProUGUI header = CreateTextLabel("HeaderTextField", window.transform);
        header.text = "Planet Status";
        header.color = Color.white;
        header.fontSize = 14;
        header.alignment = TextAlignmentOptions.Top;
        SetSourceRect(header.rectTransform, 20, 21, 200, 18);

        RectTransform images = CreateChildLayer("Images", window.transform);
        SetSourceRect(images, 0, 0, 379, 272);
        RectTransform statusImageAreaTemplate = CreateSourceRectLayer(
            "StatusImageAreaTemplate",
            images,
            126,
            88
        );
        SetSourceRect(statusImageAreaTemplate, 244, 20, 126, 88);
        statusImageAreaTemplate.gameObject.SetActive(false);
        background.raycastTarget = true;

        RawImage statusImageTemplate = CreateRawButton(
            "StatusImageTemplate",
            images,
            _facilityCardEntityPreviewPath
        );
        SetSourceRect(statusImageTemplate.rectTransform, 244, 20, 61, 25);
        statusImageTemplate.gameObject.SetActive(false);

        TextMeshProUGUI labelTemplate = CreateTextLabel("LabelTextTemplate", window.transform);
        labelTemplate.text = "Corellia";
        labelTemplate.color = Color.white;
        labelTemplate.fontSize = 14;
        labelTemplate.alignment = TextAlignmentOptions.Top;
        SetSourceRect(labelTemplate.rectTransform, 242, 140, 126, 17);
        labelTemplate.gameObject.SetActive(false);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            window.transform,
            "RowsScrollArea",
            15,
            45,
            211,
            205,
            0,
            0,
            198,
            205,
            198,
            0,
            13,
            205,
            out RectTransform rowsContent
        );

        TextMeshProUGUI leftRowTemplate = CreateTextLabel("LeftRowTextTemplate", rowsContent);
        leftRowTemplate.text = "Location:";
        leftRowTemplate.color = Color.white;
        leftRowTemplate.fontSize = 10;
        leftRowTemplate.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(leftRowTemplate.rectTransform, 0, 0, 99, 14);
        leftRowTemplate.gameObject.SetActive(false);

        TextMeshProUGUI rightRowTemplate = CreateTextLabel("RightRowTextTemplate", rowsContent);
        rightRowTemplate.text = "Corellia";
        rightRowTemplate.color = Color.white;
        rightRowTemplate.fontSize = 10;
        rightRowTemplate.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(rightRowTemplate.rectTransform, 104, 0, 89, 14);
        rightRowTemplate.gameObject.SetActive(false);

        RawImage infoButton = CreateRawImage(
            "InfoButtonImage",
            window.transform,
            _statusInfoButtonPreviewPath,
            257,
            217
        );
        Button infoButtonComponent = CreateButton(infoButton);
        RawImage closeButton = CreateRawImage(
            "CloseButtonImage",
            window.transform,
            _statusCloseButtonPreviewPath,
            322,
            217
        );
        Button closeButtonComponent = CreateButton(closeButton);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "headerTextField", header);
        AssignReference(view, "imagesRoot", images);
        AssignReference(view, "statusImageTemplate", statusImageTemplate);
        AssignReference(view, "statusImageAreaTemplate", statusImageAreaTemplate);
        AssignReference(view, "labelTextTemplate", labelTemplate);
        AssignReference(view, "rowsScrollArea", rowsScrollArea);
        AssignReference(view, "leftRowTextTemplate", leftRowTemplate);
        AssignReference(view, "rightRowTextTemplate", rightRowTemplate);
        AssignReference(view, "infoButtonImage", infoButton);
        AssignReference(view, "closeButtonImage", closeButton);
        AssignReference(view, "infoButton", infoButtonComponent);
        AssignReference(view, "closeButton", closeButtonComponent);
        AssignReference(view, "infoButtonUpTexture", LoadTexture(_statusInfoButtonPreviewPath));
        AssignReference(
            view,
            "infoButtonDisabledTexture",
            LoadTexture(_statusInfoButtonDisabledPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_statusCloseButtonPreviewPath));

        GameObject saved = SaveGeneratedPrefabAsset(window, _statusWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<StatusWindowView>();
    }

    private static FinderWindowView BuildFinderWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_finderWindowPrefabPath));

        GameObject window = new GameObject(
            "FinderWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(FinderWindowView)
        );
        FinderWindowView view = window.GetComponent<FinderWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);

        RawImage background = CreateRawButton(
            "BackgroundImage",
            window.transform,
            "Art/UI/StrategyView/ui_strategyview_finder_window_system_finder_background"
        );
        SetSourceRect(background.rectTransform, 12, 13, 400, 306);
        background.raycastTarget = true;

        RawImage overlay = CreateRawButton(
            "OverlayFrameImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Finder?.OverlayFrameImagePath
        );
        SetSourceRect(overlay.rectTransform, 0, 0, 470, 331);
        RawImage strip = CreateRawButton(
            "ButtonStripImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Finder?.TwoButtonStripImagePath
        );
        SetSourceRect(strip.rectTransform, 412, 0, 58, 330);

        RectTransform buttons = CreateSourceRectLayer("Buttons", window.transform, 470, 331);
        List<RawImage> upperButtonImages = CreateUtilityDialogButtonSlots(
            buttons,
            "Upper",
            true,
            true
        );
        List<Button> upperButtons = CreateButtons(upperButtonImages);
        List<RawImage> twoButtonImages = CreateUtilityDialogButtonSlots(
            buttons,
            "Two",
            false,
            false
        );
        List<Button> twoButtons = CreateButtons(twoButtonImages);
        List<RawImage> fourButtonImages = CreateUtilityDialogButtonSlots(
            buttons,
            "Four",
            false,
            true
        );
        List<Button> fourButtons = CreateButtons(fourButtonImages);

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", window.transform);
        title.text = "Planetary System Finder";
        title.color = Color.white;
        title.fontSize = 13;
        title.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(title.rectTransform, 23, 13, 260, 17);

        TextMeshProUGUI label = CreateTextLabel("LabelTextField", window.transform);
        label.text = "System Name";
        label.color = Color.white;
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(label.rectTransform, 37, 48, 220, 16);

        RectTransform tabs = CreateSourceRectLayer("Tabs", window.transform, 470, 331);
        string[] finderTabPreviewPaths =
        {
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_all_systems_button_up",
            GetPreviewTheme(0)?.StrategyWindows?.Finder?.SystemsButton?.UpImagePath,
            GetPreviewTheme(1)?.StrategyWindows?.Finder?.SystemsButton?.UpImagePath,
            "Art/UI/StrategyView/ui_strategyview_finder_window_neutral_systems_button_up",
            "Art/UI/StrategyView/ui_strategyview_finder_window_unexplored_systems_button_up",
        };
        List<RawImage> tabSlots = new List<RawImage>();
        for (int i = 0; i < finderTabPreviewPaths.Length; i++)
        {
            RawImage image = CreateRawButton($"TabImage{i}", tabs, finderTabPreviewPaths[i]);
            SetSourceRect(image.rectTransform, 36 + i * 52, 78, 49, 41);
            tabSlots.Add(image);
        }
        List<Button> tabButtons = CreateButtons(tabSlots);

        RectTransform layoutTemplates = CreateChildLayer("LayoutTemplates", window.transform);
        layoutTemplates.gameObject.SetActive(false);
        List<RectTransform> defaultTabSlotTemplates = CreateFinderTabSlotTemplates(
            layoutTemplates,
            "DefaultTabSlotTemplate",
            78
        );
        List<RectTransform> compactTabSlotTemplates = CreateFinderTabSlotTemplates(
            layoutTemplates,
            "CompactTabSlotTemplate",
            72
        );

        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", window.transform);
        tabTitle.text = "All Systems";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 12;
        tabTitle.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(tabTitle.rectTransform, 40, 120, 250, 16);

        TextMeshProUGUI compactTabTitle = CreateTextLabel(
            "CompactTabTitleTextTemplate",
            layoutTemplates
        );
        compactTabTitle.text = "All Personnel";
        compactTabTitle.color = Color.white;
        compactTabTitle.fontSize = 12;
        compactTabTitle.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(compactTabTitle.rectTransform, 40, 115, 250, 16);
        compactTabTitle.gameObject.SetActive(false);

        RectTransform defaultRowsClipTemplate = CreateChildLayer(
            "DefaultRowsClipTemplate",
            layoutTemplates
        );
        SetSourceRect(defaultRowsClipTemplate, 38, 137, 330, 167);
        RectTransform troopRowsClipTemplate = CreateChildLayer(
            "TroopRowsClipTemplate",
            layoutTemplates
        );
        SetSourceRect(troopRowsClipTemplate, 38, 143, 330, 161);
        RectTransform personnelRowsClipTemplate = CreateChildLayer(
            "PersonnelRowsClipTemplate",
            layoutTemplates
        );
        SetSourceRect(personnelRowsClipTemplate, 38, 132, 330, 172);
        RectTransform personnelPanelRowsClipTemplate = CreateChildLayer(
            "PersonnelPanelRowsClipTemplate",
            layoutTemplates
        );
        SetSourceRect(personnelPanelRowsClipTemplate, 38, 142, 330, 162);

        RectTransform rowsScrollPaddingTemplate = CreateChildLayer(
            "RowsScrollPaddingTemplate",
            layoutTemplates
        );
        SetSourceRect(rowsScrollPaddingTemplate, 0, 0, 330, 5);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            window.transform,
            "RowsScrollArea",
            38,
            137,
            348,
            167,
            0,
            0,
            330,
            167,
            335,
            1,
            13,
            165,
            out RectTransform rowsContent
        );
        ConfigureVerticalContent(rowsContent);

        FinderWindowRowView rowTemplate = CreateFinderRowTemplate(
            rowsContent,
            "RowTemplate",
            "Corellia",
            13,
            4,
            17,
            188
        );
        FinderWindowRowView personnelRowTemplate = CreateFinderRowTemplate(
            rowsContent,
            "PersonnelRowTemplate",
            "Leia - Corellia",
            11,
            6,
            15,
            188
        );
        FinderWindowRowView personnelPanelRowTemplate = CreateFinderRowTemplate(
            rowsContent,
            "PersonnelPanelRowTemplate",
            "Leia - Corellia",
            13,
            4,
            17,
            216
        );

        RectTransform defaultScrollbarTemplate = CreateChildLayer(
            "DefaultScrollbarTemplate",
            layoutTemplates
        );
        SetSourceRect(defaultScrollbarTemplate, 373, 138, 13, 165);
        RectTransform compactScrollbarTemplate = CreateChildLayer(
            "CompactScrollbarTemplate",
            layoutTemplates
        );
        SetSourceRect(compactScrollbarTemplate, 373, 143, 13, 160);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "overlayFrameImage", overlay);
        AssignReference(view, "buttonStripImage", strip);
        AssignReferenceArray(view, "upperButtonImages", upperButtonImages);
        AssignReferenceArray(view, "upperButtons", upperButtons);
        AssignReferenceArray(view, "twoButtonImages", twoButtonImages);
        AssignReferenceArray(view, "twoButtons", twoButtons);
        AssignReferenceArray(view, "fourButtonImages", fourButtonImages);
        AssignReferenceArray(view, "fourButtons", fourButtons);
        AssignReference(view, "titleTextField", title);
        AssignReference(view, "labelTextField", label);
        AssignReferenceArray(view, "tabImageSlots", tabSlots);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReferenceArray(view, "defaultTabSlotTemplates", defaultTabSlotTemplates);
        AssignReferenceArray(view, "compactTabSlotTemplates", compactTabSlotTemplates);
        AssignReference(view, "tabTitleTextField", tabTitle);
        AssignReference(view, "compactTabTitleTextTemplate", compactTabTitle);
        AssignReference(view, "rowsScrollArea", rowsScrollArea);
        AssignReference(view, "defaultRowsClipTemplate", defaultRowsClipTemplate);
        AssignReference(view, "troopRowsClipTemplate", troopRowsClipTemplate);
        AssignReference(view, "personnelRowsClipTemplate", personnelRowsClipTemplate);
        AssignReference(view, "personnelPanelRowsClipTemplate", personnelPanelRowsClipTemplate);
        AssignReference(view, "rowsScrollPaddingTemplate", rowsScrollPaddingTemplate);
        AssignReference(view, "rowTemplate", rowTemplate);
        AssignReference(view, "personnelRowTemplate", personnelRowTemplate);
        AssignReference(view, "personnelPanelRowTemplate", personnelPanelRowTemplate);
        AssignReference(view, "defaultScrollbarTemplate", defaultScrollbarTemplate);
        AssignReference(view, "compactScrollbarTemplate", compactScrollbarTemplate);
        AssignReference(
            view,
            "allSystemsButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_all_systems_button_up")
        );
        AssignReference(
            view,
            "allSystemsButtonDownTexture",
            LoadStrategyViewTexture(
                "ui_strategyview_encyclopedia_window_all_systems_button_pressed"
            )
        );
        AssignReference(
            view,
            "unexploredSystemsButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_finder_window_unexplored_systems_button_up")
        );
        AssignReference(
            view,
            "unexploredSystemsButtonDownTexture",
            LoadStrategyViewTexture(
                "ui_strategyview_finder_window_unexplored_systems_button_pressed"
            )
        );
        AssignReference(
            view,
            "systemFinderBackgroundTexture",
            LoadStrategyViewTexture("ui_strategyview_finder_window_system_finder_background")
        );

        buttons.SetAsLastSibling();
        GameObject saved = SaveGeneratedPrefabAsset(window, _finderWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<FinderWindowView>();
    }

    private static List<RectTransform> CreateFinderTabSlotTemplates(
        Transform parent,
        string namePrefix,
        int y
    )
    {
        List<RectTransform> slots = new List<RectTransform>();
        for (int i = 0; i < 2; i++)
        {
            RectTransform slot = CreateChildLayer($"{namePrefix}{i}", parent);
            SetSourceRect(slot, 36 + i * 52, y, 49, 41);
            slots.Add(slot);
        }

        return slots;
    }

    private static FinderWindowRowView CreateFinderRowTemplate(
        Transform parent,
        string name,
        string previewName,
        int nameFontSize,
        int nameY,
        int nameHeight,
        int firstCountX
    )
    {
        RawImage hitArea = CreateRawButton(name, parent);
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        SetSourceRect(hitArea.rectTransform, 0, 0, 330, 20);
        FinderWindowRowView row = hitArea.gameObject.AddComponent<FinderWindowRowView>();

        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", hitArea.transform);
        nameText.text = previewName;
        nameText.color = Color.gray;
        nameText.fontSize = nameFontSize;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(nameText.rectTransform, 5, nameY, 320, nameHeight);

        List<TextMeshProUGUI> countTextFields = new List<TextMeshProUGUI>();
        for (int i = 0; i < 5; i++)
        {
            TextMeshProUGUI countText = CreateTextLabel($"CountTextField{i}", hitArea.transform);
            countText.text = "1";
            countText.color = Color.white;
            countText.fontSize = 11;
            countText.alignment = TextAlignmentOptions.TopRight;
            SetSourceRect(countText.rectTransform, firstCountX + i * 28, 6, 22, 15);
            countTextFields.Add(countText);
        }

        AssignReference(row, "hitAreaImage", hitArea);
        AssignReference(row, "nameTextField", nameText);
        AssignReferenceArray(row, "countTextFields", countTextFields);
        AddTemplateLayoutElement(row);
        row.gameObject.SetActive(false);
        return row;
    }

    private static MessagesWindowView BuildMessagesWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_messagesWindowPrefabPath));

        GameObject window = new GameObject(
            "MessagesWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(MessagesWindowView)
        );
        MessagesWindowView view = window.GetComponent<MessagesWindowView>();
        MessagesWindowTheme messagesTheme = PreviewTheme?.StrategyWindows?.Messages;
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);

        RawImage background = CreateRawButton(
            "BackgroundImage",
            window.transform,
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_background"
        );
        SetSourceRect(background.rectTransform, 12, 13, 400, 306);
        background.raycastTarget = true;

        RawImage overlay = CreateRawButton(
            "OverlayFrameImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.OverlayFrameImagePath
        );
        SetSourceRect(overlay.rectTransform, 0, 0, 470, 331);
        RawImage buttonStrip = CreateRawButton(
            "ButtonStripImage",
            window.transform,
            messagesTheme?.ButtonStripImagePath
        );
        SetSourceRect(buttonStrip.rectTransform, 412, 0, 58, 330);

        RectTransform commandButtons = CreateSourceRectLayer(
            "CommandButtons",
            window.transform,
            470,
            331
        );
        RawImage closeButtonImage = CreateRawButton(
            "CloseButtonImage",
            commandButtons,
            messagesTheme?.CloseButton?.UpImagePath
        );
        SetSourceRect(
            closeButtonImage.rectTransform,
            messagesTheme?.CloseButton?.SourceLayout,
            423,
            25,
            32,
            31
        );
        Button closeButton = CreateButton(closeButtonImage);
        RawImage displayButtonImage = CreateRawButton(
            "DisplayButtonImage",
            commandButtons,
            messagesTheme?.DisplayButton?.UpImagePath
        );
        SetSourceRect(
            displayButtonImage.rectTransform,
            messagesTheme?.DisplayButton?.SourceLayout,
            423,
            93,
            32,
            31
        );
        Button displayButton = CreateButton(displayButtonImage);
        RawImage indexButtonImage = CreateRawButton(
            "IndexButtonImage",
            commandButtons,
            messagesTheme?.IndexButton?.UpImagePath
        );
        SetSourceRect(
            indexButtonImage.rectTransform,
            messagesTheme?.IndexButton?.SourceLayout,
            423,
            93,
            32,
            31
        );
        Button indexButton = CreateButton(indexButtonImage);
        RawImage signalButtonImage = CreateRawButton(
            "SignalButtonImage",
            commandButtons,
            messagesTheme?.SignalButton?.UpImagePath
        );
        SetSourceRect(
            signalButtonImage.rectTransform,
            messagesTheme?.SignalButton?.SourceLayout,
            423,
            147,
            32,
            31
        );
        Button signalButton = CreateButton(signalButtonImage);
        RawImage signalTargetButtonImage = CreateRawButton(
            "SignalTargetButtonImage",
            commandButtons,
            messagesTheme?.SignalTargetButton?.UpImagePath
        );
        SetSourceRect(
            signalTargetButtonImage.rectTransform,
            messagesTheme?.SignalTargetButton?.SourceLayout,
            423,
            201,
            32,
            31
        );
        Button signalTargetButton = CreateButton(signalTargetButtonImage);
        RawImage chatCommandButtonImage = CreateRawButton(
            "ChatCommandButtonImage",
            commandButtons,
            messagesTheme?.ChatCommandButton?.UpImagePath
        );
        SetSourceRect(
            chatCommandButtonImage.rectTransform,
            messagesTheme?.ChatCommandButton?.SourceLayout,
            423,
            255,
            32,
            31
        );
        Button chatCommandButton = CreateButton(chatCommandButtonImage);

        RectTransform tabs = CreateSourceRectLayer("Tabs", window.transform, 470, 331);
        string[] messageTabPreviewPaths =
        {
            "Art/UI/StrategyView/ui_strategyview_messages_window_all_button_up",
            PreviewTheme?.StrategyWindows?.Messages?.SupportButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Messages?.FleetButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Messages?.MissionsButton?.UpImagePath,
            "Art/UI/StrategyView/ui_strategyview_messages_window_resource_button_up",
            "Art/UI/StrategyView/ui_strategyview_messages_window_manufacturing_button_up",
            "Art/UI/StrategyView/ui_strategyview_messages_window_defense_button_up",
            "Art/UI/StrategyView/ui_strategyview_messages_window_conflict_button_up",
            "Art/UI/StrategyView/ui_strategyview_messages_window_chat_button_up",
            PreviewTheme?.StrategyWindows?.Messages?.AdviceButton?.UpImagePath,
        };
        int[] messageTabSourceX = { 0, 38, 76, 114, 151, 189, 227, 263, 301, 338 };
        int[] messageTabSourceWidth = { 36, 36, 36, 36, 36, 36, 36, 36, 36, 37 };
        List<RawImage> tabSlots = new List<RawImage>();
        for (int i = 0; i < messageTabPreviewPaths.Length; i++)
        {
            RawImage image = CreateRawButton($"TabImage{i}", tabs, messageTabPreviewPaths[i]);
            SetSourceRect(
                image.rectTransform,
                22 + messageTabSourceX[i],
                46,
                messageTabSourceWidth[i],
                41
            );
            tabSlots.Add(image);
        }
        List<Button> tabButtons = CreateButtons(tabSlots);

        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", window.transform);
        tabTitle.text = "All Messages";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 14;
        tabTitle.alignment = TextAlignmentOptions.BottomLeft;
        SetSourceRect(tabTitle.rectTransform, 35, 88, 240, 18);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            window.transform,
            "RowsScrollArea",
            25,
            108,
            369,
            194,
            0,
            0,
            356,
            194,
            356,
            0,
            13,
            194,
            out RectTransform rowsContent
        );
        ConfigureVerticalContent(rowsContent);

        RawImage rowHitArea = CreateRawButton("RowTemplate", rowsContent);
        rowHitArea.color = Color.clear;
        rowHitArea.raycastTarget = true;
        SetSourceRect(rowHitArea.rectTransform, 0, 0, 373, 21);
        MessageWindowRowView rowTemplate =
            rowHitArea.gameObject.AddComponent<MessageWindowRowView>();
        RawImage rowSelection = CreateRawButton("SelectionImage", rowHitArea.transform);
        SetSourceRect(rowSelection.rectTransform, 0, 0, 356, 21);
        RawImage rowIcon = CreateRawButton("IconImage", rowHitArea.transform);
        SetSourceRect(rowIcon.rectTransform, 1, 1, 27, 22);
        TextMeshProUGUI rowHeader = CreateTextLabel("HeaderTextField", rowHitArea.transform);
        rowHeader.text = "Message";
        rowHeader.color = Color.gray;
        rowHeader.fontSize = 11;
        rowHeader.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(rowHeader.rectTransform, 35, 3, 330, 16);
        AssignReference(rowTemplate, "hitAreaImage", rowHitArea);
        AssignReference(rowTemplate, "selectionImage", rowSelection);
        AssignReference(rowTemplate, "iconImage", rowIcon);
        AssignReference(rowTemplate, "headerTextField", rowHeader);
        AddTemplateLayoutElement(rowTemplate);
        rowTemplate.gameObject.SetActive(false);

        RawImage selectAll = CreateRawButton("SelectAllButtonImage", window.transform);
        SetSourceRect(selectAll.rectTransform, 282, 87, 56, 20);
        Button selectAllButton = CreateButton(selectAll);
        RawImage removeSelected = CreateRawButton("RemoveSelectedButtonImage", window.transform);
        SetSourceRect(removeSelected.rectTransform, 340, 87, 56, 20);
        Button removeSelectedButton = CreateButton(removeSelected);

        RawImage detailStrip = CreateRawButton("DetailStripImage", window.transform);
        SetSourceRect(detailStrip.rectTransform, 12, 14, 400, 18);
        RawImage detailCard = CreateRawButton("DetailCardImage", window.transform);
        SetSourceRect(detailCard.rectTransform, 12, 33, 400, 200);
        RawImage detailOverlay = CreateRawButton("DetailOverlayImage", window.transform);
        SetSourceRect(detailOverlay.rectTransform, 12, 33, 400, 200);
        detailOverlay.raycastTarget = false;
        RawImage detailBody = CreateRawButton("DetailBodyImage", window.transform);
        SetSourceRect(detailBody.rectTransform, 12, 232, 400, 87);
        TextMeshProUGUI detailHeader = CreateTextLabel("DetailHeaderTextField", window.transform);
        detailHeader.text = "Message";
        detailHeader.color = Color.white;
        detailHeader.fontSize = 13;
        detailHeader.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(detailHeader.rectTransform, 40, 16, 320, 18);
        RawImage detailNext = CreateRawButton("DetailNextButtonImage", window.transform);
        SetSourceRect(detailNext.rectTransform, 367, 15, 19, 15);
        Button detailNextButton = CreateButton(detailNext);
        RawImage detailPrevious = CreateRawButton("DetailPreviousButtonImage", window.transform);
        SetSourceRect(detailPrevious.rectTransform, 390, 15, 19, 15);
        Button detailPreviousButton = CreateButton(detailPrevious);

        ScrollAreaView detailLinesScrollArea = CreateScrollAreaView(
            window.transform,
            "DetailLinesScrollArea",
            17,
            234,
            395,
            80,
            0,
            0,
            382,
            80,
            382,
            0,
            13,
            80,
            out RectTransform detailLinesContent
        );
        TextMeshProUGUI detailLineTemplate = CreateTextLabel(
            "DetailLineTextTemplate",
            detailLinesContent
        );
        detailLineTemplate.text = "Message text";
        detailLineTemplate.color = Color.white;
        detailLineTemplate.fontSize = 13;
        detailLineTemplate.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(detailLineTemplate.rectTransform, 0, 0, 382, 15);
        detailLineTemplate.gameObject.SetActive(false);

        buttonStrip.transform.SetAsLastSibling();
        commandButtons.SetAsLastSibling();

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "overlayFrameImage", overlay);
        AssignReference(view, "buttonStripImage", buttonStrip);
        AssignReference(view, "closeButtonImage", closeButtonImage);
        AssignReference(view, "displayButtonImage", displayButtonImage);
        AssignReference(view, "indexButtonImage", indexButtonImage);
        AssignReference(view, "signalButtonImage", signalButtonImage);
        AssignReference(view, "signalTargetButtonImage", signalTargetButtonImage);
        AssignReference(view, "chatCommandButtonImage", chatCommandButtonImage);
        AssignReference(view, "closeButton", closeButton);
        AssignReference(view, "displayButton", displayButton);
        AssignReference(view, "indexButton", indexButton);
        AssignReference(view, "signalButton", signalButton);
        AssignReference(view, "signalTargetButton", signalTargetButton);
        AssignReference(view, "chatCommandButton", chatCommandButton);
        AssignReferenceArray(view, "tabImageSlots", tabSlots);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "tabTitleTextField", tabTitle);
        AssignReference(view, "rowsScrollArea", rowsScrollArea);
        AssignReference(view, "rowTemplate", rowTemplate);
        AssignReference(view, "selectAllButtonImage", selectAll);
        AssignReference(view, "removeSelectedButtonImage", removeSelected);
        AssignReference(view, "selectAllButton", selectAllButton);
        AssignReference(view, "removeSelectedButton", removeSelectedButton);
        AssignReference(view, "detailStripImage", detailStrip);
        AssignReference(view, "detailBodyImage", detailBody);
        AssignReference(view, "detailCardImage", detailCard);
        AssignReference(view, "detailOverlayImage", detailOverlay);
        AssignReference(view, "detailHeaderTextField", detailHeader);
        AssignReference(view, "detailNextButtonImage", detailNext);
        AssignReference(view, "detailPreviousButtonImage", detailPrevious);
        AssignReference(view, "detailNextButton", detailNextButton);
        AssignReference(view, "detailPreviousButton", detailPreviousButton);
        AssignReference(view, "detailLinesScrollArea", detailLinesScrollArea);
        AssignReference(view, "detailLineTextTemplate", detailLineTemplate);
        AssignReference(
            view,
            "backgroundTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_background")
        );
        AssignReference(
            view,
            "allButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_all_button_up")
        );
        AssignReference(
            view,
            "allButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_all_button_pressed")
        );
        AssignReference(
            view,
            "resourceButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_resource_button_up")
        );
        AssignReference(
            view,
            "resourceButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_resource_button_pressed")
        );
        AssignReference(
            view,
            "chatButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_chat_button_up")
        );
        AssignReference(
            view,
            "chatButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_chat_button_pressed")
        );
        AssignReference(
            view,
            "manufacturingButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_manufacturing_button_up")
        );
        AssignReference(
            view,
            "manufacturingButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_manufacturing_button_pressed")
        );
        AssignReference(
            view,
            "conflictButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_conflict_button_up")
        );
        AssignReference(
            view,
            "conflictButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_conflict_button_pressed")
        );
        AssignReference(
            view,
            "defenseButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_defense_button_up")
        );
        AssignReference(
            view,
            "defenseButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_defense_button_pressed")
        );
        AssignReference(
            view,
            "selectAllButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_select_all_button_up")
        );
        AssignReference(
            view,
            "selectAllButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_select_all_button_pressed")
        );
        AssignReference(
            view,
            "removeSelectedButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_remove_selected_button_up")
        );
        AssignReference(
            view,
            "removeSelectedButtonDownTexture",
            LoadStrategyViewTexture(
                "ui_strategyview_messages_window_remove_selected_button_pressed"
            )
        );
        AssignReference(
            view,
            "previousButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_previous_button_up")
        );
        AssignReference(
            view,
            "previousButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_previous_button_pressed")
        );
        AssignReference(
            view,
            "previousButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_previous_button_disabled")
        );
        AssignReference(
            view,
            "detailBodyTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_detail_body")
        );
        AssignReference(
            view,
            "detailStripTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_detail_strip")
        );
        AssignReference(
            view,
            "nextButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_next_button_up")
        );
        AssignReference(
            view,
            "nextButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_next_button_pressed")
        );
        AssignReference(
            view,
            "nextButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_next_button_disabled")
        );
        AssignReference(
            view,
            "resourceIconTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_resource_icon")
        );
        AssignReference(
            view,
            "conflictIconTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_conflict_icon")
        );

        GameObject saved = SaveGeneratedPrefabAsset(window, _messagesWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<MessagesWindowView>();
    }

    private static EncyclopediaWindowView BuildEncyclopediaWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_encyclopediaWindowPrefabPath));

        GameObject window = new GameObject(
            "EncyclopediaWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(EncyclopediaWindowView)
        );
        EncyclopediaWindowView view = window.GetComponent<EncyclopediaWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);

        RawImage background = CreateRawButton(
            "BackgroundImage",
            window.transform,
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_background"
        );
        SetSourceRect(background.rectTransform, 12, 13, 400, 306);
        background.raycastTarget = true;

        RawImage overlay = CreateRawButton(
            "OverlayFrameImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.OverlayFrameImagePath
        );
        SetSourceRect(overlay.rectTransform, 0, 0, 470, 331);
        RawImage strip = CreateRawButton(
            "ButtonStripImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.ButtonStripImagePath
        );
        SetSourceRect(strip.rectTransform, 412, 0, 58, 330);

        RectTransform buttons = CreateSourceRectLayer("Buttons", window.transform, 470, 331);
        List<RawImage> upperButtonImages = CreateEncyclopediaDialogButtonSlots(
            buttons,
            "Upper",
            true
        );
        List<Button> upperButtons = CreateButtons(upperButtonImages);
        List<RawImage> twoButtonImages = CreateUtilityDialogButtonSlots(
            buttons,
            "Two",
            false,
            false
        );
        List<Button> twoButtons = CreateButtons(twoButtonImages);
        List<RawImage> fourButtonImages = CreateEncyclopediaDialogButtonSlots(
            buttons,
            "Four",
            false
        );
        List<Button> fourButtons = CreateButtons(fourButtonImages);

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", window.transform);
        title.text = "Galactic Encyclopedia";
        title.color = Color.white;
        title.fontSize = 13;
        title.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(title.rectTransform, 100, 14, 260, 17);
        TextMeshProUGUI topic = CreateTextLabel("TopicLabelTextField", window.transform);
        topic.text = "Topic";
        topic.color = Color.white;
        topic.fontSize = 12;
        topic.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(topic.rectTransform, 37, 48, 220, 16);

        RectTransform tabs = CreateSourceRectLayer("Tabs", window.transform, 470, 331);
        string[] encyclopediaTabPreviewPaths =
        {
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_all_systems_button_up",
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_system_button_up",
            PreviewTheme?.StrategyWindows?.Encyclopedia?.ShipButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.FacilityButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.MissionsButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.TroopButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.PersonnelButton?.UpImagePath,
        };
        string[] encyclopediaTabPreviewDownPaths =
        {
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_all_systems_button_pressed",
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_system_button_pressed",
            PreviewTheme?.StrategyWindows?.Encyclopedia?.ShipButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.FacilityButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.MissionsButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.TroopButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.PersonnelButton?.DownImagePath,
        };
        List<RawImage> tabSlots = new List<RawImage>();
        for (int i = 0; i < encyclopediaTabPreviewPaths.Length; i++)
        {
            RawImage image = CreateRawButton($"TabImage{i}", tabs, encyclopediaTabPreviewPaths[i]);
            Texture upTexture = image.texture;
            Texture downTexture = LoadTexture(encyclopediaTabPreviewDownPaths[i]);
            int width = Mathf.Max(upTexture?.width ?? 49, downTexture?.width ?? 49);
            int height = Mathf.Max(upTexture?.height ?? 41, downTexture?.height ?? 41);
            SetSourceRect(image.rectTransform, 36 + i * 52, 78, width, height);
            tabSlots.Add(image);
        }
        List<Button> tabButtons = CreateButtons(tabSlots);
        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", window.transform);
        tabTitle.text = "All Databases";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 12;
        tabTitle.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(tabTitle.rectTransform, 40, 120, 330, 16);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            window.transform,
            "RowsScrollArea",
            38,
            137,
            348,
            167,
            0,
            0,
            330,
            167,
            335,
            1,
            13,
            165,
            out RectTransform rowsContent
        );
        ConfigureVerticalContent(rowsContent);
        RawImage rowHitArea = CreateRawButton("RowTemplate", rowsContent);
        rowHitArea.color = Color.clear;
        rowHitArea.raycastTarget = true;
        SetSourceRect(rowHitArea.rectTransform, 0, 0, 330, 20);
        EncyclopediaWindowRowView rowTemplate =
            rowHitArea.gameObject.AddComponent<EncyclopediaWindowRowView>();
        TextMeshProUGUI rowName = CreateTextLabel("NameTextField", rowHitArea.transform);
        rowName.text = "Database Entry";
        rowName.color = Color.gray;
        rowName.fontSize = 13;
        rowName.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(rowName.rectTransform, 5, 4, 320, 16);
        AssignReference(rowTemplate, "hitAreaImage", rowHitArea);
        AssignReference(rowTemplate, "nameTextField", rowName);
        AddTemplateLayoutElement(rowTemplate);
        rowTemplate.gameObject.SetActive(false);
        TextMeshProUGUI rowTextTemplate = CreateTextLabel("RowTextTemplate", rowsContent);
        rowTextTemplate.text = "Database Entry";
        rowTextTemplate.color = Color.gray;
        rowTextTemplate.fontSize = 13;
        rowTextTemplate.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(rowTextTemplate.rectTransform, 5, 4, 320, 16);
        rowTextTemplate.gameObject.SetActive(false);

        RawImage detailBackground = CreateRawButton(
            "DetailBackgroundImage",
            window.transform,
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_topic_background"
        );
        SetSourceRect(detailBackground.rectTransform, 12, 13, 400, 306);
        RawImage detailCard = CreateRawButton("DetailCardImage", window.transform);
        SetSourceRect(detailCard.rectTransform, 12, 31, 400, 200);
        RawImage detailPrevious = CreateRawButton(
            "DetailPreviousButtonImage",
            window.transform,
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_previous_button_up"
        );
        SetSourceRect(detailPrevious.rectTransform, 31, 13, 21, 17);
        Button detailPreviousButton = CreateButton(detailPrevious);
        RawImage detailNext = CreateRawButton(
            "DetailNextButtonImage",
            window.transform,
            "Art/UI/StrategyView/ui_strategyview_encyclopedia_window_next_button_up"
        );
        SetSourceRect(detailNext.rectTransform, 380, 13, 21, 17);
        Button detailNextButton = CreateButton(detailNext);
        TextMeshProUGUI detailTitle = CreateTextLabel("DetailTitleTextField", window.transform);
        detailTitle.text = "Database Entry";
        detailTitle.color = Color.white;
        detailTitle.fontSize = 13;
        detailTitle.alignment = TextAlignmentOptions.Top;
        SetSourceRect(detailTitle.rectTransform, 46, 14, 220, 17);

        ScrollAreaView detailLinesScrollArea = CreateScrollAreaView(
            window.transform,
            "DetailLinesScrollArea",
            14,
            233,
            399,
            81,
            0,
            1,
            375,
            80,
            386,
            0,
            13,
            80,
            out RectTransform detailLinesContent
        );
        TextMeshProUGUI detailLineTemplate = CreateTextLabel(
            "DetailLineTextTemplate",
            detailLinesContent
        );
        detailLineTemplate.text = "Entry text";
        detailLineTemplate.color = Color.white;
        detailLineTemplate.fontSize = 13;
        detailLineTemplate.alignment = TextAlignmentOptions.TopLeft;
        detailLineTemplate.textWrappingMode = TextWrappingModes.NoWrap;
        detailLineTemplate.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(detailLineTemplate.rectTransform, 2, 1, 375, 16);
        detailLineTemplate.gameObject.SetActive(false);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "overlayFrameImage", overlay);
        AssignReference(view, "buttonStripImage", strip);
        AssignReferenceArray(view, "upperButtonImages", upperButtonImages);
        AssignReferenceArray(view, "upperButtons", upperButtons);
        AssignReferenceArray(view, "twoButtonImages", twoButtonImages);
        AssignReferenceArray(view, "twoButtons", twoButtons);
        AssignReferenceArray(view, "fourButtonImages", fourButtonImages);
        AssignReferenceArray(view, "fourButtons", fourButtons);
        AssignReference(view, "titleTextField", title);
        AssignReference(view, "topicLabelTextField", topic);
        AssignReferenceArray(view, "tabImageSlots", tabSlots);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "tabTitleTextField", tabTitle);
        AssignReference(view, "rowsScrollArea", rowsScrollArea);
        AssignReference(view, "rowTemplate", rowTemplate);
        AssignReference(view, "rowTextTemplate", rowTextTemplate);
        AssignReference(view, "detailBackgroundImage", detailBackground);
        AssignReference(view, "detailCardImage", detailCard);
        AssignReference(view, "detailPreviousButtonImage", detailPrevious);
        AssignReference(view, "detailPreviousButton", detailPreviousButton);
        AssignReference(view, "detailNextButtonImage", detailNext);
        AssignReference(view, "detailNextButton", detailNextButton);
        AssignReference(view, "detailTitleTextField", detailTitle);
        AssignReference(view, "detailLinesScrollArea", detailLinesScrollArea);
        AssignReference(view, "detailLineTextTemplate", detailLineTemplate);
        AssignReference(
            view,
            "backgroundTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_background")
        );
        AssignReference(
            view,
            "topicBackgroundTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_topic_background")
        );
        AssignReference(
            view,
            "systemButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_system_button_pressed")
        );
        AssignReference(
            view,
            "systemButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_system_button_up")
        );
        AssignReference(
            view,
            "nextButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_next_button_up")
        );
        AssignReference(
            view,
            "nextButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_next_button_disabled")
        );
        AssignReference(
            view,
            "previousButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_previous_button_up")
        );
        AssignReference(
            view,
            "previousButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_previous_button_disabled")
        );
        AssignReference(
            view,
            "allSystemsButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_all_systems_button_up")
        );
        AssignReference(
            view,
            "allSystemsButtonDownTexture",
            LoadStrategyViewTexture(
                "ui_strategyview_encyclopedia_window_all_systems_button_pressed"
            )
        );

        buttons.SetAsLastSibling();
        GameObject saved = SaveGeneratedPrefabAsset(window, _encyclopediaWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<EncyclopediaWindowView>();
    }

    private static ManufacturingLaneCardView CreateManufacturingLaneCardView(
        Transform parent,
        string name,
        int cardY,
        int countTextY
    )
    {
        GameObject card = new GameObject(
            name,
            typeof(RectTransform),
            typeof(ManufacturingLaneCardView)
        );
        card.transform.SetParent(parent, false);
        ManufacturingLaneCardView view = card.GetComponent<ManufacturingLaneCardView>();
        SetSourceRect(card.GetComponent<RectTransform>(), 0, 0, 226, 304);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            card.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitArea.rectTransform, 55, cardY, 163, 74);
        RawImage baseCard = CreateRawImage(
            "BaseCardImage",
            card.transform,
            _facilityManufacturingCardPreviewPath,
            55,
            cardY
        );
        RawImage stateCard = CreateRawImage(
            "StateCardImage",
            card.transform,
            _facilityManufacturingCardStatePreviewPath,
            55,
            cardY
        );
        RectTransform entitySlot = CreateChildLayer("EntitySlot", card.transform);
        SetSourceRect(entitySlot, 150, cardY + 16, 67, 48);
        RawImage entity = CreateRightCenteredRawImage(
            "EntityImage",
            entitySlot,
            _facilityCardEntitySmallPreviewPath
        );
        Image progress = CreateImage("ProgressFillImage", card.transform);
        progress.color = new Color32(255, 255, 84, 255);
        SetSourceRect(progress.rectTransform, 56, cardY + 70, 80, 4);

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", card.transform);
        title.text = "Ship Construction";
        title.color = Color.black;
        title.fontSize = 11;
        title.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(title.rectTransform, 60, cardY, 150, 16);

        TextMeshProUGUI currentName = CreateTextLabel("CurrentNameTextField", card.transform);
        currentName.text = "Nebulon-B Frigate";
        currentName.color = Color.white;
        currentName.fontSize = 11;
        currentName.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(currentName.rectTransform, 60, cardY + 18, 130, 16);

        TextMeshProUGUI currentCount = CreateTextLabel("CurrentCountTextField", card.transform);
        currentCount.text = "Building 1";
        currentCount.color = Color.white;
        currentCount.fontSize = 11;
        currentCount.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(currentCount.rectTransform, 60, cardY + 48, 130, 16);

        TextMeshProUGUI empty = CreateTextLabel("EmptyTextField", card.transform);
        empty.text = "No Ships are being built";
        empty.color = Color.white;
        empty.fontSize = 11;
        empty.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(empty.rectTransform, 60, cardY + 18, 150, 16);

        TextMeshProUGUI destination = CreateTextLabel("DestinationTextField", card.transform);
        destination.text = "Destination: Corellia";
        destination.color = Color.white;
        destination.fontSize = 11;
        destination.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(destination.rectTransform, 60, cardY + 58, 150, 16);

        TextMeshProUGUI facilityCount = CreateTextLabel("FacilityCountTextField", card.transform);
        facilityCount.text = "1:3";
        facilityCount.color = Color.white;
        facilityCount.fontSize = 14;
        facilityCount.alignment = TextAlignmentOptions.Center;
        SetSourceRect(facilityCount.rectTransform, 6, countTextY, 46, 16);

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "baseCardImage", baseCard);
        AssignReference(view, "stateCardImage", stateCard);
        AssignReference(view, "baseTexture", LoadTexture(_facilityManufacturingCardPreviewPath));
        AssignReference(view, "entityImage", entity);
        AssignReference(view, "progressFillImage", progress);
        AssignReference(view, "titleTextField", title);
        AssignReference(view, "currentNameTextField", currentName);
        AssignReference(view, "currentCountTextField", currentCount);
        AssignReference(view, "emptyTextField", empty);
        AssignReference(view, "destinationTextField", destination);
        AssignReference(view, "facilityCountTextField", facilityCount);
        card.SetActive(false);
        return view;
    }

    private static FacilityInventoryItemView CreateFacilityInventoryItemTemplate(Transform parent)
    {
        GameObject item = new GameObject(
            "InventoryItemTemplate",
            typeof(RectTransform),
            typeof(FacilityInventoryItemView)
        );
        item.transform.SetParent(parent, false);
        FacilityInventoryItemView view = item.GetComponent<FacilityInventoryItemView>();
        SetSourceRect(item.GetComponent<RectTransform>(), 10, 70, 67, 40);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            item.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitArea.rectTransform, 0, 0, 67, 40);
        RawImage itemImage = CreateRawButton(
            "ItemImage",
            item.transform,
            _facilityInventoryItemPreviewPath
        );
        SetSourceRect(itemImage.rectTransform, 0, 0, 67, 40);
        RawImage selectionImage = CreateRawButton(
            "SelectionImage",
            item.transform,
            _facilityManufacturingSelectPreviewPath
        );
        SetSourceRect(selectionImage.rectTransform, 0, 0, 67, 40);

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "itemImage", itemImage);
        AssignReference(view, "selectionImage", selectionImage);
        AddTemplateLayoutElement(item.GetComponent<RectTransform>());
        item.SetActive(false);
        return view;
    }

    private static ConfirmDialogWindowView BuildConfirmDialogWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_confirmDialogWindowPrefabPath));

        GameObject window = new GameObject(
            "ConfirmDialogWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        window.SetActive(false);
        ConfirmDialogWindowView view = window.AddComponent<ConfirmDialogWindowView>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 424, 331);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            PreviewTheme?.ConfirmDialogTheme?.BackgroundImagePath,
            0,
            0
        );
        background.raycastTarget = true;

        RawImage title = CreateRawImage(
            "TitleImage",
            window.transform,
            PreviewTheme?.ConfirmDialogTheme?.MoveTitleImagePath,
            12,
            30
        );
        title.raycastTarget = true;
        title.gameObject.AddComponent<UIWindowDragHandle>();

        RawImage confirmButton = CreateRawImage(
            "ConfirmButtonImage",
            window.transform,
            _confirmButtonPreviewPath,
            355,
            244
        );
        Button confirmButtonComponent = CreateButton(confirmButton);
        RawImage cancelButton = CreateRawImage(
            "CancelButtonImage",
            window.transform,
            _cancelButtonPreviewPath,
            355,
            281
        );
        Button cancelButtonComponent = CreateButton(cancelButton);

        ScrollAreaView linesScrollArea = CreateScrollAreaView(
            window.transform,
            "LinesScrollArea",
            30,
            242,
            317,
            72,
            0,
            2,
            300,
            70,
            304,
            0,
            13,
            70,
            out RectTransform linesContent
        );

        TextMeshProUGUI lineTemplate = CreateTextLabel("LineTemplate", linesContent);
        lineTemplate.text = "ConfirmLine";
        lineTemplate.color = Color.white;
        lineTemplate.fontSize = 13;
        lineTemplate.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(lineTemplate.rectTransform, 0, 5, 300, 15);
        lineTemplate.gameObject.SetActive(false);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(view, "confirmButtonImage", confirmButton);
        AssignReference(view, "cancelButtonImage", cancelButton);
        AssignReference(view, "confirmButton", confirmButtonComponent);
        AssignReference(view, "cancelButton", cancelButtonComponent);
        AssignReference(view, "linesScrollArea", linesScrollArea);
        AssignReference(view, "lineTemplate", lineTemplate);
        AssignReference(view, "confirmButtonUpTexture", LoadTexture(_confirmButtonPreviewPath));
        AssignReference(view, "cancelButtonUpTexture", LoadTexture(_cancelButtonPreviewPath));

        window.SetActive(true);
        GameObject saved = SaveGeneratedPrefabAsset(window, _confirmDialogWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<ConfirmDialogWindowView>();
    }

    private static ScrollAreaView CreateScrollAreaView(
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
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        SetSourceRect(root.GetComponent<RectTransform>(), x, y, width, height);
        ScrollAreaView view = root.AddComponent<ScrollAreaView>();

        GameObject scrollObject = new GameObject(
            "ScrollRect",
            typeof(RectTransform),
            typeof(ScrollRect)
        );
        scrollObject.transform.SetParent(root.transform, false);
        SetSourceRect(
            scrollObject.GetComponent<RectTransform>(),
            viewportX,
            viewportY,
            viewportWidth,
            viewportHeight
        );
        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(RectMask2D),
            typeof(Image)
        );
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetSourceRect(viewportRect, 0, 0, viewportWidth, viewportHeight);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        contentRoot = CreateChildLayer("Content", viewport.transform);
        SetSourceRect(contentRoot, 0, 0, viewportWidth, viewportHeight);
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;

        GameObject scrollbarObject = new GameObject(
            "Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar)
        );
        scrollbarObject.transform.SetParent(root.transform, false);
        SetSourceRect(
            scrollbarObject.GetComponent<RectTransform>(),
            scrollbarX,
            scrollbarY,
            scrollbarWidth,
            scrollbarHeight
        );

        Texture2D scrollUpTexture = LoadTexture(_scrollUpArrowPreviewPath);
        Texture2D scrollDownTexture = LoadTexture(_scrollDownArrowPreviewPath);
        int upArrowHeight = scrollUpTexture == null ? 0 : scrollUpTexture.height;
        int downArrowHeight = scrollDownTexture == null ? 0 : scrollDownTexture.height;
        int trackHeight = Mathf.Max(0, scrollbarHeight - upArrowHeight - downArrowHeight);

        Image scrollbarBackground = scrollbarObject.GetComponent<Image>();
        scrollbarBackground.color = Color.clear;
        scrollbarBackground.raycastTarget = true;
        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        Image trackBackground = CreateImage("TrackBackgroundImage", scrollbarObject.transform);
        trackBackground.color = Color.black;
        SetSourceRect(trackBackground.rectTransform, 0, upArrowHeight, scrollbarWidth, trackHeight);

        RawImage scrollUpImage = CreateRawImage(
            "ScrollUpButtonImage",
            scrollbarObject.transform,
            _scrollUpArrowPreviewPath,
            0,
            0
        );
        SetSourceRect(scrollUpImage.rectTransform, 0, 0, scrollbarWidth, upArrowHeight);
        scrollUpImage.raycastTarget = true;
        Button scrollUpButton = scrollUpImage.gameObject.AddComponent<Button>();
        scrollUpButton.targetGraphic = scrollUpImage;
        scrollUpButton.transition = Selectable.Transition.None;

        RawImage scrollDownImage = CreateRawImage(
            "ScrollDownButtonImage",
            scrollbarObject.transform,
            _scrollDownArrowPreviewPath,
            0,
            scrollbarHeight - downArrowHeight
        );
        SetSourceRect(
            scrollDownImage.rectTransform,
            0,
            scrollbarHeight - downArrowHeight,
            scrollbarWidth,
            downArrowHeight
        );
        scrollDownImage.raycastTarget = true;
        Button scrollDownButton = scrollDownImage.gameObject.AddComponent<Button>();
        scrollDownButton.targetGraphic = scrollDownImage;
        scrollDownButton.transition = Selectable.Transition.None;

        RectTransform slidingArea = CreateChildLayer("SlidingArea", scrollbarObject.transform);
        SetSourceRect(slidingArea, 0, upArrowHeight, scrollbarWidth, trackHeight);
        RawImage handleImage = CreateRawImage(
            "Handle",
            slidingArea,
            _scrollBarMiddlePreviewPath,
            0,
            0
        );
        FillParent(handleImage.rectTransform);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = handleImage.rectTransform;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.None;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        AssignReference(view, "scrollRect", scrollRect);
        AssignReference(view, "contentRoot", contentRoot);
        AssignReference(view, "scrollbar", scrollbar);
        AssignReference(view, "trackBackgroundRoot", trackBackground.rectTransform);
        AssignReference(view, "slidingAreaRoot", slidingArea);
        AssignReference(view, "scrollUpButton", scrollUpButton);
        AssignReference(view, "scrollDownButton", scrollDownButton);
        return view;
    }

    private static void ConfigureVerticalContent(RectTransform contentRoot, int spacing = 0)
    {
        VerticalLayoutGroup layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = spacing;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static GridLayoutGroup ConfigureGridContent(
        RectTransform contentRoot,
        int cellWidth,
        int cellHeight,
        int columns
    )
    {
        GridLayoutGroup layout = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.cellSize = new Vector2(cellWidth, cellHeight);
        layout.spacing = Vector2.zero;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        return layout;
    }

    private static void AddTemplateLayoutElement(Component template)
    {
        RectTransform rect = template.transform as RectTransform;
        LayoutElement layoutElement = template.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = rect.sizeDelta.x;
        layoutElement.minHeight = rect.sizeDelta.y;
        layoutElement.preferredWidth = rect.sizeDelta.x;
        layoutElement.preferredHeight = rect.sizeDelta.y;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
    }

    private static PlanetSystemClusterView BuildPlanetSystemClusterPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_planetSystemClusterPrefabPath));

        GameObject root = new GameObject("PlanetSystemCluster", typeof(RectTransform));
        PlanetSystemClusterView view = root.AddComponent<PlanetSystemClusterView>();
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, 50, 50);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            root.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitArea.rectTransform, 0, 0, 50, 50);
        hitArea.raycastTarget = true;
        RawImage starTemplate = CreateRawImage(
            "StarImageTemplate",
            root.transform,
            _galaxyStarPreviewPath,
            17,
            17
        );
        RawImage headquartersTemplate = CreateRawImage(
            "HeadquartersImageTemplate",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.GalaxyHeadquartersImagePath,
            17,
            17
        );
        TextMeshProUGUI systemNameTextField = CreateTextLabel(
            "SystemNameTextField",
            root.transform
        );
        systemNameTextField.text = "SystemName";
        systemNameTextField.color = new Color32(231, 243, 83, 255);
        systemNameTextField.fontSize = 13;
        systemNameTextField.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(systemNameTextField.rectTransform, 0, 0, 100, 32);

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "systemNameTextField", systemNameTextField);
        AssignReference(view, "starImageTemplate", starTemplate);
        AssignReference(view, "headquartersImageTemplate", headquartersTemplate);

        GameObject saved = SaveGeneratedPrefabAsset(root, _planetSystemClusterPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<PlanetSystemClusterView>();
    }

    [MenuItem("Rebellion/Strategy View/Install Strategy View Root Prefab In Scene")]
    public static void InstallStrategyViewRootPrefabInScene()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath) == null)
            BuildStrategyViewRootPrefab();

        SceneRootPrefabInstaller.InstallRootPrefabInScene(
            _strategyScenePath,
            _prefabPath,
            _sceneInstanceName,
            _strategySceneRootParentPath
        );
    }

    private static GameObject CreateLayer(string name, Transform parent)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(parent, false);
        return layer;
    }

    private static RectTransform CreateChildLayer(string name, Transform parent)
    {
        GameObject layer = CreateLayer(name, parent);
        RectTransform rect = layer.GetComponent<RectTransform>();
        FillParent(rect);
        return rect;
    }

    private static RectTransform CreateSourceRectLayer(
        string name,
        Transform parent,
        int width,
        int height
    )
    {
        GameObject layer = CreateLayer(name, parent);
        RectTransform rect = layer.GetComponent<RectTransform>();
        SetSourceRect(rect, 0, 0, width, height);
        return rect;
    }

    private static GameObject SaveGeneratedPrefabAsset(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EnableRuntimeBehaviours(root);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        EnableRuntimeBehaviours(saved);
        EditorUtility.SetDirty(saved);
        PrefabUtility.SavePrefabAsset(saved);
        return saved;
    }

    private static void EnableRuntimeBehaviours(GameObject root)
    {
        if (root == null)
            return;

        System.Reflection.Assembly runtimeAssembly = typeof(UIWindow).Assembly;
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            System.Type type = behaviour == null ? null : behaviour.GetType();
            if (
                behaviour != null
                && (
                    type.Assembly == runtimeAssembly
                    || string.Equals(
                        type.Assembly.GetName().Name,
                        "GameAssembly",
                        System.StringComparison.Ordinal
                    )
                )
            )
            {
                behaviour.enabled = true;
            }
        }
    }

    private static StrategyOverlayView CreateStrategyOverlayView(Transform parent)
    {
        GameObject overlay = CreateLayer(_overlayLayerName, parent);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        SetStrategySurfaceRect(overlayRect);
        StrategyOverlayView view = overlay.AddComponent<StrategyOverlayView>();
        RawImage targetingInput = overlay.AddComponent<RawImage>();
        targetingInput.color = Color.clear;
        targetingInput.canvasRenderer.cullTransparentMesh = false;
        targetingInput.enabled = false;
        targetingInput.raycastTarget = false;

        Image top = CreateImage("DragFrameTopImage", overlay.transform);
        Image bottom = CreateImage("DragFrameBottomImage", overlay.transform);
        Image left = CreateImage("DragFrameLeftImage", overlay.transform);
        Image right = CreateImage("DragFrameRightImage", overlay.transform);
        RawImage cursor = CreateRawButton("DestinationCursorImage", overlay.transform);

        top.color = Color.white;
        bottom.color = Color.white;
        left.color = Color.white;
        right.color = Color.white;
        SetSourceRect(top.rectTransform, 0, 0, 1, 1);
        SetSourceRect(bottom.rectTransform, 0, 0, 1, 1);
        SetSourceRect(left.rectTransform, 0, 0, 1, 1);
        SetSourceRect(right.rectTransform, 0, 0, 1, 1);
        SetSourceRect(cursor.rectTransform, 0, 0, 22, 22);
        top.gameObject.SetActive(false);
        bottom.gameObject.SetActive(false);
        left.gameObject.SetActive(false);
        right.gameObject.SetActive(false);
        cursor.gameObject.SetActive(false);

        AssignReference(view, "targetingInputImage", targetingInput);
        AssignReference(view, "dragFrameTopImage", top);
        AssignReference(view, "dragFrameBottomImage", bottom);
        AssignReference(view, "dragFrameLeftImage", left);
        AssignReference(view, "dragFrameRightImage", right);
        AssignReference(view, "destinationCursorImage", cursor);
        AssignInt(view, "destinationCursorSize", _destinationCursorSize);
        AssignInt(view, "destinationCursorRadius", _destinationCursorRadius);
        return view;
    }

    private static StrategyContextMenuPresenter CreateContextMenu(Transform parent)
    {
        GameObject contextMenu = CreateLayer(_contextMenuName, parent);
        SetStrategySurfaceRect(contextMenu.GetComponent<RectTransform>());
        ContextMenuView contextMenuView = contextMenu.AddComponent<ContextMenuView>();
        ContextMenuHost host = contextMenu.AddComponent<ContextMenuHost>();
        StrategyContextMenuPresenter presenter =
            contextMenu.AddComponent<StrategyContextMenuPresenter>();
        contextMenuView.enabled = true;
        host.enabled = true;
        presenter.enabled = true;

        RawImage dismissHitArea = CreatePanelImage(
            "DismissHitAreaImage",
            contextMenu.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        FillParent(dismissHitArea.rectTransform);
        dismissHitArea.raycastTarget = true;
        dismissHitArea.canvasRenderer.cullTransparentMesh = false;
        ContextMenuDismissBoundary dismissBoundary =
            dismissHitArea.gameObject.AddComponent<ContextMenuDismissBoundary>();

        GameObject panel = CreateLayer(_contextMenuPanelTemplateName, contextMenu.transform);
        ContextMenuPanelView panelView = panel.AddComponent<ContextMenuPanelView>();
        panelView.enabled = true;
        SetSourceRect(panel.GetComponent<RectTransform>(), 120, 120, 120, 62);

        Image background = CreateImage("BackgroundImage", panel.transform);
        background.color = new Color(0f, 0f, 0f, 0.8f);
        FillParent(background.rectTransform);

        Image borderTop = CreateImage("BorderTopImage", panel.transform);
        borderTop.color = Color.white;
        SetSourceRect(borderTop.rectTransform, 0, 0, 120, 1);

        Image borderBottom = CreateImage("BorderBottomImage", panel.transform);
        borderBottom.color = Color.white;
        SetSourceRect(borderBottom.rectTransform, 0, 61, 120, 1);

        Image borderLeft = CreateImage("BorderLeftImage", panel.transform);
        borderLeft.color = Color.white;
        SetSourceRect(borderLeft.rectTransform, 0, 0, 1, 62);

        Image borderRight = CreateImage("BorderRightImage", panel.transform);
        borderRight.color = Color.white;
        SetSourceRect(borderRight.rectTransform, 119, 0, 1, 62);

        GameObject command = CreateLayer(_contextMenuCommandTemplateName, panel.transform);
        ContextMenuCommandView commandView = command.AddComponent<ContextMenuCommandView>();
        commandView.enabled = true;
        SetSourceRect(command.GetComponent<RectTransform>(), 0, 0, 120, 20);

        RawImage commandHitArea = CreatePanelImage(
            "HitAreaImage",
            command.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(commandHitArea.rectTransform, 0, 0, 120, 20);
        commandHitArea.raycastTarget = true;
        RawImage iconImage = CreateRawButton("IconImage", command.transform);
        SetSourceRect(
            iconImage.rectTransform,
            4,
            1,
            _contextMenuIconPreviewSize,
            _contextMenuIconPreviewSize
        );
        iconImage.gameObject.SetActive(false);
        TextMeshProUGUI commandTextField = CreateTextLabel("CommandTextField", command.transform);
        commandTextField.text = "MenuItem";
        commandTextField.color = Color.white;
        commandTextField.fontSize = 12;
        commandTextField.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(commandTextField.rectTransform, 6, 5, 110, 18);

        AssignReference(host, "contextMenuView", contextMenuView);
        AssignReference(presenter, "contextMenuHost", host);
        AssignReference(contextMenuView, "panelPrefab", panelView);
        AssignReference(contextMenuView, "dismissHitAreaImage", dismissHitArea);
        AssignReference(contextMenuView, "dismissBoundary", dismissBoundary);
        AssignReference(panelView, "backgroundImage", background);
        AssignReference(panelView, "borderTopImage", borderTop);
        AssignReference(panelView, "borderBottomImage", borderBottom);
        AssignReference(panelView, "borderLeftImage", borderLeft);
        AssignReference(panelView, "borderRightImage", borderRight);
        AssignReference(panelView, "commandPrefab", commandView);
        AssignReference(commandView, "hitAreaImage", commandHitArea);
        AssignReference(commandView, "iconImage", iconImage);
        AssignReference(commandView, "commandTextField", commandTextField);
        AssignInt(presenter, "speedMenuWidth", _speedContextMenuWidth);
        AssignInt(presenter, "facilityMenuWidth", _facilityContextMenuWidth);
        AssignInt(presenter, "planetSystemMenuWidth", _planetSystemContextMenuWidth);
        AssignInt(presenter, "defenseMenuWidth", _defenseContextMenuWidth);
        AssignInt(presenter, "missionsMenuWidth", _missionsContextMenuWidth);
        AssignInt(presenter, "fallbackMenuWidth", _fallbackContextMenuWidth);
        panel.SetActive(false);
        dismissHitArea.gameObject.SetActive(false);
        return presenter;
    }

    private static TextMeshProUGUI CreateHudLabel(
        string name,
        string text,
        Transform parent,
        int x,
        int y,
        TextAlignmentOptions alignment
    )
    {
        GameObject labelObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(80, 11);
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = Color.red;
        label.fontSize = 10;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.alignment = alignment;
        return label;
    }

    private static PlanetSystemPlanetView BuildPlanetSystemPlanetPrefab()
    {
        GameObject root = new GameObject("PlanetSystemPlanet", typeof(RectTransform));
        PlanetSystemPlanetView planetView = root.AddComponent<PlanetSystemPlanetView>();
        const int rootWidth = _planetPreviewWidth + 19;
        const int rootHeight = 98;
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, rootWidth, rootHeight);

        RawImage hitArea = CreateRawButton("HitAreaImage", root.transform);
        hitArea.color = Color.clear;
        hitArea.raycastTarget = false;
        hitArea.canvasRenderer.cullTransparentMesh = false;
        SetSourceRect(hitArea.rectTransform, 0, 0, rootWidth, rootHeight);
        RawImage planet = CreateRawImage("PlanetImage", root.transform, _planetPreviewPath, 10, 1);
        RawImage facility = CreateRawImage(
            "FacilityImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Buildings?.NormalImagePath,
            0,
            0
        );
        RawImage defense = CreateRawImage(
            "DefenseImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Defenses?.NormalImagePath,
            0,
            20
        );
        RawImage fleet = CreateRawImage(
            "FleetImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Fleets?.NormalImagePath,
            29,
            0
        );
        RawImage mission = CreateRawImage(
            "MissionImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Missions?.NormalImagePath,
            28,
            20
        );
        RawImage headquarters = CreateRawImage(
            "HeadquartersImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetSystemHeadquartersImagePath,
            10,
            1
        );
        BarPrefabParts energyBar = CreateBar(
            "EnergyBar",
            root.transform,
            39,
            Color.white,
            Color.blue
        );
        BarPrefabParts rawBar = CreateBar("RawBar", root.transform, 43, Color.yellow, Orange());
        BarPrefabParts supportBar = CreateFillBar("SupportBar", root.transform, 47, Color.red);
        TextMeshProUGUI planetNameTextField = CreateTextLabel(
            "PlanetNameTextField",
            root.transform
        );
        planetNameTextField.text = "PlanetName";
        planetNameTextField.color = Color.cyan;
        planetNameTextField.fontSize = 11;
        SetSourceRect(
            planetNameTextField.rectTransform,
            10 + _planetPreviewWidth / 2 - 50,
            50,
            100,
            11
        );

        AssignReference(planetView, "hitAreaImage", hitArea);
        AssignReference(planetView, "planetImage", planet);
        AssignReference(planetView, "facilityImage", facility);
        AssignReference(planetView, "defenseImage", defense);
        AssignReference(planetView, "fleetImage", fleet);
        AssignReference(planetView, "missionImage", mission);
        AssignReference(planetView, "headquartersImage", headquarters);
        AssignReference(planetView, "planetNameTextField", planetNameTextField);
        AssignReference(planetView, "energyBarRoot", energyBar.Root);
        AssignReference(planetView, "rawBarRoot", rawBar.Root);
        AssignReference(planetView, "supportBarRoot", supportBar.Root);
        AssignReference(planetView, "energyBarBackgroundImage", energyBar.Background);
        AssignReference(planetView, "energyBarFillImage", energyBar.Fill);
        AssignReferenceArray(planetView, "energyBarCellImages", energyBar.Cells);
        AssignReference(planetView, "rawBarBackgroundImage", rawBar.Background);
        AssignReference(planetView, "rawBarFillImage", rawBar.Fill);
        AssignReferenceArray(planetView, "rawBarCellImages", rawBar.Cells);
        AssignReference(planetView, "supportBarBackgroundImage", supportBar.Background);
        AssignReference(planetView, "supportBarFillImage", supportBar.Fill);

        GameObject saved = SaveGeneratedPrefabAsset(root, _planetSystemPlanetPrefabPath);
        Object.DestroyImmediate(root);
        return saved.GetComponent<PlanetSystemPlanetView>();
    }

    private static PlanetSystemWindowView BuildPlanetSystemWindowPrefab(
        PlanetSystemPlanetView planetPrefab
    )
    {
        GameObject window = CreatePlanetSystemWindowObject("PlanetSystemWindow", planetPrefab);
        GameObject saved = SaveGeneratedPrefabAsset(window, _planetSystemWindowPrefabPath);
        Object.DestroyImmediate(window);
        return saved.GetComponent<PlanetSystemWindowView>();
    }

    private static GameObject CreatePlanetSystemWindowObject(
        string name,
        PlanetSystemPlanetView planetPrefab
    )
    {
        const int windowWidth = 237;
        const int windowHeight = 359;
        GameObject window = new GameObject(
            name,
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(PlanetSystemWindowView)
        );
        PlanetSystemWindowView view = window.GetComponent<PlanetSystemWindowView>();
        RectTransform rect = window.GetComponent<RectTransform>();
        SetSourceRect(rect, 59, 36, windowWidth, windowHeight);

        RawImage dim = CreatePanelImage(
            "DimPanelImage",
            window.transform,
            _sectorWindowBackgroundOverlay
        );
        dim.raycastTarget = true;
        RawImage borderTop = CreatePanelImage("BorderTopImage", window.transform, Color.white);
        RawImage borderBottom = CreatePanelImage(
            "BorderBottomImage",
            window.transform,
            Color.white
        );
        RawImage borderLeft = CreatePanelImage("BorderLeftImage", window.transform, Color.white);
        RawImage borderRight = CreatePanelImage("BorderRightImage", window.transform, Color.white);
        TextMeshProUGUI systemNameTextField = CreateTextLabel(
            "SystemNameTextField",
            window.transform
        );
        systemNameTextField.text = "SystemName";
        systemNameTextField.color = Color.yellow;
        systemNameTextField.fontSize = 13;
        systemNameTextField.alignment = TextAlignmentOptions.Top;
        SetSourceRect(systemNameTextField.rectTransform, 0, 5, windowWidth, 13);
        RawImage swapButton = CreateRawButton(
            "SwapButtonImage",
            window.transform,
            _windowSwapPreviewPath
        );
        SetSourceRect(swapButton.rectTransform, windowWidth - 31, 3, 14, 14);
        RawImage closeButton = CreateRawButton(
            "CloseButtonImage",
            window.transform,
            _windowClosePreviewPath
        );
        SetSourceRect(closeButton.rectTransform, windowWidth - 17, 3, 14, 14);
        ConfigureWindowButtons(
            window.GetComponent<UIWindow>(),
            new[] { swapButton, closeButton },
            new[]
            {
                StrategyWindowButtonActions.SwapWindow,
                StrategyWindowButtonActions.CloseWindow,
            }
        );

        GameObject planets = new GameObject("Planets", typeof(RectTransform), typeof(RectMask2D));
        planets.transform.SetParent(window.transform, false);
        FillParent(planets.GetComponent<RectTransform>());

        AssignReference(view, "dimPanelImage", dim);
        AssignReference(view, "borderTopImage", borderTop);
        AssignReference(view, "borderBottomImage", borderBottom);
        AssignReference(view, "borderLeftImage", borderLeft);
        AssignReference(view, "borderRightImage", borderRight);
        AssignReference(view, "systemNameTextField", systemNameTextField);
        AssignReference(view, "swapButtonImage", swapButton);
        AssignReference(view, "swapButtonUpTexture", LoadTexture(_windowSwapPreviewPath));
        AssignReference(
            view,
            "swapButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_planetsystem_window_swap_button_pressed")
        );
        AssignReference(view, "closeButtonImage", closeButton);
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(view, "closeButtonDownTexture", LoadTexture(_windowCloseDownPreviewPath));
        AssignReference(view, "planetsRoot", planets.GetComponent<RectTransform>());
        AssignReference(view, "planetPrefab", planetPrefab);
        AssignFloat(view, "sectorCoordinateRange", _sectorCoordinateRange);
        AssignFloat(view, "sectorCoordinateScaleX", _sectorCoordinateScaleX);
        AssignFloat(view, "sectorCoordinateScaleY", _sectorCoordinateScaleY);
        return window;
    }

    private static RawImage CreatePanelImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        imageObject.transform.SetParent(parent, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.color = color;
        if (color.a <= 0f)
            image.canvasRenderer.cullTransparentMesh = false;
        image.raycastTarget = false;
        return image;
    }

    private static RawImage CreateRawButton(
        string name,
        Transform parent,
        string texturePath = null
    )
    {
        GameObject button = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        button.transform.SetParent(parent, false);
        RawImage image = button.GetComponent<RawImage>();
        image.texture = string.IsNullOrEmpty(texturePath) ? null : LoadTexture(texturePath);
        image.raycastTarget = false;
        if (image.texture != null)
            SetSourceRect(image.rectTransform, 0, 0, image.texture.width, image.texture.height);
        return image;
    }

    private static RawImage CreateRawImage(
        string name,
        Transform parent,
        string texturePath,
        int x,
        int y
    )
    {
        RawImage image = CreateRawButton(name, parent, texturePath);
        Texture texture = image.texture;
        int width = texture == null ? 14 : texture.width;
        int height = texture == null ? 14 : texture.height;
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    private static Button CreateButton(RawImage image)
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static List<Button> CreateButtons(IReadOnlyList<RawImage> images)
    {
        List<Button> buttons = new List<Button>();
        for (int i = 0; i < images.Count; i++)
            buttons.Add(CreateButton(images[i]));

        return buttons;
    }

    private static Slider CreateSaveMenuSlider(
        string name,
        Transform parent,
        int x,
        int y,
        out RawImage thumb
    )
    {
        Texture2D thumbTexture = LoadTexture(_saveMenuSliderPreviewPath);
        int thumbWidth = thumbTexture == null ? 14 : thumbTexture.width;
        int thumbHeight = thumbTexture == null ? 14 : thumbTexture.height;
        GameObject sliderObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider)
        );
        sliderObject.transform.SetParent(parent, false);
        SetSourceRect(
            sliderObject.GetComponent<RectTransform>(),
            x,
            y,
            _saveMenuSliderWidth,
            thumbHeight
        );

        Image hitArea = sliderObject.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        RectTransform fillArea = CreateChildLayer("FillArea", sliderObject.transform);
        thumb = CreateRawImage(
            name + "Image",
            sliderObject.transform,
            _saveMenuSliderPreviewPath,
            0,
            0
        );
        SetSourceRect(thumb.rectTransform, 0, 0, thumbWidth, thumbHeight);
        thumb.raycastTarget = true;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillArea;
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    private static RawImage CreateWindowTitleImage(Transform parent, int windowWidth)
    {
        RawImage title = CreateRawImage(
            "TitleImage",
            parent,
            PreviewTheme?.WindowTitleTheme?.ActiveImagePath,
            2,
            2
        );
        int height = title.texture == null ? 14 : title.texture.height;
        SetSourceRect(title.rectTransform, 2, 2, windowWidth - 4, height);
        title.raycastTarget = true;
        title.gameObject.AddComponent<UIWindowDragHandle>();
        return title;
    }

    private static void ConfigureWindowButtons(
        UIWindow window,
        IReadOnlyList<RawImage> images,
        IReadOnlyList<int> actions
    )
    {
        if (window == null || images == null || actions == null)
            return;

        List<Button> buttons = new List<Button>();
        List<int> assignedActions = new List<int>();
        int count = Mathf.Min(images.Count, actions.Count);
        for (int i = 0; i < count; i++)
        {
            Button button = ConfigureWindowButton(images[i], actions[i]);
            if (button == null)
                continue;

            buttons.Add(button);
            assignedActions.Add(actions[i]);
        }

        AssignReferenceArray(window, "actionButtons", buttons);
        AssignIntArray(window, "buttonActions", assignedActions);
    }

    private static Button ConfigureWindowButton(RawImage image, int action)
    {
        if (image == null || action == 0)
            return null;

        return CreateButton(image);
    }

    private static List<RawImage> CreateUtilityDialogButtonSlots(
        Transform parent,
        string prefix,
        bool useUpperLayout,
        bool fourButtons
    )
    {
        string[] texturePaths =
        {
            PreviewTheme?.StrategyWindows?.Finder?.CloseButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Finder?.TargetButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Finder?.ShipButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Finder?.FleetButton?.UpImagePath,
        };
        int[] yPositions = useUpperLayout ? new[] { 21, 89, 143, 197 } : new[] { 25, 93, 147, 201 };
        int count = fourButtons ? 4 : 2;
        List<RawImage> images = new List<RawImage>();
        for (int i = 0; i < count; i++)
        {
            RawImage image = CreateRawButton($"{prefix}ButtonImage{i}", parent, texturePaths[i]);
            Texture texture = image.texture;
            int width = texture == null ? 44 : texture.width;
            int height = texture == null ? 41 : texture.height;
            int sideOffset =
                useUpperLayout ? 0
                : fourButtons ? 15
                : 12;
            SetSourceRect(
                image.rectTransform,
                470 - width - sideOffset,
                yPositions[i],
                width,
                height
            );
            images.Add(image);
        }

        return images;
    }

    private static List<RawImage> CreateEncyclopediaDialogButtonSlots(
        Transform parent,
        string prefix,
        bool useUpperLayout
    )
    {
        string[] texturePaths =
        {
            PreviewTheme?.StrategyWindows?.Encyclopedia?.CloseButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.TopicButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.IndexButton?.UpImagePath,
        };
        int[] yPositions = useUpperLayout ? new[] { 21, 89, 143 } : new[] { 25, 93, 147 };
        List<RawImage> images = new List<RawImage>();
        for (int i = 0; i < texturePaths.Length; i++)
        {
            RawImage image = CreateRawButton($"{prefix}ButtonImage{i}", parent, texturePaths[i]);
            Texture texture = image.texture;
            int width = texture == null ? 44 : texture.width;
            int height = texture == null ? 41 : texture.height;
            int sideOffset = useUpperLayout ? 0 : 15;
            SetSourceRect(
                image.rectTransform,
                470 - width - sideOffset,
                yPositions[i],
                width,
                height
            );
            images.Add(image);
        }

        return images;
    }

    private static RawImage CreateRightCenteredRawImage(
        string name,
        Transform parent,
        string texturePath
    )
    {
        RawImage image = CreateRawButton(name, parent, texturePath);
        Texture texture = image.texture;
        int width = texture == null ? 14 : texture.width;
        int height = texture == null ? 14 : texture.height;
        SetRightCenteredRect(image.rectTransform, width, height);
        return image;
    }

    private static TextMeshProUGUI CreateSaveMenuText(
        string name,
        Transform parent,
        string text,
        int x,
        int y,
        int width,
        int height,
        int fontSize,
        TextAlignmentOptions alignment
    )
    {
        TextMeshProUGUI label = CreateTextLabel(name, parent);
        label.text = text;
        label.color = new Color32(117, 251, 76, 255);
        label.fontSize = fontSize;
        label.alignment = alignment;
        SetSourceRect(label.rectTransform, x, y, width, height);
        return label;
    }

    private static TextMeshProUGUI CreateTextLabel(string name, Transform parent)
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

    private static BarPrefabParts CreateBar(
        string name,
        Transform parent,
        int y,
        Color fillColor,
        Color emptyColor
    )
    {
        RectTransform root = CreateChildLayer(name, parent);
        SetSourceRect(root, 10, y, _planetPreviewWidth, 3);
        Image background = CreateImage("BackgroundImage", root);
        background.color = new Color32(160, 160, 160, 255);
        SetSourceRect(background.rectTransform, 0, 0, _planetPreviewWidth, 3);

        Image fill = CreateImage("FillImage", root);
        SetSourceRect(fill.rectTransform, 0, 0, _planetPreviewWidth, 3);
        fill.gameObject.SetActive(false);

        List<Image> cells = new List<Image>();
        for (int i = 0; i < 10; i++)
        {
            Image cell = CreateImage($"Cell{i}Image", root);
            cell.color = i < 7 ? fillColor : emptyColor;
            SetSourceRect(cell.rectTransform, 1 + i * 3, 0, 2, 3);
            cells.Add(cell);
        }

        return new BarPrefabParts(root, background, fill, cells);
    }

    private static BarPrefabParts CreateFillBar(string name, Transform parent, int y, Color color)
    {
        RectTransform root = CreateChildLayer(name, parent);
        SetSourceRect(root, 10, y, _planetPreviewWidth, 3);
        Image background = CreateImage("BackgroundImage", root);
        background.color = Color.green;
        SetSourceRect(background.rectTransform, 0, 0, _planetPreviewWidth, 3);

        Image fill = CreateImage("FillImage", root);
        fill.color = color;
        SetSourceRect(fill.rectTransform, 0, 0, 24, 3);
        return new BarPrefabParts(root, background, fill, new List<Image>());
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Color Orange()
    {
        return new Color32(236, 106, 46, 255);
    }

    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localPosition = Vector3.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetStrategySurfaceRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(_screenWidth, _screenHeight);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void SetSourceRect(
        RectTransform rect,
        SourceRectLayout layout,
        int fallbackX,
        int fallbackY,
        int fallbackWidth,
        int fallbackHeight
    )
    {
        SetSourceRect(
            rect,
            layout?.X ?? fallbackX,
            layout?.Y ?? fallbackY,
            layout?.Width ?? fallbackWidth,
            layout?.Height ?? fallbackHeight
        );
    }

    private static void SetRightCenteredRect(RectTransform rect, int width, int height)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static Texture2D LoadTexture(string path)
    {
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveTextureAssetPath(path));
    }

    private static Texture2D LoadStrategyViewTexture(string assetName)
    {
        return LoadTexture("Art/UI/StrategyView/" + assetName);
    }

    private static string ResolveTextureAssetPath(string path)
    {
        if (path.StartsWith("Assets/", System.StringComparison.Ordinal))
            return ResolveTextureFilePath(RemapLegacyAssetPath(path));

        string resourcePath = Path.Combine("Assets/Resources", RemapLegacyResourcePath(path))
            .Replace("\\", "/");
        return ResolveTextureFilePath(resourcePath);
    }

    private static string ResolveTextureFilePath(string path)
    {
        if (File.Exists(path))
            return path;

        string pngPath = path + ".png";
        if (File.Exists(pngPath))
            return pngPath;

        string jpgPath = path + ".jpg";
        if (File.Exists(jpgPath))
            return jpgPath;

        string jpegPath = path + ".jpeg";
        if (File.Exists(jpegPath))
            return jpegPath;

        return path;
    }

    private static string RemapLegacyAssetPath(string path)
    {
        return path.StartsWith(_legacyArtAssetRoot, System.StringComparison.Ordinal)
            ? _originalArtAssetRoot + path[_legacyArtAssetRoot.Length..]
            : path;
    }

    private static string RemapLegacyResourcePath(string path)
    {
        return path.StartsWith(_legacyArtResourceRoot, System.StringComparison.Ordinal)
            ? _originalArtResourceRoot + path[_legacyArtResourceRoot.Length..]
            : path;
    }

    private static void AssignReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignUnitCardReferences(
        StrategyUnitCardView view,
        RawImage hitAreaImage,
        RawImage backgroundImage,
        RawImage constructionOverlayImage,
        RawImage enrouteOverlayImage,
        RawImage damagedOverlayImage,
        RawImage entityImage,
        RawImage capturedOverlayImage,
        RawImage selectionImage,
        RawImage starfighterBadgeImage,
        RawImage troopBadgeImage,
        RawImage personnelBadgeImage,
        TextMeshProUGUI nameTextField,
        TextMeshProUGUI alternateNameTextTemplate
    )
    {
        AssignReference(view, "hitAreaImage", hitAreaImage);
        AssignReference(view, "backgroundImage", backgroundImage);
        AssignReference(view, "constructionOverlayImage", constructionOverlayImage);
        AssignReference(view, "enrouteOverlayImage", enrouteOverlayImage);
        AssignReference(view, "damagedOverlayImage", damagedOverlayImage);
        AssignReference(view, "entityImage", entityImage);
        AssignReference(view, "capturedOverlayImage", capturedOverlayImage);
        AssignReference(view, "selectionImage", selectionImage);
        AssignReference(view, "starfighterBadgeImage", starfighterBadgeImage);
        AssignReference(view, "troopBadgeImage", troopBadgeImage);
        AssignReference(view, "personnelBadgeImage", personnelBadgeImage);
        AssignReference(view, "nameTextField", nameTextField);
        AssignReference(view, "alternateNameTextTemplate", alternateNameTextTemplate);
    }

    private static void AssignVector2Int(Object target, string propertyName, Vector2Int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.vector2IntValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignWindowLayerLayout(StrategyWindowLayerView windowsView)
    {
        AssignInt(windowsView, "sectorLeftOpenThresholdOffset", _sectorLeftOpenThresholdOffset);
        AssignInt(windowsView, "sectorRightOpenThresholdOffset", _sectorRightOpenThresholdOffset);
        AssignVector2Int(
            windowsView,
            "constructionWindowOffset",
            new Vector2Int(_constructionWindowOffsetX, _constructionWindowOffsetY)
        );
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<Image> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<Button> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<RawImage> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<RectTransform> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<Texture2D> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<TextMeshProUGUI> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<ManufacturingLaneCardView> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignReferenceArray(
        Object target,
        string propertyName,
        IReadOnlyList<StrategyHudButtonView> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignIntArray(
        Object target,
        string propertyName,
        IReadOnlyList<int> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).intValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignWindowPrefabs(
        StrategyWindowLayerView target,
        PlanetSystemWindowView planetSystemWindowPrefab,
        FacilityWindowView facilityWindowPrefab,
        DefenseWindowView defenseWindowPrefab,
        FleetWindowView fleetWindowPrefab,
        MissionsWindowView missionsWindowPrefab,
        ConstructionWindowView constructionWindowPrefab,
        MissionCreateWindowView missionCreateWindowPrefab,
        StatusWindowView statusWindowPrefab,
        MessagesWindowView messagesWindowPrefab,
        ConfirmDialogWindowView confirmDialogWindowPrefab,
        FinderWindowView finderWindowPrefab,
        EncyclopediaWindowView encyclopediaWindowPrefab
    )
    {
        AssignWindowPrefab(target, "planetSystemWindowPrefab", planetSystemWindowPrefab);
        AssignWindowPrefab(target, "facilityWindowPrefab", facilityWindowPrefab);
        AssignWindowPrefab(target, "defenseWindowPrefab", defenseWindowPrefab);
        AssignWindowPrefab(target, "fleetWindowPrefab", fleetWindowPrefab);
        AssignWindowPrefab(target, "missionsWindowPrefab", missionsWindowPrefab);
        AssignWindowPrefab(target, "constructionWindowPrefab", constructionWindowPrefab);
        AssignWindowPrefab(target, "missionCreateWindowPrefab", missionCreateWindowPrefab);
        AssignWindowPrefab(target, "statusWindowPrefab", statusWindowPrefab);
        AssignWindowPrefab(target, "messagesWindowPrefab", messagesWindowPrefab);
        AssignWindowPrefab(target, "confirmDialogWindowPrefab", confirmDialogWindowPrefab);
        AssignWindowPrefab(target, "finderWindowPrefab", finderWindowPrefab);
        AssignWindowPrefab(target, "encyclopediaWindowPrefab", encyclopediaWindowPrefab);
    }

    private static void AssignWindowPrefab(
        StrategyWindowLayerView target,
        string fieldName,
        MonoBehaviour prefab
    )
    {
        ValidateWindowPrefab(fieldName, prefab);

        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(fieldName).objectReferenceValue = prefab;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ValidateWindowPrefab(string fieldName, MonoBehaviour prefab)
    {
        if (prefab == null)
            throw new MissingReferenceException($"{fieldName} is missing.");

        if (prefab.transform is not RectTransform rect)
            throw new System.InvalidOperationException(
                $"{fieldName} does not use a RectTransform root."
            );

        if (rect.sizeDelta.x <= 0 || rect.sizeDelta.y <= 0)
            throw new System.InvalidOperationException($"{fieldName} has no fixed prefab size.");
    }

    private readonly struct BarPrefabParts
    {
        public BarPrefabParts(
            RectTransform root,
            Image background,
            Image fill,
            IReadOnlyList<Image> cells
        )
        {
            Root = root;
            Background = background;
            Fill = fill;
            Cells = cells;
        }

        public RectTransform Root { get; }
        public Image Background { get; }
        public Image Fill { get; }
        public IReadOnlyList<Image> Cells { get; }
    }
}
