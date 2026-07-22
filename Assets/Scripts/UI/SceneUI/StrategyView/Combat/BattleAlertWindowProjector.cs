using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Projects pending encounters and completed combat results into immutable window presentation.
/// </summary>
internal sealed class BattleAlertWindowProjector
{
    private readonly BattleResultTableProjector resultTableProjector =
        new BattleResultTableProjector();

    /// <summary>
    /// Projects the current encounter state for one window.
    /// </summary>
    /// <param name="mode">The controller-owned window mode.</param>
    /// <param name="pendingPanel">The controller-owned pending-combat panel.</param>
    /// <param name="resultPanel">The controller-owned completed-result panel.</param>
    /// <param name="resultCategory">The controller-owned completed-result category.</param>
    /// <param name="pending">The current pending encounter.</param>
    /// <param name="result">The completed result owned by this window.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The complete immutable window presentation.</returns>
    internal BattleAlertWindowRenderData Project(
        BattleAlertWindowMode mode,
        BattleAlertPanel pendingPanel,
        BattleResultPanel resultPanel,
        BattleResultCategory resultCategory,
        PendingCombatResult pending,
        GameResult result,
        string playerFactionId,
        int x,
        int y,
        UIContext uiContext
    )
    {
        BattleAlertWindowTheme theme = uiContext
            ?.GetPlayerFactionTheme()
            ?.StrategyWindows?.BattleAlert;
        Color titleColor = uiContext?.GetPlayerFactionTheme()?.GetPrimaryColor() ?? Color.white;

        if (mode == BattleAlertWindowMode.Result && result != null)
        {
            return ProjectResult(
                resultPanel,
                resultCategory,
                result,
                playerFactionId,
                x,
                y,
                uiContext,
                theme,
                titleColor
            );
        }

        if (mode != BattleAlertWindowMode.Pending || pending == null)
            return ProjectHidden(x, y, titleColor);

        return ProjectPending(
            pendingPanel,
            pending,
            playerFactionId,
            x,
            y,
            uiContext,
            theme,
            titleColor
        );
    }

    /// <summary>
    /// Projects a pending encounter.
    /// </summary>
    /// <param name="panel">The controller-owned pending-combat panel.</param>
    /// <param name="pending">The pending encounter.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <returns>The pending-combat window presentation.</returns>
    private static BattleAlertWindowRenderData ProjectPending(
        BattleAlertPanel panel,
        PendingCombatResult pending,
        string playerFactionId,
        int x,
        int y,
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        Color titleColor
    )
    {
        IReadOnlyList<BattleAlertRowRenderData> rows =
            panel == BattleAlertPanel.Summary
                ? Array.Empty<BattleAlertRowRenderData>()
                : GetPendingRows(uiContext, theme, pending, panel);
        BattleAlertPendingRenderData pendingData = new BattleAlertPendingRenderData(
            panel,
            $"Battle at {GetPlanetName(pending.Planet)}",
            GetPendingHeader(theme, panel),
            panel == BattleAlertPanel.Summary
                ? GetPendingSummary(uiContext, theme, pending)
                : string.Empty,
            rows,
            CreatePendingCommandButtons(uiContext, theme, pending, playerFactionId)
        );

        return new BattleAlertWindowRenderData(
            BattleAlertWindowMode.Pending,
            x,
            y,
            GetTexture(
                uiContext,
                panel == BattleAlertPanel.Summary
                    ? theme?.SummaryBackgroundImagePath
                    : theme?.ListBackgroundImagePath
            ),
            GetTexture(uiContext, theme?.FrameImagePath),
            titleColor,
            CreatePendingViewButtons(uiContext, theme, panel),
            pendingData,
            null
        );
    }

