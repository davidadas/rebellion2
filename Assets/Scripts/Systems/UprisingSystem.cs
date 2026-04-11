using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

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
        /// Starts new uprisings when garrison is insufficient (FUN_0050a970).
        /// Applies consequence resolution each tick for planets already in uprising
        /// (FUN_00510a30_uprising -> FUN_0050c1a0_apply_uprising_resolution_to_system).
        /// Uprisings are cleared externally by SubdueUprisingMission or OwnershipSystem.
        /// </summary>
        public List<GameResult> ProcessTick(IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            List<Planet> planets = _game.GetSceneNodesByType<Planet>();

            foreach (Planet planet in planets)
            {
                if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                    continue;

                if (!planet.IsPopulated())
                    continue;

                Faction faction = _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
                if (faction == null)
                    continue;

                if (!planet.IsInUprising)
                {
                    int troopCount = CountFriendlyTroops(planet, faction.InstanceID);
                    int garrisonRequired = CalculateGarrisonRequirement(
                        planet,
                        faction,
                        _game.Config.AI.Garrison
                    );
                    int garrisonSurplus = troopCount - garrisonRequired;

                    if (garrisonSurplus < 0)
                    {
                        planet.BeginUprising();
                        results.Add(
                            new PlanetUprisingStartedResult
                            {
                                Planet = planet,
                                InstigatorFaction = FindLeadingOpposingFaction(
                                    planet,
                                    faction.InstanceID
                                ),
                                Tick = _game.CurrentTick,
                            }
                        );
                    }
                }
                else
                {
                    int ownerSupport = planet.GetPopularSupport(faction.InstanceID);
                    int troopCount = CountFriendlyTroops(planet, faction.InstanceID);

                    ResolveUprisingTableResults(
                        planet,
                        faction,
                        ownerSupport,
                        troopCount,
                        provider,
                        out int upris1Result,
                        out int upris2Result
                    );

                    ApplyUprisingConsequence(
                        planet,
                        faction.InstanceID,
                        upris1Result,
                        provider,
                        results
                    );
                    ApplyUprisingConsequence(
                        planet,
                        faction.InstanceID,
                        upris2Result,
                        provider,
                        results
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Resolves uprising using dual dice rolls and UPRIS1/UPRIS2 table lookups.
        /// Combined score = dice + (garrison_threshold - troop_multiplier * troops).
        /// Corresponds to FUN_00558460_resolve_uprising_table_results.
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

            int rollA = provider.NextInt(0, config.DiceRange) + config.DiceAddend;
            int rollB = provider.NextInt(0, config.DiceRange) + config.DiceAddend;

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

            int threshold = CalculateUprisingThreshold(supportForController);
            int combinedScore =
                rollA + rollB + (threshold - troopMultiplier * controllerTroopCount);

            upris1Result = LookupTable(config.Upris1Table, combinedScore);

            if (upris1Result > 0)
                upris2Result = LookupTable(config.Upris2Table, combinedScore);
        }

        /// <summary>
        /// Dispatches an uprising consequence to its handler.
        /// Corresponds to FUN_0050c2c0.
        /// Cases 0-5 match the original UPRIS1/UPRIS2 table values.
        /// </summary>
        private void ApplyUprisingConsequence(
            Planet planet,
            string controllerInstanceId,
            int consequence,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            switch (consequence)
            {
                case 0:
                    return;

                case 1:
                {
                    List<Building> facilities = planet
                        .GetAllBuildings()
                        .Where(b => b.GetOwnerInstanceID() == controllerInstanceId)
                        .ToList();
                    if (facilities.Count == 0)
                        return;
                    int index = provider.NextInt(0, facilities.Count);
                    Building target = facilities[index];
                    _game.DetachNode(target);
                    results.Add(
                        new PlanetIncidentResult
                        {
                            Planet = planet,
                            IncidentType = IncidentType.Uprising,
                            Severity = consequence,
                            Tick = _game.CurrentTick,
                        }
                    );
                    return;
                }

                case 2:
                {
                    List<Regiment> regiments = planet
                        .GetAllRegiments()
                        .Where(r => r.GetOwnerInstanceID() == controllerInstanceId)
                        .ToList();
                    if (regiments.Count == 0)
                        return;
                    int index = provider.NextInt(0, regiments.Count);
                    Regiment target = regiments[index];
                    _game.DetachNode(target);
                    results.Add(
                        new PlanetIncidentResult
                        {
                            Planet = planet,
                            IncidentType = IncidentType.Uprising,
                            Severity = consequence,
                            Tick = _game.CurrentTick,
                        }
                    );
                    return;
                }

                case 3:
                {
                    List<Officer> candidates = planet
                        .GetAllOfficers()
                        .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && !o.IsCaptured)
                        .ToList();
                    if (candidates.Count == 0)
                        return;
                    int index = provider.NextInt(0, candidates.Count);
                    Officer target = candidates[index];
                    target.IsCaptured = true;
                    results.Add(
                        new OfficerCaptureStateResult
                        {
                            TargetOfficer = target,
                            IsCaptured = true,
                            Context = planet,
                            Tick = _game.CurrentTick,
                        }
                    );
                    return;
                }

                case 4:
                {
                    List<Officer> candidates = planet
                        .GetAllOfficers()
                        .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && o.IsCaptured)
                        .ToList();
                    if (candidates.Count == 0)
                        return;
                    int index = provider.NextInt(0, candidates.Count);
                    Officer target = candidates[index];
                    target.IsCaptured = false;
                    target.CaptorInstanceID = null;
                    results.Add(
                        new OfficerCaptureStateResult
                        {
                            TargetOfficer = target,
                            IsCaptured = false,
                            Context = planet,
                            Tick = _game.CurrentTick,
                        }
                    );
                    return;
                }

                case 5:
                {
                    List<Officer> captured = planet
                        .GetAllOfficers()
                        .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && o.IsCaptured)
                        .ToList();
                    foreach (Officer target in captured)
                    {
                        target.IsCaptured = false;
                        target.CaptorInstanceID = null;
                        results.Add(
                            new OfficerCaptureStateResult
                            {
                                TargetOfficer = target,
                                IsCaptured = false,
                                Context = planet,
                                Tick = _game.CurrentTick,
                            }
                        );
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Calculates how many garrison troops a planet requires for the given faction.
        /// Returns 0 when popular support is at or above the threshold.
        /// Core worlds with a faction GarrisonEfficiency modifier receive a reduced requirement.
        /// Planets in active uprisings apply the uprising multiplier.
        /// Corresponds to FUN_00508370 / FUN_0050a710_adjust_garrison_requirement.
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
        /// Calculates the uprising threshold used in the dice score formula.
        /// This is the simplified garrison requirement without efficiency or uprising multipliers,
        /// matching the threshold term in FUN_00558460_resolve_uprising_table_results.
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

        /// <summary>
        /// Returns the opposing faction with the highest popular support on this planet,
        /// or null if no opposing faction has any support.
        /// </summary>
        private Faction FindLeadingOpposingFaction(Planet planet, string ownerInstanceId)
        {
            string opposingFactionId = null;
            int maxSupport = 0;
            foreach (KeyValuePair<string, int> kvp in planet.PopularSupport)
            {
                if (kvp.Key != ownerInstanceId && kvp.Value > maxSupport)
                {
                    maxSupport = kvp.Value;
                    opposingFactionId = kvp.Key;
                }
            }
            return opposingFactionId != null
                ? _game.GetFactionByOwnerInstanceID(opposingFactionId)
                : null;
        }
    }
}
