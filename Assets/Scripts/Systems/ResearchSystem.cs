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
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a new ResearchSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">The random number provider.</param>
        public ResearchSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;

            // Arm the initial timer for any faction that hasn't already had one
            // persisted from a save. NextRefreshTick == 0 is the default-uninitialized
            // sentinel; loaded saves carry a non-zero value that we keep as-is.
            GameConfig.ResearchConfig config = _game.Config.Research;
            foreach (Faction faction in _game.GetFactions())
            {
                FactionResearchState state = faction.ResearchState;
                if (state.NextRefreshTick == 0)
                    state.NextRefreshTick = _game.CurrentTick + RollRefreshDelay(config);
            }
        }

        /// <summary>
        /// Processes research for the current tick across all factions.
        /// </summary>
        /// <returns>Any primary research results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            if (_game.CurrentTick <= 0)
                return results;

            GameConfig.ResearchConfig config = _game.Config.Research;

            foreach (Faction faction in _game.GetFactions())
            {
                FactionResearchState state = faction.ResearchState;
                if (_game.CurrentTick < state.NextRefreshTick)
                    continue;

                RefreshResearchCapacity(faction, results);

                state.NextRefreshTick = _game.CurrentTick + RollRefreshDelay(config);
            }

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

                Technology unlocked = faction.ApplyResearchProgress(discipline, capacityDelta);
                if (unlocked != null)
                {
                    results.Add(
                        new ResearchOrderedResult
                        {
                            Tick = _game.CurrentTick,
                            Faction = faction,
                            FacilityType = type,
                            ResearchOrder = faction.GetHighestUnlockedOrder(type),
                            Capacity = faction.GetResearchCapacityRemaining(discipline),
                            Technology = unlocked,
                        }
                    );
                    if (faction.IsResearchExhausted(discipline))
                    {
                        results.Add(
                            new ResearchExhaustedResult
                            {
                                Tick = _game.CurrentTick,
                                Faction = faction,
                                FacilityType = type,
                                PreviousState = 0,
                                NewState = 1,
                            }
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Rolls the next refresh delay using the configured base and random spread.
        /// </summary>
        /// <param name="config">The research configuration.</param>
        /// <returns>Number of ticks until the next refresh should fire.</returns>
        private int RollRefreshDelay(GameConfig.ResearchConfig config)
        {
            return config.RefreshIntervalBase
                + _provider.NextInt(0, config.RefreshIntervalSpread + 1);
        }

        private static readonly ManufacturingType[] ResearchableTypes = new[]
        {
            ManufacturingType.Ship,
            ManufacturingType.Building,
            ManufacturingType.Troop,
        };
    }
}
