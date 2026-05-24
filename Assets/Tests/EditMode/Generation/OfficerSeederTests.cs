using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Generation;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class OfficerSeederTests
    {
        private GameGenerationConfig _rules;
        private GameSummary _summary;

        private static (Officer[] Deployed, Officer[] Unrecruited) Deploy(
            Officer[] officers,
            PlanetSystem[] systems,
            GameGenerationConfig config,
            GameSummary summary,
            IRandomNumberProvider rng
        )
        {
            GenerationContext ctx = new GenerationContext
            {
                Officers = officers,
                Systems = systems,
                Config = config,
                Summary = summary,
                Rng = rng,
            };
            new OfficerSeeder().Seed(ctx);
            return (ctx.DeployedOfficers, ctx.UnrecruitedOfficers);
        }

        [SetUp]
        public void SetUp()
        {
            _rules = new GameGenerationConfig
            {
                Officers = new OfficerSection
                {
                    NumInitialOfficers = new PlanetSizeProfile
                    {
                        Small = 2,
                        Medium = 3,
                        Large = 5,
                    },
                },
            };
            _summary = new GameSummary { GalaxySize = GameSize.Small };
        }

        private Officer MakeOfficer(
            string id,
            string factionId,
            bool isMain = false,
            bool isRecruitable = true
        )
        {
            return new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = factionId,
                IsMain = isMain,
                IsRecruitable = isRecruitable,
            };
        }

        private PlanetSystem MakeSystem(params (string planetId, string ownerId)[] planets)
        {
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            foreach ((string planetId, string ownerId) in planets)
            {
                sys.Planets.Add(new Planet { InstanceID = planetId, OwnerInstanceID = ownerId });
            }
            return sys;
        }

        [Test]
        public void Seed_WithRecruitableOfficer_IncludesInDeployed()
        {
            Officer officer = MakeOfficer("O1", "FNALL1", isRecruitable: true);
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            var results = Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, results.Deployed);
        }

        [Test]
        public void Seed_WithNonRecruitableOfficer_ExcludesFromDeployed()
        {
            Officer officer = MakeOfficer("O1", "FNALL1", isMain: false, isRecruitable: false);
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            var results = Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.IsFalse(results.Deployed.Contains(officer));
        }

        [Test]
        public void Seed_WithMainOfficersExceedingLimit_DeploysAllMain()
        {
            Officer m1 = MakeOfficer("M1", "FNALL1", isMain: true);
            Officer m2 = MakeOfficer("M2", "FNALL1", isMain: true);
            Officer m3 = MakeOfficer("M3", "FNALL1", isMain: true);
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            var results = Deploy(
                new[] { m1, m2, m3 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(3, results.Deployed.Length);
        }

        [Test]
        public void Seed_WithMoreRecruitableThanLimit_DeploysOnlyAllowed()
        {
            Officer officer1 = MakeOfficer("O1", "FNALL1");
            Officer officer2 = MakeOfficer("O2", "FNALL1");
            Officer officer3 = MakeOfficer("O3", "FNALL1");
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            var results = Deploy(
                new[] { officer1, officer2, officer3 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(2, results.Deployed.Length);
        }

        [Test]
        public void Seed_OfficerWithAmbiguousAllowedFactions_IsExcluded()
        {
            Officer ambiguous = new Officer
            {
                InstanceID = "O1",
                OwnerInstanceID = null,
                AllowedOwnerInstanceIDs = new List<string> { "FNALL1", "FNEMP1" },
                IsRecruitable = true,
            };
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            var results = Deploy(
                new[] { ambiguous },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.IsEmpty(results.Deployed);
        }

        [Test]
        public void Seed_UnrecruitedOfficers_AreComplementOfDeployed()
        {
            Officer officer1 = MakeOfficer("O1", "FNALL1");
            Officer officer2 = MakeOfficer("O2", "FNALL1");
            Officer officer3 = MakeOfficer("O3", "FNALL1");
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            var results = Deploy(
                new[] { officer1, officer2, officer3 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(1, results.Unrecruited.Length);
            Assert.IsEmpty(results.Deployed.Intersect(results.Unrecruited));
        }

        [Test]
        public void Seed_WithMultipleFactions_SelectsOfficersPerFactionIndependently()
        {
            Officer allianceOfficer1 = MakeOfficer("A1", "FNALL1");
            Officer allianceOfficer2 = MakeOfficer("A2", "FNALL1");
            Officer empireOfficer1 = MakeOfficer("E1", "FNEMP1");
            Officer empireOfficer2 = MakeOfficer("E2", "FNEMP1");
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            sys.Planets.Add(new Planet { InstanceID = "p1", OwnerInstanceID = "FNALL1" });
            sys.Planets.Add(new Planet { InstanceID = "p2", OwnerInstanceID = "FNEMP1" });

            var results = Deploy(
                new[] { allianceOfficer1, allianceOfficer2, empireOfficer1, empireOfficer2 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(4, results.Deployed.Length);
        }

        [Test]
        public void Seed_WithZeroVariance_SkillsMatchBase()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.Skills[MissionParticipantSkill.Diplomacy] = 10;
            officer.DiplomacyVariance = 0;
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.AreEqual(10, officer.Skills[MissionParticipantSkill.Diplomacy]);
        }

        [Test]
        public void Seed_WithVariance_SkillsAtLeastBase()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.Skills[MissionParticipantSkill.Espionage] = 5;
            officer.EspionageVariance = 10;
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.GreaterOrEqual(officer.Skills[MissionParticipantSkill.Espionage], 5);
        }

        [Test]
        public void Seed_WithOwnedPlanet_OfficerAddedToPlanet()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            Planet planet = new Planet { InstanceID = "p1", OwnerInstanceID = "FNALL1" };
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            sys.Planets.Add(planet);

            Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, planet.Officers);
        }

        [Test]
        public void Seed_WithInitialParentId_OfficerAddedToDesignatedPlanet()
        {
            Planet other = new Planet { InstanceID = "p1", OwnerInstanceID = "FNALL1" };
            Planet target = new Planet { InstanceID = "target", OwnerInstanceID = "FNALL1" };
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            sys.Planets.Add(other);
            sys.Planets.Add(target);

            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.InitialParentInstanceID = "target";

            Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, target.Officers);
            Assert.IsEmpty(other.Officers);
        }
    }
}
