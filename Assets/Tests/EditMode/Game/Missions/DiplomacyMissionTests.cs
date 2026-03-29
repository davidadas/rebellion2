using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class DiplomacyMissionTests
    {
        private GameRoot BuildGame(
            out Planet planet,
            int empireSupport,
            string planetOwner = "empire"
        )
        {
            GameConfig config = new GameConfig();
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
            };
            game.AttachNode(planet, system);
            return game;
        }

        private DiplomacyMission CreateAndAttachMission(GameRoot game, Planet planet)
        {
            DiplomacyMission mission = new DiplomacyMission(
                "empire",
                planet,
                new List<IMissionParticipant>(),
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            return mission;
        }

        [Test]
        public void OnSuccess_SupportBelowThreshold_NoOwnershipChange()
        {
            GameRoot game = BuildGame(out Planet planet, empireSupport: 50);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            List<GameResult> results =
                (List<GameResult>)onSuccess.Invoke(mission, new object[] { game });

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "Should not emit ownership change when support <= 60"
            );
            Assert.AreEqual("empire", planet.OwnerInstanceID, "Owner should be unchanged");
        }

        [Test]
        public void OnSuccess_SupportCrossesThreshold_EmitsOwnershipChange()
        {
            // Support at 60 — one increment pushes it to 61, crossing the threshold
            GameRoot game = BuildGame(out Planet planet, empireSupport: 60, planetOwner: null);
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            List<GameResult> results =
                (List<GameResult>)onSuccess.Invoke(mission, new object[] { game });

            PlanetOwnershipChangedResult ownershipResult = results
                .OfType<PlanetOwnershipChangedResult>()
                .SingleOrDefault();
            Assert.IsNotNull(ownershipResult, "Should emit PlanetOwnershipChangedResult");
            Assert.AreEqual("empire", ownershipResult.NewOwnerInstanceID);
            Assert.IsNull(ownershipResult.PreviousOwnerInstanceID);
        }

        [Test]
        public void OnSuccess_AlreadyOwned_NoOwnershipChangeEmitted()
        {
            // Support already above threshold, planet already owned by empire
            GameRoot game = BuildGame(out Planet planet, empireSupport: 61, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            List<GameResult> results =
                (List<GameResult>)onSuccess.Invoke(mission, new object[] { game });

            Assert.IsFalse(
                results.OfType<PlanetOwnershipChangedResult>().Any(),
                "Should not emit ownership change when planet is already owned by mission faction"
            );
        }

        [Test]
        public void OnSuccess_AlreadyOwned_NoRedundantChangeUnitOwnershipCall()
        {
            // Verify support still increments even when no ownership event fires
            GameRoot game = BuildGame(out Planet planet, empireSupport: 61, planetOwner: "empire");
            DiplomacyMission mission = CreateAndAttachMission(game, planet);

            System.Reflection.MethodInfo onSuccess = typeof(DiplomacyMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            onSuccess.Invoke(mission, new object[] { game });

            Assert.AreEqual(
                62,
                planet.GetPopularSupport("empire"),
                "Support should still increment"
            );
            Assert.AreEqual("empire", planet.OwnerInstanceID, "Owner should remain empire");
        }

        [Test]
        public void CanContinue_SupportAtMax_ReturnsFalse()
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
    }
}
