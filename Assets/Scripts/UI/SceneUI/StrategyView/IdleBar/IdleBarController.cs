using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Defines strategy-screen actions requested by the idle-bar feature.
/// </summary>
public interface IIdleBarActions
{
    /// <summary>Opens the strategy location represented by an idle-bar entry.</summary>
    /// <param name="target">The selected strategy entity.</param>
    void OpenIdleBarTarget(ISceneNode target);

    /// <summary>Requests a strategy render after idle-bar state changes.</summary>
    void RequestIdleBarRender();
}

/// <summary>
/// Reads and changes whether strategy entities appear in the idle bar.
/// </summary>
public interface IIdleBarTrackingActions
{
    /// <summary>Gets whether idle-bar controls should be exposed.</summary>
    bool IsIdleBarEnabled { get; }

    /// <summary>Reports whether an entity appears in the idle bar.</summary>
    /// <param name="entity">The entity whose tracking state is requested.</param>
    /// <returns><see langword="true"/> when the entity is tracked.</returns>
    bool IsIdleBarTracked(ISceneNode entity);

    /// <summary>Changes whether an entity appears in the idle bar.</summary>
    /// <param name="entity">The entity whose tracking state should change.</param>
    void ToggleIdleBarTracking(ISceneNode entity);
}

/// <summary>
/// Owns idle-bar projection, tracking state, and semantic action routing.
/// </summary>
public sealed class IdleBarController : IIdleBarTrackingActions
{
    private readonly Func<Faction> getPlayerFaction;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<bool> getVisibility;
    private readonly HashSet<string> ignoredEntityIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly IdleBarProjector projector;
    private readonly Func<string, ISceneNode> resolveEntity;

    private IIdleBarActions actions;
    private IdleBarView view;

    /// <inheritdoc />
    public bool IsIdleBarEnabled => getVisibility();

    /// <summary>
    /// Creates an idle-bar controller backed by current strategy state.
    /// </summary>
    /// <param name="getPlayerFaction">Returns the current player faction.</param>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="getVisibility">Returns whether the experimental feature is enabled.</param>
    /// <param name="resolveEntity">Resolves an entity by its stable instance identifier.</param>
    public IdleBarController(
        Func<Faction> getPlayerFaction,
        Func<UIContext> getUIContext,
        Func<bool> getVisibility,
        Func<string, ISceneNode> resolveEntity
    )
    {
        this.getPlayerFaction =
            getPlayerFaction ?? throw new ArgumentNullException(nameof(getPlayerFaction));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.getVisibility =
            getVisibility ?? throw new ArgumentNullException(nameof(getVisibility));
        this.resolveEntity =
            resolveEntity ?? throw new ArgumentNullException(nameof(resolveEntity));
        projector = new IdleBarProjector(getUIContext);
    }

    /// <summary>
    /// Connects the controller to strategy-screen actions.
    /// </summary>
    /// <param name="nextActions">The strategy-screen action boundary.</param>
    public void Initialize(IIdleBarActions nextActions)
    {
        actions = nextActions ?? throw new ArgumentNullException(nameof(nextActions));
    }

    /// <summary>
    /// Subscribes the controller to an authored idle-bar view exactly once.
    /// </summary>
    /// <param name="nextView">The authored idle-bar view.</param>
    public void BindView(IdleBarView nextView)
    {
        if (nextView == null)
            throw new ArgumentNullException(nameof(nextView));

        EnsureInitialized();
        if (ReferenceEquals(view, nextView))
            return;

        ReleaseView();
        view = nextView;
        view.Destroyed += HandleViewDestroyed;
        view.EntrySelected += HandleEntrySelected;
        view.EntryUntrackRequested += HandleEntryUntrackRequested;
    }

    /// <summary>
    /// Projects and renders the current idle-bar presentation.
    /// </summary>
    public void Render()
    {
        IdleBarView requiredView = GetRequiredView();
        if (!IsIdleBarEnabled)
        {
            requiredView.Render(new IdleBarRenderData(false, null, new RectInt()));
            return;
        }

        RectInt desktopBounds = GetDesktopBounds();
        IdleBarRenderData projected = projector.Project(getPlayerFaction(), desktopBounds);
        requiredView.Render(
            new IdleBarRenderData(
                true,
                projected
                    .Entries.Where(entry => !ignoredEntityIds.Contains(entry.Entity?.InstanceID))
                    .ToList(),
                projected.DesktopBounds
            )
        );
    }

    /// <summary>
    /// Resolves the authored strategy desktop bounds used to place the idle bar.
    /// </summary>
    /// <returns>The source-space desktop bounds.</returns>
    private RectInt GetDesktopBounds()
    {
        SourceRectLayout bounds = getUIContext()
            ?.GetPlayerFactionTheme()
            ?.StrategyWindowPlacements?.WindowBounds;
        if (bounds == null)
        {
            throw new MissingReferenceException(
                "StrategyWindowPlacements/WindowBounds is missing."
            );
        }

        return new RectInt(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Clears per-game tracking choices after the active game changes.
    /// </summary>
    public void ResetSession()
    {
        ignoredEntityIds.Clear();
    }

    /// <inheritdoc />
    public bool IsIdleBarTracked(ISceneNode entity)
    {
        return !string.IsNullOrEmpty(entity?.InstanceID)
            && !ignoredEntityIds.Contains(entity.InstanceID);
    }

    /// <inheritdoc />
    public void ToggleIdleBarTracking(ISceneNode entity)
    {
        if (string.IsNullOrEmpty(entity?.InstanceID))
            return;

        if (!ignoredEntityIds.Remove(entity.InstanceID))
            ignoredEntityIds.Add(entity.InstanceID);
        actions.RequestIdleBarRender();
    }

    /// <summary>
    /// Resolves and opens the selected idle-bar entity.
    /// </summary>
    /// <param name="instanceId">The selected entity identifier.</param>
    private void HandleEntrySelected(string instanceId)
    {
        ISceneNode target = string.IsNullOrEmpty(instanceId) ? null : resolveEntity(instanceId);
        if (target != null)
            actions.OpenIdleBarTarget(target);
    }

    /// <summary>
    /// Stops tracking the secondary-clicked idle-bar entity.
    /// </summary>
    /// <param name="instanceId">The untracked entity identifier.</param>
    private void HandleEntryUntrackRequested(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || !ignoredEntityIds.Add(instanceId))
            return;

        actions.RequestIdleBarRender();
    }

    /// <summary>
    /// Releases subscriptions when the bound authored view is destroyed.
    /// </summary>
    /// <param name="destroyedView">The destroyed idle-bar view.</param>
    private void HandleViewDestroyed(IdleBarView destroyedView)
    {
        if (ReferenceEquals(view, destroyedView))
            ReleaseView();
    }

    /// <summary>
    /// Releases subscriptions from the currently bound authored view.
    /// </summary>
    private void ReleaseView()
    {
        if (ReferenceEquals(view, null))
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.EntrySelected -= HandleEntrySelected;
        view.EntryUntrackRequested -= HandleEntryUntrackRequested;
        view = null;
    }

    /// <summary>
    /// Verifies action routing is available before view binding or interaction.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
        {
            throw new InvalidOperationException(
                $"{nameof(IdleBarController)} must be initialized before binding a view."
            );
        }
    }

    /// <summary>
    /// Gets the bound authored view and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The bound authored idle-bar view.</returns>
    private IdleBarView GetRequiredView()
    {
        EnsureInitialized();
        return view
            ?? throw new InvalidOperationException(
                $"{nameof(IdleBarController)} must bind a view before rendering."
            );
    }
}
