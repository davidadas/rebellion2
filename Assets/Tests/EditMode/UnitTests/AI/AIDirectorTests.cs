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
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class AIDirectorTests
    {
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

            Assert.AreEqual(18, completedSteps);
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

        [TestCase(false)]
        [TestCase(true)]
        public void ProcessFaction_CurrentTickReservation_ControlsOfficerAssignment(bool reserve)
        {
            (
                GameRoot game,
                Faction owner,
                Officer officer,
                GameResultBus results,
                AIDirector director
            ) = BuildMissionScene();
            using (director)
            {
                if (reserve)
                    results.Publish(
                        new OfficerReservedForAIResult
                        {
                            OfficerInstanceID = officer.InstanceID,
                            FactionInstanceID = owner.InstanceID,
                            Tick = game.CurrentTick,
                        }
                    );

                director.ProcessFaction(owner, game.Galaxy);

                Assert.AreEqual(!reserve, officer.IsOnMission());
            }
        }

        [Test]
        public void ProcessFaction_ReservationFromPreviousTick_AllowsOfficerAssignment()
        {
            (
                GameRoot game,
                Faction owner,
                Officer officer,
                GameResultBus results,
                AIDirector director
            ) = BuildMissionScene();
            using (director)
            {
                results.Publish(
                    new OfficerReservedForAIResult
                    {
                        OfficerInstanceID = officer.InstanceID,
                        FactionInstanceID = owner.InstanceID,
                        Tick = game.CurrentTick,
                    }
                );
                game.CurrentTick++;

                director.ProcessFaction(owner, game.Galaxy);

                Assert.IsTrue(officer.IsOnMission());
            }
        }

        [TestCase("different-owner", 0)]
        [TestCase("empire", -1)]
        public void ProcessFaction_InvalidReservation_DoesNotWithholdOfficer(
            string factionID,
            int tickOffset
        )
        {
            (
                GameRoot game,
                Faction owner,
                Officer officer,
                GameResultBus results,
                AIDirector director
            ) = BuildMissionScene();
            using (director)
            {
                results.Publish(
                    new OfficerReservedForAIResult
                    {
                        OfficerInstanceID = officer.InstanceID,
                        FactionInstanceID = factionID,
                        Tick = game.CurrentTick + tickOffset,
                    }
                );

                director.ProcessFaction(owner, game.Galaxy);

                Assert.IsTrue(officer.IsOnMission());
            }
        }

        /// <summary>
        /// Creates a synthetic diplomacy opportunity for an otherwise idle officer.
        /// </summary>
        /// <returns>The graph, faction, officer, result pipeline, and AI director.</returns>
        private static (
            GameRoot Game,
            Faction Owner,
            Officer Officer,
            GameResultBus Results,
            AIDirector Director
        ) BuildMissionScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction owner, out Faction _);
            game.CurrentTick = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", owner.InstanceID);
            origin.SetPopularSupport(owner.InstanceID, 80);
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "target",
                null,
                positionX: 1
            );
            target.SetPopularSupport(owner.InstanceID, 20);
            Officer officer = EntityFactory.CreateOfficer("officer", owner.InstanceID);
            officer.SetBaseRating(SkillRating.Diplomacy, 200);
            game.AttachNode(officer, origin);
            AITestSceneBuilder.RevealPlanet(game, owner, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, owner);
            GameResultBus results = new();
            AIDirector director = new(
                game,
                context.Missions,
                context.Movement,
                context.Manufacturing,
                context.Bombardment,
                context.PlanetaryAssault,
                context.Random,
                context.Maintenance,
                context.Headquarters,
                results
            );
            return (game, owner, officer, results, director);
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
