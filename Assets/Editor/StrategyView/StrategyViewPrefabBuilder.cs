using System.Collections.Generic;
using System.IO;
using Rebellion.Generation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the generated Strategy View and related window prefabs.
/// </summary>
public static class StrategyViewPrefabBuilder
{
    private const string _defaultPreviewThemeId = "DEFAULT";
    private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";
    private const string _planetSystemWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/PlanetSystemWindow.prefab";
    private const string _planetSystemPlanetPrefabPath =
        "Assets/Prefabs/UI/StrategyView/PlanetSystemPlanet.prefab";
    private const string _confirmDialogWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/ConfirmDialogWindow.prefab";
    private const string _battleAlertWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/BattleAlertWindow.prefab";
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
    private const string _missionsParticipantRowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/Missions/MissionsParticipantRow.prefab";
    private const string _missionCreateWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/MissionCreateWindow.prefab";
    private const string _missionCreateParticipantRowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/Missions/MissionCreateParticipantRow.prefab";
    private const string _statusWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/StatusWindow.prefab";
    private const string _advisorReportWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/AdvisorReportWindow.prefab";
    private const string _finderWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/FinderWindow.prefab";
    private const string _messagesWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";
    private const string _encyclopediaWindowPrefabPath =
        "Assets/Prefabs/UI/StrategyView/EncyclopediaWindow.prefab";
    private const string _planetSystemClusterPrefabPath =
        "Assets/Prefabs/UI/StrategyView/PlanetSystemCluster.prefab";
    private const string _commonScrollAreaPrefabPath = "Assets/Prefabs/UI/Common/ScrollArea.prefab";
    private const string _commonTextInputPrefabPath = "Assets/Prefabs/UI/Common/TextInput.prefab";
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
    private const string _modelessWindowLayerName = "ModelessWindows";
    private const string _modalWindowLayerName = "ModalWindows";
    private const string _modalInputBlockerName = "ModalInputBlocker";
    private const string _modalBackgroundDimName = "ModalBackgroundDim";
    private const string _overlayLayerName = "Overlay";
    private const string _contextMenuName = "ContextMenu";
    private const string _contextMenuPanelTemplateName = "PanelTemplate";
    private const string _contextMenuCommandTemplateName = "CommandTemplate";
    private const string _galaxyBackgroundImageName = "BackgroundImage";
    private const string _planetSystemClustersName = "PlanetSystemClusters";
    private const string _activeGalacticInformationFilterLabelName =
        "ActiveGalacticInformationFilterLabel";
    private const string _galaxyStarPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_galaxy_star_preview.png";
    private const string _planetPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_planetsystem_planet_preview.png";
    private const string _windowOpenSectorPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_open_sector_button.png";
    private const string _windowOpenSectorDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_open_sector_button_pressed.png";
    private const string _windowSwapPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_swap_button.png";
    private const string _windowClosePreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_close_button.png";
    private const string _windowCloseDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_close_button_pressed.png";
    private const string _windowMinimizePreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_minimize_button.png";
    private const string _windowMinimizeDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_window_minimize_button_pressed.png";
    private const string _confirmButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_confirm_ok_button.png";
    private const string _confirmButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_confirm_ok_button_pressed.png";
    private const string _cancelButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_confirm_cancel_button.png";
    private const string _cancelButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_confirm_cancel_button_pressed.png";
    private const string _facilityWindowBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_window_background.png";
    private const string _facilityWindowTabPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_tab.png";
    private const string _facilityManufacturingStripPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_manufacturing_strip.png";
    private const string _facilityManufacturingCardPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_manufacturing_lane_card.png";
    private const string _facilityManufacturingCardStatePreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_manufacturing_lane_state.png";
    private const string _facilityManufacturingSelectPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_manufacturing_selection.png";
    private const string _facilityInventoryItemPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_facility_inventory_item.png";
    private const string _facilityCardEntityPreviewPath =
        "Assets/Resources/Art/HD/UI/Units/ent_building_ship_yard.png";
    private const string _facilityCardEntitySmallPreviewPath =
        "Assets/Resources/Art/HD/UI/Units/ent_building_ship_yard_small.png";
    private const string _constructionWindowBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_window_background.png";
    private const string _constructionOpenButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_open_button.png";
    private const string _constructionOpenButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_open_button_pressed.png";
    private const string _constructionInfoButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_info_button.png";
    private const string _constructionInfoButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_info_button_pressed.png";
    private const string _constructionOkButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_ok_button.png";
    private const string _constructionOkButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_ok_button_pressed.png";
    private const string _constructionOkButtonDisabledPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_ok_button_disabled.png";
    private const string _constructionCancelButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_cancel_button.png";
    private const string _constructionCancelButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_cancel_button_pressed.png";
    private const string _constructionIncrementButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_increment_button.png";
    private const string _constructionIncrementButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_increment_button_pressed.png";
    private const string _constructionDecrementButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_decrement_button.png";
    private const string _constructionDecrementButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_decrement_button_pressed.png";
    private const string _constructionDropdownBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_construction_dropdown_background.png";
    private const string _defenseWindowBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_defense_window_background.png";
    private const string _defenseWindowTabPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_defense_tab.png";
    private const string _defenseSelectionPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_defense_selection.png";
    private const string _defensePersonnelBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_defense_personnel_background.png";
    private const string _defenseEnrouteBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_defense_window_enroute_background.png";
    private const string _fleetWindowBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_fleet_window_background.png";
    private const string _fleetIconPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_fleet_window_icon.png";
    private const string _fleetSelectionPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_fleet_selection.png";
    private const string _fleetShipSelectionPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_fleet_ship_selection.png";
    private const string _fleetPersonnelEnrouteBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_defense_window_enroute_background.png";
    private const string _fleetDetailBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_fleet_detail_background.png";
    private const string _fleetTabPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_fleet_tab.png";
    private const string _missionsWindowBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missions_window_background.png";
    private const string _missionsTabPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missions_tab.png";
    private const string _missionCreateMissionBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missioncreate_mission_background.png";
    private const string _missionCreatePersonnelBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missioncreate_personnel_background.png";
    private const string _missionCreateMoveRightButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missioncreate_move_right_button.png";
    private const string _missionCreateMoveRightButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missioncreate_move_right_button_pressed.png";
    private const string _missionCreateMoveLeftButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missioncreate_move_left_button.png";
    private const string _missionCreateMoveLeftButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_missioncreate_move_left_button_pressed.png";
    private const string _statusWindowBackgroundPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_status_window_background.png";
    private const string _advisorReportGalaxyPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_advisor_report_galaxy.png";
    private const string _statusInfoButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_status_info_button.png";
    private const string _statusInfoButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_status_info_button_pressed.png";
    private const string _statusInfoButtonDisabledPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_status_info_button_disabled.png";
    private const string _statusCloseButtonPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_status_close_button.png";
    private const string _statusCloseButtonDownPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_status_close_button_pressed.png";
    private const string _scrollUpArrowPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_scrollbar_arrow_up.png";
    private const string _scrollDownArrowPreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_scrollbar_arrow_pressed_2.png";
    private const string _scrollBarMiddlePreviewPath =
        "Assets/Resources/Art/HD/UI/StrategyView/ui_strategyview_scrollbar_middle.png";
    private const float _sourcePixelsPerUnit = UILayout.HdPixelsPerSourceUnit;
    private const float _screenWidth = 3840f / _sourcePixelsPerUnit;
    private const float _screenHeight = 2160f / _sourcePixelsPerUnit;
    private const int _fullSizeTextureMaxSize = 4096;
    private const string _fullSizeStrategyViewTextureRoot =
        "Assets/Resources/Art/HD/UI/StrategyView/";
    private const int _contextMenuIconPreviewSize = 14;
    private const int _contextMenuIconPanelWidth = 25;
    private const int _contextMenuCommandHeight = 20;
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
    private const int _constructionWindowOffsetX = 15;
    private const int _constructionWindowOffsetY = 25;
    private const int _itemDragStartDistance = 5;
    private const float _sectorCoordinateRange = 1024f;
    private const float _sectorCoordinateScaleX = 13f;
    private const float _sectorCoordinateScaleY = 10f;
    private const float _galaxyProjectionSourceRange = 512f;
    private const float _galaxyProjectionWidth = 315f;
    private const float _galaxyProjectionHeight = 215f;
    private const int _planetPreviewWidth = 37;
    private const int _planetSystemPlanetImageWidth = 37;
    private const int _planetSystemPlanetImageHeight = 37;
    private const int _planetSystemFacilityIconWidth = 27;
    private const int _planetSystemFacilityIconHeight = 18;
    private const int _planetSystemDefenseIconWidth = 27;
    private const int _planetSystemDefenseIconHeight = 19;
    private const int _planetSystemFleetIconWidth = 28;
    private const int _planetSystemFleetIconHeight = 18;
    private const int _planetSystemMissionIconWidth = 28;
    private const int _planetSystemMissionIconHeight = 19;
    private const int _defenseItemWidth = 61;
    private const int _defenseItemHeight = 70;
    private const int _defenseItemSpacing = 10;
    private const int _defenseItemImageHeight = 25;
    private const int _defenseItemLabelY = 30;
    private const int _defenseItemLabelWidth = 66;
    private const int _defenseItemLabelHeight = 30;
    private const int _windowChromeButtonWidth = 14;
    private const int _windowChromeButtonHeight = 14;
    private const int _fleetDetailBackgroundWidth = 132;
    private const int _fleetDetailBackgroundHeight = 266;
    private const int _fleetBannerWidth = 122;
    private const int _fleetBannerHeight = 50;
    private const int _fleetListSelectionWidth = 73;
    private const int _fleetListSelectionHeight = 47;
    private const int _fleetListIconWidth = 66;
    private const int _fleetListIconHeight = 25;
    private const int _fleetDetailSelectionWidth = 114;
    private const int _fleetDetailSelectionHeight = 37;
    private const int _facilityManufacturingStripWidth = 46;
    private const int _facilityManufacturingStripHeight = 226;
    private const int _facilityManufacturingCardWidth = 166;
    private const int _facilityManufacturingCardHeight = 79;
    private const int _facilityManufacturingCardStateWidth = 162;
    private const int _facilityManufacturingCardStateHeight = 13;
    private const int _missionListSelectionWidth = 73;
    private const int _missionListSelectionHeight = 48;
    private const int _missionCreateHeaderWidth = 108;
    private const int _missionCreateHeaderHeight = 27;
    private const int _missionCreateMoveButtonWidth = 16;
    private const int _missionCreateMoveButtonHeight = 16;
    private const int _constructionActionButtonWidth = 66;
    private const int _constructionActionButtonHeight = 33;
    private const int _constructionDropdownButtonWidth = 65;
    private const int _constructionDropdownButtonHeight = 18;
    private const int _constructionCountButtonWidth = 13;
    private const int _constructionCountButtonHeight = 8;
    private static readonly Color32 _sectorWindowBackgroundOverlay = new Color32(57, 57, 57, 230);
    private static readonly Color32 _modalBackgroundDimColor = new Color32(80, 80, 80, 160);

    private static FactionThemes _previewThemes;

    /// <summary>
    /// Gets the preview theme.
    /// </summary>
    private static FactionTheme PreviewTheme => GetDefaultPreviewTheme();

    private static IReadOnlyList<FactionTheme> PreviewThemes
    {
        get
        {
            _previewThemes ??= ResourceManager.GetConfig<FactionThemes>();
            return _previewThemes;
        }
    }

