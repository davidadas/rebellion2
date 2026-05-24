using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.World;
using Rebellion.Util.Common;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds starting territory: assigns ownership and a Strong / Weak / Neutral
    /// strength tag to every core planet based on difficulty-driven percentages,
    /// places named starting planets, and records each faction's headquarters.
    /// </summary>
    public sealed class GalaxySeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds galaxy ownership, HQs, and strength tags into the generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            ctx.Classification = BuildClassification(
                ctx.Systems,
                ctx.Factions,
                ctx.Summary,
                ctx.Config,
                ctx.Rng
            );
        }

        /// <summary>
        /// Builds the classification result: assigns ownership to starting planets and
        /// to bucket-assigned core planets, picks each faction's HQ, and tags every core
        /// planet with a Strong / Weak / Neutral strength bucket.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="factions">All factions in the game.</param>
        /// <param name="summary">Game summary with player faction and difficulty settings.</param>
        /// <param name="rules">Generation rules containing faction setups and difficulty profiles.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>Classification result with bucket map, faction HQs, and starting-planet loyalty.</returns>
        private GalaxyClassificationResult BuildClassification(
            PlanetSystem[] systems,
            Faction[] factions,
            GameSummary summary,
            GameGenerationConfig rules,
            IRandomNumberProvider rng
        )
        {
            GalaxyClassificationSection config = rules.GalaxyClassification;
            GalaxyClassificationResult result = new GalaxyClassificationResult();
            Dictionary<string, Faction> factionMap = factions.ToDictionary(f => f.InstanceID);

            ValidateStartingPlanets(config);

            (
                List<Planet> corePlanets,
                List<(
                    PlanetSystem system,
                    Planet planet,
                    FactionSetup setup,
                    StartingPlanet config
                )> resolvedStartingPlanets,
                int preassignedCoreCount
            ) = PartitionPlanets(systems, config, rng);

            AssignStartingPlanets(
                resolvedStartingPlanets,
                factionMap,
                result,
                out Dictionary<string, int> strongCountAdjustments
            );

            DifficultyProfile profile = ResolveDifficultyProfile(config, summary);

            AssignCoreBuckets(
                corePlanets,
                profile,
                preassignedCoreCount,
                strongCountAdjustments,
                result,
                rng
            );

            AssignBucketPlanetOwnership(result);

            return result;
        }

        /// <summary>
        /// Validates that no two factions claim the same starting planet in config.
        /// </summary>
        /// <param name="config">Galaxy classification config to validate.</param>
        private void ValidateStartingPlanets(GalaxyClassificationSection config)
        {
            HashSet<string> claimedPlanetIds = new HashSet<string>();
            foreach (FactionSetup setup in config.FactionSetups)
            {
                if (setup.StartingPlanets == null)
                    continue;
                foreach (StartingPlanet sp in setup.StartingPlanets)
                {
                    if (string.IsNullOrEmpty(sp.PlanetInstanceID))
                        continue;
                    if (!claimedPlanetIds.Add(sp.PlanetInstanceID))
                        throw new InvalidOperationException(
                            $"Planet '{sp.PlanetInstanceID}' is claimed by multiple factions in FactionSetups."
                        );
                }
            }
        }

        /// <summary>
        /// Separates all planets into core, rim, and starting-planet lists.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="config">Galaxy classification config with faction setups.</param>
        /// <param name="rng">Random number provider for dynamic starting planet selection.</param>
        /// <returns>Core planets, resolved starting planets, and count of
        /// pre-assigned core starting planets.</returns>
        private (
            List<Planet> corePlanets,
            List<(
                PlanetSystem system,
                Planet planet,
                FactionSetup setup,
                StartingPlanet config
            )> resolvedStartingPlanets,
            int preassignedCoreCount
        ) PartitionPlanets(
            PlanetSystem[] systems,
            GalaxyClassificationSection config,
            IRandomNumberProvider rng
        )
        {
            Dictionary<string, (FactionSetup setup, StartingPlanet config)> staticStartingPlanets =
                new Dictionary<string, (FactionSetup setup, StartingPlanet config)>();
            foreach (FactionSetup setup in config.FactionSetups)
            {
                if (setup.StartingPlanets == null)
                    continue;
                foreach (StartingPlanet sp in setup.StartingPlanets)
                {
                    if (!string.IsNullOrEmpty(sp.PlanetInstanceID) && !sp.PickFromRim)
                        staticStartingPlanets[sp.PlanetInstanceID] = (setup, sp);
                }
            }

            List<Planet> corePlanets = new List<Planet>();
            List<(PlanetSystem system, Planet planet)> rimPlanets =
                new List<(PlanetSystem system, Planet planet)>();
            List<(PlanetSystem, Planet, FactionSetup, StartingPlanet)> resolved =
                new List<(PlanetSystem, Planet, FactionSetup, StartingPlanet)>();
            int preassignedCoreCount = 0;

            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (
                        staticStartingPlanets.TryGetValue(
                            planet.InstanceID,
                            out (FactionSetup setup, StartingPlanet config) entry
                        )
                    )
                    {
                        resolved.Add((system, planet, entry.setup, entry.config));
                        if (system.SystemType == PlanetSystemType.CoreSystem)
                            preassignedCoreCount++;
                        continue;
                    }

                    if (system.SystemType == PlanetSystemType.CoreSystem)
                        corePlanets.Add(planet);
                    else
                        rimPlanets.Add((system, planet));
                }
            }

            foreach (FactionSetup setup in config.FactionSetups)
            {
                if (setup.StartingPlanets == null)
                    continue;
                foreach (StartingPlanet sp in setup.StartingPlanets)
                {
                    if (!sp.PickFromRim || rimPlanets.Count == 0)
                        continue;

                    int hqIndex = rng.NextInt(0, rimPlanets.Count);
                    (PlanetSystem system, Planet planet) = rimPlanets[hqIndex];
                    rimPlanets.RemoveAt(hqIndex);
                    resolved.Add((system, planet, setup, sp));
                }
            }

            return (corePlanets, resolved, preassignedCoreCount);
        }

        /// <summary>
        /// Sets ownership, colonization, loyalty, HQ flags, and Strong bucket assignment
        /// for all resolved starting planets.
        /// </summary>
        /// <param name="resolvedStartingPlanets">Starting planets with their faction setup and config.</param>
        /// <param name="factionMap">Faction lookup by instance ID.</param>
        /// <param name="result">Classification result to populate.</param>
        /// <param name="strongCountAdjustments">Output: per-faction count of pre-assigned core
        /// starting planets, used to adjust bucket sizes later.</param>
        private void AssignStartingPlanets(
            List<(
                PlanetSystem system,
                Planet planet,
                FactionSetup setup,
                StartingPlanet config
            )> resolvedStartingPlanets,
            Dictionary<string, Faction> factionMap,
            GalaxyClassificationResult result,
            out Dictionary<string, int> strongCountAdjustments
        )
        {
            strongCountAdjustments = new Dictionary<string, int>();

            foreach (
                (
                    PlanetSystem system,
                    Planet planet,
                    FactionSetup setup,
                    StartingPlanet spConfig
                ) in resolvedStartingPlanets
            )
            {
                Faction faction = factionMap[setup.FactionID];

                planet.OwnerInstanceID = faction.InstanceID;
                planet.IsColonized = true;
                result.StartingPlanetLoyalty[planet] = spConfig.Loyalty;
                result.BucketMap[planet] = new PlanetBucket
                {
                    FactionID = faction.InstanceID,
                    Strength = BucketStrength.Strong,
                };

                if (spConfig.IsHeadquarters)
                {
                    planet.IsHeadquarters = true;
                    faction.HQInstanceID = planet.InstanceID;
                    result.FactionHQs[faction.InstanceID] = planet;
                }

                if (system.SystemType == PlanetSystemType.CoreSystem)
                {
                    if (!strongCountAdjustments.ContainsKey(setup.FactionID))
                        strongCountAdjustments[setup.FactionID] = 0;
                    strongCountAdjustments[setup.FactionID]++;
                }
            }
        }

        /// <summary>
        /// Finds the difficulty profile matching the player's faction and difficulty level.
        /// Falls back through progressively looser matches: exact -> faction wildcard ->
        /// any wildcard -> "Default" name -> first entry.
        /// </summary>
        /// <param name="config">Galaxy classification config with difficulty profiles.</param>
        /// <param name="summary">Game summary with player faction and difficulty.</param>
        /// <returns>The best-matching difficulty profile.</returns>
        private DifficultyProfile ResolveDifficultyProfile(
            GalaxyClassificationSection config,
            GameSummary summary
        )
        {
            int difficulty = (int)summary.Difficulty;
            return config.Profiles.FirstOrDefault(p =>
                    p.PlayerFactionID == summary.PlayerFactionID && p.Difficulty == difficulty
                )
                ?? config.Profiles.FirstOrDefault(p =>
                    p.PlayerFactionID == summary.PlayerFactionID && p.Difficulty == -1
                )
                ?? config.Profiles.FirstOrDefault(p =>
                    string.IsNullOrEmpty(p.PlayerFactionID) && p.Difficulty == -1
                )
                ?? config.Profiles.FirstOrDefault(p => p.Name == "Default")
                ?? config.Profiles[0];
        }

        /// <summary>
        /// Assigns the remaining core planets to faction buckets (Strong, Weak, Neutral)
        /// based on the difficulty profile's per-faction percentages.
        /// </summary>
        /// <param name="corePlanets">Unassigned core planets to classify.</param>
        /// <param name="profile">Difficulty profile with per-faction bucket percentages.</param>
        /// <param name="preassignedCoreCount">Number of core planets already assigned as starting planets.</param>
        /// <param name="strongCountAdjustments">Per-faction count of pre-assigned core starting planets.</param>
        /// <param name="result">Classification result to populate with bucket assignments.</param>
        /// <param name="rng">Random number provider for shuffle.</param>
        private void AssignCoreBuckets(
            List<Planet> corePlanets,
            DifficultyProfile profile,
            int preassignedCoreCount,
            Dictionary<string, int> strongCountAdjustments,
            GalaxyClassificationResult result,
            IRandomNumberProvider rng
        )
        {
            int totalCore = corePlanets.Count + preassignedCoreCount;

            List<(string factionID, int strongCount, int weakCount)> factionBucketCounts =
                new List<(string factionID, int strongCount, int weakCount)>();
            foreach (FactionBucketConfig fb in profile.FactionBuckets)
            {
                int strong = totalCore * fb.StrongPct / 100;
                int weak = totalCore * fb.WeakPct / 100;

                if (strongCountAdjustments.TryGetValue(fb.FactionID, out int adj))
                    strong = Math.Max(0, strong - adj);

                factionBucketCounts.Add((fb.FactionID, strong, weak));
            }

            for (int i = corePlanets.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (corePlanets[i], corePlanets[j]) = (corePlanets[j], corePlanets[i]);
            }

            int idx = 0;
            foreach ((string factionID, int strongCount, int weakCount) in factionBucketCounts)
            {
                for (int i = 0; i < strongCount && idx < corePlanets.Count; i++, idx++)
                {
                    result.BucketMap[corePlanets[idx]] = new PlanetBucket
                    {
                        FactionID = factionID,
                        Strength = BucketStrength.Strong,
                    };
                }
                for (int i = 0; i < weakCount && idx < corePlanets.Count; i++, idx++)
                {
                    result.BucketMap[corePlanets[idx]] = new PlanetBucket
                    {
                        FactionID = factionID,
                        Strength = BucketStrength.Weak,
                    };
                }
            }

            while (idx < corePlanets.Count)
            {
                result.BucketMap[corePlanets[idx]] = new PlanetBucket
                {
                    FactionID = null,
                    Strength = BucketStrength.Neutral,
                };
                idx++;
            }
        }

        /// <summary>
        /// Sets ownership and colonization on bucket-owned planets that weren't
        /// already assigned as starting planets.
        /// </summary>
        /// <param name="result">Classification result containing the bucket map.</param>
        private void AssignBucketPlanetOwnership(GalaxyClassificationResult result)
        {
            foreach (KeyValuePair<Planet, PlanetBucket> kvp in result.BucketMap)
            {
                Planet planet = kvp.Key;
                if (planet.OwnerInstanceID != null)
                    continue;

                if (kvp.Value.FactionID != null)
                {
                    planet.OwnerInstanceID = kvp.Value.FactionID;
                    planet.IsColonized = true;
                }
            }
        }
    }
}
