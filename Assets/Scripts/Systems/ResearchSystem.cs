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
        private readonly GameRoot game;

        public ResearchSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>Processes research for the current tick across all factions.</summary>
        public List<GameResult> ProcessTick(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            GameConfig config = game.GetConfig();
            GameConfig.ResearchConfig researchConfig = config.Research;
            List<Faction> factions = game.GetFactions();

            foreach (Faction faction in factions)
            {
                AccumulateIdleFacilityCapacity(faction);
                AccumulateOfficerResearch(faction, researchConfig, provider);
                CheckLevelAdvancement(faction, researchConfig, game, results);
            }

            return results;
        }

        /// <summary>Each idle facility contributes +1 research capacity per tick.</summary>
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

        /// <summary>Idle officers at planets with matching facilities roll against their research skill.</summary>
        private void AccumulateOfficerResearch(
            Faction faction,
            GameConfig.ResearchConfig config,
            IRandomNumberProvider provider
        )
        {
            List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();

            foreach (Planet planet in planets)
            {
                foreach (Officer officer in planet.GetAllOfficers())
                {
                    // Officer must belong to this faction, be idle, and not in transit
                    if (officer.OwnerInstanceID != faction.InstanceID)
                        continue;
                    if (officer.IsOnMission() || officer.Movement != null)
                        continue;
                    if (officer.IsCaptured || officer.IsKilled)
                        continue;

                    foreach (ManufacturingType type in ResearchableTypes)
                    {
                        int skill = officer.GetResearchSkill(type);
                        if (skill <= 0)
                            continue;

                        // Must have at least one idle facility of this type on the planet
                        if (planet.GetIdleManufacturingFacilities(type) <= 0)
                            continue;

                        // Roll success: random 1-100 <= officer's research skill
                        int roll = provider.NextInt(1, 101);
                        if (roll <= skill)
                        {
                            int points =
                                config.BaseResearchPoints
                                + provider.NextInt(0, config.ResearchDiceRange + 1);
                            faction.ResearchCapacity[type] += points;
                            officer.IncrementResearchSkill(type);
                        }
                    }
                }
            }
        }

        /// <summary>Advances research level when accumulated capacity meets the threshold.</summary>
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
                    // TODO: Rebuild technology levels after research advancement

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
