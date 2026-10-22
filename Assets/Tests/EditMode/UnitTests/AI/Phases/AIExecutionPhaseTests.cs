using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIExecutionPhaseTests
    {
        [Test]
        public void Execute_BombardmentDonorOvercomesShields_LoadsItAndEnablesLaunch()
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentDonorContext(
                false,
                5,
                out Fleet receiver,
                out CapitalShip donor
            );
            context.AddProposals(new AIFleetPlanner().Plan(context));
            new AIProductionDemandGenerator().Generate(context);
            new AIScoringPhase().Execute(context);
            context.SetSelectedProposals(new AISelectionPhase().Select(context));
            Assert.IsTrue(
                context
                    .SelectedProposals.OfType<AITransferUnitProposal>()
                    .Any(proposal => proposal.Unit == donor),
                string.Join(
                    ", ",
                    context.SelectedProposals.Select(proposal => proposal.GetSortKey())
                )
            );

            new AIExecutionPhase().Execute(context);

            Assert.AreSame(
                receiver,
                donor.GetParentOfType<Fleet>(),
                "Chosen actions: "
                    + string.Join(
                        ", ",
                        context.SelectedProposals.Select(proposal => proposal.GetSortKey())
                    )
                    + "; current target: "
                    + receiver.Order?.TargetPlanetId
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            context.AddProposals(new AIFleetPlanner().Plan(context));
            new AIProductionDemandGenerator().Generate(context);
            new AIScoringPhase().Execute(context);
            context.SetSelectedProposals(new AISelectionPhase().Select(context));
            new AIExecutionPhase().Execute(context);
            Assert.IsNotNull(receiver.Movement);
            Assert.AreEqual(
                receiver.Order.TargetPlanetId,
                receiver.GetParentOfType<Planet>().InstanceID
            );
        }

        [Test]
        public void Execute_BombardingFleetNeedsOccupation_LoadsItsRegimentAndEnablesLaunch()
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentReinforcementContext(
                false,
                false,
                out Fleet bombardFleet,
                out Fleet otherFleet,
                out _
            );
            AIAttackDemand[] demands = context.AttackDemands.ToArray();
            context.AddProposals(new AIFleetPlanner().Plan(context));
            new AIProductionDemandGenerator().Generate(context);
            new AIScoringPhase().Execute(context);
            new AISelectionPhase().Execute(context);

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(1, bombardFleet.GetChildren<Regiment>(recursive: true).Count);
            Assert.AreEqual(0, otherFleet.GetChildren<Regiment>(recursive: true).Count);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            context.SetAttackDemands(demands);
            Planet target = context.Assessment.GetKnownPlanet(bombardFleet.Order.TargetPlanetId);
            new AIFleetAttackProposal(
                bombardFleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Building,
                target
            ).Execute(context);
            Assert.IsNotNull(bombardFleet.Movement);
        }

        [TestCase(
            ResearchDiscipline.ShipDesign,
            BuildingType.Shipyard,
            ManufacturingType.Ship,
            true
        )]
        [TestCase(
            ResearchDiscipline.ShipDesign,
            BuildingType.Shipyard,
            ManufacturingType.Ship,
            false
        )]
        [TestCase(
            ResearchDiscipline.FacilityDesign,
            BuildingType.ConstructionFacility,
            ManufacturingType.Building,
            true
        )]
        [TestCase(
            ResearchDiscipline.FacilityDesign,
            BuildingType.ConstructionFacility,
            ManufacturingType.Building,
            false
        )]
        [TestCase(
            ResearchDiscipline.TroopTraining,
            BuildingType.TrainingFacility,
            ManufacturingType.Troop,
            true
        )]
        [TestCase(
            ResearchDiscipline.TroopTraining,
            BuildingType.TrainingFacility,
            ManufacturingType.Troop,
            false
        )]
        public void Execute_MultipleResearchersInSameDiscipline_StartsBothMissions(
            ResearchDiscipline discipline,
            BuildingType facilityType,
            ManufacturingType manufacturingType,
            bool samePlanet
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet firstPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first-planet",
                faction.InstanceID
            );
            Planet secondPlanet = samePlanet
                ? firstPlanet
                : AITestSceneBuilder.AddPlanet(game, sector, "second-planet", faction.InstanceID);
            foreach (Planet planet in new[] { firstPlanet, secondPlanet }.Distinct())
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-facility",
                    facilityType,
                    manufacturingType
                );
            faction.ResearchCatalog[discipline] = new List<ResearchCatalogEntry>
            {
                new ResearchCatalogEntry { Order = 1 },
            };
            Officer firstOfficer = EntityFactory.CreateOfficer("first-officer", faction.InstanceID);
            Officer secondOfficer = EntityFactory.CreateOfficer(
                "second-officer",
                faction.InstanceID
            );
            firstOfficer.SetBaseRating(Officer.GetRatingForResearchDiscipline(discipline), 60);
            secondOfficer.SetBaseRating(Officer.GetRatingForResearchDiscipline(discipline), 60);
            game.AttachNode(firstOfficer, firstPlanet);
            game.AttachNode(secondOfficer, secondPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIMissionProposal first = new(
                new[] { firstOfficer },
                MissionTypeIDs.Research,
                firstPlanet,
                discipline: discipline
            );
            AIMissionProposal second = new(
                new[] { secondOfficer },
                MissionTypeIDs.Research,
                secondPlanet,
                discipline: discipline
            );
            first.SetScore(50);
            second.SetScore(40);
            Assert.IsTrue(first.CanExecute(context));
            Assert.IsTrue(second.CanExecute(context));
            context.AddProposals(new[] { first, second });
            new AISelectionPhase().Execute(context);

            new AIExecutionPhase().Execute(context);

            CollectionAssert.AreEquivalent(
                new[] { firstOfficer, secondOfficer },
                game.GetSceneNodesByType<ResearchMission>(mission =>
                        mission.OwnerInstanceID == faction.InstanceID
                        && mission.Discipline == discipline
                    )
                    .SelectMany(mission => mission.GetMainParticipants())
            );
        }

        [Test]
        public void Execute_WithSelectedExecutableProposal_ExecutesProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(1, proposal.ExecuteCount);
        }

        [Test]
        public void Execute_WithSelectedNonExecutableProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        [Test]
        public void ExecuteIncrementally_WithSelectedProposals_YieldsAfterEachProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal first = new TestAIProposal(canExecute: true);
            TestAIProposal second = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { first, second });

            IEnumerator<object> execution = new AIExecutionPhase()
                .ExecuteIncrementally(context)
                .GetEnumerator();

            Assert.IsTrue(execution.MoveNext());
            Assert.AreEqual(1, first.ExecuteCount);
            Assert.AreEqual(0, second.ExecuteCount);
            Assert.IsTrue(execution.MoveNext());
            Assert.AreEqual(1, second.ExecuteCount);
            Assert.IsFalse(execution.MoveNext());
        }

        [Test]
        public void ExecuteIncrementally_WithInvalidProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            foreach (object _ in new AIExecutionPhase().ExecuteIncrementally(context)) { }

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        [Test]
        public void ExecuteIncrementally_EconomyChangesBetweenManufacturingProposals_DoesNotOvercommitMaintenance()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet resourcePlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            Planet firstProducer = AddShipyard(game, system, empire, "first-producer");
            Planet secondProducer = AddShipyard(game, system, empire, "second-producer");
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    resourcePlanet,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    resourcePlanet,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }

            Building destroyedRefinery = resourcePlanet
                .GetAllBuildings()
                .First(building => building.BuildingType == BuildingType.Refinery);
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                maintenanceCost: 30
            );
            AIManufactureProposal first = CreateFighterProposal(firstProducer, fighter, "first");
            AIManufactureProposal second = CreateFighterProposal(secondProducer, fighter, "second");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.SetSelectedProposals(new List<AIProposal> { first, second });
            GameResultBus results = new GameResultBus();
            IEnumerator<object> execution = new AIExecutionPhase(results)
                .ExecuteIncrementally(context)
                .GetEnumerator();

            try
            {
                Assert.IsTrue(execution.MoveNext());
                Assert.AreEqual(
                    1,
                    firstProducer.GetManufacturingQueue()[ManufacturingType.Ship].Count
                );

                game.DeleteNode(destroyedRefinery);
                results.Publish(
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = destroyedRefinery,
                        Context = resourcePlanet,
                    }
                );

                Assert.IsTrue(execution.MoveNext());
                Assert.IsFalse(
                    secondProducer.GetManufacturingQueue().ContainsKey(ManufacturingType.Ship)
                );
                Assert.IsFalse(execution.MoveNext());
            }
            finally
            {
                (execution as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <returns>The created context.</returns>
        private static AITurnContext CreateContext()
        {
            return new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }

        /// <summary>
        /// Adds one ship-producing planet for an execution-phase manufacturing proposal.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="system">The containing system.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="instanceId">The planet identifier.</param>
        /// <returns>The configured producer planet.</returns>
        private static Planet AddShipyard(
            GameRoot game,
            PlanetSector system,
            Faction faction,
            string instanceId
        )
        {
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                instanceId,
                faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                $"{instanceId}-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            return planet;
        }

        /// <summary>
        /// Creates one fighter manufacturing proposal for an exact producer.
        /// </summary>
        /// <param name="producer">The producer and destination planet.</param>
        /// <param name="fighter">The fighter template.</param>
        /// <param name="instanceId">The demand identifier.</param>
        /// <returns>The manufacturing proposal.</returns>
        private static AIManufactureProposal CreateFighterProposal(
            Planet producer,
            Starfighter fighter,
            string instanceId
        )
        {
            AIProductionDemand demand = new AIProductionDemand(
                instanceId,
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                producer,
                1,
                baseDemandPercent: 100
            );
            return new AIManufactureProposal(
                demand,
                producer,
                new Technology(fighter),
                distributesDemand: true
            );
        }
    }
}
