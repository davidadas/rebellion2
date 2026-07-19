using Rebellion.Game;
using Rebellion.Game.Units;

/// <summary>
/// Performs strategy-screen actions requested by the HUD and its advisor controls.
/// </summary>
public interface IStrategyHudActions
{
    /// <summary>
    /// Begins advisor-directed construction targeting.
    /// </summary>
    /// <param name="manufacturingType">The requested manufacturing category.</param>
    /// <param name="sourceX">The source-space horizontal request coordinate.</param>
    /// <param name="sourceY">The source-space vertical request coordinate.</param>
    void BeginAdvisorConstruction(ManufacturingType manufacturingType, int sourceX, int sourceY);

    /// <summary>
    /// Opens the protocol advisor command menu at a source-space pointer position.
    /// </summary>
    /// <param name="request">The advisor-owned menu request.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    void OpenAdvisorCommandContextMenu(ContextMenuRequest request, int sourceX, int sourceY);

    /// <summary>
    /// Opens the droid advisor notification menu at a source-space pointer position.
    /// </summary>
    /// <param name="request">The advisor-owned menu request.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    void OpenAdvisorNotificationContextMenu(ContextMenuRequest request, int sourceX, int sourceY);

    /// <summary>
    /// Opens an advisor report in the requested mode.
    /// </summary>
    /// <param name="mode">The report mode to display.</param>
    void OpenAdvisorReport(AdvisorReportMode mode);

    /// <summary>
    /// Opens the requested messages tab.
    /// </summary>
    /// <param name="tab">The semantic messages tab.</param>
    void OpenMessagesTab(MessagesTab tab);

    /// <summary>
    /// Opens the speed context menu at a source-space pointer position.
    /// </summary>
    /// <param name="request">The HUD-owned menu request.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    void OpenSpeedContextMenu(ContextMenuRequest request, int sourceX, int sourceY);

    /// <summary>
    /// Executes a released main HUD button at its source-space pointer position.
    /// </summary>
    /// <param name="action">The semantic HUD action.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    void ReleaseHudButton(StrategyHudAction action, int sourceX, int sourceY);

    /// <summary>
    /// Sets the simulation speed selected from the HUD speed menu.
    /// </summary>
    /// <param name="speed">The selected simulation speed.</param>
    void SetGameSpeed(TickSpeed speed);

    /// <summary>
    /// Invalidates the current strategy HUD render.
    /// </summary>
    void RequestHudRender();
}
