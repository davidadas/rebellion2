using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Calculates garrison requirements without changing the supplied planet or faction.
    /// </summary>
    public static class UprisingQueries
    {
        /// <summary>
        /// Calculates how many garrison troops a planet requires for the given faction.
        /// Returns 0 when popular support is at or above the threshold.
        /// Operational sector suppression ships supply the garrison divisor.
        /// Planets in active uprisings apply the uprising multiplier.
        /// </summary>
        /// <param name="planet">The planet to calculate garrison requirements for.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="config">Garrison configuration parameters.</param>
        /// <param name="popularSupport">The assumed support percentage, or null to use current support.</param>
        /// <returns>The number of garrison troops required, or 0 if support is sufficient.</returns>
        public static int CalculateGarrisonRequirement(
            Planet planet,
            Faction faction,
            GameConfig.GarrisonConfig config,
            int? popularSupport = null
        )
        {
            return planet.GetGarrisonRequirement(faction, config, popularSupport);
        }
    }
}
