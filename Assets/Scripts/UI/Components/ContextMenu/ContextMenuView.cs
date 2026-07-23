using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns the shared context-menu state, panel lifecycle, placement, and input events.
/// </summary>
public sealed class ContextMenuView : MonoBehaviour, ICancelable
{
    private static readonly Color32 _defaultEnabledColor = new(255, 255, 255, 255);
    private static readonly Color32 _defaultDisabledColor = new(128, 128, 128, 255);

    [SerializeField]
    private Color32 _enabledCommandColor = new(255, 255, 255, 255);

    [SerializeField]
    private Color32 _disabledCommandColor = new(128, 128, 128, 255);

    [SerializeField]
    private ContextMenuPanelView panelPrefab;

    [SerializeField]
    private RawImage dismissHitAreaImage;

    [SerializeField]
    private ContextMenuDismissBoundary dismissBoundary;

    private readonly List<ContextMenuPanelView> panelViews = new List<ContextMenuPanelView>();
    private readonly List<ContextMenuPanel> panels = new List<ContextMenuPanel>();
    private bool initialized;

    /// <summary>
    /// Raised when the player selects an enabled leaf command.
    /// </summary>
    public event System.Action<IContextMenuCommand> CommandSelected;

    /// <summary>
    /// Raised when a pointer press outside the menu requests dismissal.
    /// </summary>
    public event System.Action<PointerEventData> DismissRequested;

    public bool Open { get; private set; }

    public object Owner { get; private set; }

    /// <summary>
    /// Initializes the authored context-menu hierarchy and event routing.
    /// </summary>
    private void Awake()
    {
        InitializeView();
    }

    /// <summary>
    /// Releases the authored dismissal event routing.
    /// </summary>
    private void OnDestroy()
    {
        if (!initialized)
            return;

        if (dismissBoundary != null)
            dismissBoundary.PointerDown -= HandleDismissBoundaryPointerDown;

        for (int index = 0; index < panelViews.Count; index++)
        {
            ContextMenuPanelView view = panelViews[index];
            if (view == null)
                continue;

            view.CommandSelected -= HandleCommandSelected;
            view.CommandStateChanged -= HandleCommandStateChanged;
        }
    }

    /// <summary>
    /// Opens a root menu at one source-space position.
    /// </summary>
    /// <param name="owner">The feature object that owns the menu.</param>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    /// <param name="width">The authored base width.</param>
    /// <param name="commands">The ordered commands to display.</param>
    /// <param name="visuals">The optional command-state colors.</param>
    public void OpenAt(
        object owner,
        int x,
        int y,
        int width,
        IReadOnlyList<ContextMenuCommandItem> commands,
        ContextMenuVisuals visuals = null
    )
    {
        InitializeView();

        panels.Clear();

        ContextMenuPanel panel = new ContextMenuPanel(
            x,
            y,
            width,
            commands,
            visuals ?? ContextMenuVisuals.Default
        );
        AdjustRootPanelPosition(panel, GetMetrics());
        panels.Add(panel);

        Owner = owner;
        Open = true;
    }

    /// <summary>
    /// Clears the current menu state and hides every rendered panel.
    /// </summary>
    public void Reset()
    {
        panels.Clear();
        Owner = null;
        Open = false;
        HidePanels();
    }

    /// <summary>
    /// Tries to close the current menu.
    /// </summary>
    /// <returns><see langword="true"/> when an open menu was closed.</returns>
    public bool TryCancel()
    {
        if (!Open)
            return false;

        Reset();
        return true;
    }

    /// <summary>
    /// Presents the current root menu and any open submenu panels.
    /// </summary>
    public void RenderCurrent()
    {
        InitializeView();
        if (!Open || panels.Count == 0)
        {
            HidePanels();
            return;
        }

        gameObject.SetActive(true);
        dismissHitAreaImage.gameObject.SetActive(true);
        for (int index = 0; index < panels.Count; index++)
        {
            ContextMenuPanel panel = panels[index];
            GetPanelView(index)
                .Render(panel.X, panel.Y, panel.Width, panel.Commands, panel.Visuals);
        }

        for (int index = panels.Count; index < panelViews.Count; index++)
            panelViews[index].Hide();
    }

    /// <summary>
    /// Calculates the rendered width for one command list.
    /// </summary>
    /// <param name="width">The menu's authored base width.</param>
    /// <param name="commands">The commands rendered by the menu.</param>
    /// <returns>The required rendered width in source units.</returns>
    public int GetMenuWidth(int width, IReadOnlyList<ContextMenuCommandItem> commands)
    {
        InitializeView();
        return panelPrefab.GetPanelWidth(width, commands);
    }

