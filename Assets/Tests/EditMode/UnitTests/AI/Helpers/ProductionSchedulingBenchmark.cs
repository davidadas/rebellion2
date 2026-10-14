using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.AI.Helpers
{
    /// <summary>Compares production scheduling against a native solver without changing live AI decisions.</summary>
    public static partial class ProductionSchedulingBenchmark
    {
        private sealed class Scenario
        {
            public AITurnContext Context;
            public List<AIManufactureProposal> Requests = new();
            public long SearchNodes;
            public bool SearchCompleted = true;
        }

        private sealed class Delivery
        {
            public int Request;
            public string Lane;
            public int Start;
            public int Completion = -1;
            public int Arrival = -1;
            public IManufacturable Unit;
        }

        private sealed class Solution
        {
            public List<Delivery> Orders;
            public List<string> Statuses = new();
            public int ProvenPriorityLevels;
            public double Seconds;
        }

        /// <summary>Runs explicit offline scheduling cases through both native optimization and actual production ticks.</summary>
        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int budget = int.Parse(GetArgument(args, "-productionSolveMilliseconds", "1000"));
                if (budget <= 0)
                    throw new ArgumentOutOfRangeException(nameof(budget));
                string output = Path.GetFullPath(
                    GetArgument(
                        args,
                        "-productionOut",
                        "SimulationResults/production-benchmark.txt"
                    )
                );
                StringBuilder report = new();
                report.AppendLine(
                    $"Offline production benchmark; solve budget={budget} ms; existing work retained; one item per lane."
                );
                report.AppendLine(
                    "Objective: earliest readiness in existing fleet priority order, followed by total delivery time. No battles, missions, or changing production requirements."
                );
                bool valid = true;
                foreach (
                    string name in new[]
                    {
                        "mixed-products",
                        "prioritized-fleets",
                        "priority-tie",
                        "occupied-yard",
                        "upcoming-yard",
                        "cycle-phases",
                        "fractional-cycles",
                        "building-yards",
                        "carrier-gated",
                        "regiments",
                        "zero-cost",
                        "delivery",
                    }
                )
                {
                    Scenario baselineScene = CreateScenario(name);
                    List<Delivery> baseline = Replay(baselineScene, null);
                    Scenario oracleScene = CreateScenario(name);
                    Solution oracle = Solve(oracleScene, baseline, budget);
                    report.AppendLine(
                        $"\n{name}: baseline readiness={string.Join(",", GetReadiness(baselineScene, baseline))}; managed search nodes={baselineScene.SearchNodes}; managed search completed={baselineScene.SearchCompleted}; solver statuses={string.Join(",", oracle.Statuses)}; proven priority levels={oracle.ProvenPriorityLevels}; elapsed={oracle.Seconds:F3}s"
                    );
                    if (oracle.Orders == null)
                    {
                        report.AppendLine(
                            "Unresolved: no native solution within the budget. Baseline is not being presented as a native optimum."
                        );
                        continue;
                    }
                    Scenario replayScene = CreateScenario(name);
                    List<Delivery> actual = Replay(replayScene, oracle.Orders);
                    report.AppendLine(
                        $"Native readiness forecast={string.Join(",", GetReadiness(oracleScene, oracle.Orders))}; actual readiness={string.Join(",", GetReadiness(replayScene, actual))}"
                    );
                    if (
                        oracle.ProvenPriorityLevels == oracle.Statuses.Count - 1
                        && !GetReadiness(baselineScene, baseline)
                            .SequenceEqual(GetReadiness(oracleScene, oracle.Orders))
                    )
                    {
                        valid = false;
                        report.AppendLine(
                            "The production selector did not match the proven fleet-readiness optimum."
                        );
                    }
                    foreach (Delivery expected in oracle.Orders.OrderBy(order => order.Request))
                    {
                        Delivery observed = actual.Single(order =>
                            order.Request == expected.Request
                        );
                        bool match =
                            expected.Start == observed.Start
                            && expected.Completion == observed.Completion
                            && expected.Arrival == observed.Arrival;
                        valid &= match;
                        report.AppendLine(
                            $"request={expected.Request} fleet={oracleScene.Requests[expected.Request].Destination.InstanceID} product={oracleScene.Requests[expected.Request].Product.GetReference().GetTypeID()} lane={expected.Lane} forecast={expected.Start}/{expected.Completion}/{expected.Arrival} actual={observed.Start}/{observed.Completion}/{observed.Arrival} match={match}"
                        );
                    }
                }
                report.AppendLine(
                    $"\nIndependent production replay valid={valid}. Unresolved cases are not proofs of optimality."
                );
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, report.ToString());
                UnityEngine.Debug.Log(report.ToString());
                UnityEditor.EditorApplication.Exit(valid ? 0 : 1);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                UnityEditor.EditorApplication.Exit(1);
            }
        }

        /// <summary>Reads a command-line benchmark option.</summary>
        /// <param name="args">Process arguments.</param>
        /// <param name="flag">The requested option.</param>
        /// <param name="fallback">The explicit default.</param>
        /// <returns>The supplied value or default.</returns>
        private static string GetArgument(string[] args, string flag, string fallback)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        /// <summary>Creates identical independent worlds for the scheduler and production replay.</summary>
        /// <param name="name">The production case.</param>
        /// <returns>Its fixed requests and actual world state.</returns>
        private static Scenario CreateScenario(string name)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out var faction, out var _);
            game.Config.AI.TickInterval = 1;
            if (name == "fractional-cycles")
            {
                game.SetFactionController(
                    faction.InstanceID,
                    "benchmark-ai",
                    PlayerControllerType.AI
                );
                game.Config.DifficultyModifiers[game.Summary.Difficulty] = new DifficultyModifiers
                {
                    ManufacturingSpeedPercent = 75,
                };
            }
            game.Config.Movement.SameSectorMinTransitTicks = name == "delivery" ? 3 : 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet weak = AITestSceneBuilder.AddPlanet(game, sector, "weak", faction.InstanceID);
            Planet strong = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "strong",
                faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                weak,
                "weak-yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                name == "cycle-phases" ? 2 : 1
            );
            int yardCount = name is "upcoming-yard" or "building-yards" ? 1 : 5;
            for (int index = 0; index < yardCount; index++)
            {
                Building yard = AITestSceneBuilder.AddProductionFacility(
                    game,
                    strong,
                    $"strong-yard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    name == "cycle-phases" ? 2 : 1
                );
                if (name == "cycle-phases")
                {
                    yard.ProductionCycleProgress = index % 2;
                    yard.ProductionPointReady = index == 0;
                }
            }
            Fleet[] fleets = Enumerable
                .Range(0, name is "prioritized-fleets" or "priority-tie" ? 2 : 1)
                .Select(index =>
                {
                    Fleet fleet = EntityFactory.CreateFleet($"fleet-{index}", faction.InstanceID);
                    game.AttachNode(fleet, weak);
                    if (name != "carrier-gated")
                        game.AttachNode(
                            AITestSceneBuilder.CreateCapitalShip(
                                $"carrier-{index}",
                                faction.InstanceID,
                                regimentCapacity: 200,
                                starfighterCapacity: 200
                            ),
                            fleet
                        );
                    return fleet;
                })
                .ToArray();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            if (name == "regiments")
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    weak,
                    "weak-training",
                    BuildingType.TrainingFacility,
                    ManufacturingType.Troop
                );
                for (int index = 0; index < 3; index++)
                    AITestSceneBuilder.AddProductionFacility(
                        game,
                        strong,
                        $"strong-training-{index}",
                        BuildingType.TrainingFacility,
                        ManufacturingType.Troop
                    );
            }
            if (name == "building-yards")
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    weak,
                    "construction",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
                for (int index = 0; index < 4; index++)
                {
                    Building yard = AITestSceneBuilder.CreateBuildingTemplate(
                        $"new-yard-{index}",
                        BuildingType.Shipyard,
                        ManufacturingType.Ship
                    );
                    yard.OwnerInstanceID = faction.InstanceID;
                    yard.ConstructionCost = 4;
                    if (!context.Manufacturing.Enqueue(weak, yard, strong))
                        throw new InvalidOperationException(
                            "Cannot prepare committed facility construction."
                        );
                }
            }
            if (name == "occupied-yard")
            {
                CapitalShip existing = AITestSceneBuilder.CreateCapitalShip(
                    "existing",
                    faction.InstanceID
                );
                existing.ConstructionCost = 20;
                if (!context.Manufacturing.Enqueue(strong, existing, fleets[0]))
                    throw new InvalidOperationException("Cannot prepare committed construction.");
                existing.ManufacturingProgress = 10;
            }
            if (name == "upcoming-yard")
            {
                for (int index = 0; index < 4; index++)
                {
                    Building yard = AITestSceneBuilder.AddProductionFacility(
                        game,
                        strong,
                        $"incoming-yard-{index}",
                        BuildingType.Shipyard,
                        ManufacturingType.Ship
                    );
                    yard.ManufacturingStatus = ManufacturingStatus.Delivering;
                    yard.Movement = new MovementState { TransitTicks = 8 };
                }
            }
            (bool Hull, int Cost)[] products = name switch
            {
                "mixed-products" => new[]
                {
                    (true, 38),
                    (true, 36),
                    (false, 28),
                    (false, 22),
                    (true, 13),
                    (false, 32),
                    (true, 3),
                },
                "prioritized-fleets" => new[]
                {
                    (true, 50),
                    (true, 31),
                    (false, 42),
                    (true, 28),
                    (false, 42),
                    (false, 46),
                    (true, 32),
                    (false, 23),
                },
                "occupied-yard" => new[] { (true, 100), (false, 5) },
                "priority-tie" => new[] { (true, 0), (true, 100) },
                "upcoming-yard" or "building-yards" => new[]
                {
                    (true, 100),
                    (false, 5),
                    (true, 30),
                },
                "zero-cost" => new[] { (true, 0), (false, 0), (true, 1), (false, 1) },
                _ => new[] { (true, 38), (false, 5), (true, 14), (false, 7) },
            };
            Scenario scenario = new() { Context = context };
            for (int index = 0; index < products.Length; index++)
            {
                IManufacturable product =
                    products[index].Hull
                        ? AITestSceneBuilder.CreateCapitalShip(
                            $"product-{index}",
                            faction.InstanceID
                        )
                    : name == "regiments"
                        ? AITestSceneBuilder.CreateRegiment($"product-{index}", faction.InstanceID)
                    : AITestSceneBuilder.CreateStarfighter($"product-{index}", faction.InstanceID);
                product.ConstructionCost = products[index].Cost;
                if (product is CapitalShip ship)
                    ship.Hyperdrive = 60;
                Fleet fleet =
                    fleets.Length > 1 && index >= (name == "priority-tie" ? 1 : 3)
                        ? fleets[1]
                        : fleets[0];
                AIProductionDemand demand = new(
                    product.GetTypeID(),
                    products[index].Hull ? AIProductionDemandKind.FleetCapitalShip
                        : product is Regiment ? AIProductionDemandKind.FleetRegiment
                        : AIProductionDemandKind.FleetStarfighter,
                    product.GetManufacturingType(),
                    BuildingType.None,
                    fleet,
                    1
                );
                AIManufactureProposal request = new(
                    demand,
                    new[] { strong, weak },
                    new Technology(product),
                    true
                );
                request.SetScore(100 - index);
                scenario.Requests.Add(request);
            }
            return scenario;
        }

        /// <summary>Runs actual manufacturing and movement, without replacing them with forecast arithmetic.</summary>
        /// <param name="scenario">A fresh copy of the fixed production case.</param>
        /// <param name="schedule">Native starts to replay, or null to run the production selector.</param>
        /// <returns>Observed start, completion, and arrival dates for each requested unit.</returns>
        private static List<Delivery> Replay(Scenario scenario, List<Delivery> schedule)
        {
            AITurnContext initial = scenario.Context;
            GameRoot game = initial.Game;
            List<Delivery> deliveries = new();
            Dictionary<string, int> committedProgress = game.GetSceneNodesByType<CapitalShip>()
                .Where(ship => ship.ManufacturingStatus == ManufacturingStatus.Building)
                .ToDictionary(ship => ship.InstanceID, ship => ship.ManufacturingProgress);
            int limit =
                scenario.Requests.Sum(request =>
                    Math.Max(1, request.Product.GetReference().GetConstructionCost())
                ) * 10
                + 100;
            for (int tick = 0; tick < limit; tick++)
            {
                game.CurrentTick = tick;
                AITurnContext context = AITestSceneBuilder.CreateContext(
                    game,
                    initial.Faction,
                    movement: initial.Movement,
                    manufacturing: initial.Manufacturing
                );
                HashSet<int> started = deliveries.Select(delivery => delivery.Request).ToHashSet();
                List<(int Index, AIManufactureProposal Action)> actions = new();
                if (schedule == null)
                {
                    AIProductionSelector selector = new(new AISelectionState());
                    var remaining = scenario
                        .Requests.Where((_, index) => !started.Contains(index))
                        .ToList();
                    foreach (
                        AIManufactureProposal request in selector
                            .ScheduleProduction(context, remaining, 0)
                            .Cast<AIManufactureProposal>()
                    )
                    {
                        if (
                            !selector.TryResolve(context, request, out var action)
                            || !action.CanSelect(context)
                            || !selector.CanReserve(context, action)
                        )
                            continue;
                        selector.Reserve(action);
                        actions.Add((scenario.Requests.IndexOf(request), action));
                    }
                    scenario.SearchNodes += selector.ScheduleSearchNodes;
                    scenario.SearchCompleted &= selector.ScheduleSearchCompleted;
                }
                else
                {
                    foreach (Delivery planned in schedule.Where(order => order.Start == tick))
                    {
                        AIManufactureProposal request = scenario.Requests[planned.Request];
                        AIManufactureProposal action = new[] { request }
                            .Concat(request.ProducerAlternatives)
                            .Single(option => option.GetProducerCapacityKey() == planned.Lane);
                        if (!action.CanSelect(context))
                            throw new InvalidOperationException(
                                $"Native start rejected at tick {tick}: request {planned.Request}, lane {planned.Lane}."
                            );
                        actions.Add((planned.Request, action));
                    }
                }
                foreach (var action in actions)
                {
                    Planet producer = action.Action.ProducerPlanet;
                    ManufacturingType type = action
                        .Action.Product.GetReference()
                        .GetManufacturingType();
                    if (
                        producer.GetManufacturingQueue().TryGetValue(type, out var queue)
                        && queue.Count > 0
                    )
                        throw new InvalidOperationException(
                            "A schedule attempted to replace committed construction."
                        );
                    action.Action.Execute(context);
                    IManufacturable unit = producer.GetManufacturingQueue()[type].Single();
                    deliveries.Add(
                        new Delivery
                        {
                            Request = action.Index,
                            Lane = action.Action.GetProducerCapacityKey(),
                            Start = tick,
                            Unit = unit,
                        }
                    );
                }
                new ManufacturingTickProcessor(initial.Manufacturing, initial.Movement).ProcessTick(
                    game
                );
                foreach (
                    Delivery delivery in deliveries.Where(delivery =>
                        delivery.Completion < 0
                        && delivery.Unit.ManufacturingStatus != ManufacturingStatus.Building
                    )
                )
                    delivery.Completion = tick + 1;
                new MovementTickProcessor(initial.Movement).ProcessTick(game);
                foreach (
                    Delivery delivery in deliveries.Where(delivery =>
                        delivery.Arrival < 0
                        && delivery.Unit.ManufacturingStatus == ManufacturingStatus.Complete
                        && delivery.Unit.Movement == null
                    )
                )
                    delivery.Arrival = tick + 1;
                foreach (var committed in committedProgress)
                {
                    CapitalShip ship = game.GetSceneNodeByInstanceID<CapitalShip>(committed.Key);
                    if (ship == null || ship.ManufacturingProgress < committed.Value)
                        throw new InvalidOperationException(
                            "Committed construction was lost or restarted."
                        );
                }
                if (
                    deliveries.Count == scenario.Requests.Count
                    && deliveries.All(delivery => delivery.Arrival >= 0)
                )
                    return deliveries;
            }
            throw new InvalidOperationException(
                "The fixed production requests did not finish within the benchmark horizon."
            );
        }

        /// <summary>Reports the completion of every required item for each prioritized fleet.</summary>
        /// <param name="scenario">The fixed requests.</param>
        /// <param name="deliveries">Forecast or observed deliveries.</param>
        /// <returns>Fleet readiness in priority order.</returns>
        private static int[] GetReadiness(Scenario scenario, IReadOnlyList<Delivery> deliveries)
        {
            return scenario
                .Requests.Select(request => request.Destination)
                .Distinct()
                .Select(fleet =>
                    deliveries
                        .Where(delivery => scenario.Requests[delivery.Request].Destination == fleet)
                        .Max(delivery => delivery.Arrival)
                )
                .ToArray();
        }
    }
}
