using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Calculates planetary control and support resistance without changing game state.</summary>
    public sealed class PlanetaryControlQueries
    {
        private readonly GameRoot _game;

        /// <summary>Creates control queries for the active game.</summary>
        /// <param name="game">The game whose factions and control configuration are consulted.</param>
        public PlanetaryControlQueries(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Applies the original weak-support reduction to a shift on a core sector.
        /// </summary>
        /// <param name="planet">The planet receiving the support shift.</param>
        /// <param name="faction">The faction whose support is changing.</param>
        /// <param name="shift">The unadjusted signed support shift.</param>
        /// <param name="divisor">The configured weak-support divisor.</param>
        /// <returns>The support shift after any core-sector reduction.</returns>
        internal static int ApplyCoreSupportResistance(
            Planet planet,
            Faction faction,
            int shift,
            int divisor
        )
        {
            if (
                shift == 0
                || divisor <= 0
                || planet?.GetParentOfType<PlanetSector>()?.SectorType != PlanetSectorType.Core
            )
                return shift;

            bool penaltyApplies = faction?.Settings?.SupportResistance switch
            {
                SupportChange.Increase => shift > 0,
                SupportChange.Decrease => shift < 0,
                _ => false,
            };
            return penaltyApplies ? shift / divisor : shift;
        }

        /// <summary>
        /// Finds the faction whose support qualifies it to control a planet.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The qualifying faction with the greatest support, or null when none qualifies.</returns>
        private Faction GetSupportController(Planet planet)
        {
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;
            return _game
                .GetFactions()
                .Where(faction => planet.GetPopularSupport(faction.InstanceID) >= threshold)
                .OrderByDescending(faction => planet.GetPopularSupport(faction.InstanceID))
                .FirstOrDefault();
        }

        /// <summary>
        /// Resolves control from active regiments before falling back to popular support.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The controlling faction, or null when control is contested or unsupported.</returns>
        public Faction GetPlanetController(Planet planet)
        {
            return GetPlanetController(planet, GetActiveRegimentOwners(planet));
        }

        /// <summary>
        /// Gets the distinct owners of completed, stationary regiments on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The active regiment owner identifiers.</returns>
        internal List<string> GetActiveRegimentOwners(Planet planet)
        {
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                .Select(regiment => regiment.GetOwnerInstanceID())
                .Where(ownerId => !string.IsNullOrEmpty(ownerId))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Resolves planetary control from active regiment owners and popular support.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="regimentOwners">The distinct active regiment owner identifiers.</param>
        /// <returns>The controlling faction, or null when control is contested or unsupported.</returns>
        internal Faction GetPlanetController(Planet planet, List<string> regimentOwners)
        {
            if (regimentOwners.Count == 1)
                return _game.GetFactionByOwnerInstanceID(regimentOwners[0]);

            if (regimentOwners.Count > 1)
                return null;

            return GetSupportController(planet);
        }
    }
}