    /// <summary>
    /// Creates command-state visuals from the authored enabled and disabled colors.
    /// </summary>
    /// <param name="activeColor">The feature-specific active command color.</param>
    /// <returns>The complete command-state visual set.</returns>
    internal ContextMenuVisuals CreateVisuals(Color32? activeColor)
    {
        return new ContextMenuVisuals(
            _enabledCommandColor,
            activeColor ?? _enabledCommandColor,
            _disabledCommandColor
        );
    }

    /// <summary>
    /// Validates and initializes the authored view once.
    /// </summary>
    private void InitializeView()
    {
        if (initialized)
            return;

        VerifyReferences();
        dismissBoundary.PointerDown += HandleDismissBoundaryPointerDown;
        panelPrefab.gameObject.SetActive(false);
        HidePanels();
        initialized = true;
    }

    /// <summary>
    /// Verifies that every authored context-menu reference is assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (panelPrefab == null)
            throw new MissingReferenceException($"{name}/PanelTemplate is missing.");
        if (dismissHitAreaImage == null)
            throw new MissingReferenceException($"{name}/DismissHitAreaImage is missing.");
        if (dismissBoundary == null)
            throw new MissingReferenceException($"{name}/DismissBoundary is missing.");
    }

    /// <summary>
    /// Gets the authored measurements used for panel placement.
    /// </summary>
    /// <returns>The authored context-menu measurements.</returns>
    private ContextMenuMetrics GetMetrics()
    {
        InitializeView();
        return panelPrefab.GetMetrics();
    }

    /// <summary>
    /// Gets or creates one panel instance from the authored panel template.
    /// </summary>
    /// <param name="index">The zero-based panel index.</param>
    /// <returns>The panel instance at the requested index.</returns>
    private ContextMenuPanelView GetPanelView(int index)
    {
        while (panelViews.Count <= index)
        {
            ContextMenuPanelView view = Instantiate(panelPrefab, transform);
            view.name = $"Panel{panelViews.Count}";
            view.CommandSelected += HandleCommandSelected;
            view.CommandStateChanged += HandleCommandStateChanged;
            panelViews.Add(view);
        }

        return panelViews[index];
    }

    /// <summary>
    /// Hides every instantiated panel and the full-surface dismissal region.
    /// </summary>
    private void HidePanels()
    {
        foreach (ContextMenuPanelView panel in panelViews)
            panel.Hide();

        if (dismissHitAreaImage != null)
            dismissHitAreaImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Keeps a root panel within the available menu surface.
    /// </summary>
    /// <param name="panel">The root panel state.</param>
    /// <param name="metrics">The authored panel measurements.</param>
    private void AdjustRootPanelPosition(ContextMenuPanel panel, ContextMenuMetrics metrics)
    {
        Vector2Int surfaceSize = GetSurfaceSize();
        int width = GetMenuWidth(panel);
        int height = metrics.GetPanelHeight(panel.Commands.Count);
        if (surfaceSize.x > 0 && panel.X + width >= surfaceSize.x)
            panel.X -= width;
        if (surfaceSize.y > 0 && panel.Y + height >= surfaceSize.y)
            panel.Y -= height;

        panel.X = Mathf.Max(0, panel.X);
        panel.Y = Mathf.Max(0, panel.Y);
    }

    /// <summary>
    /// Gets the source-space size of the context-menu surface.
    /// </summary>
    /// <returns>The available source-space size, or zero for an unresolved axis.</returns>
    private Vector2Int GetSurfaceSize()
    {
        if (transform is not RectTransform rect)
            return Vector2Int.zero;

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);

        return new Vector2Int(width, height);
    }

    /// <summary>
    /// Calculates the rendered width for one panel state.
    /// </summary>
    /// <param name="panel">The panel to measure.</param>
    /// <returns>The required rendered width in source units.</returns>
    private int GetMenuWidth(ContextMenuPanel panel)
    {
        return panelPrefab.GetPanelWidth(panel.Width, panel.Commands);
    }

    /// <summary>
    /// Forwards selection of an enabled leaf command.
    /// </summary>
    /// <param name="command">The selected command.</param>
    private void HandleCommandSelected(IContextMenuCommand command)
    {
        if (command?.Enabled == true)
            CommandSelected?.Invoke(command);
    }

    /// <summary>
    /// Applies one command hover transition and updates the submenu chain.
    /// </summary>
    /// <param name="item">The command item whose active state changed.</param>
    private void HandleCommandStateChanged(ContextMenuCommandItem item)
    {
        if (!Open || item == null)
            return;

        int panelIndex = panels.FindIndex(panel => panel.Commands.Contains(item));
        if (panelIndex < 0)
            return;

        if (item.Active)
        {
            ContextMenuPanel panel = panels[panelIndex];
            foreach (ContextMenuCommandItem command in panel.Commands)
            {
                if (!ReferenceEquals(command, item))
                    command.Active = false;
            }

            RemovePanelsAfter(panelIndex);
            if (item.Enabled && item.HasSubmenu)
                OpenSubmenu(panelIndex, item);
        }

        RenderCurrent();
    }

    /// <summary>
    /// Opens one submenu beside its active parent command.
    /// </summary>
    /// <param name="parentPanelIndex">The zero-based parent panel index.</param>
    /// <param name="parentItem">The active command that owns the submenu.</param>
    private void OpenSubmenu(int parentPanelIndex, ContextMenuCommandItem parentItem)
    {
        ContextMenuPanel parent = panels[parentPanelIndex];
        int rowIndex = parent.Commands.IndexOf(parentItem);
        if (rowIndex < 0)
            return;

        ContextMenuPanel submenu = new ContextMenuPanel(
            0,
            0,
            parent.Width,
            parentItem.SubmenuCommands,
            parent.Visuals
        );
        ContextMenuMetrics metrics = GetMetrics();
        int parentWidth = GetMenuWidth(parent);
        int submenuWidth = GetMenuWidth(submenu);
        Vector2Int surfaceSize = GetSurfaceSize();
        int leftX = parent.X - submenuWidth + metrics.BorderSize;
        submenu.X = leftX >= 0 ? leftX : parent.X + parentWidth - metrics.BorderSize;
        submenu.Y = parent.Y + metrics.BorderSize + rowIndex * metrics.RowHeight;
        AdjustSubmenuPosition(submenu, surfaceSize, metrics);
        panels.Add(submenu);
    }

    /// <summary>
    /// Keeps a submenu within the available menu surface.
    /// </summary>
    /// <param name="submenu">The submenu state.</param>
    /// <param name="surfaceSize">The available source-space size.</param>
    /// <param name="metrics">The authored panel measurements.</param>
    private void AdjustSubmenuPosition(
        ContextMenuPanel submenu,
        Vector2Int surfaceSize,
        ContextMenuMetrics metrics
    )
    {
        int width = GetMenuWidth(submenu);
        int height = metrics.GetPanelHeight(submenu.Commands.Count);
        if (surfaceSize.x > 0)
            submenu.X = Mathf.Clamp(submenu.X, 0, Mathf.Max(0, surfaceSize.x - width));
        if (surfaceSize.y > 0 && submenu.Y + height >= surfaceSize.y)
            submenu.Y -= height;

        submenu.Y = Mathf.Max(0, submenu.Y);
    }

    /// <summary>
    /// Removes every submenu after one panel in the active chain.
    /// </summary>
    /// <param name="panelIndex">The last panel index to retain.</param>
    private void RemovePanelsAfter(int panelIndex)
    {
        int removeIndex = panelIndex + 1;
        if (removeIndex < panels.Count)
            panels.RemoveRange(removeIndex, panels.Count - removeIndex);
    }

    /// <summary>
    /// Forwards a pointer press from the authored dismissal boundary.
    /// </summary>
    /// <param name="eventData">The pointer press outside the menu panels.</param>
    private void HandleDismissBoundaryPointerDown(PointerEventData eventData)
    {
        if (Open)
            DismissRequested?.Invoke(eventData);
    }

    /// <summary>
    /// Stores one panel in the active context-menu chain.
    /// </summary>
    private sealed class ContextMenuPanel
    {
        /// <summary>
        /// Creates one panel state.
        /// </summary>
        /// <param name="x">The horizontal source-space coordinate.</param>
        /// <param name="y">The vertical source-space coordinate.</param>
        /// <param name="width">The authored base width.</param>
        /// <param name="commands">The ordered panel commands.</param>
        /// <param name="visuals">The command-state colors.</param>
        public ContextMenuPanel(
            int x,
            int y,
            int width,
            IReadOnlyList<ContextMenuCommandItem> commands,
            ContextMenuVisuals visuals
        )
        {
            X = x;
            Y = y;
            Width = width;
            Commands =
                commands == null
                    ? new List<ContextMenuCommandItem>()
                    : new List<ContextMenuCommandItem>(commands);
            Visuals = visuals;
        }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; }

        public List<ContextMenuCommandItem> Commands { get; }

        public ContextMenuVisuals Visuals { get; }
    }

    /// <summary>
    /// Defines command colors for one context-menu chain.
    /// </summary>
    public sealed class ContextMenuVisuals
    {
        public static readonly ContextMenuVisuals Default = new ContextMenuVisuals(
            _defaultEnabledColor,
            _defaultEnabledColor,
            _defaultDisabledColor
        );

        /// <summary>
        /// Creates one command-color set.
        /// </summary>
        /// <param name="enabledColor">The enabled command color.</param>
        /// <param name="activeColor">The active command color.</param>
        /// <param name="disabledColor">The disabled command color.</param>
        public ContextMenuVisuals(Color32 enabledColor, Color32 activeColor, Color32 disabledColor)
        {
            EnabledColor = enabledColor;
            ActiveColor = activeColor;
            DisabledColor = disabledColor;
        }

        public Color32 EnabledColor { get; }

        public Color32 ActiveColor { get; }

        public Color32 DisabledColor { get; }
    }
}

