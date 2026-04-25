using System;
using Rebellion.Game;
using Rebellion.Util.Common;

namespace Rebellion.Generation
{
    /// <summary>
    /// Configures planet properties (energy, raw materials, colonization, popular support)
    /// based on bucket assignment. Reproduces the original game's setup_core_system and
    /// setup_rim_system functions.
    /// </summary>
    public class SystemConfigurator
    {
        /// <summary>
        /// Populates per-planet properties (energy, raw materials, colonization, popular support,
        /// visitors) across every system. Core and rim systems follow different rules derived
        /// from the classification result and generation config.
        /// </summary>
        /// <param name="systems">All generated planet systems to configure.</param>
        /// <param name="classification">Bucket/HQ assignments produced by <see cref="GalaxyClassifier"/>.</param>
        /// <param name="rules">Generation rules supplying resource dice and support formulas.</param>
        /// <param name="factionIds">IDs of every faction in the game, used to distribute support.</param>
        /// <param name="rng">Random-number provider for dice rolls and colonization chances.</param>
        public void Configure(
            PlanetSystem[] systems,
            GalaxyClassificationResult classification,
            GameGenerationRules rules,
            string[] factionIds,
            IRandomNumberProvider rng
        )
        {
            SystemResourcesSection res = rules.SystemResources;
            SystemSupportSection sup = rules.SystemSupport;

            foreach (PlanetSystem system in systems)
            {
                bool isCore = system.SystemType == PlanetSystemType.CoreSystem;

                foreach (Planet planet in system.Planets)
                {
                    // Energy
                    int energy;
                    if (isCore)
                    {
                        energy = RollDice(res.CoreEnergy, rng);
                    }
                    else
                    {
                        energy = RollDice(res.RimEnergy, rng);
                    }
                    energy = Math.Min(Math.Max(energy, res.EnergyMin), res.EnergyMax);
                    planet.EnergyCapacity = energy;

                    // Raw materials (clamped to energy as upper bound, matching original)
                    int rawMat;
                    if (isCore)
                    {
                        rawMat = RollDice(res.CoreRawMaterials, rng);
                    }
                    else
                    {
                        rawMat = RollDice(res.RimRawMaterials, rng);
                    }
                    rawMat = Math.Min(Math.Max(rawMat, res.RawMaterialsMin), res.RawMaterialsMax);
                    rawMat = Math.Min(rawMat, energy); // Can't exceed energy
                    planet.NumRawResourceNodes = rawMat;

                    // Colonization
                    if (isCore)
                    {
                        // All core systems are colonized
                        planet.IsColonized = true;
                    }
                    else if (!planet.IsColonized)
                    {
                        // Rim: 31% chance (unless already colonized by classifier)
                        int roll = rng.NextInt(0, 100);
                        if (roll < res.RimColonizationPct)
                        {
                            planet.IsColonized = true;
                        }
                    }

                    // Popular support
                    SetPopularSupport(planet, isCore, classification, sup, factionIds, rng);

                    // Core systems are always visited by all factions.
                    // Outer rim owned planets are visited by their owner.
                    if (isCore)
                    {
                        foreach (string factionId in factionIds)
                        {
                            planet.AddVisitor(factionId);
                        }
                    }
                    else if (planet.OwnerInstanceID != null)
                    {
                        planet.AddVisitor(planet.OwnerInstanceID);
                    }
                }
            }
        }

        private void SetPopularSupport(
            Planet planet,
            bool isCore,
            GalaxyClassificationResult classification,
            SystemSupportSection sup,
            string[] factionIds,
            IRandomNumberProvider rng
        )
        {
            planet.PopularSupport.Clear();

            if (factionIds.Length < 2)
                return;

            // Starting planets with configured loyalty: owner gets loyalty%, remainder to others
            if (classification.StartingPlanetLoyalty.TryGetValue(planet, out int loyalty))
            {
                string owner = planet.OwnerInstanceID;
                if (owner != null)
                {
                    int remainder = 100 - loyalty;
                    foreach (string factionId in factionIds)
                    {
                        if (factionId == owner)
                            planet.PopularSupport[factionId] = loyalty;
                        else
                            planet.PopularSupport[factionId] = remainder / (factionIds.Length - 1);
                    }
                    return;
                }
            }

            // Rim systems: owned planets get 100% owner support, unowned get even split.
            // Original: setup_rim_system checks ownership flag for faction-specific support.
            if (!isCore)
            {
                if (planet.OwnerInstanceID != null)
                {
                    foreach (string factionId in factionIds)
                    {
                        planet.PopularSupport[factionId] =
                            factionId == planet.OwnerInstanceID ? 100 : 0;
                    }
                }
                else
                {
                    int even = 100 / factionIds.Length;
                    foreach (string factionId in factionIds)
                        planet.PopularSupport[factionId] = even;
                }
                return;
            }

            // Core systems: use bucket-based support formula
            if (!classification.BucketMap.TryGetValue(planet, out PlanetBucket bucket))
            {
                // Unclassified core planet - equal support
                int even = 100 / factionIds.Length;
                foreach (string factionId in factionIds)
                    planet.PopularSupport[factionId] = even;
                return;
            }

            // Get the support formula for this bucket strength
            SupportFormula formula = bucket.Strength switch
            {
                BucketStrength.Strong => sup.Strong,
                BucketStrength.Weak => sup.Weak,
                BucketStrength.Neutral => sup.Neutral,
                _ => sup.Neutral,
            };

            // Calculate support: base + roll(random)
            int support = formula.Base;
            if (formula.Random > 0)
                support += rng.NextInt(0, formula.Random + 1);

            support = Math.Min(Math.Max(support, 0), 100);

            if (bucket.FactionID != null)
            {
                // Faction bucket: assign support to the bucket's faction, remainder to others
                int remainder = 100 - support;
                int otherCount = factionIds.Length - 1;
                foreach (string factionId in factionIds)
                {
                    if (factionId == bucket.FactionID)
                        planet.PopularSupport[factionId] = support;
                    else
                        planet.PopularSupport[factionId] = remainder / otherCount;
                }
            }
            else
            {
                // Neutral bucket: first faction gets support, remainder split among others
                planet.PopularSupport[factionIds[0]] = support;
                int remainder = 100 - support;
                int otherCount = factionIds.Length - 1;
                for (int i = 1; i < factionIds.Length; i++)
                    planet.PopularSupport[factionIds[i]] = remainder / otherCount;
            }
        }

        private int RollDice(DiceFormula formula, IRandomNumberProvider rng)
        {
            // Original roll_dice(N) calls generate_random_in_range(N+1), producing [0, N] inclusive.
            // NextInt(min, max) is [min, max) exclusive, so we use N+1 to match.
            int value = formula.Base;
            if (formula.Random1 > 0)
                value += rng.NextInt(0, formula.Random1 + 1);
            if (formula.Random2 > 0)
                value += rng.NextInt(0, formula.Random2 + 1);
            return value;
        }
    }
}
