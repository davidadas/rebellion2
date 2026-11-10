using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class VictoryObserverTests
    {
        [Test]
        public void HandleResults_HeadquartersLost_ProducesVictory()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene();
            VictoryObserver observer = CreateObserver(game);

            List<GameResult> results = observer.HandleResults(
                new GameResult[]
                {
                    new HeadquartersCapturedResult
                    {
                        Planet = headquarters,
                        Defender = defender,
                        Attacker = attacker,
                    },
                }
            );

            VictoryResult victory = results.OfType<VictoryResult>().Single();
            Assert.AreSame(attacker, victory.Winner);
            CollectionAssert.AreEqual(new[] { defender }, victory.DefeatedFactions);
            Assert.AreEqual(200, victory.Tick);
        }

        [Test]
        public void HandleResults_HeadquartersLostWithoutAttacker_UsesSoleOpponent()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene();
            VictoryObserver observer = CreateObserver(game);

            List<GameResult> results = observer.HandleResults(
                new GameResult[]
                {
                    new HeadquartersDestroyedResult { Planet = headquarters, Defender = defender },
                }
            );

            Assert.AreSame(attacker, results.OfType<VictoryResult>().Single().Winner);
        }

        [Test]
        public void HandleResults_OrderedQualifyingChanges_UsesFirstResult()
        {
            (GameRoot game, Faction empire, Faction rebels, Planet empireHeadquarters) =
                BuildScene();
            Planet rebelHeadquarters = game.GetSceneNodeByInstanceID<Planet>("hq-rebels");
            game.ChangeOwnership(rebelHeadquarters, empire.InstanceID);
            VictoryObserver observer = CreateObserver(game);

            List<GameResult> results = observer.HandleResults(
                new GameResult[]
                {
                    new HeadquartersCapturedResult
                    {
                        Planet = empireHeadquarters,
                        Defender = empire,
                        Attacker = rebels,
                    },
                    new HeadquartersCapturedResult
                    {
                        Planet = rebelHeadquarters,
                        Defender = rebels,
                        Attacker = empire,
                    },
                }
            );

            Assert.AreSame(rebels, results.OfType<VictoryResult>().Single().Winner);
        }

        [Test]
        public void HandleResults_ConquestOfficerRemainsFree_DoesNotProduceVictory()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene(
                GameVictoryCondition.Conquest
            );
            defender.Settings.ConquestOfficerInstanceIDs.Add("leader");
            Planet defenderPlanet = AddOwnedPlanet(game, headquarters, defender, "defender-home");
            game.AttachNode(
                new Officer { InstanceID = "leader", OwnerInstanceID = defender.InstanceID },
                defenderPlanet
            );
            VictoryObserver observer = CreateObserver(game);

            List<GameResult> results = observer.HandleResults(
                new GameResult[]
                {
                    new HeadquartersCapturedResult { Defender = defender, Attacker = attacker },
                }
            );

            Assert.IsEmpty(results);
        }

        [Test]
        public void HandleResults_LastConquestOfficerCaptured_ProducesVictoryImmediately()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene(
                GameVictoryCondition.Conquest
            );
            defender.Settings.ConquestOfficerInstanceIDs.Add("leader");
            Officer leader = new Officer
            {
                InstanceID = "leader",
                OwnerInstanceID = defender.InstanceID,
                IsCaptured = true,
                CaptorInstanceID = attacker.InstanceID,
            };
            game.AttachNode(leader, headquarters);
            Planet defenderPlanet = AddOwnedPlanet(game, headquarters, defender, "defender-home");
            game.AttachNode(
                new Officer { InstanceID = "other-officer", OwnerInstanceID = defender.InstanceID },
                defenderPlanet
            );
            VictoryObserver observer = CreateObserver(game);

            List<GameResult> results = observer.HandleResults(
                new GameResult[]
                {
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = leader,
                        IsCaptured = true,
                        CaptorInstanceID = attacker.InstanceID,
                    },
                }
            );

            Assert.AreSame(attacker, results.OfType<VictoryResult>().Single().Winner);
        }

        [Test]
        public void HandleResults_ConquestOfficerReleased_DoesNotProduceVictory()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene(
                GameVictoryCondition.Conquest
            );
            defender.Settings.ConquestOfficerInstanceIDs.Add("leader");
            Officer leader = new Officer
            {
                InstanceID = "leader",
                OwnerInstanceID = defender.InstanceID,
            };
            Planet defenderPlanet = AddOwnedPlanet(game, headquarters, defender, "defender-home");
            game.AttachNode(leader, defenderPlanet);
            VictoryObserver observer = CreateObserver(game);

            List<GameResult> results = observer.HandleResults(
                new GameResult[]
                {
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = leader,
                        IsCaptured = false,
                        CaptorInstanceID = attacker.InstanceID,
                    },
                }
            );

            Assert.IsEmpty(results);
        }

        [Test]
        public void Connect_LastConquestOfficerCaptured_QueuesVictoryOnResultBus()
        {
            (GameRoot game, Faction defender, Faction attacker, Planet headquarters) = BuildScene(
                GameVictoryCondition.Conquest
            );
            defender.Settings.ConquestOfficerInstanceIDs.Add("leader");
            Officer leader = new Officer
            {
                InstanceID = "leader",
                OwnerInstanceID = defender.InstanceID,
                IsCaptured = true,
                CaptorInstanceID = attacker.InstanceID,
            };
            game.AttachNode(leader, headquarters);
            GameResultBus bus = new();
            using VictoryObserver observer = CreateObserver(game);
            observer.Connect(bus);

            List<GameResult> results = bus.Publish(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = leader,
                    IsCaptured = true,
                    CaptorInstanceID = attacker.InstanceID,
                }
            );

            Assert.AreSame(attacker, results.OfType<VictoryResult>().Single().Winner);
        }

        /// <summary>
        /// Creates a victory observer for the supplied game.
        /// </summary>
        /// <param name="game">The game to observe.</param>
        /// <returns>The configured observer.</returns>
        private static VictoryObserver CreateObserver(GameRoot game)
        {
            return new VictoryObserver(new VictoryCommands(game), new VictoryQueries(game));
        }

        /// <summary>
        /// Adds a colonized planet owned by the specified faction.
        /// </summary>
        /// <param name="game">The game receiving the planet.</param>
        /// <param name="sibling">A planet in the destination sector.</param>
        /// <param name="owner">The planet owner.</param>
        /// <param name="instanceId">The planet instance identifier.</param>
        /// <returns>The attached planet.</returns>
        private static Planet AddOwnedPlanet(
            GameRoot game,
            Planet sibling,
            Faction owner,
            string instanceId
        )
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, sibling.GetParent());
            return planet;
        }

        /// <summary>
        /// Creates a two-faction victory scene.
        /// </summary>
        /// <param name="victoryCondition">The active victory condition.</param>
        /// <returns>The game, factions, and lost headquarters planet.</returns>
        private static (
            GameRoot game,
            Faction empire,
            Faction rebels,
            Planet empireHeadquarters
        ) BuildScene(GameVictoryCondition victoryCondition = GameVictoryCondition.Headquarters)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Summary = new GameSummary { VictoryCondition = victoryCondition };
            game.CurrentTick = 200;
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(rebels);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet empireHeadquarters = new Planet
            {
                InstanceID = "hq-empire",
                OwnerInstanceID = rebels.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(empireHeadquarters, sector);
            empire.HQInstanceID = empireHeadquarters.InstanceID;
            Planet rebelHeadquarters = new Planet
            {
                InstanceID = "hq-rebels",
                OwnerInstanceID = rebels.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(rebelHeadquarters, sector);
            rebels.HQInstanceID = rebelHeadquarters.InstanceID;
            return (game, empire, rebels, empireHeadquarters);
        }
    }
}
