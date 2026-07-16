using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs window-level actions requested by the Mission Create feature.
/// </summary>
public interface IMissionCreateWindowActions
{
    /// <summary>
    /// Rebuilds strategy state after a mission is created.
    /// </summary>
    void RefreshAfterMissionCreation();

    /// <summary>
    /// Opens the Encyclopedia from the Mission Create information control.
    /// </summary>
    void OpenMissionCreateInfo();
}

/// <summary>
/// Owns Mission Create sessions, participant assignment, and mission submission.
/// </summary>
public sealed class MissionCreateWindowController
{
    private readonly HashSet<MissionCreateWindowView> boundViews =
        new HashSet<MissionCreateWindowView>();
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<GameRoot> getGame;
    private readonly Func<MissionSystem> getMissionSystem;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Action markDirty;
    private readonly Action<string> playSfx;
    private readonly MissionCreateWindowProjector projector;
    private readonly Dictionary<MissionCreateWindowView, MissionCreateWindowSession> sessions =
        new Dictionary<MissionCreateWindowView, MissionCreateWindowSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private IMissionCreateWindowActions actions;

    /// <summary>
    /// Creates the Mission Create feature controller.
    /// </summary>
    /// <param name="getGame">Returns the active game.</param>
    /// <param name="getMissionSystem">Returns the active mission system.</param>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="playSfx">Plays a resolved mission acknowledgment sound.</param>
    /// <param name="windowLayer">Provides the authored Mission Create prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and registration.</param>
    /// <param name="getWindowPosition">Returns the authored Mission Create placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public MissionCreateWindowController(
        Func<GameRoot> getGame,
        Func<MissionSystem> getMissionSystem,
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.getMissionSystem =
            getMissionSystem ?? throw new ArgumentNullException(nameof(getMissionSystem));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        projector = new MissionCreateWindowProjector(getUIContext);
    }

    /// <summary>
    /// Supplies game-level Mission Create actions after the strategy controller graph is built.
    /// </summary>
    /// <param name="windowActions">The feature-specific Mission Create actions.</param>
    public void Initialize(IMissionCreateWindowActions windowActions)
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
    }

    /// <summary>
    /// Starts a Mission Create session from one valid strategy selection.
    /// </summary>
    /// <param name="view">The destination Mission Create view.</param>
    /// <param name="target">The selected mission target.</param>
    /// <param name="sourceItems">The source selection in visual order.</param>
    /// <param name="playerFactionId">The active player's faction identifier.</param>
    /// <returns>True when the source selection can create at least one mission.</returns>
    public bool TryInitializeWindow(
        MissionCreateWindowView view,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        UIWindow window = view == null ? null : view.GetComponent<UIWindow>();
        if (view == null || window == null || target == null || target.Item is Fleet)
            return false;

        List<IMissionParticipant> participants = GetMissionSourceParticipants(
            sourceItems,
            playerFactionId
        );
        if (participants.Count == 0)
            return false;

        List<StrategyMissionChoice> choices = BuildMissionChoices(participants, target);
        if (choices.Count == 0)
            return false;

        BindWindow(view);
        sessions[view] = new MissionCreateWindowSession(window, target, choices, participants);
        return true;
    }

    /// <summary>
    /// Replaces the current Mission Create window with a requested target and participant set.
    /// </summary>
    /// <param name="target">The selected mission target.</param>
    /// <param name="sourceItems">The source selection in visual order.</param>
    public void Open(StrategyMissionTarget target, IReadOnlyList<ISceneNode> sourceItems)
    {
        MissionCreateWindowView existing = FindWindow();
        if (
            existing != null
            && sessions.TryGetValue(existing, out MissionCreateWindowSession existingSession)
        )
            closeWindow(existingSession.Window);

        Vector2Int position = getWindowPosition();
        UIWindow window = windowManager.CreateWindow(
            windowLayer.MissionCreateWindowPrefab,
            windowLayer.GetWindowParent(true),
            $"MissionCreateWindow-{target?.Item?.GetDisplayName() ?? target?.Planet?.Planet?.GetDisplayName() ?? "UnknownTarget"}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.MissionCreateWindowPrefab),
            true,
            true,
            false,
            false,
            out MissionCreateWindowView view
        );
        if (
            !TryInitializeWindow(
                view,
                target,
                sourceItems,
                getGame()?.GetPlayerFaction()?.InstanceID
            )
        )
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Renders every registered Mission Create window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out MissionCreateWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Subscribes to one authored Mission Create view exactly once.
    /// </summary>
    /// <param name="view">The Mission Create view to bind.</param>
    public void BindWindow(MissionCreateWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.CancelRequested += HandleCancelRequested;
        view.ConfirmRequested += HandleConfirmRequested;
        view.Destroyed += HandleViewDestroyed;
        view.DropdownDismissRequested += HandleDropdownDismissRequested;
        view.DropdownItemRequested += HandleDropdownItemRequested;
        view.DropdownToggleRequested += HandleDropdownToggleRequested;
        view.InfoRequested += HandleInfoRequested;
        view.MoveParticipantsRequested += HandleMoveParticipantsRequested;
        view.ParticipantClicked += HandleParticipantClicked;
        view.ParticipantPressed += HandleParticipantPressed;
        view.TabRequested += HandleTabRequested;
    }

    /// <summary>
    /// Projects current session state and renders one Mission Create window.
    /// </summary>
    /// <param name="view">The destination Mission Create view.</param>
    /// <param name="window">The owning window shell.</param>
    public void RenderWindow(MissionCreateWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        MissionCreateWindowSession session = GetSession(view);

        view.Render(projector.Build(session, window));
    }

    /// <summary>
    /// Focuses and closes a cancelled Mission Create session.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    private void HandleCancelRequested(MissionCreateWindowView view)
    {
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
            return;

        session.Window.RequestFocus();
        closeWindow(session.Window);
    }

    /// <summary>
    /// Validates and starts the configured mission when confirmation is requested.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    private void HandleConfirmRequested(MissionCreateWindowView view)
    {
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
            return;

        session.Window.RequestFocus();
        if (TryStartMission(session))
        {
            actions.RefreshAfterMissionCreation();
            closeWindow(session.Window);
        }
        else
            markDirty();
    }

    /// <summary>
    /// Closes an open mission dropdown after an outside click.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    private void HandleDropdownDismissRequested(MissionCreateWindowView view)
    {
        if (
            !sessions.TryGetValue(view, out MissionCreateWindowSession session)
            || !session.DropdownOpen
        )
            return;

        session.Window.RequestFocus();
        session.DismissDropdown();
        markDirty();
    }

    /// <summary>
    /// Selects one mission choice and closes the mission dropdown.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    /// <param name="index">The requested mission-choice index.</param>
    private void HandleDropdownItemRequested(MissionCreateWindowView view, int index)
    {
        if (
            !sessions.TryGetValue(view, out MissionCreateWindowSession session)
            || !session.IsMissionIndexValid(index)
        )
            return;

        session.Window.RequestFocus();
        session.SelectMission(index);
        markDirty();
    }

    /// <summary>
    /// Toggles mission-choice dropdown visibility.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    private void HandleDropdownToggleRequested(MissionCreateWindowView view)
    {
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
            return;

        session.Window.RequestFocus();
        session.ToggleDropdown();
        markDirty();
    }

    /// <summary>
    /// Focuses the window and opens the Encyclopedia from the information control.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    private void HandleInfoRequested(MissionCreateWindowView view)
    {
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
            return;

        session.Window.RequestFocus();
        actions.OpenMissionCreateInfo();
    }

    /// <summary>
    /// Moves the selected participants out of one requested source role.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    /// <param name="sourceRole">The semantic source role.</param>
    private void HandleMoveParticipantsRequested(
        MissionCreateWindowView view,
        MissionParticipantRole sourceRole
    )
    {
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
            return;

        session.Window.RequestFocus();
        if (!session.MoveSelectedParticipants(sourceRole))
            return;

        markDirty();
    }

    /// <summary>
    /// Applies participant selection or double-click transfer within one role list.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    /// <param name="role">The semantic participant role.</param>
    /// <param name="index">The selected participant index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleParticipantClicked(
        MissionCreateWindowView view,
        MissionParticipantRole role,
        int index,
        PointerEventData eventData
    )
    {
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
            return;

        session.Window.RequestFocus();
        if (!session.SelectParticipant(role, index, eventData?.clickCount ?? 0))
            return;

        markDirty();
    }

    /// <summary>
    /// Preserves participant-row focus and fallback context-menu behavior.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    /// <param name="role">The semantic participant role.</param>
    /// <param name="index">The pressed participant index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleParticipantPressed(
        MissionCreateWindowView view,
        MissionParticipantRole role,
        int index,
        PointerEventData eventData
    )
    {
        if (
            eventData == null
            || !sessions.TryGetValue(view, out MissionCreateWindowSession session)
            || !session.IsParticipantIndexValid(role, index)
        )
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
            session.Window.RequestFocus();
        else if (eventData.button == PointerEventData.InputButton.Right)
            session.Window.RequestContext(eventData);
    }

    /// <summary>
    /// Changes the active Mission Create workflow tab and closes the dropdown.
    /// </summary>
    /// <param name="view">The requesting Mission Create view.</param>
    /// <param name="tab">The requested workflow tab.</param>
    private void HandleTabRequested(MissionCreateWindowView view, MissionCreateWindowTab tab)
    {
        if (
            !sessions.TryGetValue(view, out MissionCreateWindowSession session)
            || !session.IsTabValid(tab)
        )
            return;

        session.Window.RequestFocus();
        session.SelectTab(tab);
        markDirty();
    }

    /// <summary>
    /// Removes all state and subscriptions owned by a destroyed Mission Create view.
    /// </summary>
    /// <param name="view">The destroyed Mission Create view.</param>
    private void HandleViewDestroyed(MissionCreateWindowView view)
    {
        if (view == null)
            return;

        view.CancelRequested -= HandleCancelRequested;
        view.ConfirmRequested -= HandleConfirmRequested;
        view.Destroyed -= HandleViewDestroyed;
        view.DropdownDismissRequested -= HandleDropdownDismissRequested;
        view.DropdownItemRequested -= HandleDropdownItemRequested;
        view.DropdownToggleRequested -= HandleDropdownToggleRequested;
        view.InfoRequested -= HandleInfoRequested;
        view.MoveParticipantsRequested -= HandleMoveParticipantsRequested;
        view.ParticipantClicked -= HandleParticipantClicked;
        view.ParticipantPressed -= HandleParticipantPressed;
        view.TabRequested -= HandleTabRequested;
        boundViews.Remove(view);
        sessions.Remove(view);
    }

    /// <summary>
    /// Gets mission-capable participants from the source selection.
    /// </summary>
    /// <param name="sourceItems">The selected source scene nodes.</param>
    /// <param name="playerFactionId">The active player's faction identifier.</param>
    /// <returns>The ordered mission participants, or an empty list.</returns>
    private List<IMissionParticipant> GetMissionSourceParticipants(
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = sourceItems?.ToList() ?? new List<ISceneNode>();
        if (!StrategyContextMenuAvailability.CanCreateMission(items, playerFactionId))
            return new List<IMissionParticipant>();

        return items.OfType<IMissionParticipant>().ToList();
    }

    /// <summary>
    /// Builds mission choices available to one participant selection and target.
    /// </summary>
    /// <param name="participants">The source mission participants.</param>
    /// <param name="target">The selected mission target.</param>
    /// <returns>The available choices in mission-system order.</returns>
    private List<StrategyMissionChoice> BuildMissionChoices(
        IReadOnlyList<IMissionParticipant> participants,
        StrategyMissionTarget target
    )
    {
        List<StrategyMissionChoice> choices = new List<StrategyMissionChoice>();
        if (target?.Planet?.Planet == null)
            return choices;

        MissionStartRequest request = new MissionStartRequest
        {
            Location = target.Planet.Planet,
            SelectedTarget = target.Item,
            TargetOfficer = target.Item as Officer,
            MainParticipants = participants.ToList(),
            DecoyParticipants = new List<IMissionParticipant>(),
        };
        foreach (MissionOption option in getMissionSystem().GetAvailableMissionOptions(request))
            choices.Add(new StrategyMissionChoice(option));

        return choices;
    }

    /// <summary>
    /// Validates and starts the mission configured by one Mission Create session.
    /// </summary>
    /// <param name="session">The source Mission Create session.</param>
    /// <returns>True when the game accepted and initiated the mission.</returns>
    private bool TryStartMission(MissionCreateWindowSession session)
    {
        StrategyMissionChoice choice = session.SelectedChoice;
        Planet missionPlanet = session.Target?.Planet?.Planet;
        if (choice == null || session.Agents.Count == 0 || missionPlanet == null)
            return false;

        MissionStartRequest request = new MissionStartRequest
        {
            MissionTypeID = choice.MissionTypeID,
            Location = missionPlanet,
            SelectedTarget = session.Target.GetSpecificMissionTarget(choice.MissionTypeID),
            TargetOfficer = session.Target.GetMissionTargetOfficer(choice.MissionTypeID),
            Discipline = choice.Discipline,
            MainParticipants = session.Agents.ToList(),
            DecoyParticipants = session.Decoys.ToList(),
        };
        MissionSystem missionSystem = getMissionSystem();
        if (!missionSystem.CanCreateMission(request) || !missionSystem.InitiateMission(request))
            return false;

        PlayMissionStartVoice(session.Agents, session.Decoys);
        return true;
    }

    /// <summary>
    /// Plays the first available participant order acknowledgment after mission creation.
    /// </summary>
    /// <param name="agents">The mission's primary participants.</param>
    /// <param name="decoys">The mission's decoy participants.</param>
    private void PlayMissionStartVoice(
        IReadOnlyList<IMissionParticipant> agents,
        IReadOnlyList<IMissionParticipant> decoys
    )
    {
        IEnumerable<IMissionParticipant> participants =
            agents ?? Enumerable.Empty<IMissionParticipant>();
        if (decoys != null)
            participants = participants.Concat(decoys);

        Officer officer = participants
            .OfType<Officer>()
            .FirstOrDefault(participant => participant.HasVoicePath(OfficerVoiceLineType.Order));
        if (officer == null)
            return;

        string voicePath = officer.GetVoicePath(OfficerVoiceLineType.Order, getGame()?.Random);
        if (!string.IsNullOrEmpty(voicePath))
            playSfx(voicePath);
    }

    /// <summary>
    /// Finds the registered Mission Create view.
    /// </summary>
    /// <returns>The registered view, or null when Mission Create is closed.</returns>
    private MissionCreateWindowView FindWindow()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out MissionCreateWindowView view))
                return view;
        }

        return null;
    }

    /// <summary>
    /// Gets the controller-owned session for an initialized Mission Create view.
    /// </summary>
    /// <param name="view">The initialized Mission Create view.</param>
    /// <returns>The controller-owned session.</returns>
    private MissionCreateWindowSession GetSession(MissionCreateWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (!sessions.TryGetValue(view, out MissionCreateWindowSession session))
        {
            throw new InvalidOperationException(
                "The Mission Create view has not been initialized by this controller."
            );
        }

        return session;
    }

    /// <summary>
    /// Ensures the controller has game-level actions before binding a view.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                "MissionCreateWindowController is not initialized."
            );
    }
}

