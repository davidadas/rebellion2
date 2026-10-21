using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Describes one completed operation that may have changed a planet's orbital state.
    /// </summary>
    internal readonly struct OrbitalStateChanged
    {
        internal Planet Planet { get; }
        internal bool WasBlockaded { get; }
        internal ICollection<GameResult> Results { get; }

        /// <summary>
        /// Creates an orbital-state notification.
        /// </summary>
        /// <param name="planet">The affected planet.</param>
        /// <param name="wasBlockaded">Whether the planet was blockaded before the operation.</param>
        /// <param name="results">The operation's result collection.</param>
        internal OrbitalStateChanged(
            Planet planet,
            bool wasBlockaded,
            ICollection<GameResult> results
        )
        {
            Planet = planet;
            WasBlockaded = wasBlockaded;
            Results = results ?? throw new ArgumentNullException(nameof(results));
        }
    }

    /// <summary>
    /// Publishes completed orbital-state changes to their simulation observers.
    /// </summary>
    public sealed class OrbitalStateNotifications
    {
        internal event Action<OrbitalStateChanged> Changed;

        /// <summary>
        /// Creates an unconnected notification source for the factory to initialize.
        /// </summary>
        private OrbitalStateNotifications() { }

        /// <summary>
        /// Creates the notification source and connects its blockade observer.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <returns>The connected notification source.</returns>
        internal static OrbitalStateNotifications Create(GameRoot game)
        {
            OrbitalStateNotifications notifications = new OrbitalStateNotifications();
            _ = new BlockadeObserver(game, notifications);
            return notifications;
        }

        /// <summary>
        /// Notifies observers after an operation may have changed a planet's orbital state.
        /// </summary>
        /// <param name="planet">The affected planet.</param>
        /// <param name="wasBlockaded">Whether the planet was blockaded before the operation.</param>
        /// <param name="results">The operation's result collection.</param>
        internal void Publish(Planet planet, bool wasBlockaded, ICollection<GameResult> results)
        {
            Changed?.Invoke(new OrbitalStateChanged(planet, wasBlockaded, results));
        }
    }
}
