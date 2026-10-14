using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Selectors
{
    [TestFixture]
    public class AIProductionSelectorTests
    {
        [Test]
        public void ScheduleProduction_InterleavedNonProductionProposal_PreservesRankedOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, producer);
            Technology product = new Technology(
                AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID)
            );
            AIProductionDemand demand = new AIProductionDemand(
                "demand",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1
            );
            AIManufactureProposal first = new AIManufactureProposal(
                demand,
                producer,
                product,
                true
            );
            first.SetScore(100);
            AIFleetRoleProposal middle = new AIFleetRoleProposal(fleet, FleetRoleType.Battle);
            middle.SetScore(50);
            AIManufactureProposal last = new AIManufactureProposal(demand, producer, product, true);
            last.SetScore(10);
            AIProposal[] ranked = { first, middle, last };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            CollectionAssert.AreEqual(
                ranked,
                new AIProductionSelector(new AISelectionState())
                    .ScheduleProduction(context, ranked, 0)
                    .ToList()
            );
        }

        [TestCase(0)]
        [TestCase(double.NaN)]
        public void ScheduleProduction_IneligibleFleetRequest_YieldsEachRequestOnce(double score)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, producer);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            Technology technology = new Technology(ship);
            AIProductionDemand demand = new AIProductionDemand(
                "demand",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1
            );
            AIManufactureProposal eligible = new AIManufactureProposal(
                demand,
                producer,
                technology,
                true
            );
            eligible.SetScore(100);
            AIManufactureProposal excluded = new AIManufactureProposal(
                demand,
                producer,
                technology,
                true
            );
            excluded.SetScore(score);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionSelector selector = new AIProductionSelector(new AISelectionState());

            CollectionAssert.AreEqual(
                new AIProposal[] { eligible, excluded },
                selector
                    .ScheduleProduction(context, new AIProposal[] { eligible, excluded }, 0)
                    .ToList()
            );
        }

        [Test]
        public void ScheduleProduction_MixedProductsNeedDifferentYards_FinishesWithinKnownFeasibleTime()
        {
            Assert.LessOrEqual(
                RunProduction(
                    new[]
                    {
                        (true, 38),
                        (true, 36),
                        (false, 28),
                        (false, 22),
                        (true, 13),
                        (false, 32),
                        (true, 3),
                    }
                ),
                31
            );
        }

        [Test]
        public void ScheduleProduction_SeveralProductsNeedDifferentYards_FinishesWithinKnownFeasibleTime()
        {
            Assert.LessOrEqual(
                RunProduction(
                    new[]
                    {
                        (true, 50),
                        (true, 31),
                        (false, 42),
                        (true, 28),
                        (false, 42),
                        (false, 46),
                        (false, 32),
                        (false, 23),
                    }
                ),
                52
            );
        }

        [Test]
        public void ScheduleProduction_ProducerHasUnfinishedShip_PreservesExistingConstruction()
        {
            AITurnContext context = CreateProductionContext(
                out Planet weak,
                out Planet strong,
                out Fleet fleet
            );
            CapitalShip existing = AITestSceneBuilder.CreateCapitalShip(
                "existing",
                context.Faction.InstanceID
            );
            existing.ConstructionCost = 1000;
            Assert.IsTrue(context.Manufacturing.Enqueue(weak, existing, fleet));
            existing.ManufacturingProgress = 430;
            CapitalShip requested = AITestSceneBuilder.CreateCapitalShip(
                "requested",
                context.Faction.InstanceID
            );
            requested.ConstructionCost = 38;
            AIManufactureProposal request = CreateRequest(
                context,
                fleet,
                new[] { strong, weak },
                requested,
                100
            );

            SelectAndExecute(context, new[] { request });

            Assert.AreSame(existing, weak.GetManufacturingQueue()[ManufacturingType.Ship].Single());
            Assert.AreEqual(430, existing.ManufacturingProgress);
        }

        [Test]
        public void ScheduleProduction_PersonnelFitsCommittedCarrier_DoesNotWaitForTentativeHull()
        {
            AITurnContext context = CreateProductionContext(
                out Planet weak,
                out Planet strong,
                out Fleet fleet
            );
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "requested",
                context.Faction.InstanceID
            );
            ship.ConstructionCost = 38;
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                context.Faction.InstanceID,
                constructionCost: 5
            );
            AIManufactureProposal hullRequest = CreateRequest(
                context,
                fleet,
                new[] { strong, weak },
                ship,
                100
            );
            AIManufactureProposal fighterRequest = CreateRequest(
                context,
                fleet,
                new[] { strong, weak },
                fighter,
                50
            );

            List<AIManufactureProposal> selected = SelectAndExecute(
                context,
                new[] { hullRequest, fighterRequest }
            );

            Assert.AreEqual(2, selected.Count);
        }

        [Test]
        public void ScheduleProduction_BusyProducerFinishesSooner_WaitsWithoutCancelingConstruction()
        {
            AITurnContext context = CreateProductionContext(
                out Planet weak,
                out Planet strong,
                out Fleet fleet
            );
            CapitalShip existing = AITestSceneBuilder.CreateCapitalShip(
                "existing",
                context.Faction.InstanceID
            );
            existing.ConstructionCost = 20;
            Assert.IsTrue(context.Manufacturing.Enqueue(strong, existing, fleet));
            existing.ManufacturingProgress = 10;
            CapitalShip requested = AITestSceneBuilder.CreateCapitalShip(
                "requested",
                context.Faction.InstanceID
            );
            requested.ConstructionCost = 100;
            AIManufactureProposal request = CreateRequest(
                context,
                fleet,
                new[] { strong, weak },
                requested,
                100
            );

            List<AIManufactureProposal> selected = SelectAndExecute(context, new[] { request });

            Assert.IsEmpty(selected);
            Assert.AreSame(
                existing,
                strong.GetManufacturingQueue()[ManufacturingType.Ship].Single()
            );
            Assert.AreEqual(10, existing.ManufacturingProgress);
        }

        /// <summary>Builds fixed product requests through the actual selection, construction, and delivery contracts.</summary>
        /// <param name="requests">Capital-ship flags and construction costs for the required products.</param>
        /// <returns>The number of production ticks needed to deliver every product.</returns>
        private static int RunProduction((bool Hull, int Cost)[] requests)
        {
            AITurnContext initial = CreateProductionContext(
                out Planet weak,
                out Planet strong,
                out Fleet fleet
            );
            GameRoot game = initial.Game;
            IManufacturable[] products = requests
                .Select(
                    (request, index) =>
                    {
                        IManufacturable product = request.Hull
                            ? AITestSceneBuilder.CreateCapitalShip(
                                $"hull-{index}",
                                initial.Faction.InstanceID
                            )
                            : AITestSceneBuilder.CreateStarfighter(
                                $"fighter-{index}",
                                initial.Faction.InstanceID
                            );
                        product.ConstructionCost = request.Cost;
                        return product;
                    }
                )
                .ToArray();
            HashSet<string> started = new();
            for (int tick = 0; tick <= requests.Sum(request => request.Cost); tick++)
            {
                game.CurrentTick = tick;
                AITurnContext context = AITestSceneBuilder.CreateContext(game, initial.Faction);
                List<AIManufactureProposal> ranked = products
                    .Select((product, index) => (product, index))
                    .Where(item => !started.Contains(item.product.GetTypeID()))
                    .Select(item =>
                        CreateRequest(
                            context,
                            fleet,
                            new[] { strong, weak },
                            item.product,
                            100 - item.index
                        )
                    )
                    .ToList();
                foreach (AIManufactureProposal selected in SelectAndExecute(context, ranked))
                    started.Add(selected.Product.GetReference().GetTypeID());
                new ManufacturingTickProcessor(context.Manufacturing, context.Movement).ProcessTick(
                    game
                );
                new MovementTickProcessor(context.Movement).ProcessTick(game);
                int completed = game.GetSceneNodesByType<CapitalShip>()
                    .Cast<IManufacturable>()
                    .Concat(game.GetSceneNodesByType<Starfighter>())
                    .Count(unit =>
                        started.Contains(unit.GetTypeID())
                        && unit.ManufacturingStatus == ManufacturingStatus.Complete
                    );
                if (completed == products.Length)
                    return tick + 1;
            }
            Assert.Fail(
                "The requested fleet products did not finish within their combined construction work."
            );
            return int.MaxValue;
        }

        /// <summary>Creates a single-yard producer, a five-yard producer, and an existing receiving carrier.</summary>
        /// <param name="weak">Receives the single-yard producer.</param>
        /// <param name="strong">Receives the five-yard producer.</param>
        /// <param name="fleet">Receives the destination fleet.</param>
        /// <returns>The production turn context.</returns>
        private static AITurnContext CreateProductionContext(
            out Planet weak,
            out Planet strong,
            out Fleet fleet
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.TickInterval = 1;
            game.Config.AI.Selection.ProductionScheduleSearchNodes = 100000;
            game.Config.Movement.SameSectorMinTransitTicks = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            weak = AITestSceneBuilder.AddPlanet(game, sector, "weak", faction.InstanceID);
            strong = AITestSceneBuilder.AddPlanet(game, sector, "strong", faction.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                weak,
                "weak-yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    strong,
                    $"strong-yard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            fleet = EntityFactory.CreateFleet("fleet", faction.InstanceID);
            game.AttachNode(fleet, weak);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "carrier",
                    faction.InstanceID,
                    starfighterCapacity: 200
                ),
                fleet
            );
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>Creates one scored request that may be assigned to either eligible producer.</summary>
        /// <param name="context">The current production context.</param>
        /// <param name="fleet">The receiving fleet.</param>
        /// <param name="producers">The eligible producer alternatives.</param>
        /// <param name="product">The requested unit template.</param>
        /// <param name="score">The request's ranked score.</param>
        /// <returns>The scored single-unit proposal.</returns>
        private static AIManufactureProposal CreateRequest(
            AITurnContext context,
            Fleet fleet,
            IReadOnlyList<Planet> producers,
            IManufacturable product,
            int score
        )
        {
            AIProductionDemand demand = new(
                product.GetTypeID(),
                product is CapitalShip
                    ? AIProductionDemandKind.FleetCapitalShip
                    : AIProductionDemandKind.FleetStarfighter,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1
            );
            AIManufactureProposal request = new(demand, producers, new Technology(product), true);
            request.SetScore(score);
            return request;
        }

        /// <summary>Resolves all ranked actions before executing any construction action.</summary>
        /// <param name="context">The current production context.</param>
        /// <param name="ranked">The fixed ranked requests.</param>
        /// <returns>The executable actions selected by the scheduling contract.</returns>
        private static List<AIManufactureProposal> SelectAndExecute(
            AITurnContext context,
            IEnumerable<AIManufactureProposal> ranked
        )
        {
            AIProductionSelector selector = new(new AISelectionState());
            List<AIManufactureProposal> selected = new();
            foreach (
                AIManufactureProposal proposal in selector
                    .ScheduleProduction(context, ranked, 0)
                    .OfType<AIManufactureProposal>()
            )
            {
                if (
                    !selector.TryResolve(context, proposal, out AIManufactureProposal action)
                    || !action.CanSelect(context)
                    || !selector.CanReserve(context, action)
                )
                    continue;
                selector.Reserve(action);
                selected.Add(action);
            }
            foreach (AIManufactureProposal action in selected)
                action.Execute(context);
            return selected;
        }
    }
}
