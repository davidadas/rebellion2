using System;
using Rebellion.Game;

namespace Rebellion.Generation
{
    /// <summary>
    /// Applies post-seeding support adjustments: HQ planets are pinned to full
    /// owner loyalty, and a military presence on an owned planet boosts the owner's
    /// popular support.
    /// </summary>
    public sealed class BalanceSeeder : IGameSeeder
    {
        /// <summary>
        /// Applies post-seeding support adjustments against the generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            Apply(ctx.Systems, ctx.Config.Balance, ctx.GameConfig.Planet.MaxPopularSupport);
        }

        /// <summary>
        /// Applies the post-seeding support adjustments to every planet in every system.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="balance">Balance-pass tuning values from generation config.</param>
        /// <param name="maxSupport">The maximum popular-support value the game allows.</param>
        private void Apply(PlanetSystem[] systems, BalanceSection balance, int maxSupport)
        {
            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (planet.IsHeadquarters && !string.IsNullOrEmpty(planet.OwnerInstanceID))
                    {
                        planet.SetPopularSupport(planet.OwnerInstanceID, maxSupport, maxSupport);
                    }

                    if (!string.IsNullOrEmpty(planet.OwnerInstanceID))
                    {
                        int militaryPresence =
                            planet.GetRegimentCount()
                            + planet.GetFleets().Count
                            + planet.GetStarfighterCount();

                        if (militaryPresence > 0)
                        {
                            int currentSupport = planet.GetPopularSupport(planet.OwnerInstanceID);
                            int boost = Math.Min(
                                militaryPresence * balance.SupportBoostPerUnit,
                                balance.MaxMilitaryPresenceBoost
                            );
                            int newSupport = Math.Min(maxSupport, currentSupport + boost);
                            planet.SetPopularSupport(
                                planet.OwnerInstanceID,
                                newSupport,
                                maxSupport
                            );
                        }
                    }
                }
            }
        }
    }
}
