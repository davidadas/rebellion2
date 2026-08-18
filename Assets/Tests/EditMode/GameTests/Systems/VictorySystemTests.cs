using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class VictorySystemTests
    {
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
        public void ProcessTick_AfterVictoryDeclared_DoesNotDeclareVictoryAgain()
        {
            (_, _, _, _, VictorySystem system) = BuildScene();

            List<GameResult> firstResults = system.ProcessTick();
            List<GameResult> secondResults = system.ProcessTick();

            Assert.AreEqual(1, firstResults.OfType<VictoryResult>().Count());
            Assert.IsEmpty(secondResults);
        }

        [Test]
        public void ProcessTick_HQCapturedConquestMode_LeadersFree_ReturnsEmpty()
        {
            (GameRoot game, Faction empire, _, _, VictorySystem system) = BuildScene(
                GameVictoryCondition.Conquest
            );

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
            (_, _, Faction rebels, _, VictorySystem system) = BuildScene(
                GameVictoryCondition.Conquest
            );

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(1, results.Count);
            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreEqual(rebels, victory.Winner);
        }

        [Test]
        public void ProcessTick_MobileHeadquartersInTransit_ReturnsEmpty()
        {
            (GameRoot game, Faction empire, _, Planet empireHQ, VictorySystem system) = BuildScene(
                rebelsCaptureEmpireHQ: false
            );
            empire.Settings = new FactionSettings
            {
                Headquarters = new HeadquartersSettings
                {
                    FacilityTypeID = "BDHQ01",
                    IsMobile = true,
                },
            };
            empire.HQInstanceID = null;
            empireHQ.EnergyCapacity = 1;
            Building headquarters = new Building
            {
                InstanceID = "mobile-hq",
                TypeID = "BDHQ01",
                OwnerInstanceID = empire.InstanceID,
                BuildingType = BuildingType.Headquarters,
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 },
            };
            game.AttachNode(headquarters, empireHQ);

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_MobileHeadquartersMissing_ReturnsEmpty()
        {
            (_, Faction empire, _, _, VictorySystem system) = BuildScene(
                rebelsCaptureEmpireHQ: false
            );
            empire.Settings = new FactionSettings
            {
                Headquarters = new HeadquartersSettings
                {
                    FacilityTypeID = "BDHQ01",
                    IsMobile = true,
                },
            };

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_MobileHeadquartersCaptured_ReturnsVictoryResult()
        {
            (GameRoot game, Faction empire, Faction rebels, Planet empireHQ, VictorySystem system) =
                BuildScene(rebelsCaptureEmpireHQ: false);
            empire.Settings = new FactionSettings
            {
                Headquarters = new HeadquartersSettings
                {
                    FacilityTypeID = "BDHQ01",
                    IsMobile = true,
                },
            };
            empireHQ.OwnerInstanceID = rebels.InstanceID;
            empireHQ.EnergyCapacity = 1;
            Building headquarters = new Building
            {
                InstanceID = "mobile-hq",
                TypeID = "BDHQ01",
                OwnerInstanceID = rebels.InstanceID,
                BuildingType = BuildingType.Headquarters,
            };
            game.AttachNode(headquarters, empireHQ);

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(1, results.Count);
            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreEqual(rebels, victory.Winner);
            Assert.AreEqual(empire, victory.Loser);
        }

        [Test]
        public void ProcessTick_MultipleMobileHeadquarters_UsesDefenderHeadquarters()
        {
            (GameRoot game, Faction empire, Faction rebels, Planet empireHQ, VictorySystem system) =
                BuildScene(rebelsCaptureEmpireHQ: false);
            empire.Settings = new FactionSettings
            {
                Headquarters = new HeadquartersSettings
                {
                    FacilityTypeID = "BDHQ01",
                    IsMobile = true,
                },
            };
            empireHQ.OwnerInstanceID = rebels.InstanceID;
            empireHQ.EnergyCapacity = 1;
            game.AttachNode(
                new Building
                {
                    InstanceID = "empire-mobile-hq",
                    TypeID = "BDHQ01",
                    OwnerInstanceID = rebels.InstanceID,
                    BuildingType = BuildingType.Headquarters,
                },
                empireHQ
            );

            Faction thirdFaction = new Faction
            {
                InstanceID = "third-faction",
                HQInstanceID = "third-hq-planet",
                Settings = new FactionSettings
                {
                    Headquarters = new HeadquartersSettings
                    {
                        FacilityTypeID = "BDHQ01",
                        IsMobile = true,
                    },
                },
            };
            game.GetFactions().Add(thirdFaction);
            Planet thirdHeadquartersPlanet = new Planet
            {
                InstanceID = thirdFaction.HQInstanceID,
                OwnerInstanceID = thirdFaction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 1,
            };
            game.AttachNode(
                thirdHeadquartersPlanet,
                game.GetSceneNodeByInstanceID<PlanetSystem>("sys1")
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "third-mobile-hq",
                    TypeID = "BDHQ01",
                    OwnerInstanceID = thirdFaction.InstanceID,
                    BuildingType = BuildingType.Headquarters,
                },
                thirdHeadquartersPlanet
            );

            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual(1, results.Count);
            VictoryResult victory = results[0] as VictoryResult;
            Assert.IsNotNull(victory);
            Assert.AreSame(rebels, victory.Winner);
            Assert.AreSame(empire, victory.Loser);
        }

        [Test]
        public void HandleResults_MobileHeadquartersDestroyed_ReturnsVictory()
        {
            (GameRoot game, Faction empire, Faction rebels, Planet empireHQ, VictorySystem system) =
                BuildScene(rebelsCaptureEmpireHQ: false);

            List<GameResult> results = system.HandleResults(
                new List<HeadquartersDestroyedResult>
                {
                    new HeadquartersDestroyedResult
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
    }
}
