using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class ReconnaissanceMissionTests
    {
        private static Mission CreateMission(
            GameRoot game,
            string owner,
            Planet target,
            List<IMissionParticipant> main,
            List<IMissionParticipant> decoy
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Reconnaissance,
                game,
                owner,
                target,
                main,
                decoy
            );
        }

        private static SpecialForces CreateReconTeam(string owner)
        {
            return new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = owner,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance },
            };
        }

        [Test]
        public void Execute_UnvisitedPlanet_CapturesSnapshotWithoutSuccessRoll()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            SpecialForces reconTeam = CreateReconTeam("empire");
            game.AttachNode(reconTeam, empPlanet);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { reconTeam },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            List<GameResult> results = mission.Execute(game, new ThrowingRNG());

            Assert.IsTrue(enemyPlanet.WasVisitedBy("empire"));
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            Assert.IsTrue(empire.Fog.Snapshots.TryGetValue("sys1", out SystemSnapshot snapshot));
            Assert.IsTrue(snapshot.Planets.ContainsKey("enemy_planet"));

            GalaxyMap view = fog.BuildFactionView(empire);
            Planet viewPlanet = view
                .PlanetSystems.First(system => system.InstanceID == "sys1")
                .Planets.First(planet => planet.InstanceID == "enemy_planet");
            Assert.IsFalse(viewPlanet.IsUnexploredView);
        }

        [Test]
        public void UpdateMission_EnemyDefenderPresent_CompletesWithoutFoil()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            defender.SetBaseRating(OfficerRating.Espionage, 200);
            game.AttachNode(defender, enemyPlanet);

            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            SpecialForces reconTeam = CreateReconTeam("empire");
            game.AttachNode(reconTeam, empPlanet);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { reconTeam },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            MovementSystem movement = new MovementSystem(game, fog, new FleetSystem(game));
            movement.SendToMission(reconTeam, mission);
            reconTeam.Movement = null;
            mission.Initiate(1);

            MissionSystem system = TestSystems.CreateMissionSystem(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(enemyPlanet.WasVisitedBy("empire"));
            Assert.IsFalse(results.OfType<GameObjectDestroyedResult>().Any());
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreEqual(1, game.GetSceneNodesByType<SpecialForces>().Count);
        }

        [Test]
        public void TryCreate_NoParticipants_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant>(),
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_NullContext_ReturnsNull()
        {
            Assert.IsNull(ReconnaissanceMission.TryCreate(null));
        }

        [Test]
        public void TryCreate_VisitedPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.AddVisitor("empire");
            SpecialForces reconTeam = CreateReconTeam("empire");
            game.AttachNode(reconTeam, empPlanet);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { reconTeam },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_OfficerParticipantOnly_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_MixedPrimaryParticipants_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            SpecialForces reconTeam = CreateReconTeam("empire");
            game.AttachNode(reconTeam, empPlanet);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { reconTeam, officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new ReconnaissanceMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Reconnaissance",
                DisplayName = "Reconnaissance",
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Espionage,
                HasInitiated = true,
                MaxProgress = 10,
                CurrentProgress = 5,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Reconnaissance", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.LocationInstanceID);
            Assert.AreEqual(OfficerRating.Espionage, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(10, deserialized.MaxProgress);
            Assert.AreEqual(5, deserialized.CurrentProgress);
        }

        private class ThrowingRNG : IRandomNumberProvider
        {
            public double NextDouble()
            {
                throw new InvalidOperationException();
            }

            public int NextInt(int min, int max)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
