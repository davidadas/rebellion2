using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class MissionFactoryTests
    {
        private (GameRoot game, Planet planet, Officer officer, MissionFactory factory) BuildScene()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);

            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, planet);

            MissionFactory factory = new MissionFactory(game);
            return (game, planet, officer, factory);
        }

        private static Officer CreateUnrecruitedOfficer(string factionId)
        {
            return new Officer
            {
                InstanceID = "ur1",
                DisplayName = "ur1",
                AllowedOwnerInstanceIDs = new List<string> { factionId },
            };
        }

        [Test]
        public void TryCreateMission_ValidSabotageTarget_ReturnsTrue()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                SabotageMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<SabotageMission>(mission);
        }

        [Test]
        public void TryCreateMission_DisallowedMissionTypeID_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            game.Factions.Find(f => f.InstanceID == "empire")
                .DisallowedMissionTypeIDs.Add(SabotageMission.MissionTypeID);

            bool created = factory.TryCreateMission(
                SabotageMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_UnknownOwner_ReturnsFalse()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                SabotageMission.MissionTypeID,
                "unknown",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_RecruitmentWithProviderAndUnrecruited_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            bool created = factory.TryCreateMission(
                RecruitmentMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out Mission mission,
                provider: new StubRNG()
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<RecruitmentMission>(mission);
        }

        [Test]
        public void TryCreateMission_RecruitmentWithoutProvider_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            bool created = factory.TryCreateMission(
                RecruitmentMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out Mission mission,
                provider: null
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<RecruitmentMission>(mission);
        }

        [Test]
        public void TryCreateMission_RecruitmentNoUnrecruited_ReturnsFalse()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;

            bool created = factory.TryCreateMission(
                RecruitmentMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out _,
                provider: new StubRNG()
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_ResearchWithDiscipline_BuildsResearchMissionWithMatchingDiscipline()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                ResearchMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out Mission mission,
                discipline: ResearchDiscipline.ShipDesign
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<ResearchMission>(mission);
            Assert.AreEqual(ResearchDiscipline.ShipDesign, ((ResearchMission)mission).Discipline);
        }

        [Test]
        public void TryCreateMission_ResearchWithoutDiscipline_ReturnsFalse()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                ResearchMission.MissionTypeID,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                out _
            );

            Assert.IsFalse(created);
        }
    }
}
