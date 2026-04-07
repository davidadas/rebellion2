using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Selects, decorates, and deploys officers during game generation.
    /// </summary>
    public class OfficerGenerator
    {
        public struct OfficerResults
        {
            public Officer[] Deployed;
            public Officer[] Unrecruited;
        }

        public OfficerResults Deploy(
            PlanetSystem[] systems,
            GameGenerationRules rules,
            GameSummary summary,
            IRandomNumberProvider rng
        )
        {
            IResourceManager resourceManager = ResourceManager.Instance;
            Officer[] allOfficers = resourceManager.GetGameData<Officer>();

            Officer[] selected = SelectOfficers(allOfficers, rules, summary, rng);
            DecorateOfficers(selected, rng);
            DeployOfficers(selected, systems, rng);

            Officer[] unrecruited = allOfficers.Except(selected).ToArray();

            return new OfficerResults { Deployed = selected, Unrecruited = unrecruited };
        }

        private Officer[] SelectOfficers(
            Officer[] allOfficers,
            GameGenerationRules rules,
            GameSummary summary,
            IRandomNumberProvider rng
        )
        {
            // Group officers by faction
            Dictionary<string, List<Officer>> officersByFaction =
                new Dictionary<string, List<Officer>>();

            foreach (Officer officer in allOfficers.Shuffle(rng))
            {
                string factionId =
                    officer.OwnerInstanceID
                    ?? (
                        officer.AllowedOwnerInstanceIDs.Count == 1
                            ? officer.AllowedOwnerInstanceIDs[0]
                            : null
                    );

                if (factionId == null)
                    continue;

                if (!officersByFaction.ContainsKey(factionId))
                    officersByFaction[factionId] = new List<Officer>();

                // Main/owned officers go first
                if (officer.OwnerInstanceID != null)
                    officersByFaction[factionId].Insert(0, officer);
                else
                    officersByFaction[factionId].Add(officer);
            }

            // Determine count from galaxy size
            PlanetSizeProfile profile = rules.Officers.NumInitialOfficers;
            int numAllowed = summary.GalaxySize switch
            {
                GameSize.Small => profile.Small,
                GameSize.Medium => profile.Medium,
                _ => profile.Large,
            };

            // Select from each faction
            List<Officer> selected = new List<Officer>();
            foreach ((string ownerInstanceId, List<Officer> officers) in officersByFaction)
            {
                IEnumerable<Officer> picked = officers
                    .Where(o => o.IsMain || o.IsRecruitable)
                    .TakeWhile((o, index) => o.IsMain || index < numAllowed)
                    .Select(o =>
                    {
                        o.OwnerInstanceID = ownerInstanceId;
                        return o;
                    });

                selected.AddRange(picked);
            }

            return selected.ToArray();
        }

        private void DecorateOfficers(Officer[] officers, IRandomNumberProvider rng)
        {
            foreach (Officer officer in officers)
            {
                // Skills
                officer.SetSkillValue(
                    MissionParticipantSkill.Diplomacy,
                    officer.GetSkillValue(MissionParticipantSkill.Diplomacy)
                        + rng.NextInt(0, officer.DiplomacyVariance)
                );
                officer.SetSkillValue(
                    MissionParticipantSkill.Espionage,
                    officer.GetSkillValue(MissionParticipantSkill.Espionage)
                        + rng.NextInt(0, officer.EspionageVariance)
                );
                officer.SetSkillValue(
                    MissionParticipantSkill.Combat,
                    officer.GetSkillValue(MissionParticipantSkill.Combat)
                        + rng.NextInt(0, officer.CombatVariance)
                );
                officer.SetSkillValue(
                    MissionParticipantSkill.Leadership,
                    officer.GetSkillValue(MissionParticipantSkill.Leadership)
                        + rng.NextInt(0, officer.LeadershipVariance)
                );

                // Attributes
                officer.Loyalty += rng.NextInt(0, officer.LoyaltyVariance);
                officer.ShipResearch += rng.NextInt(0, officer.ShipResearchVariance);
                officer.TroopResearch += rng.NextInt(0, officer.TroopResearchVariance);
                officer.FacilityResearch += rng.NextInt(0, officer.FacilityResearchVariance);

                // Force sensitivity: known Jedi get full init, others are dormant potentials
                if (officer.IsKnownJedi)
                {
                    officer.IsJedi = true;
                    officer.IsForceEligible = true;
                    officer.ForceValue =
                        officer.JediLevel + rng.NextInt(0, officer.JediLevelVariance + 1);
                }
                else if (officer.JediProbability > 0)
                {
                    double jediProbability = officer.JediProbability / 100.0;
                    if (rng.NextDouble() < jediProbability)
                    {
                        officer.IsJedi = true;
                        officer.IsForceEligible = false;
                        officer.ForceValue = 0;
                    }
                }
            }
        }

        private void DeployOfficers(
            Officer[] officers,
            PlanetSystem[] systems,
            IRandomNumberProvider rng
        )
        {
            // Build destination mapping: faction -> list of planets and fleets
            Dictionary<string, List<ISceneNode>> destinations =
                new Dictionary<string, List<ISceneNode>>();

            foreach (
                Planet planet in systems
                    .SelectMany(s => s.Planets)
                    .Where(p => p.OwnerInstanceID != null)
            )
            {
                AddDestination(destinations, planet);
                foreach (Fleet fleet in planet.Fleets)
                    AddDestination(destinations, fleet);
            }

            foreach (Officer officer in officers)
            {
                List<ISceneNode> factionDests = destinations[officer.OwnerInstanceID];

                ISceneNode destination =
                    officer.InitialParentInstanceID != null
                        ? factionDests.First(n => n.InstanceID == officer.InitialParentInstanceID)
                        : factionDests[rng.NextInt(0, factionDests.Count)];

                if (destination is Planet planet)
                    planet.AddChild(officer);
                else if (destination is Fleet fleet && fleet.CapitalShips.Count > 0)
                    fleet.CapitalShips[0].AddChild(officer);
            }
        }

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
