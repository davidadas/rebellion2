using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages planetary uprisings based on garrison strength vs. popular support.
    /// Uses dual dice rolls and table lookups matching the original uprising resolution.
    /// </summary>
    public class UprisingSystem
    {
        private readonly GameRoot _game;

        public UprisingSystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Processes uprising checks for all owned, populated planets.
        /// </summary>
        /// <param name="provider">Random number provider for dice rolls.</param>
        public List<GameResult> ProcessTick(IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            List<Planet> planets = _game.GetSceneNodesByType<Planet>();

            foreach (Planet planet in planets)
            {
                if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                    continue;

                if (planet.IsInUprising)
                    continue;

                Faction faction = _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
                if (faction == null)
                    continue;

                int ownerSupport = planet.GetPopularSupport(faction.InstanceID);
                int troopCount = CountFriendlyTroops(planet, faction.InstanceID);

                // Check if garrison is insufficient
                int garrisonRequired = CalculateGarrisonRequirement(
                    planet,
                    faction,
                    _game.Config.AI.Garrison
                );

                int garrisonSurplus = troopCount - garrisonRequired;
                if (garrisonSurplus >= 0)
                    continue;

                // Garrison insufficient — resolve uprising via dice rolls and tables
                int upris1Result;
                int upris2Result;
                ResolveUprisingTableResults(
                    planet,
                    faction,
                    ownerSupport,
                    troopCount,
                    provider,
                    out upris1Result,
                    out upris2Result
                );

                // UPRIS1 result > 0 means uprising triggers
                if (upris1Result > 0)
                {
                    // Find strongest opposing faction
                    string opposingFactionId = null;
                    int maxOpposingSupport = 0;
                    foreach (KeyValuePair<string, int> kvp in planet.PopularSupport)
                    {
                        if (kvp.Key != planet.OwnerInstanceID && kvp.Value > maxOpposingSupport)
                        {
                            maxOpposingSupport = kvp.Value;
                            opposingFactionId = kvp.Key;
                        }
                    }

                    if (opposingFactionId != null)
                    {
                        Faction previousOwner = faction;
                        Faction newOwner = _game.GetFactionByOwnerInstanceID(opposingFactionId);
                        _game.ChangeUnitOwnership(planet, opposingFactionId);
                        planet.BeginUprising();

                        results.Add(
                            new GameObjectControlChangedResult
                            {
                                GameObject = planet,
                                PreviousOwner = previousOwner,
                                NewOwner = newOwner,
                                Tick = _game.CurrentTick,
                            }
                        );
                        results.Add(
                            new PlanetUprisingStartedResult
                            {
                                Planet = planet,
                                InstigatorFaction = newOwner,
                                Tick = _game.CurrentTick,
                            }
                        );
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Resolves uprising using dual dice rolls and UPRIS1/UPRIS2 table lookups.
        /// Combined score = dice + (garrison_threshold - troop_multiplier * troops).
        /// </summary>
        private void ResolveUprisingTableResults(
            Planet planet,
            Faction faction,
            int supportForController,
            int controllerTroopCount,
            IRandomNumberProvider provider,
            out int upris1Result,
            out int upris2Result
        )
        {
            upris1Result = 0;
            upris2Result = 0;

            GameConfig.UprisingConfig config = _game.Config.Uprising;

            // Two independent dice rolls: each is random(0..range-1) + addend
            int rollA = provider.NextInt(0, config.DiceRange) + config.DiceAddend;
            int rollB = provider.NextInt(0, config.DiceRange) + config.DiceAddend;

            // Troop effectiveness multiplier — only on core systems (matches original)
            int troopMultiplier = 1;
            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.UprisingResistance > 1
            )
            {
                troopMultiplier = faction.Modifiers.UprisingResistance;
            }

            // Garrison threshold (how many troops we need)
            int threshold = CalculateUprisingThreshold(supportForController);

            // Combined uprising score
            int combinedScore =
                rollA + rollB + (threshold - troopMultiplier * controllerTroopCount);

            // Look up UPRIS1 table
            upris1Result = LookupTable(config.Upris1Table, combinedScore);

            // Only look up UPRIS2 if UPRIS1 produced a result
            if (upris1Result > 0)
            {
                upris2Result = LookupTable(config.Upris2Table, combinedScore);
            }
        }

        /// <summary>
        /// Calculates how many garrison troops a planet requires for the given faction.
        /// Returns 0 when popular support is at or above the threshold.
        /// Core worlds with a faction GarrisonEfficiency modifier receive a reduced requirement.
        /// Planets in active uprisings apply the uprising multiplier.
        /// </summary>
        public static int CalculateGarrisonRequirement(
            Planet planet,
            Faction faction,
            GameConfig.GarrisonConfig config
        )
        {
            int popularSupport = planet.GetPopularSupport(faction.InstanceID);

            if (popularSupport >= config.SupportThreshold)
                return 0;

            int garrison = (int)
                Math.Ceiling(
                    (config.SupportThreshold - popularSupport) / (double)config.GarrisonDivisor
                );

            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.GarrisonEfficiency > 1
            )
            {
                garrison /= faction.Modifiers.GarrisonEfficiency;
            }

            if (planet.IsInUprising)
                garrison *= config.UprisingMultiplier;

            return garrison;
        }

        /// <summary>
        /// Calculates the uprising threshold: ceil((60 - support) / 10) when support &lt; 60.
        /// </summary>
        private int CalculateUprisingThreshold(int supportForController)
        {
            GameConfig.GarrisonConfig config = _game.Config.AI.Garrison;

            if (supportForController >= config.SupportThreshold)
                return 0;

            return (int)
                Math.Ceiling(
                    (config.SupportThreshold - supportForController)
                        / (double)config.GarrisonDivisor
                );
        }

        /// <summary>
        /// Looks up a value from an uprising table. Finds the highest threshold
        /// that the score meets or exceeds, and returns the associated value.
        /// </summary>
        private static int LookupTable(Dictionary<int, int> table, int score)
        {
            int result = 0;
            foreach (KeyValuePair<int, int> entry in table.OrderBy(e => e.Key))
            {
                if (score >= entry.Key)
                    result = entry.Value;
                else
                    break;
            }
            return result;
        }

        /// <summary>
        /// Counts friendly regiment troops at a planet.
        /// </summary>
        private static int CountFriendlyTroops(Planet planet, string factionId)
        {
            return planet.GetAllRegiments().Count(r => r.GetOwnerInstanceID() == factionId);
        }
    }
}