/// <summary>
/// Stores controller-owned state for one live Mission Create window.
/// </summary>
internal sealed class MissionCreateWindowSession
{
    private const int _doubleClickCount = 2;

    private readonly List<IMissionParticipant> agents = new List<IMissionParticipant>();
    private readonly List<StrategyMissionChoice> choices = new List<StrategyMissionChoice>();
    private readonly List<IMissionParticipant> decoys = new List<IMissionParticipant>();
    private readonly IReadOnlyList<IMissionParticipant> readOnlyAgents;
    private readonly IReadOnlyList<StrategyMissionChoice> readOnlyChoices;
    private readonly IReadOnlyList<IMissionParticipant> readOnlyDecoys;
    private readonly HashSet<int> selectedAgents = new HashSet<int>();
    private readonly HashSet<int> selectedDecoys = new HashSet<int>();

    /// <summary>
    /// Creates a Mission Create session from validated target, choices, and participants.
    /// </summary>
    /// <param name="window">The owning Mission Create window.</param>
    /// <param name="target">The selected mission target.</param>
    /// <param name="choices">The available mission choices.</param>
    /// <param name="participants">The initial primary participants.</param>
    public MissionCreateWindowSession(
        UIWindow window,
        StrategyMissionTarget target,
        IEnumerable<StrategyMissionChoice> choices,
        IEnumerable<IMissionParticipant> participants
    )
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        readOnlyAgents = agents.AsReadOnly();
        readOnlyChoices = this.choices.AsReadOnly();
        readOnlyDecoys = decoys.AsReadOnly();
        this.choices.AddRange(choices ?? throw new ArgumentNullException(nameof(choices)));
        agents.AddRange(participants ?? throw new ArgumentNullException(nameof(participants)));
        SelectedMissionIndex = this.choices.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Gets the target.
    /// </summary>
    public StrategyMissionTarget Target { get; }

