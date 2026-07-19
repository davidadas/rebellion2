using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one context-menu panel from an authored command-row template.
/// </summary>
public sealed class ContextMenuPanelView : MonoBehaviour
{
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

    private readonly List<ContextMenuCommandView> commandViews = new List<ContextMenuCommandView>();
    private bool initialized;
    private int borderSize;

    /// <summary>
    /// Raised when one rendered row invokes its command.
    /// </summary>
    internal event System.Action<IContextMenuCommand> CommandSelected;

    /// <summary>
    /// Raised when one rendered row changes its active state.
    /// </summary>
    internal event System.Action<ContextMenuCommandItem> CommandStateChanged;

    /// <summary>
    /// Detaches listeners from command rows owned by this panel.
    /// </summary>
    private void OnDestroy()
    {
        for (int index = 0; index < commandViews.Count; index++)
        {
            ContextMenuCommandView view = commandViews[index];
            if (view == null)
                continue;

            view.Selected -= HandleCommandSelected;
            view.StateChanged -= HandleCommandStateChanged;
        }
    }

    /// <summary>
    /// Renders one panel and its ordered command rows.
    /// </summary>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    /// <param name="sourceWidth">The authored base width.</param>
    /// <param name="commands">The ordered commands to render.</param>
    /// <param name="visuals">The command-state colors.</param>
    internal void Render(
        int x,
        int y,
        int sourceWidth,
        IReadOnlyList<ContextMenuCommandItem> commands,
        ContextMenuView.ContextMenuVisuals visuals
    )
    {
        InitializeView();

        int commandCount = commands?.Count ?? 0;
        bool hasIconColumn = HasIconColumn(commands);
        ContextMenuMetrics metrics = GetMetrics();
        int width = GetPanelWidth(sourceWidth, hasIconColumn, commands);
        int height = metrics.GetPanelHeight(commandCount);
        UILayout.SetSourceRect(transform as RectTransform, x, y, width, height);

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

        for (int index = 0; index < commandCount; index++)
        {
            ContextMenuCommandItem item = commands[index];
            GetCommandView(index)
                .Render(item, GetCommandColor(item, visuals), hasIconColumn, width, index);
        }

        for (int index = commandCount; index < commandViews.Count; index++)
            commandViews[index].Hide();

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides this panel without discarding its reusable command rows.
    /// </summary>
    internal void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Gets the authored measurements used for panel sizing and placement.
    /// </summary>
    /// <returns>The authored context-menu measurements.</returns>
    internal ContextMenuMetrics GetMetrics()
    {
        InitializeView();
        return new ContextMenuMetrics(
            commandPrefab.RowHeight,
            commandPrefab.IconPanelWidth,
            borderSize
        );
    }

    /// <summary>
    /// Calculates the rendered width required by one command list.
    /// </summary>
    /// <param name="sourceWidth">The authored base width.</param>
    /// <param name="commands">The commands whose labels must fit.</param>
    /// <returns>The required rendered width in source units.</returns>
    internal int GetPanelWidth(int sourceWidth, IReadOnlyList<ContextMenuCommandItem> commands)
    {
        InitializeView();
        return GetPanelWidth(sourceWidth, HasIconColumn(commands), commands);
    }

    /// <summary>
    /// Calculates the rendered width required by one measured command list.
    /// </summary>
    /// <param name="sourceWidth">The authored base width.</param>
    /// <param name="hasIconColumn">Whether the panel reserves an icon column.</param>
    /// <param name="commands">The commands whose labels must fit.</param>
    /// <returns>The required rendered width in source units.</returns>
    private int GetPanelWidth(
        int sourceWidth,
        bool hasIconColumn,
        IReadOnlyList<ContextMenuCommandItem> commands
    )
    {
        InitializeView();

        int width = GetMetrics().GetPanelWidth(sourceWidth, hasIconColumn);
        if (commands == null)
            return width;

        for (int index = 0; index < commands.Count; index++)
        {
            width = Mathf.Max(
                width,
                commandPrefab.GetMinimumPanelWidth(commands[index]?.Text, hasIconColumn)
            );
        }

        return width;
    }

    /// <summary>
    /// Validates and initializes the authored panel template once.
    /// </summary>
    private void InitializeView()
    {
        if (initialized)
            return;

        VerifyReferences();
        borderSize = UILayout.GetSourceRect(borderTopImage.rectTransform).height;
        commandPrefab.gameObject.SetActive(false);
        initialized = true;
    }

    /// <summary>
    /// Verifies that every authored panel reference is assigned.
    /// </summary>
    private void VerifyReferences()
    {
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

    /// <summary>
    /// Gets or creates one command row from the authored row template.
    /// </summary>
    /// <param name="index">The zero-based command-row index.</param>
    /// <returns>The command-row instance at the requested index.</returns>
    private ContextMenuCommandView GetCommandView(int index)
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

    /// <summary>
    /// Determines whether a command list reserves an icon column.
    /// </summary>
    /// <param name="commands">The commands to inspect.</param>
    /// <returns><see langword="true"/> when at least one command uses the icon column.</returns>
    private static bool HasIconColumn(IReadOnlyList<ContextMenuCommandItem> commands)
    {
        if (commands == null)
            return false;

        for (int index = 0; index < commands.Count; index++)
        {
            if (commands[index]?.UsesIconColumn == true)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one command's color from its availability and active state.
    /// </summary>
    /// <param name="item">The command item to present.</param>
    /// <param name="visuals">The panel's command-state colors.</param>
    /// <returns>The resolved command color.</returns>
    private static Color32 GetCommandColor(
        ContextMenuCommandItem item,
        ContextMenuView.ContextMenuVisuals visuals
    )
    {
        if (item?.Enabled != true)
            return visuals.DisabledColor;

        return item.Active ? visuals.ActiveColor : visuals.EnabledColor;
    }

    /// <summary>
    /// Forwards command selection from one rendered row.
    /// </summary>
    /// <param name="command">The selected command.</param>
    private void HandleCommandSelected(IContextMenuCommand command)
    {
        CommandSelected?.Invoke(command);
    }

    /// <summary>
    /// Forwards an active-state transition from one rendered row.
    /// </summary>
    /// <param name="item">The command item whose state changed.</param>
    private void HandleCommandStateChanged(ContextMenuCommandItem item)
    {
        CommandStateChanged?.Invoke(item);
    }
}
