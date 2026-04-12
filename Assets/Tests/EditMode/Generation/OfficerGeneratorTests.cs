using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class OfficerGeneratorTests
    {
        private OfficerGenerator _generator;
        private GameGenerationRules _rules;
        private GameSummary _summary;

        [SetUp]
        public void SetUp()
        {
            _generator = new OfficerGenerator();
            _rules = new GameGenerationRules
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
        public void Deploy_WithRecruitableOfficer_IncludesInDeployed()
        {
            Officer officer = MakeOfficer("O1", "FNALL1", isRecruitable: true);
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            OfficerGenerator.OfficerResults results = _generator.Deploy(
                new[] { officer },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.Contains(officer, results.Deployed);
        }

        [Test]
        public void Deploy_WithNonRecruitableOfficer_ExcludesFromDeployed()
        {
            Officer officer = MakeOfficer("O1", "FNALL1", isMain: false, isRecruitable: false);
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            OfficerGenerator.OfficerResults results = _generator.Deploy(
                new[] { officer },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.IsFalse(results.Deployed.Contains(officer));
        }

        [Test]
        public void Deploy_WithMainOfficersExceedingLimit_DeploysAllMain()
        {
            Officer m1 = MakeOfficer("M1", "FNALL1", isMain: true);
            Officer m2 = MakeOfficer("M2", "FNALL1", isMain: true);
            Officer m3 = MakeOfficer("M3", "FNALL1", isMain: true);
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            OfficerGenerator.OfficerResults results = _generator.Deploy(
                new[] { m1, m2, m3 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(3, results.Deployed.Length);
        }

        [Test]
        public void Deploy_WithMoreRecruitableThanLimit_DeploysOnlyAllowed()
        {
            Officer officer1 = MakeOfficer("O1", "FNALL1");
            Officer officer2 = MakeOfficer("O2", "FNALL1");
            Officer officer3 = MakeOfficer("O3", "FNALL1");
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            OfficerGenerator.OfficerResults results = _generator.Deploy(
                new[] { officer1, officer2, officer3 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(2, results.Deployed.Length);
        }

        [Test]
        public void Deploy_OfficerWithAmbiguousAllowedFactions_IsExcluded()
        {
            Officer ambiguous = new Officer
            {
                InstanceID = "O1",
                OwnerInstanceID = null,
                AllowedOwnerInstanceIDs = new List<string> { "FNALL1", "FNEMP1" },
                IsRecruitable = true,
            };
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            OfficerGenerator.OfficerResults results = _generator.Deploy(
                new[] { ambiguous },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.IsEmpty(results.Deployed);
        }

        [Test]
        public void Deploy_UnrecruitedOfficers_AreComplementOfDeployed()
        {
            Officer officer1 = MakeOfficer("O1", "FNALL1");
            Officer officer2 = MakeOfficer("O2", "FNALL1");
            Officer officer3 = MakeOfficer("O3", "FNALL1");
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            OfficerGenerator.OfficerResults results = _generator.Deploy(
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
        public void Deploy_WithMultipleFactions_SelectsOfficersPerFactionIndependently()
        {
            Officer allianceOfficer1 = MakeOfficer("A1", "FNALL1");
            Officer allianceOfficer2 = MakeOfficer("A2", "FNALL1");
            Officer empireOfficer1 = MakeOfficer("E1", "FNEMP1");
            Officer empireOfficer2 = MakeOfficer("E2", "FNEMP1");
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            sys.Planets.Add(new Planet { InstanceID = "p1", OwnerInstanceID = "FNALL1" });
            sys.Planets.Add(new Planet { InstanceID = "p2", OwnerInstanceID = "FNEMP1" });

            OfficerGenerator.OfficerResults results = _generator.Deploy(
                new[] { allianceOfficer1, allianceOfficer2, empireOfficer1, empireOfficer2 },
                new[] { sys },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(4, results.Deployed.Length);
        }

        [Test]
        public void Deploy_WithZeroVariance_SkillsMatchBase()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.Skills[MissionParticipantSkill.Diplomacy] = 10;
            officer.DiplomacyVariance = 0;
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            _generator.Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.AreEqual(10, officer.Skills[MissionParticipantSkill.Diplomacy]);
        }

        [Test]
        public void Deploy_WithVariance_SkillsAtLeastBase()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.Skills[MissionParticipantSkill.Espionage] = 5;
            officer.EspionageVariance = 10;
            PlanetSystem sys = MakeSystem(("p1", "FNALL1"));

            _generator.Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.GreaterOrEqual(officer.Skills[MissionParticipantSkill.Espionage], 5);
        }

        [Test]
        public void Deploy_WithOwnedPlanet_OfficerAddedToPlanet()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            Planet planet = new Planet { InstanceID = "p1", OwnerInstanceID = "FNALL1" };
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            sys.Planets.Add(planet);

            _generator.Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, planet.Officers);
        }

        [Test]
        public void Deploy_WithInitialParentId_OfficerAddedToDesignatedPlanet()
        {
            Planet other = new Planet { InstanceID = "p1", OwnerInstanceID = "FNALL1" };
            Planet target = new Planet { InstanceID = "target", OwnerInstanceID = "FNALL1" };
            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            sys.Planets.Add(other);
            sys.Planets.Add(target);

            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.InitialParentInstanceID = "target";

            _generator.Deploy(new[] { officer }, new[] { sys }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, target.Officers);
            Assert.IsEmpty(other.Officers);
        }
    }
}
