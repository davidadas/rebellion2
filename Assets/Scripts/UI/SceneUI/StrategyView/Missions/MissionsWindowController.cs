using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs window-level actions requested by the Missions feature.
/// </summary>
public interface IMissionsWindowActions
{
    /// <summary>
    /// Opens status information for one mission or participant.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    void OpenMissionsStatus(StrategyStatusTarget target);

    /// <summary>
    /// Opens Encyclopedia information for one mission or participant.
    /// </summary>
    /// <param name="target">The selected mission information target.</param>
    void OpenMissionsInfo(StrategyStatusTarget target);
}

/// <summary>
/// Owns Missions-window sessions, selection, targeting, and context commands.
/// </summary>
public sealed class MissionsWindowController : IStrategyContextMenuProvider, IContextMenuReceiver
{
    private readonly HashSet<MissionsWindowView> boundViews = new HashSet<MissionsWindowView>();
    private readonly Func<int, int, Vector2Int> getWindowPosition;
    private readonly Action markDirty;
    private readonly MissionsWindowProjector projector;
    private readonly Dictionary<MissionsWindowView, MissionsWindowSession> sessions =
        new Dictionary<MissionsWindowView, MissionsWindowSession>();
    private readonly TargetingController targetingController;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private IMissionsWindowActions actions;

