using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class VictoryObserverTests
    {
        [Test]
        public void HandleResults_HeadquartersCaptured_ReturnsVictory()
        {
            (
                GameRoot game,
                Faction empire,
                Faction rebels,
                Planet empireHQ,
                VictoryCommands system
            ) = BuildScene(rebelsCaptureEmpireHQ: false);

            List<GameResult> results = new VictoryObserver(system).HandleResults(
                new List<HeadquartersLostResult>
                {
                    new HeadquartersCapturedResult
                    {
                        Planet = empireHQ,
                        Defender = empire,
                        Attacker = rebels,
                    },
                }
            );

            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreSame(rebels, victory.Winner);
            Assert.AreSame(empire, victory.Loser);
        }

        [Test]
        public void HandleResults_MultipleHeadquartersLosses_DeclaresOnlyFirstVictory()
        {
            (_, Faction defender, Faction firstAttacker, _, VictoryCommands system) = BuildScene();
            Faction secondAttacker = new Faction { InstanceID = "other" };

            List<GameResult> results = new VictoryObserver(system).HandleResults(
                new HeadquartersLostResult[]
                {
                    null,
                    new HeadquartersCapturedResult { Defender = defender },
                    new HeadquartersDestroyedResult
                    {
                        Attacker = firstAttacker,
                        Defender = defender,
                    },
                    new HeadquartersCapturedResult
                    {
                        Attacker = secondAttacker,
                        Defender = defender,
                    },
                }
            );

            VictoryResult victory = results.OfType<VictoryResult>().Single();
            Assert.AreSame(firstAttacker, victory.Winner);
            Assert.AreEqual(200, victory.Tick);
            Assert.IsEmpty(system.ProcessTick());
            Assert.IsEmpty(
                new VictoryObserver(system).HandleResults(
                    new HeadquartersLostResult[]
                    {
                        new HeadquartersCapturedResult
                        {
                            Attacker = secondAttacker,
                            Defender = defender,
                        },
                    }
                )
            );
        }

        [Test]
        public void HandleResults_ConquestLossWithFreeLeader_AllowsNextEligibleDefeat()
        {
            (
                GameRoot game,
                Faction defender,
                Faction attacker,
                Planet planet,
                VictoryCommands system
            ) = BuildScene(GameVictoryCondition.Conquest);
            Planet friendlyPlanet = new Planet
            {
                InstanceID = "friendly",
                OwnerInstanceID = defender.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(friendlyPlanet, planet.GetParent());
            game.AttachNode(
                new Officer
                {
                    InstanceID = "free-leader",
                    OwnerInstanceID = defender.InstanceID,
                    IsMain = true,
                },
                friendlyPlanet
            );
            Faction otherDefender = new Faction { InstanceID = "other-defender" };
            game.GetFactions().Add(otherDefender);

            List<GameResult> results = new VictoryObserver(system).HandleResults(
                new HeadquartersLostResult[]
                {
                    new HeadquartersCapturedResult { Attacker = attacker, Defender = defender },
                    new HeadquartersCapturedResult
                    {
                        Attacker = attacker,
                        Defender = otherDefender,
                    },
                }
            );

            Assert.AreSame(otherDefender, results.OfType<VictoryResult>().Single().Loser);
        }

        [Test]
        public void HandleResults_NullBatch_ReturnsEmpty()
        {
            (_, _, _, _, VictoryCommands system) = BuildScene();

            Assert.IsEmpty(new VictoryObserver(system).HandleResults(null));
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="victoryCondition">The victory condition.</param>
        /// <param name="rebelsCaptureEmpireHQ">Whether rebels capture empire hq.</param>
        /// <returns>The constructed scene.</returns>
        private (
            GameRoot game,
            Faction empire,
            Faction rebels,
            Planet empireHQ,
            VictoryCommands system
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
            game.GetFactions().Add(empire);
            game.GetFactions().Add(rebels);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet empireHQ = new Planet
            {
                InstanceID = "hq_empire",
                OwnerInstanceID = rebelsCaptureEmpireHQ ? "rebels" : "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(empireHQ, planetSector);
            empire.HQInstanceID = "hq_empire";

            return (game, empire, rebels, empireHQ, new VictoryCommands(game));
        }
    }
}
