using Rebellion.Game.Galaxy;

namespace Rebellion.Generation
{
    /// <summary>
    /// Sets headquarters popular support to full loyalty for their owners.
    /// </summary>
    public sealed class BalanceSeeder : IGameSeeder
    {
        /// <summary>
        /// Sets owned headquarters to full popular support after generation.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            foreach (PlanetSector sector in ctx.Sectors)
            {
                foreach (Planet planet in sector.GetChildren<Planet>())
                {
                    if (planet.IsHeadquarters && !string.IsNullOrEmpty(planet.OwnerInstanceID))
                        planet.SetFullPopularSupport(planet.OwnerInstanceID);
                }
            }
        }
    }
}
