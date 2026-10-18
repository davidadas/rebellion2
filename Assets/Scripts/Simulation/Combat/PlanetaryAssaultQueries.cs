using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates whether fleets can assault a planet without resolving combat.
    /// </summary>
    public sealed class PlanetaryAssaultQueries
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates assault eligibility queries for the active game.
        /// </summary>
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
            return TryResolveCommand(
                    fleets,
                    planet,
                    out List<Fleet> liveFleets,
                    out Planet livePlanet
                )
                && !PlanetaryAssaultResolver.IsBlockedByShields(
                    livePlanet,
                    _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                )
                && PlanetaryAssaultResolver.HasReadyAttackers(liveFleets);
        }

        /// <summary>
        /// Resolves visible fleet and planet snapshots to the authoritative game graph.
        /// </summary>
        /// <param name="fleets">The requested attacking fleets.</param>
        /// <param name="planet">The requested target planet.</param>
        /// <param name="liveFleets">Receives the registered attacking fleets.</param>
        /// <param name="livePlanet">Receives the registered target planet.</param>
        /// <returns>True when every input resolves and the resulting command can begin.</returns>
        internal bool TryResolveCommand(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            out List<Fleet> liveFleets,
            out Planet livePlanet
        )
        {
            liveFleets = new List<Fleet>();
            livePlanet = string.IsNullOrEmpty(planet?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);
            if (livePlanet == null || fleets == null || fleets.Count < 1)
                return false;

            HashSet<string> fleetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Fleet fleet in fleets)
            {
                if (string.IsNullOrEmpty(fleet?.InstanceID) || !fleetIds.Add(fleet.InstanceID))
                    return false;

                Fleet liveFleet = _game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID);
                if (liveFleet == null)
                    return false;

                liveFleets.Add(liveFleet);
            }

            return CanAssault(liveFleets, livePlanet);
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
