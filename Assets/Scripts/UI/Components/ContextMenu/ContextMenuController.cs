using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Describes one context-menu operation and its completion receiver.
/// </summary>
public sealed class ContextMenuRequest
{
    /// <summary>
    /// Creates a context-menu request.
    /// </summary>
    /// <param name="source">The feature state that opened the menu.</param>
    /// <param name="commands">The commands available in the menu.</param>
    /// <param name="receiver">The menu completion receiver.</param>
    public ContextMenuRequest(
        object source,
        IReadOnlyList<IContextMenuCommand> commands,
        IContextMenuReceiver receiver
    )
    {
        Source = source;
        Commands = commands?.ToList() ?? new List<IContextMenuCommand>();
        Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
    }

    public object Source { get; }

    public IReadOnlyList<IContextMenuCommand> Commands { get; }

    public IContextMenuReceiver Receiver { get; }
}

/// <summary>
/// Provides the text and availability of a context-menu command.
/// </summary>
public interface IContextMenuCommand
{
    string Text { get; }

    bool Enabled { get; }
}

/// <summary>
/// Exposes nested commands owned by one context-menu command.
/// </summary>
public interface IContextMenuParentCommand : IContextMenuCommand
{
    IReadOnlyList<IContextMenuCommand> ChildCommands { get; }
}

/// <summary>
/// Receives selection or cancellation of a context-menu request.
/// </summary>
public interface IContextMenuReceiver
{
    /// <summary>
    /// Handles selection of an enabled command.
    /// </summary>
    /// <param name="request">The completed menu request.</param>
    /// <param name="command">The selected command.</param>
    void OnContextMenuCommandSelected(ContextMenuRequest request, IContextMenuCommand command);

    /// <summary>
    /// Handles cancellation of a menu request.
    /// </summary>
    /// <param name="request">The canceled menu request.</param>
    void OnContextMenuCancelled(ContextMenuRequest request);
}

/// <summary>
/// Owns the lifecycle and completion routing of the current context menu.
/// </summary>
public sealed class ContextMenuController : ICancelable
{
    private ContextMenuRequest activeRequest;

    public bool IsOpen => activeRequest != null;

    public ContextMenuRequest ActiveRequest => activeRequest;

    /// <summary>
    /// Opens a context menu after canceling any previous request.
    /// </summary>
    /// <param name="request">The request to open.</param>
    public void Open(ContextMenuRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        Cancel();

        activeRequest = request;
    }

    /// <summary>
    /// Completes the active request with one of its enabled commands.
    /// </summary>
    /// <param name="command">The selected command.</param>
    /// <returns><see langword="true"/> when the active request accepted the command.</returns>
    public bool TrySelectCommand(IContextMenuCommand command)
    {
        if (
            activeRequest is null
            || command?.Enabled != true
            || !ContainsCommand(activeRequest.Commands, command)
        )
            return false;

        ContextMenuRequest request = activeRequest;
        activeRequest = null;

        request.Receiver.OnContextMenuCommandSelected(request, command);
        return true;
    }

    /// <summary>
    /// Determines whether a command belongs to a request's complete command tree.
    /// </summary>
    /// <param name="commands">The command tree to search.</param>
    /// <param name="target">The command being selected.</param>
    /// <returns><see langword="true"/> when the command belongs to the tree.</returns>
    private static bool ContainsCommand(
        IReadOnlyList<IContextMenuCommand> commands,
        IContextMenuCommand target
    )
    {
        for (int index = 0; index < commands.Count; index++)
        {
            IContextMenuCommand command = commands[index];
            if (ReferenceEquals(command, target))
                return true;

            if (
                command is IContextMenuParentCommand parent
                && parent.ChildCommands != null
                && ContainsCommand(parent.ChildCommands, target)
            )
                return true;
        }

        return false;
    }

    /// <summary>
    /// Cancels the active context-menu request.
    /// </summary>
    public void Cancel()
    {
        if (activeRequest == null)
            return;

        ContextMenuRequest request = activeRequest;
        activeRequest = null;

        request.Receiver.OnContextMenuCancelled(request);
    }

    /// <summary>
    /// Tries to cancel the active context-menu request.
    /// </summary>
    /// <returns><see langword="true"/> when a request was canceled.</returns>
    public bool TryCancel()
    {
        if (activeRequest == null)
            return false;

        Cancel();
        return true;
    }
}
