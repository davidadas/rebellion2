using System;

/// <summary>
/// Exposes the application Options surface to a scene that owns its modal presentation.
/// </summary>
public interface IOptionsMenuPresenter : IDisposable
{
    bool IsOpen { get; }

    /// <summary>
    /// Opens or focuses the application Options surface.
    /// </summary>
    /// <param name="initialTab">The initial page.</param>
    void Open(OptionsMenuTab initialTab = OptionsMenuTab.Gameplay);

    /// <summary>
    /// Closes the application Options surface.
    /// </summary>
    void Close();

    /// <summary>
    /// Projects the current settings and window state.
    /// </summary>
    void RenderWindows();
}
