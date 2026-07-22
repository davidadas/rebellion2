using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using UnityEngine;

/// <summary>
/// Performs strategy-window actions requested by the battle-alert feature.
/// </summary>
public interface IBattleAlertWindowActions
{
    /// <summary>
    /// Opens the fleet pane for a completed battle's planet.
    /// </summary>
    /// <param name="planet">The battle planet.</param>
    /// <param name="sourceX">The source window's horizontal coordinate.</param>
    /// <param name="sourceY">The source window's vertical coordinate.</param>
    void OpenBattleResultFleet(Planet planet, int sourceX, int sourceY);

    /// <summary>
    /// Opens the system window for a completed battle.
    /// </summary>
    /// <param name="system">The battle's planetary system.</param>
    /// <param name="sourceX">The source window's horizontal coordinate.</param>
    /// <param name="sourceY">The source window's vertical coordinate.</param>
    void OpenBattleResultSystem(PlanetSystem system, int sourceX, int sourceY);

    /// <summary>
    /// Rebuilds the visible strategy snapshot after combat changes game state.
    /// </summary>
    void RebuildBattleSnapshot();
}

/// <summary>
/// Owns battle-alert lifecycle, per-window state, combat commands, audio sequencing, and navigation.
/// </summary>
public sealed class BattleAlertWindowController
{
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<PendingCombatResult> getPendingCombat;
    private readonly Func<SpaceCombatResult> resolveRetreat;
    private readonly Func<SpaceCombatResult> autoResolve;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Action markDirty;
    private readonly Action<string> playSfx;
    private readonly Action<string> playTrack;
    private readonly Action stopMusic;
    private readonly BattleAlertWindowProjector projector = new BattleAlertWindowProjector();
    private readonly Dictionary<BattleAlertWindowView, BattleAlertWindowSession> sessions =
        new Dictionary<BattleAlertWindowView, BattleAlertWindowSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private IBattleAlertWindowActions actions;

