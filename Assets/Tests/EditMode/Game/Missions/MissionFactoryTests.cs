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

        // --- Diplomacy ---

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
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", _enemyPlanet));
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

        // --- SubdueUprising ---

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

        // --- Espionage ---

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
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Espionage, "empire", _enemyPlanet));
        }

        // --- InciteUprising ---

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

        // --- Research ---

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

        // --- JediTraining ---

        [Test]
        public void CanCreateMission_JediTraining_OwnPlanetWithTeacher_ReturnsTrue()
        {
            Officer teacher = EntityFactory.CreateOfficer("teacher", "empire");
            teacher.IsJedi = true;
            teacher.IsJediTeacher = true;
            teacher.IsForceEligible = true;
            _game.AttachNode(teacher, _ownPlanet);

            Assert.IsTrue(
                _factory.CanCreateMission(MissionType.JediTraining, "empire", _ownPlanet)
            );
        }

        [Test]
        public void CanCreateMission_JediTraining_OwnPlanetNoTeacher_ReturnsFalse()
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

        // --- Sabotage ---

        [Test]
        public void CanCreateMission_Sabotage_AnyPlanet_ReturnsTrue()
        {
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Sabotage, "empire", _ownPlanet));
            Assert.IsTrue(_factory.CanCreateMission(MissionType.Sabotage, "empire", _enemyPlanet));
        }

        // --- Target selection missions ---

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
                _factory.CanCreateMission(
                    MissionType.Rescue,
                    "empire",
                    _enemyPlanet,
                    new StubRNG()
                )
            );
        }

        [Test]
        public void CanCreateMission_Rescue_NoCapturedOfficer_ReturnsFalse()
        {
            Assert.IsFalse(
                _factory.CanCreateMission(
                    MissionType.Rescue,
                    "empire",
                    _enemyPlanet,
                    new StubRNG()
                )
            );
        }

        // --- Non-planet target ---

        [Test]
        public void CanCreateMission_NonPlanetTarget_ReturnsFalse()
        {
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _ownPlanet);

            Assert.IsFalse(_factory.CanCreateMission(MissionType.Diplomacy, "empire", officer));
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Espionage, "empire", officer));
            Assert.IsFalse(_factory.CanCreateMission(MissionType.Sabotage, "empire", officer));
        }
    }
}