    /// <summary>
    /// Projects a completed encounter.
    /// </summary>
    /// <param name="panel">The controller-owned completed-result panel.</param>
    /// <param name="category">The controller-owned completed-result category.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <returns>The completed-result window presentation.</returns>
    private BattleAlertWindowRenderData ProjectResult(
        BattleResultPanel panel,
        BattleResultCategory category,
        GameResult result,
        string playerFactionId,
        int x,
        int y,
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        Color titleColor
    )
    {
        bool detail = panel is BattleResultPanel.FirstForces or BattleResultPanel.SecondForces;
        bool direct = panel == BattleResultPanel.Direct;
        bool planetaryResult = result is BombardmentResult or PlanetaryAssaultResult;
        string ownerInstanceId = GetResultOwnerID(theme, panel);
        BattleResultTableRenderData table = detail
            ? resultTableProjector.Project(uiContext, result, ownerInstanceId, category)
            : null;
        BattleAlertResultRenderData resultData = new BattleAlertResultRenderData(
            panel,
            category,
            GetResultTitle(result),
            direct
                    ? "Select one of the following buttons to close this display and go directly to..."
                : panel == BattleResultPanel.Summary
                    ? GetResultSummary(uiContext, theme, result, playerFactionId)
                : string.Empty,
            CreateButton(uiContext, theme?.ResultCloseButton, true, false),
            detail ? GetResultHeader(theme, panel) : string.Empty,
            detail ? GetOwnerColor(uiContext, ownerInstanceId) : Color.white,
            detail ? GetCategoryTitle(category) : string.Empty,
            detail ? GetColumnHeaders(category) : Array.Empty<string>(),
            detail
                ? CreateCategoryButtons(uiContext, theme, result, category)
                : Array.Empty<BattleResultCategoryRenderData>(),
            planetaryResult,
            direct
                ? CreateDirectButtons(uiContext, theme)
                : Array.Empty<BattleAlertButtonRenderData>(),
            table
        );

        return new BattleAlertWindowRenderData(
            BattleAlertWindowMode.Result,
            x,
            y,
            GetTexture(
                uiContext,
                GetResultBackgroundPath(uiContext, theme, result, panel, category)
            ),
            GetTexture(uiContext, theme?.ResultFrameImagePath ?? theme?.FrameImagePath),
            titleColor,
            CreateResultViewButtons(uiContext, theme, panel),
            null,
            resultData
        );
    }

    /// <summary>
    /// Projects an inactive window when no encounter is available.
    /// </summary>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <returns>The hidden window presentation.</returns>
    private static BattleAlertWindowRenderData ProjectHidden(int x, int y, Color titleColor)
    {
        return new BattleAlertWindowRenderData(
            BattleAlertWindowMode.Hidden,
            x,
            y,
            null,
            null,
            titleColor,
            Array.Empty<BattleAlertButtonRenderData>(),
            null,
            null
        );
    }

    /// <summary>
    /// Creates the four primary pending-combat buttons.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="activePanel">The controller-owned pending panel.</param>
    /// <returns>The primary buttons in source order.</returns>
    private static IReadOnlyList<BattleAlertButtonRenderData> CreatePendingViewButtons(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        BattleAlertPanel activePanel
    )
    {
        WindowButtonImageTheme[] themes =
        {
            theme?.SummaryButton,
            theme?.FirstForcesButton,
            theme?.SecondForcesButton,
            theme?.SystemAssetsButton,
        };
        BattleAlertButtonRenderData[] buttons = new BattleAlertButtonRenderData[themes.Length];
        for (int i = 0; i < themes.Length; i++)
        {
            buttons[i] = CreateButton(
                uiContext,
                themes[i],
                true,
                activePanel == BattleAlertPanelCatalog.Ordered[i]
            );
        }

        return buttons;
    }

    /// <summary>
    /// Creates retreat, automatic-resolution, and take-command buttons.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="pending">The pending encounter.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The command buttons in source order.</returns>
    private static IReadOnlyList<BattleAlertButtonRenderData> CreatePendingCommandButtons(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        PendingCombatResult pending,
        string playerFactionId
    )
    {
        return new[]
        {
            CreateButton(
                uiContext,
                theme?.RetreatButton,
                CanPlayerRetreat(pending, playerFactionId),
                false
            ),
            CreateButton(uiContext, theme?.AutoResolveButton, true, false),
            CreateButton(uiContext, theme?.TakeCommandButton, false, false),
        };
    }

    /// <summary>
    /// Creates the four primary completed-result buttons.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="activePanel">The controller-owned result panel.</param>
    /// <returns>The primary result buttons in source order.</returns>
    private static IReadOnlyList<BattleAlertButtonRenderData> CreateResultViewButtons(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        BattleResultPanel activePanel
    )
    {
        WindowButtonImageTheme[] themes =
        {
            theme?.ResultSummaryButton,
            theme?.FirstForcesResultDefeatedButton,
            theme?.SecondForcesResultVictoriousButton,
            theme?.ResultDirectButton,
        };
        BattleAlertButtonRenderData[] buttons = new BattleAlertButtonRenderData[themes.Length];
        for (int i = 0; i < themes.Length; i++)
        {
            buttons[i] = CreateButton(
                uiContext,
                themes[i],
                true,
                activePanel
                    == BattleAlertPanelCatalog.ToResultPanel(BattleAlertPanelCatalog.Ordered[i])
            );
        }

        return buttons;
    }

