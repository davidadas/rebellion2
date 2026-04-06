using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

/// <summary>
/// Manages research and technology advancement during each game tick.
/// </summary>
namespace Rebellion.Systems
{
    public class ResearchSystem
    {
        public ResearchSystem(GameRoot game) { }

        /// <summary>
        /// Processes research for the current tick across all factions.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <returns>Any results produced during this tick (e.g. level advancements).</returns>
        public List<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            GameConfig config = game.GetConfig();
            GameConfig.ResearchConfig researchConfig = config.Research;
            List<Faction> factions = game.GetFactions();

            foreach (Faction faction in factions)
            {
                AccumulateIdleFacilityCapacity(faction);
                CheckLevelAdvancement(faction, researchConfig, game, results);
            }

            return results;
        }

        /// <summary>
        /// Each idle facility contributes +1 research capacity per tick.
        /// </summary>
        /// <param name="faction">The faction to accumulate capacity for.</param>
        private void AccumulateIdleFacilityCapacity(Faction faction)
        {
            List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();

            foreach (Planet planet in planets)
            {
                foreach (ManufacturingType type in ResearchableTypes)
                {
                    int idleCount = planet.GetIdleManufacturingFacilities(type);
                    if (idleCount > 0)
                    {
                        faction.ResearchCapacity[type] += idleCount;
                    }
                }
            }
        }

        /// <summary>
        /// Advances research level when accumulated capacity meets the threshold.
        /// </summary>
        /// <param name="faction">The faction to check advancement for.</param>
        /// <param name="config">Research configuration (level costs).</param>
        /// <param name="game">The game instance.</param>
        /// <param name="results">List to append advancement results to.</param>
        private void CheckLevelAdvancement(
            Faction faction,
            GameConfig.ResearchConfig config,
            GameRoot game,
            List<GameResult> results
        )
        {
            foreach (ManufacturingType type in ResearchableTypes)
            {
                int currentLevel = faction.GetResearchLevel(type);
                int nextLevel = currentLevel + 1;

                if (!config.LevelCosts.TryGetValue(nextLevel, out int cost))
                    continue;

                if (faction.ResearchCapacity[type] >= cost)
                {
                    faction.ResearchCapacity[type] -= cost;
                    faction.SetResearchLevel(type, nextLevel);

                    results.Add(new ResearchLevelAdvancedResult
                    {
                        Tick = game.CurrentTick,
                        FactionInstanceID = faction.InstanceID,
                        ResearchType = type,
                        NewLevel = nextLevel,
                    });

                    GameLogger.Log(
                        $"{faction.DisplayName} advanced {type} research to level {nextLevel}"
                    );
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
