using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ContextMenuPanelView : MonoBehaviour
{
    private readonly List<ContextMenuCommandView> commandViews = new List<ContextMenuCommandView>();
    private bool capturedTemplateLayout;
    private bool bound;
    private int borderSize;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private Image borderTopImage;

    [SerializeField]
    private Image borderBottomImage;

    [SerializeField]
    private Image borderLeftImage;

    [SerializeField]
    private Image borderRightImage;

    [SerializeField]
    private ContextMenuCommandView commandPrefab;

    internal event System.Action<IContextMenuCommand> CommandSelected;
    internal event System.Action CommandStateChanged;

    public void Render(ContextMenuPanelRenderData data)
    {
        EnsureBound();

        RectTransform rect = transform as RectTransform;
        bool hasIconColumn = HasIconColumn(data.Commands);
        ContextMenuMetrics metrics = GetMetrics();
        int width = GetPanelWidth(data.Width, hasIconColumn, data.Commands);
        int height = metrics.GetPanelHeight(data.Commands.Count);
        UILayout.SetSourceRect(rect, data.X, data.Y, width, height);

        backgroundImage.color = new Color(0f, 0f, 0f, 0.8f);
        UILayout.SetStretch(backgroundImage.rectTransform);

        UILayout.SetSourceRect(borderTopImage.rectTransform, 0, 0, width, borderSize);
        UILayout.SetSourceRect(
            borderBottomImage.rectTransform,
            0,
            height - borderSize,
            width,
            borderSize
        );
        UILayout.SetSourceRect(borderLeftImage.rectTransform, 0, 0, borderSize, height);
        UILayout.SetSourceRect(
            borderRightImage.rectTransform,
            width - borderSize,
            0,
            borderSize,
            height
        );

        for (int i = 0; i < data.Commands.Count; i++)
        {
            ContextMenuCommandView command = GetCommand(i);
            command.Render(data.Commands[i], hasIconColumn, width, i);
        }

        for (int i = data.Commands.Count; i < commandViews.Count; i++)
            commandViews[i].Hide();

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public ContextMenuMetrics GetMetrics()
    {
        EnsureBound();
        return new ContextMenuMetrics(
            commandPrefab.RowHeight,
            commandPrefab.CommandHitTopOffset,
            commandPrefab.CommandHitHeight,
            commandPrefab.IconColumnWidth,
            commandPrefab.IconPanelWidth,
            borderSize
        );
    }

    internal int GetPanelWidth(
        int sourceWidth,
        bool hasIconColumn,
        IReadOnlyList<ContextMenuCommandItem> commands
    )
    {
        ContextMenuMetrics metrics = GetMetrics();
        int width = metrics.GetPanelWidth(sourceWidth, hasIconColumn);
        if (commands == null)
            return width;

        for (int i = 0; i < commands.Count; i++)
            width = Mathf.Max(
                width,
                commandPrefab.GetMinimumPanelWidth(commands[i]?.Text, hasIconColumn)
            );

        return width;
    }

    private int GetPanelWidth(
        int sourceWidth,
        bool hasIconColumn,
        IReadOnlyList<ContextMenuCommandRenderData> commands
    )
    {
        ContextMenuMetrics metrics = GetMetrics();
        int width = metrics.GetPanelWidth(sourceWidth, hasIconColumn);
        if (commands == null)
            return width;

        for (int i = 0; i < commands.Count; i++)
            width = Mathf.Max(
                width,
                commandPrefab.GetMinimumPanelWidth(commands[i]?.Text, hasIconColumn)
            );

        return width;
    }

    private void Awake()
    {
        TryBind();
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (borderTopImage == null)
            throw new MissingReferenceException($"{name}/BorderTopImage is missing.");
        if (borderBottomImage == null)
            throw new MissingReferenceException($"{name}/BorderBottomImage is missing.");
        if (borderLeftImage == null)
            throw new MissingReferenceException($"{name}/BorderLeftImage is missing.");
        if (borderRightImage == null)
            throw new MissingReferenceException($"{name}/BorderRightImage is missing.");
        if (commandPrefab == null)
            throw new MissingReferenceException($"{name}/CommandTemplate is missing.");
    }

    private void EnsureBound()
    {
        VerifyReferences();
        Bind();
    }

    private bool TryBind()
    {
        if (
            backgroundImage == null
            || borderTopImage == null
            || borderBottomImage == null
            || borderLeftImage == null
            || borderRightImage == null
            || commandPrefab == null
        )
        {
            return false;
        }

        Bind();
        return true;
    }

    private void Bind()
    {
        if (bound)
            return;

        CaptureTemplateLayout();
        commandPrefab.gameObject.SetActive(false);
        bound = true;
    }

    private ContextMenuCommandView GetCommand(int index)
    {
        while (commandViews.Count <= index)
        {
            ContextMenuCommandView view = Instantiate(commandPrefab, transform);
            view.name = $"Command{commandViews.Count}";
            view.Selected += HandleCommandSelected;
            view.StateChanged += HandleCommandStateChanged;
            commandViews.Add(view);
        }

        return commandViews[index];
    }

    private void CaptureTemplateLayout()
    {
        if (capturedTemplateLayout)
            return;

        borderSize = UILayout.GetSourceRect(borderTopImage.rectTransform).height;
        commandPrefab.CaptureTemplateLayout();
        capturedTemplateLayout = true;
    }

    private static bool HasIconColumn(IReadOnlyList<ContextMenuCommandRenderData> commands)
    {
        if (commands == null)
            return false;

        for (int i = 0; i < commands.Count; i++)
        {
            if (commands[i].Item?.UsesIconColumn == true)
                return true;
        }

        return false;
    }

    private void HandleCommandSelected(IContextMenuCommand command)
    {
        CommandSelected?.Invoke(command);
    }

    private void HandleCommandStateChanged()
    {
        CommandStateChanged?.Invoke();
    }
}
