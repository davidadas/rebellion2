using System;
using Rebellion.Game.World;

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
            Apply(ctx.Systems, ctx.Config.Balance);
        }

        /// <summary>
        /// Applies the post-seeding support adjustments to every planet in every system.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="balance">Balance-pass tuning values from generation config.</param>
        private void Apply(PlanetSystem[] systems, BalanceSection balance)
        {
            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                    ApplyPlanetSupportAdjustments(planet, balance);
            }
        }

        /// <summary>
        /// Applies all post-seeding support adjustments to one planet.
        /// </summary>
        /// <param name="planet">The planet to adjust.</param>
        /// <param name="balance">Balance-pass tuning values.</param>
        private void ApplyPlanetSupportAdjustments(Planet planet, BalanceSection balance)
        {
            PinHeadquartersSupport(planet);
            ApplyMilitaryPresenceSupportBoost(planet, balance);
        }

        /// <summary>
        /// Pins headquarters support to the maximum for its owner.
        /// </summary>
        /// <param name="planet">The planet to adjust.</param>
        private void PinHeadquartersSupport(Planet planet)
        {
            if (planet.IsHeadquarters && !string.IsNullOrEmpty(planet.OwnerInstanceID))
                planet.SetFullPopularSupport(planet.OwnerInstanceID);
        }

        /// <summary>
        /// Applies the military-presence popular-support boost to an owned planet.
        /// </summary>
        /// <param name="planet">The planet to adjust.</param>
        /// <param name="balance">Balance-pass tuning values.</param>
        private void ApplyMilitaryPresenceSupportBoost(Planet planet, BalanceSection balance)
        {
            if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                return;

            int militaryPresence = GetMilitaryPresence(planet);
            if (militaryPresence <= 0)
                return;

            int currentSupport = planet.GetPopularSupport(planet.OwnerInstanceID);
            int boost = Math.Min(
                militaryPresence * balance.SupportBoostPerUnit,
                balance.MaxMilitaryPresenceBoost
            );
            planet.SetPopularSupport(planet.OwnerInstanceID, currentSupport + boost);
        }

        /// <summary>
        /// Gets the seeded military presence count on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The number of present military units or fleets.</returns>
        private static int GetMilitaryPresence(Planet planet)
        {
            return planet.GetRegimentCount()
                + planet.GetFleets().Count
                + planet.GetStarfighterCount();
        }
    }
}
