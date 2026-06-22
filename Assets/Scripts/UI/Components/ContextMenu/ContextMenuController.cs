using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ContextMenuRequest
{
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

public interface IContextMenuCommand
{
    string Text { get; }
    bool Enabled { get; }
}

public interface IContextMenuReceiver
{
    void OnContextMenuCommandSelected(ContextMenuRequest request, IContextMenuCommand command);
    void OnContextMenuCancelled(ContextMenuRequest request);
}

public sealed class ContextMenuController : ICancelable
{
    private ContextMenuRequest activeRequest;

    public bool IsOpen => activeRequest != null;
    public ContextMenuRequest ActiveRequest => activeRequest;

    public void Open(ContextMenuRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        Cancel();

        activeRequest = request;
    }

    public bool TrySelectCommand(IContextMenuCommand command)
    {
        if (
            activeRequest == null
            || command == null
            || !command.Enabled
            || !activeRequest.Commands.Contains(command)
        )
            return false;

        ContextMenuRequest request = activeRequest;
        activeRequest = null;

        request.Receiver.OnContextMenuCommandSelected(request, command);
        return true;
    }

    public void Cancel()
    {
        if (activeRequest == null)
            return;

        ContextMenuRequest request = activeRequest;
        activeRequest = null;

        request.Receiver.OnContextMenuCancelled(request);
    }

    public bool TryCancel()
    {
        if (activeRequest == null)
            return false;

        Cancel();
        return true;
    }
}
