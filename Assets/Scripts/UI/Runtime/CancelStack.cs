using System.Collections.Generic;

/// <summary>
/// Routes cancellation to registered handlers in reverse registration order.
/// </summary>
public sealed class CancelStack
{
    private readonly List<ICancelable> cancelables = new List<ICancelable>();

    /// <summary>
    /// Promotes a cancellation handler to the top of the stack.
    /// </summary>
    /// <param name="cancelable">The handler to register.</param>
    public void Register(ICancelable cancelable)
    {
        if (cancelable == null)
            return;

        cancelables.Remove(cancelable);
        cancelables.Add(cancelable);
    }

    /// <summary>
    /// Removes a cancellation handler from the stack.
    /// </summary>
    /// <param name="cancelable">The handler to remove.</param>
    public void Unregister(ICancelable cancelable)
    {
        if (cancelable == null)
            return;

        cancelables.Remove(cancelable);
    }

    /// <summary>
    /// Gives registered handlers a reverse-order opportunity to cancel.
    /// </summary>
    /// <returns><see langword="true"/> when a handler accepted cancellation.</returns>
    public bool TryCancel()
    {
        for (int index = cancelables.Count - 1; index >= 0; index--)
        {
            if (cancelables[index]?.TryCancel() == true)
                return true;
        }

        return false;
    }
}
