using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class MissionFactoryTests
    {
        private GameRoot _game;
        private Planet _ownPlanet;
        private Planet _enemyPlanet;
        private MissionFactory _factory;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestConfig.Create();
            _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });
            _game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);

            _ownPlanet = new Planet
            {
                InstanceID = "own_planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
                VisitingFactionIDs = new List<string> { "empire" },
            };
            _game.AttachNode(_ownPlanet, system);

            _enemyPlanet = new Planet
            {
                InstanceID = "enemy_planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "rebels", 60 } },
                VisitingFactionIDs = new List<string> { "empire", "rebels" },
            };
            _game.AttachNode(_enemyPlanet, system);

            _factory = new MissionFactory(_game);
        }

        [Test]
        public void CanCreateMission_Diplomacy_OwnPlanet_ReturnsTrue()
        {
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_Diplomacy_NeutralPlanet_ReturnsTrue()
        {
            _ownPlanet.OwnerInstanceID = null;
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_Diplomacy_EnemyPlanet_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.Diplomacy, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_Diplomacy_UncolonizedPlanet_ReturnsFalse()
        {
            _ownPlanet.IsColonized = false;
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_Diplomacy_MaxSupport_ReturnsFalse()
        {
            _game.SetPlanetPopularSupport(_ownPlanet, "empire", 100);
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_Diplomacy_PlanetInUprising_ReturnsFalse()
        {
            _ownPlanet.BeginUprising();
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_Diplomacy_NotVisited_ReturnsFalse()
        {
            _ownPlanet.VisitingFactionIDs.Clear();
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_SubdueUprising_OwnPlanetInUprising_ReturnsTrue()
        {
            _ownPlanet.BeginUprising();
            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.SubdueUprising, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_SubdueUprising_OwnPlanetNotInUprising_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.SubdueUprising, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_SubdueUprising_EnemyPlanet_ReturnsFalse()
        {
            _enemyPlanet.BeginUprising();
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.SubdueUprising, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_Espionage_EnemyPlanet_ReturnsTrue()
        {
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Espionage, "empire", _enemyPlanet));
        }

        [Test]
        public void CanCreateMission_Espionage_OwnPlanet_ReturnsFalse()
        {
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Espionage, "empire", _ownPlanet));
        }

        [Test]
        public void CanCreateMission_Espionage_NotVisited_ReturnsFalse()
        {
            _enemyPlanet.VisitingFactionIDs.Remove("empire");
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.Espionage, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_InciteUprising_EnemyPlanet_ReturnsTrue()
        {
            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.InciteUprising, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_InciteUprising_OwnPlanet_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.InciteUprising, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_InciteUprising_AlreadyInUprising_ReturnsFalse()
        {
            _enemyPlanet.BeginUprising();
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.InciteUprising, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_Research_OwnPlanet_ReturnsTrue()
        {
            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.ShipDesignResearch, "empire", _ownPlanet)
            );
            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.TroopTrainingResearch, "empire", _ownPlanet)
            );
            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.FacilityDesignResearch, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_Research_EnemyPlanet_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.ShipDesignResearch, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_JediTraining_OwnPlanetWithTrainer_ReturnsTrue()
        {
            Officer trainer = EntityFactory.CreateOfficer("trainer", "empire");
            trainer.IsJedi = true;
            trainer.IsJediTrainer = true;
            trainer.IsForceEligible = true;
            _game.AttachNode(trainer, _ownPlanet);

            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.JediTraining, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_JediTraining_OwnPlanetNoTrainer_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.JediTraining, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_JediTraining_EnemyPlanet_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.JediTraining, "empire", _enemyPlanet)
            );
        }

        [Test]
        public void CanCreateMission_Sabotage_AnyPlanet_ReturnsTrue()
        {
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Sabotage, "empire", _ownPlanet));
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Sabotage, "empire", _enemyPlanet));
        }

        [Test]
        public void CanCreateMission_Assassination_EnemyOfficerPresent_ReturnsTrue()
        {
            Officer enemy = EntityFactory.CreateOfficer("e1", "rebels");
            _game.AttachNode(enemy, _enemyPlanet);

            Assert.IsTrue(
                _factory.CanCreateMission(
                    MissionType.Assassination,
                    "empire",
                    _enemyPlanet,
                    new StubRNG()
                )
            );
        }

        [Test]
        public void CanCreateMission_Assassination_NoEnemyOfficer_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(
                    MissionType.Assassination,
                    "empire",
                    _enemyPlanet,
                    new StubRNG()
                )
            );
        }

        [Test]
        public void CanCreateMission_Rescue_CapturedOfficerPresent_ReturnsTrue()
        {
            Officer captured = EntityFactory.CreateOfficer("c1", "empire");
            captured.IsCaptured = true;
            _game.AttachNode(captured, _enemyPlanet);

            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.Rescue, "empire", _enemyPlanet, new StubRNG())
            );
        }

        [Test]
        public void CanCreateMission_Rescue_NoCapturedOfficer_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(MissionType.Rescue, "empire", _enemyPlanet, new StubRNG())
            );
        }

        [Test]
        public void CanCreateMission_NonPlanetTarget_ReturnsFalse()
        {
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _ownPlanet);

            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", officer));
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Espionage, "empire", officer));
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Sabotage, "empire", officer));
        }

        [Test]
        public void CreateMission_FactionDisallowedMissionType_ThrowsError()
        {
            Faction faction = _game.Factions.Find(f => f.InstanceID == "empire");
            faction.DisallowedMissionTypes.Add(MissionType.Sabotage);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _ownPlanet);

            Assert.Throws<InvalidOperationException>(() =>
                _factory.CreateMission(
                    MissionType.Sabotage,
                    "empire",
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    _ownPlanet
                )
            );
        }

        [Test]
        public void CreateMission_FactionAllowedMissionType_CreatesMission()
        {
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _ownPlanet);

            Mission mission = _factory.CreateMission(
                MissionType.Sabotage,
                "empire",
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                _ownPlanet
            );

            Assert.IsNotNull(mission);
            Assert.IsInstanceOf<SabotageMission>(mission);
        }

        [Test]
        public void CreateMission_SpecialForcesNotAllowedForMissionType_ThrowsError()
        {
            SpecialForces spec = new SpecialForces
            {
                InstanceID = "sf1",
                DisplayName = "TestSpec",
                AllowedMissionTypes = new List<MissionType> { MissionType.Espionage },
            };

            Assert.Throws<InvalidOperationException>(() =>
                _factory.CreateMission(
                    MissionType.Sabotage,
                    "empire",
                    new List<IMissionParticipant> { spec },
                    new List<IMissionParticipant>(),
                    _ownPlanet
                )
            );
        }

        [Test]
        public void CreateMission_SpecialForcesAllowedForMissionType_CreatesMission()
        {
            SpecialForces spec = new SpecialForces
            {
                InstanceID = "sf1",
                DisplayName = "TestSpec",
                AllowedMissionTypes = new List<MissionType> { MissionType.Sabotage },
            };

            Mission mission = _factory.CreateMission(
                MissionType.Sabotage,
                "empire",
                new List<IMissionParticipant> { spec },
                new List<IMissionParticipant>(),
                _ownPlanet
            );

            Assert.IsNotNull(mission);
            Assert.IsInstanceOf<SabotageMission>(mission);
        }

        [Test]
        public void CanPerformMission_Officer_ReturnsTrue()
        {
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _ownPlanet);

            Assert.IsTrue(officer.CanPerformMission(MissionType.Sabotage));
            Assert.IsTrue(officer.CanPerformMission(MissionType.Espionage));
            Assert.IsTrue(officer.CanPerformMission(MissionType.Assassination));
        }

        [Test]
        public void CanPerformMission_SpecialForces_RespectsAllowedList()
        {
            SpecialForces spec = new SpecialForces
            {
                InstanceID = "sf1",
                AllowedMissionTypes = new List<MissionType>
                {
                    MissionType.Espionage,
                    MissionType.Sabotage,
                },
            };

            Assert.IsTrue(spec.CanPerformMission(MissionType.Espionage));
            Assert.IsTrue(spec.CanPerformMission(MissionType.Sabotage));
            Assert.IsFalse(spec.CanPerformMission(MissionType.Assassination));
            Assert.IsFalse(spec.CanPerformMission(MissionType.Rescue));
        }

        [Test]
        public void CreateMission_DecoySpecialForcesNotAllowed_ThrowsError()
        {
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _ownPlanet);

            SpecialForces decoy = new SpecialForces
            {
                InstanceID = "sf1",
                DisplayName = "DecoySpec",
                AllowedMissionTypes = new List<MissionType> { MissionType.Espionage },
            };

            Assert.Throws<InvalidOperationException>(() =>
                _factory.CreateMission(
                    MissionType.Sabotage,
                    "empire",
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant> { decoy },
                    _ownPlanet
                )
            );
        }
    }
}
