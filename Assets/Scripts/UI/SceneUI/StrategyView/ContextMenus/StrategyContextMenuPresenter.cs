using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class StrategyContextMenuPresenter : MonoBehaviour, ICancelable
{
    private static readonly Color32 EnabledColor = new(255, 255, 255, 255);
    private static readonly Color32 DisabledColor = new(128, 128, 128, 255);

    private UIContext uiContext;
    private bool bound;

    [SerializeField]
    private ContextMenuHost contextMenuHost;

    [SerializeField]
    private int speedMenuWidth;

    [SerializeField]
    private int facilityMenuWidth;

    [SerializeField]
    private int planetSystemMenuWidth;

    [SerializeField]
    private int defenseMenuWidth;

    [SerializeField]
    private int missionsMenuWidth;

    [SerializeField]
    private int fallbackMenuWidth;

    internal StrategyContextMenuLayout Layout =>
        new StrategyContextMenuLayout(
            facilityMenuWidth,
            planetSystemMenuWidth,
            defenseMenuWidth,
            missionsMenuWidth,
            fallbackMenuWidth
        );
    internal bool Open => contextMenuHost != null && contextMenuHost.Open;
    internal UIWindow Window => contextMenuHost?.Owner as UIWindow;
    internal int HotspotX => contextMenuHost?.HotspotX ?? 0;
    internal int HotspotY => contextMenuHost?.HotspotY ?? 0;
    internal event System.Action<StrategyMenuCommand> CommandSelected;
    internal event System.Action<PointerEventData> DismissRequested;

    public void Initialize(UIContext context)
    {
        uiContext = context;
    }

    internal void Show(StrategyContextMenuData menu)
    {
        if (menu == null)
        {
            Reset();
            return;
        }

        EnsureBound();
        contextMenuHost.OpenAt(
            menu.Window,
            menu.X,
            menu.Y,
            menu.Width,
            BuildCommandItems(menu.Commands),
            BuildVisuals()
        );
    }

    internal void OpenSpeedMenu(int x, int y)
    {
        Show(new StrategyContextMenuData(null, x, y, speedMenuWidth, BuildSpeedMenuCommands()));
    }

    internal void Reset()
    {
        contextMenuHost?.Reset();
    }

    public bool TryCancel()
    {
        return contextMenuHost?.TryCancel() == true;
    }

    internal void RenderCurrent()
    {
        contextMenuHost?.RenderCurrent();
    }

    public ContextMenuMetrics GetMetrics()
    {
        EnsureBound();
        return contextMenuHost.GetMetrics();
    }

    internal int GetMenuWidth(int width, IReadOnlyList<StrategyMenuCommand> commands)
    {
        EnsureBound();
        return contextMenuHost.GetMenuWidth(width, BuildCommandItems(commands));
    }

    private void Awake()
    {
        TryBind();
    }

    private void OnDestroy()
    {
        if (contextMenuHost == null)
            return;

        contextMenuHost.CommandSelected -= HandleCommandSelected;
        contextMenuHost.DismissRequested -= HandleDismissRequested;
    }

    private void VerifyReferences()
    {
        if (contextMenuHost == null)
            throw new MissingReferenceException($"{name}/ContextMenuHost is missing.");
    }

    private void EnsureBound()
    {
        VerifyReferences();
        Bind();
    }

    private bool TryBind()
    {
        if (contextMenuHost == null)
            return false;

        Bind();
        return true;
    }

    private void Bind()
    {
        if (bound)
            return;

        contextMenuHost.CommandSelected -= HandleCommandSelected;
        contextMenuHost.CommandSelected += HandleCommandSelected;
        contextMenuHost.DismissRequested -= HandleDismissRequested;
        contextMenuHost.DismissRequested += HandleDismissRequested;
        bound = true;
    }

    private static List<StrategyMenuCommand> BuildSpeedMenuCommands()
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedPause,
                "Pause",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(0)
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedVerySlow,
                "Very Slow",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(1)
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedSlow,
                "Slow",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(2)
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedMedium,
                "Medium",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(3)
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedFast,
                "Fast",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(4)
            ),
        };
    }

    private List<ContextMenuCommandItem> BuildCommandItems(
        IReadOnlyList<StrategyMenuCommand> commands
    )
    {
        List<ContextMenuCommandItem> items = new List<ContextMenuCommandItem>();
        if (commands == null)
            return items;

        for (int i = 0; i < commands.Count; i++)
        {
            StrategyMenuCommand command = commands[i];
            if (command == null)
                continue;

            items.Add(
                new ContextMenuCommandItem(
                    command,
                    GetCommandIconTexture(command, false),
                    GetCommandIconTexture(command, true),
                    command.UsesIconColumn
                )
            );
        }

        return items;
    }

    private ContextMenuView.ContextMenuVisuals BuildVisuals()
    {
        Color32 hotColor = EnabledColor;
        FactionTheme playerTheme = uiContext?.GetPlayerFactionTheme();
        if (playerTheme != null)
            hotColor = playerTheme.GetPrimaryColor();

        return new ContextMenuView.ContextMenuVisuals(EnabledColor, hotColor, DisabledColor);
    }

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

    private void HandleCommandSelected(IContextMenuCommand command)
    {
        if (command is StrategyMenuCommand strategyCommand)
            CommandSelected?.Invoke(strategyCommand);
    }

    private void HandleDismissRequested(PointerEventData eventData)
    {
        DismissRequested?.Invoke(eventData);
    }
}

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
