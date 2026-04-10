using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class DiplomacyMissionTests
    {
        private static DiplomacyMission CreateDiplomacyMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            MissionContext ctx = new MissionContext
            {
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
            };
            return DiplomacyMission.TryCreate(ctx);
        }

        private GameRoot BuildGame(
            out Planet planet,
            int empireSupport,
            string planetOwner = "empire"
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);

            planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = planetOwner,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", empireSupport } },
                VisitingFactionIDs = new List<string> { "empire" },
            };
            game.AttachNode(planet, system);
            return game;
        }

        private DiplomacyMission CreateAndAttachMission(GameRoot game, Planet planet)
        {
            DiplomacyMission mission = CreateDiplomacyMission(
                "empire",
                planet,
                new List<IMissionParticipant>(),
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            return mission;
        }

        [Test]
        public void Execute_SupportBelowThreshold_NoOwnershipChange()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            List<GameResult> results =
                (List<GameResult>)
                    onSuccess.Invoke(mission, new object[] { game, new FixedRNG(0.0) });

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "Should not emit ownership change when support <= 60"
            );
            Assert.AreEqual("empire", planet.OwnerInstanceID, "Owner should be unchanged");
        }

        [Test]
        public void Execute_SupportCrossesThreshold_EmitsOwnershipChange()
        {
            // Support at 60 — one increment pushes it to 61, crossing the threshold
            GameRoot game = BuildGame(out Planet planet, empireSupport: 60, planetOwner: null);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            List<GameResult> results =
                (List<GameResult>)
                    onSuccess.Invoke(mission, new object[] { game, new FixedRNG(0.0) });

            PlanetOwnershipChangedResult ownershipResult = results
                .OfType<PlanetOwnershipChangedResult>()
                .SingleOrDefault();
            Assert.IsNotNull(ownershipResult, "Should emit PlanetOwnershipChangedResult");
            Assert.AreEqual("empire", ownershipResult.NewOwner.InstanceID);
            Assert.IsNull(ownershipResult.PreviousOwner);
        }

        [Test]
        public void Execute_PlanetAlreadyOwned_NoOwnershipChangeEmitted()
        {
            // Support already above threshold, planet already owned by empire
            GameRoot game = BuildGame(out Planet planet, empireSupport: 61, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            List<GameResult> results =
                (List<GameResult>)
                    onSuccess.Invoke(mission, new object[] { game, new FixedRNG(0.0) });

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "Should not emit ownership change when planet is already owned by mission faction"
            );
        }

        [Test]
        public void Execute_PlanetAlreadyOwned_NoRedundantChangeUnitOwnershipCall()
        {
            // Verify support still increments even when no ownership event fires
            GameRoot game = BuildGame(out Planet planet, empireSupport: 61, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            onSuccess.Invoke(mission, new object[] { game, new FixedRNG(0.0) });

            Assert.AreEqual(
                62,
                planet.GetPopularSupport("empire"),
                "Support should still increment"
            );
            Assert.AreEqual("empire", planet.OwnerInstanceID, "Owner should remain empire");
        }

        [Test]
        public void IsCanceled_WhenUprisingStarts_ReturnsTrue()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            planet.BeginUprising();

            Assert.IsTrue(
                mission.IsCanceled(game),
                "Diplomacy mission should be canceled when target planet enters uprising"
            );
        }

        [Test]
        public void TryCreate_UncolonizedPlanet_ReturnsNull()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            planet.IsColonized = false;

            Assert.IsNull(
                CreateDiplomacyMission(
                    "empire",
                    planet,
                    new List<IMissionParticipant>(),
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void TryCreate_PlanetSupportAtMax_ReturnsNull()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 100);

            Assert.IsNull(
                CreateDiplomacyMission(
                    "empire",
                    planet,
                    new List<IMissionParticipant>(),
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void TryCreate_EnemyOwnedPlanet_ReturnsNull()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50, planetOwner: "rebels");

            Assert.IsNull(
                CreateDiplomacyMission(
                    "empire",
                    planet,
                    new List<IMissionParticipant>(),
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void TryCreate_PlanetInUprising_ReturnsNull()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            planet.BeginUprising();

            Assert.IsNull(
                CreateDiplomacyMission(
                    "empire",
                    planet,
                    new List<IMissionParticipant>(),
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void Execute_SupportReachedMaxBeforeExecution_ReturnsFailed()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 99, planetOwner: "empire");

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, planet);

            DiplomacyMission mission = CreateDiplomacyMission(
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            mission.Initiate(new StubRNG());

            game.SetPlanetPopularSupport(planet, "empire", 100);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when support reaches 100 before execution"
            );
        }

        [Test]
        public void CanContinue_SupportReachedMax_ReturnsFalse()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 99, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            game.SetPlanetPopularSupport(planet, "empire", 100);

            Assert.IsFalse(
                mission.CanContinue(game),
                "Mission should cancel when support is at 100"
            );
        }

        [Test]
        public void CanContinue_SupportBelowMax_ReturnsTrue()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 99, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            Assert.IsTrue(
                mission.CanContinue(game),
                "Mission should continue when support is below 100"
            );
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Diplomacy",
                DisplayName = "Diplomacy",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Diplomacy,
                HasInitiated = true,
                MaxProgress = 12,
                CurrentProgress = 3,
            };

            string xml = SerializationHelper.Serialize(mission);
            DiplomacyMission deserialized = SerializationHelper.Deserialize<DiplomacyMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("FACTION1", deserialized.OwnerInstanceID);
            Assert.AreEqual("Diplomacy", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Diplomacy, deserialized.ParticipantSkill);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(12, deserialized.MaxProgress);
            Assert.AreEqual(3, deserialized.CurrentProgress);
        }
    }
}
