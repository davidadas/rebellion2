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
        [Test]
        public void ResolveObjective_EnemyPlanetTarget_CapturesSnapshotForFaction()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            game.Config.Espionage.CoreSectorBonus = new GameConfig.RandomCountConfig();
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
                empire.Fog.Snapshots.ContainsKey("sector1"),
                "Espionage success should capture a FOW snapshot for the faction"
            );
            CollectionAssert.AreEquivalent(
                new[] { enemyPlanet.InstanceID },
                RevealedPlanetIDs(empire)
            );
        }

        [Test]
        public void ResolveObjective_EnemyPlanetTarget_CapturesCurrentPlanetContents()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
            PlanetSnapshot snapshot = empire.Fog.Snapshots["sector1"].Planets["enemy_planet"];
            Assert.IsTrue(snapshot.Buildings.Any(item => item.InstanceID == "enemy_building"));
        }

        [Test]
        public void ResolveObjective_EnemyPlanetTarget_RevealsEnemyMissions()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
            PlanetSnapshot snapshot = empire.Fog.Snapshots["sector1"].Planets["enemy_planet"];
            Assert.AreEqual(1, snapshot.Missions.Count);
            Assert.AreEqual(enemyMission.InstanceID, snapshot.Missions[0].InstanceID);
        }

        [Test]
        public void ResolveObjective_CoreTarget_RevealsSameAllegianceCorePlanets()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            PlanetSector targetSector = enemyPlanet.GetParentOfType<PlanetSector>();
            targetSector.SectorType = PlanetSectorType.Core;
            AddSector(game, "core2", "core_planet2", PlanetSectorType.Core);
            AddSector(game, "core3", "core_planet3", PlanetSectorType.Core);
            AddSector(game, "rim1", "rim_planet1", PlanetSectorType.OuterRim);
            game.Config.Espionage.CoreSectorBonus = new GameConfig.RandomCountConfig
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
        public void ResolveObjective_CoreTarget_ReportsEveryAdditionalSectorRevealed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            PlanetSector corellian = AddSector(
                game,
                "core2",
                "core_planet2",
                PlanetSectorType.Core
            );
            corellian.DisplayName = "Corellian";
            PlanetSector sluis = AddSector(game, "core3", "core_planet3", PlanetSectorType.Core);
            sluis.DisplayName = "Sluis";
            game.Config.Espionage.CoreSectorBonus = new GameConfig.RandomCountConfig { Base = 2 };
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

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            PlanetSectorsRevealedResult intelligence = results
                .OfType<PlanetSectorsRevealedResult>()
                .Single();
            Assert.AreEqual(mission.InstanceID, intelligence.MissionInstanceID);
            CollectionAssert.AreEquivalent(
                new[] { "Corellian", "Sluis" },
                intelligence.AdditionalSectors.Select(sector => sector.DisplayName)
            );
        }

        [Test]
        public void ResolveObjective_OuterRimTarget_DoesNotRevealBonusPlanets()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            AddSector(game, "core2", "core_planet2", PlanetSectorType.Core);
            game.Config.Espionage.CoreSectorBonus = new GameConfig.RandomCountConfig { Base = 10 };
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

        [TestCase("empire")]
        [TestCase(null)]
        public void ResolveObjective_NonEnemyCoreTarget_DoesNotRevealBonusPlanets(
            string targetOwnerId
        )
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            enemyPlanet.OwnerInstanceID = targetOwnerId;
            enemyPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            AddSector(game, "core2", "core_planet2", PlanetSectorType.Core);
            game.Config.Espionage.CoreSectorBonus = new GameConfig.RandomCountConfig { Base = 10 };
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
        public void ResolveObjective_MobileHeadquartersTarget_CanRevealCoreAndOuterRimPlanets()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            AddSector(game, "core2", "core_planet2", PlanetSectorType.Core);
            AddSector(game, "rim1", "rim_planet1", PlanetSectorType.OuterRim);
            AddSector(game, "neutral1", "neutral_planet1", PlanetSectorType.OuterRim, null);
            Faction rebels = game.GetFactionByOwnerInstanceID("rebels");
            rebels.HQInstanceID = enemyPlanet.InstanceID;
            rebels.Settings.Headquarters.IsMobile = true;
            game.Config.Espionage.HeadquartersBonus = new GameConfig.RandomCountConfig
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
        public void ResolveObjective_FixedHeadquartersTarget_CanRevealCoreAndOuterRimPlanets()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            AddSector(game, "core2", "core_planet2", PlanetSectorType.Core);
            AddSector(game, "rim1", "rim_planet1", PlanetSectorType.OuterRim);
            AddSector(game, "neutral1", "neutral_planet1", PlanetSectorType.OuterRim, null);
            Faction rebels = game.GetFactionByOwnerInstanceID("rebels");
            rebels.HQInstanceID = enemyPlanet.InstanceID;
            rebels.Settings.Headquarters.IsMobile = false;
            game.Config.Espionage.HeadquartersBonus = new GameConfig.RandomCountConfig
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
        public void ResolveObjective_WithoutFogSystem_DoesNotThrow()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
        public void ResolveObjective_PlanetBecameOwnedByMissionFaction_StillSucceeds()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Success,
                completed.Outcome,
                "Espionage should still succeed when planet changed ownership before execution"
            );
        }

        [Test]
        public void ResolveObjective_ForeignPlanetTarget_ImprovesSuccessfulOfficerEspionageRating()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
        public void ResolveObjective_MultipleOfficers_TriesNextOfficerWhenLowestScoreOfficerFails()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            officer.SetBaseRating(OfficerRating.Espionage, 0);
            Officer strongerOfficer = EntityFactory.CreateOfficer("o2", "empire");
            strongerOfficer.SetBaseRating(OfficerRating.Espionage, 100);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { strongerOfficer, officer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 0 },
                { 100, 100 },
            };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0));

            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void ResolveObjective_MultipleOfficersSucceed_ImprovesEverySuccessfulOfficer()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");
            officer.SetBaseRating(OfficerRating.Espionage, 50);
            Officer strongerOfficer = EntityFactory.CreateOfficer("o2", "empire");
            strongerOfficer.SetBaseRating(OfficerRating.Espionage, 100);
            int officerRatingBefore = officer.GetBaseRating(OfficerRating.Espionage);
            int strongerRatingBefore = strongerOfficer.GetBaseRating(OfficerRating.Espionage);

            Mission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { strongerOfficer, officer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 0 },
                { 50, 50 },
                { 100, 100 },
            };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(
                officerRatingBefore + 1,
                officer.GetBaseRating(OfficerRating.Espionage)
            );
            Assert.AreEqual(
                strongerRatingBefore + 1,
                strongerOfficer.GetBaseRating(OfficerRating.Espionage)
            );
        }

        [Test]
        public void ResolveObjective_DecoyParticipant_DoesNotImproveOfficerEspionageRating()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
        public void ResolveObjective_OwnPlanetTarget_DoesNotImproveOfficerEspionageRating()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empirePlanet.VisitingFactionIDs.Add("empire");
            int ratingBefore = officer.GetBaseRating(OfficerRating.Espionage);

            Mission mission = CreateMission(
                game,
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empirePlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(
                ratingBefore,
                officer.GetBaseRating(OfficerRating.Espionage),
                "Officer espionage rating should not improve on successful espionage against an owned planet"
            );
        }

        [Test]
        public void ResolveObjective_NeutralPlanetTarget_DoesNotImproveOfficerEspionageRating()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            enemyPlanet.OwnerInstanceID = null;
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

            Assert.AreEqual(ratingBefore, officer.GetBaseRating(OfficerRating.Espionage));
        }

        [Test]
        public void TryCreate_NotVisitedPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            // empirePlanet has no VisitingFactionIDs — empire has not visited it
            Mission mission = CreateMission(
                game,
                "empire",
                empirePlanet,
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
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empirePlanet.VisitingFactionIDs.Add("empire");

            Mission mission = CreateMission(
                game,
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNotNull(
                mission,
                "TryCreate should succeed for a visited planet regardless of ownership"
            );
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
                DetectionResolved = true,
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
            Assert.IsTrue(deserialized.DetectionResolved);
        }

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

        private static PlanetSector AddSector(
            GameRoot game,
            string sectorInstanceId,
            string planetInstanceId,
            PlanetSectorType sectorType,
            string ownerInstanceId = "rebels"
        )
        {
            PlanetSector sector = new PlanetSector
            {
                InstanceID = sectorInstanceId,
                SectorType = sectorType,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(
                new Planet
                {
                    InstanceID = planetInstanceId,
                    OwnerInstanceID = ownerInstanceId,
                    IsColonized = true,
                },
                sector
            );
            return sector;
        }

        private static List<string> RevealedPlanetIDs(Faction faction)
        {
            return faction
                .Fog.Snapshots.Values.SelectMany(snapshot => snapshot.Planets.Keys)
                .ToList();
        }
    }
}
