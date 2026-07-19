using System;

/// <summary>
/// Defines strategy-screen actions requested by the galactic-information selector.
/// </summary>
public interface IGalacticInformationDisplayActions
{
    /// <summary>
    /// Requests a strategy render after selector state or the active filter changes.
    /// </summary>
    void RequestGalacticInformationRender();
}

/// <summary>
/// Owns galactic-information selector state, filter selection, audio policy, and view routing.
/// </summary>
public sealed class GalacticInformationDisplayController : ICancelable
{
    private readonly GalacticInformationDisplayProjector projector;
    private readonly Action<string> playSfx;

    private IGalacticInformationDisplayActions actions;
    private GalacticInformationDisplayView displayView;
    private GalacticInformationLegendView legendView;
    private GalacticInformationFilterMode filterMode = GalacticInformationFilterMode.DisplayOff;
    private int activeCategoryIndex = -1;
    private int hoveredFilterIndex = -1;
    private bool displayOffHovered;
    private bool open;

    /// <summary>
    /// Creates a galactic-information controller with current-context and audio dependencies.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    public GalacticInformationDisplayController(
        Func<UIContext> getUIContext,
        Action<string> playSfx
    )
    {
        projector = new GalacticInformationDisplayProjector(getUIContext);
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
    }

    public bool Open => open;

    public GalacticInformationFilterMode FilterMode => filterMode;

    /// <summary>
    /// Connects the controller to strategy-screen actions.
    /// </summary>
    /// <param name="nextActions">The strategy-screen action boundary.</param>
    public void Initialize(IGalacticInformationDisplayActions nextActions)
    {
        actions = nextActions ?? throw new ArgumentNullException(nameof(nextActions));
    }

    /// <summary>
    /// Subscribes the controller to the authored selector and retained legend views.
    /// </summary>
    /// <param name="nextDisplayView">The authored selector view.</param>
    /// <param name="nextLegendView">The authored retained legend view.</param>
    public void BindViews(
        GalacticInformationDisplayView nextDisplayView,
        GalacticInformationLegendView nextLegendView
    )
    {
        if (nextDisplayView == null)
            throw new ArgumentNullException(nameof(nextDisplayView));
        if (nextLegendView == null)
            throw new ArgumentNullException(nameof(nextLegendView));

        EnsureInitialized();
        if (
            ReferenceEquals(displayView, nextDisplayView)
            && ReferenceEquals(legendView, nextLegendView)
        )
        {
            return;
        }

        ReleaseViews();
        displayView = nextDisplayView;
        legendView = nextLegendView;
        BindDisplayView();
        legendView.CloseRequested += HandleLegendCloseRequested;
        legendView.Destroyed += HandleLegendViewDestroyed;
        RenderDisplay();
        legendView.Hide();
    }

    /// <summary>
    /// Opens the selector and resets transient category and hover state.
    /// </summary>
    public void Show()
    {
        GetRequiredDisplayView();
        if (open)
            return;

        open = true;
        ResetTransientState();
        RenderDisplay();
    }

    /// <summary>
    /// Hides the selector and retained legend without changing the active filter.
    /// </summary>
    public void Hide()
    {
        open = false;
        ResetTransientState();
        if (displayView != null)
            RenderDisplay();
        legendView?.Hide();
    }

    /// <summary>
    /// Cancels the open selector using the same dismissal behavior as an outside click.
    /// </summary>
    /// <returns>True when an open selector was dismissed.</returns>
    public bool TryCancel()
    {
        if (!open)
            return false;

        DismissSelector();
        return true;
    }

    /// <summary>
    /// Selects one semantic filter, closes selector overlays, and requests map projection.
    /// </summary>
    /// <param name="mode">The selected filter mode.</param>
    internal void SelectFilter(GalacticInformationFilterMode mode)
    {
        bool changed = filterMode != mode;
        filterMode = mode;
        Hide();
        if (changed && mode != GalacticInformationFilterMode.DisplayOff)
            playSfx(StrategyUISoundPaths.GalacticInformationControl);

        actions.RequestGalacticInformationRender();
    }

    /// <summary>
    /// Opens one category submenu and clears competing hover state.
    /// </summary>
    /// <param name="categoryIndex">The requested category index.</param>
    private void HandleCategoryRequested(int categoryIndex)
    {
        if (!open || categoryIndex < 0)
            return;

        activeCategoryIndex = categoryIndex;
        hoveredFilterIndex = -1;
        displayOffHovered = false;
        RenderDisplay();
    }

    /// <summary>
    /// Highlights the selector's display-off row and closes any category submenu.
    /// </summary>
    private void HandleDisplayOffEntered()
    {
        if (!open)
            return;

        activeCategoryIndex = -1;
        hoveredFilterIndex = -1;
        displayOffHovered = true;
        RenderDisplay();
    }

    /// <summary>
    /// Clears the selector's display-off hover state.
    /// </summary>
    private void HandleDisplayOffExited()
    {
        if (!open || !displayOffHovered)
            return;

        displayOffHovered = false;
        RenderDisplay();
    }

    /// <summary>
    /// Selects display-off mode from the selector row.
    /// </summary>
    private void HandleDisplayOffSelected()
    {
        SelectFilter(GalacticInformationFilterMode.DisplayOff);
    }

    /// <summary>
    /// Highlights one filter row in the active category submenu.
    /// </summary>
    /// <param name="categoryIndex">The row's owning category index.</param>
    /// <param name="filterIndex">The hovered filter index.</param>
    private void HandleFilterEntered(int categoryIndex, int filterIndex)
    {
        if (!open || activeCategoryIndex != categoryIndex || filterIndex < 0)
            return;

        hoveredFilterIndex = filterIndex;
        RenderDisplay();
    }

    /// <summary>
    /// Clears one filter row's hover state.
    /// </summary>
    /// <param name="categoryIndex">The row's owning category index.</param>
    /// <param name="filterIndex">The exited filter index.</param>
    private void HandleFilterExited(int categoryIndex, int filterIndex)
    {
        if (!open || activeCategoryIndex != categoryIndex || hoveredFilterIndex != filterIndex)
        {
            return;
        }

        hoveredFilterIndex = -1;
        RenderDisplay();
    }

    /// <summary>
    /// Selects one filter row emitted by the active submenu.
    /// </summary>
    /// <param name="mode">The selected filter mode.</param>
    private void HandleFilterSelected(GalacticInformationFilterMode mode)
    {
        SelectFilter(mode);
    }

    /// <summary>
    /// Dismisses the selector after an outside click.
    /// </summary>
    private void HandleDismissRequested()
    {
        if (open)
            DismissSelector();
    }

    /// <summary>
    /// Hides the retained legend when its close control is clicked.
    /// </summary>
    private void HandleLegendCloseRequested()
    {
        legendView?.Hide();
        actions.RequestGalacticInformationRender();
    }

    /// <summary>
    /// Releases subscriptions when Unity destroys the authored selector view.
    /// </summary>
    /// <param name="destroyedView">The destroyed selector view.</param>
    private void HandleDisplayViewDestroyed(GalacticInformationDisplayView destroyedView)
    {
        if (!ReferenceEquals(displayView, destroyedView))
            return;

        UnbindDisplayView();
        displayView = null;
        open = false;
        ResetTransientState();
    }

    /// <summary>
    /// Releases subscriptions when Unity destroys the retained legend view.
    /// </summary>
    /// <param name="destroyedView">The destroyed legend view.</param>
    private void HandleLegendViewDestroyed(GalacticInformationLegendView destroyedView)
    {
        if (!ReferenceEquals(legendView, destroyedView))
            return;

        legendView.CloseRequested -= HandleLegendCloseRequested;
        legendView.Destroyed -= HandleLegendViewDestroyed;
        legendView = null;
    }

    /// <summary>
    /// Hides the selector, plays its dismissal sound, and requests a strategy render.
    /// </summary>
    private void DismissSelector()
    {
        Hide();
        playSfx(StrategyUISoundPaths.GalacticInformationControl);
        actions.RequestGalacticInformationRender();
    }

    /// <summary>
    /// Applies the current controller-owned selector state to the authored view.
    /// </summary>
    private void RenderDisplay()
    {
        if (displayView == null)
            return;

        GalacticInformationDisplayRenderData data = projector.Project(
            new GalacticInformationDisplayState(
                open,
                activeCategoryIndex,
                hoveredFilterIndex,
                displayOffHovered
            )
        );
        displayView.Render(data);
    }

    /// <summary>
    /// Resets category and hover state used only while the selector is open.
    /// </summary>
    private void ResetTransientState()
    {
        activeCategoryIndex = -1;
        hoveredFilterIndex = -1;
        displayOffHovered = false;
    }

    /// <summary>
    /// Subscribes every semantic selector event exactly once.
    /// </summary>
    private void BindDisplayView()
    {
        displayView.CategoryRequested += HandleCategoryRequested;
        displayView.Destroyed += HandleDisplayViewDestroyed;
        displayView.DismissRequested += HandleDismissRequested;
        displayView.DisplayOffEntered += HandleDisplayOffEntered;
        displayView.DisplayOffExited += HandleDisplayOffExited;
        displayView.DisplayOffSelected += HandleDisplayOffSelected;
        displayView.FilterEntered += HandleFilterEntered;
        displayView.FilterExited += HandleFilterExited;
        displayView.FilterSelected += HandleFilterSelected;
    }

    /// <summary>
    /// Releases every semantic selector subscription.
    /// </summary>
    private void UnbindDisplayView()
    {
        if (displayView == null)
            return;

        displayView.CategoryRequested -= HandleCategoryRequested;
        displayView.Destroyed -= HandleDisplayViewDestroyed;
        displayView.DismissRequested -= HandleDismissRequested;
        displayView.DisplayOffEntered -= HandleDisplayOffEntered;
        displayView.DisplayOffExited -= HandleDisplayOffExited;
        displayView.DisplayOffSelected -= HandleDisplayOffSelected;
        displayView.FilterEntered -= HandleFilterEntered;
        displayView.FilterExited -= HandleFilterExited;
        displayView.FilterSelected -= HandleFilterSelected;
    }

    /// <summary>
    /// Releases subscriptions from the currently bound selector and legend views.
    /// </summary>
    private void ReleaseViews()
    {
        UnbindDisplayView();
        if (legendView != null)
        {
            legendView.CloseRequested -= HandleLegendCloseRequested;
            legendView.Destroyed -= HandleLegendViewDestroyed;
        }

        displayView = null;
        legendView = null;
        open = false;
        ResetTransientState();
    }

    /// <summary>
    /// Verifies action routing is available before view binding or interaction.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GalacticInformationDisplayController)} must be initialized before use."
            );
        }
    }

    /// <summary>
    /// Gets the bound selector view and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The bound authored selector view.</returns>
    private GalacticInformationDisplayView GetRequiredDisplayView()
    {
        EnsureInitialized();
        return displayView
            ?? throw new InvalidOperationException(
                $"{nameof(GalacticInformationDisplayController)} must bind its views before use."
            );
    }
}
