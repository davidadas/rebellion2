using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.PlayerRequests;

/// <summary>
/// Owns coordinated execution and persistence from session creation through release.
/// Presentation reports readiness without replacing the session's execution authority.
/// </summary>
public interface IGameSessionOwner : IPlayerRequestDispatcher
{
    /// <summary>
    /// Gets whether this session can accept a save request under its current connection state.
    /// </summary>
    bool CanRequestSave { get; }

    /// <summary>
    /// Saves through the session's current connected or disconnected policy.
    /// </summary>
    /// <param name="fileName">The requested save identifier.</param>
    /// <param name="displayName">The requested display name, or null for the default label.</param>
    /// <returns>True after the session's required save publications complete.</returns>
    Task<bool> SaveGameAsync(string fileName, string displayName);

    /// <summary>
    /// Loads through the session's coordination policy without falling back to a local load.
    /// </summary>
    /// <param name="fileName">The selected save identifier.</param>
    /// <returns>True after the coordinated load completes.</returns>
    Task<bool> LoadGameAsync(string fileName);

    /// <summary>
    /// Handles a completed tick using this session's autosave policy.
    /// </summary>
    /// <param name="settings">The current autosave preferences.</param>
    void ProcessAutosaveTick(UserGameplaySettings settings);

    /// <summary>
    /// Establishes participant ownership before simulation services are constructed.
    /// </summary>
    /// <param name="game">The graph entering the coordinated session.</param>
    void PrepareGame(GameRoot game);

    /// <summary>
    /// Connects the declared owner to the newly constructed runtime before presentation starts.
    /// </summary>
    /// <param name="runtime">The runtime that owns the session.</param>
    void BindSessionManager(GameSessionManager runtime);

    /// <summary>
    /// Receives readiness from the presentation belonging to this runtime.
    /// </summary>
    /// <param name="runtime">The runtime whose presentation is ready.</param>
    void NotifyPresentationReady(GameSessionManager runtime);

    /// <summary>
    /// Releases references and work belonging to the outgoing runtime.
    /// </summary>
    /// <param name="runtime">The runtime ending its session.</param>
    void ReleaseSessionManager(GameSessionManager runtime);

    /// <summary>
    /// Admits a simulation tick through the coordinated clock and peer protocol.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed application frame interval.</param>
    /// <returns>True when the local session should begin its next tick.</returns>
    bool TryBeginGameTick(float elapsedSeconds);

    /// <summary>
    /// Keeps a completed campaign connected until its final state and both presentations are acknowledged.
    /// </summary>
    /// <returns>True after coordinated campaign completion, or false after interruption.</returns>
    Task<bool> WaitForCampaignCompletionAsync();
}
