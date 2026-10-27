using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Resolves blockade transitions from state captured around an orbital mutation.
    /// </summary>
    public sealed class BlockadeTransitionResolver
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates a blockade transition resolver for the active game.
        /// </summary>
        /// <param name="game">The active game.</param>
        public BlockadeTransitionResolver(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Returns a blockade result when an orbital mutation changed the planet's blockade state.
        /// </summary>
        /// <param name="planet">The affected planet.</param>
        /// <param name="wasBlockaded">Whether the planet was blockaded before the mutation.</param>
        /// <returns>The completed blockade transition, or null when the state did not change.</returns>
        internal BlockadeChangedResult Resolve(Planet planet, bool wasBlockaded)
        {
            if (planet == null)
                return null;

            bool isBlockaded = planet.IsBlockaded();
            if (wasBlockaded == isBlockaded)
                return null;

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
                return null;

            return new BlockadeChangedResult
            {
                Planet = planet,
                BlockadingFleet = blockadingFleet,
                Blockaded = isBlockaded,
                Tick = _game.CurrentTick,
            };
        }
    }
}
