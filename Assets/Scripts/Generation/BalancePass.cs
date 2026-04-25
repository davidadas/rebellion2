using System;
using System.Linq;
using Rebellion.Game;

namespace Rebellion.Generation
{
    /// <summary>
    /// Post-placement support adjustments.
    /// HQ planets get support = 101 (clamped to 100, but signals max loyalty).
    /// Military presence shifts support toward the owning faction.
    /// </summary>
    public class BalancePass
    {
        /// <summary>
        /// Applies post-placement support adjustments across every planet in the given systems:
        /// HQ planets are bumped to full loyalty for their owning faction, and military presence
        /// shifts support toward the unit owner.
        /// </summary>
        /// <param name="systems">All generated planet systems in the galaxy.</param>
        /// <param name="factions">All factions participating in the game.</param>
        public void Apply(PlanetSystem[] systems, Faction[] factions)
        {
            string[] factionIds = factions.Select(f => f.InstanceID).ToArray();

            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                {
                    // HQ planets get max support
                    if (planet.IsHeadquarters && !string.IsNullOrEmpty(planet.OwnerInstanceID))
                    {
                        planet.SetPopularSupport(planet.OwnerInstanceID, 100, 100);
                    }

                    // Military presence shifts support
                    if (!string.IsNullOrEmpty(planet.OwnerInstanceID))
                    {
                        int militaryPresence =
                            planet.GetRegimentCount()
                            + planet.GetFleets().Count
                            + planet.GetStarfighterCount();

                        if (militaryPresence > 0)
                        {
                            int currentSupport = planet.GetPopularSupport(planet.OwnerInstanceID);
                            int boost = Math.Min(militaryPresence * 2, 10);
                            int newSupport = Math.Min(100, currentSupport + boost);
                            planet.SetPopularSupport(planet.OwnerInstanceID, newSupport, 100);
                        }
                    }
                }
            }
        }
    }
}
