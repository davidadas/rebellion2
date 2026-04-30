using System.Collections.Generic;
using System.Linq;
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

                ResearchProgressSnapshot before = faction.GetResearchProgressSnapshot(discipline);
                faction.ApplyResearchCapacityChange(discipline, capacityDelta);
                ResearchProgressSnapshot after = faction.GetResearchProgressSnapshot(discipline);
                AppendResearchResults(
                    results,
                    faction,
                    discipline,
                    before,
                    after,
                    _game.CurrentTick
                );
            }
        }

        /// <summary>
        /// Appends any research transition results detected between two discipline snapshots.
        /// </summary>
        /// <param name="results">The result collection to append to.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="discipline">The discipline being compared.</param>
        /// <param name="before">The snapshot before mutation.</param>
        /// <param name="after">The snapshot after mutation.</param>
        /// <param name="tick">The current game tick.</param>
        private static void AppendResearchResults(
            List<GameResult> results,
            Faction faction,
            ResearchDiscipline discipline,
            ResearchProgressSnapshot before,
            ResearchProgressSnapshot after,
            int tick
        )
        {
            ManufacturingType facilityType = Faction.ToManufacturingType(discipline);

            if (after.CurrentOrder != before.CurrentOrder)
            {
                results.Add(
                    new ResearchOrderedResult
                    {
                        Tick = tick,
                        Faction = faction,
                        FacilityType = facilityType,
                        ResearchOrder = after.CurrentOrder,
                        Capacity = after.CapacityRemaining,
                    }
                );
            }

            if (!before.IsExhausted && after.IsExhausted)
            {
                results.Add(
                    new ResearchExhaustedResult
                    {
                        Tick = tick,
                        Faction = faction,
                        FacilityType = facilityType,
                        PreviousState = 0,
                        NewState = 1,
                    }
                );
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