    /// <summary>
    /// Creates the Missions feature controller.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="findVisibleNode">Resolves a node from the visible galaxy snapshot.</param>
    /// <param name="targetingController">Owns the active strategy targeting request.</param>
    /// <param name="windowLayer">Provides the authored Missions prefab and normal window layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getWindowPosition">Clamps a requested Missions-window placement.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public MissionsWindowController(
        Func<UIContext> getUIContext,
        Func<string, ISceneNode> findVisibleNode,
        TargetingController targetingController,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<int, int, Vector2Int> getWindowPosition,
        Action markDirty
    )
    {
        projector = new MissionsWindowProjector(getUIContext, findVisibleNode);
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Supplies game-level Missions actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="windowActions">The feature-specific Missions actions.</param>
    public void Initialize(IMissionsWindowActions windowActions)
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
    }

    /// <summary>
    /// Starts a Missions-window session for one authored view and planet projection.
    /// </summary>
    /// <param name="view">The destination Missions view.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeWindow(MissionsWindowView view, GalaxyMapPlanet planet)
    {
        UIWindow window = view == null ? null : view.GetComponent<UIWindow>();
        if (view == null || window == null || planet?.Planet == null)
            return false;

        BindWindow(view);
        sessions[view] = new MissionsWindowSession(planet, window);
        return true;
    }

    /// <summary>
    /// Opens or focuses the Missions window for a planet.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="sourceX">The requested source-space horizontal position.</param>
    /// <param name="sourceY">The requested source-space vertical position.</param>
    /// <param name="created">Receives whether a new window was created.</param>
    /// <returns>The opened or focused window shell.</returns>
    public UIWindow Open(GalaxyMapPlanet planet, int sourceX, int sourceY, out bool created)
    {
        created = false;
        if (planet?.Planet == null)
            return null;

        MissionsWindowSession existing = FindWindow(planet.Planet.InstanceID);
        if (existing != null)
        {
            windowManager.Focus(existing.Window);
            return existing.Window;
        }

        Vector2Int position = getWindowPosition(sourceX, sourceY);
        UIWindow window = windowManager.CreateWindow(
            windowLayer.MissionsWindowPrefab,
            windowLayer.GetWindowParent(false),
            $"MissionsWindow-{planet.Planet.GetDisplayName()}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.MissionsWindowPrefab),
            false,
            true,
            true,
            false,
            out MissionsWindowView view
        );
        if (!TryInitializeWindow(view, planet))
        {
            windowManager.DestroyWindow(window);
            return null;
        }

        created = true;
        markDirty();
        return window;
    }

    /// <summary>
    /// Renders every registered Missions window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out MissionsWindowView view))
                RenderWindow(view, window, window.ActiveWindow);
        }
    }

    /// <summary>
    /// Rebinds Missions sessions to a refreshed galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                !windowManager.TryGetWindowView(window, out MissionsWindowView view)
                || !sessions.TryGetValue(view, out MissionsWindowSession session)
            )
                continue;

            GalaxyMapPlanet planet = FindFreshPlanet(session.Planet, sectors);
            if (planet == null)
                continue;

            session.RebindPlanet(planet);
        }
    }

    /// <summary>
    /// Subscribes to one authored Missions view exactly once.
    /// </summary>
    /// <param name="view">The Missions view to bind.</param>
    public void BindWindow(MissionsWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.Destroyed += HandleViewDestroyed;
        view.MissionDoubleClicked += HandleMissionDoubleClicked;
        view.MissionDropped += HandleMissionDropped;
        view.MissionPressed += HandleMissionPressed;
        view.MissionReleased += HandleMissionReleased;
        view.ParticipantPressed += HandleParticipantPressed;
        view.SurfaceClicked += HandleSurfaceClicked;
        view.TabRequested += HandleTabRequested;
    }

    /// <summary>
    /// Projects current domain state and renders one Missions window.
    /// </summary>
    /// <param name="view">The destination Missions view.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    public void RenderWindow(MissionsWindowView view, UIWindow window, bool active)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        MissionsWindowSession session = GetSession(view);

        view.Render(projector.Build(session, window, active));
    }

    /// <summary>
    /// Gets the planet represented by one Missions view.
    /// </summary>
    /// <param name="view">The Missions view.</param>
    /// <returns>The represented planet, or null.</returns>
    public GalaxyMapPlanet GetPlanet(MissionsWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out MissionsWindowSession session)
            ? session.Planet
            : null;
    }

    /// <summary>
    /// Replaces one Missions session's stale planet projection while preserving local UI state.
    /// </summary>
    /// <param name="view">The Missions view.</param>
    /// <param name="planet">The fresh strategy planet projection.</param>
    public void ReconcileWindow(MissionsWindowView view, GalaxyMapPlanet planet)
    {
        if (view == null || planet?.Planet == null)
            return;
        if (!sessions.TryGetValue(view, out MissionsWindowSession session))
            return;

        session.RebindPlanet(planet);
    }

    /// <summary>
    /// Selects the mission and participant represented by a Finder result.
    /// </summary>
    /// <param name="view">The destination Missions view.</param>
    /// <param name="row">The selected Finder result.</param>
    /// <returns>True when the represented target belongs to this Missions session.</returns>
    public bool SelectFinderTarget(MissionsWindowView view, FinderWindowRow row)
    {
        return row != null && SelectTarget(view, row.Node, row.Mission);
    }

    /// <summary>
    /// Selects the mission containing one mission or participant scene node.
    /// </summary>
    /// <param name="view">The destination Missions view.</param>
    /// <param name="target">The mission or participant to select.</param>
    /// <param name="targetMission">An optional mission supplied by Finder projection.</param>
    /// <returns>True when the target belongs to this Missions session.</returns>
    public bool SelectTarget(
        MissionsWindowView view,
        ISceneNode target,
        Mission targetMission = null
    )
    {
        if (
            view == null
            || target == null && targetMission == null
            || !sessions.TryGetValue(view, out MissionsWindowSession session)
        )
            return false;

        IReadOnlyList<Mission> missions = session.Missions;
        Mission missionTarget =
            targetMission ?? target as Mission ?? target?.GetParentOfType<Mission>();
        int missionIndex = FindMissionIndex(missions, missionTarget);
        IMissionParticipant participant = target as IMissionParticipant;
        if (missionIndex < 0 && participant != null)
            missionIndex = FindParticipantMissionIndex(missions, participant);

        if (missionIndex < 0)
            return false;

        Mission selectedMission = missions[missionIndex];
        MissionParticipantRole role =
            participant != null
            && ContainsParticipant(selectedMission.DecoyParticipants, participant)
                ? MissionParticipantRole.Decoy
                : MissionParticipantRole.Agent;
        session.SelectTarget(missionIndex, role);
        markDirty();
        return true;
    }

    /// <summary>
    /// Gets the status target owned by one Missions session's current context selection.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <returns>The selected mission or participant status target.</returns>
    public StrategyStatusTarget GetStatusTarget(MissionsWindowView view)
    {
        if (view == null || !sessions.TryGetValue(view, out MissionsWindowSession session))
            return null;

        ISceneNode participant = session.ContextParticipant;
        if (participant != null)
            return new StrategyStatusTarget(session.Planet, participant);

        Mission mission = session.SelectedMission;
        return mission == null ? null : new StrategyStatusTarget(session.Planet, mission);
    }

    /// <summary>
    /// Gets the active participant role owned by one Missions session.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <returns>The active role, or the primary-agent role when no session exists.</returns>
    internal MissionParticipantRole GetActiveRole(MissionsWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out MissionsWindowSession session)
            ? session.ActiveRole
            : MissionParticipantRole.Agent;
    }

    /// <summary>
    /// Gets the selected mission index owned by one Missions session.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <returns>The selected mission index, or negative one when no session exists.</returns>
    internal int GetSelectedMissionIndex(MissionsWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out MissionsWindowSession session)
            ? session.SelectedMissionIndex
            : -1;
    }

    /// <inheritdoc />
    public bool TryCreateContextMenu(
        StrategyContextMenuProviderContext context,
        out ContextMenuRequest request,
        out int width
    )
    {
        request = null;
        width = 0;
        if (
            context?.Window == null
            || !windowManager.TryGetWindowView(context.Window, out MissionsWindowView view)
            || !sessions.TryGetValue(view, out MissionsWindowSession session)
        )
            return false;

        session.CaptureParticipant(view.GetParticipantIndex(context.EventData));

        List<StrategyMenuCommand> commands = BuildContextMenu(session.SelectedMission);
        request = new ContextMenuRequest(
            context,
            commands.Cast<IContextMenuCommand>().ToList(),
            this
        );
        width = context.Layout.MissionsMenuWidth;
        return true;
    }

    /// <summary>
    /// Routes one selected Missions context command.
    /// </summary>
    /// <param name="request">The completed context-menu request.</param>
    /// <param name="command">The selected context-menu command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not StrategyContextMenuProviderContext context
            || command is not StrategyMenuCommand strategyCommand
            || !windowManager.TryGetWindowView(context.Window, out MissionsWindowView view)
        )
            return;

        StrategyStatusTarget target = GetStatusTarget(view);
        switch (strategyCommand.Action)
        {
            case StrategyContextMenuActions.Encyclopedia:
                actions.OpenMissionsInfo(target);
                break;
            case StrategyContextMenuActions.Status:
                actions.OpenMissionsStatus(target);
                break;
        }
    }

    /// <summary>
    /// Handles context-menu cancellation without changing Missions state.
    /// </summary>
    /// <param name="request">The canceled context-menu request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Builds Missions context commands in their fixed visual order.
    /// </summary>
    /// <param name="mission">The selected mission, or null.</param>
    /// <returns>The ordered menu commands.</returns>
    internal static List<StrategyMenuCommand> BuildContextMenu(Mission mission)
    {
        bool hasMission = mission != null;
        bool canAbort = hasMission && !mission.IsWaitingForParticipants();
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.Encyclopedia,
                "Encyclopedia",
                hasMission
            ),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", hasMission),
            new StrategyMenuCommand(StrategyContextMenuActions.Abort, "Abort", canAbort),
        };
    }

    /// <summary>
    /// Reports whether one mission contains a participant in either role.
    /// </summary>
    /// <param name="mission">The mission to inspect.</param>
    /// <param name="participant">The participant to find.</param>
    /// <returns>True when the mission contains the participant.</returns>
    private static bool ContainsParticipant(Mission mission, IMissionParticipant participant)
    {
        return ContainsParticipant(mission?.MainParticipants, participant)
            || ContainsParticipant(mission?.DecoyParticipants, participant);
    }

    /// <summary>
    /// Finds one mission by persistent identity in visual order.
    /// </summary>
    /// <param name="missions">The ordered missions to search.</param>
    /// <param name="target">The mission identity to find.</param>
    /// <returns>The matching visual index, or negative one.</returns>
    private static int FindMissionIndex(IReadOnlyList<Mission> missions, Mission target)
    {
        if (target == null)
            return -1;

        for (int index = 0; index < missions.Count; index++)
        {
            if (
                string.Equals(
                    missions[index]?.InstanceID,
                    target.InstanceID,
                    StringComparison.Ordinal
                )
            )
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Finds the mission containing one participant identity in visual order.
    /// </summary>
    /// <param name="missions">The ordered missions to search.</param>
    /// <param name="participant">The participant identity to find.</param>
    /// <returns>The matching visual index, or negative one.</returns>
    private static int FindParticipantMissionIndex(
        IReadOnlyList<Mission> missions,
        IMissionParticipant participant
    )
    {
        for (int index = 0; index < missions.Count; index++)
        {
            if (ContainsParticipant(missions[index], participant))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Handles a mission-row press and preserves focus, targeting, and context-menu ordering.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="index">The pressed mission index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionPressed(
        MissionsWindowView view,
        int index,
        PointerEventData eventData
    )
    {
        if (eventData == null || !sessions.TryGetValue(view, out MissionsWindowSession session))
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
            session.Window.RequestFocus();
        if (
            targetingController.IsTargeting
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;
        if (!session.SelectMission(index))
            return;

        markDirty();
        if (eventData.button == PointerEventData.InputButton.Right)
            session.Window.RequestContext(eventData);
    }

    /// <summary>
    /// Routes a mission-row release into the active strategy targeting request.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="index">The released mission index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionReleased(
        MissionsWindowView view,
        int index,
        PointerEventData eventData
    )
    {
        TrySelectTarget(view, index);
    }

    /// <summary>
    /// Routes a drop over a mission row into the active strategy targeting request.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="index">The destination mission index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionDropped(
        MissionsWindowView view,
        int index,
        PointerEventData eventData
    )
    {
        TrySelectTarget(view, index);
    }

    /// <summary>
    /// Opens status for a double-clicked mission when targeting is inactive.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="index">The double-clicked mission index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleMissionDoubleClicked(
        MissionsWindowView view,
        int index,
        PointerEventData eventData
    )
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || targetingController.IsTargeting
            || !sessions.TryGetValue(view, out MissionsWindowSession session)
            || !session.SelectMission(index)
        )
            return;

        actions.OpenMissionsStatus(GetStatusTarget(view));
    }

    /// <summary>
    /// Captures a participant context target or focuses the window on left press.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="index">The pressed participant index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleParticipantPressed(
        MissionsWindowView view,
        int index,
        PointerEventData eventData
    )
    {
        if (eventData == null || !sessions.TryGetValue(view, out MissionsWindowSession session))
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            session.Window.RequestFocus();
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        session.CaptureParticipant(index);
        session.Window.RequestContext(eventData);
    }

    /// <summary>
    /// Routes a Missions surface click into planet-level targeting.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleSurfaceClicked(MissionsWindowView view, PointerEventData eventData)
    {
        TrySelectTarget(view, -1);
    }

    /// <summary>
    /// Changes the active participant role for the selected mission.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="role">The requested participant role.</param>
    private void HandleTabRequested(MissionsWindowView view, MissionParticipantRole role)
    {
        if (
            !MissionsWindowRenderData.OrderedRoles.Contains(role)
            || !sessions.TryGetValue(view, out MissionsWindowSession session)
            || session.SelectedMission == null
            || role == session.ActiveRole
        )
            return;

        session.SelectRole(role);
        markDirty();
    }

    /// <summary>
    /// Removes all state and subscriptions owned by a destroyed Missions view.
    /// </summary>
    /// <param name="view">The destroyed Missions view.</param>
    private void HandleViewDestroyed(MissionsWindowView view)
    {
        if (view == null)
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.MissionDoubleClicked -= HandleMissionDoubleClicked;
        view.MissionDropped -= HandleMissionDropped;
        view.MissionPressed -= HandleMissionPressed;
        view.MissionReleased -= HandleMissionReleased;
        view.ParticipantPressed -= HandleParticipantPressed;
        view.SurfaceClicked -= HandleSurfaceClicked;
        view.TabRequested -= HandleTabRequested;
        boundViews.Remove(view);
        sessions.Remove(view);
    }

    /// <summary>
    /// Routes a mission or planet target into the active targeting request.
    /// </summary>
    /// <param name="view">The source Missions view.</param>
    /// <param name="missionIndex">The mission index, or negative one for the planet.</param>
    /// <returns>True when the active targeting request accepted the target.</returns>
    private bool TrySelectTarget(MissionsWindowView view, int missionIndex)
    {
        if (
            !targetingController.IsTargeting
            || !sessions.TryGetValue(view, out MissionsWindowSession session)
        )
            return false;

        return targetingController.TrySelectTarget(
            new StrategyMissionTarget(session.Planet, session.GetMission(missionIndex))
        );
    }

    /// <summary>
    /// Reports whether an ordered participant collection contains one participant identity.
    /// </summary>
    /// <param name="participants">The participants to inspect.</param>
    /// <param name="target">The participant to find.</param>
    /// <returns>True when matching object or persistent identity is present.</returns>
    private static bool ContainsParticipant(
        IEnumerable<IMissionParticipant> participants,
        IMissionParticipant target
    )
    {
        string targetId = (target as IGameEntity)?.GetInstanceID();
        return (participants ?? Enumerable.Empty<IMissionParticipant>()).Any(participant =>
            participant == target
            || !string.IsNullOrEmpty(targetId)
                && string.Equals(
                    (participant as IGameEntity)?.GetInstanceID(),
                    targetId,
                    StringComparison.Ordinal
                )
        );
    }

    /// <summary>
    /// Finds the Missions window session representing a planet.
    /// </summary>
    /// <param name="planetId">The represented planet identifier.</param>
    /// <returns>The matching Missions session, or null when none is open.</returns>
    private MissionsWindowSession FindWindow(string planetId)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out MissionsWindowView view)
                && sessions.TryGetValue(view, out MissionsWindowSession session)
                && session.Planet?.Planet?.InstanceID == planetId
            )
                return session;
        }

        return null;
    }

    /// <summary>
    /// Gets the controller-owned session for an initialized Missions view.
    /// </summary>
    /// <param name="view">The initialized Missions view.</param>
    /// <returns>The controller-owned session.</returns>
    private MissionsWindowSession GetSession(MissionsWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (!sessions.TryGetValue(view, out MissionsWindowSession session))
        {
            throw new InvalidOperationException(
                "The Missions view has not been initialized by this controller."
            );
        }

        return session;
    }

    /// <summary>
    /// Resolves a projected planet against a refreshed sector collection.
    /// </summary>
    /// <param name="planet">The previous projected planet.</param>
    /// <param name="sectors">The refreshed visible sectors.</param>
    /// <returns>The refreshed planet, or null when it is no longer represented.</returns>
    private static GalaxyMapPlanet FindFreshPlanet(
        GalaxyMapPlanet planet,
        IReadOnlyList<GalaxyMapSector> sectors
    )
    {
        string planetId = planet?.Planet?.InstanceID;
        return planetId == null
            ? null
            : sectors
                .SelectMany(sector => sector.Planets)
                .FirstOrDefault(item => item.Planet?.InstanceID == planetId);
    }

    /// <summary>
    /// Ensures the controller has game-level actions before binding a view.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException("MissionsWindowController is not initialized.");
    }
}

