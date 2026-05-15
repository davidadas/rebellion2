using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Common;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds per-planet stats: energy capacity, raw resource nodes, colonization,
    /// popular support, and starting visitors.
    /// </summary>
    public sealed class PlanetSeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds per-planet stats across every system in the generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            Configure(ctx.Systems, ctx.Classification, ctx.Config, ctx.Summary, ctx.Rng);
        }

        /// <summary>
        /// Populates per-planet stats across every system. Core and rim systems follow
        /// different rules derived from the classification result and generation config.
        /// </summary>
        /// <param name="systems">All generated planet systems to configure.</param>
        /// <param name="classification">Bucket and HQ assignments produced by <see cref="GalaxySeeder"/>.</param>
        /// <param name="rules">Generation rules supplying resource dice and support formulas.</param>
        /// <param name="summary">Game summary; resource availability selects the resource profile, faction IDs drive support distribution.</param>
        /// <param name="rng">Random number provider for dice rolls and colonization chances.</param>
        private void Configure(
            PlanetSystem[] systems,
            GalaxyClassificationResult classification,
            GameGenerationConfig rules,
            GameSummary summary,
            IRandomNumberProvider rng
        )
        {
            SystemResourceProfile res = ResolveResourceProfile(
                rules.SystemResources,
                summary.ResourceAvailability
            );
            SystemSupportSection sup = rules.SystemSupport;
            string[] factionIds = summary.StartingFactionIDs;

            foreach (PlanetSystem system in systems)
            {
                bool isCore = system.SystemType == PlanetSystemType.CoreSystem;

                foreach (Planet planet in system.Planets)
                {
                    RollPlanetResources(planet, isCore, res, rng);
                    ResolveColonization(planet, isCore, res.RimColonizationPct, rng);
                    SetPopularSupport(planet, isCore, classification, sup, factionIds, rng);
                    RecordStartingVisitors(planet, isCore, factionIds);
                }
            }
        }

        /// <summary>
        /// Picks the resource profile matching the player's chosen availability, falling
        /// back to Normal (or the first profile) if no exact match is configured.
        /// </summary>
        /// <param name="section">The resource section with all profiles.</param>
        /// <param name="availability">The availability tier chosen on the new-game screen.</param>
        /// <returns>The matching <see cref="SystemResourceProfile"/>.</returns>
        private SystemResourceProfile ResolveResourceProfile(
            SystemResourcesSection section,
            GameResourceAvailability availability
        )
        {
            if (section?.Profiles == null || section.Profiles.Count == 0)
            {
                throw new InvalidOperationException(
                    "SystemResources must define at least one profile. Check GameGenerationConfig.xml."
                );
            }

            return section.Profiles.FirstOrDefault(p => p.Availability == availability)
                ?? section.Profiles.FirstOrDefault(p =>
                    p.Availability == GameResourceAvailability.Normal
                )
                ?? section.Profiles[0];
        }

        /// <summary>
        /// Rolls a planet's energy capacity and raw-resource node count, clamped to the
        /// configured min/max range. Raw materials are further capped at the rolled
        /// energy so a planet never has more mineable nodes than energy slots.
        /// </summary>
        /// <param name="planet">The planet to update.</param>
        /// <param name="isCore">True when the planet sits in a core system.</param>
        /// <param name="res">Resource dice formulas and clamps.</param>
        /// <param name="rng">Random number provider.</param>
        private void RollPlanetResources(
            Planet planet,
            bool isCore,
            SystemResourceProfile res,
            IRandomNumberProvider rng
        )
        {
            int energy = RollDice(isCore ? res.CoreEnergy : res.RimEnergy, rng);
            energy = Math.Min(Math.Max(energy, res.EnergyMin), res.EnergyMax);
            planet.EnergyCapacity = energy;

            int rawMat = RollDice(isCore ? res.CoreRawMaterials : res.RimRawMaterials, rng);
            rawMat = Math.Min(Math.Max(rawMat, res.RawMaterialsMin), res.RawMaterialsMax);
            rawMat = Math.Min(rawMat, energy);
            planet.NumRawResourceNodes = rawMat;
        }

        /// <summary>
        /// Marks a planet colonized. Core planets are always colonized; rim planets
        /// that aren't already colonized roll for it against the configured percentage.
        /// </summary>
        /// <param name="planet">The planet to update.</param>
        /// <param name="isCore">True when the planet sits in a core system.</param>
        /// <param name="rimColonizationPct">Percent chance (0-100) a rim planet is colonized.</param>
        /// <param name="rng">Random number provider.</param>
        private void ResolveColonization(
            Planet planet,
            bool isCore,
            int rimColonizationPct,
            IRandomNumberProvider rng
        )
        {
            if (isCore)
            {
                planet.IsColonized = true;
                return;
            }
            if (planet.IsColonized)
            {
                return;
            }
            if (rng.NextInt(0, 100) < rimColonizationPct)
            {
                planet.IsColonized = true;
            }
        }

        /// <summary>
        /// Records which factions have already visited a planet at game start. Core
        /// planets are visible to every faction; rim planets only to their owner.
        /// </summary>
        /// <param name="planet">The planet to update.</param>
        /// <param name="isCore">True when the planet sits in a core system.</param>
        /// <param name="factionIds">IDs of every faction in the game.</param>
        private void RecordStartingVisitors(Planet planet, bool isCore, string[] factionIds)
        {
            if (isCore)
            {
                foreach (string factionId in factionIds)
                {
                    planet.AddVisitor(factionId);
                }
                return;
            }
            if (planet.OwnerInstanceID != null)
            {
                planet.AddVisitor(planet.OwnerInstanceID);
            }
        }

        /// <summary>
        /// Distributes popular support for a single planet across all factions, choosing
        /// the right distribution strategy based on whether the planet is a starting
        /// planet, a rim planet, or a core planet with or without a bucket assignment.
        /// </summary>
        /// <param name="planet">The planet whose support is being set.</param>
        /// <param name="isCore">True when the planet sits in a core system.</param>
        /// <param name="classification">Bucket and HQ assignments from <see cref="GalaxySeeder"/>.</param>
        /// <param name="sup">Support formulas keyed by bucket strength.</param>
        /// <param name="factionIds">IDs of every faction in the game.</param>
        /// <param name="rng">Random number provider for support rolls.</param>
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

            if (
                classification.StartingPlanetLoyalty.TryGetValue(planet, out int loyalty)
                && planet.OwnerInstanceID != null
            )
            {
                DistributeOwnerLoyalty(planet, planet.OwnerInstanceID, loyalty, factionIds);
                return;
            }

            if (!isCore)
            {
                DistributeRimSupport(planet, factionIds);
                return;
            }

            if (!classification.BucketMap.TryGetValue(planet, out PlanetBucket bucket))
            {
                DistributeEvenly(planet, factionIds);
                return;
            }

            int support = RollBucketSupport(bucket, sup, rng);
            DistributeBucketSupport(planet, bucket, support, factionIds);
        }

        /// <summary>
        /// Distributes popular support so the owner receives a fixed share and the
        /// remainder is split evenly among all other factions.
        /// </summary>
        /// <param name="planet">The planet to update.</param>
        /// <param name="owner">The faction ID receiving the owner share.</param>
        /// <param name="ownerShare">The support percentage going to the owner.</param>
        /// <param name="factionIds">IDs of every faction in the game.</param>
        private void DistributeOwnerLoyalty(
            Planet planet,
            string owner,
            int ownerShare,
            string[] factionIds
        )
        {
            int othersShare = (100 - ownerShare) / (factionIds.Length - 1);
            foreach (string factionId in factionIds)
            {
                planet.PopularSupport[factionId] = factionId == owner ? ownerShare : othersShare;
            }
        }

        /// <summary>
        /// Distributes popular support on a rim planet: the owner (if any) gets full
        /// support, otherwise support is split evenly among all factions.
        /// </summary>
        /// <param name="planet">The rim planet to update.</param>
        /// <param name="factionIds">IDs of every faction in the game.</param>
        private void DistributeRimSupport(Planet planet, string[] factionIds)
        {
            if (planet.OwnerInstanceID != null)
            {
                foreach (string factionId in factionIds)
                {
                    planet.PopularSupport[factionId] =
                        factionId == planet.OwnerInstanceID ? 100 : 0;
                }
                return;
            }
            DistributeEvenly(planet, factionIds);
        }

        /// <summary>
        /// Splits popular support evenly across all factions.
        /// </summary>
        /// <param name="planet">The planet to update.</param>
        /// <param name="factionIds">IDs of every faction in the game.</param>
        private void DistributeEvenly(Planet planet, string[] factionIds)
        {
            int even = 100 / factionIds.Length;
            foreach (string factionId in factionIds)
                planet.PopularSupport[factionId] = even;
        }

        /// <summary>
        /// Rolls the popular-support percentage for a bucket-assigned core planet
        /// using the strength-specific support formula.
        /// </summary>
        /// <param name="bucket">The planet's bucket assignment.</param>
        /// <param name="sup">Support formulas keyed by bucket strength.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>A clamped support value in the [0, 100] range.</returns>
        private int RollBucketSupport(
            PlanetBucket bucket,
            SystemSupportSection sup,
            IRandomNumberProvider rng
        )
        {
            SupportFormula formula = bucket.Strength switch
            {
                BucketStrength.Strong => sup.Strong,
                BucketStrength.Weak => sup.Weak,
                _ => sup.Neutral,
            };

            int support = formula.Base;
            if (formula.Random > 0)
                support += rng.NextInt(0, formula.Random + 1);
            return Math.Min(Math.Max(support, 0), 100);
        }

        /// <summary>
        /// Distributes a rolled support value to a bucket-assigned planet. Faction
        /// buckets give the bucket owner the rolled value; neutral buckets give it to
        /// the first faction. In both cases the remainder is split evenly among the
        /// other factions.
        /// </summary>
        /// <param name="planet">The planet to update.</param>
        /// <param name="bucket">The planet's bucket assignment.</param>
        /// <param name="support">The rolled support value.</param>
        /// <param name="factionIds">IDs of every faction in the game.</param>
        private void DistributeBucketSupport(
            Planet planet,
            PlanetBucket bucket,
            int support,
            string[] factionIds
        )
        {
            int otherCount = factionIds.Length - 1;
            int remainder = (100 - support) / otherCount;

            if (bucket.FactionID != null)
            {
                foreach (string factionId in factionIds)
                {
                    planet.PopularSupport[factionId] =
                        factionId == bucket.FactionID ? support : remainder;
                }
                return;
            }

            planet.PopularSupport[factionIds[0]] = support;
            for (int i = 1; i < factionIds.Length; i++)
                planet.PopularSupport[factionIds[i]] = remainder;
        }

        /// <summary>
        /// Rolls a dice formula: base value plus up to two optional random terms.
        /// </summary>
        /// <param name="formula">The formula to roll.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>The rolled value.</returns>
        private int RollDice(DiceFormula formula, IRandomNumberProvider rng)
        {
            int value = formula.Base;
            if (formula.Random1 > 0)
                value += rng.NextInt(0, formula.Random1 + 1);
            if (formula.Random2 > 0)
                value += rng.NextInt(0, formula.Random2 + 1);
            return value;
        }
    }
}
