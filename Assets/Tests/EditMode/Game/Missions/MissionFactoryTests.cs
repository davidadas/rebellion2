using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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

        private static Regiment CreateSabotageTarget(GameRoot game, Planet planet)
        {
            Regiment target = EntityFactory.CreateRegiment("sabotage-target", "empire");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, planet);
            return target;
        }

        private static void AddShipResearchFacility(GameRoot game, Planet planet)
        {
            planet.EnergyCapacity = 10;
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        private static MissionContext CreateContext(
            GameRoot game,
            string missionTypeID,
            string ownerInstanceID,
            IMissionParticipant participant,
            Planet target,
            ResearchDiscipline? discipline = null,
            ISceneNode selectedTarget = null
        )
        {
            return new MissionContext
            {
                Game = game,
                MissionTypeID = missionTypeID,
                OwnerInstanceId = ownerInstanceID,
                Location = target,
                MainParticipants = new List<IMissionParticipant> { participant },
                DecoyParticipants = new List<IMissionParticipant>(),
                Discipline = discipline,
                SelectedTarget = selectedTarget,
            };
        }

        [Test]
        public void TryCreateMission_ValidSabotageTarget_ReturnsMissionWithMatchingConfigKey()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.AreEqual(MissionTypeIDs.Sabotage, mission.ConfigKey);
        }

        [Test]
        public void TryCreateMission_SabotageTargetUnderConstruction_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            target.ManufacturingStatus = ManufacturingStatus.Building;

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_SabotageTargetCarriedByMovingFleet_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            Fleet fleet = new Fleet
            {
                InstanceID = "moving-fleet",
                OwnerInstanceID = "empire",
                Movement = new MovementState(),
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.MoveNode(target, ship);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_DisallowedMissionTypeID_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            game.Factions.Find(f => f.InstanceID == "empire")
                .DisallowedMissionTypeIDs.Add(MissionTypeIDs.Sabotage);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_UnknownOwner_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "unknown",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_NullGame_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            factory = new MissionFactory(null);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_MixedPrimaryParticipantOwners_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            Officer rebelOfficer = EntityFactory.CreateOfficer("o2", "rebels");
            MissionContext context = CreateContext(
                game,
                MissionTypeIDs.Sabotage,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );
            context.MainParticipants.Add(rebelOfficer);

            bool created = factory.TryCreateMission(context, out _);

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_MixedDecoyParticipantOwner_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            Officer rebelDecoy = EntityFactory.CreateOfficer("o2", "rebels");
            MissionContext context = CreateContext(
                game,
                MissionTypeIDs.Sabotage,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );
            context.DecoyParticipants.Add(rebelDecoy);

            bool created = factory.TryCreateMission(context, out _);

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_ParticipantOnExistingMission_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            StubMission existingMission = EntityFactory.CreateMission(
                "existing-mission",
                "empire",
                planet.InstanceID
            );
            game.AttachNode(existingMission, planet);
            existingMission.MainParticipants.Add(officer);
            game.MoveNode(officer, existingMission);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_ParticipantInTransit_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            officer.Movement = new MovementState { TransitTicks = 10 };

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Sabotage,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_NullOptionalFields_DoesNotMutateContext()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            MissionContext context = CreateContext(
                game,
                MissionTypeIDs.Sabotage,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );
            context.Game = null;
            context.DecoyParticipants = null;

            bool created = factory.TryCreateMission(context, out _);

            Assert.IsTrue(created);
            Assert.IsNull(context.Game);
            Assert.IsNull(context.DecoyParticipants);
        }

        [Test]
        public void TryCreateMission_RecruitmentWithUnrecruited_ReturnsMissionWithMatchingConfigKey()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;
            game.UnrecruitedOfficers.Add(CreateUnrecruitedOfficer("empire"));

            bool created = factory.TryCreateMission(
                CreateContext(game, MissionTypeIDs.Recruitment, "empire", officer, planet),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.AreEqual(MissionTypeIDs.Recruitment, mission.ConfigKey);
        }

        [Test]
        public void TryCreateMission_RecruitmentNoUnrecruited_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;

            bool created = factory.TryCreateMission(
                CreateContext(game, MissionTypeIDs.Recruitment, "empire", officer, planet),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_ResearchWithDiscipline_ReturnsMissionWithMatchingDiscipline()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.ShipResearch = 1;
            AddShipResearchFacility(game, planet);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    MissionTypeIDs.Research,
                    "empire",
                    officer,
                    planet,
                    discipline: ResearchDiscipline.ShipDesign
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.AreEqual(MissionTypeIDs.Research, mission.ConfigKey);
            Assert.AreEqual(ResearchDiscipline.ShipDesign, ((ResearchMission)mission).Discipline);
        }

        [Test]
        public void TryCreateMission_ResearchWithoutDiscipline_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                CreateContext(game, MissionTypeIDs.Research, "empire", officer, planet),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void GetAvailableMissionOptions_MultipleOptions_ReturnsEspionageLast()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            planet.AddVisitor("empire");
            MissionContext context = CreateContext(game, null, "empire", officer, planet);

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            Assert.IsTrue(options.Any(option => option.MissionTypeID == MissionTypeIDs.Diplomacy));
            Assert.AreEqual(MissionTypeIDs.Espionage, options.Last().MissionTypeID);
        }
    }
}