/// <summary>
/// Stores controller-owned state for one live Missions window.
/// </summary>
internal sealed class MissionsWindowSession
{
    private string contextParticipantInstanceId;
    private string selectedMissionInstanceId;
    private int selectedMissionIndexHint = -1;

    /// <summary>
    /// Creates a Missions-window session for one planet projection.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="window">The owning window shell.</param>
    public MissionsWindowSession(GalaxyMapPlanet planet, UIWindow window)
    {
        Planet = planet ?? throw new ArgumentNullException(nameof(planet));
        Window = window ?? throw new ArgumentNullException(nameof(window));
        ReconcileSelection();
    }

    /// <summary>
    /// Gets the represented planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; private set; }

    /// <summary>
    /// Gets the owning window shell.
    /// </summary>
    public UIWindow Window { get; }

    /// <summary>
    /// Gets the active participant role.
    /// </summary>
    public MissionParticipantRole ActiveRole { get; private set; } = MissionParticipantRole.Agent;

    /// <summary>
    /// Gets the current visual index of the selected mission.
    /// </summary>
    public int SelectedMissionIndex => FindMissionIndex(selectedMissionInstanceId);

    /// <summary>
    /// Gets the current visual index of the context participant.
    /// </summary>
    public int ContextParticipantIndex => FindParticipantIndex(contextParticipantInstanceId);

