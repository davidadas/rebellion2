using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Selects, decorates, and deploys officers during game generation.
    /// </summary>
    public sealed class OfficerSeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds officers into the generation context: selects, decorates, and places
        /// them, then stores the deployed and unrecruited pools on the context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            List<StartingOfficerRule> startingOfficerRules = GetApplicableStartingOfficerRules(
                ctx.Config.Officers,
                ctx.Summary.GalaxySize
            );
            Officer[] selected = SelectOfficers(
                ctx.Officers,
                ctx.Config,
                ctx.Summary,
                startingOfficerRules,
                ctx.Rng
            );
            DecorateOfficers(selected, ctx.Rng);
            DeployOfficers(selected, ctx.Sectors, startingOfficerRules, ctx.Rng);

            ctx.DeployedOfficers = selected;
            ctx.UnrecruitedOfficers = ctx.Officers.Except(selected).ToArray();
        }

        /// <summary>
        /// Selects which officers will be deployed at game start, grouped by faction
        /// and capped by the per-galaxy-size officer count.
        /// </summary>
        /// <param name="allOfficers">The full officer roster available for this game.</param>
        /// <param name="rules">Generation rules controlling officer selection.</param>
        /// <param name="summary">Game summary; galaxy size determines per-faction officer count.</param>
        /// <param name="startingOfficerRules">Guaranteed starting-officer rules for this galaxy size.</param>
        /// <param name="rng">Random number provider used for shuffling.</param>
        /// <returns>The officers selected for deployment.</returns>
        private Officer[] SelectOfficers(
            Officer[] allOfficers,
            GameGenerationConfig rules,
            GameSummary summary,
            IReadOnlyList<StartingOfficerRule> startingOfficerRules,
            IRandomNumberProvider rng
        )
        {
            Dictionary<string, List<Officer>> officersByFaction = GroupOfficersByFaction(
                allOfficers,
                rng
            );
            int recruitableLimit = ResolveOfficerCount(
                rules.Officers.NumStartingOfficers,
                summary.GalaxySize
            );
            HashSet<string> guaranteedOfficerIDs = new HashSet<string>(
                startingOfficerRules.Select(rule => rule.OfficerInstanceID),
                StringComparer.Ordinal
            );

            List<Officer> selected = new List<Officer>();
            foreach ((string factionId, List<Officer> officers) in officersByFaction)
            {
                selected.AddRange(
                    PickFactionRoster(officers, factionId, recruitableLimit, guaranteedOfficerIDs)
                );
            }

            string missingOfficerID = guaranteedOfficerIDs.FirstOrDefault(id =>
                selected.All(officer => officer.InstanceID != id)
            );
            if (missingOfficerID != null)
                throw new InvalidOperationException(
                    $"Starting officer rule references unknown officer '{missingOfficerID}'."
                );
            return selected.ToArray();
        }

        /// <summary>
        /// Selects the scenario-authored starting-officer rules that apply to one galaxy size.
        /// </summary>
        /// <param name="section">Officer generation configuration.</param>
        /// <param name="galaxySize">The generated galaxy size.</param>
        /// <returns>The applicable starting-officer rules.</returns>
        private static List<StartingOfficerRule> GetApplicableStartingOfficerRules(
            OfficerSection section,
            GameSize galaxySize
        )
        {
            return section
                .StartingOfficers.Where(rule =>
                    rule.GalaxySizes.Count == 0 || rule.GalaxySizes.Contains(galaxySize)
                )
                .ToList();
        }

        /// <summary>
        /// Groups officers by the faction that will own them, dropping any whose faction
        /// cannot be uniquely resolved. Officers with an explicit owner are placed at the
        /// front of their group so they survive the recruitable cutoff in
        /// <see cref="PickFactionRoster"/>.
        /// </summary>
        /// <param name="allOfficers">The full officer roster.</param>
        /// <param name="rng">Random number provider used for shuffling.</param>
        /// <returns>The officers grouped by faction ID.</returns>
        private Dictionary<string, List<Officer>> GroupOfficersByFaction(
            Officer[] allOfficers,
            IRandomNumberProvider rng
        )
        {
            Dictionary<string, List<Officer>> groups = new Dictionary<string, List<Officer>>();
            foreach (Officer officer in allOfficers.Shuffle(rng))
            {
                string factionId = ResolveOfficerFaction(officer);
                if (factionId == null)
                    continue;

                if (!groups.ContainsKey(factionId))
                    groups[factionId] = new List<Officer>();

                if (officer.OwnerInstanceID != null)
                    groups[factionId].Insert(0, officer);
                else
                    groups[factionId].Add(officer);
            }
            return groups;
        }

        /// <summary>
        /// Returns the faction that should own an officer at game start: their explicit
        /// owner if set, or their single recruiting faction. Officers with ambiguous factions
        /// return null and are skipped.
        /// </summary>
        /// <param name="officer">The officer being grouped.</param>
        /// <returns>The owning faction ID, or null when ambiguous.</returns>
        private string ResolveOfficerFaction(Officer officer)
        {
            if (officer.OwnerInstanceID != null)
                return officer.OwnerInstanceID;
            if (officer.RecruitingFactionInstanceIDs.Count == 1)
                return officer.RecruitingFactionInstanceIDs[0];
            return null;
        }

        /// <summary>
        /// Resolves the per-faction recruitable-officer count from a size-keyed profile.
        /// </summary>
        /// <param name="profile">The size-keyed profile.</param>
        /// <param name="size">The galaxy size for this game.</param>
        /// <returns>The number of recruitable officers a faction may receive.</returns>
        private int ResolveOfficerCount(PlanetSizeProfile profile, GameSize size)
        {
            return size switch
            {
                GameSize.Small => profile.Small,
                GameSize.Medium => profile.Medium,
                _ => profile.Large,
            };
        }

        /// <summary>
        /// Picks one faction's roster up to the configured total, while always retaining
        /// officers whose authored data requires them to start in play.
        /// </summary>
        /// <param name="officers">The faction's grouped officers (owned officers first).</param>
        /// <param name="factionId">The owning faction ID.</param>
        /// <param name="initialOfficerCount">Configured total starting officers.</param>
        /// <param name="guaranteedOfficerIDs">Officers guaranteed by scenario generation rules.</param>
        /// <returns>The officers picked for this faction.</returns>
        private List<Officer> PickFactionRoster(
            List<Officer> officers,
            string factionId,
            int initialOfficerCount,
            ISet<string> guaranteedOfficerIDs
        )
        {
            List<Officer> guaranteedOfficers = officers
                .Where(officer => guaranteedOfficerIDs.Contains(officer.InstanceID))
                .ToList();
            int remainingCount = Math.Max(0, initialOfficerCount - guaranteedOfficers.Count);
            List<Officer> picked = guaranteedOfficers
                .Concat(
                    officers
                        .Except(guaranteedOfficers)
                        .Where(officer => officer.IsRecruitable)
                        .Take(remainingCount)
                )
                .ToList();
            foreach (Officer officer in picked)
                officer.OwnerInstanceID = factionId;
            return picked;
        }

        /// <summary>
        /// Rolls variance for each officer's ratings and attributes and resolves their
        /// force-sensitivity state (known Jedi, dormant potential, or non-Jedi).
        /// </summary>
        /// <param name="officers">The officers to decorate.</param>
        /// <param name="rng">Random number provider for variance rolls.</param>
        private void DecorateOfficers(Officer[] officers, IRandomNumberProvider rng)
        {
            foreach (Officer officer in officers)
            {
                AddRatingVariance(officer, OfficerRating.Diplomacy, officer.DiplomacyVariance, rng);
                AddRatingVariance(officer, OfficerRating.Espionage, officer.EspionageVariance, rng);
                AddRatingVariance(officer, OfficerRating.Combat, officer.CombatVariance, rng);
                AddRatingVariance(
                    officer,
                    OfficerRating.Leadership,
                    officer.LeadershipVariance,
                    rng
                );

                officer.Loyalty += RollVariance(officer.LoyaltyVariance, rng);
                officer.ShipResearch += RollVariance(officer.ShipResearchVariance, rng);
                officer.TroopResearch += RollVariance(officer.TroopResearchVariance, rng);
                officer.FacilityResearch += RollVariance(officer.FacilityResearchVariance, rng);

                if (officer.IsKnownJedi)
                {
                    officer.IsForceSensitive = true;
                    officer.IsForceEligible = true;
                    officer.ForceValue =
                        officer.JediLevel + rng.NextInt(0, officer.JediLevelVariance + 1);
                }
                else if (officer.JediProbability > 0)
                {
                    double jediProbability = officer.JediProbability / 100.0;
                    if (rng.NextDouble() < jediProbability)
                    {
                        officer.IsForceSensitive = true;
                        officer.IsForceEligible = false;
                        officer.ForceValue = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a random value in [0, variance) to the officer's existing rating.
        /// </summary>
        /// <param name="officer">The officer whose rating is rolled.</param>
        /// <param name="rating">The rating to add variance to.</param>
        /// <param name="variance">The exclusive upper bound for the random variance.</param>
        /// <param name="rng">Random number provider.</param>
        private void AddRatingVariance(
            Officer officer,
            OfficerRating rating,
            int variance,
            IRandomNumberProvider rng
        )
        {
            officer.SetBaseRating(
                rating,
                officer.GetBaseRating(rating) + RollVariance(variance, rng)
            );
        }

        /// <summary>
        /// Returns a non-negative variance roll, returning 0 when <paramref name="variance"/>
        /// is non-positive to avoid calling <see cref="IRandomNumberProvider.NextInt"/> with
        /// an empty range.
        /// </summary>
        /// <param name="variance">Exclusive upper bound for the roll.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>A value in [0, variance), or 0 when variance is non-positive.</returns>
        private int RollVariance(int variance, IRandomNumberProvider rng)
        {
            return variance <= 0 ? 0 : rng.NextInt(0, variance);
        }

        /// <summary>
        /// Places each selected officer on a destination owned by their faction —
        /// either an explicit initial-parent target or a random owned planet
        /// or fleet.
        /// </summary>
        /// <param name="officers">The officers selected for deployment.</param>
        /// <param name="sectors">All planet sectors, used to enumerate per-faction destinations.</param>
        /// <param name="startingOfficerRules">Starting-officer placement rules for this galaxy.</param>
        /// <param name="rng">Random number provider for destination selection.</param>
        private void DeployOfficers(
            Officer[] officers,
            PlanetSector[] sectors,
            IReadOnlyList<StartingOfficerRule> startingOfficerRules,
            IRandomNumberProvider rng
        )
        {
            Dictionary<string, List<ISceneNode>> destinations =
                new Dictionary<string, List<ISceneNode>>();

            foreach (
                Planet planet in sectors
                    .SelectMany(s => s.GetChildren<Planet>())
                    .Where(p => p.OwnerInstanceID != null)
            )
            {
                AddDestination(destinations, planet);
                foreach (Fleet fleet in planet.GetChildren<Fleet>())
                    AddDestination(destinations, fleet);
            }

            foreach (Officer officer in officers)
            {
                List<ISceneNode> factionDests = destinations[officer.OwnerInstanceID];

                StartingOfficerRule rule = startingOfficerRules.FirstOrDefault(candidate =>
                    candidate.OfficerInstanceID == officer.InstanceID
                );
                ISceneNode destination = ResolveOfficerDestination(rule, factionDests, rng);

                if (destination is Planet planet)
                    planet.AddChild(officer);
                else if (destination is Fleet fleet && fleet.GetChildren<CapitalShip>().Count > 0)
                    fleet.GetChildren<CapitalShip>()[0].AddChild(officer);
            }
        }

        /// <summary>
        /// Resolves the starting destination for an officer.
        /// </summary>
        /// <param name="rule">The applicable scenario placement rule, when one exists.</param>
        /// <param name="factionDests">Available destinations owned by the officer's faction.</param>
        /// <param name="rng">Random number provider for fallback destination selection.</param>
        /// <returns>The destination that should receive the officer.</returns>
        private ISceneNode ResolveOfficerDestination(
            StartingOfficerRule rule,
            List<ISceneNode> factionDests,
            IRandomNumberProvider rng
        )
        {
            if (rule?.DestinationTypeID != null)
            {
                return factionDests.First(n =>
                    n is Planet planet && planet.TypeID == rule.DestinationTypeID
                );
            }

            if (rule?.DestinationInstanceID != null)
                return factionDests.First(n => n.InstanceID == rule.DestinationInstanceID);

            return factionDests[rng.NextInt(0, factionDests.Count)];
        }

        /// <summary>
        /// Adds a destination node to the per-faction deployment-target lookup.
        /// </summary>
        /// <param name="mapping">The faction → destinations map being built.</param>
        /// <param name="destination">The destination node to add (planet or fleet).</param>
        private void AddDestination(
            Dictionary<string, List<ISceneNode>> mapping,
            ISceneNode destination
        )
        {
            if (!mapping.ContainsKey(destination.OwnerInstanceID))
                mapping[destination.OwnerInstanceID] = new List<ISceneNode>();
            mapping[destination.OwnerInstanceID].Add(destination);
        }
    }
}
