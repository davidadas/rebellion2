using System.Linq;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages research and technology advancement during each game tick.
    /// </summary>
    public class ResearchSystem : IGameSystem
    {
        private const int CapacityRefreshPulseTicks = 10;
        private readonly GameRoot _game;

        /// <summary>
        /// Creates a new ResearchSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public ResearchSystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Processes research for the current tick across all factions.
        /// </summary>
        /// <returns>Any primary research results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            if (_game.CurrentTick % CapacityRefreshPulseTicks != 0)
                return results;

            foreach (Faction faction in _game.GetFactions())
                RefreshResearchCapacity(faction, results);

            return results;
        }

        /// <summary>
        /// Refreshes discipline research capacity from completed facilities on owned core systems
        /// and immediately applies any resulting single-step order advances.
        /// </summary>
        /// <param name="faction">The faction to accumulate research capacity for.</param>
        /// <param name="results">Collection to append any research results to.</param>
        private void RefreshResearchCapacity(Faction faction, List<GameResult> results)
        {
            List<Planet> corePlanets = faction
                .GetOwnedUnitsByType<Planet>()
                .Where(planet =>
                    planet.GetParent() is PlanetSystem system
                    && system.GetSystemType() == PlanetSystemType.CoreSystem
                )
                .ToList();

            foreach (ManufacturingType type in ResearchableTypes)
            {
                int capacityDelta = 0;
                ResearchDiscipline discipline = Faction.ToResearchDiscipline(type);

                foreach (Planet planet in corePlanets)
                {
                    capacityDelta += planet
                        .GetBuildings(type)
                        .Count(building =>
                            building.GetManufacturingStatus() == ManufacturingStatus.Complete
                            && building.Movement == null
                        );
                }

                List<GameResult> disciplineResults = faction.ApplyResearchCapacityChange(
                    discipline,
                    capacityDelta
                );
                foreach (GameResult result in disciplineResults)
                {
                    result.Tick = _game.CurrentTick;
                    results.Add(result);
                }
            }
        }

        private static readonly ManufacturingType[] ResearchableTypes = new[]
        {
            ManufacturingType.Ship,
            ManufacturingType.Building,
            ManufacturingType.Troop,
        };
    }
}
