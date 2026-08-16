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
    public class EspionageMissionTests
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
                MissionTypeIDs.Espionage,
                game,
                owner,
                target,
                main,
                decoy
            );
        }

        [Test]
        public void Execute_EnemyPlanetTarget_CapturesSnapshotForFaction()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            game.Config.Espionage.CoreSystemBonus = new GameConfig.RandomCountConfig();
            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            Assert.IsTrue(
                empire.Fog.Snapshots.ContainsKey("sys1"),
                "Espionage success should capture a FOW snapshot for the faction"
            );
            CollectionAssert.AreEquivalent(
                new[] { enemyPlanet.InstanceID },
                RevealedPlanetIDs(empire)
            );
        }

        [Test]
        public void Execute_EnemyPlanetTarget_CapturesCurrentPlanetContents()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            Building building = new Building
            {
                InstanceID = "enemy_building",
                OwnerInstanceID = "rebels",
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            PlanetSnapshot snapshot = empire.Fog.Snapshots["sys1"].Planets["enemy_planet"];
            Assert.IsTrue(snapshot.Buildings.Any(item => item.InstanceID == "enemy_building"));
        }

        [Test]
        public void Execute_EnemyPlanetTarget_RevealsEnemyMissions()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            Mission enemyMission = EntityFactory.CreateMission(
                "enemy_mission",
                "rebels",
                enemyPlanet.InstanceID
            );
            game.AttachNode(enemyMission, enemyPlanet);

            Mission espionageMission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(espionageMission, enemyPlanet);
            espionageMission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(espionageMission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            PlanetSnapshot snapshot = empire.Fog.Snapshots["sys1"].Planets["enemy_planet"];
            Assert.AreEqual(1, snapshot.Missions.Count);
            Assert.AreEqual(enemyMission.InstanceID, snapshot.Missions[0].InstanceID);
        }

        [Test]
        public void Execute_CoreTarget_RevealsSameAllegianceCorePlanets()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            PlanetSystem targetSystem = enemyPlanet.GetParentOfType<PlanetSystem>();
            targetSystem.SystemType = PlanetSystemType.CoreSystem;
            AddSystem(game, "core2", "core_planet2", PlanetSystemType.CoreSystem);
            AddSystem(game, "core3", "core_planet3", PlanetSystemType.CoreSystem);
            AddSystem(game, "rim1", "rim_planet1", PlanetSystemType.OuterRim);
            game.Config.Espionage.CoreSystemBonus = new GameConfig.RandomCountConfig
            {
                Base = 2,
                Spread = 0,
            };
            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            CollectionAssert.AreEquivalent(
                new[] { "enemy_planet", "core_planet2", "core_planet3" },
                RevealedPlanetIDs(empire)
            );
        }

        [Test]
        public void Execute_CoreTarget_ReportsEveryAdditionalSystemRevealed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.CoreSystem;
            PlanetSystem corellia = AddSystem(
                game,
                "core2",
                "core_planet2",
                PlanetSystemType.CoreSystem
            );
            corellia.DisplayName = "Corellia";
            PlanetSystem sullust = AddSystem(
                game,
                "core3",
                "core_planet3",
                PlanetSystemType.CoreSystem
            );
            sullust.DisplayName = "Sullust";
            game.Config.Espionage.CoreSystemBonus = new GameConfig.RandomCountConfig { Base = 2 };
            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
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

            SystemsRevealedResult intelligence = results.OfType<SystemsRevealedResult>().Single();
            Assert.AreEqual(mission.InstanceID, intelligence.MissionInstanceID);
            CollectionAssert.AreEquivalent(
                new[] { "Corellia", "Sullust" },
                intelligence.AdditionalSystems.Select(system => system.DisplayName)
            );
        }

        [Test]
        public void Execute_OuterRimTarget_DoesNotRevealBonusPlanets()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.OuterRim;
            AddSystem(game, "core2", "core_planet2", PlanetSystemType.CoreSystem);
            game.Config.Espionage.CoreSystemBonus = new GameConfig.RandomCountConfig { Base = 10 };
            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            CollectionAssert.AreEquivalent(new[] { "enemy_planet" }, RevealedPlanetIDs(empire));
        }

        [Test]
        public void Execute_MobileHeadquartersTarget_CanRevealCoreAndOuterRimPlanets()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.CoreSystem;
            AddSystem(game, "core2", "core_planet2", PlanetSystemType.CoreSystem);
            AddSystem(game, "rim1", "rim_planet1", PlanetSystemType.OuterRim);
            AddSystem(game, "neutral1", "neutral_planet1", PlanetSystemType.OuterRim, null);
            Faction rebels = game.GetFactionByOwnerInstanceID("rebels");
            rebels.HQInstanceID = enemyPlanet.InstanceID;
            rebels.Settings.Headquarters.IsMobile = true;
            game.Config.Espionage.MobileHeadquartersBonus = new GameConfig.RandomCountConfig
            {
                Base = 10,
            };
            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            CollectionAssert.AreEquivalent(
                new[] { "enemy_planet", "core_planet2", "rim_planet1" },
                RevealedPlanetIDs(empire)
            );
        }

        [Test]
        public void Execute_CapitalTarget_CanRevealCoreAndOuterRimPlanets()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.CoreSystem;
            AddSystem(game, "rim1", "rim_planet1", PlanetSystemType.OuterRim);
            game.Config.Espionage.CapitalPlanetInstanceID = enemyPlanet.InstanceID;
            game.Config.Espionage.CapitalObserverFactionInstanceID = "empire";
            game.Config.Espionage.CapitalBonus = new GameConfig.RandomCountConfig { Base = 10 };
            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            CollectionAssert.Contains(RevealedPlanetIDs(empire), "rim_planet1");
        }

        [Test]
        public void Execute_WithoutFogSystem_DoesNotThrow()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            Assert.DoesNotThrow(() => MissionSceneBuilder.RunToSuccess(mission, game));
        }

        [Test]
        public void Execute_PlanetBecameOwnedByMissionFaction_StillSucceeds()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            // Planet changes hands before execution — espionage is still valid on any visited planet
            enemyPlanet.OwnerInstanceID = "empire";

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Success,
                completed.Outcome,
                "Espionage should still succeed when planet changed ownership before execution"
            );
        }

        [Test]
        public void Execute_ForeignPlanetTarget_ImprovesSuccessfulOfficerEspionageRating()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            int ratingBefore = officer.GetBaseRating(OfficerRating.Espionage);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(
                ratingBefore + 1,
                officer.GetBaseRating(OfficerRating.Espionage),
                "Officer espionage rating should improve on successful espionage against another faction"
            );
        }

        [Test]
        public void Execute_MultipleParticipants_ImprovesOnlySuccessfulOfficers()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            officer.SetBaseRating(OfficerRating.Espionage, 0);
            Officer successfulOfficer = EntityFactory.CreateOfficer("o2", "empire");
            successfulOfficer.SetBaseRating(OfficerRating.Espionage, 100);
            int failedRatingBefore = officer.GetBaseRating(OfficerRating.Espionage);
            int successfulRatingBefore = successfulOfficer.GetBaseRating(OfficerRating.Espionage);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer, successfulOfficer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 0 },
                { 100, 100 },
            };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(failedRatingBefore, officer.GetBaseRating(OfficerRating.Espionage));
            Assert.AreEqual(
                successfulRatingBefore + 1,
                successfulOfficer.GetBaseRating(OfficerRating.Espionage)
            );
        }

        [Test]
        public void Execute_DecoyParticipant_DoesNotImproveOfficerEspionageRating()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            int decoyRatingBefore = decoy.GetBaseRating(OfficerRating.Espionage);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant> { decoy }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(
                decoyRatingBefore,
                decoy.GetBaseRating(OfficerRating.Espionage),
                "Decoys should not improve from successful espionage execution"
            );
        }

        [Test]
        public void Execute_OwnPlanetTarget_DoesNotImproveOfficerEspionageRating()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.VisitingFactionIDs.Add("empire");
            int ratingBefore = officer.GetBaseRating(OfficerRating.Espionage);

            Mission mission = CreateMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(
                ratingBefore,
                officer.GetBaseRating(OfficerRating.Espionage),
                "Officer espionage rating should not improve on successful espionage against an owned planet"
            );
        }

        [Test]
        public void TryCreate_NotVisitedPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            // empPlanet has no VisitingFactionIDs — empire has not visited it
            Mission mission = CreateMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission, "TryCreate should return null when planet has not been visited");
        }

        [Test]
        public void TryCreate_VisitedOwnPlanet_ReturnsNotNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNotNull(
                mission,
                "TryCreate should succeed for a visited planet regardless of ownership"
            );
        }

        private static PlanetSystem AddSystem(
            GameRoot game,
            string systemInstanceID,
            string planetInstanceID,
            PlanetSystemType systemType,
            string ownerInstanceID = "rebels"
        )
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = systemInstanceID,
                SystemType = systemType,
            };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(
                new Planet
                {
                    InstanceID = planetInstanceID,
                    OwnerInstanceID = ownerInstanceID,
                    IsColonized = true,
                },
                system
            );
            return system;
        }

        private static List<string> RevealedPlanetIDs(Faction faction)
        {
            return faction
                .Fog.Snapshots.Values.SelectMany(snapshot => snapshot.Planets.Keys)
                .ToList();
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new EspionageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Espionage",
                DisplayName = "Espionage",
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Espionage,
                HasInitiated = true,
                MaxProgress = 10,
                CurrentProgress = 5,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Espionage", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.LocationInstanceID);
            Assert.AreEqual(OfficerRating.Espionage, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(10, deserialized.MaxProgress);
            Assert.AreEqual(5, deserialized.CurrentProgress);
        }
    }
}
