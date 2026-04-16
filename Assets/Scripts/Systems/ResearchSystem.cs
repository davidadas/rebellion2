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
        /// <returns>Any technology unlock results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Faction faction in _game.GetFactions())
            {
                AccumulateIdleFacilityCapacity(faction);
                CheckUnitUnlock(faction, results);
            }

            return results;
        }

        /// <summary>
        /// Each idle facility contributes +1 research capacity per tick.
        /// </summary>
        /// <param name="faction">The faction to accumulate research capacity for.</param>
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
        /// Unlocks the next technology in the queue when accumulated capacity
        /// meets the target's ResearchDifficulty. Loops to handle carry-over.
        /// </summary>
        /// <param name="faction">The faction to check unlocks for.</param>
        /// <param name="results">Collection to append any unlock results to.</param>
        private void CheckUnitUnlock(Faction faction, List<GameResult> results)
        {
            foreach (ManufacturingType type in ResearchableTypes)
            {
                while (true)
                {
                    Technology target = faction.GetCurrentResearchTarget(type);
                    if (target == null)
                        break;

                    int difficulty = target.GetResearchDifficulty();
                    if (faction.ResearchCapacity[type] < difficulty)
                        break;

                    faction.ResearchCapacity[type] -= difficulty;
                    faction.SetHighestUnlockedOrder(type, target.GetResearchOrder());

                    string techName = target.GetReference().GetDisplayName();
                    results.Add(
                        new TechnologyUnlockedResult
                        {
                            Tick = _game.CurrentTick,
                            Faction = faction,
                            ResearchType = type,
                            TechnologyName = techName,
                            ResearchOrder = target.GetResearchOrder(),
                        }
                    );

                    GameLogger.Log(
                        $"{faction.DisplayName} unlocked {type} technology: {techName} (order {target.GetResearchOrder()})"
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
