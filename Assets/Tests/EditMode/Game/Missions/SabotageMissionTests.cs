using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class SabotageMissionTests
    {
        private static Mission CreateSabotageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode specificTarget = null
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Sabotage,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                specificTarget
            );
        }

        [Test]
        public void Execute_BuildingOnEnemyPlanet_RemovesBuilding()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
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
        public void Execute_BuildingOnEnemyPlanet_ReturnsBuildingSabotagedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            Assert.IsTrue(
                results.OfType<GameObjectSabotagedResult>().Any(),
                "Sabotage success should return GameObjectSabotagedResult"
            );
        }

        [Test]
        public void Execute_BuildingOnEnemyPlanet_SetsSaboteurOnResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            GameObjectSabotagedResult sabotaged = results
                .OfType<GameObjectSabotagedResult>()
                .First();
            Assert.AreEqual(
                officer.InstanceID,
                sabotaged.Saboteur.InstanceID,
                "Saboteur should be the main participant"
            );
        }

        [Test]
        public void Execute_BuildingRemovedBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            game.DetachNode(building);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when all buildings removed before execution"
            );
        }

        [Test]
        public void Execute_SpecificBuildingTarget_RemovesSelectedBuilding()
        {
            (
                GameRoot game,
                Planet empPlanet,
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
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(enemyPlanet.InstanceID, mission.TargetInstanceID);
            Assert.AreEqual(
                selectedBuilding.InstanceID,
                ((SabotageMission)mission).SabotageTargetInstanceID
            );
            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>("b2"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
            Assert.AreEqual(
                selectedBuilding,
                results.OfType<GameObjectSabotagedResult>().Single().SabotagedObject
            );
        }

        [Test]
        public void TryCreate_OfficerTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
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
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new SabotageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Sabotage",
                DisplayName = "Sabotage",
                TargetInstanceID = "PLANET1",
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
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual("BUILDING1", ((SabotageMission)deserialized).SabotageTargetInstanceID);
            Assert.AreEqual(OfficerRating.Combat, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(6, deserialized.MaxProgress);
            Assert.AreEqual(4, deserialized.CurrentProgress);
        }
    }
}
