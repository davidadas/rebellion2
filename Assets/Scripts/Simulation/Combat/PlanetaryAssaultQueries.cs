using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Evaluates whether fleets can assault a planet without resolving combat.</summary>
    public sealed class PlanetaryAssaultQueries
    {
        private readonly GameRoot _game;

        /// <summary>Creates assault eligibility queries for the active game.</summary>
        /// <param name="game">The game supplying the current assault configuration.</param>
        public PlanetaryAssaultQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Determines whether the supplied fleets can execute a planetary assault.
        /// </summary>
        /// <param name="fleets">The fleets attempting the assault.</param>
        /// <param name="planet">The planet being assaulted.</param>
        /// <returns>True when the fleets contain ready troops and shields do not block them.</returns>
        public bool CanExecute(IReadOnlyList<Fleet> fleets, Planet planet)
        {
            return CanAssault(fleets, planet)
                && !PlanetaryAssaultResolver.IsBlockedByShields(
                    planet,
                    _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                )
                && PlanetaryAssaultResolver.HasReadyAttackers(fleets);
        }

        /// <summary>
        /// Determines whether the supplied fleets can begin an assault at the planet.
        /// </summary>
        /// <param name="fleets">The fleets attempting the assault.</param>
        /// <param name="planet">The planet being assaulted.</param>
        /// <returns>True when every fleet is stationary, colocated, and owned by one faction.</returns>
        internal static bool CanAssault(IReadOnlyList<Fleet> fleets, Planet planet)
        {
            if (
                planet?.IsDestroyed != false
                || fleets?.Any() != true
                || fleets.Any(fleet => fleet == null)
            )
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && planet.GetOwnerInstanceID() != ownerId
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && !fleet.IsInCombat
                    && fleet.GetParent() == planet
                );
        }
    }
}
