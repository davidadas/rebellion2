using Rebellion.Game;

namespace Rebellion.App
{
    /// <summary>
    /// Owns coordinated execution and persistence from session creation through release.
    /// Presentation reports readiness without replacing the session's execution authority.
    /// </summary>
    public interface IGameSessionOwner : IPlayerActionDispatcher, IGamePersistenceCoordinator
    {
        /// <summary>
        /// Establishes participant ownership before simulation services are constructed.
        /// </summary>
        /// <param name="game">The graph entering the coordinated session.</param>
        void PrepareGame(GameRoot game);

        /// <summary>
        /// Connects the declared owner to the newly constructed runtime before presentation starts.
        /// </summary>
        /// <param name="runtime">The runtime that owns the session.</param>
        void BindRuntime(GameRuntime runtime);

        /// <summary>
        /// Receives readiness from the presentation belonging to this runtime.
        /// </summary>
        /// <param name="runtime">The runtime whose presentation is ready.</param>
        void NotifyPresentationReady(GameRuntime runtime);

        /// <summary>
        /// Releases references and work belonging to the outgoing runtime.
        /// </summary>
        /// <param name="runtime">The runtime ending its session.</param>
        void ReleaseRuntime(GameRuntime runtime);

        /// <summary>
        /// Admits a simulation tick through the coordinated clock and peer protocol.
        /// </summary>
        /// <param name="elapsedSeconds">The elapsed application frame interval.</param>
        /// <returns>True when the local session should begin its next tick.</returns>
        bool TryBeginGameTick(float elapsedSeconds);
    }
}