    /// <summary>
    /// Creates completed-result category filters in displayed order.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="activeCategory">The controller-owned result category.</param>
    /// <returns>The category controls in displayed order.</returns>
    private static IReadOnlyList<BattleResultCategoryRenderData> CreateCategoryButtons(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        GameResult result,
        BattleResultCategory activeCategory
    )
    {
        IReadOnlyList<BattleResultCategory> resultCategories =
            BattleResultCategoryCatalog.GetForResult(result);
        List<BattleResultCategoryRenderData> categories = new List<BattleResultCategoryRenderData>(
            resultCategories.Count
        );
        foreach (BattleResultCategory category in resultCategories)
        {
            WindowButtonImageTheme buttonTheme = GetCategoryButtonTheme(theme, category);
            SourceRectLayout layout = GetCategoryButtonLayout(theme, buttonTheme, result, category);
            categories.Add(
                new BattleResultCategoryRenderData(
                    category,
                    CreateButton(uiContext, buttonTheme, true, activeCategory == category, layout)
                )
            );
        }

        return categories;
    }

    /// <summary>
    /// Creates completed-result navigation controls.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <returns>The system and fleet controls in source order.</returns>
    private static IReadOnlyList<BattleAlertButtonRenderData> CreateDirectButtons(
        UIContext uiContext,
        BattleAlertWindowTheme theme
    )
    {
        return new[]
        {
            CreateButton(uiContext, theme?.ResultDirectSystemButton, true, false),
            CreateButton(uiContext, theme?.ResultDirectFleetButton, true, false),
        };
    }

    /// <summary>
    /// Creates one themed button presentation.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The button theme.</param>
    /// <param name="enabled">Whether the button accepts input.</param>
    /// <param name="selected">Whether the selected-state texture is displayed.</param>
    /// <param name="sourceLayout">The optional context-specific source layout.</param>
    /// <returns>The immutable button presentation.</returns>
    private static BattleAlertButtonRenderData CreateButton(
        UIContext uiContext,
        WindowButtonImageTheme theme,
        bool enabled,
        bool selected,
        SourceRectLayout sourceLayout = null
    )
    {
        string normalPath = enabled
            ? selected
                ? theme?.DownImagePath
                : theme?.UpImagePath
            : theme?.DisabledImagePath ?? theme?.UpImagePath;
        return new BattleAlertButtonRenderData(
            enabled,
            GetTexture(uiContext, normalPath),
            enabled ? GetTexture(uiContext, theme?.DownImagePath) : null,
            GetSourceBounds(sourceLayout ?? theme?.SourceLayout)
        );
    }

    /// <summary>
    /// Converts an optional configured source layout to immutable render bounds.
    /// </summary>
    /// <param name="layout">The configured source layout.</param>
    /// <returns>The source bounds, or null when no layout is configured.</returns>
    private static RectInt? GetSourceBounds(SourceRectLayout layout)
    {
        return layout == null ? null : new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }

    /// <summary>
    /// Projects pending-combat rows for the selected panel.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="pending">The pending encounter.</param>
    /// <param name="panel">The selected pending panel.</param>
    /// <returns>The displayed rows, including an empty-state row when needed.</returns>
    private static IReadOnlyList<BattleAlertRowRenderData> GetPendingRows(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        PendingCombatResult pending,
        BattleAlertPanel panel
    )
    {
        List<BattleAlertRowRenderData> rows = new List<BattleAlertRowRenderData>();
        switch (panel)
        {
            case BattleAlertPanel.FirstForces:
                AddFleetRows(rows, pending, theme?.FirstForcesOwnerInstanceID, uiContext);
                break;
            case BattleAlertPanel.SecondForces:
                AddFleetRows(rows, pending, theme?.SecondForcesOwnerInstanceID, uiContext);
                break;
            case BattleAlertPanel.SystemAssets:
                AddSystemRows(rows, pending?.Planet, uiContext);
                break;
        }

        if (rows.Count > 0)
            return rows;

        string emptyText =
            panel == BattleAlertPanel.SystemAssets ? "No system assets found." : "No units found.";
        return new[] { new BattleAlertRowRenderData(emptyText, null) };
    }

