using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Messages;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Supplies representative content to exact gameplay windows displayed by the artwork editor.
/// </summary>
internal static class ContentEditorArtworkWindowPreviewRenderer
{
    /// <summary>
    /// Runs the copied window's normal render path with selected-faction artwork.
    /// </summary>
    /// <param name="window">The copied gameplay-window root.</param>
    /// <param name="content">The representative records and artwork for the selected pack.</param>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    internal static void Render(
        RectTransform window,
        ContentEditorArtworkPreviewContent content,
        FactionTheme theme,
        ContentAssets assets
    )
    {
        Texture2D title = GetFactionMask(
            assets,
            theme.WindowTitleTheme?.ActiveImagePath,
            theme,
            FactionMaskColor.ActiveWindowTitleColorHex
        );
        StrategyHudView hud = window.GetComponentInChildren<StrategyHudView>(true);
        if (hud != null)
        {
            RenderTacticalInterface(hud, theme, assets);
            RenderGalaxyMap(
                window.GetComponentInChildren<GalaxyMapView>(true),
                content,
                theme,
                assets
            );
            RenderStrategyAdvisors(window, theme.StrategyAdvisor, assets);
            RenderStrategyContextMenu(
                window.GetComponentInChildren<ContextMenuView>(true),
                theme,
                assets
            );
            return;
        }
        if (window.TryGetComponent(out DefenseWindowView defense))
        {
            DefenseWindowTheme windowTheme = theme.StrategyWindows?.Defense;
            var personnel = content.GetEntities("Officer", 2).ToList();
            if (personnel.Count < 2)
                personnel.AddRange(content.GetEntities("SpecialForce", 2 - personnel.Count));
            DefenseWindowTab activeTab = DefenseWindowTab.Personnel;
            DefenseWindowTabRenderData[] tabs = DefenseWindowRenderData
                .OrderedTabs.Select(tab =>
                {
                    WindowTabImageTheme tabTheme = tab switch
                    {
                        DefenseWindowTab.Personnel => windowTheme?.PersonnelTab,
                        DefenseWindowTab.Regiments => windowTheme?.TroopTab,
                        DefenseWindowTab.Starfighters => windowTheme?.FighterTab,
                        DefenseWindowTab.Shields => windowTheme?.ShieldTab,
                        DefenseWindowTab.Batteries => windowTheme?.BatteryTab,
                        _ => null,
                    };
                    return CreateTab(tab, tabTheme, tab == activeTab, assets);
                })
                .ToArray();
            Texture2D background = GetTexture(assets, windowTheme?.PersonnelBackgroundImagePath);
            defense.Render(
                new DefenseWindowRenderData(
                    0,
                    0,
                    title,
                    "Preview system",
                    activeTab,
                    "Personnel",
                    string.Empty,
                    tabs,
                    personnel
                        .Select(
                            (entity, index) =>
                                CreateUnitCard(
                                    entity.Name,
                                    background,
                                    entity.SmallTexture,
                                    index == 0
                                        ? GetFactionMask(
                                            assets,
                                            windowTheme?.SelectionImagePath,
                                            theme,
                                            FactionMaskColor.SelectionColorHex
                                        )
                                        : null
                                )
                        )
                        .ToArray()
                )
            );
            return;
        }

        if (window.TryGetComponent(out FleetWindowView fleet))
        {
            FleetWindowTheme windowTheme = theme.StrategyWindows?.Fleet;
            UnitTileIcons icons = theme.PlanetOverlayTheme?.UnitTileIcons;
            IReadOnlyList<ContentEditorArtworkPreviewEntity> ships = content.GetEntities(
                "CapitalShip",
                2
            );
            FleetWindowTab activeTab = FleetWindowTab.CapitalShips;
            Texture2D detailBackground = GetTexture(assets, windowTheme?.DetailBackgroundImagePath);
            Texture2D banner = GetTexture(assets, windowTheme?.BannerImagePath);
            Texture2D detailSelection = GetFactionMask(
                assets,
                icons?.FleetDetailSelectionImagePath,
                theme,
                FactionMaskColor.SelectionColorHex
            );
            Texture2D constructionBackground = GetTexture(
                assets,
                icons?.FleetConstructionSmallImagePath
            );
            FleetWindowTabRenderData[] tabs = FleetWindowRenderData
                .OrderedTabs.Select(tab =>
                {
                    WindowTabImageTheme tabTheme = tab switch
                    {
                        FleetWindowTab.CapitalShips => windowTheme?.Tabs?.CapitalShips,
                        FleetWindowTab.Starfighters => windowTheme?.Tabs?.Starfighters,
                        FleetWindowTab.Regiments => windowTheme?.Tabs?.Regiments,
                        FleetWindowTab.Personnel => windowTheme?.Tabs?.Officers,
                        _ => null,
                    };
                    Texture2D normal = GetTexture(
                        assets,
                        tab == activeTab ? tabTheme?.ActiveImagePath : tabTheme?.InactiveImagePath
                    );
                    return new FleetWindowTabRenderData(
                        tab,
                        normal,
                        GetTexture(assets, tabTheme?.ActiveImagePath) ?? normal
                    );
                })
                .ToArray();
            fleet.Render(
                new FleetWindowRenderData(
                    0,
                    0,
                    title,
                    "Preview system",
                    detailBackground,
                    CreateFleetRows(ships, icons, theme, assets),
                    activeTab,
                    0,
                    true,
                    banner,
                    null,
                    null,
                    "First fleet",
                    theme.GetPrimaryColor(),
                    true,
                    "8",
                    "12",
                    tabs,
                    ships
                        .Select(
                            (ship, index) =>
                                CreateUnitCard(
                                    ship.Name,
                                    index == 1 ? constructionBackground : null,
                                    ship.SmallTexture,
                                    index == 0 ? detailSelection : null
                                )
                        )
                        .ToArray(),
                    -1,
                    -1,
                    string.Empty
                )
            );
            BindFleetPreviewImages(fleet);
            return;
        }

        if (window.TryGetComponent(out FacilityWindowView facility))
        {
            FacilityWindowTheme windowTheme = theme.StrategyWindows?.Facility;
            ContentEditorArtworkPreviewEntity ship = content.GetFirst("CapitalShip");
            ContentEditorArtworkPreviewEntity regiment = content.GetFirst("Regiment");
            IReadOnlyList<ContentEditorArtworkPreviewEntity> buildings = content.GetEntities(
                "Building",
                3
            );
            ContentEditorArtworkPreviewEntity building =
                buildings.FirstOrDefault() ?? ContentEditorArtworkPreviewEntity.Empty;
            ManufacturingLaneStateTheme laneTheme = theme
                .PlanetWindowTheme
                ?.BuildingsPane
                ?.ManufacturingLaneState;
            Texture2D controlNormal = GetTexture(assets, windowTheme?.ControlTab?.ActiveImagePath);
            Texture2D controlPressed = GetTexture(
                assets,
                windowTheme?.ControlTab?.InactiveImagePath
            );
            Texture2D activeLane = GetFactionMask(
                assets,
                laneTheme?.ActiveImagePath,
                theme,
                FactionMaskColor.ActiveBuildingPaneColorHex
            );
            Texture2D inactiveLane = GetFactionMask(
                assets,
                laneTheme?.InactiveImagePath,
                theme,
                FactionMaskColor.InactiveBuildingPaneColorHex
            );
            facility.Render(
                new FacilityWindowRenderData(
                    0,
                    0,
                    title,
                    "Preview system",
                    FacilityWindowTab.Manufacturing,
                    FacilityWindowRenderData
                        .OrderedTabs.Select(tab => new FacilityWindowTabRenderData(
                            tab,
                            tab == FacilityWindowTab.Manufacturing
                                ? FacilityWindowTabState.Active
                                : FacilityWindowTabState.Inactive
                        ))
                        .ToArray(),
                    controlNormal,
                    controlPressed ?? controlNormal,
                    new[]
                    {
                        CreateManufacturingLane(
                            activeLane,
                            ship.SmallTexture,
                            "Shipyards",
                            ship.Name
                        ),
                        CreateManufacturingLane(
                            inactiveLane,
                            regiment.SmallTexture,
                            "Training facilities",
                            regiment.Name
                        ),
                        CreateManufacturingLane(
                            inactiveLane,
                            building.SmallTexture,
                            "Construction yards",
                            building.Name
                        ),
                    },
                    "Inventory",
                    buildings
                        .Select(
                            (item, index) =>
                                new FacilityInventoryItemRenderData(item.SmallTexture, index == 0)
                        )
                        .ToArray(),
                    GetFactionMask(
                        assets,
                        windowTheme?.SelectionImagePath,
                        theme,
                        FactionMaskColor.SelectionColorHex
                    )
                )
            );
            return;
        }

        if (window.TryGetComponent(out ConstructionWindowView construction))
        {
            IReadOnlyList<ContentEditorArtworkPreviewEntity> buildings = content.GetEntities(
                "Building",
                3
            );
            ContentEditorArtworkPreviewEntity selected =
                buildings.FirstOrDefault() ?? ContentEditorArtworkPreviewEntity.Empty;
            construction.Render(
                new ConstructionWindowRenderData(
                    0,
                    0,
                    title,
                    selected.DisplayTexture,
                    selected.Name,
                    2,
                    "120",
                    "8",
                    "6 days",
                    "8 days",
                    false,
                    true,
                    buildings
                        .Select(item => new StrategyDropdownItemRenderData(
                            item.SmallTexture,
                            item.Name,
                            Color.white
                        ))
                        .ToArray()
                )
            );
            return;
        }

        if (window.TryGetComponent(out MissionsWindowView missions))
        {
            MissionsWindowTheme windowTheme = theme.StrategyWindows?.Missions;
            IReadOnlyList<ContentEditorArtworkPreviewEntity> officers = content.GetEntities(
                "Officer",
                2
            );
            Texture2D missionIcon = GetTexture(
                assets,
                theme.MissionIcons?.GetImagePath(MissionIconKeys.Diplomacy, true)
            );
            Texture2D reconnaissanceIcon = GetTexture(
                assets,
                theme.MissionIcons?.GetImagePath(MissionIconKeys.Reconnaissance, true)
            );
            Texture2D selection = GetFactionMask(
                assets,
                windowTheme?.SelectionImagePath,
                theme,
                FactionMaskColor.SelectionColorHex
            );
            missions.Render(
                new MissionsWindowRenderData(
                    0,
                    0,
                    title,
                    "Preview system",
                    MissionParticipantRole.Agent,
                    0,
                    true,
                    "Mission target",
                    missionIcon,
                    new[]
                    {
                        new MissionListRowRenderData("Diplomatic mission", missionIcon, selection),
                        new MissionListRowRenderData(
                            "Reconnaissance mission",
                            reconnaissanceIcon,
                            null
                        ),
                    },
                    MissionsWindowRenderData
                        .OrderedRoles.Select(role =>
                        {
                            WindowTabImageTheme tabTheme =
                                role == MissionParticipantRole.Agent
                                    ? windowTheme?.AgentsTab
                                    : windowTheme?.DecoysTab;
                            Texture2D normal = GetTexture(
                                assets,
                                role == MissionParticipantRole.Agent
                                    ? tabTheme?.ActiveImagePath
                                    : tabTheme?.InactiveImagePath
                            );
                            return new MissionsWindowTabRenderData(
                                role,
                                normal,
                                GetTexture(assets, tabTheme?.ActiveImagePath) ?? normal
                            );
                        })
                        .ToArray(),
                    officers
                        .Select(officer => new MissionParticipantRowRenderData(
                            officer.Name,
                            theme.GetPrimaryColor(),
                            null,
                            officer.SmallTexture
                        ))
                        .ToArray()
                )
            );
            return;
        }

        if (window.TryGetComponent(out MissionCreateWindowView missionCreate))
        {
            MissionCreateWindowTheme windowTheme = theme.StrategyWindows?.MissionCreate;
            IReadOnlyList<ContentEditorArtworkPreviewEntity> officers = content.GetEntities(
                "Officer",
                2
            );
            Texture2D missionIcon = GetTexture(
                assets,
                theme.MissionIcons?.GetImagePath(MissionIconKeys.Diplomacy, false)
            );
            missionCreate.Render(
                new MissionCreateWindowRenderData(
                    0,
                    0,
                    MissionCreateWindowTab.Mission,
                    false,
                    true,
                    GetTexture(assets, windowTheme?.TitleImagePath) ?? title,
                    "Diplomatic mission",
                    missionIcon,
                    "Mission target",
                    missionIcon,
                    false,
                    GetTexture(assets, windowTheme?.AgentsHeaderImagePath),
                    GetTexture(assets, windowTheme?.DecoysHeaderImagePath),
                    MissionCreateWindowRenderData
                        .OrderedTabs.Select(tab =>
                        {
                            WindowTabImageTheme tabTheme =
                                tab == MissionCreateWindowTab.Mission
                                    ? windowTheme?.MissionTab
                                    : windowTheme?.PersonnelTab;
                            Texture2D normal = GetTexture(
                                assets,
                                tab == MissionCreateWindowTab.Mission
                                    ? tabTheme?.ActiveImagePath
                                    : tabTheme?.InactiveImagePath
                            );
                            return new MissionCreateTabRenderData(
                                tab,
                                normal,
                                GetTexture(assets, tabTheme?.ActiveImagePath) ?? normal
                            );
                        })
                        .ToArray(),
                    new[]
                    {
                        new StrategyDropdownItemRenderData(
                            missionIcon,
                            "Diplomatic mission",
                            Color.white,
                            new MissionOddsRenderData(72, 18)
                        ),
                        new StrategyDropdownItemRenderData(
                            missionIcon,
                            "Reconnaissance mission",
                            Color.white,
                            new MissionOddsRenderData(64, 24)
                        ),
                    },
                    officers
                        .Take(1)
                        .Select(officer => new MissionParticipantRowRenderData(
                            officer.Name,
                            theme.GetPrimaryColor(),
                            null,
                            officer.SmallTexture
                        ))
                        .ToArray(),
                    officers
                        .Skip(1)
                        .Take(1)
                        .Select(officer => new MissionParticipantRowRenderData(
                            officer.Name,
                            theme.GetPrimaryColor(),
                            null,
                            officer.SmallTexture
                        ))
                        .ToArray(),
                    new MissionOddsRenderData(72, 18),
                    true,
                    GetTexture(assets, theme.StrategyCheckboxTheme?.FrameImagePath),
                    GetTexture(assets, theme.StrategyCheckboxTheme?.CheckMarkImagePath)
                )
            );
            return;
        }

        if (window.TryGetComponent(out StatusWindowView status))
        {
            StatusWindowTheme windowTheme = theme.StrategyWindows?.Status;
            ContentEditorArtworkPreviewEntity ship = content.GetFirst("CapitalShip");
            status.Render(
                new StatusWindowRenderData(
                    0,
                    0,
                    GetTexture(assets, windowTheme?.BackgroundImagePath),
                    true,
                    false,
                    "Fleet status",
                    new[] { ship.DisplayTexture },
                    ship.Name,
                    new[]
                    {
                        new StatusWindowRowRenderData("Location", "Preview system"),
                        new StatusWindowRowRenderData("Capital ships", "2"),
                        new StatusWindowRowRenderData("Starfighters", "6"),
                        new StatusWindowRowRenderData("Personnel", "3"),
                    }
                )
            );
            return;
        }

        if (window.TryGetComponent(out BattleAlertWindowView battleAlert))
        {
            BattleAlertWindowTheme windowTheme = theme.StrategyWindows?.BattleAlert;
            IReadOnlyList<ContentEditorArtworkPreviewEntity> ships = content.GetEntities(
                "CapitalShip",
                3
            );
            battleAlert.RenderPending(
                Vector2Int.zero,
                GetTexture(assets, windowTheme?.ListBackgroundImagePath),
                GetTexture(assets, windowTheme?.FrameImagePath),
                theme.GetPrimaryColor(),
                new[]
                {
                    CreateBattleButton(windowTheme?.SummaryButton, assets),
                    CreateBattleButton(windowTheme?.FirstForcesButton, assets),
                    CreateBattleButton(windowTheme?.SecondForcesButton, assets),
                    CreateBattleButton(windowTheme?.SystemAssetsButton, assets),
                },
                new BattleAlertPendingRenderData(
                    BattleAlertPanel.FirstForces,
                    "Battle at Preview system",
                    "Friendly forces",
                    "Choose how these forces should respond.",
                    ships.Select(ship => new BattleAlertRowRenderData(
                        ship.Name,
                        ship.SmallTexture
                    )),
                    new[]
                    {
                        CreateBattleButton(windowTheme?.RetreatButton, assets),
                        CreateBattleButton(windowTheme?.AutoResolveButton, assets),
                        CreateBattleButton(windowTheme?.TakeCommandButton, assets),
                    }
                )
            );
            return;
        }

        if (window.TryGetComponent(out MessagesWindowView messages))
        {
            MessagesWindowTheme windowTheme = theme.StrategyWindows?.Messages;
            ContentEditorArtworkPreviewEntity officer = content.GetFirst("Officer");
            MessagesCommandButtonRenderData close = CreateMessageButton(
                windowTheme?.CloseButton,
                assets
            );
            messages.Render(
                new MessagesWindowRenderData(
                    true,
                    Vector2Int.zero,
                    GetTexture(assets, windowTheme?.OverlayFrameImagePath),
                    new MessagesCommandBarRenderData(
                        GetTexture(assets, windowTheme?.ButtonStripImagePath),
                        close,
                        CreateMessageButton(windowTheme?.DisplayButton, assets),
                        CreateMessageButton(windowTheme?.IndexButton, assets),
                        CreateMessageButton(windowTheme?.SignalButton, assets),
                        CreateMessageButton(windowTheme?.SignalTargetButton, assets),
                        CreateMessageButton(windowTheme?.ChatCommandButton, assets)
                    ),
                    null,
                    new MessagesDetailPanelRenderData(
                        "preview-message-1",
                        officer.Name,
                        "Representative authored message artwork from the selected faction.",
                        officer.MessageTexture,
                        null,
                        GetTexture(assets, windowTheme?.GetIconImagePath(MessageType.Mission)),
                        true,
                        true
                    )
                )
            );
            return;
        }

        if (window.TryGetComponent(out FinderWindowView finder))
        {
            FinderWindowTheme windowTheme = theme.StrategyWindows?.Finder;
            WindowButtonImageTheme[] tabThemes =
            {
                windowTheme?.AllSystemsButton,
                windowTheme?.SystemsButton,
                windowTheme?.NeutralSystemsButton,
                windowTheme?.UnexploredSystemsButton,
            };
            finder.Render(
                new FinderWindowRenderData(
                    FinderMode.Systems,
                    false,
                    0,
                    0,
                    string.Empty,
                    "System finder",
                    "System name:",
                    "All systems",
                    new FinderWindowFrameRenderData(
                        0,
                        0,
                        470,
                        331,
                        true,
                        theme.UseUpperButtonLayout,
                        GetTexture(assets, windowTheme?.SystemFinderBackgroundImagePath),
                        GetTexture(assets, windowTheme?.OverlayFrameImagePath),
                        GetTexture(assets, windowTheme?.TwoButtonStripImagePath),
                        new[]
                        {
                            CreateFinderButton(
                                FinderWindowCommand.Close,
                                windowTheme?.CloseButton,
                                assets
                            ),
                            CreateFinderButton(
                                FinderWindowCommand.Target,
                                windowTheme?.TargetButton,
                                assets
                            ),
                        }
                    ),
                    tabThemes
                        .Select(button => new FinderWindowTabRenderData(
                            GetTexture(assets, button?.UpImagePath),
                            GetTexture(assets, button?.DownImagePath)
                        ))
                        .ToArray(),
                    new[]
                    {
                        new FinderWindowRowRenderData(
                            "preview-system-1",
                            "Preview system",
                            true,
                            new[] { "2", "6", "3" }
                        ),
                        new FinderWindowRowRenderData(
                            "preview-system-2",
                            "Frontier system",
                            false,
                            new[] { "1", "4", "2" }
                        ),
                    }
                )
            );
            return;
        }

        if (window.TryGetComponent(out EncyclopediaWindowView encyclopedia))
        {
            EncyclopediaWindowTheme windowTheme = theme.StrategyWindows?.Encyclopedia;
            ContentEditorArtworkPreviewEntity entry = content.GetFirst(
                "CapitalShip",
                "Officer",
                "Building"
            );
            bool showTopic =
                window.name == ContentEditorArtworkWindowPreview.EncyclopediaDetailWindowName;
            const EncyclopediaWindowTab activeTab = EncyclopediaWindowTab.AllDatabases;
            encyclopedia.Render(
                new EncyclopediaWindowRenderData(
                    showTopic,
                    new EncyclopediaWindowFrameRenderData(
                        0,
                        0,
                        470,
                        331,
                        true,
                        theme.UseUpperButtonLayout,
                        GetTexture(assets, windowTheme?.OverlayFrameImagePath),
                        GetTexture(assets, windowTheme?.ButtonStripImagePath),
                        new[]
                        {
                            CreateEncyclopediaButton(
                                EncyclopediaWindowCommand.Close,
                                windowTheme?.CloseButton,
                                assets
                            ),
                            CreateEncyclopediaButton(
                                EncyclopediaWindowCommand.ShowTopic,
                                windowTheme?.TopicButton,
                                assets,
                                showTopic
                            ),
                            CreateEncyclopediaButton(
                                EncyclopediaWindowCommand.ShowIndex,
                                windowTheme?.IndexButton,
                                assets,
                                !showTopic
                            ),
                        }
                    ),
                    new EncyclopediaWindowIndexRenderData(
                        activeTab,
                        0,
                        string.Empty,
                        EncyclopediaWindowTabCatalog.GetTitle(activeTab),
                        Enumerable
                            .Range(0, EncyclopediaWindowTabCatalog.Count)
                            .Select(index => EncyclopediaWindowTabCatalog.GetTab(index))
                            .Select(tab => new EncyclopediaTabRenderData(
                                tab,
                                GetTexture(
                                    assets,
                                    GetEncyclopediaTabTheme(windowTheme, tab)
                                        ?.GetImagePath(tab == activeTab)
                                )
                            ))
                            .ToArray(),
                        new[]
                        {
                            new EncyclopediaWindowRowRenderData(
                                "content-editor-preview",
                                entry.Name,
                                true
                            ),
                        }
                    ),
                    new EncyclopediaWindowDetailRenderData(
                        entry.Name,
                        entry.EncyclopediaDescription,
                        entry.EncyclopediaTexture,
                        true,
                        false,
                        entry.EncyclopediaBackground
                    )
                )
            );
            return;
        }

        if (window.TryGetComponent(out AdvisorReportWindowView advisor))
        {
            AdvisorReportWindowTheme windowTheme = theme.StrategyWindows?.AdvisorReport;
            Texture2D galaxy = GetTexture(assets, windowTheme?.GalaxyImagePath);
            advisor.Render(
                new AdvisorReportWindowRenderData(
                    0,
                    0,
                    AdvisorReportMode.GalaxyOverview,
                    GetTexture(assets, windowTheme?.BackgroundImagePath),
                    galaxy,
                    "Galaxy overview",
                    new[]
                    {
                        content.GetFirst("CapitalShip"),
                        content.GetFirst("Starfighter"),
                        content.GetFirst("Regiment"),
                    }
                        .Where(unit => unit.SmallTexture != null)
                        .Select(unit => new AdvisorReportRowRenderData(
                            unit.SmallTexture,
                            "001",
                            unit.MaintenanceCost.ToString("D4")
                        ))
                        .ToArray()
                )
            );
            return;
        }

        if (window.TryGetComponent(out ConfirmDialogWindowView confirm))
        {
            ConfirmDialogTheme windowTheme = theme.ConfirmDialogTheme;
            confirm.Render(
                new ConfirmDialogWindowRenderData(
                    0,
                    0,
                    GetTexture(assets, windowTheme?.BackgroundImagePath),
                    GetTexture(assets, windowTheme?.MoveTitleImagePath),
                    new[] { "Move selected units", "to Preview system?" }
                )
            );
        }
    }

    /// <summary>
    /// Runs the exact strategy HUD render path with representative counters and the selected
    /// faction's tactical-interface artwork.
    /// </summary>
    /// <param name="hud">The nested gameplay HUD.</param>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    private static void RenderTacticalInterface(
        StrategyHudView hud,
        FactionTheme theme,
        ContentAssets assets
    )
    {
        TacticalHUDLayout layout = theme.TacticalHUDLayout;
        Color color = theme.GetPrimaryColor();
        hud.Render(
            new StrategyHudViewData(
                GetTexture(assets, layout?.ImagePath),
                CreateHudCounter("1204", color, layout?.TickCounterSourceLayout),
                CreateHudCounter("480", color, layout?.RawMaterialsSourceLayout),
                CreateHudCounter("315", color, layout?.RefinedMaterialsSourceLayout),
                CreateHudCounter("+92", color, layout?.MaintenanceSourceLayout),
                null,
                GetTexture(assets, layout?.SpeedIndicators?.MediumImagePath),
                GetSourceRect(layout?.SpeedIndicatorSourceLayout),
                GetSourceRect(layout?.SpeedContextSourceLayout),
                CreateHudButtons(layout?.Buttons, assets),
                CreateHudNotifications(layout?.MessageNotifications, assets)
            )
        );
    }

    /// <summary>
    /// Opens a representative gameplay context menu so its faction-owned state icons are visible
    /// and editable on the strategy-view surface that actually consumes them.
    /// </summary>
    /// <param name="menu">The retained gameplay context-menu view.</param>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    private static void RenderStrategyContextMenu(
        ContextMenuView menu,
        FactionTheme theme,
        ContentAssets assets
    )
    {
        if (menu == null)
            return;
        StrategyContextMenuTheme contextTheme = theme.StrategyContextMenuTheme;
        Texture2D arrowOff = GetTexture(assets, contextTheme?.ArrowOffImagePath);
        Texture2D arrowOn = GetTexture(assets, contextTheme?.ArrowOnImagePath);
        Texture2D checkMark = GetTexture(assets, contextTheme?.CheckMarkImagePath);
        StrategyMenuCommand move = new(StrategyMenuAction.Move, "Move", true);
        StrategyMenuCommand status = new(StrategyMenuAction.Status, "Status", true);
        StrategyMenuCommand submenu = new("Orders", true, new[] { move, status });
        menu.OpenAt(
            menu,
            510,
            180,
            120,
            new[]
            {
                new ContextMenuCommandItem(
                    submenu,
                    arrowOff,
                    arrowOn,
                    usesIconColumn: true,
                    centerNativeIcon: true,
                    submenuCommands: new[]
                    {
                        new ContextMenuCommandItem(move),
                        new ContextMenuCommandItem(status),
                    }
                ),
                new ContextMenuCommandItem(
                    new StrategyMenuCommand(StrategyMenuAction.Status, "Selected", true),
                    checkMark,
                    checkMark,
                    usesIconColumn: true,
                    centerNativeIcon: true,
                    selected: true,
                    selectionColors: new[] { (Color32)theme.GetPrimaryColor() }
                ),
                new ContextMenuCommandItem(
                    new StrategyMenuCommand(StrategyMenuAction.Encyclopedia, "Unavailable", false)
                ),
            },
            new ContextMenuView.ContextMenuVisuals(
                Color.white,
                theme.GetPrimaryColor(),
                new Color32(128, 128, 128, 255)
            )
        );
        menu.RenderCurrent();
    }

    /// <summary>
    /// Runs the exact galaxy-map render path with representative normal, knowledge-state, and
    /// headquarters markers across the complete authored galaxy.
    /// </summary>
    /// <param name="map">The nested gameplay galaxy map.</param>
    /// <param name="content">The working pack's representative content.</param>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    private static void RenderGalaxyMap(
        GalaxyMapView map,
        ContentEditorArtworkPreviewContent content,
        FactionTheme theme,
        ContentAssets assets
    )
    {
        if (map == null)
            return;
        GalaxyBackground background = theme.GalaxyBackground;
        PlanetIcons icons = background?.PlanetIcons;
        Texture2D backgroundTexture = GetTexture(assets, background?.ImagePath);
        RectInt? backgroundBounds =
            backgroundTexture == null
                ? null
                : new RectInt(
                    background?.SourcePosition?.X ?? 0,
                    background?.SourcePosition?.Y ?? 0,
                    UILayout.ToSourceUnits(backgroundTexture.width),
                    UILayout.ToSourceUnits(backgroundTexture.height)
                );
        Texture2D headquarters = GetTexture(
            assets,
            theme.PlanetOverlayTheme?.GalaxyHeadquartersImagePath
        );
        Texture2D[] markerStates =
        {
            GetFactionMask(assets, icons?.Small, theme, FactionMaskColor.PlanetMarkerColorHex),
            GetFactionMask(assets, icons?.Medium, theme, FactionMaskColor.PlanetMarkerColorHex),
            GetFactionMask(assets, icons?.Large, theme, FactionMaskColor.PlanetMarkerColorHex),
            GetFactionMask(assets, icons?.XL, theme, FactionMaskColor.PlanetMarkerColorHex),
            GetTexture(assets, background?.UnexploredPlanetIconPath),
        };
        Texture2D defaultMarker = markerStates[0];
        int planetIndex = 0;
        GalaxyMapClusterRenderData[] clusters = content
            .GetPlanetSectors()
            .Select(sector =>
            {
                GalaxyMapStarRenderData[] stars = sector
                    .Planets.Select(planet =>
                    {
                        int stateIndex = planetIndex < markerStates.Length ? planetIndex : 0;
                        Texture2D marker = markerStates[stateIndex] ?? defaultMarker;
                        Texture2D headquartersMarker = planetIndex == 0 ? headquarters : null;
                        planetIndex++;
                        return new GalaxyMapStarRenderData(
                            planet.Element.Element("InstanceID")?.Value ?? planet.ID,
                            planet.X - sector.X,
                            planet.Y - sector.Y,
                            marker,
                            headquartersMarker
                        );
                    })
                    .ToArray();
                return new GalaxyMapClusterRenderData(
                    sector.Element.Element("InstanceID")?.Value ?? sector.ID,
                    sector.X,
                    sector.Y,
                    sector.Name,
                    false,
                    stars
                );
            })
            .ToArray();
        map.Render(
            new GalaxyMapRenderData(
                backgroundTexture,
                backgroundBounds,
                Color.white,
                default,
                clusters
            )
        );
    }

    /// <summary>
    /// Resolves the configured Encyclopedia tab artwork for one semantic database.
    /// </summary>
    /// <param name="theme">The selected faction's Encyclopedia theme.</param>
    /// <param name="tab">The semantic database tab.</param>
    /// <returns>The configured tab artwork, or null when it is absent.</returns>
    private static WindowButtonImageTheme GetEncyclopediaTabTheme(
        EncyclopediaWindowTheme theme,
        EncyclopediaWindowTab tab
    )
    {
        return tab switch
        {
            EncyclopediaWindowTab.AllDatabases => theme?.AllDatabasesButton,
            EncyclopediaWindowTab.Systems => theme?.SystemsButton,
            EncyclopediaWindowTab.Ships => theme?.ShipButton,
            EncyclopediaWindowTab.Facilities => theme?.FacilityButton,
            EncyclopediaWindowTab.Missions => theme?.MissionsButton,
            EncyclopediaWindowTab.Troops => theme?.TroopButton,
            EncyclopediaWindowTab.Personnel => theme?.PersonnelButton,
            _ => null,
        };
    }

    /// <summary>
    /// Runs the exact advisor render path so the complete strategy frame contains the selected
    /// faction's idle artwork instead of opaque placeholder images.
    /// </summary>
    /// <param name="window">The complete strategy-view preview root.</param>
    /// <param name="theme">The selected faction's advisor theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    private static void RenderStrategyAdvisors(
        RectTransform window,
        StrategyAdvisorTheme theme,
        ContentAssets assets
    )
    {
        StrategyAdvisorView advisors = window.GetComponentInChildren<StrategyAdvisorView>(true);
        if (advisors == null)
            return;
        Texture2D protocol =
            theme != null
            && !string.IsNullOrWhiteSpace(theme.AnimationImageRoot)
            && !string.IsNullOrWhiteSpace(theme.ProtocolIdleAnimation)
                ? GetTexture(
                    assets,
                    theme.GetFramePath(theme.ProtocolIdleAnimation, 0, droid: false)
                )
                : null;
        Texture2D droid =
            theme != null
            && !string.IsNullOrWhiteSpace(theme.AnimationImageRoot)
            && !string.IsNullOrWhiteSpace(theme.DroidIdleAnimation)
                ? GetTexture(assets, theme.GetFramePath(theme.DroidIdleAnimation, 0, droid: true))
                : null;
        advisors.Render(
            new StrategyAdvisorViewData(
                theme != null,
                protocol,
                droid,
                GetSourceRect(theme?.ProtocolSourceLayout),
                GetSourceRect(theme?.DroidSourceLayout),
                theme?.FrameIntervalSeconds ?? 0f
            )
        );
    }

    /// <summary>
    /// Creates one representative strategy HUD counter at its authored source-space bounds.
    /// </summary>
    /// <param name="text">The representative counter value.</param>
    /// <param name="color">The selected faction color.</param>
    /// <param name="layout">The optional authored counter bounds.</param>
    /// <returns>The immutable counter presentation.</returns>
    private static StrategyHudCounterViewData CreateHudCounter(
        string text,
        Color color,
        SourceRectLayout layout
    ) => new(text, color, GetSourceRect(layout));

    /// <summary>
    /// Projects the selected faction's tactical command buttons in authored slot order.
    /// </summary>
    /// <param name="themes">The authored button themes.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The exact HUD button render data.</returns>
    private static IReadOnlyList<StrategyHudButtonViewData> CreateHudButtons(
        IReadOnlyList<StrategyHudButtonTheme> themes,
        ContentAssets assets
    )
    {
        if (themes == null)
            return System.Array.Empty<StrategyHudButtonViewData>();
        return themes
            .Where(button => button?.HitArea != null)
            .Select(button => new StrategyHudButtonViewData(
                button.Action,
                GetRequiredSourceRect(button.HitArea),
                GetTexture(assets, button.UpImagePath),
                GetTexture(assets, button.PressedImagePath),
                GetRequiredSourceRect(button.PressedImageLayout ?? button.HitArea)
            ))
            .ToArray();
    }

    /// <summary>
    /// Projects the selected faction's default message notifications in authored slot order.
    /// </summary>
    /// <param name="themes">The authored notification themes.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The exact HUD notification render data.</returns>
    private static IReadOnlyList<StrategyHudMessageNotificationViewData> CreateHudNotifications(
        IReadOnlyList<StrategyHudMessageNotificationTheme> themes,
        ContentAssets assets
    )
    {
        if (themes == null)
            return System.Array.Empty<StrategyHudMessageNotificationViewData>();
        return themes
            .Where(notification => notification?.SourceLayout != null)
            .Select(notification => new StrategyHudMessageNotificationViewData(
                notification.Tab,
                GetTexture(assets, notification.DefaultImagePath),
                GetRequiredSourceRect(notification.SourceLayout)
            ))
            .ToArray();
    }

    /// <summary>
    /// Creates representative fleet rows with authored ships directly beneath their parent fleet.
    /// </summary>
    /// <param name="ships">The faction's representative capital ships.</param>
    /// <param name="icons">The selected faction's fleet-list state artwork.</param>
    /// <param name="theme">The faction that supplies the selection-frame color.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The populated fleet-list presentation.</returns>
    private static FleetListRowRenderData[] CreateFleetRows(
        IReadOnlyList<ContentEditorArtworkPreviewEntity> ships,
        UnitTileIcons icons,
        FactionTheme theme,
        ContentAssets assets
    )
    {
        Texture2D fleetIcon = GetTexture(assets, icons?.FleetListIconImagePath);
        List<FleetListRowRenderData> rows = new()
        {
            new FleetListRowRenderData(
                "First fleet",
                fleetIcon,
                null,
                null,
                GetTexture(assets, icons?.FleetStarfightersBadgeImagePath),
                GetTexture(assets, icons?.FleetTroopsBadgeImagePath),
                GetTexture(assets, icons?.FleetPersonnelBadgeImagePath),
                GetFactionMask(
                    assets,
                    icons?.FleetListSelectionImagePath,
                    theme,
                    FactionMaskColor.SelectionColorHex
                ),
                0,
                showTreeBranch: ships.Count > 0,
                treeContinuesBelow: ships.Count > 0,
                treeHasHorizontalBranch: ships.Count > 0
            ),
        };
        for (int index = 0; index < ships.Count; index++)
        {
            ContentEditorArtworkPreviewEntity ship = ships[index];
            rows.Add(
                new FleetListRowRenderData(
                    ship.Name,
                    ship.SmallTexture,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0,
                    isCapitalShip: true,
                    showTreeBranch: true,
                    treeContinuesAbove: true,
                    treeContinuesBelow: index < ships.Count - 1,
                    treeHasHorizontalBranch: true
                )
            );
        }

        rows.Add(
            new FleetListRowRenderData(
                "Fleet in transit",
                fleetIcon,
                GetTexture(assets, icons?.FleetListEnrouteIconImagePath),
                null,
                null,
                null,
                null,
                null,
                1
            )
        );
        rows.Add(
            new FleetListRowRenderData(
                "Damaged fleet",
                fleetIcon,
                null,
                GetTexture(assets, icons?.FleetListDamagedIconImagePath),
                null,
                null,
                null,
                null,
                2
            )
        );

        return rows.ToArray();
    }

    /// <summary>
    /// Gives dynamically pooled fleet rows and detail cards stable semantic bindings so shared
    /// textures still open the exact theme field represented by each layer.
    /// </summary>
    /// <param name="fleet">The rendered gameplay fleet window.</param>
    private static void BindFleetPreviewImages(FleetWindowView fleet)
    {
        FleetListRowView[] rows = fleet.GetComponentsInChildren<FleetListRowView>(true);
        foreach (
            FleetListRowView row in rows.Where(row =>
                row.gameObject.activeInHierarchy && !row.IsCapitalShip
            )
        )
        {
            BindThemeImage(
                row.transform,
                "IconImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetListIconImagePath"
            );
            BindThemeImage(
                row.transform,
                "EnrouteOverlayImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetListEnrouteIconImagePath"
            );
            BindThemeImage(
                row.transform,
                "DamagedOverlayImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetListDamagedIconImagePath"
            );
            BindThemeImage(
                row.transform,
                "StarfighterBadgeImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetStarfightersBadgeImagePath"
            );
            BindThemeImage(
                row.transform,
                "TroopBadgeImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetTroopsBadgeImagePath"
            );
            BindThemeImage(
                row.transform,
                "PersonnelBadgeImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetPersonnelBadgeImagePath"
            );
            BindThemeImage(
                row.transform,
                "SelectionImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetListSelectionImagePath"
            );
        }

        StrategyUnitCardView[] details = fleet
            .GetComponentsInChildren<StrategyUnitCardView>(true)
            .Where(detail => detail.gameObject.activeInHierarchy && detail.name != "ItemTemplate")
            .ToArray();
        if (details.Length > 0)
        {
            BindThemeImage(
                details[0].transform,
                "SelectionImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetDetailSelectionImagePath"
            );
        }
        if (details.Length > 1)
        {
            BindThemeImage(
                details[1].transform,
                "BackgroundImage",
                "PlanetOverlayTheme.UnitTileIcons.FleetConstructionSmallImagePath"
            );
        }
    }

    /// <summary>
    /// Adds one semantic theme binding to a named image below a dynamic gameplay view.
    /// </summary>
    /// <param name="root">The dynamic view root.</param>
    /// <param name="imageName">The exact image object name.</param>
    /// <param name="memberPath">The dotted path from <see cref="FactionTheme"/>.</param>
    private static void BindThemeImage(Transform root, string imageName, string memberPath)
    {
        RawImage image = root.GetComponentsInChildren<RawImage>(true)
            .FirstOrDefault(candidate => candidate.name == imageName);
        if (image == null)
            return;
        ContentEditorThemeTextureBinding binding =
            image.GetComponent<ContentEditorThemeTextureBinding>()
            ?? image.gameObject.AddComponent<ContentEditorThemeTextureBinding>();
        binding.SetMemberPath(memberPath);
    }

    /// <summary>
    /// Creates a Defense tab using its normal and pressed selected-faction images.
    /// </summary>
    /// <param name="tab">The represented Defense tab.</param>
    /// <param name="theme">The tab artwork.</param>
    /// <param name="active">Whether the tab is selected.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The complete tab presentation.</returns>
    private static DefenseWindowTabRenderData CreateTab(
        DefenseWindowTab tab,
        WindowTabImageTheme theme,
        bool active,
        ContentAssets assets
    )
    {
        Texture2D normal = GetTexture(
            assets,
            active ? theme?.ActiveImagePath : theme?.InactiveImagePath
        );
        return new DefenseWindowTabRenderData(
            tab,
            normal,
            GetTexture(assets, theme?.ActiveImagePath) ?? normal
        );
    }

    /// <summary>
    /// Creates a representative strategy unit card without adding gameplay state.
    /// </summary>
    /// <param name="name">The displayed card name.</param>
    /// <param name="background">The themed card background.</param>
    /// <param name="entity">The representative foreground artwork.</param>
    /// <param name="selection">The optional selected-card frame.</param>
    /// <returns>The complete card presentation.</returns>
    private static StrategyUnitCardRenderData CreateUnitCard(
        string name,
        Texture background,
        Texture entity,
        Texture selection
    )
    {
        return new StrategyUnitCardRenderData(
            name,
            Color.white,
            true,
            false,
            background,
            null,
            null,
            entity,
            null,
            selection,
            0,
            null,
            null,
            null,
            false
        );
    }

    /// <summary>
    /// Creates one populated manufacturing lane for the facilities preview.
    /// </summary>
    /// <param name="state">The lane-state artwork.</param>
    /// <param name="entity">The manufactured item artwork.</param>
    /// <param name="title">The lane title.</param>
    /// <param name="currentName">The current item label.</param>
    /// <returns>The populated manufacturing-lane presentation.</returns>
    private static ManufacturingLaneCardRenderData CreateManufacturingLane(
        Texture2D state,
        Texture entity,
        string title,
        string currentName
    )
    {
        return new ManufacturingLaneCardRenderData(
            state,
            entity,
            45,
            100,
            title,
            "No production",
            currentName,
            "2",
            "Destination: Preview system",
            "2 / 3"
        );
    }

    /// <summary>
    /// Creates one selected-faction Messages command button.
    /// </summary>
    /// <param name="theme">The command artwork.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The command presentation.</returns>
    private static MessagesCommandButtonRenderData CreateMessageButton(
        WindowButtonImageTheme theme,
        ContentAssets assets
    )
    {
        return new MessagesCommandButtonRenderData(
            GetTexture(assets, theme?.UpImagePath),
            GetTexture(assets, theme?.DownImagePath),
            GetSourceRect(theme?.SourceLayout),
            true,
            true
        );
    }

    /// <summary>
    /// Creates one battle-alert control from the selected faction's authored button images.
    /// </summary>
    /// <param name="theme">The battle control artwork.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The complete battle-alert control presentation.</returns>
    private static BattleAlertButtonRenderData CreateBattleButton(
        WindowButtonImageTheme theme,
        ContentAssets assets
    )
    {
        return new BattleAlertButtonRenderData(
            true,
            GetTexture(assets, theme?.UpImagePath),
            GetTexture(assets, theme?.DownImagePath),
            GetSourceRect(theme?.SourceLayout)
        );
    }

    /// <summary>
    /// Creates one Finder command button from its selected-faction theme.
    /// </summary>
    /// <param name="command">The represented Finder command.</param>
    /// <param name="theme">The command artwork.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The complete Finder command presentation.</returns>
    private static FinderWindowDialogButtonRenderData CreateFinderButton(
        FinderWindowCommand command,
        WindowButtonImageTheme theme,
        ContentAssets assets
    )
    {
        return new FinderWindowDialogButtonRenderData(
            command,
            GetTexture(assets, theme?.UpImagePath),
            GetTexture(assets, theme?.DownImagePath),
            GetSourceRect(theme?.SourceLayout)
        );
    }

    /// <summary>
    /// Creates one Encyclopedia command button from its selected-faction theme.
    /// </summary>
    /// <param name="command">The represented Encyclopedia command.</param>
    /// <param name="theme">The command artwork.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="active">Whether the button represents the panel currently being shown.</param>
    /// <returns>The complete Encyclopedia command presentation.</returns>
    private static EncyclopediaDialogButtonRenderData CreateEncyclopediaButton(
        EncyclopediaWindowCommand command,
        WindowButtonImageTheme theme,
        ContentAssets assets,
        bool active = false
    )
    {
        Texture texture = GetTexture(assets, theme?.GetImagePath(active));
        return new EncyclopediaDialogButtonRenderData(
            command,
            texture,
            GetTexture(assets, theme?.GetImagePath(true)) ?? texture,
            GetSourceRect(theme?.SourceLayout)
        );
    }

    /// <summary>
    /// Converts optional authored source-layout data into a Unity rectangle.
    /// </summary>
    /// <param name="layout">The optional source-space layout.</param>
    /// <returns>The source rectangle, or null when no layout is configured.</returns>
    private static RectInt? GetSourceRect(SourceRectLayout layout)
    {
        return layout == null ? null : new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }

    /// <summary>
    /// Converts required authored source-layout data into a Unity rectangle.
    /// </summary>
    /// <param name="layout">The required source-space layout.</param>
    /// <returns>The equivalent source rectangle.</returns>
    private static RectInt GetRequiredSourceRect(SourceRectLayout layout) =>
        new(layout.X, layout.Y, layout.Width, layout.Height);

    /// <summary>
    /// Resolves one optional content address without retaining an invalid asset request.
    /// </summary>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="address">The optional content address.</param>
    /// <returns>The resolved texture, or null for an absent address.</returns>
    private static Texture2D GetTexture(ContentAssets assets, string address)
    {
        return string.IsNullOrWhiteSpace(address) ? null : assets.GetTexture(address);
    }

    /// <summary>
    /// Resolves faction masks with the same color scheme as gameplay and keeps the setting identity.
    /// </summary>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="address">The optional artwork address.</param>
    /// <param name="theme">The faction theme supplying the artwork and optional scheme.</param>
    /// <param name="role">The setting that controls the tint.</param>
    /// <returns>The texture shown by the preview.</returns>
    private static Texture2D GetFactionMask(
        ContentAssets assets,
        string address,
        FactionTheme theme,
        FactionMaskColor role
    ) =>
        theme.GetMaskTint(role) is Color tint
            ? assets.GetAlphaTintedTexture(address, (Color32)tint, role)
            : GetTexture(assets, address);
}
