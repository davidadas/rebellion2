using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class MissionFactoryTests
    {
        [Test]
        public void TryCreateMission_ValidSabotageTarget_ReturnsMissionWithMatchingConfigKey()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    SabotageMission.MissionTypeID,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.AreEqual(SabotageMission.MissionTypeID, mission.ConfigKey);
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
                    SabotageMission.MissionTypeID,
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
                OwnerInstanceID = "rebels",
                Movement = new MovementState(),
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.MoveNode(target, ship);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    SabotageMission.MissionTypeID,
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
            game.GetFactions()
                .Find(f => f.InstanceID == "empire")
                .DisallowedMissionTypeIDs.Add(SabotageMission.MissionTypeID);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    SabotageMission.MissionTypeID,
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
                    SabotageMission.MissionTypeID,
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
                    SabotageMission.MissionTypeID,
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
                SabotageMission.MissionTypeID,
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
                SabotageMission.MissionTypeID,
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
        public void TryCreateMission_InjuredPrimaryOfficer_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            officer.InjuryPoints = 1;

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    SabotageMission.MissionTypeID,
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
        public void TryCreateMission_InjuredDecoyOfficer_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.InjuryPoints = 1;
            game.AttachNode(decoy, planet);
            MissionContext context = CreateContext(
                game,
                SabotageMission.MissionTypeID,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );
            context.DecoyParticipants.Add(decoy);

            bool created = factory.TryCreateMission(context, out _);

            Assert.IsFalse(created);
        }

        [Test]
        public void TryCreateMission_DuplicatePrimaryParticipant_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            MissionContext context = CreateContext(
                game,
                SabotageMission.MissionTypeID,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );
            context.MainParticipants.Add(officer);

            Assert.IsFalse(factory.TryCreateMission(context, out _));
        }

        [Test]
        public void TryCreateMission_ParticipantUsedAsPrimaryAndDecoy_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Regiment target = CreateSabotageTarget(game, planet);
            MissionContext context = CreateContext(
                game,
                SabotageMission.MissionTypeID,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );
            context.DecoyParticipants.Add(officer);

            Assert.IsFalse(factory.TryCreateMission(context, out _));
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
            existingMission.AddChild(officer);
            game.MoveNode(officer, existingMission);

            bool created = factory.TryCreateMission(
                CreateContext(
                    game,
                    SabotageMission.MissionTypeID,
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
                    SabotageMission.MissionTypeID,
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
        public void TryCreateMission_ParticipantAboardMovingFleet_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            planet.AddVisitor("empire");
            Fleet fleet = EntityFactory.CreateFleet("moving-fleet", "empire");
            fleet.Movement = new MovementState { TransitTicks = 10 };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.MoveNode(officer, ship);

            bool created = factory.TryCreateMission(
                CreateContext(game, DiplomacyMission.MissionTypeID, "empire", officer, planet),
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
                SabotageMission.MissionTypeID,
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
            game.GetUnrecruitedOfficers().Add(CreateUnrecruitedOfficer("empire"));

            bool created = factory.TryCreateMission(
                CreateContext(game, RecruitmentMission.MissionTypeID, "empire", officer, planet),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.AreEqual(RecruitmentMission.MissionTypeID, mission.ConfigKey);
        }

        [Test]
        public void TryCreateMission_RecruitmentNoUnrecruited_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;

            bool created = factory.TryCreateMission(
                CreateContext(game, RecruitmentMission.MissionTypeID, "empire", officer, planet),
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
                    ResearchMission.MissionTypeID,
                    "empire",
                    officer,
                    planet,
                    discipline: ResearchDiscipline.ShipDesign
                ),
                out Mission mission
            );

            Assert.IsTrue(created);
            Assert.AreEqual(ResearchMission.MissionTypeID, mission.ConfigKey);
            Assert.AreEqual(ResearchDiscipline.ShipDesign, ((ResearchMission)mission).Discipline);
        }

        [Test]
        public void TryCreateMission_ResearchWithoutDiscipline_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();

            bool created = factory.TryCreateMission(
                CreateContext(game, ResearchMission.MissionTypeID, "empire", officer, planet),
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

            Assert.IsTrue(
                options.Any(option => option.MissionTypeID == DiplomacyMission.MissionTypeID)
            );
            Assert.AreEqual(EspionageMission.MissionTypeID, options.Last().MissionTypeID);
        }

        [Test]
        public void GetAvailableMissionOptions_CapitalShipTarget_ReturnsOnlyEntityMissions()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            game.AttachNode(fleet, planet);
            game.AttachNode(target, fleet);
            MissionContext context = CreateContext(
                game,
                null,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            Assert.IsTrue(
                options.Any(option => option.MissionTypeID == SabotageMission.MissionTypeID)
            );
            Assert.IsTrue(options.All(option => option.TargetKind != MissionTargetKind.Planet));
        }

        [Test]
        public void GetAvailableMissionOptions_SabotageUnitTargets_ReturnSabotage()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            planet.EnergyCapacity = 100;
            ISceneNode[] targets =
            {
                EntityFactory.CreateBuilding("building", "rebels"),
                EntityFactory.CreateRegiment("regiment", "rebels"),
                EntityFactory.CreateStarfighter("starfighter", "rebels"),
                new SpecialForces { InstanceID = "special-forces", OwnerInstanceID = "rebels" },
            };

            foreach (ISceneNode target in targets)
            {
                target.OwnerInstanceID = "empire";
                ((IManufacturable)target).ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(target, planet);
                target.OwnerInstanceID = "rebels";
                MissionContext context = CreateContext(
                    game,
                    null,
                    "empire",
                    officer,
                    planet,
                    selectedTarget: target
                );

                List<MissionOption> options = factory.GetAvailableMissionOptions(context);

                Assert.AreEqual(1, options.Count);
                Assert.AreEqual(SabotageMission.MissionTypeID, options[0].MissionTypeID);
            }
        }

        [Test]
        public void GetAvailableMissionOptions_EnemyOfficerTarget_ReturnsHostileOfficerMissions()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            Fleet fleet = EntityFactory.CreateFleet("target-fleet", "rebels");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "target-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(target, ship);
            MissionContext context = CreateContext(
                game,
                null,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            CollectionAssert.AreEquivalent(
                new[] { AbductionMission.MissionTypeID, AssassinationMission.MissionTypeID },
                options.Select(option => option.MissionTypeID)
            );
        }

        [Test]
        public void GetAvailableMissionOptions_FleetTarget_ReturnsNoMissions()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Fleet target = EntityFactory.CreateFleet("target", "rebels");
            game.AttachNode(target, planet);
            MissionContext context = CreateContext(
                game,
                null,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            Assert.IsEmpty(options);
        }

        [Test]
        public void GetAvailableMissionOptions_PlanetDestroyingCapitalShipTarget_ExcludesSabotage()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CanDestroyPlanets = true,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(target, fleet);
            MissionContext context = CreateContext(
                game,
                null,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == SabotageMission.MissionTypeID)
            );
        }

        [Test]
        public void GetAvailableMissionOptions_PlanetTarget_ReturnsOnlyPlanetMissions()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            planet.AddVisitor("empire");
            MissionContext context = CreateContext(game, null, "empire", officer, planet);

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            Assert.IsNotEmpty(options);
            Assert.IsTrue(options.All(option => option.TargetKind == MissionTargetKind.Planet));
        }

        [Test]
        public void GetAvailableMissionOptions_DisallowedEntityMission_ExcludesMission()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            game.GetFactions()[0].DisallowedMissionTypeIDs.Add(SabotageMission.MissionTypeID);
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            game.AttachNode(fleet, planet);
            game.AttachNode(target, fleet);
            MissionContext context = CreateContext(
                game,
                null,
                "empire",
                officer,
                planet,
                selectedTarget: target
            );

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == SabotageMission.MissionTypeID)
            );
        }

        [Test]
        public void GetAvailableMissionOptions_RecruitmentAndDiplomacy_ListsRecruitmentFirst()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.IsMain = true;
            game.GetUnrecruitedOfficers().Add(CreateUnrecruitedOfficer("empire"));
            planet.AddVisitor("empire");
            MissionContext context = CreateContext(game, null, "empire", officer, planet);

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            int recruitmentIndex = options.FindIndex(option =>
                option.MissionTypeID == RecruitmentMission.MissionTypeID
            );
            int diplomacyIndex = options.FindIndex(option =>
                option.MissionTypeID == DiplomacyMission.MissionTypeID
            );
            Assert.GreaterOrEqual(recruitmentIndex, 0);
            Assert.Greater(diplomacyIndex, recruitmentIndex);
        }

        [Test]
        public void GetAvailableMissionOptions_WithResearchAndDiplomacy_ListsResearchFirst()
        {
            (GameRoot game, Planet planet, Officer officer, MissionFactory factory) = BuildScene();
            officer.ShipResearch = 1;
            officer.TroopResearch = 1;
            officer.FacilityResearch = 1;
            planet.EnergyCapacity = 30;
            ManufacturingType[] facilityTypes =
            {
                ManufacturingType.Ship,
                ManufacturingType.Troop,
                ManufacturingType.Building,
            };
            foreach (ManufacturingType facilityType in facilityTypes)
            {
                game.AttachNode(
                    new Building
                    {
                        InstanceID = $"{facilityType}-facility",
                        OwnerInstanceID = "empire",
                        ProductionType = facilityType,
                        ProcessRate = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    planet
                );
            }
            planet.AddVisitor("empire");
            MissionContext context = CreateContext(game, null, "empire", officer, planet);

            List<MissionOption> options = factory.GetAvailableMissionOptions(context);

            int diplomacyIndex = options.FindIndex(option =>
                option.MissionTypeID == DiplomacyMission.MissionTypeID
            );
            List<int> researchIndexes = options
                .Select((option, index) => (option, index))
                .Where(entry => entry.option.MissionTypeID == ResearchMission.MissionTypeID)
                .Select(entry => entry.index)
                .ToList();
            Assert.IsNotEmpty(researchIndexes);
            Assert.IsTrue(researchIndexes.All(index => index < diplomacyIndex));
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet, Officer officer, MissionFactory factory) BuildScene()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);

            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, planetSector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, planet);

            MissionFactory factory = new MissionFactory(game);
            return (game, planet, officer, factory);
        }

        /// <summary>
        /// Creates unrecruited officer.
        /// </summary>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The created unrecruited officer.</returns>
        private static Officer CreateUnrecruitedOfficer(string factionId)
        {
            return new Officer
            {
                InstanceID = "ur1",
                DisplayName = "ur1",
                RecruitingFactionInstanceIDs = new List<string> { factionId },
            };
        }

        /// <summary>
        /// Creates sabotage target.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <returns>The created sabotage target.</returns>
        private static Regiment CreateSabotageTarget(GameRoot game, Planet planet)
        {
            Regiment target = EntityFactory.CreateRegiment("sabotage-target", "empire");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, planet);
            target.OwnerInstanceID = "rebels";
            return target;
        }

        /// <summary>
        /// Adds ship research facility.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
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

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="target">The target.</param>
        /// <param name="discipline">The discipline.</param>
        /// <param name="selectedTarget">The selected target.</param>
        /// <returns>The created context.</returns>
        private static MissionContext CreateContext(
            GameRoot game,
            string missionTypeId,
            string ownerInstanceId,
            IMissionParticipant participant,
            Planet target,
            ResearchDiscipline? discipline = null,
            ISceneNode selectedTarget = null
        )
        {
            return new MissionContext
            {
                Game = game,
                MissionTypeID = missionTypeId,
                OwnerInstanceId = ownerInstanceId,
                Location = target,
                MainParticipants = new List<IMissionParticipant> { participant },
                DecoyParticipants = new List<IMissionParticipant>(),
                Discipline = discipline,
                SelectedTarget = selectedTarget,
            };
        }
    }
}
