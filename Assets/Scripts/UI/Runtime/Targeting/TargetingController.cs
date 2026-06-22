using System;

public interface ITargetable
{
    object Target { get; }
}

public interface ITargetingCursor
{
    void Show(int x, int y);
    void MoveTo(int x, int y);
    void Hide();
}

public sealed class TargetingRequest
{
    public TargetingRequest(string prompt, object source, ITargetingReceiver receiver)
    {
        Prompt = prompt ?? string.Empty;
        Source = source;
        Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
    }

    public string Prompt { get; }
    public object Source { get; }
    public ITargetingReceiver Receiver { get; }
}

public interface ITargetingReceiver
{
    void OnTargetSelected(TargetingRequest request, object target);
    void OnTargetingCancelled(TargetingRequest request);
}

public sealed class TargetingController : ICancelable
{
    private readonly ITargetingCursor cursor;
    private TargetingRequest activeRequest;
    private bool cursorVisible;

    public TargetingController(ITargetingCursor cursor = null)
    {
        this.cursor = cursor;
    }

    public bool IsTargeting => activeRequest != null;
    public TargetingRequest ActiveRequest => activeRequest;

    public void Begin(TargetingRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        Cancel();

        activeRequest = request;
        cursorVisible = false;
    }

    public void Begin(TargetingRequest request, int cursorX, int cursorY)
    {
        Begin(request);
        cursorVisible = true;
        cursor?.Show(cursorX, cursorY);
    }

    public void MoveCursor(int x, int y)
    {
        if (activeRequest != null && cursorVisible)
            cursor?.MoveTo(x, y);
    }

    public bool TrySelectTarget(ITargetable targetable)
    {
        if (activeRequest == null || targetable == null)
            return false;

        TargetingRequest request = activeRequest;
        activeRequest = null;
        HideCursor();

        request.Receiver.OnTargetSelected(request, targetable.Target);
        return true;
    }

    public void Cancel()
    {
        if (activeRequest == null)
            return;

        TargetingRequest request = activeRequest;
        activeRequest = null;
        HideCursor();

        request.Receiver.OnTargetingCancelled(request);
    }

    public bool TryCancel()
    {
        if (activeRequest == null)
            return false;

        Cancel();
        return true;
    }

    private void HideCursor()
    {
        if (!cursorVisible)
            return;

        cursorVisible = false;
        cursor?.Hide();
    }
}
