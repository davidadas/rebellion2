using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

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

        private static MissionStartRequest CreateRequest(
            string missionTypeID,
            string ownerInstanceID,
            IMissionParticipant participant,
            Planet target,
            IRandomNumberProvider provider = null,
            ResearchDiscipline? discipline = null
        )
        {
            return new MissionStartRequest
            {
                MissionTypeID = missionTypeID,
                OwnerInstanceID = ownerInstanceID,
                Target = target,
                MainParticipants = new List<IMissionParticipant> { participant },
                RandomProvider = provider,
                Discipline = discipline,
            };
        }

        [Test]
        public void TryCreateMission_ValidSabotageTarget_ReturnsTrue()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                CreateRequest(MissionTypeIDs.Sabotage, "empire", officer, planet),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<Mission>(mission);
        }

        [Test]
        public void TryCreateMission_DisallowedMissionTypeID_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            game.Factions.Find(f => f.InstanceID == "empire")
                .DisallowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);

            bool created = factory.TryCreateMission(
                CreateRequest(MissionTypeIDs.Sabotage, "empire", officer, planet),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_UnknownOwner_ReturnsFalse()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                CreateRequest(MissionTypeIDs.Sabotage, "unknown", officer, planet),
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
                CreateRequest(
                    MissionTypeIDs.Recruitment,
                    "empire",
                    officer,
                    planet,
                    provider: new StubRNG()
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<Mission>(mission);
        }

        [Test]
        public void TryCreateMission_RecruitmentWithoutProvider_ReturnsTrue()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            bool created = factory.TryCreateMission(
                CreateRequest(
                    MissionTypeIDs.Recruitment,
                    "empire",
                    officer,
                    planet,
                    provider: null
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<Mission>(mission);
        }

        [Test]
        public void TryCreateMission_RecruitmentNoUnrecruited_ReturnsFalse()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;

            bool created = factory.TryCreateMission(
                CreateRequest(
                    MissionTypeIDs.Recruitment,
                    "empire",
                    officer,
                    planet,
                    provider: new StubRNG()
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_ResearchWithDiscipline_BuildsResearchMissionWithMatchingDiscipline()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                CreateRequest(
                    MissionTypeIDs.Research,
                    "empire",
                    officer,
                    planet,
                    discipline: ResearchDiscipline.ShipDesign
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.IsInstanceOf<Mission>(mission);
            Assert.AreEqual(ResearchDiscipline.ShipDesign, ((Mission)mission).Discipline);
        }

        [Test]
        public void TryCreateMission_ResearchWithoutDiscipline_ReturnsFalse()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                CreateRequest(MissionTypeIDs.Research, "empire", officer, planet),
                out _
            );

            Assert.IsFalse(created);
        }
    }
}