    /// <summary>
    /// Gets the current missions in domain order.
    /// </summary>
    public IReadOnlyList<Mission> Missions => Planet.Planet.Missions;

    /// <summary>
    /// Gets the currently selected mission, or null.
    /// </summary>
    public Mission SelectedMission => GetMission(SelectedMissionIndex);

    /// <summary>
    /// Gets the selected mission's participants for the active role.
    /// </summary>
    public IReadOnlyList<IMissionParticipant> ActiveParticipants
    {
        get
        {
            Mission mission = SelectedMission;
            if (mission == null)
                return Array.Empty<IMissionParticipant>();

            return ActiveRole switch
            {
                MissionParticipantRole.Agent => mission.MainParticipants,
                MissionParticipantRole.Decoy => mission.DecoyParticipants,
                _ => throw new InvalidOperationException(
                    $"Unsupported mission participant role: {ActiveRole}."
                ),
            };
        }
    }

    /// <summary>
    /// Gets the participant captured by the latest context gesture, or null.
    /// </summary>
    public ISceneNode ContextParticipant
    {
        get
        {
            IReadOnlyList<IMissionParticipant> participants = ActiveParticipants;
            return ContextParticipantIndex >= 0 && ContextParticipantIndex < participants.Count
                ? participants[ContextParticipantIndex] as ISceneNode
                : null;
        }
    }

