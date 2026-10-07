using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class SabotageMissionTests
    {
        [Test]
        public void TryCreate_TargetCarriedByMovingFleet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            Regiment target = EntityFactory.CreateRegiment("target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
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
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(target, ship);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_OfficerTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            Officer targetOfficer = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(targetOfficer, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                targetOfficer
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_FriendlyTarget_ReturnsNull()
        {
            var (game, empirePlanet, _, officer, _) = MissionSceneBuilder.Build();
            Regiment target = EntityFactory.CreateRegiment("target", "empire");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, empirePlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_PlanetDestroyingCapitalShip_ReturnsNull()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CanDestroyPlanets = true,
            };
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(target, fleet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void ResolveObjective_BuildingOnEnemyPlanet_RemovesBuilding()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                building
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(
                0,
                enemyPlanet.GetAllBuildings().Count,
                "Building should be removed on sabotage success"
            );
        }

        [Test]
        public void ResolveObjective_BuildingOnEnemyPlanet_ReturnsBuildingSabotagedResult()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                building
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            Assert.IsTrue(
                results.OfType<GameObjectSabotagedResult>().Any(),
                "Sabotage success should return GameObjectSabotagedResult"
            );
        }

        [Test]
        public void ResolveObjective_CapitalShipWithOfficer_RestoresOfficerAtLocalPlanet()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer saboteur,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            PlanetSector sector = enemyPlanet.GetParentOfType<PlanetSector>();
            Planet fallback = new Planet
            {
                InstanceID = "rebel-fallback",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 125,
                PositionY = 0,
            };
            Fleet fleet = EntityFactory.CreateFleet("target-fleet", "rebels");
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
            };
            Officer carriedOfficer = EntityFactory.CreateOfficer("carried-officer", "rebels");
            game.AttachNode(fallback, sector);
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(target, fleet);
            game.AttachNode(carriedOfficer, target);
            MovementCommands movement = new MovementCommands(
                game,
                fog,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(game, movement, new MovementQueries(game)).Connect(resultBus);
            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { saboteur },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));
            resultBus.Publish(results);

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<CapitalShip>(target.InstanceID, includeDisabled: true)
            );
            Assert.AreSame(
                carriedOfficer,
                game.GetSceneNodeByInstanceID<Officer>(
                    carriedOfficer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(enemyPlanet, carriedOfficer.GetParent());
            Assert.AreNotSame(fallback, carriedOfficer.GetParent());
            Assert.IsNull(carriedOfficer.Movement);
        }

        [Test]
        public void ResolveObjective_CapitalShipWithInactiveOfficer_RelocatesWithoutActivating()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer saboteur,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            PlanetSector sector = enemyPlanet.GetParentOfType<PlanetSector>();
            Planet fallback = new Planet
            {
                InstanceID = "rebel-fallback",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 125,
                PositionY = 0,
            };
            Fleet fleet = EntityFactory.CreateFleet("target-fleet", "rebels");
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
            };
            Officer carriedOfficer = EntityFactory.CreateOfficer("carried-officer", "rebels");
            carriedOfficer.IsEnabled = false;
            game.AttachNode(fallback, sector);
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(target, fleet);
            game.AttachNode(carriedOfficer, target);
            MovementCommands movement = new MovementCommands(
                game,
                fog,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(game, movement, new MovementQueries(game)).Connect(resultBus);
            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { saboteur },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));
            resultBus.Publish(results);

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<CapitalShip>(target.InstanceID, includeDisabled: true)
            );
            Assert.AreSame(
                carriedOfficer,
                game.GetSceneNodeByInstanceID<Officer>(
                    carriedOfficer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(enemyPlanet, carriedOfficer.GetParent());
            Assert.AreNotSame(fallback, carriedOfficer.GetParent());
            Assert.IsNull(carriedOfficer.Movement);
            Assert.IsFalse(carriedOfficer.IsEnabled);
        }

        [Test]
        public void ResolveObjective_CapitalShipWithNonOfficerCargo_DestroysCargo()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer saboteur,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            Fleet fleet = EntityFactory.CreateFleet("target-fleet", "rebels");
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
                StarfighterCapacity = 1,
                RegimentCapacity = 1,
            };
            Starfighter fighter = EntityFactory.CreateStarfighter("carried-fighter", "rebels");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            Regiment regiment = EntityFactory.CreateRegiment("carried-regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(target, fleet);
            game.AttachNode(fighter, target);
            game.AttachNode(regiment, target);
            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { saboteur },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            mission.ResolveObjective(game, new FixedRNG(0.0));

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID, includeDisabled: true)
            );
        }

        [Test]
        public void ResolveObjective_CapitalShipTarget_DoesNotRefundMaterials()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer saboteur,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            Faction targetOwner = game.GetFactionByOwnerInstanceID("rebels");
            targetOwner.RefinedMaterialStockpile = 11;
            Fleet fleet = EntityFactory.CreateFleet("target-fleet", "rebels");
            CapitalShip target = new CapitalShip
            {
                InstanceID = "target-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
                ConstructionCost = 50,
            };
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(target, fleet);
            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { saboteur },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            mission.ResolveObjective(game, new FixedRNG(0.0));

            Assert.AreEqual(11, targetOwner.RefinedMaterialStockpile);
        }

        [Test]
        public void ResolveObjective_BuildingOnEnemyPlanet_SetsSaboteurOnResult()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                building
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            GameObjectSabotagedResult sabotaged = results
                .OfType<GameObjectSabotagedResult>()
                .First();
            Assert.AreEqual(
                officer.InstanceID,
                sabotaged.DestroyedBy.InstanceID,
                "Saboteur should be the main participant"
            );
        }

        [Test]
        public void ResolveObjective_SurfaceRegiment_ReturnsGarrisonChange()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                regiment
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.AreSame(
                enemyPlanet,
                results.OfType<PlanetGarrisonChangedResult>().Single().Planet
            );
        }

        [Test]
        public void ProcessTick_BuildingRemovedBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                building
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            game.DetachNode(building);

            MovementCommands movement = new MovementCommands(
                game,
                fog,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missionSystem = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.0),
                movement
            );

            List<GameResult> results = missionSystem.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when all buildings removed before execution"
            );
        }

        [Test]
        public void ResolveObjective_SpecificBuildingTarget_RemovesSelectedBuilding()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Building firstBuilding = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building selectedBuilding = new Building
            {
                InstanceID = "b2",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Refinery,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(firstBuilding, enemyPlanet);
            game.AttachNode(selectedBuilding, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                selectedBuilding
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            Assert.AreEqual(enemyPlanet.InstanceID, mission.LocationInstanceID);
            Assert.AreEqual(
                selectedBuilding.InstanceID,
                ((SabotageMission)mission).SabotageTargetInstanceID
            );
            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>("b2"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
            Assert.AreEqual(
                selectedBuilding,
                results.OfType<GameObjectSabotagedResult>().Single().DestroyedObject
            );
        }

        [Test]
        public void RollParticipantSuccess_Default_UsesAverageOfEspionageAndCombat()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            Regiment target = EntityFactory.CreateRegiment("target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, enemyPlanet);
            officer.SetBaseRating(SkillRating.Espionage, 20);
            officer.SetBaseRating(SkillRating.Combat, 80);
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int>
            {
                { 0, 0 },
                { 50, 100 },
                { 60, 0 },
            };
            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            bool succeeded = mission.RollParticipantSuccess(officer, new FixedRNG(0.5), game);

            Assert.IsTrue(succeeded);
        }

        [Test]
        public void ResolveObjective_SuccessfulOfficer_ImprovesEspionageAndCombatRatings()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            Regiment target = EntityFactory.CreateRegiment("target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, enemyPlanet);
            officer.SetBaseRating(SkillRating.Espionage, 20);
            officer.SetBaseRating(SkillRating.Combat, 80);
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int>
            {
                { 50, 100 },
            };
            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.5));

            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreEqual(21, officer.GetBaseRating(SkillRating.Espionage));
            Assert.AreEqual(81, officer.GetBaseRating(SkillRating.Combat));
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new SabotageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Sabotage",
                DisplayName = "Sabotage",
                LocationInstanceID = "PLANET1",
                SabotageTargetInstanceID = "BUILDING1",
                ParticipantRating = SkillRating.Combat,
                HasInitiated = true,
                MaxProgress = 6,
                CurrentProgress = 4,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Sabotage", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.LocationInstanceID);
            Assert.AreEqual("BUILDING1", ((SabotageMission)deserialized).SabotageTargetInstanceID);
            Assert.AreEqual(SkillRating.Combat, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(6, deserialized.MaxProgress);
            Assert.AreEqual(4, deserialized.CurrentProgress);
        }

        /// <summary>
        /// Creates sabotage mission.
        /// </summary>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="target">The target.</param>
        /// <param name="mainParticipants">The main participants.</param>
        /// <param name="decoyParticipants">The decoy participants.</param>
        /// <param name="selectedTarget">The selected target.</param>
        /// <returns>The created sabotage mission.</returns>
        private static Mission CreateSabotageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode selectedTarget = null
        )
        {
            return MissionTestFactory.TryCreate(
                SabotageMission.MissionTypeID,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                selectedTarget
            );
        }
    }
}
