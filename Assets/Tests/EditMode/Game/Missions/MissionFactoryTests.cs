using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Systems;

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
        public void CanCreateMission_ValidSabotageTarget_ReturnsTrue()
        {
            (_, Planet planet, _, MissionFactory factory) = BuildScene();

            Assert.IsTrue(
                factory.CanCreateMission(MissionType.Sabotage, "empire", planet, new StubRNG())
            );
        }

        [Test]
        public void CanCreateMission_DisallowedMissionType_ReturnsFalse()
        {
            (GameRoot game, Planet planet, _, MissionFactory factory) = BuildScene();
            game.Factions.Find(f => f.InstanceID == "empire")
                .DisallowedMissionTypes.Add(MissionType.Sabotage);

            Assert.IsFalse(
                factory.CanCreateMission(MissionType.Sabotage, "empire", planet, new StubRNG())
            );
        }

        [Test]
        public void CanCreateMission_RecruitmentWithProviderAndUnrecruited_ReturnsTrue()
        {
            (GameRoot game, Planet planet, _, MissionFactory factory) = BuildScene();
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            Assert.IsTrue(
                factory.CanCreateMission(
                    MissionType.Recruitment,
                    "empire",
                    planet,
                    provider: new StubRNG()
                )
            );
        }

        [Test]
        public void CanCreateMission_RecruitmentWithoutProvider_ReturnsFalse()
        {
            (GameRoot game, Planet planet, _, MissionFactory factory) = BuildScene();
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            Assert.IsFalse(
                factory.CanCreateMission(MissionType.Recruitment, "empire", planet, provider: null)
            );
        }

        [Test]
        public void CanCreateMission_RecruitmentNoUnrecruited_ReturnsFalse()
        {
            (_, Planet planet, _, MissionFactory factory) = BuildScene();

            Assert.IsFalse(
                factory.CanCreateMission(
                    MissionType.Recruitment,
                    "empire",
                    planet,
                    provider: new StubRNG()
                )
            );
        }

        [Test]
        public void CreateMission_DisallowedMissionType_Throws()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            game.Factions.Find(f => f.InstanceID == "empire")
                .DisallowedMissionTypes.Add(MissionType.Sabotage);

            Assert.Throws<InvalidOperationException>(() =>
                factory.CreateMission(
                    MissionType.Sabotage,
                    "empire",
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    planet
                )
            );
        }

        [Test]
        public void CreateMission_ValidSabotage_ReturnsMission()
        {
            (_, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            Mission mission = factory.CreateMission(
                MissionType.Sabotage,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet
            );

            Assert.IsNotNull(mission);
            Assert.IsInstanceOf<SabotageMission>(mission);
        }

        [Test]
        public void CreateMission_RecruitmentWithProvider_ReturnsMission()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            Mission mission = factory.CreateMission(
                MissionType.Recruitment,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                planet,
                provider: new StubRNG()
            );

            Assert.IsNotNull(mission);
            Assert.IsInstanceOf<RecruitmentMission>(mission);
        }
    }
}