/// <summary>
/// Combines one command with its shared context-menu presentation state.
/// </summary>
public sealed class ContextMenuCommandItem
{
    /// <summary>
    /// Creates one context-menu command item.
    /// </summary>
    /// <param name="command">The command invoked by the item.</param>
    /// <param name="iconTexture">The default icon texture.</param>
    /// <param name="activeIconTexture">The active-state icon texture.</param>
    /// <param name="usesIconColumn">Whether the item reserves the panel icon column.</param>
    /// <param name="centerNativeIcon">Whether the icon uses its native source size.</param>
    /// <param name="submenuCommands">The optional submenu commands.</param>
    public ContextMenuCommandItem(
        IContextMenuCommand command,
        Texture iconTexture = null,
        Texture activeIconTexture = null,
        bool usesIconColumn = false,
        bool centerNativeIcon = false,
        IReadOnlyList<ContextMenuCommandItem> submenuCommands = null
    )
    {
        Command = command ?? throw new System.ArgumentNullException(nameof(command));
        IconTexture = iconTexture;
        ActiveIconTexture = activeIconTexture;
        UsesIconColumn = usesIconColumn || iconTexture != null || activeIconTexture != null;
        CenterNativeIcon = centerNativeIcon;
        SubmenuCommands =
            submenuCommands == null
                ? new List<ContextMenuCommandItem>()
                : new List<ContextMenuCommandItem>(submenuCommands);
    }

