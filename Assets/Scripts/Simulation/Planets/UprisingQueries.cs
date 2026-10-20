using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Calculates garrison requirements without changing the supplied planet or faction.
    /// </summary>
    public static class UprisingQueries
    {
        /// <summary>
        /// Calculates how many garrison troops a planet requires for the given faction.
        /// Returns 0 when popular support is at or above the threshold.
        /// Sector planet-destroyer presence applies the faction's garrison efficiency divisor.
        /// Planets in active uprisings apply the uprising multiplier.
        /// </summary>
        /// <param name="planet">The planet to calculate garrison requirements for.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="config">Garrison configuration parameters.</param>
        /// <returns>The number of garrison troops required, or 0 if support is sufficient.</returns>
        public static int CalculateGarrisonRequirement(
            Planet planet,
            Faction faction,
            GameConfig.GarrisonConfig config
        )
        {
            return planet.GetGarrisonRequirement(faction, config);
        }
    }
}
