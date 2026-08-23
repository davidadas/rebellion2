using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

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
                FogOfWarSystem fog
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
                FogOfWarSystem fog
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
        public void ResolveObjective_BuildingOnEnemyPlanet_RemovesBuilding()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
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
                FogOfWarSystem fog
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
        public void ResolveObjective_BuildingOnEnemyPlanet_SetsSaboteurOnResult()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
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
                FogOfWarSystem fog
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
        public void UpdateMission_BuildingRemovedBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
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

            MovementSystem movement = new MovementSystem(game, fog, new FleetSystem(game));
            MissionSystem missionSystem = TestSystems.CreateMissionSystem(
                game,
                new FixedRNG(0.0),
                movement
            );

            List<GameResult> results = missionSystem.UpdateMission(mission);

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
                FogOfWarSystem fog
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
        public void ResolveObjective_PlanetDestroyingShip_UsesDedicatedSuccessTable()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip deathStar = new CapitalShip
            {
                InstanceID = "death-star",
                TypeID = "CUSTOM_PLANET_DESTROYER",
                CanDestroyPlanets = true,
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(deathStar, fleet);
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int> { { 0, 0 } };
            game.Config.ProbabilityTables.Mission.DeathStarSabotage = new Dictionary<int, int>
            {
                { 0, 100 },
            };

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                deathStar
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.5));

            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreSame(
                deathStar,
                results.OfType<GameObjectSabotagedResult>().Single().DestroyedObject
            );
        }

        [Test]
        public void RollParticipantSuccess_UsesAverageOfEspionageAndCombat()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = EntityFactory.CreateRegiment("target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, enemyPlanet);
            officer.SetBaseRating(OfficerRating.Espionage, 20);
            officer.SetBaseRating(OfficerRating.Combat, 80);
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
        public void RollParticipantSuccess_PlanetDestroyingShipUsesAveragedScore()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip deathStar = new CapitalShip
            {
                InstanceID = "death-star",
                CanDestroyPlanets = true,
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, enemyPlanet);
            game.AttachNode(deathStar, fleet);
            officer.SetBaseRating(OfficerRating.Espionage, 20);
            officer.SetBaseRating(OfficerRating.Combat, 80);
            game.Config.ProbabilityTables.Mission.DeathStarSabotage = new Dictionary<int, int>
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
                deathStar
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
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = EntityFactory.CreateRegiment("target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, enemyPlanet);
            officer.SetBaseRating(OfficerRating.Espionage, 20);
            officer.SetBaseRating(OfficerRating.Combat, 80);
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
            Assert.AreEqual(21, officer.GetBaseRating(OfficerRating.Espionage));
            Assert.AreEqual(81, officer.GetBaseRating(OfficerRating.Combat));
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
                ParticipantRating = OfficerRating.Combat,
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
            Assert.AreEqual(OfficerRating.Combat, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(6, deserialized.MaxProgress);
            Assert.AreEqual(4, deserialized.CurrentProgress);
        }

        private static Mission CreateSabotageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode selectedTarget = null
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Sabotage,
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