    public IContextMenuCommand Command { get; }

    public string Text => Command.Text;

    public bool Enabled => Command.Enabled;

    public bool Active { get; set; }

    public bool UsesIconColumn { get; }

    public bool CenterNativeIcon { get; }

    public IReadOnlyList<ContextMenuCommandItem> SubmenuCommands { get; }

    public bool HasSubmenu => SubmenuCommands.Count > 0;

    public Texture IconTexture { get; }

    public Texture ActiveIconTexture { get; }

    /// <summary>
    /// Gets the icon texture for the current active state.
    /// </summary>
    /// <returns>The active icon when configured; otherwise, the default icon.</returns>
    public Texture GetIconTexture()
    {
        if (Active && ActiveIconTexture != null)
            return ActiveIconTexture;

        return IconTexture;
    }
}

/// <summary>
/// Exposes the authored measurements required to position context-menu panels.
/// </summary>
internal readonly struct ContextMenuMetrics
{
    /// <summary>
    /// Creates one authored context-menu measurement set.
    /// </summary>
    /// <param name="rowHeight">The command-row height.</param>
    /// <param name="iconPanelWidth">The additional width reserved for icon panels.</param>
    /// <param name="borderSize">The panel-border thickness.</param>
    public ContextMenuMetrics(int rowHeight, int iconPanelWidth, int borderSize)
    {
        RowHeight = rowHeight;
        IconPanelWidth = iconPanelWidth;
        BorderSize = borderSize;
    }

    public int RowHeight { get; }

    public int IconPanelWidth { get; }

    public int BorderSize { get; }

    /// <summary>
    /// Calculates one panel width from its authored width and icon-column state.
    /// </summary>
    /// <param name="baseWidth">The authored base width.</param>
    /// <param name="hasIconColumn">Whether the panel reserves an icon column.</param>
    /// <returns>The rendered panel width in source units.</returns>
    public int GetPanelWidth(int baseWidth, bool hasIconColumn)
    {
        return baseWidth + (hasIconColumn ? IconPanelWidth : 0);
    }

    /// <summary>
    /// Calculates the rendered height for one panel.
    /// </summary>
    /// <param name="commandCount">The number of command rows.</param>
    /// <returns>The rendered panel height in source units.</returns>
    public int GetPanelHeight(int commandCount)
    {
        return commandCount * RowHeight + BorderSize * 2;
    }
}
