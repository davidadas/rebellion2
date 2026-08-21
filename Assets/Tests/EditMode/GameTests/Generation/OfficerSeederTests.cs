using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class OfficerSeederTests
    {
        private GameGenerationConfig _rules;
        private GameSummary _summary;

        [SetUp]
        public void SetUp()
        {
            _rules = new GameGenerationConfig
            {
                Officers = new OfficerSection
                {
                    NumStartingOfficers = new PlanetSizeProfile
                    {
                        Small = 2,
                        Medium = 3,
                        Large = 5,
                    },
                },
            };
            _summary = new GameSummary { GalaxySize = GameSize.Small };
        }

        [Test]
        public void Seed_WithRecruitableOfficer_IncludesInDeployed()
        {
            Officer officer = MakeOfficer("O1", "FNALL1", isRecruitable: true);
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { officer },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.Contains(officer, results.Deployed);
        }

        [Test]
        public void Seed_WithNonRecruitableOfficer_ExcludesFromDeployed()
        {
            Officer officer = MakeOfficer("O1", "FNALL1", isMain: false, isRecruitable: false);
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { officer },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.IsFalse(results.Deployed.Contains(officer));
        }

        [Test]
        public void Seed_WithGuaranteedOfficersExceedingLimit_DeploysAllGuaranteed()
        {
            Officer m1 = MakeOfficer("M1", "FNALL1", isMain: true);
            Officer m2 = MakeOfficer("M2", "FNALL1", isMain: true);
            Officer m3 = MakeOfficer("M3", "FNALL1", isMain: true);
            _rules.Officers.StartingOfficers.AddRange(
                new[]
                {
                    new StartingOfficerRule { OfficerInstanceID = m1.InstanceID },
                    new StartingOfficerRule { OfficerInstanceID = m2.InstanceID },
                    new StartingOfficerRule { OfficerInstanceID = m3.InstanceID },
                }
            );
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { m1, m2, m3 },
                new[] { sector },
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
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { officer1, officer2, officer3 },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(2, results.Deployed.Length);
        }

        [Test]
        public void Seed_StartingOfficerCount_IsTotalRatherThanAdditionalRecruitableCount()
        {
            Officer main = MakeOfficer("M1", "FNALL1", isMain: true);
            Officer recruitable1 = MakeOfficer("O1", "FNALL1");
            Officer recruitable2 = MakeOfficer("O2", "FNALL1");
            _rules.Officers.StartingOfficers.Add(
                new StartingOfficerRule { OfficerInstanceID = main.InstanceID }
            );
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { main, recruitable1, recruitable2 },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(2, results.Deployed.Length);
            Assert.Contains(main, results.Deployed);
        }

        [Test]
        public void Seed_GuaranteedStarter_IsIncludedWithoutBecomingMainCharacter()
        {
            _rules.Officers.NumStartingOfficers.Small = 1;
            _rules.Officers.StartingOfficers.Add(
                new StartingOfficerRule { OfficerInstanceID = "STARTER" }
            );
            Officer starter = MakeOfficer("STARTER", "FNALL1");
            Officer recruitable = MakeOfficer("RANDOM", "FNALL1");
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { starter, recruitable },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.AreEqual(1, results.Deployed.Length);
            Assert.Contains(starter, results.Deployed);
            Assert.IsFalse(starter.IsMain);
        }

        [Test]
        public void Seed_StartingOfficerRuleForDifferentGalaxySize_DoesNotGuaranteeOfficer()
        {
            _rules.Officers.NumStartingOfficers.Small = 0;
            _rules.Officers.StartingOfficers.Add(
                new StartingOfficerRule
                {
                    OfficerInstanceID = "LARGE_STARTER",
                    GalaxySizes = new List<GameSize> { GameSize.Large },
                }
            );
            Officer officer = MakeOfficer("LARGE_STARTER", "FNALL1");
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { officer },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.IsEmpty(results.Deployed);
        }

        [Test]
        public void Seed_OfficerWithAmbiguousAllowedFactions_IsExcluded()
        {
            Officer ambiguous = new Officer
            {
                InstanceID = "O1",
                OwnerInstanceID = null,
                RecruitingFactionInstanceIDs = new List<string> { "FNALL1", "FNEMP1" },
                IsRecruitable = true,
            };
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { ambiguous },
                new[] { sector },
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
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { officer1, officer2, officer3 },
                new[] { sector },
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
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            sector.AddChild(
                new Planet
                {
                    InstanceID = "p1",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                }
            );
            sector.AddChild(
                new Planet
                {
                    InstanceID = "p2",
                    OwnerInstanceID = "FNEMP1",
                    IsColonized = true,
                }
            );

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { allianceOfficer1, allianceOfficer2, empireOfficer1, empireOfficer2 },
                new[] { sector },
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
            officer.Ratings[OfficerRating.Diplomacy] = 10;
            officer.DiplomacyVariance = 0;
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            Deploy(new[] { officer }, new[] { sector }, _rules, _summary, new StubRNG());

            Assert.AreEqual(10, officer.Ratings[OfficerRating.Diplomacy]);
        }

        [Test]
        public void Seed_WithVariance_SkillsAtLeastBase()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            officer.Ratings[OfficerRating.Espionage] = 5;
            officer.EspionageVariance = 10;
            PlanetSector sector = MakeSector(("p1", "FNALL1"));

            Deploy(new[] { officer }, new[] { sector }, _rules, _summary, new StubRNG());

            Assert.GreaterOrEqual(officer.Ratings[OfficerRating.Espionage], 5);
        }

        [Test]
        public void Seed_WithOwnedPlanet_OfficerAddedToPlanet()
        {
            Officer officer = MakeOfficer("O1", "FNALL1");
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            sector.AddChild(planet);

            Deploy(new[] { officer }, new[] { sector }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, planet.GetChildren<Officer>().ToList());
        }

        [Test]
        public void Seed_WithStartingOfficerDestinationID_OfficerAddedToDesignatedPlanet()
        {
            Planet other = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            sector.AddChild(other);
            sector.AddChild(target);

            Officer officer = MakeOfficer("O1", "FNALL1");
            _rules.Officers.StartingOfficers.Add(
                new StartingOfficerRule
                {
                    OfficerInstanceID = officer.InstanceID,
                    DestinationInstanceID = "target",
                }
            );

            Deploy(new[] { officer }, new[] { sector }, _rules, _summary, new StubRNG());

            Assert.Contains(officer, target.GetChildren<Officer>().ToList());
            Assert.IsEmpty(other.GetChildren<Officer>());
        }

        [Test]
        public void Seed_WithStartingOfficerDestinationType_DeploysPinnedOfficerOutsideLimit()
        {
            _rules.Officers.NumStartingOfficers.Small = 0;

            Planet yavin = new Planet
            {
                InstanceID = "YAVIN",
                TypeID = "PLSUM06",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            Planet other = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            sector.AddChild(other);
            sector.AddChild(yavin);

            Officer pinned = MakeOfficer("CHEWBACCA", null);
            pinned.RecruitingFactionInstanceIDs = new List<string> { "FNALL1" };
            _rules.Officers.StartingOfficers.Add(
                new StartingOfficerRule
                {
                    OfficerInstanceID = pinned.InstanceID,
                    DestinationTypeID = "PLSUM06",
                }
            );
            Officer recruitable = MakeOfficer("O1", "FNALL1");

            (Officer[] Deployed, Officer[] Unrecruited) results = Deploy(
                new[] { recruitable, pinned },
                new[] { sector },
                _rules,
                _summary,
                new StubRNG()
            );

            Assert.Contains(pinned, results.Deployed);
            Assert.Contains(recruitable, results.Unrecruited);
            Assert.Contains(pinned, yavin.GetChildren<Officer>().ToList());
            Assert.IsEmpty(other.GetChildren<Officer>());
        }

        private static (Officer[] Deployed, Officer[] Unrecruited) Deploy(
            Officer[] officers,
            PlanetSector[] sectors,
            GameGenerationConfig config,
            GameSummary summary,
            IRandomNumberProvider rng
        )
        {
            GenerationContext ctx = new GenerationContext
            {
                Officers = officers,
                Sectors = sectors,
                Config = config,
                Summary = summary,
                Rng = rng,
            };
            new OfficerSeeder().Seed(ctx);
            return (ctx.DeployedOfficers, ctx.UnrecruitedOfficers);
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

        private PlanetSector MakeSector(params (string planetId, string ownerId)[] planets)
        {
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            foreach ((string planetId, string ownerId) in planets)
            {
                sector.AddChild(
                    new Planet
                    {
                        InstanceID = planetId,
                        OwnerInstanceID = ownerId,
                        IsColonized = true,
                    }
                );
            }
            return sector;
        }
    }
}
