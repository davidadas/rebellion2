using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Projects Strategy commands and faction visuals into the shared context-menu view.
/// </summary>
public sealed class StrategyContextMenuPresenter : MonoBehaviour, ICancelable
{
    [SerializeField]
    private ContextMenuView contextMenuView;

    [SerializeField]
    private int speedMenuWidth;

    [SerializeField]
    private int facilityMenuWidth;

    [SerializeField]
    private int fleetMenuWidth;

    [SerializeField]
    private int fleetBombardmentMenuWidth;

    [SerializeField]
    private int planetSystemMenuWidth;

    [SerializeField]
    private int defenseMenuWidth;

    [SerializeField]
    private int missionsMenuWidth;

    [SerializeField]
    private int fallbackMenuWidth;

    private UIContext uiContext;
    private bool initialized;

    /// <summary>
    /// Raised when the player selects a Strategy command.
    /// </summary>
    internal event System.Action<StrategyMenuCommand> CommandSelected;

    /// <summary>
    /// Raised when a pointer press outside the menu requests dismissal.
    /// </summary>
    internal event System.Action<PointerEventData> DismissRequested;

    internal StrategyContextMenuLayout Layout =>
        new StrategyContextMenuLayout(
            facilityMenuWidth,
            fleetMenuWidth,
            fleetBombardmentMenuWidth,
            planetSystemMenuWidth,
            defenseMenuWidth,
            missionsMenuWidth,
            fallbackMenuWidth
        );

    internal int SpeedMenuWidth => speedMenuWidth;

    internal bool Open => contextMenuView && contextMenuView.Open;

    internal UIWindow Window => contextMenuView?.Owner as UIWindow;

    /// <summary>
    /// Initializes the authored context-menu event routing.
    /// </summary>
    private void Awake()
    {
        InitializeView();
    }

    /// <summary>
    /// Releases the authored context-menu event routing.
    /// </summary>
    private void OnDestroy()
    {
        if (!initialized || contextMenuView == null)
            return;

        contextMenuView.CommandSelected -= HandleCommandSelected;
        contextMenuView.DismissRequested -= HandleDismissRequested;
    }

    /// <summary>
    /// Supplies the UI services used to resolve faction-specific menu visuals.
    /// </summary>
    /// <param name="context">The active UI context.</param>
    public void Initialize(UIContext context)
    {
        uiContext = context;
    }

    /// <summary>
    /// Tries to close the currently open context menu.
    /// </summary>
    /// <returns><see langword="true"/> when an open menu was closed.</returns>
    public bool TryCancel()
    {
        InitializeView();
        return contextMenuView.TryCancel();
    }

    /// <summary>
    /// Opens one Strategy context menu.
    /// </summary>
    /// <param name="menu">The menu content and source-space placement.</param>
    internal void Show(StrategyContextMenuData menu)
    {
        if (menu == null)
        {
            Reset();
            return;
        }

        InitializeView();
        contextMenuView.OpenAt(
            menu.Window,
            menu.X,
            menu.Y,
            menu.Width,
            BuildCommandItems(menu.Commands),
            BuildVisuals()
        );
    }

    /// <summary>
    /// Clears the current menu state and presentation.
    /// </summary>
    internal void Reset()
    {
        contextMenuView?.Reset();
    }

    /// <summary>
    /// Presents the current menu state.
    /// </summary>
    internal void RenderCurrent()
    {
        contextMenuView?.RenderCurrent();
    }

    /// <summary>
    /// Calculates the rendered width for one Strategy command list.
    /// </summary>
    /// <param name="width">The menu's authored base width.</param>
    /// <param name="commands">The commands rendered by the menu.</param>
    /// <returns>The required rendered width in source units.</returns>
    internal int GetMenuWidth(int width, IReadOnlyList<StrategyMenuCommand> commands)
    {
        InitializeView();
        return contextMenuView.GetMenuWidth(width, BuildCommandItems(commands));
    }

    /// <summary>
    /// Validates and binds the shared context-menu view once.
    /// </summary>
    private void InitializeView()
    {
        if (initialized)
            return;

        if (contextMenuView == null)
            throw new MissingReferenceException($"{name}/ContextMenuView is missing.");

        contextMenuView.CommandSelected += HandleCommandSelected;
        contextMenuView.DismissRequested += HandleDismissRequested;
        initialized = true;
    }

    /// <summary>
    /// Projects Strategy commands into shared context-menu items.
    /// </summary>
    /// <param name="commands">The Strategy commands to project.</param>
    /// <returns>The ordered shared context-menu items.</returns>
    private List<ContextMenuCommandItem> BuildCommandItems(
        IReadOnlyList<StrategyMenuCommand> commands
    )
    {
        List<ContextMenuCommandItem> items = new List<ContextMenuCommandItem>();
        if (commands == null)
            return items;

        for (int index = 0; index < commands.Count; index++)
        {
            StrategyMenuCommand command = commands[index];
            if (command == null)
                continue;

            items.Add(
                new ContextMenuCommandItem(
                    command,
                    GetCommandIconTexture(command, false),
                    GetCommandIconTexture(command, true),
                    command.UsesIconColumn,
                    command.IsSubmenu
                        || StrategyContextMenuIconKeys.TryGetSpeed(command.IconKey, out _),
                    BuildCommandItems(command.SubmenuCommands)
                )
            );
        }

        return items;
    }