    /// <summary>
    /// Gets the explicit default theme used for faction-neutral prefab previews.
    /// </summary>
    /// <returns>The configured default preview theme.</returns>
    private static FactionTheme GetDefaultPreviewTheme()
    {
        for (int index = 0; index < PreviewThemes.Count; index++)
        {
            FactionTheme theme = PreviewThemes[index];
            if (
                string.Equals(
                    theme?.FactionInstanceID,
                    _defaultPreviewThemeId,
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return theme;
            }
        }

        throw new System.InvalidOperationException(
            $"Faction theme '{_defaultPreviewThemeId}' is required for prefab authoring."
        );
    }

    /// <summary>
    /// Gets one deterministic playable-theme preview for a faction tab slot.
    /// </summary>
    /// <param name="index">The zero-based preview slot index.</param>
    /// <returns>The matching complete faction theme, or the default preview theme.</returns>
    private static FactionTheme GetFactionTabPreviewTheme(int index)
    {
        List<FactionTheme> themes = new List<FactionTheme>();
        for (int themeIndex = 0; themeIndex < PreviewThemes.Count; themeIndex++)
        {
            FactionTheme theme = PreviewThemes[themeIndex];
            if (
                theme?.TacticalHUDLayout != null
                && theme.StrategyWindows?.Finder?.SystemsButton != null
                && !string.Equals(
                    theme.FactionInstanceID,
                    _defaultPreviewThemeId,
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                themes.Add(theme);
            }
        }

        themes.Sort(
            (left, right) =>
                string.Compare(
                    left.FactionInstanceID,
                    right.FactionInstanceID,
                    System.StringComparison.Ordinal
                )
        );
        if (index < 0 || index >= themes.Count)
            return PreviewTheme;

        return themes[index];
    }

    /// <summary>
    /// Authors the themed HUD command-button views in their configured order.
    /// </summary>
    /// <param name="parent">The HUD button container.</param>
    /// <returns>The authored HUD button views.</returns>
    private static List<UIRaycastArea> CreateHudButtonViews(Transform parent)
    {
        List<UIRaycastArea> views = new List<UIRaycastArea>();
        List<StrategyHudButtonTheme> buttons = PreviewTheme?.TacticalHUDLayout?.Buttons;
        if (buttons == null)
            return views;

        for (int i = 0; i < buttons.Count; i++)
        {
            StrategyHudButtonTheme button = buttons[i];
            views.Add(CreateHudButtonView($"{button.Action}Button", parent, button));
        }

        return views;
    }

    /// <summary>
    /// Authors the themed HUD message-notification image slots.
    /// </summary>
    /// <param name="parent">The HUD notification container.</param>
    /// <returns>The authored notification images.</returns>
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
                $"{notification.Tab}MessageNotificationButton",
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

    /// <summary>
    /// Authors the Strategy advisor hierarchy from the first complete faction preview theme.
    /// </summary>
    /// <param name="parent">The HUD transform that owns the advisor.</param>
    /// <returns>The configured advisor view.</returns>
    private static StrategyAdvisorView CreateStrategyAdvisorView(Transform parent)
    {
        GameObject root = new GameObject(
            "AdvisorView",
            typeof(RectTransform),
            typeof(StrategyAdvisorView)
        );
        root.transform.SetParent(parent, false);
        FillParent(root.GetComponent<RectTransform>());

        StrategyAdvisorTheme theme = GetFactionTabPreviewTheme(0)?.StrategyAdvisor;
        if (theme == null)
            throw new MissingReferenceException("Preview StrategyAdvisor theme is missing.");

        RawImage protocolImage = CreateRawImage(
            "ProtocolImage",
            root.transform,
            theme.GetFramePath(theme.ProtocolIdleBitmapID, 0, false),
            theme.ProtocolSourceLayout
        );
        RawImage droidImage = CreateRawImage(
            "DroidImage",
            root.transform,
            theme.GetFramePath(theme.DroidIdleBitmapID, 0, true),
            theme.DroidSourceLayout
        );
        UIRaycastArea protocolInput = CreateHudButtonView(
            "ProtocolInput",
            root.transform,
            theme.ProtocolSourceLayout
        );
        UIRaycastArea droidInput = CreateHudButtonView(
            "DroidInput",
            root.transform,
            theme.DroidSourceLayout
        );

        StrategyAdvisorView view = EnableRuntimeComponent(root.GetComponent<StrategyAdvisorView>());
        AssignReference(view, "protocolImage", protocolImage);
        AssignReference(view, "droidImage", droidImage);
        AssignReference(view, "protocolInput", protocolInput);
        AssignReference(view, "droidInput", droidInput);
        return view;
    }

    /// <summary>
    /// Authors one HUD input surface from a complete button theme.
    /// </summary>
    /// <param name="name">The input surface name.</param>
    /// <param name="parent">The owning HUD transform.</param>
    /// <param name="theme">The button theme supplying hit-area geometry.</param>
    /// <returns>The configured raycast area.</returns>
    private static UIRaycastArea CreateHudButtonView(
        string name,
        Transform parent,
        StrategyHudButtonTheme theme
    )
    {
        SourceRectLayout hitArea = theme?.HitArea;
        return CreateHudButtonView(name, parent, hitArea);
    }

    /// <summary>
    /// Authors one HUD input surface from source-space geometry.
    /// </summary>
    /// <param name="name">The input surface name.</param>
    /// <param name="parent">The owning HUD transform.</param>
    /// <param name="hitArea">The source-space hit-area geometry.</param>
    /// <returns>The configured raycast area.</returns>
    private static UIRaycastArea CreateHudButtonView(
        string name,
        Transform parent,
        SourceRectLayout hitArea
    )
    {
        GameObject button = new GameObject(name, typeof(RectTransform));
        button.SetActive(false);
        button.transform.SetParent(parent, false);
        SetSourceRect(
            button.GetComponent<RectTransform>(),
            hitArea?.X ?? 0,
            hitArea?.Y ?? 0,
            hitArea?.Width ?? 1,
            hitArea?.Height ?? 1
        );

        RawImage hitAreaImage = CreatePanelImage(
            "RaycastTargetImage",
            button.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(hitAreaImage.rectTransform, 0, 0, hitArea?.Width ?? 1, hitArea?.Height ?? 1);
        UIRaycastArea view = EnableRuntimeComponent(button.AddComponent<UIRaycastArea>());
        AssignReference(view, "raycastTargetImage", hitAreaImage);
        button.SetActive(true);
        return view;
    }

    /// <summary>
    /// Authors the reusable bookmark row template.
    /// </summary>
    /// <param name="parent">The bookmark-bar transform.</param>
    /// <param name="layout">The bookmark source-space layout.</param>
    /// <returns>The configured bookmark template.</returns>
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
        BookmarkSlotView view = EnableRuntimeComponent(slot.GetComponent<BookmarkSlotView>());
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

    /// <summary>
    /// Rebuilds the authored Strategy View root prefab and its registered window references.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Strategy View Root Prefab")]
    public static void BuildStrategyViewRootPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        Directory.CreateDirectory(Path.GetDirectoryName(_prefabPath));
        _previewThemes = null;
        EnsureStrategyViewBackgroundTexturesImportedAtFullSize();
        PlanetSystemWindowView planetSystemWindowPrefab = LoadWindowPrefab<PlanetSystemWindowView>(
            _planetSystemWindowPrefabPath
        );
        ConfirmDialogWindowView confirmDialogWindowPrefab =
            LoadWindowPrefab<ConfirmDialogWindowView>(_confirmDialogWindowPrefabPath);
        BattleAlertWindowView battleAlertWindowPrefab = LoadWindowPrefab<BattleAlertWindowView>(
            _battleAlertWindowPrefabPath
        );
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
        AdvisorReportWindowView advisorReportWindowPrefab =
            LoadWindowPrefab<AdvisorReportWindowView>(_advisorReportWindowPrefabPath);
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

        StrategyController controller = EnableRuntimeComponent(
            root.AddComponent<StrategyController>()
        );
        GameFlowController gameFlowController = EnableRuntimeComponent(
            root.AddComponent<GameFlowController>()
        );
        AssignReference(gameFlowController, "strategyController", controller);

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
        GalaxyMapView galaxyMapView = EnableRuntimeComponent(
            galaxyMap.AddComponent<GalaxyMapView>()
        );

        RectTransform background = CreateChildLayer(
            _galaxyBackgroundImageName,
            galaxyMap.transform
        );
        RawImage backgroundImage = background.gameObject.AddComponent<RawImage>();
        backgroundImage.raycastTarget = false;
        GalaxyBackground previewGalaxyBackground = PreviewTheme?.GalaxyBackground;
        Texture2D backgroundTexture = LoadTexture(previewGalaxyBackground?.ImagePath);
        backgroundImage.texture = backgroundTexture;
        if (backgroundTexture != null)
            SetSourceRect(
                background,
                previewGalaxyBackground?.SourcePosition?.X ?? _defaultGalaxyBackgroundX,
                previewGalaxyBackground?.SourcePosition?.Y ?? _defaultGalaxyBackgroundY,
                ToSourceUnits(backgroundTexture.width),
                ToSourceUnits(backgroundTexture.height)
            );

        RectTransform planetSystemClusters = CreateChildLayer(
            _planetSystemClustersName,
            galaxyMap.transform
        );
        GalacticInformationDisplayTheme galacticInformationTheme =
            PreviewTheme?.GalacticInformationDisplay
            ?? throw new MissingReferenceException(
                "Preview GalacticInformationDisplay theme is missing."
            );
        TextMeshProUGUI activeFilterLabel = CreateTextLabel(
            _activeGalacticInformationFilterLabelName,
            galaxyMap.transform
        );
        activeFilterLabel.text = string.Empty;
        activeFilterLabel.color = galacticInformationTheme.GetActiveFilterLabelColor();
        activeFilterLabel.fontSize = galacticInformationTheme.ActiveFilterLabelFontSize;
        activeFilterLabel.alignment = TextAlignmentOptions.Top;
        activeFilterLabel.raycastTarget = false;
        SourceRectLayout activeFilterLabelLayout =
            galacticInformationTheme.ActiveFilterLabelSourceLayout
            ?? throw new MissingReferenceException(
                "Preview GalacticInformationDisplay active filter label layout is missing."
            );
        SetSourceRect(
            activeFilterLabel.rectTransform,
            activeFilterLabelLayout.X,
            activeFilterLabelLayout.Y,
            activeFilterLabelLayout.Width,
            activeFilterLabelLayout.Height
        );
        activeFilterLabel.gameObject.SetActive(false);

        GameObject bookmarks = CreateLayer("Bookmarks", root.transform);
        RectTransform bookmarksRect = bookmarks.GetComponent<RectTransform>();
        SetStrategySurfaceRect(bookmarksRect);
        BookmarkBarView bookmarkBarView = EnableRuntimeComponent(
            bookmarks.AddComponent<BookmarkBarView>()
        );
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
        StrategyHudView hudView = EnableRuntimeComponent(hud.AddComponent<StrategyHudView>());

        RectTransform hudBackground = CreateChildLayer(_hudBackgroundImageName, hud.transform);
        RawImage hudBackgroundImage = hudBackground.gameObject.AddComponent<RawImage>();
        hudBackgroundImage.texture = LoadTexture(PreviewTheme?.TacticalHUDLayout?.ImagePath);
        hudBackgroundImage.enabled = hudBackgroundImage.texture != null;
        hudBackgroundImage.raycastTarget = false;
        StrategyAdvisorView advisorView = CreateStrategyAdvisorView(hud.transform);

        RectTransform hudTextFields = CreateChildLayer(_hudTextFieldsName, hud.transform);
        TacticalHUDLayout previewHudLayout = PreviewTheme?.TacticalHUDLayout;
        TextMeshProUGUI tickLabel = CreateHudLabel(
            _hudTickTextFieldName,
            "0",
            hudTextFields,
            previewHudLayout?.TickCounterSourceLayout,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI rawMaterialsLabel = CreateHudLabel(
            _hudRawMaterialsTextFieldName,
            "0",
            hudTextFields,
            previewHudLayout?.RawMaterialsSourceLayout,
            TextAlignmentOptions.TopRight
        );
        TextMeshProUGUI refinedMaterialsLabel = CreateHudLabel(
            _hudRefinedMaterialsTextFieldName,
            "0",
            hudTextFields,
            previewHudLayout?.RefinedMaterialsSourceLayout,
            TextAlignmentOptions.TopRight
        );
        TextMeshProUGUI maintenanceLabel = CreateHudLabel(
            _hudMaintenanceTextFieldName,
            "0",
            hudTextFields,
            previewHudLayout?.MaintenanceSourceLayout,
            TextAlignmentOptions.TopRight
        );
        RawImage speedIndicatorImage = CreateRawImage(
            "SpeedIndicatorImage",
            hud.transform,
            previewHudLayout?.SpeedIndicators?.MediumImagePath,
            previewHudLayout?.SpeedIndicatorSourceLayout
        );
        RawImage galacticInformationDisplayImage = CreateRawImage(
            "GalacticInformationDisplayImage",
            hud.transform,
            previewHudLayout?.GalacticInformationDisplayImagePath,
            previewHudLayout?.GalacticInformationDisplayImageLayout
        );
        List<RawImage> messageNotificationImages = CreateHudMessageNotificationImages(
            hud.transform
        );
        List<Button> messageNotificationButtons = CreateButtons(messageNotificationImages);
        List<UIRaycastArea> hudButtonViews = CreateHudButtonViews(hud.transform);
        UIRaycastArea speedContextView = CreateHudButtonView(
            "GameSpeedButton",
            hud.transform,
            PreviewTheme?.TacticalHUDLayout?.SpeedContextSourceLayout
        );
        RawImage pressedMainButtonImage = CreateRawImage(
            "PressedMainButtonImage",
            hud.transform,
            null,
            0,
            0
        );
        pressedMainButtonImage.enabled = false;
        pressedMainButtonImage.raycastTarget = false;
        pressedMainButtonImage.gameObject.SetActive(false);

        GameObject windows = CreateLayer(_windowLayerName, root.transform);
        RectTransform windowsRect = windows.GetComponent<RectTransform>();
        SetStrategySurfaceRect(windowsRect);
        UIWindowManager windowManager = EnableRuntimeComponent(
            windows.AddComponent<UIWindowManager>()
        );
        StrategyWindowLayerView windowsView = EnableRuntimeComponent(
            windows.AddComponent<StrategyWindowLayerView>()
        );
        RectTransform modelessWindowLayer = CreateChildLayer(
            _modelessWindowLayerName,
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
        RawImage modalBackgroundDim = CreatePanelImage(
            _modalBackgroundDimName,
            modalWindowLayer,
            _modalBackgroundDimColor
        );
        FillParent(modalBackgroundDim.rectTransform);
        modalBackgroundDim.raycastTarget = false;
        modalBackgroundDim.gameObject.SetActive(false);
        StrategyOverlayView overlayView = CreateStrategyOverlayView(root.transform);
        GalacticInformationLegendView galacticInformationLegend =
            CreateGalacticInformationLegendView(root.transform);
        GalacticInformationDisplayView galacticInformationDisplay =
            CreateGalacticInformationDisplayView(root.transform);

        StrategyContextMenuPresenter contextMenu = CreateContextMenu(root.transform);

        AssignReference(controller, "strategySurface", surfaceRect);
        AssignReference(controller, "strategySurfaceImage", surfaceImage);
        AssignReference(controller, "strategyHud", hudView);
        AssignReference(controller, "strategyContextMenu", contextMenu);
        AssignReference(controller, "strategyOverlay", overlayView);
        AssignReference(controller, "strategyWindowLayerView", windowsView);
        AssignReference(controller, "strategyWindowManager", windowManager);
        AssignReference(controller, "galacticInformationDisplay", galacticInformationDisplay);
        AssignReference(controller, "galacticInformationLegend", galacticInformationLegend);
        AssignReference(windowsView, "modelessWindowLayer", modelessWindowLayer);
        AssignReference(windowsView, "modalWindowLayer", modalWindowLayer);
        AssignReference(windowsView, "modalInputBlockerImage", modalInputBlocker);
        AssignReference(windowsView, "modalBackgroundDimImage", modalBackgroundDim);
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
            advisorReportWindowPrefab,
            messagesWindowPrefab,
            confirmDialogWindowPrefab,
            battleAlertWindowPrefab,
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
        AssignReference(
            hudView,
            "galacticInformationDisplayImage",
            galacticInformationDisplayImage
        );
        AssignReference(hudView, "pressedMainButtonImage", pressedMainButtonImage);
        AssignReferenceArray(hudView, "messageNotificationImages", messageNotificationImages);
        AssignReferenceArray(hudView, "messageNotificationButtons", messageNotificationButtons);
        AssignReferenceArray(hudView, "buttonViews", hudButtonViews);
        AssignReference(hudView, "speedContextView", speedContextView);
        AssignReference(hudView, "advisorView", advisorView);
        AssignReference(galaxyMapView, "background", background);
        AssignReference(galaxyMapView, "backgroundImage", backgroundImage);
        AssignReference(galaxyMapView, "planetSystemClusters", planetSystemClusters);
        AssignReference(galaxyMapView, "activeFilterLabel", activeFilterLabel);
        AssignReference(galaxyMapView, "planetSystemClusterPrefab", planetSystemClusterPrefab);

        SaveGeneratedPrefabAsset(root, _prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Rebuilds shared controls, every Strategy window, and the Strategy View root in dependency
    /// order.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild All Strategy View Prefabs")]
    public static void RebuildAllStrategyViewPrefabs()
    {
        UIAuthoringGuard.EnsureEditMode();
        _previewThemes = null;
        EnsureStrategyViewBackgroundTexturesImportedAtFullSize();
        CommonUIPrefabBuilder.RebuildSharedControlPrefabs();

        PlanetSystemPlanetView planetPrefab = BuildPlanetSystemPlanetPrefab();
        BuildPlanetSystemWindowPrefab(planetPrefab);
        BuildPlanetSystemClusterPrefab();
        BuildConfirmDialogWindowPrefab();
        BuildBattleAlertWindowPrefab();
        BuildFacilityWindowPrefab();
        BuildConstructionWindowPrefab();
        BuildDefenseWindowPrefab();
        BuildFleetWindowPrefab();
        BuildMissionsWindowPrefab();
        BuildMissionCreateWindowPrefab();
        BuildStatusWindowPrefab();
        BuildAdvisorReportWindowPrefab();
        BuildFinderWindowPrefab();
        BuildMessagesWindowPrefab();
        BuildEncyclopediaWindowPrefab();
        BuildStrategyViewRootPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Rebuilds the planet-system prefabs and refreshes the Strategy window registry.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Planet System Prefabs")]
    public static void BuildPlanetSystemPrefabs()
    {
        UIAuthoringGuard.EnsureEditMode();
        Directory.CreateDirectory(Path.GetDirectoryName(_planetSystemPlanetPrefabPath));

        PlanetSystemPlanetView planetPrefab = BuildPlanetSystemPlanetPrefab();
        BuildPlanetSystemWindowPrefab(planetPrefab);
        RegisterWindowPrefabsInRoot();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Rebuilds the planet-system cluster prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Planet System Cluster Prefab")]
    public static void RebuildPlanetSystemClusterPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildPlanetSystemClusterPrefab();
    }

    /// <summary>
    /// Refreshes the Strategy root's serialized window-prefab registry.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Refresh Strategy View Root Window Prefabs")]
    public static void RefreshStrategyViewRootWindowPrefabs()
    {
        UIAuthoringGuard.EnsureEditMode();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the confirmation-dialog window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Confirm Dialog Window Prefab")]
    public static void RebuildConfirmDialogWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildConfirmDialogWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the battle-alert window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Battle Alert Window Prefab")]
    public static void RebuildBattleAlertWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        EnsureStrategyViewBackgroundTexturesImportedAtFullSize();
        BuildBattleAlertWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the facility window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Facility Window Prefab")]
    public static void RebuildFacilityWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildFacilityWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the construction window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Construction Window Prefab")]
    public static void RebuildConstructionWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildConstructionWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the defense window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Defense Window Prefab")]
    public static void RebuildDefenseWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildDefenseWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the fleet window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Fleet Window Prefab")]
    public static void RebuildFleetWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildFleetWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the missions window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Missions Window Prefab")]
    public static void RebuildMissionsWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildMissionsWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the mission-creation window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Mission Create Window Prefab")]
    public static void RebuildMissionCreateWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildMissionCreateWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the status window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Status Window Prefab")]
    public static void RebuildStatusWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildStatusWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the advisor-report window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Advisor Report Window Prefab")]
    public static void RebuildAdvisorReportWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildAdvisorReportWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the Finder window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Finder Window Prefab")]
    public static void RebuildFinderWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildFinderWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the messages window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Messages Window Prefab")]
    public static void RebuildMessagesWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildMessagesWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Rebuilds and registers the encyclopedia window prefab.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Rebuild Encyclopedia Window Prefab")]
    public static void RebuildEncyclopediaWindowPrefab()
    {
        UIAuthoringGuard.EnsureEditMode();
        BuildEncyclopediaWindowPrefab();
        RegisterWindowPrefabsInRoot();
    }

    /// <summary>
    /// Authors the Facility window prefab.
    /// </summary>
    /// <returns>The generated Facility window view.</returns>
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
        FacilityWindowView view = EnableRuntimeComponent(window.GetComponent<FacilityWindowView>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _facilityWindowBackgroundPreviewPath,
            0,
            0,
            windowWidth,
            windowHeight
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.GetComponent<UIWindow>(), windowWidth);
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
            CreateRawImage(
                "OpenSectorButtonImage",
                buttons,
                _windowOpenSectorPreviewPath,
                3,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "MinimizeButtonImage",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "CloseButtonImage",
                buttons,
                _windowClosePreviewPath,
                windowWidth - 17,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
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
                $"{FacilityWindowRenderData.OrderedTabs[i]}TabButtonImage",
                tabs,
                _facilityWindowTabPreviewPath
            );
            SetSourceRect(tabImage.rectTransform, i * 38, 20, 38, 33);
            tabImages.Add(tabImage);
        }
        List<Button> tabButtons = CreateButtons(tabImages);
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < tabImages.Count; index++)
            tabPressVisuals.Add(tabImages[index].GetComponent<RawImagePressVisual>());

        RawImage strip = CreateRawImage(
            "ManufacturingStripImage",
            window.transform,
            _facilityManufacturingStripPreviewPath,
            6,
            72,
            _facilityManufacturingStripWidth,
            _facilityManufacturingStripHeight
        );

        List<ManufacturingLaneCardView> manufacturingCards = new List<ManufacturingLaneCardView>
        {
            CreateManufacturingLaneCardView(
                window.transform,
                "ShipyardsManufacturingLaneCard",
                56,
                120
            ),
            CreateManufacturingLaneCardView(
                window.transform,
                "TrainingManufacturingLaneCard",
                138,
                201
            ),
            CreateManufacturingLaneCardView(
                window.transform,
                "ConstructionManufacturingLaneCard",
                220,
                281
            ),
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

        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignFacilityWindowTabTextures(view);
        AssignReference(view, "manufacturingStripImage", strip);
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

    /// <summary>
    /// Assigns the complete authored facility-tab texture set.
    /// </summary>
    /// <param name="view">The generated facility window.</param>
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

    /// <summary>
    /// Authors the Construction window prefab.
    /// </summary>
    /// <returns>The generated Construction window view.</returns>
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
        ConstructionWindowView view = EnableRuntimeComponent(
            window.GetComponent<ConstructionWindowView>()
        );
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _constructionWindowBackgroundPreviewPath,
            0,
            0,
            windowWidth,
            windowHeight
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.GetComponent<UIWindow>(), windowWidth);
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
            CreateRawImage(
                "CloseButtonImage",
                buttons,
                _windowClosePreviewPath,
                windowWidth - 17,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
        };
        int[] buttonActions = { StrategyWindowButtonActions.CloseWindow };
        ConfigureWindowButtons(window.GetComponent<UIWindow>(), buttonImages, buttonActions);
        List<RawImagePressVisual> buttonPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < buttonImages.Count; index++)
            buttonPressVisuals.Add(buttonImages[index].GetComponent<RawImagePressVisual>());

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
        SetSourceRect(buildCountLabel.rectTransform, 20, 196, 118, 15);

        TMP_InputField buildCount = CreateTextInputField(
            "BuildCountInputField",
            window.transform,
            string.Empty,
            141,
            196,
            45,
            17
        );
        buildCount.contentType = TMP_InputField.ContentType.IntegerNumber;
        buildCount.characterValidation = TMP_InputField.CharacterValidation.Integer;
        buildCount.characterLimit = byte.MaxValue.ToString().Length;
        buildCount.textComponent.fontSize = 13;
        buildCount.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        buildCount.SetTextWithoutNotify("1");

        RawImage increment = CreateRawImage(
            "IncrementButtonImage",
            window.transform,
            _constructionIncrementButtonPreviewPath,
            189,
            196,
            _constructionCountButtonWidth,
            _constructionCountButtonHeight
        );
        Button incrementButton = CreateButton(increment);
        RawImage decrement = CreateRawImage(
            "DecrementButtonImage",
            window.transform,
            _constructionDecrementButtonPreviewPath,
            189,
            205,
            _constructionCountButtonWidth,
            _constructionCountButtonHeight
        );
        Button decrementButton = CreateButton(decrement);

        TextMeshProUGUI constructionCost = CreateTextLabel(
            "ConstructionCostTextField",
            window.transform
        );
        constructionCost.text = "100";
        constructionCost.color = Color.white;
        constructionCost.fontSize = 14;
        constructionCost.alignment = TextAlignmentOptions.Center;
        SetSourceRect(constructionCost.rectTransform, 35, 111, 68, 25);

        TextMeshProUGUI maintenanceCost = CreateTextLabel(
            "MaintenanceCostTextField",
            window.transform
        );
        maintenanceCost.text = "10";
        maintenanceCost.color = Color.white;
        maintenanceCost.fontSize = 14;
        maintenanceCost.alignment = TextAlignmentOptions.Center;
        SetSourceRect(maintenanceCost.rectTransform, 135, 111, 68, 25);

        TextMeshProUGUI completionLabel = CreateTextLabel(
            "CompletionLabelTextField",
            window.transform
        );
        completionLabel.text = "Best Time to Completion";
        completionLabel.color = Color.white;
        completionLabel.fontSize = 11;
        completionLabel.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(completionLabel.rectTransform, 9, 148, 148, 18);

        TextMeshProUGUI completionValue = CreateTextLabel(
            "CompletionValueTextField",
            window.transform
        );
        completionValue.text = "N/A";
        completionValue.color = Color.white;
        completionValue.fontSize = 11;
        completionValue.alignment = TextAlignmentOptions.TopRight;
        SetSourceRect(completionValue.rectTransform, 139, 148, 30, 18);

        TextMeshProUGUI completionDays = CreateTextLabel(
            "CompletionDaysTextField",
            window.transform
        );
        completionDays.text = "Days";
        completionDays.color = Color.white;
        completionDays.fontSize = 11;
        completionDays.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(completionDays.rectTransform, 170, 148, 30, 18);

        TextMeshProUGUI deploymentLabel = CreateTextLabel(
            "DeploymentLabelTextField",
            window.transform
        );
        deploymentLabel.text = "Best Time to Deployment";
        deploymentLabel.color = Color.white;
        deploymentLabel.fontSize = 11;
        deploymentLabel.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(deploymentLabel.rectTransform, 9, 169, 148, 18);

        TextMeshProUGUI deploymentValue = CreateTextLabel(
            "DeploymentValueTextField",
            window.transform
        );
        deploymentValue.text = "N/A";
        deploymentValue.color = Color.white;
        deploymentValue.fontSize = 11;
        deploymentValue.alignment = TextAlignmentOptions.TopRight;
        SetSourceRect(deploymentValue.rectTransform, 139, 169, 30, 18);

        TextMeshProUGUI deploymentDays = CreateTextLabel(
            "DeploymentDaysTextField",
            window.transform
        );
        deploymentDays.text = "Days";
        deploymentDays.color = Color.white;
        deploymentDays.fontSize = 11;
        deploymentDays.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(deploymentDays.rectTransform, 170, 169, 30, 18);

        RawImage dropdownButton = CreateRawImage(
            "DropdownButtonImage",
            window.transform,
            _constructionOpenButtonPreviewPath,
            79,
            90,
            _constructionDropdownButtonWidth,
            _constructionDropdownButtonHeight
        );
        Button dropdownButtonComponent = CreateButton(dropdownButton);
        RawImage infoButton = CreateRawImage(
            "InfoButtonImage",
            window.transform,
            _constructionInfoButtonPreviewPath,
            5,
            224,
            _constructionActionButtonWidth,
            _constructionActionButtonHeight
        );
        Button infoButtonComponent = CreateButton(infoButton);
        RawImage okButton = CreateRawImage(
            "OkButtonImage",
            window.transform,
            _constructionOkButtonPreviewPath,
            73,
            224,
            _constructionActionButtonWidth,
            _constructionActionButtonHeight
        );
        Button okButtonComponent = CreateButton(okButton);
        RawImage cancelButton = CreateRawImage(
            "CancelButtonImage",
            window.transform,
            _constructionCancelButtonPreviewPath,
            141,
            224,
            _constructionActionButtonWidth,
            _constructionActionButtonHeight
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

        string[] dropdownBackgroundNames =
        {
            "TopDropdownBackgroundImage",
            "MiddleDropdownBackgroundImage",
            "BottomDropdownBackgroundImage",
        };
        List<RawImage> dropdownBackgrounds = new List<RawImage>();
        for (int i = 0; i < 3; i++)
        {
            RawImage dropdownBackground = CreateRawImage(
                dropdownBackgroundNames[i],
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

        RectTransform layoutTemplates = CreateChildLayer("LayoutTemplates", window.transform);
        layoutTemplates.gameObject.SetActive(false);
        StrategyDropdownItemView dropdownItemRowTemplate = CreateStrategyDropdownItemTemplate(
            layoutTemplates,
            "DropdownItemRowTemplate",
            new RectInt(0, 4, 180, 70),
            new RectInt(0, 0, 180, 48),
            new RectInt(0, 48, 180, 18),
            _facilityCardEntityPreviewPath,
            "Nebulon-B Frigate",
            12,
            TextAlignmentOptions.Top
        );

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "buttonImages", buttonImages);
        AssignReferenceArray(view, "buttonPressVisuals", buttonPressVisuals);
        AssignIntArray(view, "buttonActions", buttonActions);
        AssignReference(view, "selectedItemImage", selectedItem);
        AssignReference(view, "selectedNameTextField", selectedName);
        AssignReference(view, "buildCountLabelTextField", buildCountLabel);
        AssignReference(view, "buildCountInputField", buildCount);
        AssignReference(view, "incrementButtonImage", increment);
        AssignReference(
            view,
            "incrementButtonPressVisual",
            increment.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "decrementButtonImage", decrement);
        AssignReference(
            view,
            "decrementButtonPressVisual",
            decrement.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "constructionCostTextField", constructionCost);
        AssignReference(view, "maintenanceCostTextField", maintenanceCost);
        AssignReference(view, "completionLabelTextField", completionLabel);
        AssignReference(view, "completionValueTextField", completionValue);
        AssignReference(view, "completionDaysTextField", completionDays);
        AssignReference(view, "deploymentLabelTextField", deploymentLabel);
        AssignReference(view, "deploymentValueTextField", deploymentValue);
        AssignReference(view, "deploymentDaysTextField", deploymentDays);
        AssignReference(view, "dropdownButtonImage", dropdownButton);
        AssignReference(
            view,
            "dropdownButtonPressVisual",
            dropdownButton.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "infoButtonImage", infoButton);
        AssignReference(
            view,
            "infoButtonPressVisual",
            infoButton.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "okButtonImage", okButton);
        AssignReference(view, "okButtonPressVisual", okButton.GetComponent<RawImagePressVisual>());
        AssignReference(view, "cancelButtonImage", cancelButton);
        AssignReference(
            view,
            "cancelButtonPressVisual",
            cancelButton.GetComponent<RawImagePressVisual>()
        );
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
        AssignReference(view, "dropdownItemRowTemplate", dropdownItemRowTemplate);
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_windowClosePreviewPath));
        AssignReference(
            view,
            "incrementButtonUpTexture",
            LoadTexture(_constructionIncrementButtonPreviewPath)
        );
        AssignReference(
            view,
            "incrementButtonDownTexture",
            LoadTexture(_constructionIncrementButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "decrementButtonUpTexture",
            LoadTexture(_constructionDecrementButtonPreviewPath)
        );
        AssignReference(
            view,
            "decrementButtonDownTexture",
            LoadTexture(_constructionDecrementButtonDownPreviewPath)
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
            "okButtonDisabledTexture",
            LoadTexture(_constructionOkButtonDisabledPreviewPath)
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

        dropdown.gameObject.SetActive(false);
        GameObject saved = SaveGeneratedPrefabAsset(window, _constructionWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<ConstructionWindowView>();
    }

    /// <summary>
    /// Authors the Defense window prefab.
    /// </summary>
    /// <returns>The generated Defense window view.</returns>
    private static DefenseWindowView BuildDefenseWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_defenseWindowPrefabPath));
        const int windowWidth = 235;
        const int windowHeight = 304;

        GameObject window = new GameObject(
            "DefenseWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        window.SetActive(false);
        DefenseWindowView view = EnableRuntimeComponent(window.AddComponent<DefenseWindowView>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _defenseWindowBackgroundPreviewPath,
            0,
            0,
            windowWidth,
            windowHeight
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.GetComponent<UIWindow>(), windowWidth);
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
            CreateRawImage(
                "OpenSectorButtonImage",
                buttons,
                _windowOpenSectorPreviewPath,
                3,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "MinimizeButtonImage",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "CloseButtonImage",
                buttons,
                _windowClosePreviewPath,
                windowWidth - 17,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
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
        for (int i = 0; i < DefenseWindowRenderData.TabCount; i++)
        {
            RawImage tabImage = CreateRawButton(
                $"{DefenseWindowRenderData.OrderedTabs[i]}TabButtonImage",
                tabs,
                _defenseWindowTabPreviewPath
            );
            SetSourceRect(tabImage.rectTransform, 27 + i * 36, 20, 36, 33);
            tabImages.Add(tabImage);
        }
        List<Button> tabButtons = CreateButtons(tabImages);
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        foreach (RawImage tabImage in tabImages)
            tabPressVisuals.Add(tabImage.GetComponent<RawImagePressVisual>());

        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", window.transform);
        tabTitle.text = "Trooper Regiments";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 13;
        tabTitle.alignment = TextAlignmentOptions.Top;
        SetSourceRect(tabTitle.rectTransform, 2, 51, 231, 16);

        TextMeshProUGUI garrisonRequirement = CreateTextLabel(
            "GarrisonRequirementTextField",
            window.transform
        );
        garrisonRequirement.text = "Garrison Requirement: 0";
        garrisonRequirement.color = Color.white;
        garrisonRequirement.fontSize = 13;
        garrisonRequirement.alignment = TextAlignmentOptions.Top;
        SetSourceRect(garrisonRequirement.rectTransform, 2, 63, 228, 15);

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
        GridLayoutGroup itemsGridLayout = ConfigureGridContent(
            itemsContent,
            _defenseItemWidth + _defenseItemSpacing,
            _defenseItemHeight,
            3
        );

        StrategyUnitCardView itemCardTemplate = CreateDefenseUnitCardTemplate(itemsContent);

        AssignReference(view, "windowShell", window.GetComponent<UIWindow>());
        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "tabTitleTextField", tabTitle);
        AssignReference(view, "garrisonRequirementTextField", garrisonRequirement);
        AssignReference(view, "itemsScrollArea", itemsScrollArea);
        AssignReference(view, "itemsGridLayout", itemsGridLayout);
        AssignReference(view, "itemCardTemplate", itemCardTemplate);

        GameObject saved = SaveGeneratedPrefabAsset(window, _defenseWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<DefenseWindowView>();
    }

    /// <summary>
    /// Authors the Defense window's reusable unit-card template.
    /// </summary>
    /// <param name="parent">The card container transform.</param>
    /// <returns>The configured unit-card template.</returns>
    private static StrategyUnitCardView CreateDefenseUnitCardTemplate(Transform parent)
    {
        GameObject item = new GameObject(
            "ItemCardTemplate",
            typeof(RectTransform),
            typeof(UIPointerGestureRelay),
            typeof(StrategyUnitCardView)
        );
        item.transform.SetParent(parent, false);
        StrategyUnitCardView view = EnableRuntimeComponent(
            item.GetComponent<StrategyUnitCardView>()
        );
        SetSourceRect(
            item.GetComponent<RectTransform>(),
            0,
            0,
            _defenseItemWidth + _defenseItemSpacing,
            _defenseItemHeight
        );

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            item.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;
        SetSourceRect(hitArea.rectTransform, 0, 0, _defenseItemWidth, _defenseItemHeight);
        RawImage background = CreateRawButton(
            "BackgroundImage",
            item.transform,
            _defensePersonnelBackgroundPreviewPath
        );
        SetSourceRect(background.rectTransform, 0, 0, _defenseItemWidth, _defenseItemImageHeight);
        RawImage constructionOverlay = CreateRawButton(
            "ConstructionOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetConstructionSmallImagePath
        );
        SetSourceRect(
            constructionOverlay.rectTransform,
            0,
            0,
            _defenseItemWidth,
            _defenseItemImageHeight
        );
        RawImage entity = CreateRawButton(
            "EntityImage",
            item.transform,
            _facilityCardEntityPreviewPath
        );
        SetSourceRect(entity.rectTransform, 0, 0, _defenseItemWidth, _defenseItemImageHeight);
        RawImage enrouteOverlay = CreateRawButton(
            "EnrouteOverlayImage",
            item.transform,
            _defenseEnrouteBackgroundPreviewPath
        );
        SetSourceRect(
            enrouteOverlay.rectTransform,
            0,
            0,
            _defenseItemWidth,
            _defenseItemImageHeight
        );
        RawImage damagedOverlay = CreateRawButton(
            "DamagedOverlayImage",
            item.transform,
            PreviewTheme?.PlanetOverlayTheme?.UnitTileIcons?.FleetListDamagedIconImagePath
        );
        SetSourceRect(
            damagedOverlay.rectTransform,
            0,
            0,
            _defenseItemWidth,
            _defenseItemImageHeight
        );
        RawImage capturedOverlay = CreateRawButton("CapturedOverlayImage", item.transform);
        SetSourceRect(
            capturedOverlay.rectTransform,
            0,
            0,
            _defenseItemWidth,
            _defenseItemImageHeight
        );
        RawImage selection = CreateRawButton(
            "SelectionImage",
            item.transform,
            _defenseSelectionPreviewPath
        );
        SetSourceRect(selection.rectTransform, 0, 0, _defenseItemWidth, _defenseItemImageHeight);

        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", item.transform);
        nameText.text = "Mon Calamari Regiment";
        nameText.color = Color.white;
        nameText.fontSize = 11;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.maxVisibleLines = 2;
        SetSourceRect(
            nameText.rectTransform,
            0,
            _defenseItemLabelY,
            _defenseItemLabelWidth,
            _defenseItemLabelHeight
        );

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

    /// <summary>
    /// Authors the Fleet window prefab.
    /// </summary>
    /// <returns>The generated Fleet window view.</returns>
    private static FleetWindowView BuildFleetWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_fleetWindowPrefabPath));
        const int windowWidth = 235;
        const int windowHeight = 304;

        GameObject window = new GameObject("FleetWindow", typeof(RectTransform), typeof(UIWindow));
        window.SetActive(false);
        FleetWindowView view = EnableRuntimeComponent(window.AddComponent<FleetWindowView>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _fleetWindowBackgroundPreviewPath,
            0,
            0,
            windowWidth,
            windowHeight
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.GetComponent<UIWindow>(), windowWidth);
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
            CreateRawImage(
                "OpenSectorButtonImage",
                buttons,
                _windowOpenSectorPreviewPath,
                3,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "MinimizeButtonImage",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "CloseButtonImage",
                buttons,
                _windowClosePreviewPath,
                windowWidth - 17,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
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
            29,
            _fleetDetailBackgroundWidth,
            _fleetDetailBackgroundHeight
        );
        RawImage banner = CreateRawImage(
            "BannerImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Fleet?.BannerImagePath,
            103,
            40,
            _fleetBannerWidth,
            _fleetBannerHeight
        );
        RawImage bannerEnrouteOverlay = CreateRawImage(
            "BannerEnrouteOverlayImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Status?.FleetBannerEnrouteImagePath,
            103,
            40,
            _fleetBannerWidth,
            _fleetBannerHeight
        );
        RawImage bannerDamagedOverlay = CreateRawImage(
            "BannerDamagedOverlayImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.Status?.FleetBannerDamagedImagePath,
            103,
            40,
            _fleetBannerWidth,
            _fleetBannerHeight
        );
        TextMeshProUGUI fleetName = CreateTextLabel("FleetNameTextField", window.transform);
        fleetName.text = "Fleet";
        fleetName.color = Color.yellow;
        fleetName.fontSize = 12;
        fleetName.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(fleetName.rectTransform, 100, 32, 126, 16);

        TMP_InputField renameInput = CreateTextInputField(
            "RenameInputField",
            window.transform,
            string.Empty,
            100,
            32,
            126,
            16
        );
        renameInput.gameObject.SetActive(false);

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
            RawImage tabImage = CreateRawButton(
                $"{FleetWindowRenderData.OrderedTabs[i]}TabButtonImage",
                tabs,
                defaultTabTexturePaths[i]
            );
            SetSourceRect(tabImage.rectTransform, 100 + i * 30, 96, 30, 29);
            tabImages.Add(tabImage);
        }
        List<Button> tabButtons = CreateButtons(tabImages);
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        for (int i = 0; i < tabImages.Count; i++)
            tabPressVisuals.Add(tabImages[i].GetComponent<RawImagePressVisual>());

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

        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
        AssignReference(view, "fleetListScrollArea", fleetListScrollArea);
        AssignReference(view, "fleetListRowTemplate", listRowTemplate);
        AssignReference(view, "detailBackgroundImage", detailBackground);
        AssignReference(view, "bannerImage", banner);
        AssignReference(view, "bannerEnrouteOverlayImage", bannerEnrouteOverlay);
        AssignReference(view, "bannerDamagedOverlayImage", bannerDamagedOverlay);
        AssignReference(view, "fleetNameTextField", fleetName);
        AssignReference(view, "renameInputField", renameInput);
        AssignReference(view, "capacityLeftTextField", capacityLeft);
        AssignReference(view, "capacityRightTextField", capacityRight);
        AssignReference(view, "tabsRoot", tabs);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "detailItemsScrollArea", detailItemsScrollArea);
        AssignReference(view, "detailItemsScrollPaddingTemplate", detailItemsScrollPaddingTemplate);
        AssignReference(view, "detailItemTemplate", detailItemTemplate);

        window.SetActive(true);
        GameObject saved = SaveGeneratedPrefabAsset(window, _fleetWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<FleetWindowView>();
    }

    /// <summary>
    /// Authors the Fleet window's reusable list-row template.
    /// </summary>
    /// <param name="parent">The fleet-list content transform.</param>
    /// <returns>The configured fleet-list row template.</returns>
    private static FleetListRowView CreateFleetListRowTemplate(Transform parent)
    {
        GameObject row = new GameObject("FleetListRowTemplate", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        row.SetActive(false);
        FleetListRowView view = EnableRuntimeComponent(row.AddComponent<FleetListRowView>());
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
            0,
            _fleetListSelectionWidth,
            _fleetListSelectionHeight
        );
        RawImage icon = CreateRawImage(
            "IconImage",
            row.transform,
            _fleetIconPreviewPath,
            5,
            5,
            _fleetListIconWidth,
            _fleetListIconHeight
        );
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

    /// <summary>
    /// Authors the Fleet detail panel's reusable unit-card template.
    /// </summary>
    /// <param name="parent">The detail-card container transform.</param>
    /// <returns>The configured unit-card template.</returns>
    private static StrategyUnitCardView CreateFleetDetailUnitCardTemplate(Transform parent)
    {
        GameObject item = new GameObject(
            "FleetDetailItemTemplate",
            typeof(RectTransform),
            typeof(UIPointerGestureRelay),
            typeof(StrategyUnitCardView)
        );
        item.transform.SetParent(parent, false);
        StrategyUnitCardView view = EnableRuntimeComponent(
            item.GetComponent<StrategyUnitCardView>()
        );
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
        RawImage entity = CreateRawImage(
            "EntityImage",
            item.transform,
            _facilityCardEntityPreviewPath,
            25,
            5
        );
        SetSourceRect(entity.rectTransform, 25, 5, 61, 25);
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
            0,
            _fleetDetailSelectionWidth,
            _fleetDetailSelectionHeight
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

    /// <summary>
    /// Authors the Missions window prefab.
    /// </summary>
    /// <returns>The generated Missions window view.</returns>
    private static MissionsWindowView BuildMissionsWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_missionsWindowPrefabPath));
        MissionParticipantRowView participantRowPrefab = BuildMissionsParticipantRowPrefab();
        const int windowWidth = 235;
        const int windowHeight = 304;

        GameObject window = new GameObject(
            "MissionsWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(MissionsWindowView)
        );
        MissionsWindowView view = EnableRuntimeComponent(window.GetComponent<MissionsWindowView>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _missionsWindowBackgroundPreviewPath,
            0,
            0,
            windowWidth,
            windowHeight
        );
        background.raycastTarget = true;

        RawImage title = CreateWindowTitleImage(window.GetComponent<UIWindow>(), windowWidth);
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
            CreateRawImage(
                "OpenSectorButtonImage",
                buttons,
                _windowOpenSectorPreviewPath,
                3,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "MinimizeButtonImage",
                buttons,
                _windowMinimizePreviewPath,
                windowWidth - 31,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
            CreateRawImage(
                "CloseButtonImage",
                buttons,
                _windowClosePreviewPath,
                windowWidth - 17,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
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
        for (int i = 0; i < MissionsWindowRenderData.TabCount; i++)
        {
            RawImage tab = CreateRawButton(
                $"{MissionsWindowRenderData.OrderedRoles[i]}TabButtonImage",
                tabs,
                _missionsTabPreviewPath
            );
            SetSourceRect(tab.rectTransform, 105 + i * 61, 127, 61, 16);
            tabImages.Add(tab);
        }
        List<Button> tabButtons = CreateButtons(tabImages);
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < tabImages.Count; index++)
            tabPressVisuals.Add(tabImages[index].GetComponent<RawImagePressVisual>());

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
        MissionParticipantRowView participantTemplate = InstantiateMissionParticipantRowTemplate(
            participantRowPrefab,
            participantsContent,
            "ParticipantRowTemplate"
        );

        AssignReference(view, "titleImage", title);
        AssignReference(view, "captionTextField", caption);
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
        AssignReferenceArray(view, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReference(view, "participantsScrollArea", participantsScrollArea);
        AssignReference(view, "participantRowTemplate", participantTemplate);

        GameObject saved = SaveGeneratedPrefabAsset(window, _missionsWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<MissionsWindowView>();
    }

    /// <summary>
    /// Authors the Mission Create window prefab.
    /// </summary>
    /// <returns>The generated Mission Create window view.</returns>
    private static MissionCreateWindowView BuildMissionCreateWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_missionCreateWindowPrefabPath));
        MissionParticipantRowView participantRowPrefab = BuildMissionCreateParticipantRowPrefab();
        const int windowWidth = 259;
        const int windowHeight = 355;

        GameObject window = new GameObject(
            "MissionCreateWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        window.SetActive(false);
        UIWindow windowShell = window.GetComponent<UIWindow>();
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, windowWidth, windowHeight);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            _missionCreateMissionBackgroundPreviewPath,
            0,
            0,
            windowWidth,
            windowHeight
        );
        const int titleWidth = 240;
        const int titleHeight = 17;
        background.raycastTarget = true;

        List<RawImage> titleImages = new List<RawImage>
        {
            CreateRawImage(
                "LeftTitleImage",
                window.transform,
                PreviewTheme?.StrategyWindows?.MissionCreate?.TitleImagePath,
                2,
                2
            ),
            CreateRawImage(
                "RightTitleImage",
                window.transform,
                PreviewTheme?.StrategyWindows?.MissionCreate?.TitleImagePath,
                windowWidth - titleWidth - 2,
                2
            ),
        };
        for (int i = 0; i < titleImages.Count; i++)
        {
            titleImages[i].raycastTarget = true;
            ConfigureWindowDragHandle(titleImages[i].gameObject, windowShell);
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
            CreateRawImage(
                "CloseButtonImage",
                buttons,
                _windowClosePreviewPath,
                windowWidth - 17,
                3,
                _windowChromeButtonWidth,
                _windowChromeButtonHeight
            ),
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
            "MissionTabButtonImage",
            tabs,
            PreviewTheme?.StrategyWindows?.MissionCreate?.MissionTab?.InactiveImagePath
        );
        SetSourceRect(firstTab.rectTransform, 8, 20, 116, 33);
        Button firstTabButton = CreateButton(firstTab);
        tabImages.Add(firstTab);
        RawImage secondTab = CreateRawButton(
            "PersonnelTabButtonImage",
            tabs,
            PreviewTheme?.StrategyWindows?.MissionCreate?.PersonnelTab?.InactiveImagePath
        );
        SetSourceRect(secondTab.rectTransform, 136, 20, 116, 33);
        Button secondTabButton = CreateButton(secondTab);
        tabImages.Add(secondTab);
        List<Button> tabButtons = new List<Button> { firstTabButton, secondTabButton };
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < tabImages.Count; index++)
            tabPressVisuals.Add(tabImages[index].GetComponent<RawImagePressVisual>());

        RawImage infoButton = CreateRawImage(
            "InfoButtonImage",
            window.transform,
            _constructionInfoButtonPreviewPath,
            33,
            320,
            _constructionActionButtonWidth,
            _constructionActionButtonHeight
        );
        Button infoButtonComponent = CreateButton(infoButton);
        infoButton
            .GetComponent<RawImagePressVisual>()
            .SetTextures(
                LoadTexture(_constructionInfoButtonPreviewPath),
                LoadTexture(_constructionInfoButtonDownPreviewPath)
            );
        RawImage okButton = CreateRawImage(
            "OkButtonImage",
            window.transform,
            _constructionOkButtonPreviewPath,
            102,
            320,
            _constructionActionButtonWidth,
            _constructionActionButtonHeight
        );
        Button okButtonComponent = CreateButton(okButton);
        okButton
            .GetComponent<RawImagePressVisual>()
            .SetTextures(
                LoadTexture(_constructionOkButtonPreviewPath),
                LoadTexture(_constructionOkButtonDownPreviewPath)
            );
        RawImage cancelButton = CreateRawImage(
            "CancelButtonImage",
            window.transform,
            _constructionCancelButtonPreviewPath,
            170,
            320,
            _constructionActionButtonWidth,
            _constructionActionButtonHeight
        );
        Button cancelButtonComponent = CreateButton(cancelButton);
        cancelButton
            .GetComponent<RawImagePressVisual>()
            .SetTextures(
                LoadTexture(_constructionCancelButtonPreviewPath),
                LoadTexture(_constructionCancelButtonDownPreviewPath)
            );

        RectTransform missionSelection = CreateChildLayer("MissionSelection", window.transform);
        SetSourceRect(missionSelection, 0, 0, 259, 355);
        RawImage dropdownButton = CreateRawImage(
            "DropdownButtonImage",
            missionSelection,
            _constructionOpenButtonPreviewPath,
            101,
            174,
            _constructionDropdownButtonWidth,
            _constructionDropdownButtonHeight
        );
        Button dropdownButtonComponent = CreateButton(dropdownButton);
        RawImagePressVisual dropdownButtonPressVisual =
            dropdownButton.GetComponent<RawImagePressVisual>();
        dropdownButtonPressVisual.SetTextures(
            LoadTexture(_constructionOpenButtonPreviewPath),
            LoadTexture(_constructionOpenButtonDownPreviewPath)
        );
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

        string[] dropdownBackgroundNames =
        {
            "TopDropdownBackgroundImage",
            "BottomDropdownBackgroundImage",
        };
        for (int i = 0; i < 2; i++)
        {
            RawImage dropdownBackground = CreateRawImage(
                dropdownBackgroundNames[i],
                dropdown,
                _constructionDropdownBackgroundPreviewPath,
                0,
                i * 61
            );
            SetSourceRect(dropdownBackground.rectTransform, 0, i * 61, 197, i == 0 ? 61 : 53);
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
        RectTransform personnel = CreateChildLayer("Personnel", window.transform);
        SetSourceRect(personnel, 0, 0, 259, 355);
        RawImage agentsHeader = CreateRawImage(
            "AgentsHeaderImage",
            personnel,
            PreviewTheme?.StrategyWindows?.MissionCreate?.AgentsHeaderImagePath,
            8,
            65,
            _missionCreateHeaderWidth,
            _missionCreateHeaderHeight
        );
        RawImage decoysHeader = CreateRawImage(
            "DecoysHeaderImage",
            personnel,
            PreviewTheme?.StrategyWindows?.MissionCreate?.DecoysHeaderImagePath,
            136,
            65,
            _missionCreateHeaderWidth,
            _missionCreateHeaderHeight
        );
        RawImage moveRight = CreateRawImage(
            "MoveRightButtonImage",
            personnel,
            _missionCreateMoveRightButtonPreviewPath,
            120,
            136,
            _missionCreateMoveButtonWidth,
            _missionCreateMoveButtonHeight
        );
        Button moveRightButton = CreateButton(moveRight);
        moveRight
            .GetComponent<RawImagePressVisual>()
            .SetTextures(
                LoadTexture(_missionCreateMoveRightButtonPreviewPath),
                LoadTexture(_missionCreateMoveRightButtonDownPreviewPath)
            );
        RawImage moveLeft = CreateRawImage(
            "MoveLeftButtonImage",
            personnel,
            _missionCreateMoveLeftButtonPreviewPath,
            120,
            221,
            _missionCreateMoveButtonWidth,
            _missionCreateMoveButtonHeight
        );
        Button moveLeftButton = CreateButton(moveLeft);
        moveLeft
            .GetComponent<RawImagePressVisual>()
            .SetTextures(
                LoadTexture(_missionCreateMoveLeftButtonPreviewPath),
                LoadTexture(_missionCreateMoveLeftButtonDownPreviewPath)
            );

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
        StrategyDropdownItemView dropdownItemRowTemplate = CreateStrategyDropdownItemTemplate(
            missionCreateLayoutTemplates,
            "DropdownItemRowTemplate",
            new RectInt(0, 0, 197, 110),
            new RectInt(2, 25, 197, 85),
            new RectInt(3, 3, 190, 16),
            _facilityCardEntityPreviewPath,
            "Espionage",
            13,
            TextAlignmentOptions.Top
        );
        RectTransform dropdownContentPaddingTemplate = CreateChildLayer(
            "DropdownContentPaddingTemplate",
            missionCreateLayoutTemplates
        );
        SetSourceRect(dropdownContentPaddingTemplate, 0, 0, 197, 5);
        MissionParticipantRowView agentTemplate = InstantiateMissionParticipantRowTemplate(
            participantRowPrefab,
            agentsContent,
            "AgentRowTemplate"
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
        MissionParticipantRowView decoyTemplate = InstantiateMissionParticipantRowTemplate(
            participantRowPrefab,
            decoysContent,
            "DecoyRowTemplate"
        );

        MissionCreateWindowView view = EnableRuntimeComponent(
            window.AddComponent<MissionCreateWindowView>()
        );
        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "titleTextField", title);
        AssignReferenceArray(view, "titleImages", titleImages);
        AssignReferenceArray(view, "tabImages", tabImages);
        AssignReferenceArray(view, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReferenceArray(view, "tabActiveTextures", tabActiveTextures);
        AssignReferenceArray(view, "tabInactiveTextures", tabInactiveTextures);
        AssignReference(view, "infoButton", infoButtonComponent);
        AssignReference(view, "okButton", okButtonComponent);
        AssignReference(view, "cancelButton", cancelButtonComponent);
        AssignReference(view, "missionSelectionRoot", missionSelection);
        AssignReference(view, "dropdownButtonImage", dropdownButton);
        AssignReference(view, "dropdownButtonPressVisual", dropdownButtonPressVisual);
        AssignReference(view, "dropdownButton", dropdownButtonComponent);
        AssignReference(view, "selectedMissionImage", selectedMission);
        AssignReference(view, "selectedMissionNameTextField", selectedMissionName);
        AssignReference(view, "targetPreviewImage", targetPreview);
        AssignReference(view, "targetPreviewNameTextField", targetPreviewName);
        AssignReference(view, "dropdownRoot", dropdown);
        AssignReference(view, "dropdownScrollArea", dropdownScrollArea);
        AssignReference(view, "dropdownItemRowTemplate", dropdownItemRowTemplate);
        AssignReference(view, "dropdownContentPaddingTemplate", dropdownContentPaddingTemplate);
        AssignReference(view, "personnelRoot", personnel);
        AssignReference(view, "agentsHeaderImage", agentsHeader);
        AssignReference(view, "decoysHeaderImage", decoysHeader);
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

    /// <summary>
    /// Authors the Missions window's reusable list-row template.
    /// </summary>
    /// <param name="parent">The mission-list content transform.</param>
    /// <returns>The configured mission-list row template.</returns>
    private static MissionListRowView CreateMissionListRowTemplate(Transform parent)
    {
        GameObject row = new GameObject(
            "MissionListRowTemplate",
            typeof(RectTransform),
            typeof(UIPointerGestureRelay),
            typeof(MissionListRowView)
        );
        row.transform.SetParent(parent, false);
        MissionListRowView view = EnableRuntimeComponent(row.GetComponent<MissionListRowView>());
        UIPointerGestureRelay pointerGestures = EnableRuntimeComponent(
            row.GetComponent<UIPointerGestureRelay>()
        );
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
            0,
            _missionListSelectionWidth,
            _missionListSelectionHeight
        );

        AssignReference(view, "hitAreaImage", hitArea);
        AssignReference(view, "pointerGestures", pointerGestures);
        AssignReference(view, "iconImage", icon);
        AssignReference(view, "nameTextField", nameText);
        AssignReference(view, "selectionImage", selection);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors a reusable dropdown item with fixed image and text slots.
    /// </summary>
    /// <param name="parent">The dropdown content transform.</param>
    /// <param name="name">The template object name.</param>
    /// <param name="rowRect">The row source-space rectangle.</param>
    /// <param name="imageRect">The image source-space rectangle.</param>
    /// <param name="textRect">The text source-space rectangle.</param>
    /// <param name="previewImagePath">The preview image path.</param>
    /// <param name="previewText">The preview label.</param>
    /// <param name="fontSize">The preview font size.</param>
    /// <param name="alignment">The preview text alignment.</param>
    /// <returns>The configured dropdown item template.</returns>
    private static StrategyDropdownItemView CreateStrategyDropdownItemTemplate(
        Transform parent,
        string name,
        RectInt rowRect,
        RectInt imageRect,
        RectInt textRect,
        string previewImagePath,
        string previewText,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(StrategyDropdownItemView)
        );
        row.transform.SetParent(parent, false);
        SetSourceRect(
            row.GetComponent<RectTransform>(),
            rowRect.x,
            rowRect.y,
            rowRect.width,
            rowRect.height
        );

        Image hitArea = row.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        Button button = row.GetComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;

        RawImage itemImage = CreateRawImage(
            "ItemImage",
            row.transform,
            previewImagePath,
            imageRect.x,
            imageRect.y
        );
        SetSourceRect(
            itemImage.rectTransform,
            imageRect.x,
            imageRect.y,
            imageRect.width,
            imageRect.height
        );
        TextMeshProUGUI itemText = CreateTextLabel("ItemTextField", row.transform);
        itemText.text = previewText;
        itemText.color = Color.white;
        itemText.fontSize = fontSize;
        itemText.alignment = alignment;
        SetSourceRect(
            itemText.rectTransform,
            textRect.x,
            textRect.y,
            textRect.width,
            textRect.height
        );

        StrategyDropdownItemView view = EnableRuntimeComponent(
            row.GetComponent<StrategyDropdownItemView>()
        );
        AssignReference(view, "button", button);
        AssignReference(view, "itemImage", itemImage);
        AssignReference(view, "itemTextField", itemText);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors the Missions participant-row prefab variant.
    /// </summary>
    /// <returns>The generated participant-row prefab view.</returns>
    private static MissionParticipantRowView BuildMissionsParticipantRowPrefab()
    {
        MissionParticipantRowView view = CreateMissionParticipantRowTemplate(
            "MissionsParticipantRow",
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
        GameObject saved = SaveGeneratedPrefabAsset(
            view.gameObject,
            _missionsParticipantRowPrefabPath
        );
        Object.DestroyImmediate(view.gameObject);
        return saved.GetComponent<MissionParticipantRowView>();
    }

    /// <summary>
    /// Authors the Mission Create participant-row prefab variant.
    /// </summary>
    /// <returns>The generated participant-row prefab view.</returns>
    private static MissionParticipantRowView BuildMissionCreateParticipantRowPrefab()
    {
        MissionParticipantRowView view = CreateMissionParticipantRowTemplate(
            "MissionCreateParticipantRow",
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
        GameObject saved = SaveGeneratedPrefabAsset(
            view.gameObject,
            _missionCreateParticipantRowPrefabPath
        );
        Object.DestroyImmediate(view.gameObject);
        return saved.GetComponent<MissionParticipantRowView>();
    }

    /// <summary>
    /// Instantiates an authored mission-participant row prefab as a nested list template.
    /// </summary>
    /// <param name="prefab">The authored participant-row prefab.</param>
    /// <param name="parent">The participant-list transform.</param>
    /// <param name="name">The nested template object name.</param>
    /// <returns>The nested participant-row template.</returns>
    private static MissionParticipantRowView InstantiateMissionParticipantRowTemplate(
        MissionParticipantRowView prefab,
        Transform parent,
        string name
    )
    {
        GameObject instance = (GameObject)
            PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
        instance.name = name;
        MissionParticipantRowView view = instance.GetComponent<MissionParticipantRowView>();
        if (view == null)
            throw new MissingReferenceException(
                $"{prefab.name} is missing {nameof(MissionParticipantRowView)}."
            );

        view.enabled = true;
        return view;
    }

    /// <summary>
    /// Authors one mission-participant row prefab variant.
    /// </summary>
    /// <param name="name">The prefab root name.</param>
    /// <param name="width">The row width.</param>
    /// <param name="height">The row height.</param>
    /// <param name="rowY">The row source-space y-coordinate.</param>
    /// <param name="entityX">The entity image x-coordinate.</param>
    /// <param name="entityY">The entity image y-coordinate.</param>
    /// <param name="entityWidth">The entity image width.</param>
    /// <param name="entityHeight">The entity image height.</param>
    /// <param name="nameX">The name label x-coordinate.</param>
    /// <param name="nameY">The name label y-coordinate.</param>
    /// <param name="nameWidth">The name label width.</param>
    /// <param name="nameHeight">The name label height.</param>
    /// <returns>The configured participant-row template.</returns>
    private static MissionParticipantRowView CreateMissionParticipantRowTemplate(
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
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(UIPointerGestureRelay),
            typeof(MissionParticipantRowView)
        );
        UIPointerGestureRelay pointerGestures = EnableRuntimeComponent(
            row.GetComponent<UIPointerGestureRelay>()
        );
        MissionParticipantRowView view = EnableRuntimeComponent(
            row.GetComponent<MissionParticipantRowView>()
        );
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
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(nameText.rectTransform, nameX, nameY, nameWidth, nameHeight);

        AssignReference(view, "pointerGestures", pointerGestures);
        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "entityImage", entity);
        AssignReference(view, "nameTextField", nameText);
        AssignReference(
            view,
            "backgroundTexture",
            LoadTexture(_defensePersonnelBackgroundPreviewPath)
        );
        AssignReference(
            view,
            "inTransitBackgroundTexture",
            LoadTexture(_fleetPersonnelEnrouteBackgroundPreviewPath)
        );
        view.enabled = true;
        return view;
    }

    /// <summary>
    /// Updates the Strategy root's window registry inside an isolated prefab editing scene.
    /// </summary>
    private static void RegisterWindowPrefabsInRoot()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath) == null)
            throw new System.InvalidOperationException("Strategy view root prefab was not found.");

        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);
        try
        {
            StrategyWindowLayerView windowsView =
                root.GetComponentInChildren<StrategyWindowLayerView>(true);
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
                LoadWindowPrefab<AdvisorReportWindowView>(_advisorReportWindowPrefabPath),
                LoadWindowPrefab<MessagesWindowView>(_messagesWindowPrefabPath),
                LoadWindowPrefab<ConfirmDialogWindowView>(_confirmDialogWindowPrefabPath),
                LoadWindowPrefab<BattleAlertWindowView>(_battleAlertWindowPrefabPath),
                LoadWindowPrefab<FinderWindowView>(_finderWindowPrefabPath),
                LoadWindowPrefab<EncyclopediaWindowView>(_encyclopediaWindowPrefabPath)
            );

            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath, out bool success);
            if (!success)
                throw new System.InvalidOperationException(
                    "Strategy view root prefab window registration could not be saved."
                );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Loads a required generated window component from its prefab asset.
    /// </summary>
    /// <typeparam name="T">The window component type.</typeparam>
    /// <param name="path">The prefab asset path.</param>
    /// <returns>The required window component.</returns>
    private static T LoadWindowPrefab<T>(string path)
        where T : MonoBehaviour
    {
        return LoadPrefabComponent<T>(path);
    }

    /// <summary>
    /// Loads a required root component from a prefab asset.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="path">The prefab asset path.</param>
    /// <returns>The required root component.</returns>
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

    /// <summary>
    /// Instantiates a required nested prefab component below an authored parent.
    /// </summary>
    /// <typeparam name="T">The required root component type.</typeparam>
    /// <param name="path">The nested prefab asset path.</param>
    /// <param name="parent">The generated parent transform.</param>
    /// <returns>The required component on the nested prefab instance.</returns>
    private static T InstantiatePrefabComponent<T>(string path, Transform parent)
        where T : MonoBehaviour
    {
        T prefabComponent = LoadPrefabComponent<T>(path);
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

    /// <summary>
    /// Finds one required direct child component in a generated or nested prefab hierarchy.
    /// </summary>
    /// <typeparam name="T">The required child component type.</typeparam>
    /// <param name="parent">The direct parent transform.</param>
    /// <param name="childName">The required child name.</param>
    /// <returns>The matching child component.</returns>
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

    /// <summary>
    /// Applies the explicit enabled state required for an authored runtime component.
    /// </summary>
    /// <typeparam name="T">The runtime MonoBehaviour type.</typeparam>
    /// <param name="component">The authored component.</param>
    /// <returns>The enabled component.</returns>
    private static T EnableRuntimeComponent<T>(T component)
        where T : MonoBehaviour
    {
        if (component == null)
            throw new System.ArgumentNullException(nameof(component));

        component.enabled = true;
        return component;
    }

    /// <summary>
    /// Authors the Status window prefab.
    /// </summary>
    /// <returns>The generated Status window view.</returns>
    private static StatusWindowView BuildStatusWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statusWindowPrefabPath));

        GameObject window = new GameObject(
            "StatusWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(StatusWindowView)
        );
        StatusWindowView view = EnableRuntimeComponent(window.GetComponent<StatusWindowView>());
        ConfigureWindowRoot(window.GetComponent<UIWindow>());
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
            258,
            218
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
        AssignReference(
            view,
            "infoButtonPressVisual",
            infoButton.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "closeButtonImage", closeButton);
        AssignReference(
            view,
            "closeButtonPressVisual",
            closeButton.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "infoButton", infoButtonComponent);
        AssignReference(view, "closeButton", closeButtonComponent);
        AssignReference(view, "infoButtonUpTexture", LoadTexture(_statusInfoButtonPreviewPath));
        AssignReference(
            view,
            "infoButtonDownTexture",
            LoadTexture(_statusInfoButtonDownPreviewPath)
        );
        AssignReference(
            view,
            "infoButtonDisabledTexture",
            LoadTexture(_statusInfoButtonDisabledPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_statusCloseButtonPreviewPath));
        AssignReference(
            view,
            "closeButtonDownTexture",
            LoadTexture(_statusCloseButtonDownPreviewPath)
        );

        GameObject saved = SaveGeneratedPrefabAsset(window, _statusWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<StatusWindowView>();
    }

    /// <summary>
    /// Authors the advisor-report window prefab.
    /// </summary>
    /// <returns>The generated advisor-report window view.</returns>
    private static AdvisorReportWindowView BuildAdvisorReportWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_advisorReportWindowPrefabPath));

        GameObject window = new GameObject(
            "AdvisorReportWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(AdvisorReportWindowView)
        );
        AdvisorReportWindowView view = EnableRuntimeComponent(
            window.GetComponent<AdvisorReportWindowView>()
        );
        ConfigureWindowRoot(window.GetComponent<UIWindow>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 379, 272);

        RawImage background = CreateRawImage(
            "BackgroundImage",
            window.transform,
            PreviewTheme?.StrategyWindows?.AdvisorReport?.BackgroundImagePath,
            0,
            0
        );
        background.raycastTarget = true;
        RawImage galaxy = CreateRawImage(
            "GalaxyImage",
            window.transform,
            _advisorReportGalaxyPreviewPath,
            244,
            20
        );

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", window.transform);
        title.text = "Galaxy Overview";
        title.color = Color.white;
        title.fontSize = 14;
        title.alignment = TextAlignmentOptions.Top;
        SetSourceRect(title.rectTransform, 242, 140, 126, 34);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            window.transform,
            "RowsScrollArea",
            15,
            40,
            211,
            210,
            0,
            0,
            198,
            210,
            198,
            7,
            13,
            203,
            out RectTransform rowsContent
        );
        ConfigureVerticalContent(rowsContent);
        AdvisorReportRowView overviewRowTemplate = CreateAdvisorReportRowTemplate(
            rowsContent,
            "OverviewRowTemplate",
            0,
            64,
            80,
            30,
            110,
            80
        );
        AdvisorReportRowView objectiveRowTemplate = CreateAdvisorReportRowTemplate(
            rowsContent,
            "ObjectiveRowTemplate",
            0,
            50,
            50,
            150,
            0,
            0
        );
        RectTransform rowsPaddingTemplate = CreateSourceRectLayer(
            "RowsPaddingTemplate",
            rowsContent,
            198,
            5
        );
        rowsPaddingTemplate.gameObject.SetActive(false);

        RawImage infoButton = CreateRawImage(
            "InfoButtonImage",
            window.transform,
            _statusInfoButtonDisabledPreviewPath,
            257,
            217
        );
        RawImage closeButton = CreateRawImage(
            "CloseButtonImage",
            window.transform,
            _statusCloseButtonPreviewPath,
            322,
            217
        );
        Button closeButtonComponent = CreateButton(closeButton);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "galaxyImage", galaxy);
        AssignReference(view, "titleTextField", title);
        AssignReference(view, "rowsScrollArea", rowsScrollArea);
        AssignReference(view, "overviewRowTemplate", overviewRowTemplate);
        AssignReference(view, "objectiveRowTemplate", objectiveRowTemplate);
        AssignReference(view, "rowsPaddingTemplate", rowsPaddingTemplate);
        AssignReference(view, "infoButtonImage", infoButton);
        AssignReference(view, "closeButtonImage", closeButton);
        AssignReference(
            view,
            "closeButtonPressVisual",
            closeButton.GetComponent<RawImagePressVisual>()
        );
        AssignReference(view, "closeButton", closeButtonComponent);
        AssignReference(
            view,
            "infoButtonDisabledTexture",
            LoadTexture(_statusInfoButtonDisabledPreviewPath)
        );
        AssignReference(view, "closeButtonUpTexture", LoadTexture(_statusCloseButtonPreviewPath));
        AssignReference(
            view,
            "closeButtonDownTexture",
            LoadTexture(_statusCloseButtonDownPreviewPath)
        );

        GameObject saved = SaveGeneratedPrefabAsset(window, _advisorReportWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<AdvisorReportWindowView>();
    }

    /// <summary>
    /// Authors a reusable advisor-report row template.
    /// </summary>
    /// <param name="parent">The report-list transform.</param>
    /// <param name="name">The template object name.</param>
    /// <param name="imageX">The image-slot x-coordinate.</param>
    /// <param name="imageWidth">The image-slot width.</param>
    /// <param name="primaryX">The primary label x-coordinate.</param>
    /// <param name="primaryWidth">The primary label width.</param>
    /// <param name="secondaryX">The secondary label x-coordinate.</param>
    /// <param name="secondaryWidth">The secondary label width.</param>
    /// <returns>The configured advisor-report row template.</returns>
    private static AdvisorReportRowView CreateAdvisorReportRowTemplate(
        Transform parent,
        string name,
        int imageX,
        int imageWidth,
        int primaryX,
        int primaryWidth,
        int secondaryX,
        int secondaryWidth
    )
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(AdvisorReportRowView));
        row.transform.SetParent(parent, false);
        SetSourceRect(row.GetComponent<RectTransform>(), 0, 0, 198, 35);
        AdvisorReportRowView view = EnableRuntimeComponent(
            row.GetComponent<AdvisorReportRowView>()
        );

        RectTransform imageSlot = CreateSourceRectLayer("ImageSlot", row.transform, imageWidth, 35);
        SetSourceRect(imageSlot, imageX, 0, imageWidth, 35);
        RawImage image = CreateRawImage("Image", row.transform, null, imageX, 0);
        SetSourceRect(image.rectTransform, imageX, 0, imageWidth, 35);

        TextMeshProUGUI primary = CreateTextLabel("PrimaryTextField", row.transform);
        primary.text = "000";
        primary.color = Color.white;
        primary.fontSize = 13;
        primary.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(primary.rectTransform, primaryX, 13, primaryWidth, 17);

        TextMeshProUGUI secondary = null;
        if (secondaryWidth > 0)
        {
            secondary = CreateTextLabel("SecondaryTextField", row.transform);
            secondary.text = "0000";
            secondary.color = Color.white;
            secondary.fontSize = 13;
            secondary.alignment = TextAlignmentOptions.TopLeft;
            SetSourceRect(secondary.rectTransform, secondaryX, 13, secondaryWidth, 17);
        }

        AssignReference(view, "imageSlot", imageSlot);
        AssignReference(view, "image", image);
        AssignReference(view, "primaryTextField", primary);
        AssignReference(view, "secondaryTextField", secondary);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors the Finder window prefab.
    /// </summary>
    /// <returns>The generated Finder window view.</returns>
    private static FinderWindowView BuildFinderWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_finderWindowPrefabPath));

        GameObject window = new GameObject(
            "FinderWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(FinderWindowView)
        );
        FinderWindowView view = EnableRuntimeComponent(window.GetComponent<FinderWindowView>());
        ConfigureWindowRoot(window.GetComponent<UIWindow>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);

        RawImage background = CreateRawButton(
            "BackgroundImage",
            window.transform,
            "Art/HD/UI/StrategyView/ui_strategyview_finder_window_system_finder_background"
        );
        SetSourceRect(background.rectTransform, 12, 13, 400, 306);

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
            "UpperLayout",
            true,
            true
        );
        List<Button> upperButtons = CreateButtons(upperButtonImages);
        List<RawImagePressVisual> upperButtonPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < upperButtonImages.Count; index++)
        {
            upperButtonPressVisuals.Add(
                upperButtonImages[index].GetComponent<RawImagePressVisual>()
            );
        }
        List<RawImage> twoButtonImages = CreateUtilityDialogButtonSlots(
            buttons,
            "TwoButtonLayout",
            false,
            false
        );
        List<Button> twoButtons = CreateButtons(twoButtonImages);
        List<RawImagePressVisual> twoButtonPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < twoButtonImages.Count; index++)
        {
            twoButtonPressVisuals.Add(twoButtonImages[index].GetComponent<RawImagePressVisual>());
        }
        List<RawImage> fourButtonImages = CreateUtilityDialogButtonSlots(
            buttons,
            "FourButtonLayout",
            false,
            true
        );
        List<Button> fourButtons = CreateButtons(fourButtonImages);
        List<RawImagePressVisual> fourButtonPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < fourButtonImages.Count; index++)
        {
            fourButtonPressVisuals.Add(fourButtonImages[index].GetComponent<RawImagePressVisual>());
        }

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
        SetSourceRect(label.rectTransform, 37, 48, 120, 16);

        TMP_InputField labelInput = CreateTextInputField(
            "LabelInputField",
            window.transform,
            string.Empty,
            141,
            45,
            249,
            17
        );

        RectTransform tabs = CreateSourceRectLayer("Tabs", window.transform, 470, 331);
        string[] finderTabPreviewPaths =
        {
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_all_systems_button_up",
            GetFactionTabPreviewTheme(0)?.StrategyWindows?.Finder?.SystemsButton?.UpImagePath,
            GetFactionTabPreviewTheme(1)?.StrategyWindows?.Finder?.SystemsButton?.UpImagePath,
            "Art/HD/UI/StrategyView/ui_strategyview_finder_window_neutral_systems_button_up",
            "Art/HD/UI/StrategyView/ui_strategyview_finder_window_unexplored_systems_button_up",
        };
        List<RawImage> tabSlots = new List<RawImage>();
        for (int i = 0; i < finderTabPreviewPaths.Length; i++)
        {
            RawImage image = CreateRawButton(
                $"TabSlot{i}ButtonImage",
                tabs,
                finderTabPreviewPaths[i]
            );
            SetSourceRect(image.rectTransform, 36 + i * 52, 78, 49, 41);
            tabSlots.Add(image);
        }
        List<Button> tabButtons = CreateButtons(tabSlots);
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < tabSlots.Count; index++)
            tabPressVisuals.Add(tabSlots[index].GetComponent<RawImagePressVisual>());

        RectTransform layoutTemplates = CreateChildLayer("LayoutTemplates", window.transform);
        layoutTemplates.gameObject.SetActive(false);
        List<RectTransform> defaultTabSlotTemplates = CreateFinderTabSlotTemplates(
            layoutTemplates,
            "Default",
            78
        );
        List<RectTransform> compactTabSlotTemplates = CreateFinderTabSlotTemplates(
            layoutTemplates,
            "Compact",
            72
        );

        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", window.transform);
        tabTitle.text = "All Systems";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 12;
        tabTitle.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(tabTitle.rectTransform, 40, 120, 250, 16);

        TextMeshProUGUI defaultTabTitle = CreateTextLabel(
            "DefaultTabTitleTextTemplate",
            layoutTemplates
        );
        defaultTabTitle.text = "All Systems";
        defaultTabTitle.color = Color.white;
        defaultTabTitle.fontSize = 12;
        defaultTabTitle.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(defaultTabTitle.rectTransform, 40, 120, 250, 16);
        defaultTabTitle.gameObject.SetActive(false);

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
        RectTransform personnelScrollbarTemplate = CreateChildLayer(
            "PersonnelScrollbarTemplate",
            layoutTemplates
        );
        SetSourceRect(personnelScrollbarTemplate, 373, 132, 13, 172);
        RectTransform personnelPanelScrollbarTemplate = CreateChildLayer(
            "PersonnelPanelScrollbarTemplate",
            layoutTemplates
        );
        SetSourceRect(personnelPanelScrollbarTemplate, 373, 142, 13, 162);

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "overlayFrameImage", overlay);
        AssignReference(view, "buttonStripImage", strip);
        AssignReferenceArray(view, "upperButtonImages", upperButtonImages);
        AssignReferenceArray(view, "upperButtonPressVisuals", upperButtonPressVisuals);
        AssignReferenceArray(view, "upperButtons", upperButtons);
        AssignReferenceArray(view, "twoButtonImages", twoButtonImages);
        AssignReferenceArray(view, "twoButtonPressVisuals", twoButtonPressVisuals);
        AssignReferenceArray(view, "twoButtons", twoButtons);
        AssignReferenceArray(view, "fourButtonImages", fourButtonImages);
        AssignReferenceArray(view, "fourButtonPressVisuals", fourButtonPressVisuals);
        AssignReferenceArray(view, "fourButtons", fourButtons);
        AssignReference(view, "titleTextField", title);
        AssignReference(view, "labelTextField", label);
        AssignReference(view, "labelInputField", labelInput);
        AssignReferenceArray(view, "tabImageSlots", tabSlots);
        AssignReferenceArray(view, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(view, "tabButtons", tabButtons);
        AssignReferenceArray(view, "defaultTabSlotTemplates", defaultTabSlotTemplates);
        AssignReferenceArray(view, "compactTabSlotTemplates", compactTabSlotTemplates);
        AssignReference(view, "tabTitleTextField", tabTitle);
        AssignReference(view, "defaultTabTitleTextTemplate", defaultTabTitle);
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
        AssignInt(view, "troopRowPitch", 25);
        AssignReference(view, "defaultScrollbarTemplate", defaultScrollbarTemplate);
        AssignReference(view, "compactScrollbarTemplate", compactScrollbarTemplate);
        AssignReference(view, "personnelScrollbarTemplate", personnelScrollbarTemplate);
        AssignReference(view, "personnelPanelScrollbarTemplate", personnelPanelScrollbarTemplate);
        buttons.SetAsLastSibling();
        GameObject saved = SaveGeneratedPrefabAsset(window, _finderWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<FinderWindowView>();
    }

    /// <summary>
    /// Authors the fixed Finder faction-tab slots.
    /// </summary>
    /// <param name="parent">The Finder window transform.</param>
    /// <param name="layoutName">The tab layout name.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <returns>The authored tab slots.</returns>
    private static List<RectTransform> CreateFinderTabSlotTemplates(
        Transform parent,
        string layoutName,
        int y
    )
    {
        List<RectTransform> slots = new List<RectTransform>();
        for (int i = 0; i < 2; i++)
        {
            RectTransform slot = CreateChildLayer($"{layoutName}TabSlot{i}Template", parent);
            SetSourceRect(slot, 36 + i * 52, y, 49, 41);
            slots.Add(slot);
        }

        return slots;
    }

    /// <summary>
    /// Authors a reusable Finder result-row template.
    /// </summary>
    /// <param name="parent">The Finder list transform.</param>
    /// <param name="name">The template object name.</param>
    /// <param name="previewName">The preview result name.</param>
    /// <param name="nameFontSize">The result-name font size.</param>
    /// <param name="nameY">The result-name y-coordinate.</param>
    /// <param name="nameHeight">The result-name height.</param>
    /// <param name="firstCountX">The first count-column x-coordinate.</param>
    /// <returns>The configured Finder row template.</returns>
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
        FinderWindowRowView row = EnableRuntimeComponent(
            hitArea.gameObject.AddComponent<FinderWindowRowView>()
        );

        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", hitArea.transform);
        nameText.text = previewName;
        nameText.color = Color.gray;
        nameText.fontSize = nameFontSize;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(nameText.rectTransform, 5, nameY, 320, nameHeight);

        List<TextMeshProUGUI> countTextFields = new List<TextMeshProUGUI>();
        for (int i = 0; i < 5; i++)
        {
            TextMeshProUGUI countText = CreateTextLabel(
                $"CountColumnSlot{i}TextField",
                hitArea.transform
            );
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
        AssignReference(row, "layoutElement", row.GetComponent<LayoutElement>());
        row.gameObject.SetActive(false);
        return row;
    }

    /// <summary>
    /// Authors the Messages window prefab, including its final static draw order.
    /// </summary>
    /// <returns>The generated Messages window view.</returns>
    private static MessagesWindowView BuildMessagesWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_messagesWindowPrefabPath));

        GameObject window = new GameObject(
            "MessagesWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(MessagesWindowView)
        );
        MessagesWindowView view = EnableRuntimeComponent(window.GetComponent<MessagesWindowView>());
        UIWindow windowShell = window.GetComponent<UIWindow>();
        ConfigureWindowRoot(windowShell);
        MessagesWindowTheme messagesTheme = PreviewTheme?.StrategyWindows?.Messages;
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);

        RectTransform indexPanelTransform = CreateSourceRectLayer(
            "IndexPanel",
            window.transform,
            470,
            331
        );
        MessagesIndexPanelView indexPanel = EnableRuntimeComponent(
            indexPanelTransform.gameObject.AddComponent<MessagesIndexPanelView>()
        );

        RawImage background = CreateRawButton(
            "BackgroundImage",
            indexPanelTransform,
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_background"
        );
        SetSourceRect(background.rectTransform, 12, 13, 400, 306);
        background.raycastTarget = true;

        RawImage overlay = CreateRawButton(
            "OverlayFrameImage",
            window.transform,
            messagesTheme?.OverlayFrameImagePath
        );
        SetSourceRect(overlay.rectTransform, 0, 0, 470, 331);

        RectTransform commandBarTransform = CreateSourceRectLayer(
            "CommandBar",
            window.transform,
            470,
            331
        );
        MessagesCommandBarView commandBar = EnableRuntimeComponent(
            commandBarTransform.gameObject.AddComponent<MessagesCommandBarView>()
        );
        RawImage buttonStrip = CreateRawButton(
            "ButtonStripImage",
            commandBarTransform,
            messagesTheme?.ButtonStripImagePath
        );
        SetSourceRect(buttonStrip.rectTransform, 412, 0, 58, 330);
        RawImage closeButtonImage = CreateRawButton(
            "CloseButtonImage",
            commandBarTransform,
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
            commandBarTransform,
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
            commandBarTransform,
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
            commandBarTransform,
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
            commandBarTransform,
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
            commandBarTransform,
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

        RectTransform tabs = CreateSourceRectLayer("Tabs", indexPanelTransform, 470, 331);
        string[] messageTabPreviewPaths =
        {
            "Art/HD/UI/StrategyView/ui_strategyview_messages_window_all_button_up",
            PreviewTheme?.StrategyWindows?.Messages?.SupportButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Messages?.FleetButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Messages?.MissionsButton?.UpImagePath,
            "Art/HD/UI/StrategyView/ui_strategyview_messages_window_resource_button_up",
            "Art/HD/UI/StrategyView/ui_strategyview_messages_window_manufacturing_button_up",
            "Art/HD/UI/StrategyView/ui_strategyview_messages_window_defense_button_up",
            "Art/HD/UI/StrategyView/ui_strategyview_messages_window_conflict_button_up",
            "Art/HD/UI/StrategyView/ui_strategyview_messages_window_chat_button_up",
            PreviewTheme?.StrategyWindows?.Messages?.AdviceButton?.UpImagePath,
        };
        int[] messageTabSourceX = { 0, 38, 76, 114, 151, 189, 227, 263, 301, 338 };
        int[] messageTabSourceWidth = { 36, 36, 36, 36, 36, 36, 36, 36, 36, 37 };
        List<RawImage> tabSlots = new List<RawImage>();
        for (int i = 0; i < messageTabPreviewPaths.Length; i++)
        {
            RawImage image = CreateRawButton(
                $"{(MessagesTab)i}TabButtonImage",
                tabs,
                messageTabPreviewPaths[i]
            );
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
        List<RawImagePressVisual> tabPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < tabSlots.Count; index++)
            tabPressVisuals.Add(tabSlots[index].GetComponent<RawImagePressVisual>());

        TextMeshProUGUI tabTitle = CreateTextLabel("TitleTextField", indexPanelTransform);
        tabTitle.text = "All Messages";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 14;
        tabTitle.alignment = TextAlignmentOptions.BottomLeft;
        SetSourceRect(tabTitle.rectTransform, 35, 88, 240, 18);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            indexPanelTransform,
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
        MessageWindowRowView rowTemplate = EnableRuntimeComponent(
            rowHitArea.gameObject.AddComponent<MessageWindowRowView>()
        );
        RawImage rowSelection = CreateRawButton("SelectionImage", rowHitArea.transform);
        SetSourceRect(rowSelection.rectTransform, 0, 0, 356, 21);
        RawImage rowIcon = CreateRawButton("IconImage", rowHitArea.transform);
        SetSourceRect(rowIcon.rectTransform, 1, 1, 15, 16);
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

        RawImage selectAll = CreateRawButton("SelectAllButtonImage", indexPanelTransform);
        SetSourceRect(selectAll.rectTransform, 282, 87, 56, 20);
        Button selectAllButton = CreateButton(selectAll);
        RawImage removeSelected = CreateRawButton("RemoveSelectedButtonImage", indexPanelTransform);
        SetSourceRect(removeSelected.rectTransform, 340, 87, 56, 20);
        Button removeSelectedButton = CreateButton(removeSelected);

        RectTransform detailPanelTransform = CreateSourceRectLayer(
            "DetailPanel",
            window.transform,
            470,
            331
        );
        MessagesDetailPanelView detailPanel = EnableRuntimeComponent(
            detailPanelTransform.gameObject.AddComponent<MessagesDetailPanelView>()
        );
        RawImage detailStrip = CreateRawButton("DetailStripImage", detailPanelTransform);
        SetSourceRect(detailStrip.rectTransform, 12, 14, 400, 18);
        RawImage detailCard = CreateRawButton("DetailCardImage", detailPanelTransform);
        SetSourceRect(detailCard.rectTransform, 12, 33, 400, 200);
        RawImage detailOverlay = CreateRawButton("DetailOverlayImage", detailPanelTransform);
        SetSourceRect(detailOverlay.rectTransform, 12, 33, 400, 200);
        detailOverlay.raycastTarget = false;
        RawImage detailBody = CreateRawButton("DetailBodyImage", detailPanelTransform);
        SetSourceRect(detailBody.rectTransform, 12, 232, 400, 87);
        RawImage detailIcon = CreateRawButton("DetailIconImage", detailPanelTransform);
        SetSourceRect(detailIcon.rectTransform, 19, 15, 15, 16);
        TextMeshProUGUI detailHeader = CreateTextLabel(
            "DetailHeaderTextField",
            detailPanelTransform
        );
        detailHeader.text = "Message";
        detailHeader.color = Color.white;
        detailHeader.fontSize = 13;
        detailHeader.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(detailHeader.rectTransform, 40, 13, 320, 18);
        RawImage detailNext = CreateRawButton("DetailNextButtonImage", detailPanelTransform);
        SetSourceRect(detailNext.rectTransform, 367, 15, 19, 15);
        Button detailNextButton = CreateButton(detailNext);
        RawImage detailPrevious = CreateRawButton(
            "DetailPreviousButtonImage",
            detailPanelTransform
        );
        SetSourceRect(detailPrevious.rectTransform, 390, 15, 19, 15);
        Button detailPreviousButton = CreateButton(detailPrevious);

        ScrollAreaView detailLinesScrollArea = CreateScrollAreaView(
            detailPanelTransform,
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

        Transform[] detailLayerOrder =
        {
            detailCard.transform,
            detailOverlay.transform,
            detailStrip.transform,
            detailBody.transform,
            detailLinesScrollArea.transform,
            detailIcon.transform,
            detailHeader.transform,
            detailNext.transform,
            detailPrevious.transform,
        };
        for (int index = 0; index < detailLayerOrder.Length; index++)
            detailLayerOrder[index].SetAsLastSibling();

        Transform[] windowLayerOrder =
        {
            indexPanelTransform,
            detailPanelTransform,
            overlay.transform,
            commandBarTransform,
        };
        for (int index = 0; index < windowLayerOrder.Length; index++)
            windowLayerOrder[index].SetAsLastSibling();

        AssignReference(view, "windowShell", windowShell);
        AssignReference(view, "overlayFrameImage", overlay);
        AssignReference(view, "commandBar", commandBar);
        AssignReference(view, "indexPanel", indexPanel);
        AssignReference(view, "detailPanel", detailPanel);

        AssignReference(commandBar, "buttonStripImage", buttonStrip);
        AssignReference(commandBar, "closeButtonImage", closeButtonImage);
        AssignReference(
            commandBar,
            "closeButtonPressVisual",
            closeButtonImage.GetComponent<RawImagePressVisual>()
        );
        AssignReference(commandBar, "closeButton", closeButton);
        AssignReference(commandBar, "displayButtonImage", displayButtonImage);
        AssignReference(
            commandBar,
            "displayButtonPressVisual",
            displayButtonImage.GetComponent<RawImagePressVisual>()
        );
        AssignReference(commandBar, "displayButton", displayButton);
        AssignReference(commandBar, "indexButtonImage", indexButtonImage);
        AssignReference(
            commandBar,
            "indexButtonPressVisual",
            indexButtonImage.GetComponent<RawImagePressVisual>()
        );
        AssignReference(commandBar, "indexButton", indexButton);
        AssignReference(commandBar, "signalButtonImage", signalButtonImage);
        AssignReference(
            commandBar,
            "signalButtonPressVisual",
            signalButtonImage.GetComponent<RawImagePressVisual>()
        );
        AssignReference(commandBar, "signalButton", signalButton);
        AssignReference(commandBar, "signalTargetButtonImage", signalTargetButtonImage);
        AssignReference(
            commandBar,
            "signalTargetButtonPressVisual",
            signalTargetButtonImage.GetComponent<RawImagePressVisual>()
        );
        AssignReference(commandBar, "signalTargetButton", signalTargetButton);
        AssignReference(commandBar, "chatButtonImage", chatCommandButtonImage);
        AssignReference(
            commandBar,
            "chatButtonPressVisual",
            chatCommandButtonImage.GetComponent<RawImagePressVisual>()
        );
        AssignReference(commandBar, "chatButton", chatCommandButton);

        AssignReference(indexPanel, "backgroundImage", background);
        AssignReferenceArray(indexPanel, "tabImages", tabSlots);
        AssignReferenceArray(indexPanel, "tabPressVisuals", tabPressVisuals);
        AssignReferenceArray(indexPanel, "tabButtons", tabButtons);
        AssignReference(indexPanel, "titleTextField", tabTitle);
        AssignReference(indexPanel, "rowsScrollArea", rowsScrollArea);
        AssignReference(indexPanel, "rowTemplate", rowTemplate);
        AssignInt(indexPanel, "rowsContentPadding", 5);
        AssignReference(indexPanel, "selectAllButtonImage", selectAll);
        AssignReference(
            indexPanel,
            "selectAllButtonPressVisual",
            selectAll.GetComponent<RawImagePressVisual>()
        );
        AssignReference(indexPanel, "selectAllButton", selectAllButton);
        AssignReference(indexPanel, "removeSelectedButtonImage", removeSelected);
        AssignReference(
            indexPanel,
            "removeSelectedButtonPressVisual",
            removeSelected.GetComponent<RawImagePressVisual>()
        );
        AssignReference(indexPanel, "removeSelectedButton", removeSelectedButton);

        AssignReference(detailPanel, "stripImage", detailStrip);
        AssignReference(detailPanel, "cardImage", detailCard);
        AssignReference(detailPanel, "overlayImage", detailOverlay);
        AssignReference(detailPanel, "bodyImage", detailBody);
        AssignReference(detailPanel, "iconImage", detailIcon);
        AssignReference(detailPanel, "headerTextField", detailHeader);
        AssignReference(detailPanel, "nextButtonImage", detailNext);
        AssignReference(
            detailPanel,
            "nextButtonPressVisual",
            detailNext.GetComponent<RawImagePressVisual>()
        );
        AssignReference(detailPanel, "nextButton", detailNextButton);
        AssignReference(detailPanel, "previousButtonImage", detailPrevious);
        AssignReference(
            detailPanel,
            "previousButtonPressVisual",
            detailPrevious.GetComponent<RawImagePressVisual>()
        );
        AssignReference(detailPanel, "previousButton", detailPreviousButton);
        AssignReference(detailPanel, "linesScrollArea", detailLinesScrollArea);
        AssignReference(detailPanel, "lineTextTemplate", detailLineTemplate);
        AssignInt(detailPanel, "linesContentPadding", 4);

        AssignVector2Int(rowTemplate, "normalIconOffset", new Vector2Int(-1, -1));
        AssignReference(
            indexPanel,
            "backgroundTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_background")
        );
        AssignReference(
            indexPanel,
            "selectAllButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_select_all_button_up")
        );
        AssignReference(
            indexPanel,
            "selectAllButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_select_all_button_pressed")
        );
        AssignReference(
            indexPanel,
            "removeSelectedButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_remove_selected_button_up")
        );
        AssignReference(
            indexPanel,
            "removeSelectedButtonDownTexture",
            LoadStrategyViewTexture(
                "ui_strategyview_messages_window_remove_selected_button_pressed"
            )
        );
        AssignReference(
            detailPanel,
            "previousButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_previous_button_up")
        );
        AssignReference(
            detailPanel,
            "previousButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_previous_button_pressed")
        );
        AssignReference(
            detailPanel,
            "previousButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_previous_button_disabled")
        );
        AssignReference(
            detailPanel,
            "bodyTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_detail_body")
        );
        AssignReference(
            detailPanel,
            "stripTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_detail_strip")
        );
        AssignReference(
            detailPanel,
            "nextButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_next_button_up")
        );
        AssignReference(
            detailPanel,
            "nextButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_next_button_pressed")
        );
        AssignReference(
            detailPanel,
            "nextButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_messages_window_next_button_disabled")
        );
        GameObject saved = SaveGeneratedPrefabAsset(window, _messagesWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<MessagesWindowView>();
    }

    /// <summary>
    /// Authors the Encyclopedia window prefab.
    /// </summary>
    /// <returns>The generated Encyclopedia window view.</returns>
    private static EncyclopediaWindowView BuildEncyclopediaWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_encyclopediaWindowPrefabPath));

        GameObject window = new GameObject(
            "EncyclopediaWindow",
            typeof(RectTransform),
            typeof(UIWindow),
            typeof(EncyclopediaWindowView)
        );
        EncyclopediaWindowView view = EnableRuntimeComponent(
            window.GetComponent<EncyclopediaWindowView>()
        );
        ConfigureWindowRoot(window.GetComponent<UIWindow>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);

        RawImage background = CreateRawButton(
            "BackgroundImage",
            window.transform,
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_background"
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
            "UpperLayout",
            true
        );
        List<Button> upperButtons = CreateButtons(upperButtonImages);
        List<RawImagePressVisual> upperButtonPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < upperButtonImages.Count; index++)
        {
            upperButtonPressVisuals.Add(
                upperButtonImages[index].GetComponent<RawImagePressVisual>()
            );
        }
        List<RawImage> lowerButtonImages = CreateEncyclopediaDialogButtonSlots(
            buttons,
            "LowerLayout",
            false
        );
        List<Button> lowerButtons = CreateButtons(lowerButtonImages);
        List<RawImagePressVisual> lowerButtonPressVisuals = new List<RawImagePressVisual>();
        for (int index = 0; index < lowerButtonImages.Count; index++)
        {
            lowerButtonPressVisuals.Add(
                lowerButtonImages[index].GetComponent<RawImagePressVisual>()
            );
        }

        RectTransform indexPanelRoot = CreateSourceRectLayer(
            "IndexPanel",
            window.transform,
            470,
            331
        );
        EncyclopediaIndexPanelView indexPanel = EnableRuntimeComponent(
            indexPanelRoot.gameObject.AddComponent<EncyclopediaIndexPanelView>()
        );

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", indexPanelRoot);
        title.text = "Galactic Encyclopedia";
        title.color = Color.white;
        title.fontSize = 13;
        title.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(title.rectTransform, 100, 14, 260, 17);
        TextMeshProUGUI topic = CreateTextLabel("TopicLabelTextField", indexPanelRoot);
        topic.text = "Topic";
        topic.color = Color.white;
        topic.fontSize = 12;
        topic.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(topic.rectTransform, 37, 48, 220, 16);
        TMP_InputField entryName = CreateTextInputField(
            "EntryNameInputField",
            indexPanelRoot,
            string.Empty,
            143,
            45,
            245,
            18
        );

        RectTransform tabs = CreateSourceRectLayer("Tabs", indexPanelRoot, 470, 331);
        string[] encyclopediaTabPreviewPaths =
        {
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_all_systems_button_up",
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_system_button_up",
            PreviewTheme?.StrategyWindows?.Encyclopedia?.ShipButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.FacilityButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.MissionsButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.TroopButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.PersonnelButton?.UpImagePath,
        };
        string[] encyclopediaTabPreviewDownPaths =
        {
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_all_systems_button_pressed",
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_system_button_pressed",
            PreviewTheme?.StrategyWindows?.Encyclopedia?.ShipButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.FacilityButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.MissionsButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.TroopButton?.DownImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.PersonnelButton?.DownImagePath,
        };
        List<RawImage> tabSlots = new List<RawImage>();
        for (int i = 0; i < encyclopediaTabPreviewPaths.Length; i++)
        {
            RawImage image = CreateRawButton(
                $"{EncyclopediaWindowTabCatalog.GetTab(i)}TabButtonImage",
                tabs,
                encyclopediaTabPreviewPaths[i]
            );
            Texture upTexture = image.texture;
            Texture downTexture = LoadTexture(encyclopediaTabPreviewDownPaths[i]);
            int upWidth = GetTextureSourceWidthOrDefault(upTexture, 49);
            int downWidth = GetTextureSourceWidthOrDefault(downTexture, 49);
            int upHeight = GetTextureSourceHeightOrDefault(upTexture, 41);
            int downHeight = GetTextureSourceHeightOrDefault(downTexture, 41);
            int width = Mathf.Max(upWidth, downWidth);
            int height = Mathf.Max(upHeight, downHeight);
            SetSourceRect(image.rectTransform, 36 + i * 52, 78, width, height);
            tabSlots.Add(image);
        }
        List<Button> tabButtons = CreateButtons(tabSlots);
        TextMeshProUGUI tabTitle = CreateTextLabel("TabTitleTextField", indexPanelRoot);
        tabTitle.text = "All Databases";
        tabTitle.color = Color.white;
        tabTitle.fontSize = 12;
        tabTitle.alignment = TextAlignmentOptions.TopLeft;
        SetSourceRect(tabTitle.rectTransform, 40, 120, 330, 16);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            indexPanelRoot,
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
        EncyclopediaWindowRowView rowTemplate = EnableRuntimeComponent(
            rowHitArea.gameObject.AddComponent<EncyclopediaWindowRowView>()
        );
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

        RectTransform detailPanelRoot = CreateSourceRectLayer(
            "DetailPanel",
            window.transform,
            470,
            331
        );
        EncyclopediaDetailPanelView detailPanel = EnableRuntimeComponent(
            detailPanelRoot.gameObject.AddComponent<EncyclopediaDetailPanelView>()
        );

        RawImage detailBackground = CreateRawButton(
            "DetailBackgroundImage",
            detailPanelRoot,
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_topic_background"
        );
        SetSourceRect(detailBackground.rectTransform, 12, 13, 400, 306);
        RawImage detailCard = CreateRawButton("DetailCardImage", detailPanelRoot);
        SetSourceRect(detailCard.rectTransform, 12, 31, 400, 200);
        RawImage detailPrevious = CreateRawButton(
            "DetailPreviousButtonImage",
            detailPanelRoot,
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_previous_button_up"
        );
        SetSourceRect(detailPrevious.rectTransform, 31, 13, 21, 17);
        Button detailPreviousButton = CreateButton(detailPrevious);
        RawImage detailNext = CreateRawButton(
            "DetailNextButtonImage",
            detailPanelRoot,
            "Art/HD/UI/StrategyView/ui_strategyview_encyclopedia_window_next_button_up"
        );
        SetSourceRect(detailNext.rectTransform, 380, 13, 21, 17);
        Button detailNextButton = CreateButton(detailNext);
        TextMeshProUGUI detailTitle = CreateTextLabel("DetailTitleTextField", detailPanelRoot);
        detailTitle.text = "Database Entry";
        detailTitle.color = Color.white;
        detailTitle.fontSize = 13;
        detailTitle.alignment = TextAlignmentOptions.Top;
        SetSourceRect(detailTitle.rectTransform, 46, 14, 220, 17);

        ScrollAreaView detailLinesScrollArea = CreateScrollAreaView(
            detailPanelRoot,
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
        detailLineTemplate.text = "Entry text";
        detailLineTemplate.color = Color.white;
        detailLineTemplate.fontSize = 13;
        detailLineTemplate.alignment = TextAlignmentOptions.TopLeft;
        detailLineTemplate.textWrappingMode = TextWrappingModes.NoWrap;
        detailLineTemplate.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(detailLineTemplate.rectTransform, 0, 0, 382, 16);
        detailLineTemplate.gameObject.SetActive(false);

        AssignReference(view, "overlayFrameImage", overlay);
        AssignReference(view, "buttonStripImage", strip);
        AssignReferenceArray(view, "upperButtonImages", upperButtonImages);
        AssignReferenceArray(view, "upperButtonPressVisuals", upperButtonPressVisuals);
        AssignReferenceArray(view, "upperButtons", upperButtons);
        AssignReferenceArray(view, "lowerButtonImages", lowerButtonImages);
        AssignReferenceArray(view, "lowerButtonPressVisuals", lowerButtonPressVisuals);
        AssignReferenceArray(view, "lowerButtons", lowerButtons);
        AssignReference(view, "indexPanel", indexPanel);
        AssignReference(view, "detailPanel", detailPanel);

        AssignReference(indexPanel, "backgroundImage", background);
        AssignReference(indexPanel, "entryNameInputField", entryName);
        AssignReferenceArray(indexPanel, "tabImageSlots", tabSlots);
        AssignReferenceArray(indexPanel, "tabButtons", tabButtons);
        AssignReference(indexPanel, "tabTitleTextField", tabTitle);
        AssignReference(indexPanel, "rowsScrollArea", rowsScrollArea);
        AssignReference(indexPanel, "rowTemplate", rowTemplate);
        AssignReference(indexPanel, "rowTextTemplate", rowTextTemplate);
        AssignReference(indexPanel, "navigationScope", window.GetComponent<RectTransform>());
        AssignInt(indexPanel, "contentBottomPadding", 1);
        AssignReference(
            indexPanel,
            "systemButtonDownTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_system_button_pressed")
        );
        AssignReference(
            indexPanel,
            "systemButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_system_button_up")
        );
        AssignReference(
            indexPanel,
            "allSystemsButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_all_systems_button_up")
        );
        AssignReference(
            indexPanel,
            "allSystemsButtonDownTexture",
            LoadStrategyViewTexture(
                "ui_strategyview_encyclopedia_window_all_systems_button_pressed"
            )
        );

        AssignReference(detailPanel, "cardImage", detailCard);
        AssignReference(detailPanel, "previousButtonImage", detailPrevious);
        AssignReference(detailPanel, "previousButton", detailPreviousButton);
        AssignReference(detailPanel, "nextButtonImage", detailNext);
        AssignReference(detailPanel, "nextButton", detailNextButton);
        AssignReference(detailPanel, "titleTextField", detailTitle);
        AssignReference(detailPanel, "linesScrollArea", detailLinesScrollArea);
        AssignReference(detailPanel, "lineTextTemplate", detailLineTemplate);
        AssignInt(detailPanel, "indentationWidth", 25);
        AssignInt(detailPanel, "columnGap", 20);
        AssignInt(detailPanel, "contentBottomPadding", 4);
        AssignInt(detailPanel, "scrollStepOverlap", 1);
        AssignReference(
            detailPanel,
            "nextButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_next_button_up")
        );
        AssignReference(
            detailPanel,
            "nextButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_next_button_disabled")
        );
        AssignReference(
            detailPanel,
            "previousButtonUpTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_previous_button_up")
        );
        AssignReference(
            detailPanel,
            "previousButtonDisabledTexture",
            LoadStrategyViewTexture("ui_strategyview_encyclopedia_window_previous_button_disabled")
        );

        buttons.SetAsLastSibling();
        GameObject saved = SaveGeneratedPrefabAsset(window, _encyclopediaWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<EncyclopediaWindowView>();
    }

    /// <summary>
    /// Authors a reusable manufacturing-lane card.
    /// </summary>
    /// <param name="parent">The Facility window transform.</param>
    /// <param name="name">The card object name.</param>
    /// <param name="cardY">The card source-space y-coordinate.</param>
    /// <param name="countTextY">The facility-count label y-coordinate.</param>
    /// <returns>The configured manufacturing-lane card.</returns>
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
        ManufacturingLaneCardView view = EnableRuntimeComponent(
            card.GetComponent<ManufacturingLaneCardView>()
        );
        SetSourceRect(card.GetComponent<RectTransform>(), 0, 0, 226, 304);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            card.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        hitArea.raycastTarget = true;
        SetSourceRect(hitArea.rectTransform, 55, cardY, 163, 74);
        CreateRawImage(
            "BaseCardImage",
            card.transform,
            _facilityManufacturingCardPreviewPath,
            55,
            cardY,
            _facilityManufacturingCardWidth,
            _facilityManufacturingCardHeight
        );
        RawImage stateCard = CreateRawImage(
            "StateCardImage",
            card.transform,
            _facilityManufacturingCardStatePreviewPath,
            55,
            cardY,
            _facilityManufacturingCardStateWidth,
            _facilityManufacturingCardStateHeight
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
        SetSourceRect(progress.rectTransform, 56, cardY + 65, 158, 4);

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
        AssignReference(view, "stateCardImage", stateCard);
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

    /// <summary>
    /// Authors the Facility window's reusable inventory item template.
    /// </summary>
    /// <param name="parent">The inventory container transform.</param>
    /// <returns>The configured inventory item template.</returns>
    private static FacilityInventoryItemView CreateFacilityInventoryItemTemplate(Transform parent)
    {
        GameObject item = new GameObject(
            "InventoryItemTemplate",
            typeof(RectTransform),
            typeof(FacilityInventoryItemView)
        );
        item.transform.SetParent(parent, false);
        FacilityInventoryItemView view = EnableRuntimeComponent(
            item.GetComponent<FacilityInventoryItemView>()
        );
        SetSourceRect(item.GetComponent<RectTransform>(), 10, 82, 67, 40);

        RawImage hitArea = CreatePanelImage(
            "HitAreaImage",
            item.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        hitArea.raycastTarget = true;
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

    /// <summary>
    /// Authors the reusable confirmation window prefab.
    /// </summary>
    /// <returns>The generated confirmation window view.</returns>
    private static ConfirmDialogWindowView BuildConfirmDialogWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_confirmDialogWindowPrefabPath));

        GameObject window = new GameObject(
            "ConfirmDialogWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        window.SetActive(false);
        UIWindow windowShell = window.GetComponent<UIWindow>();
        ConfirmDialogWindowView view = EnableRuntimeComponent(
            window.AddComponent<ConfirmDialogWindowView>()
        );
        ConfigureWindowRoot(windowShell);
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
        ConfigureWindowDragHandle(title.gameObject, windowShell);

        RawImage confirmButton = CreateRawImage(
            "ConfirmButtonImage",
            window.transform,
            _confirmButtonPreviewPath,
            355,
            244
        );
        Button confirmButtonComponent = CreateButton(confirmButton);
        RawImagePressVisual confirmButtonPressVisual =
            confirmButton.GetComponent<RawImagePressVisual>();
        RawImage cancelButton = CreateRawImage(
            "CancelButtonImage",
            window.transform,
            _cancelButtonPreviewPath,
            355,
            281
        );
        Button cancelButtonComponent = CreateButton(cancelButton);
        RawImagePressVisual cancelButtonPressVisual =
            cancelButton.GetComponent<RawImagePressVisual>();

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
        AssignReference(view, "confirmButtonPressVisual", confirmButtonPressVisual);
        AssignReference(view, "cancelButtonPressVisual", cancelButtonPressVisual);
        AssignReference(view, "linesScrollArea", linesScrollArea);
        AssignReference(view, "lineTemplate", lineTemplate);
        AssignReference(view, "confirmButtonUpTexture", LoadTexture(_confirmButtonPreviewPath));
        AssignReference(
            view,
            "confirmButtonDownTexture",
            LoadTexture(_confirmButtonDownPreviewPath)
        );
        AssignReference(view, "cancelButtonUpTexture", LoadTexture(_cancelButtonPreviewPath));
        AssignReference(view, "cancelButtonDownTexture", LoadTexture(_cancelButtonDownPreviewPath));

        window.SetActive(true);
        GameObject saved = SaveGeneratedPrefabAsset(window, _confirmDialogWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<ConfirmDialogWindowView>();
    }

    /// <summary>
    /// Authors the battle-alert and battle-result window prefab.
    /// </summary>
    /// <returns>The generated battle-alert window view.</returns>
    private static BattleAlertWindowView BuildBattleAlertWindowPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_battleAlertWindowPrefabPath));

        GameObject window = new GameObject(
            "BattleAlertWindow",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        window.SetActive(false);
        BattleAlertWindowView view = EnableRuntimeComponent(
            window.AddComponent<BattleAlertWindowView>()
        );
        ConfigureWindowRoot(window.GetComponent<UIWindow>());
        SetSourceRect(window.GetComponent<RectTransform>(), 0, 0, 470, 331);
        BattleAlertWindowTheme theme = PreviewTheme?.StrategyWindows?.BattleAlert;

        RawImage panelBackground = CreateRawImage(
            "PanelBackgroundImage",
            window.transform,
            theme?.SummaryBackgroundImagePath,
            12,
            13
        );

        RawImage frame = CreateRawImage(
            "FrameImage",
            window.transform,
            theme?.FrameImagePath,
            0,
            0
        );
        frame.raycastTarget = true;

        TextMeshProUGUI title = CreateTextLabel("TitleTextField", window.transform);
        title.text = "Battle at System";
        title.color = Color.white;
        title.fontSize = 18;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Top;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(title.rectTransform, 32, 28, 358, 22);

        TextMeshProUGUI header = CreateTextLabel("HeaderTextField", window.transform);
        header.text = "Battle Summary";
        header.color = Color.white;
        header.fontSize = 18;
        header.fontStyle = FontStyles.Bold;
        header.alignment = TextAlignmentOptions.Top;
        header.textWrappingMode = TextWrappingModes.NoWrap;
        header.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(header.rectTransform, 32, 54, 358, 20);

        TextMeshProUGUI summary = CreateTextLabel("SummaryTextField", window.transform);
        summary.text = "The attacking fleet has entered the system.";
        summary.color = Color.white;
        summary.fontSize = 12;
        summary.fontStyle = FontStyles.Bold;
        summary.alignment = TextAlignmentOptions.Top;
        summary.textWrappingMode = TextWrappingModes.Normal;
        summary.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(summary.rectTransform, 42, 232, 338, 60);

        ScrollAreaView rowsScrollArea = CreateScrollAreaView(
            window.transform,
            "RowsScrollArea",
            32,
            70,
            358,
            186,
            0,
            6,
            358,
            180,
            316,
            0,
            13,
            180,
            out RectTransform rowsContent
        );
        rowsScrollArea.gameObject.SetActive(false);
        ConfigureVerticalContent(rowsContent);
        BattleAlertRowView rowTemplate = CreateBattleAlertRowTemplate(rowsContent);

        TextMeshProUGUI resultPlanetaryTitle = CreateBattleResultText(
            "ResultPlanetaryTitleTextField",
            window.transform,
            "Battle at System",
            new RectInt(12, 13, 400, 24),
            24,
            FontStyles.Normal,
            TextAlignmentOptions.Top
        );
        resultPlanetaryTitle.GetComponent<Shadow>().enabled = false;
        TextMeshProUGUI resultFleetTitle = CreateBattleResultText(
            "ResultFleetTitleTextField",
            window.transform,
            "Battle at System",
            new RectInt(12, 13, 400, 24),
            24,
            FontStyles.Normal,
            TextAlignmentOptions.Top
        );
        resultFleetTitle.GetComponent<Shadow>().enabled = false;
        TextMeshProUGUI resultSummary = CreateBattleResultText(
            "ResultSummaryTextField",
            window.transform,
            "Battle summary",
            new RectInt(25, 225, 350, 70),
            18,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft
        );
        resultSummary.textWrappingMode = TextWrappingModes.Normal;
        TextMeshProUGUI resultPlanetaryForceHeader = CreateBattleResultText(
            "ResultPlanetaryForceHeaderTextField",
            window.transform,
            string.IsNullOrEmpty(theme?.FirstForcesHeaderText)
                ? "First Forces"
                : theme.FirstForcesHeaderText,
            new RectInt(12, 36, 347, 20),
            18,
            FontStyles.Normal,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI resultFleetForceHeader = CreateBattleResultText(
            "ResultFleetForceHeaderTextField",
            window.transform,
            string.IsNullOrEmpty(theme?.FirstForcesHeaderText)
                ? "First Forces"
                : theme.FirstForcesHeaderText,
            new RectInt(12, 36, 347, 20),
            18,
            FontStyles.Normal,
            TextAlignmentOptions.Top
        );
        TextMeshProUGUI resultFleetFilters = CreateBattleResultText(
            "ResultFleetFiltersTextField",
            window.transform,
            "Filters",
            new RectInt(86, 70, 165, 18),
            16,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft
        );
        TextMeshProUGUI resultPlanetaryTableTitle = CreateBattleResultText(
            "ResultPlanetaryTableTitleTextField",
            window.transform,
            "Capital Ships",
            new RectInt(38, 100, 400, 18),
            16,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft
        );
        TextMeshProUGUI resultFleetTableTitle = CreateBattleResultText(
            "ResultFleetTableTitleTextField",
            window.transform,
            "Capital Ships",
            new RectInt(38, 100, 400, 18),
            16,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft
        );
        TextMeshProUGUI[] resultPlanetaryStandardColumnHeaders =
        {
            CreateBattleResultText(
                "ResultPlanetaryOperationalHeaderTextField",
                window.transform,
                "Operational",
                new RectInt(38, 117, 165, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
            CreateBattleResultText(
                "ResultPlanetaryDestroyedHeaderTextField",
                window.transform,
                "Destroyed",
                new RectInt(204, 117, 165, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
        };
        TextMeshProUGUI[] resultFleetStandardColumnHeaders =
        {
            CreateBattleResultText(
                "ResultFleetOperationalHeaderTextField",
                window.transform,
                "Operational",
                new RectInt(38, 117, 165, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
            CreateBattleResultText(
                "ResultFleetDestroyedHeaderTextField",
                window.transform,
                "Destroyed",
                new RectInt(204, 117, 165, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
        };
        TextMeshProUGUI[] resultPlanetaryPersonnelColumnHeaders =
        {
            CreateBattleResultText(
                "ResultPlanetarySurvivorsHeaderTextField",
                window.transform,
                "Survivors",
                new RectInt(38, 117, 110, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
            CreateBattleResultText(
                "ResultPlanetaryCapturedHeaderTextField",
                window.transform,
                "Captured",
                new RectInt(165, 117, 110, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
            CreateBattleResultText(
                "ResultPlanetaryKilledHeaderTextField",
                window.transform,
                "Killed",
                new RectInt(275, 117, 110, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
        };
        TextMeshProUGUI[] resultFleetPersonnelColumnHeaders =
        {
            CreateBattleResultText(
                "ResultFleetSurvivorsHeaderTextField",
                window.transform,
                "Survivors",
                new RectInt(38, 117, 110, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
            CreateBattleResultText(
                "ResultFleetCapturedHeaderTextField",
                window.transform,
                "Captured",
                new RectInt(165, 117, 110, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
            CreateBattleResultText(
                "ResultFleetKilledHeaderTextField",
                window.transform,
                "Killed",
                new RectInt(275, 117, 110, 18),
                16,
                FontStyles.Normal,
                TextAlignmentOptions.Top
            ),
        };
        TextMeshProUGUI[] resultDetailTextFields =
        {
            resultPlanetaryForceHeader,
            resultFleetForceHeader,
            resultFleetFilters,
            resultPlanetaryTableTitle,
            resultFleetTableTitle,
        };
        foreach (TextMeshProUGUI textField in resultDetailTextFields)
            textField.GetComponent<Shadow>().enabled = false;
        foreach (TextMeshProUGUI textField in resultPlanetaryStandardColumnHeaders)
            textField.GetComponent<Shadow>().enabled = false;
        foreach (TextMeshProUGUI textField in resultFleetStandardColumnHeaders)
            textField.GetComponent<Shadow>().enabled = false;
        foreach (TextMeshProUGUI textField in resultPlanetaryPersonnelColumnHeaders)
            textField.GetComponent<Shadow>().enabled = false;
        foreach (TextMeshProUGUI textField in resultFleetPersonnelColumnHeaders)
            textField.GetComponent<Shadow>().enabled = false;

        ScrollAreaView resultRowsScrollArea = CreateScrollAreaView(
            window.transform,
            "ResultRowsScrollArea",
            37,
            136,
            347,
            166,
            0,
            0,
            347,
            166,
            334,
            0,
            13,
            166,
            out RectTransform resultRowsContent
        );
        resultRowsScrollArea.gameObject.SetActive(false);
        RectTransform resultStandardOperationalColumn = CreateSourceRectLayer(
            "ResultStandardOperationalColumn",
            resultRowsContent,
            165,
            166
        );
        SetSourceRect(resultStandardOperationalColumn, 0, 0, 165, 166);
        RectTransform resultStandardDestroyedColumn = CreateSourceRectLayer(
            "ResultStandardDestroyedColumn",
            resultRowsContent,
            165,
            166
        );
        SetSourceRect(resultStandardDestroyedColumn, 165, 0, 165, 166);
        RectTransform resultPersonnelOperationalColumn = CreateSourceRectLayer(
            "ResultPersonnelOperationalColumn",
            resultRowsContent,
            110,
            166
        );
        SetSourceRect(resultPersonnelOperationalColumn, 0, 0, 110, 166);
        RectTransform resultPersonnelDestroyedColumn = CreateSourceRectLayer(
            "ResultPersonnelDestroyedColumn",
            resultRowsContent,
            110,
            166
        );
        SetSourceRect(resultPersonnelDestroyedColumn, 220, 0, 110, 166);

        RectTransform resultLayoutTemplates = CreateChildLayer(
            "ResultLayoutTemplates",
            window.transform
        );
        resultLayoutTemplates.gameObject.SetActive(false);
        BattleResultItemView resultStandardItemTemplate = CreateBattleResultItemTemplate(
            resultLayoutTemplates,
            "ResultStandardItemTemplate",
            new RectInt(0, 0, 165, 70),
            new RectInt(21, 10, 122, 50),
            new RectInt(0, 60, 165, 70),
            new RectInt(0, 10, 165, 70)
        );
        BattleResultItemView resultPersonnelItemTemplate = CreateBattleResultItemTemplate(
            resultLayoutTemplates,
            "ResultPersonnelItemTemplate",
            new RectInt(0, 0, 110, 50),
            new RectInt(0, 0, 110, 50),
            new RectInt(0, 50, 110, 50),
            new RectInt(0, 10, 110, 50)
        );

        WindowButtonImageTheme[] viewButtonThemes =
        {
            theme?.SummaryButton,
            theme?.FirstForcesButton,
            theme?.SecondForcesButton,
            theme?.SystemAssetsButton,
        };
        string[] viewButtonNames =
        {
            "SummaryButtonImage",
            "FirstForcesButtonImage",
            "SecondForcesButtonImage",
            "SystemAssetsButtonImage",
        };
        List<RawImage> viewButtonImages = new List<RawImage>();
        List<RawImagePressVisual> viewButtonPressVisuals = new List<RawImagePressVisual>();
        List<Button> viewButtons = new List<Button>();
        for (int i = 0; i < viewButtonThemes.Length; i++)
        {
            RawImage image = CreateBattleAlertButtonImage(
                viewButtonNames[i],
                window.transform,
                viewButtonThemes[i],
                418,
                21 + i * 60,
                41,
                41
            );
            viewButtonImages.Add(image);
            viewButtons.Add(CreateButton(image));
            viewButtonPressVisuals.Add(image.GetComponent<RawImagePressVisual>());
        }

        WindowButtonImageTheme[] commandButtonThemes =
        {
            theme?.RetreatButton,
            theme?.AutoResolveButton,
            theme?.TakeCommandButton,
        };
        string[] commandButtonNames =
        {
            "RetreatButtonImage",
            "AutoResolveButtonImage",
            "TakeCommandButtonImage",
        };
        int[] commandButtonFallbackXs = { 12, 146, 280 };
        List<RawImage> commandButtonImages = new List<RawImage>();
        List<RawImagePressVisual> commandButtonPressVisuals = new List<RawImagePressVisual>();
        List<Button> commandButtons = new List<Button>();
        for (int i = 0; i < commandButtonThemes.Length; i++)
        {
            RawImage image = CreateBattleAlertButtonImage(
                commandButtonNames[i],
                window.transform,
                commandButtonThemes[i],
                commandButtonFallbackXs[i],
                296,
                134,
                27
            );
            commandButtonImages.Add(image);
            commandButtons.Add(CreateButton(image));
            commandButtonPressVisuals.Add(image.GetComponent<RawImagePressVisual>());
        }

        RawImage resultCloseButtonImage = CreateBattleAlertButtonImage(
            "ResultCloseButtonImage",
            window.transform,
            theme?.ResultCloseButton,
            423,
            25,
            32,
            31
        );
        Button resultCloseButton = CreateButton(resultCloseButtonImage);
        RawImagePressVisual resultCloseButtonPressVisual =
            resultCloseButtonImage.GetComponent<RawImagePressVisual>();
        resultCloseButtonImage.gameObject.SetActive(false);

        WindowButtonImageTheme[] resultCategoryButtonThemes =
        {
            theme?.ResultCapitalShipsButton,
            theme?.ResultStarfightersButton,
            theme?.ResultManufacturingButton,
            theme?.ResultDefenseButton,
            theme?.ResultTroopsButton,
            theme?.ResultPersonnelButton,
        };
        SourceRectLayout[] resultCategoryButtonLayouts =
        {
            theme?.PlanetaryResultCapitalShipsLayout,
            theme?.PlanetaryResultStarfightersLayout,
            theme?.PlanetaryResultManufacturingLayout,
            theme?.PlanetaryResultDefenseLayout,
            theme?.PlanetaryResultTroopsLayout,
            theme?.PlanetaryResultPersonnelLayout,
        };
        string[] resultCategoryButtonNames =
        {
            "ResultCapitalShipsButtonImage",
            "ResultStarfightersButtonImage",
            "ResultManufacturingButtonImage",
            "ResultDefenseButtonImage",
            "ResultTroopsButtonImage",
            "ResultPersonnelButtonImage",
        };
        int[] resultCategoryButtonFallbackXs = { 36, 98, 160, 222, 284, 348 };
        List<RawImage> resultCategoryButtonImages = new List<RawImage>();
        List<RawImagePressVisual> resultCategoryButtonPressVisuals =
            new List<RawImagePressVisual>();
        List<Button> resultCategoryButtons = new List<Button>();
        for (int i = 0; i < resultCategoryButtonThemes.Length; i++)
        {
            RawImage image = CreateBattleAlertButtonImage(
                resultCategoryButtonNames[i],
                window.transform,
                resultCategoryButtonThemes[i],
                resultCategoryButtonFallbackXs[i],
                59,
                49,
                41,
                resultCategoryButtonLayouts[i]
            );
            resultCategoryButtonImages.Add(image);
            resultCategoryButtons.Add(CreateButton(image));
            resultCategoryButtonPressVisuals.Add(image.GetComponent<RawImagePressVisual>());
            image.gameObject.SetActive(false);
        }

        WindowButtonImageTheme[] resultDirectButtonThemes =
        {
            theme?.ResultDirectSystemButton,
            theme?.ResultDirectFleetButton,
        };
        string[] resultDirectButtonNames =
        {
            "ResultDirectSystemButtonImage",
            "ResultDirectFleetButtonImage",
        };
        int[] resultDirectButtonFallbackXs = { 29, 226 };
        List<RawImage> resultDirectButtonImages = new List<RawImage>();
        List<RawImagePressVisual> resultDirectButtonPressVisuals = new List<RawImagePressVisual>();
        List<Button> resultDirectButtons = new List<Button>();
        for (int i = 0; i < resultDirectButtonThemes.Length; i++)
        {
            RawImage image = CreateBattleAlertButtonImage(
                resultDirectButtonNames[i],
                window.transform,
                resultDirectButtonThemes[i],
                resultDirectButtonFallbackXs[i],
                182,
                169,
                96
            );
            resultDirectButtonImages.Add(image);
            resultDirectButtons.Add(CreateButton(image));
            resultDirectButtonPressVisuals.Add(image.GetComponent<RawImagePressVisual>());
            image.gameObject.SetActive(false);
        }

        AssignReference(view, "panelBackgroundImage", panelBackground);
        AssignReference(view, "frameImage", frame);
        AssignReference(view, "titleTextField", title);
        AssignReference(view, "headerTextField", header);
        AssignReference(view, "summaryTextField", summary);
        AssignReference(view, "rowsScrollArea", rowsScrollArea);
        AssignReference(view, "rowTemplate", rowTemplate);
        AssignReference(view, "resultPlanetaryTitleTextField", resultPlanetaryTitle);
        AssignReference(view, "resultFleetTitleTextField", resultFleetTitle);
        AssignReference(view, "resultSummaryTextField", resultSummary);
        AssignReference(view, "resultPlanetaryForceHeaderTextField", resultPlanetaryForceHeader);
        AssignReference(view, "resultFleetForceHeaderTextField", resultFleetForceHeader);
        AssignReference(view, "resultFleetFiltersTextField", resultFleetFilters);
        AssignReference(view, "resultPlanetaryTableTitleTextField", resultPlanetaryTableTitle);
        AssignReference(view, "resultFleetTableTitleTextField", resultFleetTableTitle);
        AssignReferenceArray(
            view,
            "resultPlanetaryStandardColumnHeaderTextFields",
            resultPlanetaryStandardColumnHeaders
        );
        AssignReferenceArray(
            view,
            "resultFleetStandardColumnHeaderTextFields",
            resultFleetStandardColumnHeaders
        );
        AssignReferenceArray(
            view,
            "resultPlanetaryPersonnelColumnHeaderTextFields",
            resultPlanetaryPersonnelColumnHeaders
        );
        AssignReferenceArray(
            view,
            "resultFleetPersonnelColumnHeaderTextFields",
            resultFleetPersonnelColumnHeaders
        );
        AssignReference(view, "resultRowsScrollArea", resultRowsScrollArea);
        AssignReference(view, "resultStandardOperationalColumn", resultStandardOperationalColumn);
        AssignReference(view, "resultStandardDestroyedColumn", resultStandardDestroyedColumn);
        AssignReference(view, "resultPersonnelOperationalColumn", resultPersonnelOperationalColumn);
        AssignReference(view, "resultPersonnelDestroyedColumn", resultPersonnelDestroyedColumn);
        AssignReference(view, "resultStandardItemTemplate", resultStandardItemTemplate);
        AssignReference(view, "resultPersonnelItemTemplate", resultPersonnelItemTemplate);
        AssignReferenceArray(view, "viewButtonImages", viewButtonImages);
        AssignReferenceArray(view, "viewButtonPressVisuals", viewButtonPressVisuals);
        AssignReferenceArray(view, "viewButtons", viewButtons);
        AssignReferenceArray(view, "commandButtonImages", commandButtonImages);
        AssignReferenceArray(view, "commandButtonPressVisuals", commandButtonPressVisuals);
        AssignReferenceArray(view, "commandButtons", commandButtons);
        AssignReference(view, "resultCloseButtonImage", resultCloseButtonImage);
        AssignReference(view, "resultCloseButtonPressVisual", resultCloseButtonPressVisual);
        AssignReference(view, "resultCloseButton", resultCloseButton);
        AssignReferenceArray(view, "resultCategoryButtonImages", resultCategoryButtonImages);
        AssignReferenceArray(
            view,
            "resultCategoryButtonPressVisuals",
            resultCategoryButtonPressVisuals
        );
        AssignReferenceArray(view, "resultCategoryButtons", resultCategoryButtons);
        AssignReferenceArray(view, "resultDirectButtonImages", resultDirectButtonImages);
        AssignReferenceArray(
            view,
            "resultDirectButtonPressVisuals",
            resultDirectButtonPressVisuals
        );
        AssignReferenceArray(view, "resultDirectButtons", resultDirectButtons);

        window.SetActive(true);
        GameObject saved = SaveGeneratedPrefabAsset(window, _battleAlertWindowPrefabPath);
        Object.DestroyImmediate(window);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return saved.GetComponent<BattleAlertWindowView>();
    }

    /// <summary>
    /// Authors one battle-alert button image from themed or fallback source bounds.
    /// </summary>
    /// <param name="name">The authored image object name.</param>
    /// <param name="parent">The authored image parent.</param>
    /// <param name="theme">The applicable button theme.</param>
    /// <param name="fallbackX">The fallback horizontal source coordinate.</param>
    /// <param name="fallbackY">The fallback vertical source coordinate.</param>
    /// <param name="fallbackWidth">The fallback source width.</param>
    /// <param name="fallbackHeight">The fallback source height.</param>
    /// <param name="sourceLayout">An optional context-specific source rectangle.</param>
    /// <returns>The authored button image.</returns>
    private static RawImage CreateBattleAlertButtonImage(
        string name,
        Transform parent,
        WindowButtonImageTheme theme,
        int fallbackX,
        int fallbackY,
        int fallbackWidth,
        int fallbackHeight,
        SourceRectLayout sourceLayout = null
    )
    {
        if (sourceLayout != null)
            return CreateRawImage(name, parent, theme?.UpImagePath, sourceLayout);

        if (theme?.SourceLayout != null)
            return CreateRawImage(name, parent, theme.UpImagePath, theme.SourceLayout);

        return CreateRawImage(
            name,
            parent,
            theme?.UpImagePath,
            fallbackX,
            fallbackY,
            fallbackWidth,
            fallbackHeight
        );
    }

    /// <summary>
    /// Authors the battle-alert list's reusable row template.
    /// </summary>
    /// <param name="parent">The battle-alert row container.</param>
    /// <returns>The configured battle-alert row template.</returns>
    private static BattleAlertRowView CreateBattleAlertRowTemplate(Transform parent)
    {
        GameObject row = new GameObject(
            "RowTemplate",
            typeof(RectTransform),
            typeof(BattleAlertRowView)
        );
        row.transform.SetParent(parent, false);
        SetSourceRect(row.GetComponent<RectTransform>(), 0, 0, 358, 34);
        RectTransform iconSlot = CreateSourceRectLayer("IconSlot", row.transform, 165, 34);
        SetSourceRect(iconSlot, 0, 0, 165, 34);
        RawImage icon = CreateRawImage("IconImage", row.transform, null, 0, 0);
        SetSourceRect(icon.rectTransform, 0, 0, 165, 34);
        TextMeshProUGUI text = CreateTextLabel("TextField", row.transform);
        text.text = "Combat row";
        text.color = Color.white;
        text.fontSize = 12;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(text.rectTransform, 0, 9, 358, 16);

        BattleAlertRowView view = EnableRuntimeComponent(row.GetComponent<BattleAlertRowView>());
        AssignReference(view, "iconSlot", iconSlot);
        AssignReference(view, "iconImage", icon);
        AssignReference(view, "textField", text);
        AssignFloat(view, "iconTextGap", 5);
        AddTemplateLayoutElement(row.GetComponent<RectTransform>());
        row.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors one initially hidden battle-result text element.
    /// </summary>
    /// <param name="name">The text object name.</param>
    /// <param name="parent">The owning result transform.</param>
    /// <param name="previewText">The preview text.</param>
    /// <param name="rect">The source-space text rectangle.</param>
    /// <param name="fontSize">The authored font size.</param>
    /// <param name="fontStyle">The authored font style.</param>
    /// <param name="alignment">The authored text alignment.</param>
    /// <returns>The configured text element.</returns>
    private static TextMeshProUGUI CreateBattleResultText(
        string name,
        Transform parent,
        string previewText,
        RectInt rect,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment
    )
    {
        TextMeshProUGUI text = CreateTextLabel(name, parent);
        text.text = previewText;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(text.rectTransform, rect.x, rect.y, rect.width, rect.height);
        text.gameObject.SetActive(false);
        return text;
    }

    /// <summary>
    /// Authors a reusable battle-result item template.
    /// </summary>
    /// <param name="parent">The result-list transform.</param>
    /// <param name="name">The template object name.</param>
    /// <param name="itemRect">The item source-space rectangle.</param>
    /// <param name="imageRect">The result-image source-space rectangle.</param>
    /// <param name="nameRect">The result-name source-space rectangle.</param>
    /// <param name="emptyRect">The empty-state source-space rectangle.</param>
    /// <returns>The configured battle-result item template.</returns>
    private static BattleResultItemView CreateBattleResultItemTemplate(
        Transform parent,
        string name,
        RectInt itemRect,
        RectInt imageRect,
        RectInt nameRect,
        RectInt emptyRect
    )
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(BattleResultItemView));
        item.transform.SetParent(parent, false);
        SetSourceRect(
            item.GetComponent<RectTransform>(),
            itemRect.x,
            itemRect.y,
            itemRect.width,
            itemRect.height
        );
        RectTransform imageSlot = CreateSourceRectLayer(
            "ImageSlot",
            item.transform,
            imageRect.width,
            imageRect.height
        );
        SetSourceRect(imageSlot, imageRect.x, imageRect.y, imageRect.width, imageRect.height);
        RawImage baseImage = CreateRawImage("BaseImage", item.transform, null, 0, 0);
        SetSourceRect(
            baseImage.rectTransform,
            imageRect.x,
            imageRect.y,
            imageRect.width,
            imageRect.height
        );
        RawImage withdrawingOverlay = CreateRawImage(
            "WithdrawingOverlayImage",
            item.transform,
            null,
            0,
            0
        );
        SetSourceRect(
            withdrawingOverlay.rectTransform,
            imageRect.x,
            imageRect.y,
            imageRect.width,
            imageRect.height
        );
        RawImage damagedOverlay = CreateRawImage("DamagedOverlayImage", item.transform, null, 0, 0);
        SetSourceRect(
            damagedOverlay.rectTransform,
            imageRect.x,
            imageRect.y,
            imageRect.width,
            imageRect.height
        );
        RawImage capturedOverlay = CreateRawImage(
            "CapturedOverlayImage",
            item.transform,
            null,
            0,
            0
        );
        SetSourceRect(
            capturedOverlay.rectTransform,
            imageRect.x,
            imageRect.y,
            imageRect.width,
            imageRect.height
        );

        TextMeshProUGUI nameText = CreateTextLabel("NameTextField", item.transform);
        nameText.text = "Corellian Corvette";
        nameText.color = Color.white;
        nameText.fontSize = 14;
        nameText.fontStyle = FontStyles.Normal;
        nameText.alignment = TextAlignmentOptions.Top;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Overflow;
        nameText.GetComponent<Shadow>().enabled = false;
        SetSourceRect(
            nameText.rectTransform,
            nameRect.x,
            nameRect.y,
            nameRect.width,
            nameRect.height
        );
        TextMeshProUGUI emptyText = CreateTextLabel("EmptyTextField", item.transform);
        emptyText.text = "None";
        emptyText.color = Color.white;
        emptyText.fontSize = 18;
        emptyText.fontStyle = FontStyles.Normal;
        emptyText.alignment = TextAlignmentOptions.Center;
        emptyText.textWrappingMode = TextWrappingModes.NoWrap;
        emptyText.overflowMode = TextOverflowModes.Overflow;
        emptyText.GetComponent<Shadow>().enabled = false;
        SetSourceRect(
            emptyText.rectTransform,
            emptyRect.x,
            emptyRect.y,
            emptyRect.width,
            emptyRect.height
        );

        BattleResultItemView view = EnableRuntimeComponent(
            item.GetComponent<BattleResultItemView>()
        );
        AssignReference(view, "imageSlot", imageSlot);
        AssignReference(view, "baseImage", baseImage);
        AssignReference(view, "withdrawingOverlayImage", withdrawingOverlay);
        AssignReference(view, "damagedOverlayImage", damagedOverlay);
        AssignReference(view, "capturedOverlayImage", capturedOverlay);
        AssignReference(view, "nameTextField", nameText);
        AssignReference(view, "emptyTextField", emptyText);
        AddTemplateLayoutElement(item.GetComponent<RectTransform>());
        item.SetActive(false);
        return view;
    }

    /// <summary>
    /// Instantiates and configures a nested shared scroll-area prefab.
    /// </summary>
    /// <param name="parent">The generated parent transform.</param>
    /// <param name="name">The instance name.</param>
    /// <param name="x">The source-space root x-coordinate.</param>
    /// <param name="y">The source-space root y-coordinate.</param>
    /// <param name="width">The source-space root width.</param>
    /// <param name="height">The source-space root height.</param>
    /// <param name="viewportX">The source-space viewport x-coordinate.</param>
    /// <param name="viewportY">The source-space viewport y-coordinate.</param>
    /// <param name="viewportWidth">The source-space viewport width.</param>
    /// <param name="viewportHeight">The source-space viewport height.</param>
    /// <param name="scrollbarX">The source-space scrollbar x-coordinate.</param>
    /// <param name="scrollbarY">The source-space scrollbar y-coordinate.</param>
    /// <param name="scrollbarWidth">The source-space scrollbar width.</param>
    /// <param name="scrollbarHeight">The source-space scrollbar height.</param>
    /// <param name="contentRoot">The configured content root.</param>
    /// <returns>The nested shared scroll-area view.</returns>
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
        ScrollAreaView view = InstantiatePrefabComponent<ScrollAreaView>(
            _commonScrollAreaPrefabPath,
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
        RectTransform scrollbarRect = scrollbar.transform as RectTransform;
        SetSourceRect(scrollbarRect, scrollbarX, scrollbarY, scrollbarWidth, scrollbarHeight);

        Texture2D scrollUpTexture = LoadTexture(_scrollUpArrowPreviewPath);
        Texture2D scrollDownTexture = LoadTexture(_scrollDownArrowPreviewPath);
        int upArrowHeight = GetTextureSourceHeightOrDefault(scrollUpTexture, 9);
        int downArrowHeight = GetTextureSourceHeightOrDefault(scrollDownTexture, 9);
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
        SetSourceRect(scrollUpImage.rectTransform, 0, 0, scrollbarWidth, upArrowHeight);
        scrollUpImage.raycastTarget = true;
        Button scrollUpButton = scrollUpImage.GetComponent<Button>();
        scrollUpButton.targetGraphic = scrollUpImage;
        scrollUpButton.transition = Selectable.Transition.None;

        RawImage scrollDownImage = FindRequiredChild<RawImage>(
            scrollbar.transform,
            "ScrollDownButtonImage"
        );
        scrollDownImage.texture = scrollDownTexture;
        SetSourceRect(
            scrollDownImage.rectTransform,
            0,
            scrollbarHeight - downArrowHeight,
            scrollbarWidth,
            downArrowHeight
        );
        scrollDownImage.raycastTarget = true;
        Button scrollDownButton = scrollDownImage.GetComponent<Button>();
        scrollDownButton.targetGraphic = scrollDownImage;
        scrollDownButton.transition = Selectable.Transition.None;

        RectTransform slidingArea = FindRequiredChild<RectTransform>(
            scrollbar.transform,
            "SlidingArea"
        );
        SetSourceRect(slidingArea, 0, upArrowHeight, scrollbarWidth, trackHeight);
        RawImage handleImage = FindRequiredChild<RawImage>(slidingArea, "Handle");
        handleImage.texture = LoadTexture(_scrollBarMiddlePreviewPath);
        FillParent(handleImage.rectTransform);
        handleImage.raycastTarget = true;
        scrollbar.handleRect = handleImage.rectTransform;
        handleImage.rectTransform.anchoredPosition = Vector2.zero;
        handleImage.rectTransform.sizeDelta = Vector2.zero;
        scrollbar.targetGraphic = handleImage;
        scrollbar.transition = Selectable.Transition.None;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        root.SetActive(true);
        return view;
    }

    /// <summary>
    /// Adds fixed vertical-list layout behavior to generated content.
    /// </summary>
    /// <param name="contentRoot">The content transform.</param>
    /// <param name="spacing">The source-space spacing between children.</param>
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

    /// <summary>
    /// Adds a fixed-column grid layout to generated content.
    /// </summary>
    /// <param name="contentRoot">The content transform.</param>
    /// <param name="cellWidth">The source-space cell width.</param>
    /// <param name="cellHeight">The source-space cell height.</param>
    /// <param name="columns">The fixed column count.</param>
    /// <returns>The configured grid layout.</returns>
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

    /// <summary>
    /// Adds fixed preferred dimensions for a generated list template.
    /// </summary>
    /// <param name="template">The template component.</param>
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

    /// <summary>
    /// Authors the reusable planet-system cluster prefab.
    /// </summary>
    /// <returns>The generated planet-system cluster view.</returns>
    private static PlanetSystemClusterView BuildPlanetSystemClusterPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_planetSystemClusterPrefabPath));

        GameObject root = new GameObject("PlanetSystemCluster", typeof(RectTransform));
        PlanetSystemClusterView view = EnableRuntimeComponent(
            root.AddComponent<PlanetSystemClusterView>()
        );
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

    /// <summary>
    /// Installs the generated Strategy root beneath the authored scene host.
    /// </summary>
    [MenuItem("Rebellion/Strategy View/Install Strategy View Root Prefab In Scene")]
    public static void InstallStrategyViewRootPrefabInScene()
    {
        UIAuthoringGuard.EnsureEditMode();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath) == null)
            BuildStrategyViewRootPrefab();

        SceneRootPrefabInstaller.InstallRootPrefabInScene(
            _strategyScenePath,
            _prefabPath,
            _sceneInstanceName,
            _strategySceneRootParentPath
        );
    }

    /// <summary>
    /// Creates one RectTransform layer beneath a generated parent.
    /// </summary>
    /// <param name="name">The layer name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <returns>The generated layer.</returns>
    private static GameObject CreateLayer(string name, Transform parent)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(parent, false);
        return layer;
    }

    /// <summary>
    /// Creates one child layer stretched across its parent.
    /// </summary>
    /// <param name="name">The layer name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <returns>The stretched layer transform.</returns>
    private static RectTransform CreateChildLayer(string name, Transform parent)
    {
        GameObject layer = CreateLayer(name, parent);
        RectTransform rect = layer.GetComponent<RectTransform>();
        FillParent(rect);
        return rect;
    }

    /// <summary>
    /// Creates one top-left source-space layer with fixed dimensions.
    /// </summary>
    /// <param name="name">The layer name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The configured layer transform.</returns>
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

    /// <summary>
    /// Saves a generated hierarchy as a prefab asset without changing authored component state.
    /// </summary>
    /// <param name="root">The generated hierarchy root.</param>
    /// <param name="path">The destination prefab asset path.</param>
    /// <returns>The saved prefab asset root.</returns>
    private static GameObject SaveGeneratedPrefabAsset(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success || saved == null)
            throw new System.InvalidOperationException(
                $"Failed to save generated prefab at {path}."
            );
        return saved;
    }

    /// <summary>
    /// Authors the Strategy overlay and its targeting input surface.
    /// </summary>
    /// <param name="parent">The Strategy root transform.</param>
    /// <returns>The configured Strategy overlay view.</returns>
    private static StrategyOverlayView CreateStrategyOverlayView(Transform parent)
    {
        GameObject overlay = CreateLayer(_overlayLayerName, parent);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        SetStrategySurfaceRect(overlayRect);
        StrategyOverlayView view = EnableRuntimeComponent(
            overlay.AddComponent<StrategyOverlayView>()
        );
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

    /// <summary>
    /// Authors the galactic-information selector and its nested submenu prefabs.
    /// </summary>
    /// <param name="parent">The Strategy root transform.</param>
    /// <returns>The configured galactic-information display view.</returns>
    private static GalacticInformationDisplayView CreateGalacticInformationDisplayView(
        Transform parent
    )
    {
        GalacticInformationDisplayTheme theme = PreviewTheme?.GalacticInformationDisplay;
        if (theme == null)
            throw new MissingReferenceException(
                "Preview GalacticInformationDisplay theme is missing."
            );

        GameObject root = CreateLayer("GalacticInformationDisplay", parent);
        root.SetActive(false);
        SetStrategySurfaceRect(root.GetComponent<RectTransform>());
        GalacticInformationDisplayView view = EnableRuntimeComponent(
            root.AddComponent<GalacticInformationDisplayView>()
        );
        UIRaycastArea dismissHitArea = CreateStretchRaycastArea("DismissHitArea", root.transform);

        GameObject selector = CreateLayer("SelectorPanel", root.transform);
        RectTransform selectorRect = selector.GetComponent<RectTransform>();
        SetSourceRect(
            selectorRect,
            theme.SelectorSourceLayout.X,
            theme.SelectorSourceLayout.Y,
            theme.SelectorSourceLayout.Width,
            theme.SelectorSourceLayout.Height
        );
        Image background = CreateImage("BackgroundImage", selector.transform);
        background.color = theme.GetBackgroundColor();
        FillParent(background.rectTransform);
        GalacticInformationFrameView frame = CreateGalacticInformationFrameView(selector.transform);

        List<RawImage> categoryIcons = new List<RawImage>();
        List<RawImage> categoryArrows = new List<RawImage>();
        List<TextMeshProUGUI> categoryLabels = new List<TextMeshProUGUI>();
        List<UIRaycastArea> categoryHitAreas = new List<UIRaycastArea>();
        List<GalacticInformationSubmenuView> submenus = new List<GalacticInformationSubmenuView>();
        for (int i = 0; i < theme.Categories.Count; i++)
        {
            GalacticInformationCategoryTheme category = theme.Categories[i];
            string categoryName = category.Label.Replace(" ", string.Empty);
            RawImage arrow = CreateRawImage(
                $"{categoryName}CategoryArrowImage",
                selector.transform,
                theme.SubmenuArrowInactiveImagePath,
                0,
                0
            );
            RawImage icon = CreateRawImage(
                $"{categoryName}CategoryIconImage",
                selector.transform,
                category.IconImagePath,
                0,
                0
            );
            TextMeshProUGUI label = CreateGalacticInformationLabel(
                $"{categoryName}CategoryTextField",
                selector.transform,
                category.Label
            );
            UIRaycastArea hitArea = CreateHudButtonView(
                $"{categoryName}CategoryHitArea",
                selector.transform,
                category.RowSourceLayout
            );
            GalacticInformationSubmenuView submenu = CreateGalacticInformationSubmenuView(
                selector.transform,
                theme,
                category,
                categoryName
            );
            categoryArrows.Add(arrow);
            categoryIcons.Add(icon);
            categoryLabels.Add(label);
            categoryHitAreas.Add(hitArea);
            submenus.Add(submenu);
        }

        TextMeshProUGUI displayOffText = CreateGalacticInformationLabel(
            "DisplayOffText",
            selector.transform,
            theme.DisplayOffLabel
        );
        UIRaycastArea displayOffHitArea = CreateHudButtonView(
            "DisplayOffHitArea",
            selector.transform,
            theme.DisplayOffRowSourceLayout
        );

        AssignReference(view, "dismissHitArea", dismissHitArea);
        AssignReference(view, "selectorPanel", selectorRect);
        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "frameView", frame);
        AssignReferenceArray(view, "categoryIconImages", categoryIcons);
        AssignReferenceArray(view, "categoryArrowImages", categoryArrows);
        AssignReferenceArray(view, "categoryTextFields", categoryLabels);
        AssignReferenceArray(view, "categoryHitAreas", categoryHitAreas);
        AssignReferenceArray(view, "submenuViews", submenus);
        AssignReference(view, "displayOffTextField", displayOffText);
        AssignReference(view, "displayOffHitArea", displayOffHitArea);
        root.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors one galactic-information category submenu.
    /// </summary>
    /// <param name="parent">The display host transform.</param>
    /// <param name="theme">The complete display theme.</param>
    /// <param name="category">The category theme.</param>
    /// <param name="categoryName">The category hierarchy name.</param>
    /// <returns>The configured submenu view.</returns>
    private static GalacticInformationSubmenuView CreateGalacticInformationSubmenuView(
        Transform parent,
        GalacticInformationDisplayTheme theme,
        GalacticInformationCategoryTheme category,
        string categoryName
    )
    {
        GameObject root = CreateLayer($"{categoryName}Submenu", parent);
        GalacticInformationSubmenuView view = EnableRuntimeComponent(
            root.AddComponent<GalacticInformationSubmenuView>()
        );
        SourceRectLayout layout = category.SubmenuSourceLayout;
        SetSourceRect(
            root.GetComponent<RectTransform>(),
            layout.X,
            layout.Y,
            layout.Width,
            layout.Height
        );
        Image background = CreateImage("BackgroundImage", root.transform);
        background.color = theme.GetBackgroundColor();
        FillParent(background.rectTransform);
        GalacticInformationFrameView frame = CreateGalacticInformationFrameView(root.transform);
        List<RawImage> icons = new List<RawImage>();
        List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        List<UIRaycastArea> hitAreas = new List<UIRaycastArea>();
        for (int i = 0; i < category.Filters.Count; i++)
        {
            GalacticInformationFilterTheme filter = category.Filters[i];
            icons.Add(
                CreateRawImage(
                    $"{filter.Mode}FilterIconImage",
                    root.transform,
                    filter.IconImagePath,
                    0,
                    0
                )
            );
            labels.Add(
                CreateGalacticInformationLabel(
                    $"{filter.Mode}FilterTextField",
                    root.transform,
                    filter.Label
                )
            );
            hitAreas.Add(
                CreateHudButtonView(
                    $"{filter.Mode}FilterHitArea",
                    root.transform,
                    filter.RowSourceLayout
                )
            );
        }

        AssignReference(view, "backgroundImage", background);
        AssignReference(view, "frameView", frame);
        AssignReferenceArray(view, "iconImages", icons);
        AssignReferenceArray(view, "textFields", labels);
        AssignReferenceArray(view, "hitAreas", hitAreas);
        root.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors the galactic-information display legend.
    /// </summary>
    /// <param name="parent">The display host transform.</param>
    /// <returns>The configured legend view.</returns>
    private static GalacticInformationLegendView CreateGalacticInformationLegendView(
        Transform parent
    )
    {
        GalacticInformationDisplayTheme theme = PreviewTheme?.GalacticInformationDisplay;
        GalacticInformationFilterTheme filter = theme?.GetFilter(
            GalacticInformationFilterMode.PopularSupport
        );
        Texture2D legendTexture = LoadTexture(filter?.LegendImagePath);
        if (
            theme?.LegendSourcePosition == null
            || theme.CloseSourceInset == null
            || legendTexture == null
        )
            throw new MissingReferenceException(
                "Preview GalacticInformationDisplay legend is missing."
            );

        Vector2Int legendSize = UILayout.GetTextureSourceSize(legendTexture);
        GameObject root = CreateLayer("GalacticInformationLegend", parent);
        root.SetActive(false);
        GalacticInformationLegendView view = EnableRuntimeComponent(
            root.AddComponent<GalacticInformationLegendView>()
        );
        SetSourceRect(
            root.GetComponent<RectTransform>(),
            theme.LegendSourcePosition.X,
            theme.LegendSourcePosition.Y,
            legendSize.x,
            legendSize.y
        );
        RawImage legend = CreateRawImage(
            "LegendImage",
            root.transform,
            filter.LegendImagePath,
            0,
            0,
            legendSize.x,
            legendSize.y
        );
        GalacticInformationFrameView frame = CreateGalacticInformationFrameView(root.transform);
        RawImage close = CreateRawImage("CloseImage", root.transform, theme.CloseUpImagePath, 0, 0);
        Vector2Int closeSize = UILayout.GetTextureSourceSize(close.texture);
        SourceRectLayout closeLayout = new SourceRectLayout
        {
            X = legendSize.x - closeSize.x - theme.CloseSourceInset.X,
            Y = theme.CloseSourceInset.Y,
            Width = closeSize.x,
            Height = closeSize.y,
        };
        SetSourceRect(
            close.rectTransform,
            closeLayout.X,
            closeLayout.Y,
            closeLayout.Width,
            closeLayout.Height
        );
        UIRaycastArea closeHitArea = CreateHudButtonView(
            "CloseHitArea",
            root.transform,
            closeLayout
        );

        AssignReference(view, "legendImage", legend);
        AssignReference(view, "frameView", frame);
        AssignReference(view, "closeImage", close);
        AssignReference(view, "closeHitArea", closeHitArea);
        root.SetActive(false);
        return view;
    }

    /// <summary>
    /// Authors the galactic-information display frame.
    /// </summary>
    /// <param name="parent">The display host transform.</param>
    /// <returns>The configured frame view.</returns>
    private static GalacticInformationFrameView CreateGalacticInformationFrameView(Transform parent)
    {
        string[] names =
        {
            "TopLeftImage",
            "TopRightImage",
            "BottomLeftImage",
            "BottomRightImage",
            "TopImage",
            "LeftImage",
            "RightImage",
            "BottomImage",
        };
        GameObject root = CreateLayer("Frame", parent);
        FillParent(root.GetComponent<RectTransform>());
        GalacticInformationFrameView view = EnableRuntimeComponent(
            root.AddComponent<GalacticInformationFrameView>()
        );
        List<RawImage> images = new List<RawImage>();
        for (int i = 0; i < names.Length; i++)
            images.Add(CreateRawImage(names[i], root.transform, null, 0, 0));

        AssignReferenceArray(view, "frameImages", images);
        return view;
    }

    /// <summary>
    /// Authors one galactic-information display label.
    /// </summary>
    /// <param name="name">The label object name.</param>
    /// <param name="parent">The owning display transform.</param>
    /// <param name="value">The preview text.</param>
    /// <returns>The configured label.</returns>
    private static TextMeshProUGUI CreateGalacticInformationLabel(
        string name,
        Transform parent,
        string value
    )
    {
        TextMeshProUGUI label = CreateTextLabel(name, parent);
        label.text = value ?? string.Empty;
        label.color = Color.white;
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        SetSourceRect(label.rectTransform, 0, 0, 1, 1);
        return label;
    }

    /// <summary>
    /// Authors a transparent raycast area stretched across its parent.
    /// </summary>
    /// <param name="name">The raycast-area object name.</param>
    /// <param name="parent">The owning transform.</param>
    /// <returns>The configured raycast area.</returns>
    private static UIRaycastArea CreateStretchRaycastArea(string name, Transform parent)
    {
        GameObject root = CreateLayer(name, parent);
        FillParent(root.GetComponent<RectTransform>());
        RawImage target = CreatePanelImage(
            "RaycastTargetImage",
            root.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        FillParent(target.rectTransform);
        target.raycastTarget = true;
        UIRaycastArea area = EnableRuntimeComponent(root.AddComponent<UIRaycastArea>());
        AssignReference(area, "raycastTargetImage", target);
        return area;
    }

    /// <summary>
    /// Authors the complete Strategy context-menu hierarchy.
    /// </summary>
    /// <param name="parent">The Strategy root transform.</param>
    /// <returns>The configured context-menu presenter.</returns>
    private static StrategyContextMenuPresenter CreateContextMenu(Transform parent)
    {
        GameObject contextMenu = CreateLayer(_contextMenuName, parent);
        SetStrategySurfaceRect(contextMenu.GetComponent<RectTransform>());
        ContextMenuView contextMenuView = EnableRuntimeComponent(
            contextMenu.AddComponent<ContextMenuView>()
        );
        StrategyContextMenuPresenter presenter = EnableRuntimeComponent(
            contextMenu.AddComponent<StrategyContextMenuPresenter>()
        );

        RawImage dismissHitArea = CreatePanelImage(
            "DismissHitAreaImage",
            contextMenu.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        FillParent(dismissHitArea.rectTransform);
        dismissHitArea.raycastTarget = true;
        dismissHitArea.canvasRenderer.cullTransparentMesh = false;
        ContextMenuDismissBoundary dismissBoundary = EnableRuntimeComponent(
            dismissHitArea.gameObject.AddComponent<ContextMenuDismissBoundary>()
        );

        GameObject panel = CreateLayer(_contextMenuPanelTemplateName, contextMenu.transform);
        ContextMenuPanelView panelView = EnableRuntimeComponent(
            panel.AddComponent<ContextMenuPanelView>()
        );
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
        ContextMenuCommandView commandView = EnableRuntimeComponent(
            command.AddComponent<ContextMenuCommandView>()
        );
        SetSourceRect(command.GetComponent<RectTransform>(), 0, 0, 120, _contextMenuCommandHeight);

        RawImage commandHitArea = CreatePanelImage(
            "HitAreaImage",
            command.transform,
            new Color(1f, 1f, 1f, 0f)
        );
        SetSourceRect(commandHitArea.rectTransform, 0, 0, 120, _contextMenuCommandHeight);
        commandHitArea.raycastTarget = true;
        RectTransform iconPanel = CreateSourceRectLayer(
            "IconPanel",
            command.transform,
            _contextMenuIconPanelWidth,
            _contextMenuCommandHeight
        );
        RawImage iconImage = CreateRawButton("IconImage", iconPanel);
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

        AssignReference(presenter, "contextMenuView", contextMenuView);
        AssignReference(contextMenuView, "panelPrefab", panelView);
        AssignReference(contextMenuView, "dismissHitAreaImage", dismissHitArea);
        AssignReference(contextMenuView, "dismissBoundary", dismissBoundary);
        AssignColor(contextMenuView, "_enabledCommandColor", Color.white);
        AssignColor(contextMenuView, "_disabledCommandColor", new Color32(128, 128, 128, 255));
        AssignReference(panelView, "borderTopImage", borderTop);
        AssignReference(panelView, "borderBottomImage", borderBottom);
        AssignReference(panelView, "borderLeftImage", borderLeft);
        AssignReference(panelView, "borderRightImage", borderRight);
        AssignReference(panelView, "commandPrefab", commandView);
        AssignReference(commandView, "hitAreaImage", commandHitArea);
        AssignReference(commandView, "iconPanel", iconPanel);
        AssignReference(commandView, "iconImage", iconImage);
        AssignReference(commandView, "commandTextField", commandTextField);
        AssignInt(presenter, "speedMenuWidth", _speedContextMenuWidth);
        AssignInt(presenter, "facilityMenuWidth", _facilityContextMenuWidth);
        AssignInt(presenter, "fleetMenuWidth", _fleetContextMenuWidth);
        AssignInt(presenter, "fleetBombardmentMenuWidth", _fleetBombardmentContextMenuWidth);
        AssignInt(presenter, "planetSystemMenuWidth", _planetSystemContextMenuWidth);
        AssignInt(presenter, "defenseMenuWidth", _defenseContextMenuWidth);
        AssignInt(presenter, "missionsMenuWidth", _missionsContextMenuWidth);
        AssignInt(presenter, "fallbackMenuWidth", _fallbackContextMenuWidth);
        command.SetActive(false);
        panel.SetActive(false);
        dismissHitArea.gameObject.SetActive(false);
        return presenter;
    }

    /// <summary>
    /// Authors one themed HUD text field.
    /// </summary>
    /// <param name="name">The label object name.</param>
    /// <param name="text">The preview text.</param>
    /// <param name="parent">The HUD text container.</param>
    /// <param name="layout">The authored source-space layout.</param>
    /// <param name="alignment">The text alignment.</param>
    /// <returns>The configured HUD label.</returns>
    private static TextMeshProUGUI CreateHudLabel(
        string name,
        string text,
        Transform parent,
        SourceRectLayout layout,
        TextAlignmentOptions alignment
    )
    {
        if (layout == null)
            throw new MissingReferenceException($"{name} layout is missing.");

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
        rect.anchoredPosition = new Vector2(layout.X, -layout.Y);
        rect.sizeDelta = new Vector2(layout.Width, layout.Height);
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

    /// <summary>
    /// Authors the reusable planet-system planet prefab.
    /// </summary>
    /// <returns>The generated planet-system planet view.</returns>
    private static PlanetSystemPlanetView BuildPlanetSystemPlanetPrefab()
    {
        GameObject root = new GameObject("PlanetSystemPlanet", typeof(RectTransform));
        root.SetActive(false);
        PlanetSystemPlanetView planetView = EnableRuntimeComponent(
            root.AddComponent<PlanetSystemPlanetView>()
        );
        const int rootWidth = _planetPreviewWidth + 19;
        const int rootHeight = 98;
        SetSourceRect(root.GetComponent<RectTransform>(), 0, 0, rootWidth, rootHeight);

        RawImage hitArea = CreateRawButton("HitAreaImage", root.transform);
        hitArea.color = Color.clear;
        hitArea.raycastTarget = false;
        hitArea.canvasRenderer.cullTransparentMesh = false;
        UILayout.SetStretch(hitArea.rectTransform);
        RawImage planet = CreateRawImage(
            "PlanetImage",
            root.transform,
            _planetPreviewPath,
            10,
            1,
            _planetSystemPlanetImageWidth,
            _planetSystemPlanetImageHeight
        );
        RawImage uprising = CreateRawImage(
            "UprisingImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetSystemUprisingImagePath,
            10,
            1,
            _planetSystemPlanetImageWidth,
            _planetSystemPlanetImageHeight
        );
        uprising.raycastTarget = false;
        RawImage facility = CreateRawImage(
            "FacilityImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Buildings?.NormalImagePath,
            0,
            0,
            _planetSystemFacilityIconWidth,
            _planetSystemFacilityIconHeight
        );
        RawImage defense = CreateRawImage(
            "DefenseImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Defenses?.NormalImagePath,
            0,
            20,
            _planetSystemDefenseIconWidth,
            _planetSystemDefenseIconHeight
        );
        RawImage fleet = CreateRawImage(
            "FleetImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Fleets?.NormalImagePath,
            29,
            0,
            _planetSystemFleetIconWidth,
            _planetSystemFleetIconHeight
        );
        RawImage mission = CreateRawImage(
            "MissionImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetOverlayIcons?.Missions?.NormalImagePath,
            28,
            20,
            _planetSystemMissionIconWidth,
            _planetSystemMissionIconHeight
        );
        RawImage headquarters = CreateRawImage(
            "HeadquartersImage",
            root.transform,
            PreviewTheme?.PlanetOverlayTheme?.PlanetSystemHeadquartersImagePath,
            10,
            1,
            _planetSystemPlanetImageWidth,
            _planetSystemPlanetImageHeight
        );
        int segmentedBarCellCount = GetPlanetSystemSegmentedBarCellCount();
        BarPrefabParts energyBar = CreateBar(
            "EnergyBar",
            root.transform,
            39,
            segmentedBarCellCount,
            Color.white,
            Color.blue
        );
        BarPrefabParts rawBar = CreateBar(
            "RawBar",
            root.transform,
            43,
            segmentedBarCellCount,
            Color.yellow,
            Orange()
        );
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
        AssignReference(planetView, "uprisingImage", uprising);
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

        root.SetActive(true);
        GameObject saved = SaveGeneratedPrefabAsset(root, _planetSystemPlanetPrefabPath);
        Object.DestroyImmediate(root);
        return saved.GetComponent<PlanetSystemPlanetView>();
    }

    /// <summary>
    /// Authors the Planet System window prefab around its nested planet prefab.
    /// </summary>
    /// <param name="planetPrefab">The reusable planet view prefab.</param>
    /// <returns>The generated Planet System window view.</returns>
    private static PlanetSystemWindowView BuildPlanetSystemWindowPrefab(
        PlanetSystemPlanetView planetPrefab
    )
    {
        GameObject window = CreatePlanetSystemWindowObject("PlanetSystemWindow", planetPrefab);
        GameObject saved = SaveGeneratedPrefabAsset(window, _planetSystemWindowPrefabPath);
        Object.DestroyImmediate(window);
        return saved.GetComponent<PlanetSystemWindowView>();
    }

    /// <summary>
    /// Authors a Planet System window hierarchy before prefab serialization.
    /// </summary>
    /// <param name="name">The window object name.</param>
    /// <param name="planetPrefab">The reusable planet view prefab.</param>
    /// <returns>The generated window hierarchy root.</returns>
    private static GameObject CreatePlanetSystemWindowObject(
        string name,
        PlanetSystemPlanetView planetPrefab
    )
    {
        const int windowWidth = 226;
        const int windowHeight = 349;
        GameObject window = new GameObject(name, typeof(RectTransform), typeof(UIWindow));
        window.SetActive(false);
        PlanetSystemWindowView view = EnableRuntimeComponent(
            window.AddComponent<PlanetSystemWindowView>()
        );
        RectTransform rect = window.GetComponent<RectTransform>();
        SetSourceRect(rect, 59, 36, windowWidth, windowHeight);

        RawImage dim = CreatePanelImage(
            "DimPanelImage",
            window.transform,
            _sectorWindowBackgroundOverlay
        );
        dim.raycastTarget = true;
        UILayout.SetStretch(dim.rectTransform);
        RawImage borderTop = CreatePanelImage("BorderTopImage", window.transform, Color.white);
        UILayout.SetTopStretchRect(borderTop.rectTransform, 0, 0, 0, 1);
        RawImage borderBottom = CreatePanelImage(
            "BorderBottomImage",
            window.transform,
            Color.white
        );
        UILayout.SetBottomStretchRect(borderBottom.rectTransform, 0, 0, 0, 1);
        RawImage borderLeft = CreatePanelImage("BorderLeftImage", window.transform, Color.white);
        UILayout.SetLeftStretchRect(borderLeft.rectTransform, 0, 0, 0, 1);
        RawImage borderRight = CreatePanelImage("BorderRightImage", window.transform, Color.white);
        UILayout.SetRightStretchRect(borderRight.rectTransform, 0, 0, 0, 1);
        TextMeshProUGUI systemNameTextField = CreateTextLabel(
            "SystemNameTextField",
            window.transform
        );
        systemNameTextField.text = "SystemName";
        systemNameTextField.color = new Color32(231, 243, 83, 255);
        systemNameTextField.fontSize = 13;
        systemNameTextField.alignment = TextAlignmentOptions.Top;
        UILayout.SetTopStretchRect(systemNameTextField.rectTransform, 0, 5, 0, 13);
        RawImage swapButton = CreateRawButton(
            "SwapButtonImage",
            window.transform,
            _windowSwapPreviewPath
        );
        UILayout.SetTopRightRect(swapButton.rectTransform, 17, 3, 14, 14);
        RawImage closeButton = CreateRawButton(
            "CloseButtonImage",
            window.transform,
            _windowClosePreviewPath
        );
        UILayout.SetTopRightRect(closeButton.rectTransform, 3, 3, 14, 14);
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
        UILayout.SetStretch(planets.GetComponent<RectTransform>());

        AssignReference(view, "systemNameTextField", systemNameTextField);
        AssignReference(view, "planetsRoot", planets.GetComponent<RectTransform>());
        AssignReference(view, "planetPrefab", planetPrefab);
        AssignFloat(view, "sectorCoordinateRange", _sectorCoordinateRange);
        AssignFloat(view, "sectorCoordinateScaleX", _sectorCoordinateScaleX);
        AssignFloat(view, "sectorCoordinateScaleY", _sectorCoordinateScaleY);
        AssignFloat(view, "galaxyProjectionSourceRange", _galaxyProjectionSourceRange);
        AssignFloat(view, "galaxyProjectionWidth", _galaxyProjectionWidth);
        AssignFloat(view, "galaxyProjectionHeight", _galaxyProjectionHeight);
        AssignInt(view, "planetPositionOffsetY", -1);
        return window;
    }

    /// <summary>
    /// Authors one solid-color RawImage panel.
    /// </summary>
    /// <param name="name">The panel object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="color">The panel color.</param>
    /// <returns>The configured panel image.</returns>
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

    /// <summary>
    /// Authors one RawImage slot with an optional initial texture.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="texturePath">The optional texture asset path.</param>
    /// <returns>The configured RawImage.</returns>
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
        {
            Vector2Int size = UILayout.GetTextureSourceSize(image.texture);
            SetSourceRect(image.rectTransform, 0, 0, size.x, size.y);
        }
        return image;
    }

    /// <summary>
    /// Authors one RawImage at a source-space position using its texture dimensions.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="texturePath">The optional texture asset path.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <returns>The configured RawImage.</returns>
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
        int width = GetTextureSourceWidthOrDefault(texture, 14);
        int height = GetTextureSourceHeightOrDefault(texture, 14);
        SetSourceRect(image.rectTransform, x, y, width, height);
        return image;
    }

    /// <summary>
    /// Authors one RawImage with an explicit source-space rectangle.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="texturePath">The optional texture asset path.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The configured RawImage.</returns>
    private static RawImage CreateRawImage(
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

    /// <summary>
    /// Authors one RawImage from a complete source-space layout.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="texturePath">The optional texture asset path.</param>
    /// <param name="layout">The required source-space layout.</param>
    /// <returns>The configured RawImage.</returns>
    private static RawImage CreateRawImage(
        string name,
        Transform parent,
        string texturePath,
        SourceRectLayout layout
    )
    {
        if (layout == null)
            throw new MissingReferenceException($"{name} layout is missing.");

        return CreateRawImage(
            name,
            parent,
            texturePath,
            layout.X,
            layout.Y,
            layout.Width,
            layout.Height
        );
    }

    /// <summary>
    /// Authors a button and explicitly wired pressed-state visual on a RawImage.
    /// </summary>
    /// <param name="image">The button target image.</param>
    /// <returns>The configured button.</returns>
    private static Button CreateButton(RawImage image)
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
        return button;
    }

    /// <summary>
    /// Authors one button for each RawImage in stable input order.
    /// </summary>
    /// <param name="images">The ordered target images.</param>
    /// <returns>The authored buttons.</returns>
    private static List<Button> CreateButtons(IReadOnlyList<RawImage> images)
    {
        List<Button> buttons = new List<Button>();
        for (int i = 0; i < images.Count; i++)
            buttons.Add(CreateButton(images[i]));

        return buttons;
    }

    /// <summary>
    /// Authors a draggable window title image.
    /// </summary>
    /// <param name="window">The owning window.</param>
    /// <param name="windowWidth">The authored window width.</param>
    /// <returns>The configured title image.</returns>
    private static RawImage CreateWindowTitleImage(UIWindow window, int windowWidth)
    {
        RawImage title = CreateRawImage(
            "TitleImage",
            window.transform,
            PreviewTheme?.WindowTitleTheme?.ActiveImagePath,
            2,
            2
        );
        int height = GetTextureSourceHeightOrDefault(title.texture, 14);
        SetSourceRect(title.rectTransform, 2, 2, windowWidth - 4, height);
        title.raycastTarget = true;
        ConfigureWindowDragHandle(title.gameObject, window);
        return title;
    }

    /// <summary>
    /// Adds a drag handle with an explicit reference to its authored owning window.
    /// </summary>
    /// <param name="handleObject">The generated drag-surface GameObject.</param>
    /// <param name="window">The authored owning window.</param>
    private static void ConfigureWindowDragHandle(GameObject handleObject, UIWindow window)
    {
        if (window == null)
            throw new MissingReferenceException($"{handleObject.name} has no owning UIWindow.");
        if (!handleObject.transform.IsChildOf(window.transform))
            throw new System.InvalidOperationException(
                $"{handleObject.name} is not a child of {window.name}."
            );

        UIWindowDragHandle dragHandle = EnableRuntimeComponent(
            handleObject.AddComponent<UIWindowDragHandle>()
        );
        AssignReference(dragHandle, "window", window);
    }

    /// <summary>
    /// Authors a UIWindow's ordered action-button mapping.
    /// </summary>
    /// <param name="window">The generated window shell.</param>
    /// <param name="images">The ordered command images.</param>
    /// <param name="actions">The ordered command identifiers.</param>
    private static void ConfigureWindowButtons(
        UIWindow window,
        IReadOnlyList<RawImage> images,
        IReadOnlyList<int> actions
    )
    {
        if (window == null || images == null || actions == null)
            return;

        ConfigureWindowRoot(window);

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

    /// <summary>
    /// Authors the shared UIWindow interaction group and its explicit default state.
    /// </summary>
    /// <param name="window">The generated window shell.</param>
    private static void ConfigureWindowRoot(UIWindow window)
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

    /// <summary>
    /// Authors one window action button when its image and action are valid.
    /// </summary>
    /// <param name="image">The target image.</param>
    /// <param name="action">The semantic window action.</param>
    /// <returns>The configured button, or null when no control is required.</returns>
    private static Button ConfigureWindowButton(RawImage image, int action)
    {
        if (image == null || action == 0)
            return null;

        Button button = CreateButton(image);
        image
            .GetComponent<RawImagePressVisual>()
            .SetTextures(image.texture, GetWindowButtonPressedTexture(action));
        return button;
    }

    /// <summary>
    /// Resolves the authored pressed texture for one shared window-shell action.
    /// </summary>
    /// <param name="action">The semantic window-shell action.</param>
    /// <returns>The pressed texture, or null when the action has no authored pressed state.</returns>
    private static Texture2D GetWindowButtonPressedTexture(int action)
    {
        return action switch
        {
            StrategyWindowButtonActions.OpenSector => LoadTexture(_windowOpenSectorDownPreviewPath),
            StrategyWindowButtonActions.MinimizeWindow => LoadTexture(
                _windowMinimizeDownPreviewPath
            ),
            StrategyWindowButtonActions.CloseWindow => LoadTexture(_windowCloseDownPreviewPath),
            StrategyWindowButtonActions.SwapWindow => LoadStrategyViewTexture(
                "ui_strategyview_planetsystem_window_swap_button_pressed"
            ),
            _ => null,
        };
    }

    /// <summary>
    /// Authors the fixed utility-window command image slots.
    /// </summary>
    /// <param name="parent">The window transform.</param>
    /// <param name="layoutName">The command layout name.</param>
    /// <param name="useUpperLayout">Whether to use the upper command layout.</param>
    /// <param name="fourButtons">Whether to author four command slots.</param>
    /// <returns>The authored command images.</returns>
    private static List<RawImage> CreateUtilityDialogButtonSlots(
        Transform parent,
        string layoutName,
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
        string[] commandNames = { "Close", "Target", "Ship", "Fleet" };
        int[] yPositions = useUpperLayout ? new[] { 21, 89, 143, 197 } : new[] { 25, 93, 147, 201 };
        int count = fourButtons ? 4 : 2;
        List<RawImage> images = new List<RawImage>();
        for (int i = 0; i < count; i++)
        {
            RawImage image = CreateRawButton(
                $"{layoutName}{commandNames[i]}ButtonImage",
                parent,
                texturePaths[i]
            );
            Texture texture = image.texture;
            int width = GetTextureSourceWidthOrDefault(texture, 44);
            int height = GetTextureSourceHeightOrDefault(texture, 41);
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

    /// <summary>
    /// Authors the fixed encyclopedia command image slots.
    /// </summary>
    /// <param name="parent">The encyclopedia window transform.</param>
    /// <param name="layoutName">The command layout name.</param>
    /// <param name="useUpperLayout">Whether to use the upper command layout.</param>
    /// <returns>The authored command images.</returns>
    private static List<RawImage> CreateEncyclopediaDialogButtonSlots(
        Transform parent,
        string layoutName,
        bool useUpperLayout
    )
    {
        string[] texturePaths =
        {
            PreviewTheme?.StrategyWindows?.Encyclopedia?.CloseButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.TopicButton?.UpImagePath,
            PreviewTheme?.StrategyWindows?.Encyclopedia?.IndexButton?.UpImagePath,
        };
        string[] commandNames = { "Close", "Topic", "Index" };
        int[] yPositions = useUpperLayout ? new[] { 21, 89, 143 } : new[] { 25, 93, 147 };
        List<RawImage> images = new List<RawImage>();
        for (int i = 0; i < texturePaths.Length; i++)
        {
            RawImage image = CreateRawButton(
                $"{layoutName}{commandNames[i]}ButtonImage",
                parent,
                texturePaths[i]
            );
            Texture texture = image.texture;
            int width = GetTextureSourceWidthOrDefault(texture, 44);
            int height = GetTextureSourceHeightOrDefault(texture, 41);
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

    /// <summary>
    /// Authors one RawImage anchored to its parent's right center.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <param name="texturePath">The texture asset path.</param>
    /// <returns>The configured RawImage.</returns>
    private static RawImage CreateRightCenteredRawImage(
        string name,
        Transform parent,
        string texturePath
    )
    {
        RawImage image = CreateRawButton(name, parent, texturePath);
        Texture texture = image.texture;
        int width = GetTextureSourceWidthOrDefault(texture, 14);
        int height = GetTextureSourceHeightOrDefault(texture, 14);
        SetRightCenteredRect(image.rectTransform, width, height);
        return image;
    }

    /// <summary>
    /// Authors one consistently styled TMP label.
    /// </summary>
    /// <param name="name">The label object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <returns>The configured label.</returns>
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

    /// <summary>
    /// Instantiates and configures a nested shared single-line TMP input prefab.
    /// </summary>
    /// <param name="name">The input instance name.</param>
    /// <param name="parent">The generated parent transform.</param>
    /// <param name="placeholderText">The initial placeholder text.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <returns>The nested TMP input instance.</returns>
    private static TMP_InputField CreateTextInputField(
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
            _commonTextInputPrefabPath,
            parent
        );
        GameObject inputObject = input.gameObject;
        inputObject.name = name;
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

    /// <summary>
    /// Authors one segmented planet-system status bar.
    /// </summary>
    /// <param name="name">The bar object name.</param>
    /// <param name="parent">The planet view transform.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="cellCount">The number of authored segmented cells.</param>
    /// <param name="fillColor">The occupied-cell color.</param>
    /// <param name="emptyColor">The unoccupied-cell color.</param>
    /// <returns>The authored bar parts.</returns>
    private static BarPrefabParts CreateBar(
        string name,
        Transform parent,
        int y,
        int cellCount,
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
        for (int i = 0; i < cellCount; i++)
        {
            Image cell = CreateImage($"Segment{i}Image", root);
            cell.color = i < 7 ? fillColor : emptyColor;
            SetSourceRect(cell.rectTransform, 1 + i * 3, 0, 2, 3);
            cells.Add(cell);
        }

        return new BarPrefabParts(root, background, fill, cells);
    }

    /// <summary>
    /// Gets the largest segmented resource count supported by game generation.
    /// </summary>
    /// <returns>The required authored cell count.</returns>
    private static int GetPlanetSystemSegmentedBarCellCount()
    {
        GameGenerationConfig config = ResourceManager.GetConfig<GameGenerationConfig>();
        IReadOnlyList<SystemResourceProfile> profiles = config?.SystemResources?.Profiles;
        if (profiles == null || profiles.Count == 0)
        {
            throw new System.InvalidOperationException(
                "GameGenerationConfig must define at least one system resource profile."
            );
        }

        int cellCount = 0;
        for (int index = 0; index < profiles.Count; index++)
        {
            SystemResourceProfile profile = profiles[index];
            if (profile == null)
                continue;

            cellCount = Mathf.Max(cellCount, profile.EnergyMax, profile.RawMaterialsMax);
        }

        IReadOnlyList<HQFacilityLoadout> loadouts = config.FacilityGeneration?.HQLoadouts;
        int loadoutCellCount = 0;
        if (loadouts != null)
        {
            for (int index = 0; index < loadouts.Count; index++)
            {
                int facilityCount = loadouts[index]?.FacilityTypeIDs?.Count ?? 0;
                loadoutCellCount = Mathf.Max(loadoutCellCount, facilityCount);
            }
        }
        cellCount += loadoutCellCount;

        if (cellCount < 2)
        {
            throw new System.InvalidOperationException(
                "System resource profiles must support at least two segmented bar cells."
            );
        }

        return cellCount;
    }

    /// <summary>
    /// Authors one continuous planet-system status bar.
    /// </summary>
    /// <param name="name">The bar object name.</param>
    /// <param name="parent">The planet view transform.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="color">The fill color.</param>
    /// <returns>The authored bar parts.</returns>
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

    /// <summary>
    /// Authors one non-raycasting UGUI Image.
    /// </summary>
    /// <param name="name">The image object name.</param>
    /// <param name="parent">The parent transform.</param>
    /// <returns>The configured image.</returns>
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

    /// <summary>
    /// Gets the authored orange status color.
    /// </summary>
    /// <returns>The status color.</returns>
    private static Color Orange()
    {
        return new Color32(236, 106, 46, 255);
    }

    /// <summary>
    /// Stretches a RectTransform across its parent without offsets.
    /// </summary>
    /// <param name="rect">The target transform.</param>
    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Applies the fixed Strategy source surface rectangle.
    /// </summary>
    /// <param name="rect">The target transform.</param>
    private static void SetStrategySurfaceRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(_screenWidth, _screenHeight);
        rect.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Applies one top-left source-space rectangle.
    /// </summary>
    /// <param name="rect">The target transform.</param>
    /// <param name="x">The source-space x-coordinate.</param>
    /// <param name="y">The source-space y-coordinate.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
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
    /// Converts imported texture pixels to source-space units.
    /// </summary>
    /// <param name="texturePixels">The imported texture dimension.</param>
    /// <returns>The rounded source-space dimension.</returns>
    private static int ToSourceUnits(int texturePixels)
    {
        return Mathf.RoundToInt(texturePixels / _sourcePixelsPerUnit);
    }

    /// <summary>
    /// Gets a texture's source width or a fallback when the texture is unavailable.
    /// </summary>
    /// <param name="texture">The optional texture.</param>
    /// <param name="fallback">The fallback source width.</param>
    /// <returns>The resolved source width.</returns>
    private static int GetTextureSourceWidthOrDefault(Texture texture, int fallback)
    {
        int width = UILayout.GetTextureSourceWidth(texture);
        return width > 0 ? width : fallback;
    }

    /// <summary>
    /// Gets a texture's source height or a fallback when the texture is unavailable.
    /// </summary>
    /// <param name="texture">The optional texture.</param>
    /// <param name="fallback">The fallback source height.</param>
    /// <returns>The resolved source height.</returns>
    private static int GetTextureSourceHeightOrDefault(Texture texture, int fallback)
    {
        int height = UILayout.GetTextureSourceHeight(texture);
        return height > 0 ? height : fallback;
    }

    /// <summary>
    /// Ensures full-surface Strategy textures retain their imported dimensions.
    /// </summary>
    private static void EnsureStrategyViewBackgroundTexturesImportedAtFullSize()
    {
        HashSet<string> assetPaths = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (FactionTheme theme in PreviewThemes)
        {
            AddFullSizeTexturePath(assetPaths, theme?.TacticalHUDLayout?.ImagePath);
            AddFullSizeTexturePath(assetPaths, theme?.GalaxyBackground?.ImagePath);
            AddBattleAlertFullSizeTexturePaths(assetPaths, theme?.StrategyWindows?.BattleAlert);
        }

        foreach (string assetPath in assetPaths)
            EnsureTextureImportedAtFullSize(assetPath);
    }

    /// <summary>
    /// Adds every full-surface battle-alert texture path to an import set.
    /// </summary>
    /// <param name="assetPaths">The import-path set.</param>
    /// <param name="theme">The optional battle-alert theme.</param>
    private static void AddBattleAlertFullSizeTexturePaths(
        HashSet<string> assetPaths,
        BattleAlertWindowTheme theme
    )
    {
        if (theme == null)
            return;

        AddFullSizeTexturePath(assetPaths, theme.FrameImagePath);
        AddFullSizeTexturePath(assetPaths, theme.ResultFrameImagePath);
        AddFullSizeTexturePath(assetPaths, theme.ResultSummaryImagePath);
        AddFullSizeTexturePath(assetPaths, theme.FirstForcesDefeatedImagePath);
        AddFullSizeTexturePath(assetPaths, theme.FirstForcesVictoriousImagePath);
        AddFullSizeTexturePath(assetPaths, theme.SecondForcesDefeatedImagePath);
        AddFullSizeTexturePath(assetPaths, theme.SecondForcesVictoriousImagePath);
        AddFullSizeTexturePath(assetPaths, theme.SummaryBackgroundImagePath);
        AddFullSizeTexturePath(assetPaths, theme.ListBackgroundImagePath);
        AddFullSizeTexturePath(assetPaths, theme.ResultListBackgroundImagePath);
        AddFullSizeTexturePath(assetPaths, theme.ResultPersonnelListBackgroundImagePath);
        AddFullSizeTexturePath(assetPaths, theme.ResultDirectBackgroundImagePath);
    }

    /// <summary>
    /// Adds one eligible full-surface texture path to an import set.
    /// </summary>
    /// <param name="assetPaths">The import-path set.</param>
    /// <param name="path">The optional resource or asset path.</param>
    private static void AddFullSizeTexturePath(HashSet<string> assetPaths, string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        string assetPath = ResolveTextureAssetPath(path);
        if (
            !assetPath.StartsWith(_fullSizeStrategyViewTextureRoot, System.StringComparison.Ordinal)
        )
        {
            return;
        }

        assetPaths.Add(assetPath);
    }

    /// <summary>
    /// Applies the required import settings to one full-surface texture.
    /// </summary>
    /// <param name="assetPath">The texture asset path.</param>
    private static void EnsureTextureImportedAtFullSize(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.None)
        {
            importer.spriteImportMode = SpriteImportMode.None;
            changed = true;
        }

        if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        changed |= EnsurePlatformTextureMaxSize(
            importer,
            importer.GetDefaultPlatformTextureSettings()
        );
        changed |= EnsurePlatformTextureMaxSize(
            importer,
            importer.GetPlatformTextureSettings("Standalone")
        );

        if (changed)
            importer.SaveAndReimport();
    }

    /// <summary>
    /// Raises one platform texture limit when it would truncate authored art.
    /// </summary>
    /// <param name="importer">The texture importer.</param>
    /// <param name="settings">The platform settings.</param>
    /// <returns>True when the settings changed.</returns>
    private static bool EnsurePlatformTextureMaxSize(
        TextureImporter importer,
        TextureImporterPlatformSettings settings
    )
    {
        if (settings.maxTextureSize >= _fullSizeTextureMaxSize)
            return false;

        settings.maxTextureSize = _fullSizeTextureMaxSize;
        importer.SetPlatformTextureSettings(settings);
        return true;
    }

    /// <summary>
    /// Applies a source layout with explicit fallback dimensions.
    /// </summary>
    /// <param name="rect">The target transform.</param>
    /// <param name="layout">The optional source layout.</param>
    /// <param name="fallbackX">The fallback x-coordinate.</param>
    /// <param name="fallbackY">The fallback y-coordinate.</param>
    /// <param name="fallbackWidth">The fallback width.</param>
    /// <param name="fallbackHeight">The fallback height.</param>
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

    /// <summary>
    /// Applies a right-centered rectangle with fixed dimensions.
    /// </summary>
    /// <param name="rect">The target transform.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetRightCenteredRect(RectTransform rect, int width, int height)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Loads an optional texture from a resource-relative or asset-relative path.
    /// </summary>
    /// <param name="path">The optional texture path.</param>
    /// <returns>The loaded texture, or null.</returns>
    private static Texture2D LoadTexture(string path)
    {
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveTextureAssetPath(path));
    }

    /// <summary>
    /// Loads one texture from the Strategy View HD art root.
    /// </summary>
    /// <param name="assetName">The texture asset name.</param>
    /// <returns>The loaded texture, or null.</returns>
    private static Texture2D LoadStrategyViewTexture(string assetName)
    {
        return LoadTexture("Art/HD/UI/StrategyView/" + assetName);
    }

    /// <summary>
    /// Resolves a resource-relative or asset-relative texture path.
    /// </summary>
    /// <param name="path">The texture path.</param>
    /// <returns>The resolved asset path.</returns>
    private static string ResolveTextureAssetPath(string path)
    {
        if (path.StartsWith("Assets/", System.StringComparison.Ordinal))
            return ResolveTextureFilePath(path);

        string resourcePath = Path.Combine("Assets/Resources", path).Replace("\\", "/");
        return ResolveTextureFilePath(resourcePath);
    }

    /// <summary>
    /// Resolves an extensionless texture path to an existing image asset when possible.
    /// </summary>
    /// <param name="path">The candidate asset path.</param>
    /// <returns>The resolved image path or original candidate.</returns>
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

    /// <summary>
    /// Assigns one required serialized object reference.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The object reference value.</param>
    private static void AssignReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns the complete serialized visual graph for a unit-card template.
    /// </summary>
    /// <param name="view">The generated unit-card view.</param>
    /// <param name="hitAreaImage">The card hit-area image.</param>
    /// <param name="backgroundImage">The card background image.</param>
    /// <param name="constructionOverlayImage">The construction overlay image.</param>
    /// <param name="enrouteOverlayImage">The enroute overlay image.</param>
    /// <param name="damagedOverlayImage">The damaged overlay image.</param>
    /// <param name="entityImage">The entity image.</param>
    /// <param name="capturedOverlayImage">The captured overlay image.</param>
    /// <param name="selectionImage">The selection image.</param>
    /// <param name="starfighterBadgeImage">The starfighter badge image.</param>
    /// <param name="troopBadgeImage">The troop badge image.</param>
    /// <param name="personnelBadgeImage">The personnel badge image.</param>
    /// <param name="nameTextField">The primary name field.</param>
    /// <param name="alternateNameTextTemplate">The alternate-name template.</param>
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

    /// <summary>
    /// Assigns one required serialized Vector2Int value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void AssignVector2Int(Object target, string propertyName, Vector2Int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.vector2IntValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one required serialized floating-point value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void AssignFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one required serialized color value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void AssignColor(Object target, string propertyName, Color value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.colorValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one required serialized integer value.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized property name.</param>
    /// <param name="value">The value to assign.</param>
    private static void AssignInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns the authored default offsets for generated Strategy windows.
    /// </summary>
    /// <param name="windowsView">The generated window layer.</param>
    private static void AssignWindowLayerLayout(StrategyWindowLayerView windowsView)
    {
        AssignVector2Int(
            windowsView,
            "constructionWindowOffset",
            new Vector2Int(_constructionWindowOffsetX, _constructionWindowOffsetY)
        );
        AssignInt(windowsView, "itemDragStartDistance", _itemDragStartDistance);
    }

    /// <summary>
    /// Assigns one required serialized array of Unity object references.
    /// </summary>
    /// <typeparam name="T">The Unity object reference type.</typeparam>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized array property name.</param>
    /// <param name="values">The ordered reference values.</param>
    private static void AssignReferenceArray<T>(
        Object target,
        string propertyName,
        IReadOnlyList<T> values
    )
        where T : Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one required serialized integer array.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized array property name.</param>
    /// <param name="values">The ordered integer values.</param>
    private static void AssignIntArray(
        Object target,
        string propertyName,
        IReadOnlyList<int> values
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = FindRequiredProperty(target, serializedObject, propertyName);
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).intValue = values[i];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns every generated Strategy window prefab to the root registry.
    /// </summary>
    /// <param name="target">The generated Strategy window layer.</param>
    /// <param name="planetSystemWindowPrefab">The planet-system window prefab.</param>
    /// <param name="facilityWindowPrefab">The facility window prefab.</param>
    /// <param name="defenseWindowPrefab">The defense window prefab.</param>
    /// <param name="fleetWindowPrefab">The fleet window prefab.</param>
    /// <param name="missionsWindowPrefab">The missions window prefab.</param>
    /// <param name="constructionWindowPrefab">The construction window prefab.</param>
    /// <param name="missionCreateWindowPrefab">The mission-creation window prefab.</param>
    /// <param name="statusWindowPrefab">The status window prefab.</param>
    /// <param name="advisorReportWindowPrefab">The advisor-report window prefab.</param>
    /// <param name="messagesWindowPrefab">The messages window prefab.</param>
    /// <param name="confirmDialogWindowPrefab">The confirmation-dialog window prefab.</param>
    /// <param name="battleAlertWindowPrefab">The battle-alert window prefab.</param>
    /// <param name="finderWindowPrefab">The Finder window prefab.</param>
    /// <param name="encyclopediaWindowPrefab">The encyclopedia window prefab.</param>
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
        AdvisorReportWindowView advisorReportWindowPrefab,
        MessagesWindowView messagesWindowPrefab,
        ConfirmDialogWindowView confirmDialogWindowPrefab,
        BattleAlertWindowView battleAlertWindowPrefab,
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
        AssignWindowPrefab(target, "advisorReportWindowPrefab", advisorReportWindowPrefab);
        AssignWindowPrefab(target, "messagesWindowPrefab", messagesWindowPrefab);
        AssignWindowPrefab(target, "confirmDialogWindowPrefab", confirmDialogWindowPrefab);
        AssignWindowPrefab(target, "battleAlertWindowPrefab", battleAlertWindowPrefab);
        AssignWindowPrefab(target, "finderWindowPrefab", finderWindowPrefab);
        AssignWindowPrefab(target, "encyclopediaWindowPrefab", encyclopediaWindowPrefab);
    }

    /// <summary>
    /// Validates and assigns one generated window prefab.
    /// </summary>
    /// <param name="target">The generated Strategy window layer.</param>
    /// <param name="fieldName">The serialized registry field name.</param>
    /// <param name="prefab">The window prefab component.</param>
    private static void AssignWindowPrefab(
        StrategyWindowLayerView target,
        string fieldName,
        MonoBehaviour prefab
    )
    {
        ValidateWindowPrefab(fieldName, prefab);

        AssignReference(target, fieldName, prefab);
    }

    /// <summary>
    /// Validates one generated window prefab's required root dimensions.
    /// </summary>
    /// <param name="fieldName">The serialized registry field name.</param>
    /// <param name="prefab">The window prefab component.</param>
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

    /// <summary>
    /// Groups the authored visual references for one planet-system status bar.
    /// </summary>
    private readonly struct BarPrefabParts
    {
        /// <summary>
        /// Creates one complete status-bar reference set.
        /// </summary>
        /// <param name="root">The bar root.</param>
        /// <param name="background">The background image.</param>
        /// <param name="fill">The fill image.</param>
        /// <param name="cells">The optional segmented cell images.</param>
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

    /// <summary>
    /// Finds one required serialized property on an authoring target.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="serializedObject">The serialized object wrapper.</param>
    /// <param name="propertyName">The required property name.</param>
    /// <returns>The required serialized property.</returns>
    private static SerializedProperty FindRequiredProperty(
        Object target,
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new System.MissingMemberException(target.GetType().Name, propertyName);

        return property;
    }
}
