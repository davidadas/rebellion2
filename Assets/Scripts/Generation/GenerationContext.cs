using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Util.Common;

namespace Rebellion.Generation
{
    /// <summary>
    /// Carries every value a game-generation run needs: inputs from the caller,
    /// configuration loaded from <see cref="ResourceManager"/>, the game-data arrays
    /// being seeded into a playable world, and outputs that downstream seeders consume.
    /// One instance is constructed by <see cref="GameBuilder"/> and threaded through
    /// each seeder in order.
    /// </summary>
    public sealed class GenerationContext
    {
        public GameSummary Summary { get; set; }
        public GameGenerationConfig Config { get; set; }
        public GameConfig GameConfig { get; set; }
        public IRandomNumberProvider Rng { get; set; }

        public PlanetSystem[] Systems { get; set; }
        public Faction[] Factions { get; set; }
        public Building[] Buildings { get; set; }
        public CapitalShip[] CapitalShips { get; set; }
        public Starfighter[] Starfighters { get; set; }
        public Regiment[] Regiments { get; set; }
        public SpecialForces[] SpecialForces { get; set; }
        public Officer[] Officers { get; set; }
        public GameEvent[] Events { get; set; }

        public GalaxyClassificationResult Classification { get; set; }
        public List<Building> DeployedBuildings { get; set; }
        public Officer[] DeployedOfficers { get; set; }
        public Officer[] UnrecruitedOfficers { get; set; }
        public GameRoot Game { get; set; }
    }
}