    /// <summary>
    /// Resolves the enabled, active, and disabled colors for the player faction.
    /// </summary>
    /// <returns>The current menu visuals.</returns>
    private ContextMenuView.ContextMenuVisuals BuildVisuals()
    {
        FactionTheme playerTheme = uiContext?.GetPlayerFactionTheme();
        Color32? activeColor = playerTheme == null ? null : (Color32)playerTheme.GetPrimaryColor();
        return contextMenuView.CreateVisuals(activeColor);
    }

    /// <summary>
    /// Resolves one command icon for its default or active state.
    /// </summary>
    /// <param name="command">The command whose icon is requested.</param>
    /// <param name="active">Whether to resolve the active-state icon.</param>
    /// <returns>The resolved icon texture, or <see langword="null"/> when none is configured.</returns>
    private Texture2D GetCommandIconTexture(StrategyMenuCommand command, bool active)
    {
        if (command == null)
            return null;

        int iconKey = GetCommandIconKey(command, active);
        if (StrategyContextMenuIconKeys.TryGetSpeed(iconKey, out int sourceSpeed))
        {
            string speedPath = uiContext
                ?.GetPlayerFactionTheme()
                ?.TacticalHUDLayout?.SpeedIndicators?.GetImagePath(sourceSpeed);
            return uiContext?.GetTexture(speedPath);
        }

        string path = iconKey switch
        {
            StrategyContextMenuIconKeys.SubmenuArrowOn => uiContext
                ?.GetPlayerFactionTheme()
                ?.StrategyContextMenuTheme?.ArrowOnImagePath,
            StrategyContextMenuIconKeys.SubmenuArrowOff => uiContext
                ?.GetPlayerFactionTheme()
                ?.StrategyContextMenuTheme?.ArrowOffImagePath,
            StrategyContextMenuIconKeys.CheckMark => uiContext
                ?.GetPlayerFactionTheme()
                ?.StrategyContextMenuTheme?.CheckMarkImagePath,
            _ => null,
        };
        return uiContext?.GetTexture(path);
    }

    /// <summary>
    /// Resolves the configured icon key for one command state.
    /// </summary>
    /// <param name="command">The command whose icon key is requested.</param>
    /// <param name="active">Whether to resolve the active-state icon key.</param>
    /// <returns>The resolved context-menu icon key.</returns>
    private static int GetCommandIconKey(StrategyMenuCommand command, bool active)
    {
        if (command == null)
            return StrategyContextMenuIconKeys.None;

        if (command.IsSubmenu)
        {
            return command.Enabled && active
                ? StrategyContextMenuIconKeys.SubmenuArrowOn
                : StrategyContextMenuIconKeys.SubmenuArrowOff;
        }

        return command.IconKey;
    }

    /// <summary>
    /// Forwards a selected shared command when it belongs to the Strategy command model.
    /// </summary>
    /// <param name="command">The selected shared command.</param>
    private void HandleCommandSelected(IContextMenuCommand command)
    {
        if (command is StrategyMenuCommand strategyCommand)
            CommandSelected?.Invoke(strategyCommand);
    }

    /// <summary>
    /// Forwards a pointer-driven dismissal request.
    /// </summary>
    /// <param name="eventData">The pointer press outside the menu.</param>
    private void HandleDismissRequested(PointerEventData eventData)
    {
        DismissRequested?.Invoke(eventData);
    }
}

/// <summary>
/// Defines stable icon identifiers used by Strategy context-menu commands.
/// </summary>
public static class StrategyContextMenuIconKeys
{
    public const int None = 0;
    public const int PausedSpeed = 1;
    public const int VerySlowSpeed = 2;
    public const int SlowSpeed = 3;
    public const int MediumSpeed = 4;
    public const int FastSpeed = 5;
    public const int SubmenuArrowOn = 6;
    public const int SubmenuArrowOff = 7;
    public const int CheckMark = 8;

    /// <summary>
    /// Maps one source speed to its menu icon identifier.
    /// </summary>
    /// <param name="sourceSpeed">The source speed value.</param>
    /// <returns>The matching menu icon identifier.</returns>
    public static int GetSpeedIconKey(int sourceSpeed)
    {
        return sourceSpeed switch
        {
            1 => VerySlowSpeed,
            2 => SlowSpeed,
            3 => MediumSpeed,
            4 => FastSpeed,
            _ => PausedSpeed,
        };
    }

    /// <summary>
    /// Tries to map one menu icon identifier to a source speed.
    /// </summary>
    /// <param name="iconKey">The menu icon identifier.</param>
    /// <param name="sourceSpeed">The resolved source speed.</param>
    /// <returns><see langword="true"/> when the icon represents a game speed.</returns>
    public static bool TryGetSpeed(int iconKey, out int sourceSpeed)
    {
        sourceSpeed = iconKey switch
        {
            VerySlowSpeed => 1,
            SlowSpeed => 2,
            MediumSpeed => 3,
            FastSpeed => 4,
            PausedSpeed => 0,
            _ => -1,
        };
        return sourceSpeed >= 0;
    }
}
