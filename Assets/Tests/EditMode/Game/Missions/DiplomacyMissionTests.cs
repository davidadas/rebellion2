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

        private static List<GameResult> InvokeOnSuccess(
            DiplomacyMission mission,
            GameRoot game,
            IRandomNumberProvider rng
        )
        {
            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            return (List<GameResult>)onSuccess.Invoke(mission, new object[] { game, rng });
        }

        [Test]
        public void Execute_SupportBelowThreshold_NoOwnershipChange()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            List<GameResult> results = InvokeOnSuccess(mission, game, new FixedRNG(0.0));

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "Should not emit ownership change when support <= 60"
            );
            Assert.AreEqual("empire", planet.OwnerInstanceID, "Owner should be unchanged");
        }

        [Test]
        public void Execute_SupportCrossesThreshold_NoOwnershipChangeEmitted()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 60, planetOwner: null);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            List<GameResult> results = InvokeOnSuccess(mission, game, new FixedRNG(0.0));

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "DiplomacyMission should not emit ownership change; PlanetaryControlSystem handles transfers"
            );
            Assert.AreEqual(
                62,
                planet.GetPopularSupport("empire"),
                "Support should still increment"
            );
        }

        [Test]
        public void Execute_PlanetAlreadyOwned_NoOwnershipChangeEmitted()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 61, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            List<GameResult> results = InvokeOnSuccess(mission, game, new FixedRNG(0.0));

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "Should not emit ownership change when planet is already owned by mission faction"
            );
        }

        [Test]
        public void OnSuccess_PlanetAlreadyOwned_IncrementsSupportWithoutChangingOwner()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 61, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            InvokeOnSuccess(mission, game, new FixedRNG(0.0));

            Assert.AreEqual(
                63,
                planet.GetPopularSupport("empire"),
                "Support should still increment"
            );
            Assert.AreEqual("empire", planet.OwnerInstanceID, "Owner should remain empire");
        }

        [Test]
        public void OnSuccess_SuccessProbabilityTable_DoesNotAffectSupportGain()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50, planetOwner: "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.SetBaseRating(OfficerRating.Diplomacy, 100);
            game.AttachNode(officer, planet);
            DiplomacyMission mission = CreateDiplomacyMission(
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 70 } }
            );
            game.Config.SupportShift.DiplomacyCompletionSupportBonus = 1;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportBase = 1;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportRange = 0;

            InvokeOnSuccess(mission, game, new FixedRNG(0.0));

            Assert.AreEqual(52, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void OnSuccess_OwnedPlanet_UsesDiplomacySupportConfig()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            game.Config.SupportShift.DiplomacyCompletionSupportBonus = 3;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportBase = 5;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportRange = 10;

            InvokeOnSuccess(mission, game, new SequenceRNG(new[] { 7 }));

            Assert.AreEqual(65, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void OnSuccess_NeutralPlanet_UsesNeutralDiplomacySupportConfig()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50, planetOwner: null);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            game.Config.SupportShift.DiplomacyCompletionSupportBonus = 3;
            game.Config.SupportShift.DiplomacyNeutralPlanetSupportBase = 2;
            game.Config.SupportShift.DiplomacyNeutralPlanetSupportRange = 4;

            InvokeOnSuccess(mission, game, new SequenceRNG(new[] { 4 }));

            Assert.AreEqual(59, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void OnSuccess_InvertSupportShift_SubtractsCompletionBonus()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50, planetOwner: "empire");
            game.GetFactionByOwnerInstanceID("empire").Settings.InvertSupportShift = true;
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            game.Config.SupportShift.DiplomacyCompletionSupportBonus = 1;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportBase = 1;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportRange = 0;

            InvokeOnSuccess(mission, game, new FixedRNG(0.0));

            Assert.AreEqual(50, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void Execute_SuccessProbability_UsesRegimentDefenseMinusOpposingSupportPlusDiplomacyRating()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 80, planetOwner: "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.SetBaseRating(OfficerRating.Diplomacy, 40);
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                UprisingDefense = 20,
            };
            game.AttachNode(officer, planet);
            game.AttachNode(regiment, planet);
            DiplomacyMission mission = CreateDiplomacyMission(
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int>
                {
                    { -20, 0 },
                    { 40, 100 },
                    { 41, 0 },
                }
            );
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
        }

        [Test]
        public void ShouldAbort_WhenUprisingStarts_ReturnsTrue()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            planet.BeginUprising();

            Assert.IsTrue(
                mission.ShouldAbort(game),
                "Diplomacy mission should be canceled when target planet enters uprising"
            );
        }

        [Test]
        public void ShouldAbort_WhenPlanetTakenByThirdFaction_ReturnsTrue()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50, planetOwner: null);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            planet.OwnerInstanceID = "rebels";

            Assert.IsTrue(
                mission.ShouldAbort(game),
                "Diplomacy mission should be canceled when target planet is taken by another faction"
            );
        }

        [Test]
        public void ShouldAbort_WhenPlanetTakenByMissionFaction_ReturnsFalse()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 70, planetOwner: null);
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            DiplomacyMission mission = CreateDiplomacyMission(
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);

            planet.OwnerInstanceID = "empire";

            Assert.IsFalse(
                mission.ShouldAbort(game),
                "Diplomacy mission should not abort when target planet joins the mission faction"
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_WhenPlanetTakenByMissionFactionBelowMaxSupport_ReturnsTrue()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 70, planetOwner: null);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            planet.OwnerInstanceID = "empire";

            Assert.IsTrue(
                mission.ShouldRepeatAfterCompletion(game),
                "Diplomacy mission should repeat below 100 support after target joins the mission faction"
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
        public void Execute_SupportAlreadyAtMax_ReturnsSuccess()
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

            planet.SetFullPopularSupport("empire");

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Success,
                completed.Outcome,
                "Mission succeeds even when support is already at max; ShouldRepeatAfterCompletion tears it down after"
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_SupportReachedMax_ReturnsFalse()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 99, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);
            planet.SetFullPopularSupport("empire");

            Assert.IsFalse(
                mission.ShouldRepeatAfterCompletion(game),
                "Mission should cancel when support is at 100"
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_SupportBelowMax_ReturnsTrue()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 99, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            Assert.IsTrue(
                mission.ShouldRepeatAfterCompletion(game),
                "Mission should repeat when support is below 100"
            );
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Diplomacy",
                DisplayName = "Diplomacy",
                TargetInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Diplomacy,
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
            Assert.AreEqual(OfficerRating.Diplomacy, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(12, deserialized.MaxProgress);
            Assert.AreEqual(3, deserialized.CurrentProgress);
        }
    }
}
