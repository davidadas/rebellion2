using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ContextMenuView : MonoBehaviour, ICancelable
{
    private static readonly Color32 DefaultEnabledColor = new(255, 255, 255, 255);
    private static readonly Color32 DefaultDisabledColor = new(128, 128, 128, 255);

    private readonly List<ContextMenuPanelView> panelViews = new List<ContextMenuPanelView>();
    private readonly List<ContextMenuPanel> panels = new List<ContextMenuPanel>();
    private bool bound;

    [SerializeField]
    private ContextMenuPanelView panelPrefab;

    [SerializeField]
    private RawImage dismissHitAreaImage;

    [SerializeField]
    private ContextMenuDismissBoundary dismissBoundary;

    public bool Open { get; private set; }
    public object Owner { get; private set; }
    public int HotspotX { get; private set; }
    public int HotspotY { get; private set; }

    public event System.Action<IContextMenuCommand> CommandSelected;
    public event System.Action<PointerEventData> DismissRequested;

    public void OpenAt(
        object owner,
        int x,
        int y,
        int width,
        IReadOnlyList<ContextMenuCommandItem> commands,
        ContextMenuVisuals visuals = null
    )
    {
        panels.Clear();
        HotspotX = x;
        HotspotY = y;
        ContextMenuPanel panel = new ContextMenuPanel
        {
            X = x,
            Y = y,
            Width = width,
            Visible = true,
            Commands =
                commands == null
                    ? new List<ContextMenuCommandItem>()
                    : new List<ContextMenuCommandItem>(commands),
            Visuals = visuals ?? ContextMenuVisuals.Default,
        };
        panel.HasIconColumn = HasIconColumn(panel.Commands);
        AdjustVisibility(panel, GetMetrics());
        panels.Add(panel);
        Owner = owner;
        Open = true;
    }

    public void Reset()
    {
        panels.Clear();
        Owner = null;
        Open = false;
        HotspotX = 0;
        HotspotY = 0;
        Hide();
    }

    public bool TryCancel()
    {
        if (!Open)
            return false;

        Reset();
        return true;
    }

    public void RenderCurrent()
    {
        if (!Open)
        {
            Hide();
            return;
        }

        ContextMenuMetrics metrics = GetMetrics();
        List<ContextMenuPanelRenderData> renderPanels = new List<ContextMenuPanelRenderData>();
        for (int index = 0; index < panels.Count; index++)
        {
            ContextMenuPanel panel = panels[index];
            if (!panel.Visible)
                continue;

            int finalWidth = GetMenuWidth(panel);
            int x = index == 0 ? panel.X : panel.X - finalWidth + metrics.BorderSize;
            int y = panel.Y;

            List<ContextMenuCommandRenderData> commands = new List<ContextMenuCommandRenderData>();
            foreach (ContextMenuCommandItem command in panel.Commands)
            {
                commands.Add(
                    new ContextMenuCommandRenderData
                    {
                        Text = command.Text,
                        Color = GetCommandColor(command, panel.Visuals),
                        Item = command,
                    }
                );
            }

            renderPanels.Add(
                new ContextMenuPanelRenderData
                {
                    X = x,
                    Y = y,
                    Width = panel.Width,
                    Commands = commands,
                }
            );
        }

        Render(new ContextMenuRenderData { Panels = renderPanels });
    }

    public void Render(ContextMenuRenderData data)
    {
        EnsureBound();

        if (data?.Panels == null || data.Panels.Count == 0)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        dismissHitAreaImage.gameObject.SetActive(true);
        for (int i = 0; i < data.Panels.Count; i++)
        {
            ContextMenuPanelView panel = GetPanel(i);
            panel.Render(data.Panels[i]);
        }

        for (int i = data.Panels.Count; i < panelViews.Count; i++)
            panelViews[i].Hide();
    }

    public void Hide()
    {
        foreach (ContextMenuPanelView panel in panelViews)
            panel.Hide();

        if (dismissHitAreaImage != null)
            dismissHitAreaImage.gameObject.SetActive(false);
    }

    public ContextMenuMetrics GetMetrics()
    {
        EnsureBound();
        return panelPrefab.GetMetrics();
    }

    public void RequestDismiss(PointerEventData eventData)
    {
        if (Open)
            DismissRequested?.Invoke(eventData);
    }

    public int GetMenuWidth(int width, IReadOnlyList<ContextMenuCommandItem> commands)
    {
        EnsureBound();
        return panelPrefab.GetPanelWidth(width, HasIconColumn(commands), commands);
    }

    private void Awake()
    {
        TryBind();
    }

    private void OnDestroy()
    {
        if (dismissBoundary != null)
            dismissBoundary.PointerDown -= HandleDismissBoundaryPointerDown;
    }

    private void VerifyReferences()
    {
        if (panelPrefab == null)
            throw new MissingReferenceException($"{name}/PanelTemplate is missing.");
        if (dismissHitAreaImage == null)
            throw new MissingReferenceException($"{name}/DismissHitAreaImage is missing.");
        if (dismissBoundary == null)
            throw new MissingReferenceException($"{name}/DismissBoundary is missing.");
    }

    private void EnsureBound()
    {
        VerifyReferences();
        Bind();
    }

    private bool TryBind()
    {
        if (panelPrefab == null || dismissHitAreaImage == null || dismissBoundary == null)
            return false;

        Bind();
        return true;
    }

    private void Bind()
    {
        if (bound)
            return;

        dismissBoundary.PointerDown -= HandleDismissBoundaryPointerDown;
        dismissBoundary.PointerDown += HandleDismissBoundaryPointerDown;
        panelPrefab.gameObject.SetActive(false);
        Hide();
        bound = true;
    }

    private ContextMenuPanelView GetPanel(int index)
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

    private void AdjustVisibility(ContextMenuPanel panel, ContextMenuMetrics metrics)
    {
        Vector2Int surfaceSize = GetSurfaceSize();
        int width = GetMenuWidth(panel);
        int height = metrics.GetPanelHeight(panel.Commands.Count);
        if (surfaceSize.x > 0 && panel.X + width >= surfaceSize.x)
            panel.X -= width;
        if (surfaceSize.y > 0 && panel.Y + height >= surfaceSize.y)
            panel.Y -= height;
    }

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

    private int GetMenuWidth(ContextMenuPanel panel)
    {
        EnsureBound();
        return panelPrefab.GetPanelWidth(panel.Width, panel.HasIconColumn, panel.Commands);
    }

    private static bool HasIconColumn(IReadOnlyList<ContextMenuCommandItem> commands)
    {
        if (commands == null)
            return false;

        for (int i = 0; i < commands.Count; i++)
        {
            if (commands[i]?.UsesIconColumn == true)
                return true;
        }

        return false;
    }

    private static Color32 GetCommandColor(
        ContextMenuCommandItem command,
        ContextMenuVisuals visuals
    )
    {
        if (command?.Enabled != true)
            return visuals.DisabledColor;

        return command.Active ? visuals.ActiveColor : visuals.EnabledColor;
    }

    private void HandleCommandSelected(IContextMenuCommand command)
    {
        if (command?.Enabled == true)
            CommandSelected?.Invoke(command);
    }

    private void HandleCommandStateChanged()
    {
        if (Open)
            RenderCurrent();
    }

    private void HandleDismissBoundaryPointerDown(PointerEventData eventData)
    {
        RequestDismiss(eventData);
    }

    private sealed class ContextMenuPanel
    {
        public int X;
        public int Y;
        public int Width;
        public bool Visible;
        public bool HasIconColumn;
        public List<ContextMenuCommandItem> Commands = new List<ContextMenuCommandItem>();
        public ContextMenuVisuals Visuals = ContextMenuVisuals.Default;
    }

    public sealed class ContextMenuVisuals
    {
        public static readonly ContextMenuVisuals Default = new ContextMenuVisuals(
            DefaultEnabledColor,
            DefaultEnabledColor,
            DefaultDisabledColor
        );

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

public sealed class ContextMenuCommandItem
{
    public ContextMenuCommandItem(
        IContextMenuCommand command,
        Texture iconTexture = null,
        Texture activeIconTexture = null,
        bool usesIconColumn = false
    )
    {
        Command = command ?? throw new System.ArgumentNullException(nameof(command));
        IconTexture = iconTexture;
        ActiveIconTexture = activeIconTexture;
        UsesIconColumn = usesIconColumn || iconTexture != null || activeIconTexture != null;
    }

    public IContextMenuCommand Command { get; }
    public string Text => Command.Text;
    public bool Enabled => Command.Enabled;
    public bool Active { get; set; }
    public bool UsesIconColumn { get; }
    public Texture IconTexture { get; }
    public Texture ActiveIconTexture { get; }

    public Texture GetIconTexture()
    {
        if (Active && ActiveIconTexture != null)
            return ActiveIconTexture;

        return IconTexture;
    }
}

public readonly struct ContextMenuMetrics
{
    public ContextMenuMetrics(
        int rowHeight,
        int commandHitTopOffset,
        int commandHitHeight,
        int iconColumnWidth,
        int iconPanelWidth,
        int borderSize
    )
    {
        RowHeight = rowHeight;
        CommandHitTopOffset = commandHitTopOffset;
        CommandHitHeight = commandHitHeight;
        IconColumnWidth = iconColumnWidth;
        IconPanelWidth = iconPanelWidth;
        BorderSize = borderSize;
    }

    public int RowHeight { get; }
    public int CommandHitTopOffset { get; }
    public int CommandHitHeight { get; }
    public int IconColumnWidth { get; }
    public int IconPanelWidth { get; }
    public int BorderSize { get; }

    public int GetPanelWidth(int baseWidth, bool hasIconColumn)
    {
        return baseWidth + (hasIconColumn ? IconPanelWidth : 0);
    }

    public int GetPanelHeight(int commandCount)
    {
        return commandCount * RowHeight + BorderSize * 2;
    }
}

public sealed class ContextMenuRenderData
{
    public IReadOnlyList<ContextMenuPanelRenderData> Panels { get; set; }
}

public sealed class ContextMenuPanelRenderData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public IReadOnlyList<ContextMenuCommandRenderData> Commands { get; set; }
}

public sealed class ContextMenuCommandRenderData
{
    public string Text { get; set; }
    public Color32 Color { get; set; }
    public ContextMenuCommandItem Item { get; set; }
}