    /// <summary>
    /// Gets one mission by visual index.
    /// </summary>
    /// <param name="index">The requested visual index.</param>
    /// <returns>The mission, or null.</returns>
    public Mission GetMission(int index)
    {
        return index >= 0 && index < Missions.Count ? Missions[index] : null;
    }

    /// <summary>
    /// Reports whether one participant index exists in the active mission role.
    /// </summary>
    /// <param name="index">The participant index to validate.</param>
    /// <returns>True when the index exists.</returns>
    public bool IsParticipantIndexValid(int index)
    {
        return index >= 0 && index < ActiveParticipants.Count;
    }

    /// <summary>
    /// Selects one mission by visual index and clears participant context state.
    /// </summary>
    /// <param name="index">The requested mission index.</param>
    /// <returns>True when the requested mission exists.</returns>
    public bool SelectMission(int index)
    {
        Mission mission = GetMission(index);
        if (mission == null || string.IsNullOrEmpty(mission.InstanceID))
            return false;

        selectedMissionInstanceId = mission.InstanceID;
        selectedMissionIndexHint = index;
        contextParticipantInstanceId = null;
        return true;
    }

    /// <summary>
    /// Selects a mission and the participant role containing a requested target.
    /// </summary>
    /// <param name="missionIndex">The target mission's visual index.</param>
    /// <param name="role">The participant role containing the target.</param>
    /// <returns>True when the requested mission exists.</returns>
    public bool SelectTarget(int missionIndex, MissionParticipantRole role)
    {
        if (!SelectMission(missionIndex))
            return false;

        ActiveRole = role;
        return true;
    }