    /// <summary>
    /// Adds fleets owned by one side of a pending encounter without duplicates.
    /// </summary>
    /// <param name="rows">The destination row collection.</param>
    /// <param name="pending">The pending encounter.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private static void AddFleetRows(
        List<BattleAlertRowRenderData> rows,
        PendingCombatResult pending,
        string ownerInstanceId,
        UIContext uiContext
    )
    {
        HashSet<string> addedFleetIds = new HashSet<string>();
        AddFleet(rows, pending?.AttackerFleet, ownerInstanceId, addedFleetIds, uiContext);
        AddFleet(rows, pending?.DefenderFleet, ownerInstanceId, addedFleetIds, uiContext);

        if (pending?.Planet == null)
            return;

        foreach (Fleet fleet in pending.Planet.GetFleets())
            AddFleet(rows, fleet, ownerInstanceId, addedFleetIds, uiContext);

        AddPlanetStarfighterRows(rows, pending.Planet, ownerInstanceId, uiContext);
    }

    /// <summary>
    /// Adds active planetary starfighters owned by one combat side to pending rows.
    /// </summary>
    /// <param name="rows">The destination row collection.</param>
    /// <param name="planet">The battle planet.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private static void AddPlanetStarfighterRows(
        List<BattleAlertRowRenderData> rows,
        Planet planet,
        string ownerInstanceId,
        UIContext uiContext
    )
    {
        foreach (
            Starfighter fighter in planet.Starfighters.Where(fighter =>
                fighter.GetOwnerInstanceID() == ownerInstanceId && IsActiveStarfighter(fighter)
            )
        )
        {
            rows.Add(
                new BattleAlertRowRenderData(
                    fighter.GetDisplayName(),
                    uiContext?.GetEntityTexture(fighter, true)
                )
            );
        }
    }

    /// <summary>
    /// Adds one matching fleet hierarchy when it has not already been represented.
    /// </summary>
    /// <param name="rows">The destination row collection.</param>
    /// <param name="fleet">The candidate fleet.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="addedFleetIds">The fleet identifiers already represented.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private static void AddFleet(
        List<BattleAlertRowRenderData> rows,
        Fleet fleet,
        string ownerInstanceId,
        HashSet<string> addedFleetIds,
        UIContext uiContext
    )
    {
        if (fleet == null || fleet.GetOwnerInstanceID() != ownerInstanceId)
            return;
        if (!addedFleetIds.Add(fleet.GetInstanceID()))
            return;

        rows.Add(
            new BattleAlertRowRenderData(fleet.GetDisplayName(), GetFleetTexture(uiContext, fleet))
        );
        AddDescendantRows(rows, fleet, uiContext);
    }

    /// <summary>
    /// Adds system assets and their descendants while excluding fleets.
    /// </summary>
    /// <param name="rows">The destination row collection.</param>
    /// <param name="planet">The battle planet.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private static void AddSystemRows(
        List<BattleAlertRowRenderData> rows,
        Planet planet,
        UIContext uiContext
    )
    {
        if (planet == null)
            return;

        foreach (ISceneNode child in planet.GetChildren())
        {
            if (child is Fleet || child is Starfighter fighter && IsActiveStarfighter(fighter))
                continue;

            rows.Add(
                new BattleAlertRowRenderData(
                    child.GetDisplayName(),
                    uiContext?.GetEntityTexture(child, true)
                )
            );
            AddDescendantRows(rows, child, uiContext);
        }
    }

    /// <summary>
    /// Returns whether a planetary starfighter can participate in a pending battle.
    /// </summary>
    /// <param name="fighter">The starfighter to inspect.</param>
    /// <returns>True when the squadron is complete, stationary, and has surviving fighters.</returns>
    private static bool IsActiveStarfighter(Starfighter fighter)
    {
        return fighter.ManufacturingStatus == ManufacturingStatus.Complete
            && fighter.Movement == null
            && fighter.CurrentSquadronSize > 0;
    }

    /// <summary>
    /// Adds all descendants of a pending-combat row in scene-graph order.
    /// </summary>
    /// <param name="rows">The destination row collection.</param>
    /// <param name="node">The parent scene node.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    private static void AddDescendantRows(
        List<BattleAlertRowRenderData> rows,
        ISceneNode node,
        UIContext uiContext
    )
    {
        if (node == null)
            return;

        foreach (ISceneNode child in node.GetChildren())
        {
            rows.Add(
                new BattleAlertRowRenderData(
                    child.GetDisplayName(),
                    uiContext?.GetEntityTexture(child, true)
                )
            );
            AddDescendantRows(rows, child, uiContext);
        }
    }

    /// <summary>
    /// Returns the themed fleet-list texture for a fleet owner.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="fleet">The fleet to represent.</param>
    /// <returns>The fleet-list texture, or null when no mapping exists.</returns>
    private static Texture2D GetFleetTexture(UIContext uiContext, Fleet fleet)
    {
        string ownerId = fleet?.GetOwnerInstanceID();
        return string.IsNullOrEmpty(ownerId)
            ? null
            : uiContext?.GetTexture(
                uiContext
                    .GetTheme(ownerId)
                    ?.PlanetOverlayTheme?.UnitTileIcons?.FleetListIconImagePath
            );
    }

    /// <summary>
    /// Returns the active pending-panel header.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="panel">The selected pending panel.</param>
    /// <returns>The displayed panel header.</returns>
    private static string GetPendingHeader(BattleAlertWindowTheme theme, BattleAlertPanel panel)
    {
        return panel switch
        {
            BattleAlertPanel.FirstForces => GetThemeText(
                theme?.FirstForcesHeaderText,
                "First Forces"
            ),
            BattleAlertPanel.SecondForces => GetThemeText(
                theme?.SecondForcesHeaderText,
                "Second Forces"
            ),
            BattleAlertPanel.SystemAssets => "System Assets",
            _ => "Battle Summary",
        };
    }

    /// <summary>
    /// Builds the pending-combat summary text.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="pending">The pending encounter.</param>
    /// <returns>The displayed pending-combat summary.</returns>
    private static string GetPendingSummary(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        PendingCombatResult pending
    )
    {
        string attackerSide = GetOwnerLabel(
            uiContext,
            theme,
            BattleResultPresentation.FirstNonBlank(
                pending.AttackerOwnerInstanceID,
                pending.AttackerFleet?.GetOwnerInstanceID()
            ),
            "Attacking"
        );
        string defenderSide = GetOwnerLabel(
            uiContext,
            theme,
            BattleResultPresentation.FirstNonBlank(
                pending.DefenderOwnerInstanceID,
                pending.DefenderFleet?.GetOwnerInstanceID()
            ),
            "Defending"
        );
        return $"The {attackerSide} fleet has entered the {GetPlanetName(pending.Planet)} system. {defenderSide} forces have been detected on an intercept course.";
    }

    /// <summary>
    /// Builds the completed battle-result summary text.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The displayed completed-result summary.</returns>
    private static string GetResultSummary(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        GameResult result,
        string playerFactionId
    )
    {
        return result switch
        {
            SpaceCombatResult spaceCombat => GetSpaceResultSummary(
                uiContext,
                theme,
                spaceCombat,
                playerFactionId
            ),
            BombardmentResult bombardment => GetBombardmentSummary(uiContext, theme, bombardment),
            PlanetaryAssaultResult assault => GetPlanetaryAssaultSummary(uiContext, theme, assault),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Builds the completed space-combat summary text.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed space-combat result.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The displayed completed-result summary.</returns>
    private static string GetSpaceResultSummary(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        SpaceCombatResult result,
        string playerFactionId
    )
    {
        if (result == null || result.Winner == CombatSide.Draw)
            return $"The battle at {GetPlanetName(result?.Planet)} is indecisive.\n\nThere has been no victor.";

        string planetName = GetPlanetName(result.Planet);
        CombatSide? playerSide = BattleResultPresentation.GetSideForOwner(result, playerFactionId);
        CombatSide? losingSide = BattleResultPresentation.GetOpposingSide(result.Winner);
        string winnerName = GetSideLabel(uiContext, theme, result, result.Winner, "Victorious");

        if (playerSide.HasValue && result.Winner != playerSide.Value)
        {
            string playerName = GetSideLabel(uiContext, theme, result, playerSide.Value, "Your");
            return
                BattleResultPresentation.GetOutcome(result, playerSide.Value)
                == SpaceCombatSideOutcome.Destroyed
                ? $"The {playerName} fleet was defeated at {planetName}. Your fleet has been destroyed."
                : $"The {playerName} fleet was defeated at {planetName}. Your fleet has withdrawn.";
        }

        string losingFleetText = string.Empty;
        if (losingSide.HasValue)
        {
            string loserName = GetSideLabel(uiContext, theme, result, losingSide.Value, "opposing");
            losingFleetText =
                BattleResultPresentation.GetOutcome(result, losingSide.Value)
                == SpaceCombatSideOutcome.Destroyed
                    ? $" The {loserName} fleet has been completely destroyed."
                    : $" The {loserName} fleet has withdrawn.";
        }

        return $"The {winnerName} fleet is victorious.\n\n{planetName} is now under blockade by {winnerName} forces.{losingFleetText}";
    }

    /// <summary>
    /// Builds the completed orbital-bombardment summary text.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed bombardment result.</param>
    /// <returns>The displayed bombardment summary.</returns>
    private static string GetBombardmentSummary(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        BombardmentResult result
    )
    {
        string attacker = GetOwnerLabel(
            uiContext,
            theme,
            result?.AttackerOwnerInstanceID,
            "Attacking"
        );
        string planet = GetPlanetName(result?.Planet);
        if (string.IsNullOrEmpty(result?.DefenderOwnerInstanceID))
            return $"{attacker} ships have conducted an orbital strike on the non-aligned system {planet}.";

        string defender = GetOwnerLabel(
            uiContext,
            theme,
            result.DefenderOwnerInstanceID,
            "defending"
        );
        return $"{attacker} ships have conducted an orbital strike on the {defender} system of {planet}.";
    }

    /// <summary>
    /// Builds the completed planetary-assault summary text.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed planetary-assault result.</param>
    /// <returns>The displayed planetary-assault summary.</returns>
    private static string GetPlanetaryAssaultSummary(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        PlanetaryAssaultResult result
    )
    {
        string attacker = GetOwnerLabel(
            uiContext,
            theme,
            result?.AttackerOwnerInstanceID,
            "Attacking"
        );
        string planet = GetPlanetName(result?.Planet);
        if (string.IsNullOrEmpty(result?.DefenderOwnerInstanceID))
        {
            return result?.Success == true
                ? $"{attacker} troops have seized control of the neutral system {planet}."
                : $"The neutral system {planet} has repulsed an attack by {attacker} troops.";
        }

        string defender = GetOwnerLabel(
            uiContext,
            theme,
            result.DefenderOwnerInstanceID,
            "defending"
        );
        return result.Success
            ? $"{attacker} troops have taken control of the {defender} system {planet}."
            : $"{defender} Troops have defended {planet} from an {attacker} assault.";
    }

    /// <summary>
    /// Returns the source title for a supported completed combat result.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The displayed result title.</returns>
    private static string GetResultTitle(GameResult result)
    {
        return result switch
        {
            BombardmentResult bombardment =>
                $"Orbital bombardment of {GetPlanetName(bombardment.Planet)}",
            PlanetaryAssaultResult assault => $"Assault on {GetPlanetName(assault.Planet)}",
            SpaceCombatResult spaceCombat => $"Battle at {GetPlanetName(spaceCombat.Planet)}",
            _ => "Battle Result",
        };
    }

    /// <summary>
    /// Returns the completed-result background path for the selected panel and category.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="panel">The selected result panel.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The selected result background path.</returns>
    private static string GetResultBackgroundPath(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        GameResult result,
        BattleResultPanel panel,
        BattleResultCategory category
    )
    {
        return panel switch
        {
            BattleResultPanel.FirstForces or BattleResultPanel.SecondForces =>
                GetResultListBackgroundPath(theme, category),
            BattleResultPanel.Direct => BattleResultPresentation.FirstNonBlank(
                theme?.ResultDirectBackgroundImagePath,
                theme?.ResultSummaryImagePath
            ),
            _ => GetResultSummaryImagePath(uiContext, theme, result),
        };
    }

    /// <summary>
    /// Returns the summary artwork for a supported completed combat result.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The selected summary artwork path.</returns>
    private static string GetResultSummaryImagePath(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        GameResult result
    )
    {
        return result switch
        {
            SpaceCombatResult spaceCombat => BattleResultPresentation.GetSummaryImagePath(
                theme,
                spaceCombat
            ),
            BombardmentResult bombardment => GetBombardmentSummaryImagePath(theme, bombardment),
            PlanetaryAssaultResult assault => BattleResultPresentation.FirstNonBlank(
                uiContext
                    ?.GetTheme(assault.AttackerOwnerInstanceID)
                    ?.StrategyWindows?.BattleAlert?.PlanetaryAssaultImagePath,
                theme?.PlanetaryAssaultImagePath,
                theme?.ResultSummaryImagePath
            ),
            _ => theme?.ResultSummaryImagePath,
        };
    }

    /// <summary>
    /// Returns bombardment summary artwork according to the sides that sustained losses.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed bombardment result.</param>
    /// <returns>The selected bombardment artwork path.</returns>
    private static string GetBombardmentSummaryImagePath(
        BattleAlertWindowTheme theme,
        BombardmentResult result
    )
    {
        if (HasBombardmentAttackerLosses(result))
            return theme?.BombardmentAttackerLossesImagePath;
        if (HasBombardmentTargetLosses(result))
            return theme?.BombardmentTargetLossesImagePath;
        return theme?.BombardmentNoLossesImagePath;
    }

    /// <summary>
    /// Returns whether the attacking fleet sustained bombardment-defense losses.
    /// </summary>
    /// <param name="result">The completed bombardment result.</param>
    /// <returns>True when an attacking capital ship was damaged or destroyed.</returns>
    private static bool HasBombardmentAttackerLosses(BombardmentResult result)
    {
        return result?.DestroyedCapitalShips?.Count > 0
            || result?.AttackerShipDamage?.Any(damage =>
                damage != null && damage.HullAfter < damage.HullBefore
            ) == true;
    }

    /// <summary>
    /// Returns whether the bombardment damaged the target system.
    /// </summary>
    /// <param name="result">The completed bombardment result.</param>
    /// <returns>True when any target asset or system value was destroyed.</returns>
    private static bool HasBombardmentTargetLosses(BombardmentResult result)
    {
        return result != null
            && (
                result.PlanetDestroyed
                || result.HeadquartersDestroyed
                || result.EnergyCapacityDamage > 0
                || result.AllocatedEnergyDamage > 0
                || result.DestroyedRegiments.Count > 0
                || result.DestroyedBuildings.Count > 0
                || result.Events.OfType<OfficerKilledResult>().Any()
            );
    }

    /// <summary>
    /// Returns the completed-result list background for a selected category.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="category">The selected result category.</param>
    /// <returns>The selected list background path.</returns>
    private static string GetResultListBackgroundPath(
        BattleAlertWindowTheme theme,
        BattleResultCategory category
    )
    {
        return category == BattleResultCategory.Personnel
            ? BattleResultPresentation.FirstNonBlank(
                theme?.ResultPersonnelListBackgroundImagePath,
                theme?.ResultListBackgroundImagePath
            )
            : BattleResultPresentation.FirstNonBlank(
                theme?.ResultListBackgroundImagePath,
                theme?.SummaryBackgroundImagePath
            );
    }

    /// <summary>
    /// Returns the header for the selected result force panel.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="panel">The selected result panel.</param>
    /// <returns>The displayed force header.</returns>
    private static string GetResultHeader(BattleAlertWindowTheme theme, BattleResultPanel panel)
    {
        return panel == BattleResultPanel.SecondForces
            ? GetThemeText(theme?.SecondForcesHeaderText, "Second Forces")
            : GetThemeText(theme?.FirstForcesHeaderText, "First Forces");
    }

    /// <summary>
    /// Returns the owner represented by a completed-result force panel.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="panel">The selected result panel.</param>
    /// <returns>The represented owner identifier.</returns>
    private static string GetResultOwnerID(BattleAlertWindowTheme theme, BattleResultPanel panel)
    {
        return panel == BattleResultPanel.SecondForces
            ? theme?.SecondForcesOwnerInstanceID
            : theme?.FirstForcesOwnerInstanceID;
    }

    /// <summary>
    /// Returns the display title for a completed-result category.
    /// </summary>
    /// <param name="category">The selected result category.</param>
    /// <returns>The displayed category title.</returns>
    private static string GetCategoryTitle(BattleResultCategory category)
    {
        return category switch
        {
            BattleResultCategory.Starfighters => "Starfighters",
            BattleResultCategory.Manufacturing => "Manufacturing Facilities",
            BattleResultCategory.Defense => "Defensive Facilities",
            BattleResultCategory.Troops => "Troops",
            BattleResultCategory.Personnel => "Personnel",
            _ => "Capital Ships",
        };
    }

    /// <summary>
    /// Returns result-table column headers for a selected category.
    /// </summary>
    /// <param name="category">The selected result category.</param>
    /// <returns>The displayed column headers in source order.</returns>
    private static IReadOnlyList<string> GetColumnHeaders(BattleResultCategory category)
    {
        return category == BattleResultCategory.Personnel
            ? new[] { "Survivors", "Captured", "Killed" }
            : new[] { "Operational", "Destroyed" };
    }

    /// <summary>
    /// Returns the configured filter-button theme for a completed-result category.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="category">The requested result category.</param>
    /// <returns>The category button theme.</returns>
    private static WindowButtonImageTheme GetCategoryButtonTheme(
        BattleAlertWindowTheme theme,
        BattleResultCategory category
    )
    {
        return category switch
        {
            BattleResultCategory.Starfighters => theme?.ResultStarfightersButton,
            BattleResultCategory.Manufacturing => theme?.ResultManufacturingButton,
            BattleResultCategory.Defense => theme?.ResultDefenseButton,
            BattleResultCategory.Troops => theme?.ResultTroopsButton,
            BattleResultCategory.Personnel => theme?.ResultPersonnelButton,
            _ => theme?.ResultCapitalShipsButton,
        };
    }

    /// <summary>
    /// Returns the configured source-space layout for one result category button.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="buttonTheme">The selected category button theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="category">The requested result category.</param>
    /// <returns>The category button layout.</returns>
    private static SourceRectLayout GetCategoryButtonLayout(
        BattleAlertWindowTheme theme,
        WindowButtonImageTheme buttonTheme,
        GameResult result,
        BattleResultCategory category
    )
    {
        return result is BombardmentResult or PlanetaryAssaultResult
            ? GetPlanetaryCategoryLayout(theme, category)
            : buttonTheme?.SourceLayout;
    }

    /// <summary>
    /// Returns the planetary-result layout for one category.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="category">The requested result category.</param>
    /// <returns>The configured contextual layout.</returns>
    private static SourceRectLayout GetPlanetaryCategoryLayout(
        BattleAlertWindowTheme theme,
        BattleResultCategory category
    )
    {
        return category switch
        {
            BattleResultCategory.CapitalShips => theme?.PlanetaryResultCapitalShipsLayout,
            BattleResultCategory.Starfighters => theme?.PlanetaryResultStarfightersLayout,
            BattleResultCategory.Manufacturing => theme?.PlanetaryResultManufacturingLayout,
            BattleResultCategory.Defense => theme?.PlanetaryResultDefenseLayout,
            BattleResultCategory.Troops => theme?.PlanetaryResultTroopsLayout,
            BattleResultCategory.Personnel => theme?.PlanetaryResultPersonnelLayout,
            _ => null,
        };
    }

    /// <summary>
    /// Returns the faction color represented by a result owner.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <returns>The configured faction color, or white when no owner is available.</returns>
    private static Color GetOwnerColor(UIContext uiContext, string ownerInstanceId)
    {
        return uiContext == null || string.IsNullOrEmpty(ownerInstanceId)
            ? Color.white
            : uiContext.ResolveFactionColor(ownerInstanceId);
    }

    /// <summary>
    /// Returns whether the player's represented combat side may retreat.
    /// </summary>
    /// <param name="pending">The pending encounter.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>True when the player's side may retreat.</returns>
    private static bool CanPlayerRetreat(PendingCombatResult pending, string playerFactionId)
    {
        if (pending == null || string.IsNullOrEmpty(playerFactionId))
            return false;
        string attackerOwnerInstanceId = BattleResultPresentation.FirstNonBlank(
            pending.AttackerOwnerInstanceID,
            pending.AttackerFleet?.GetOwnerInstanceID()
        );
        string defenderOwnerInstanceId = BattleResultPresentation.FirstNonBlank(
            pending.DefenderOwnerInstanceID,
            pending.DefenderFleet?.GetOwnerInstanceID()
        );
        if (attackerOwnerInstanceId == playerFactionId)
            return pending.AttackerCanRetreat;
        if (defenderOwnerInstanceId == playerFactionId)
            return pending.DefenderCanRetreat;
        return false;
    }

    /// <summary>
    /// Returns a themed or faction-derived label for an owner.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="ownerInstanceId">The represented owner identifier.</param>
    /// <param name="fallback">The fallback label.</param>
    /// <returns>The displayed side label.</returns>
    private static string GetOwnerLabel(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        string ownerInstanceId,
        string fallback
    )
    {
        if (!string.IsNullOrEmpty(ownerInstanceId))
        {
            if (ownerInstanceId == theme?.FirstForcesOwnerInstanceID)
                return GetThemeText(theme?.FirstForcesSummaryLabel, fallback);
            if (ownerInstanceId == theme?.SecondForcesOwnerInstanceID)
                return GetThemeText(theme?.SecondForcesSummaryLabel, fallback);
        }

        Faction faction = uiContext?.Game?.Factions?.FirstOrDefault(item =>
            item?.InstanceID == ownerInstanceId
        );
        string factionName = faction?.GetDisplayName();
        return string.IsNullOrEmpty(factionName) ? fallback : factionName;
    }

    /// <summary>
    /// Returns a completed-result side label from its fleet.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="side">The represented combat side.</param>
    /// <param name="fallback">The fallback label.</param>
    /// <returns>The displayed side label.</returns>
    private static string GetSideLabel(
        UIContext uiContext,
        BattleAlertWindowTheme theme,
        SpaceCombatResult result,
        CombatSide side,
        string fallback
    )
    {
        string ownerInstanceId = side switch
        {
            CombatSide.Attacker => BattleResultPresentation.FirstNonBlank(
                result?.AttackerOwnerInstanceID,
                result?.AttackerFleet?.GetOwnerInstanceID()
            ),
            CombatSide.Defender => BattleResultPresentation.FirstNonBlank(
                result?.DefenderOwnerInstanceID,
                result?.DefenderFleet?.GetOwnerInstanceID()
            ),
            _ => null,
        };
        return GetOwnerLabel(uiContext, theme, ownerInstanceId, fallback);
    }

    /// <summary>
    /// Returns a texture from the current UI context.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="path">The configured texture path.</param>
    /// <returns>The loaded texture, or null when unavailable.</returns>
    private static Texture2D GetTexture(UIContext uiContext, string path)
    {
        return uiContext?.GetTexture(path);
    }

    /// <summary>
    /// Returns a planet display name with the established missing-planet fallback.
    /// </summary>
    /// <param name="planet">The planet to name.</param>
    /// <returns>The displayed planet name.</returns>
    private static string GetPlanetName(Planet planet)
    {
        return planet?.GetDisplayName() ?? "Unknown System";
    }

    /// <summary>
    /// Returns configured theme text or a fallback when configuration is empty.
    /// </summary>
    /// <param name="text">The configured text.</param>
    /// <param name="fallback">The fallback text.</param>
    /// <returns>The selected text.</returns>
    private static string GetThemeText(string text, string fallback)
    {
        return string.IsNullOrEmpty(text) ? fallback : text;
    }
}
