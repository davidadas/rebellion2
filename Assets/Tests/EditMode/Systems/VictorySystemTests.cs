using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class VictorySystemTests
    {
        // Builds a two-faction scene where rebels own empire's HQ by default.
        // Call site controls VictoryCondition and whether to flip the HQ.
        private (
            GameRoot game,
            Faction empire,
            Faction rebels,
            Planet empireHQ,
            VictorySystem system
        ) BuildScene(
            GameVictoryCondition victoryCondition = GameVictoryCondition.Headquarters,
            bool rebelsCaptureEmpireHQ = true
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Summary = new GameSummary { VictoryCondition = victoryCondition };
            game.CurrentTick = 200;

            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet empireHQ = new Planet
            {
                InstanceID = "hq_empire",
                OwnerInstanceID = rebelsCaptureEmpireHQ ? "rebels" : "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(empireHQ, system);
            empire.HQInstanceID = "hq_empire";

            return (game, empire, rebels, empireHQ, new VictorySystem(game));
        }

        [Test]
        public void ProcessTick_HQNotConfigured_ReturnsEmpty()
        {
            (GameRoot game, Faction empire, _, _, VictorySystem system) = BuildScene();
            empire.HQInstanceID = null;

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(0, results.Count, "No HQ configured should return no results");
        }

        [Test]
        public void ProcessTick_HQStillOwnedByDefender_ReturnsEmpty()
        {
            (_, _, _, _, VictorySystem system) = BuildScene(rebelsCaptureEmpireHQ: false);

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(0, results.Count, "HQ held by defender should not trigger victory");
        }

        [Test]
        public void ProcessTick_HQCapturedHeadquartersMode_ReturnsVictoryResult()
        {
            (_, Faction empire, Faction rebels, _, VictorySystem system) = BuildScene(
                GameVictoryCondition.Headquarters
            );

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(1, results.Count);
            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreEqual(rebels, victory.Winner);
            Assert.AreEqual(empire, victory.Loser);
        }

        [Test]
        public void ProcessTick_HQCapturedConquestMode_LeadersFree_ReturnsEmpty()
        {
            (GameRoot game, Faction empire, _, _, VictorySystem system) = BuildScene(
                GameVictoryCondition.Conquest
            );

            // Leader must live on an empire-owned planet, not the captured HQ.
            Planet empirePlanet = new Planet
            {
                InstanceID = "p_empire",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(empirePlanet, game.GetSceneNodeByInstanceID<PlanetSystem>("sys1"));

            Officer leader = new Officer
            {
                InstanceID = "leader1",
                OwnerInstanceID = "empire",
                IsMain = true,
                IsCaptured = false,
            };
            game.AttachNode(leader, empirePlanet);

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(
                0,
                results.Count,
                "Conquest mode with free leader should not trigger victory"
            );
        }

        [Test]
        public void ProcessTick_HQCapturedConquestMode_AllLeadersCaptured_ReturnsVictoryResult()
        {
            (GameRoot game, Faction empire, Faction rebels, _, VictorySystem system) = BuildScene(
                GameVictoryCondition.Conquest
            );

            Officer leader = new Officer
            {
                InstanceID = "leader1",
                OwnerInstanceID = "empire",
                IsMain = true,
                IsCaptured = true,
            };
            game.AttachNode(leader, game.GetSceneNodeByInstanceID<Planet>("hq_empire"));

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(1, results.Count);
            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreEqual(rebels, victory.Winner);
            Assert.AreEqual(empire, victory.Loser);
        }

        [Test]
        public void ProcessTick_HQCapturedConquestMode_NoMainCharacters_ReturnsVictoryResult()
        {
            // If a faction has no main characters at all, treat as all captured (prevents softlock).
            (_, _, Faction rebels, _, VictorySystem system) = BuildScene(
                GameVictoryCondition.Conquest
            );

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(1, results.Count);
            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreEqual(rebels, victory.Winner);
        }
    }
}