    /// <summary>
    /// Selects a participant role and clears context owned by the previous role.
    /// </summary>
    /// <param name="role">The requested participant role.</param>
    /// <returns>True when the active role changed.</returns>
    public bool SelectRole(MissionParticipantRole role)
    {
        if (role == ActiveRole)
            return false;

        ActiveRole = role;
        contextParticipantInstanceId = null;
        return true;
    }

    /// <summary>
    /// Captures one participant by its current visual index.
    /// </summary>
    /// <param name="index">The context-targeted participant index.</param>
    public void CaptureParticipant(int index)
    {
        contextParticipantInstanceId =
            index >= 0
            && index < ActiveParticipants.Count
            && ActiveParticipants[index] is ISceneNode participant
            && !string.IsNullOrEmpty(participant.InstanceID)
                ? participant.InstanceID
                : null;
    }

    /// <summary>
    /// Rebinds the session to a refreshed projection of its represented planet.
    /// </summary>
    /// <param name="planet">The refreshed represented planet.</param>
    public void RebindPlanet(GalaxyMapPlanet planet)
    {
        Planet = planet ?? throw new ArgumentNullException(nameof(planet));
        ReconcileSelection();
    }

    /// <summary>
    /// Clamps local selection to the current domain collections.
    /// </summary>
    public void ReconcileSelection()
    {
        if (Missions.Count == 0)
        {
            selectedMissionInstanceId = null;
            selectedMissionIndexHint = -1;
            contextParticipantInstanceId = null;
            return;
        }

        int currentIndex = SelectedMissionIndex;
        if (currentIndex < 0)
        {
            int fallbackIndex =
                selectedMissionIndexHint < 0
                    ? 0
                    : Math.Min(selectedMissionIndexHint, Missions.Count - 1);
            SelectMission(fallbackIndex);
            return;
        }

        selectedMissionIndexHint = currentIndex;
        if (ContextParticipantIndex < 0)
            contextParticipantInstanceId = null;
    }

    /// <summary>
    /// Finds the current visual index of a mission instance identifier.
    /// </summary>
    /// <param name="instanceId">The mission instance identifier.</param>
    /// <returns>The current visual index, or negative one when unavailable.</returns>
    private int FindMissionIndex(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return -1;

        for (int index = 0; index < Missions.Count; index++)
        {
            if (string.Equals(Missions[index]?.InstanceID, instanceId, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Finds the current visual index of a participant instance identifier.
    /// </summary>
    /// <param name="instanceId">The participant instance identifier.</param>
    /// <returns>The current visual index, or negative one when unavailable.</returns>
    private int FindParticipantIndex(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return -1;

        for (int index = 0; index < ActiveParticipants.Count; index++)
        {
            if (
                ActiveParticipants[index] is ISceneNode participant
                && string.Equals(participant.InstanceID, instanceId, StringComparison.Ordinal)
            )
                return index;
        }

        return -1;
    }
}
