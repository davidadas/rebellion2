/// <summary>
/// Handles cancellation of the most immediate operation owned by a UI feature.
/// </summary>
public interface ICancelable
{
    /// <summary>
    /// Tries to cancel the current operation.
    /// </summary>
    /// <returns><see langword="true"/> when cancellation was handled.</returns>
    bool TryCancel();
}