    /// <summary>
    /// Gets the owning Mission Create window.
    /// </summary>
    public UIWindow Window { get; }

    /// <summary>
    /// Gets the choices.
    /// </summary>
    public IReadOnlyList<StrategyMissionChoice> Choices => readOnlyChoices;

    /// <summary>
    /// Gets the agents.
    /// </summary>
    public IReadOnlyList<IMissionParticipant> Agents => readOnlyAgents;

    /// <summary>
    /// Gets the decoys.
    /// </summary>
    public IReadOnlyList<IMissionParticipant> Decoys => readOnlyDecoys;

    /// <summary>
    /// Gets the selected agents.
    /// </summary>
    public IReadOnlyCollection<int> SelectedAgents => selectedAgents;

    /// <summary>
    /// Gets the selected decoys.
    /// </summary>
    public IReadOnlyCollection<int> SelectedDecoys => selectedDecoys;

    /// <summary>
    /// Gets the active tab.
    /// </summary>
    public MissionCreateWindowTab ActiveTab { get; private set; } = MissionCreateWindowTab.Mission;

    /// <summary>
    /// Gets the selected mission index.
    /// </summary>
    public int SelectedMissionIndex { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the dropdown is open.
    /// </summary>
    public bool DropdownOpen { get; private set; }

    /// <summary>
    /// Gets the currently selected mission choice, or null.
    /// </summary>
    public StrategyMissionChoice SelectedChoice =>
        SelectedMissionIndex >= 0 && SelectedMissionIndex < Choices.Count
            ? Choices[SelectedMissionIndex]
            : null;

    /// <summary>
    /// Closes the mission dropdown when it is open.
    /// </summary>
    /// <returns>True when the dropdown changed.</returns>
    public bool DismissDropdown()
    {
        if (!DropdownOpen)
            return false;

        DropdownOpen = false;
        return true;
    }

    /// <summary>
    /// Selects one available mission and closes the mission dropdown.
    /// </summary>
    /// <param name="index">The mission-choice index.</param>
    /// <returns>True when the requested mission exists.</returns>
    public bool SelectMission(int index)
    {
        if (!IsMissionIndexValid(index))
            return false;

        SelectedMissionIndex = index;
        DropdownOpen = false;
        return true;
    }

    /// <summary>
    /// Reports whether one mission-choice index exists.
    /// </summary>
    /// <param name="index">The mission-choice index.</param>
    /// <returns>True when the requested mission exists.</returns>
    public bool IsMissionIndexValid(int index)
    {
        return index >= 0 && index < choices.Count;
    }

    /// <summary>
    /// Toggles mission-dropdown visibility.
    /// </summary>
    public void ToggleDropdown()
    {
        DropdownOpen = !DropdownOpen;
    }

    /// <summary>
    /// Moves the selected participants out of one source role.
    /// </summary>
    /// <param name="sourceRole">The semantic source role.</param>
    /// <returns>True when at least one selected participant moved.</returns>
    public bool MoveSelectedParticipants(MissionParticipantRole sourceRole)
    {
        switch (sourceRole)
        {
            case MissionParticipantRole.Agent:
                return MoveSelectedParticipants(agents, decoys, selectedAgents);
            case MissionParticipantRole.Decoy:
                return MoveSelectedParticipants(decoys, agents, selectedDecoys);
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies participant selection or double-click transfer within one role list.
    /// </summary>
    /// <param name="role">The semantic participant role.</param>
    /// <param name="index">The requested participant index.</param>
    /// <param name="clickCount">The pointer click count.</param>
    /// <returns>True when the requested participant exists and was processed.</returns>
    public bool SelectParticipant(MissionParticipantRole role, int index, int clickCount)
    {
        switch (role)
        {
            case MissionParticipantRole.Agent:
                return SelectParticipant(agents, decoys, selectedAgents, index, clickCount);
            case MissionParticipantRole.Decoy:
                return SelectParticipant(decoys, agents, selectedDecoys, index, clickCount);
            default:
                return false;
        }
    }

    /// <summary>
    /// Reports whether one row index exists in the requested participant role.
    /// </summary>
    /// <param name="role">The semantic participant role.</param>
    /// <param name="index">The row index to validate.</param>
    /// <returns>True when the requested participant exists.</returns>
    public bool IsParticipantIndexValid(MissionParticipantRole role, int index)
    {
        return role switch
        {
            MissionParticipantRole.Agent => index >= 0 && index < agents.Count,
            MissionParticipantRole.Decoy => index >= 0 && index < decoys.Count,
            _ => false,
        };
    }

    /// <summary>
    /// Changes the active workflow tab and closes the mission dropdown.
    /// </summary>
    /// <param name="tab">The requested workflow tab.</param>
    /// <returns>True when the requested tab exists.</returns>
    public bool SelectTab(MissionCreateWindowTab tab)
    {
        if (!IsTabValid(tab))
            return false;

        ActiveTab = tab;
        DropdownOpen = false;
        return true;
    }

    /// <summary>
    /// Reports whether one workflow tab exists.
    /// </summary>
    /// <param name="tab">The requested workflow tab.</param>
    /// <returns>True when the requested tab exists.</returns>
    public bool IsTabValid(MissionCreateWindowTab tab)
    {
        return MissionCreateWindowRenderData.OrderedTabs.Contains(tab);
    }

    /// <summary>
    /// Moves selected participants between role lists while preserving source order.
    /// </summary>
    /// <param name="source">The source role list.</param>
    /// <param name="destination">The destination role list.</param>
    /// <param name="selection">The selected source indices.</param>
    /// <returns>True when at least one selected participant moved.</returns>
    private static bool MoveSelectedParticipants(
        List<IMissionParticipant> source,
        List<IMissionParticipant> destination,
        HashSet<int> selection
    )
    {
        if (source.Count == 0 || selection.Count == 0)
            return false;

        List<IMissionParticipant> remaining = new List<IMissionParticipant>();
        for (int index = 0; index < source.Count; index++)
        {
            if (selection.Contains(index))
                destination.Add(source[index]);
            else
                remaining.Add(source[index]);
        }

        source.Clear();
        source.AddRange(remaining);
        selection.Clear();
        return true;
    }

    /// <summary>
    /// Applies selection or double-click transfer to one participant role.
    /// </summary>
    /// <param name="source">The source participant role.</param>
    /// <param name="destination">The destination participant role.</param>
    /// <param name="selection">The selected source indices.</param>
    /// <param name="index">The requested source index.</param>
    /// <param name="clickCount">The pointer click count.</param>
    /// <returns>True when the requested participant exists and was processed.</returns>
    private static bool SelectParticipant(
        List<IMissionParticipant> source,
        List<IMissionParticipant> destination,
        HashSet<int> selection,
        int index,
        int clickCount
    )
    {
        if (index < 0 || index >= source.Count)
            return false;

        if (clickCount >= _doubleClickCount)
        {
            selection.Clear();
            selection.Add(index);
            MoveSelectedParticipants(source, destination, selection);
            return true;
        }

        SelectableListSelection.SelectRangeItem(selection, index, source.Count);
        return true;
    }
}
