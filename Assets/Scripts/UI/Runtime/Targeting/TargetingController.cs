using System;

/// <summary>
/// Exposes the domain target represented by an interactive UI element.
/// </summary>
public interface ITargetable
{
    /// <summary>
    /// Gets the target.
    /// </summary>
    object Target { get; }
}

/// <summary>
/// Presents and positions the cursor used during a targeting operation.
/// </summary>
public interface ITargetingCursor
{
    /// <summary>
    /// Shows the targeting cursor at a source-space position.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    void Show(int x, int y);

    /// <summary>
    /// Moves the targeting cursor to a source-space position.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    void MoveTo(int x, int y);

    /// <summary>
    /// Hides the targeting cursor.
    /// </summary>
    void Hide();
}

/// <summary>
/// Describes one active targeting operation and its completion receiver.
/// </summary>
public sealed class TargetingRequest
{
    /// <summary>
    /// Creates a targeting request.
    /// </summary>
    /// <param name="prompt">The prompt associated with the operation.</param>
    /// <param name="source">The feature state that initiated targeting.</param>
    /// <param name="receiver">The targeting completion receiver.</param>
    public TargetingRequest(string prompt, object source, ITargetingReceiver receiver)
    {
        Prompt = prompt ?? string.Empty;
        Source = source;
        Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
    }

    /// <summary>
    /// Gets the prompt.
    /// </summary>
    public string Prompt { get; }

    /// <summary>
    /// Gets the source.
    /// </summary>
    public object Source { get; }

    /// <summary>
    /// Gets the receiver.
    /// </summary>
    public ITargetingReceiver Receiver { get; }
}

/// <summary>
/// Receives completion or cancellation of a targeting request.
/// </summary>
public interface ITargetingReceiver
{
    /// <summary>
    /// Handles successful target selection.
    /// </summary>
    /// <param name="request">The completed request.</param>
    /// <param name="target">The selected domain target.</param>
    void OnTargetSelected(TargetingRequest request, object target);

    /// <summary>
    /// Handles targeting cancellation.
    /// </summary>
    /// <param name="request">The canceled request.</param>
    void OnTargetingCancelled(TargetingRequest request);
}

/// <summary>
/// Owns the lifecycle of the current targeting request and its optional cursor.
/// </summary>
public sealed class TargetingController : ICancelable
{
    private readonly ITargetingCursor cursor;
    private TargetingRequest activeRequest;
    private bool cursorVisible;

    /// <summary>
    /// Creates a targeting controller with an optional cursor presenter.
    /// </summary>
    /// <param name="cursor">The cursor presenter, or <see langword="null"/>.</param>
    public TargetingController(ITargetingCursor cursor = null)
    {
        this.cursor = cursor;
    }

    /// <summary>
    /// Gets a value indicating whether targeting is active.
    /// </summary>
    public bool IsTargeting => activeRequest != null;

    /// <summary>
    /// Gets the active request.
    /// </summary>
    public TargetingRequest ActiveRequest => activeRequest;

    /// <summary>
    /// Starts a targeting request without showing a cursor.
    /// </summary>
    /// <param name="request">The request to start.</param>
    public void Begin(TargetingRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        Cancel();

        activeRequest = request;
        cursorVisible = false;
    }

    /// <summary>
    /// Starts a targeting request and shows its cursor.
    /// </summary>
    /// <param name="request">The request to start.</param>
    /// <param name="cursorX">The initial cursor x-coordinate.</param>
    /// <param name="cursorY">The initial cursor y-coordinate.</param>
    public void Begin(TargetingRequest request, int cursorX, int cursorY)
    {
        Begin(request);
        cursorVisible = true;
        cursor?.Show(cursorX, cursorY);
    }

    /// <summary>
    /// Moves the visible targeting cursor.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    public void MoveCursor(int x, int y)
    {
        if (activeRequest != null && cursorVisible)
            cursor?.MoveTo(x, y);
    }

    /// <summary>
    /// Completes the active request with an interactive target.
    /// </summary>
    /// <param name="targetable">The interactive target.</param>
    /// <returns><see langword="true"/> when an active request was completed.</returns>
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

    /// <summary>
    /// Cancels the active targeting request.
    /// </summary>
    public void Cancel()
    {
        if (activeRequest == null)
            return;

        TargetingRequest request = activeRequest;
        activeRequest = null;
        HideCursor();

        request.Receiver.OnTargetingCancelled(request);
    }

    /// <summary>
    /// Tries to cancel the active targeting request.
    /// </summary>
    /// <returns><see langword="true"/> when a request was canceled.</returns>
    public bool TryCancel()
    {
        if (activeRequest == null)
            return false;

        Cancel();
        return true;
    }

    /// <summary>
    /// Hides the cursor when it is currently presented.
    /// </summary>
    private void HideCursor()
    {
        if (!cursorVisible)
            return;

        cursorVisible = false;
        cursor?.Hide();
    }
}