    /// <summary>
    /// Creates a battle-alert controller from explicit combat operations.
    /// </summary>
    /// <param name="getPendingCombat">Returns the current pending encounter.</param>
    /// <param name="resolveRetreat">Resolves a player retreat.</param>
    /// <param name="autoResolve">Automatically resolves the pending encounter.</param>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    /// <param name="playTrack">Plays a music-track path.</param>
    /// <param name="stopMusic">Stops the current music track.</param>
    /// <param name="windowLayer">Provides the authored battle-alert prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and registration.</param>
    /// <param name="getWindowPosition">Returns the authored battle-alert placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    internal BattleAlertWindowController(
        Func<PendingCombatResult> getPendingCombat,
        Func<SpaceCombatResult> resolveRetreat,
        Func<SpaceCombatResult> autoResolve,
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        Action<string> playTrack,
        Action stopMusic,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.getPendingCombat =
            getPendingCombat ?? throw new ArgumentNullException(nameof(getPendingCombat));
        this.resolveRetreat =
            resolveRetreat ?? throw new ArgumentNullException(nameof(resolveRetreat));
        this.autoResolve = autoResolve ?? throw new ArgumentNullException(nameof(autoResolve));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.playTrack = playTrack ?? throw new ArgumentNullException(nameof(playTrack));
        this.stopMusic = stopMusic ?? throw new ArgumentNullException(nameof(stopMusic));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Connects the controller to strategy-window actions.
    /// </summary>
    /// <param name="actions">The feature-specific battle-alert actions.</param>
    public void Initialize(IBattleAlertWindowActions actions)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    /// <summary>
    /// Subscribes the controller to a battle-alert view exactly once.
    /// </summary>
    /// <param name="view">The battle-alert view to bind.</param>
    public void BindWindow(BattleAlertWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        EnsureInitialized();
        if (sessions.ContainsKey(view))
            return;

        UIWindow window = view.GetComponent<UIWindow>();
        if (window == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");

        BattleAlertWindowSession session = new BattleAlertWindowSession(window);
        session.ReconcileMode(GetWindowMode(getPendingCombat(), null));
        sessions.Add(view, session);
        view.ChoiceRequested += HandleChoiceRequested;
        view.CloseRequested += HandleCloseRequested;
        view.ControlPressed += HandleControlPressed;
        view.Destroyed += HandleViewDestroyed;
        view.OpenFleetRequested += HandleOpenFleetRequested;
        view.OpenSystemRequested += HandleOpenSystemRequested;
        view.PrimaryPanelRequested += HandlePrimaryPanelRequested;
        view.ResultCategoryRequested += HandleResultCategoryRequested;
    }

    /// <summary>
    /// Projects and renders one battle-alert window.
    /// </summary>
    /// <param name="view">The destination battle-alert view.</param>
    /// <param name="window">The window shell supplying position.</param>
    public void RenderWindow(BattleAlertWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        view.Render(CreateRenderData(view, GetPlayerFactionId(), window.X, window.Y));
    }

    /// <summary>
    /// Renders every registered battle-alert window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out BattleAlertWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Reconciles battle-alert window lifecycle with the current pending encounter.
    /// </summary>
    /// <returns>True when a battle-alert window was opened or closed.</returns>
    public bool SyncPendingCombatWindow()
    {
        EnsureInitialized();
        PendingCombatResult pending = getPendingCombat();
        BattleAlertWindowView existing = FindWindow();

        if (pending == null)
        {
            if (existing == null)
                return false;

            BattleAlertWindowSession session = GetSession(existing);
            if (session.Result != null)
                return false;

            CloseWindow(existing);
            return true;
        }

        if (existing != null)
        {
            BattleAlertWindowSession session = GetSession(existing);
            session.ReconcileMode(GetWindowMode(pending, session.Result));
            return false;
        }

        BattleAlertWindowView opened = OpenWindow();
        if (opened == null)
            return false;

        PlayPendingMusic();
        return true;
    }

    /// <summary>
    /// Opens a completed combat result in the shared battle-result window.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    internal void OpenResult(GameResult result)
    {
        if (
            result is not SpaceCombatResult and not BombardmentResult and not PlanetaryAssaultResult
        )
            throw new ArgumentException("Unsupported battle result.", nameof(result));

        EnsureInitialized();
        BattleAlertWindowView view = FindWindow() ?? OpenWindow();
        if (view == null)
            return;

        SetCombatResult(view, result);
        if (result is SpaceCombatResult spaceCombatResult)
            PlayResultMusic(spaceCombatResult);
        else if (result is PlanetaryAssaultResult)
            playSfx(StrategyUISoundPaths.PlanetaryAssault);
        markDirty();
    }

    /// <summary>
    /// Finds the registered battle-alert view.
    /// </summary>
    /// <returns>The open battle-alert view, or null when none is registered.</returns>
    internal BattleAlertWindowView FindWindow()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out BattleAlertWindowView view))
                return view;
        }

        return null;
    }

    /// <summary>
    /// Returns whether a bound view owns a completed combat result.
    /// </summary>
    /// <param name="view">The battle-alert view to inspect.</param>
    /// <returns>True when the view owns a completed result.</returns>
    internal bool HasCombatResult(BattleAlertWindowView view)
    {
        return !ReferenceEquals(view, null)
            && sessions.TryGetValue(view, out BattleAlertWindowSession session)
            && session.Result != null;
    }

    /// <summary>
    /// Creates immutable presentation for one bound battle-alert view.
    /// </summary>
    /// <param name="view">The view whose controller-owned session is projected.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <returns>The complete battle-alert presentation.</returns>
    internal BattleAlertWindowRenderData CreateRenderData(
        BattleAlertWindowView view,
        string playerFactionId,
        int x,
        int y
    )
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        BattleAlertWindowSession session = GetSession(view);
        if (string.IsNullOrEmpty(playerFactionId))
            playerFactionId = GetPlayerFactionId();

        PendingCombatResult pending = getPendingCombat();

        return projector.Project(
            session.Mode,
            session.PendingPanel,
            session.ResultPanel,
            session.ResultCategory,
            pending,
            session.Result,
            playerFactionId,
            x,
            y,
            getUIContext()
        );
    }

    /// <summary>
    /// Returns completed-result music from the player's perspective.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <param name="playerFactionId">The current player faction identifier.</param>
    /// <returns>The configured result music path, or null when none applies.</returns>
    internal static string GetBattleResultMusicPath(
        BattleAlertWindowTheme theme,
        SpaceCombatResult result,
        string playerFactionId
    )
    {
        if (theme == null || result == null)
            return null;

        CombatSide? playerSide = BattleResultPresentation.GetSideForOwner(result, playerFactionId);
        if (!playerSide.HasValue || result.Winner == CombatSide.Draw)
            return BattleResultPresentation.FirstNonBlank(
                theme.ResultDrawMusicPath,
                theme.ResultMusicPath
            );

        return result.Winner == playerSide.Value
            ? BattleResultPresentation.FirstNonBlank(
                theme.ResultVictoryMusicPath,
                theme.ResultMusicPath
            )
            : BattleResultPresentation.FirstNonBlank(
                theme.ResultDefeatMusicPath,
                theme.ResultMusicPath
            );
    }

    /// <summary>
    /// Returns victory artwork, or the withdrawing faction's defeated artwork for withdrawal.
    /// </summary>
    /// <param name="theme">The active battle-alert theme.</param>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The selected summary artwork path.</returns>
    internal static string GetResultSummaryImagePath(
        BattleAlertWindowTheme theme,
        SpaceCombatResult result
    )
    {
        return BattleResultPresentation.GetSummaryImagePath(theme, result);
    }

    /// <summary>
    /// Handles a semantic battle choice from a bound view.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    /// <param name="choice">The requested combat choice.</param>
    private void HandleChoiceRequested(BattleAlertWindowView view, BattleAlertChoice choice)
    {
        if (
            !sessions.TryGetValue(view, out BattleAlertWindowSession session)
            || session.Result != null
        )
            return;

        switch (choice)
        {
            case BattleAlertChoice.Retreat:
                ResolveRetreat(view);
                break;
            case BattleAlertChoice.AutoResolve:
                ResolveAutomatically(view);
                break;
        }
    }

    /// <summary>
    /// Resolves a retreat and preserves completed-result presentation in the requesting window.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    private void ResolveRetreat(BattleAlertWindowView view)
    {
        SpaceCombatResult result = resolveRetreat();
        if (result == null)
            return;

        SetCombatResult(view, result);
        stopMusic();
        actions.RebuildBattleSnapshot();
        markDirty();
    }

    /// <summary>
    /// Resolves combat automatically and reconciles the result window lifecycle.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    private void ResolveAutomatically(BattleAlertWindowView view)
    {
        SpaceCombatResult result = autoResolve();
        if (result != null)
        {
            SetCombatResult(view, result);
            PlayResultMusic(result);
            actions.RebuildBattleSnapshot();
            markDirty();
            return;
        }

        actions.RebuildBattleSnapshot();
        CloseWindow(view);
        markDirty();
    }

    /// <summary>
    /// Closes the requesting battle-alert window.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    private void HandleCloseRequested(BattleAlertWindowView view)
    {
        CloseWindow(view);
    }

    /// <summary>
    /// Closes one bound battle-alert window through the screen lifecycle.
    /// </summary>
    /// <param name="view">The battle-alert view to close.</param>
    private void CloseWindow(BattleAlertWindowView view)
    {
        if (view != null && sessions.TryGetValue(view, out BattleAlertWindowSession session))
            closeWindow(session.Window);
    }

    /// <summary>
    /// Plays the shared strategy control sound.
    /// </summary>
    private void HandleControlPressed()
    {
        playSfx(StrategyUISoundPaths.ControlPress);
    }

    /// <summary>
    /// Opens the completed battle's fleet pane before closing the result window.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    private void HandleOpenFleetRequested(BattleAlertWindowView view)
    {
        if (TryGetResultSession(view, out BattleAlertWindowSession session))
        {
            Planet planet = GetResultPlanet(session.Result);
            if (planet == null)
            {
                CloseWindow(view);
                return;
            }

            actions.OpenBattleResultFleet(planet, session.Window.X, session.Window.Y);
        }

        CloseWindow(view);
    }

    /// <summary>
    /// Opens the completed battle's system window before closing the result window.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    private void HandleOpenSystemRequested(BattleAlertWindowView view)
    {
        if (TryGetResultSession(view, out BattleAlertWindowSession session))
        {
            Planet planet = GetResultPlanet(session.Result);
            if (planet?.GetParent() is not PlanetSystem system)
            {
                CloseWindow(view);
                return;
            }

            actions.OpenBattleResultSystem(system, session.Window.X, session.Window.Y);
        }

        CloseWindow(view);
    }

    /// <summary>
    /// Applies a primary-panel gesture to the mode-specific controller-owned session state.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    /// <param name="panel">The requested primary panel.</param>
    private void HandlePrimaryPanelRequested(BattleAlertWindowView view, BattleAlertPanel panel)
    {
        if (!sessions.TryGetValue(view, out BattleAlertWindowSession session))
            return;

        bool selectionChanged = session.Mode switch
        {
            BattleAlertWindowMode.Pending => session.SelectPendingPanel(panel),
            BattleAlertWindowMode.Result => session.SelectResultPanel(
                BattleAlertPanelCatalog.ToResultPanel(panel)
            ),
            _ => false,
        };
        if (selectionChanged)
            markDirty();
    }

    /// <summary>
    /// Applies a result-category gesture to controller-owned session state.
    /// </summary>
    /// <param name="view">The requesting battle-alert view.</param>
    /// <param name="category">The requested completed-result category.</param>
    private void HandleResultCategoryRequested(
        BattleAlertWindowView view,
        BattleResultCategory category
    )
    {
        if (
            sessions.TryGetValue(view, out BattleAlertWindowSession session)
            && session.Mode == BattleAlertWindowMode.Result
            && session.SelectResultCategory(category)
        )
        {
            markDirty();
        }
    }

    /// <summary>
    /// Unsubscribes a destroyed view and releases its feature state.
    /// </summary>
    /// <param name="view">The destroyed battle-alert view.</param>
    private void HandleViewDestroyed(BattleAlertWindowView view)
    {
        if (ReferenceEquals(view, null) || !sessions.ContainsKey(view))
            return;

        view.ChoiceRequested -= HandleChoiceRequested;
        view.CloseRequested -= HandleCloseRequested;
        view.ControlPressed -= HandleControlPressed;
        view.Destroyed -= HandleViewDestroyed;
        view.OpenFleetRequested -= HandleOpenFleetRequested;
        view.OpenSystemRequested -= HandleOpenSystemRequested;
        view.PrimaryPanelRequested -= HandlePrimaryPanelRequested;
        view.ResultCategoryRequested -= HandleResultCategoryRequested;
        sessions.Remove(view);
    }

    /// <summary>
    /// Stores a completed result and resets the controller-owned result selection.
    /// </summary>
    /// <param name="view">The result view.</param>
    /// <param name="result">The completed combat result.</param>
    private void SetCombatResult(BattleAlertWindowView view, GameResult result)
    {
        sessions[view].Complete(result);
    }

    /// <summary>
    /// Creates and registers a battle-alert window at its authored placement.
    /// </summary>
    /// <returns>The created battle-alert view.</returns>
    private BattleAlertWindowView OpenWindow()
    {
        Vector2Int position = getWindowPosition();
        windowManager.CreateWindow(
            windowLayer.BattleAlertWindowPrefab,
            windowLayer.GetWindowParent(true),
            "BattleAlertWindow",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.BattleAlertWindowPrefab),
            true,
            true,
            false,
            false,
            out BattleAlertWindowView view
        );
        if (view != null)
            BindWindow(view);
        markDirty();
        return view;
    }

    /// <summary>
    /// Returns the controller-owned session for an attached battle-alert view.
    /// </summary>
    /// <param name="view">The attached battle-alert view.</param>
    /// <returns>The session owned by the view.</returns>
    private BattleAlertWindowSession GetSession(BattleAlertWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out BattleAlertWindowSession session))
            return session;

        throw new InvalidOperationException($"{view.name} is not bound to this controller.");
    }

    /// <summary>
    /// Returns the session that owns a completed result for a bound view.
    /// </summary>
    /// <param name="view">The battle-alert view to inspect.</param>
    /// <param name="session">The session with a stored completed combat result.</param>
    /// <returns>True when a completed result is stored.</returns>
    private bool TryGetResultSession(
        BattleAlertWindowView view,
        out BattleAlertWindowSession session
    )
    {
        if (
            !ReferenceEquals(view, null)
            && sessions.TryGetValue(view, out session)
            && session.Result != null
        )
            return true;

        session = null;
        return false;
    }

    /// <summary>
    /// Returns the presentation mode represented by the current combat state.
    /// </summary>
    /// <param name="pending">The current pending encounter.</param>
    /// <param name="result">The completed result owned by the window.</param>
    /// <returns>The mode that should be presented.</returns>
    private static BattleAlertWindowMode GetWindowMode(
        PendingCombatResult pending,
        GameResult result
    )
    {
        if (result != null)
            return BattleAlertWindowMode.Result;

        return pending == null ? BattleAlertWindowMode.Hidden : BattleAlertWindowMode.Pending;
    }

    /// <summary>
    /// Returns the planet represented by a supported completed combat result.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    /// <returns>The result planet, or null when unavailable.</returns>
    private static Planet GetResultPlanet(GameResult result)
    {
        return result switch
        {
            SpaceCombatResult spaceCombat => spaceCombat.Planet,
            BombardmentResult bombardment => bombardment.Planet,
            PlanetaryAssaultResult assault => assault.Planet,
            _ => null,
        };
    }

    /// <summary>
    /// Plays configured pending-battle music without restarting the current track first.
    /// </summary>
    private void PlayPendingMusic()
    {
        PlayMusicTrack(GetBattleTheme()?.BattleMusicPath, false);
    }

    /// <summary>
    /// Plays configured completed-result music from the player's perspective.
    /// </summary>
    /// <param name="result">The completed combat result.</param>
    private void PlayResultMusic(SpaceCombatResult result)
    {
        string musicPath = GetBattleResultMusicPath(GetBattleTheme(), result, GetPlayerFactionId());
        PlayMusicTrack(musicPath, true);
    }

    /// <summary>
    /// Plays a configured music track with optional restart behavior.
    /// </summary>
    /// <param name="musicPath">The music track path.</param>
    /// <param name="restart">Whether to stop current music before playback.</param>
    private void PlayMusicTrack(string musicPath, bool restart)
    {
        if (string.IsNullOrWhiteSpace(musicPath))
            return;

        if (restart)
            stopMusic();
        playTrack(musicPath);
    }

    /// <summary>
    /// Returns the current battle-alert theme.
    /// </summary>
    /// <returns>The current battle-alert theme, or null when no UI context is active.</returns>
    private BattleAlertWindowTheme GetBattleTheme()
    {
        return getUIContext()?.GetPlayerFactionTheme()?.StrategyWindows?.BattleAlert;
    }

    /// <summary>
    /// Returns the current player faction identifier with the saved-game fallback.
    /// </summary>
    /// <returns>The current player faction identifier.</returns>
    private string GetPlayerFactionId()
    {
        UIContext uiContext = getUIContext();
        string playerFactionId = uiContext?.Game?.Summary?.PlayerFactionID;
        if (!string.IsNullOrEmpty(playerFactionId))
            return playerFactionId;

        return uiContext
            ?.Game?.Factions?.FirstOrDefault(faction => !string.IsNullOrEmpty(faction.PlayerID))
            ?.InstanceID;
    }

    /// <summary>
    /// Verifies strategy window actions were connected before controller use.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                "BattleAlertWindowController.Initialize must be called first."
            );
    }

    /// <summary>
    /// Owns mutable presentation state for one battle-alert window instance.
    /// </summary>
    private sealed class BattleAlertWindowSession
    {
        /// <summary>
        /// Creates one battle-alert window session.
        /// </summary>
        /// <param name="window">The owning battle-alert window.</param>
        public BattleAlertWindowSession(UIWindow window)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        internal BattleAlertWindowMode Mode { get; private set; } = BattleAlertWindowMode.Hidden;

        internal BattleAlertPanel PendingPanel { get; private set; } = BattleAlertPanel.Summary;

        internal BattleResultCategory ResultCategory { get; private set; } =
            BattleResultCategoryCatalog.Ordered[0];

        internal BattleResultPanel ResultPanel { get; private set; } = BattleResultPanel.Summary;

        internal GameResult Result { get; private set; }

        internal UIWindow Window { get; }

        /// <summary>
        /// Stores a completed combat result and restores default result selections.
        /// </summary>
        /// <param name="result">The completed combat result.</param>
        internal void Complete(GameResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            ReconcileMode(BattleAlertWindowMode.Result);
            ResultCategory =
                result is PlanetaryAssaultResult
                    ? BattleResultCategory.Troops
                    : BattleResultCategory.CapitalShips;
        }

        /// <summary>
        /// Reconciles the mode and restores default selections after a mode transition.
        /// </summary>
        /// <param name="nextMode">The mode that will be presented.</param>
        internal void ReconcileMode(BattleAlertWindowMode nextMode)
        {
            if (Mode == nextMode)
                return;

            Mode = nextMode;
            PendingPanel = BattleAlertPanel.Summary;
            ResultPanel = BattleResultPanel.Summary;
            ResultCategory = BattleResultCategoryCatalog.Ordered[0];
        }

        /// <summary>
        /// Selects a pending-combat panel.
        /// </summary>
        /// <param name="panel">The pending-combat panel to select.</param>
        /// <returns>True when the selection changed.</returns>
        internal bool SelectPendingPanel(BattleAlertPanel panel)
        {
            if (PendingPanel == panel)
                return false;

            PendingPanel = panel;
            return true;
        }

        /// <summary>
        /// Selects a completed-result category.
        /// </summary>
        /// <param name="category">The completed-result category to select.</param>
        /// <returns>True when the selection changed.</returns>
        internal bool SelectResultCategory(BattleResultCategory category)
        {
            if (
                ResultCategory == category
                || !BattleResultCategoryCatalog.GetForResult(Result).Contains(category)
            )
                return false;

            ResultCategory = category;
            return true;
        }

        /// <summary>
        /// Selects a completed-result panel.
        /// </summary>
        /// <param name="panel">The completed-result panel to select.</param>
        /// <returns>True when the selection changed.</returns>
        internal bool SelectResultPanel(BattleResultPanel panel)
        {
            if (ResultPanel == panel)
                return false;

            ResultPanel = panel;
            return true;
        }
    }
}
