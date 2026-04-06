using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Util.Common;

/// <summary>
/// Manages research and technology advancement during each game tick.
///
/// Three-phase per-tick processing (matches original disassembly):
///   Phase 1 - Passive capacity: each idle facility contributes +1 capacity per tick.
///   Phase 2 - Officer research: idle officers at planets with matching facilities
///             roll against their research skill. On success, they generate
///             BaseResearchPoints + random(0, DiceRange) points and improve by 1.
///   Phase 3 - Level advancement: when accumulated capacity >= level cost threshold,
///             research level advances, cost is subtracted, and tech tree rebuilds.
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

        /// <summary>
        /// Processes research for the current tick across all factions.
        /// </summary>
        public void ProcessTick(GameRoot game, IRandomNumberProvider provider)
        {
            GameConfig config = game.GetConfig();
            GameConfig.ResearchConfig researchConfig = config.Research;
            List<Faction> factions = game.GetFactions();

            foreach (Faction faction in factions)
            {
                // Phase 1: Passive capacity from idle facilities
                AccumulateIdleFacilityCapacity(faction);

                // Phase 2: Officer research contributions
                AccumulateOfficerResearch(faction, researchConfig, provider);

                // Phase 3: Level advancement
                CheckLevelAdvancement(faction, researchConfig, game);
            }
        }

        /// <summary>
        /// Phase 1: Each idle manufacturing facility on a faction's planets
        /// contributes +1 research capacity per tick to the matching type.
        /// A facility is "idle" when it has no items in its manufacturing queue.
        /// </summary>
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
        /// Phase 2: Officers that are idle (not on a mission, not in transit)
        /// and located on a planet with at least one matching idle facility
        /// roll against their research skill. On success:
        ///   - Award BaseResearchPoints + random(0, DiceRange) to faction capacity
        ///   - Increment the officer's research skill by 1
        /// </summary>
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

        /// <summary>
        /// Phase 3: Check if accumulated capacity meets or exceeds the cost
        /// for the next research level. If so, advance the level, subtract
        /// the cost, and rebuild the faction's technology tree.
        /// </summary>
        private void CheckLevelAdvancement(
            Faction faction,
            GameConfig.ResearchConfig config,
            GameRoot game
        )
        {
            foreach (ManufacturingType type in ResearchableTypes)
            {
                int currentLevel = faction.GetResearchLevel(type);
                int nextLevel = currentLevel + 1;

                if (!config.LevelCosts.TryGetValue(nextLevel, out int cost))
                {
                    // No more levels to advance - research exhausted for this type
                    continue;
                }

                if (faction.ResearchCapacity[type] >= cost)
                {
                    faction.ResearchCapacity[type] -= cost;
                    faction.SetResearchLevel(type, nextLevel);
                    // TODO: Rebuild technology levels after research advancement
                    // faction.LoadTechnologyLevels(templates) — needs ResourceManager access

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
