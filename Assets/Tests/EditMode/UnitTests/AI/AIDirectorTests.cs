using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class AIDirectorTests
    {
        [Test]
        public void ProcessTick_OptionalProductionAcrossTurns_RetainsSessionPriority()
        {
            var (game, faction, planet, system) = CreateOptionalDefenseScene();
            system.ProcessTick();
            IManufacturable order = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .Single();
            Assert.IsTrue(system.IsOptionalProduction(order));

            game.CurrentTick += game.Config.AI.TickInterval;
            system.ProcessTick();

            Assert.AreSame(
                order,
                planet.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
            Assert.IsTrue(system.IsOptionalProduction(order));
        }

        [Test]
        public void ProcessTick_EmergencyEnds_KeepsExistingOrderProtected()
        {
            var (game, faction, planet, system) = CreateOptionalDefenseScene();
            system.ProcessTick();
            IManufacturable order = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .Single();
            Assert.IsTrue(system.IsOptionalProduction(order));
            Faction enemy = game.GetFactions().Single(candidate => candidate != faction);
            Fleet hostile = EntityFactory.CreateFleet("hostile", enemy.InstanceID);
            game.AttachNode(hostile, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("hostile-ship", enemy.InstanceID),
                hostile
            );
            game.CurrentTick += game.Config.AI.TickInterval;
            system.ProcessTick();
            Assert.IsFalse(system.IsOptionalProduction(order));

            game.DetachNode(hostile);
            game.CurrentTick += game.Config.AI.TickInterval;
            system.ProcessTick();

            Assert.AreSame(
                order,
                planet.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
            Assert.IsFalse(system.IsOptionalProduction(order));
        }

        [Test]
        public void ProcessTick_CompletedOptionalOrder_ReleasesPriorityMemory()
        {
            var (game, faction, planet, system) = CreateOptionalDefenseScene();
            system.ProcessTick();
            IManufacturable order = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .Single();
            Assert.IsTrue(system.IsOptionalProduction(order));
            order.ManufacturingStatus = ManufacturingStatus.Complete;
            order.ManufacturingProgress = order.ConstructionCost;

            game.CurrentTick += game.Config.AI.TickInterval;
            system.ProcessTick();

            Assert.IsFalse(system.IsOptionalProduction(order));
        }

        [Test]
        public void ProcessTick_SaveReload_ProtectsQueuedWorkWithoutSavedPriorityData()
        {
            var (game, faction, planet, system) = CreateOptionalDefenseScene();
            system.ProcessTick();
            Building order = (Building)
                planet.GetManufacturingQueue()[ManufacturingType.Building].Single();
            order.ManufacturingProgress = 3;
            Assert.IsTrue(system.IsOptionalProduction(order));
            string saved = SerializationHelper.Serialize(game);
            StringAssert.DoesNotContain("IsAIProductionFiller", saved);
            StringAssert.DoesNotContain("optionalProductionOrders", saved);
            GameRoot loaded = SerializationHelper.Deserialize<GameRoot>(saved);
            loaded.SetConfig(game.Config);
            using GameSession session = new GameSession(loaded, TestGameData.Create(game.Config));
            AIDirector resumed = session.GetService<AIDirector>();
            Building existing = loaded.GetSceneNodeByInstanceID<Building>(order.InstanceID);

            resumed.ProcessTick();

            Assert.IsFalse(resumed.IsOptionalProduction(existing));
            Assert.AreSame(
                existing,
                loaded
                    .GetSceneNodeByInstanceID<Planet>(planet.InstanceID)
                    .GetManufacturingQueue()[ManufacturingType.Building]
                    .Single()
            );
            Assert.AreEqual(3, existing.ManufacturingProgress);
        }

        [Test]
        public void ProcessTick_BeforeConfiguredInterval_DoesNotProcessFaction()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval - 1;

            system.ProcessTick();

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void ProcessTick_AtConfiguredInterval_ProcessesFaction()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;

            system.ProcessTick();

            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
        }

        [Test]
        public void ProcessTickIncrementally_AtConfiguredInterval_YieldsBetweenWorkUnits()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;
            List<GameResult> results = new List<GameResult>();

            int completedSteps = system.ProcessTickIncrementally(results).Count();

            Assert.AreEqual(19, completedSteps);
            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
        }

        [Test]
        public void ProcessTick_AtConfiguredInterval_PublishesFactionTurnLifecycle()
        {
            (GameRoot game, Fleet _, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;
            string factionId = game.GetFactions().Single(game.IsFactionAIControlled).InstanceID;
            List<string> lifecycle = new List<string>();
            system.FactionTurnStarted += faction => lifecycle.Add($"start:{faction.InstanceID}");
            system.FactionTurnCompleted += faction =>
                lifecycle.Add($"complete:{faction.InstanceID}");

            system.ProcessTick();

            CollectionAssert.AreEqual(
                new[] { $"start:{factionId}", $"complete:{factionId}" },
                lifecycle
            );
        }

        /// <summary>
        /// Creates an AI whose only available construction is an optional headquarters shield.
        /// </summary>
        /// <returns>The game, acting faction, producer, and session-scoped director.</returns>
        private static (GameRoot, Faction, Planet, AIDirector) CreateOptionalDefenseScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "ai",
                        FactionID = faction.InstanceID,
                        ControllerType = PlayerControllerType.AI,
                    }
                );
            game.GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "human",
                        FactionID = enemy.InstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            game.Config.AI.Infrastructure.PlanetaryShieldDemandPercent = 100;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                faction.InstanceID,
                energyCapacity: 50
            );
            planet.SetPopularSupport(faction.InstanceID, 100);
            planet.IsHeadquarters = true;
            faction.HQInstanceID = planet.InstanceID;
            faction.RefinedMaterialStockpile = 10000;
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            faction.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shield),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIDirector system = new AIDirector(
                game,
                context.Missions,
                context.Movement,
                context.Manufacturing,
                context.Bombardment,
                context.PlanetaryAssault,
                context.Random,
                context.Maintenance,
                context.Headquarters
            );
            game.CurrentTick = game.Config.AI.TickInterval;
            return (game, faction, planet, system);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private static (GameRoot Game, Fleet Fleet, AIDirector System) BuildScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "player",
                        FactionID = rebels.InstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );
            game.Config.AI.TickInterval = 7;
            game.Config.AI.FleetDeployment.AttackUtility.OpportunityCost.Weight = 0;
            PlanetSector planetSystem = AITestSceneBuilder.AddSector(game, "system");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSystem,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSystem,
                "enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIDirector system = new AIDirector(
                game,
                context.Missions,
                context.Movement,
                context.Manufacturing,
                context.Bombardment,
                context.PlanetaryAssault,
                context.Random,
                context.Maintenance,
                context.Headquarters
            );

            return (game, fleet, system);
        }
    }
}
