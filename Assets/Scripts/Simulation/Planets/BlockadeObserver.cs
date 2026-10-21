using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Converts completed orbital-state changes into blockade transition results.
    /// </summary>
    internal sealed class BlockadeObserver
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates an observer and subscribes it to orbital-state notifications.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="notifications">The orbital-state notification source.</param>
        internal BlockadeObserver(GameRoot game, OrbitalStateNotifications notifications)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            if (notifications == null)
                throw new ArgumentNullException(nameof(notifications));

            notifications.Changed += Observe;
        }

        /// <summary>
        /// Emits a blockade result when an orbital mutation changed the planet's blockade state.
        /// </summary>
        /// <param name="change">The completed orbital-state change.</param>
        private void Observe(OrbitalStateChanged change)
        {
            Planet planet = change.Planet;
            if (planet == null)
                return;

            bool isBlockaded = planet.IsBlockaded();
            if (change.WasBlockaded == isBlockaded)
                return;

            Fleet blockadingFleet = isBlockaded
                ? planet
                    .GetChildren<Fleet>()
                    .FirstOrDefault(fleet =>
                        fleet.Movement == null
                        && fleet.OwnerInstanceID != planet.OwnerInstanceID
                        && fleet.HasOperationalCapitalShips()
                    )
                : null;
            if (isBlockaded && blockadingFleet == null)
                return;

            change.Results.Add(
                new BlockadeChangedResult
                {
                    Planet = planet,
                    BlockadingFleet = blockadingFleet,
                    Blockaded = isBlockaded,
                    Tick = _game.CurrentTick,
                }
            );
        }
    }
}
