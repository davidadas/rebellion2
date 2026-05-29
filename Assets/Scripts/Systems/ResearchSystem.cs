using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
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

            InitializeResearchTimers();
        }

        /// <summary>
        /// Initializes research timers for factions that do not already have one.
        /// </summary>
        private void InitializeResearchTimers()
        {
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

            foreach (ResearchDiscipline discipline in _researchDisciplines)
            {
                int capacityDelta = CountCompleteFacilities(corePlanets, discipline);

                Technology unlocked = faction.ApplyResearchProgress(discipline, capacityDelta);
                if (unlocked == null)
                    continue;

                results.Add(
                    new ResearchOrderedResult
                    {
                        Tick = _game.CurrentTick,
                        Faction = faction,
                        Discipline = discipline,
                        ResearchOrder = faction.GetHighestUnlockedOrder(discipline),
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
                            Discipline = discipline,
                            PreviousState = 0,
                            NewState = 1,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Counts complete, stationary facilities across the given planets that contribute to
        /// the specified research discipline.
        /// </summary>
        /// <param name="planets">The planets to inspect.</param>
        /// <param name="discipline">The research discipline whose facilities to count.</param>
        /// <returns>The number of contributing facilities.</returns>
        private static int CountCompleteFacilities(
            List<Planet> planets,
            ResearchDiscipline discipline
        )
        {
            ManufacturingType facilityType = discipline.ToManufacturingType();
            int total = 0;
            foreach (Planet planet in planets)
            {
                total += planet
                    .GetBuildings(facilityType)
                    .Count(building =>
                        building.GetManufacturingStatus() == ManufacturingStatus.Complete
                        && building.Movement == null
                    );
            }
            return total;
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

        private static readonly ResearchDiscipline[] _researchDisciplines = new[]
        {
            ResearchDiscipline.ShipDesign,
            ResearchDiscipline.FacilityDesign,
            ResearchDiscipline.TroopTraining,
        };
    }
}
