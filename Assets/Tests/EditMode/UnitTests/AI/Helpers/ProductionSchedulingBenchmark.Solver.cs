using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Google.OrTools.Sat;
using Rebellion.AI;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.AI.Helpers
{
    public static partial class ProductionSchedulingBenchmark
    {
        private static readonly Dictionary<
            (int Rate, double Phase, bool Ready, double Increment, int Horizon),
            long[]
        > _schedulingPointForecasts = new();

        /// <summary>Schedules all fleet requests jointly, refining readiness in existing priority order.</summary>
        /// <param name="scenario">The fixed production inputs.</param>
        /// <param name="baseline">A schedule observed through actual manufacturing.</param>
        /// <param name="budget">Total milliseconds available to optimize the snapshot.</param>
        /// <returns>The native schedule and explicit feasibility and optimality outcomes.</returns>
        private static Solution Solve(Scenario scenario, List<Delivery> baseline, int budget)
        {
            AITurnContext context = scenario.Context;
            var requests = scenario.Requests;
            Fleet[] fleets = requests
                .Select(request => (Fleet)request.Destination)
                .Distinct()
                .ToArray();
            var groups = requests
                .Select(request =>
                    (
                        Product: request.Product.GetReference(),
                        Fleet: Array.IndexOf(fleets, request.Destination),
                        Options: new[] { request }
                            .Concat(request.ProducerAlternatives)
                            .Where(option => option.CanPlan(context))
                            .ToDictionary(option => option.GetProducerCapacityKey())
                    )
                )
                .ToArray();
            var producers = groups
                .SelectMany(group => group.Options)
                .GroupBy(option => option.Key)
                .ToDictionary(group => group.Key, group => group.First().Value.ProducerPlanet);
            var types = groups
                .SelectMany(group => group.Options)
                .GroupBy(option => option.Key)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Value.Product.GetReference().GetManufacturingType()
                );
            var hints = producers.ToDictionary(
                producer => producer.Key,
                producer =>
                    baseline
                        .Where(order => order.Lane == producer.Key)
                        .OrderBy(order => order.Start)
                        .ToList()
            );
            int horizon = baseline.Max(order => order.Arrival);
            var model = new CpModel();
            var completion = fleets
                .Select(_ => model.NewIntVar(0, horizon, "fleet-readiness"))
                .ToArray();
            var selected = groups.Select(_ => new List<BoolVar>()).ToArray();
            var slots =
                new List<(
                    string Lane,
                    IntVar Start,
                    IntVar End,
                    Dictionary<int, BoolVar> Choices
                )>();
            var arrivals = new List<IntVar>();
            var fighterCapacity = new CumulativeConstraint[fleets.Length];
            var regimentCapacity = new CumulativeConstraint[fleets.Length];
            for (int fleetIndex = 0; fleetIndex < fleets.Length; fleetIndex++)
            {
                var fighterBerths = GetCarrierCapacity(context, fleets[fleetIndex], true);
                var regimentBerths = GetCarrierCapacity(context, fleets[fleetIndex], false);
                fighterCapacity[fleetIndex] = model.AddCumulative(
                    fighterBerths.Sum(carrier => (long)carrier.Capacity)
                        + groups
                            .Where(group => group.Fleet == fleetIndex)
                            .Sum(group =>
                                group.Product is CapitalShip ship
                                    ? (long)ship.StarfighterCapacity
                                    : 0
                            )
                );
                regimentCapacity[fleetIndex] = model.AddCumulative(
                    regimentBerths.Sum(carrier => (long)carrier.Capacity)
                        + groups
                            .Where(group => group.Fleet == fleetIndex)
                            .Sum(group =>
                                group.Product is CapitalShip ship ? (long)ship.RegimentCapacity : 0
                            )
                );
                foreach (var carrier in fighterBerths)
                    fighterCapacity[fleetIndex]
                        .AddDemand(
                            model.NewFixedSizeIntervalVar(
                                0,
                                Math.Min((long)horizon + 1, carrier.Ready),
                                "unavailable-fighter-berths"
                            ),
                            carrier.Capacity
                        );
                foreach (var carrier in regimentBerths)
                    regimentCapacity[fleetIndex]
                        .AddDemand(
                            model.NewFixedSizeIntervalVar(
                                0,
                                Math.Min((long)horizon + 1, carrier.Ready),
                                "unavailable-regiment-berths"
                            ),
                            carrier.Capacity
                        );
            }
            _schedulingPointForecasts.Clear();
            foreach (var producer in producers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var forecast = context.ManufacturingQueries.GetProductionCapacityForecast(
                    producer.Value,
                    types[producer.Key]
                );
                if (!forecast.HasValue)
                    continue;
                var capacity = forecast.Value;
                var facilities = capacity
                    .Facilities.Where(facility => facility.ActivationTicks < horizon)
                    .GroupBy(facility => facility)
                    .Select(group => (Facility: group.Key, Count: group.Count()))
                    .ToArray();
                if (facilities.Length == 0)
                    continue;
                var eligible = Enumerable
                    .Range(0, groups.Length)
                    .Where(index => groups[index].Options.ContainsKey(producer.Key))
                    .ToArray();
                int minimumCost = eligible.Min(index =>
                    Math.Max(0, groups[index].Product.GetConstructionCost())
                );
                int minimumDuration = GetSchedulingDurationBound(
                    facilities.Select(group =>
                        (group.Facility.Rate, group.Facility.Ready, group.Count)
                    ),
                    capacity.Increment,
                    minimumCost
                );
                int slotCount = Math.Min(
                    eligible.Length,
                    Math.Max(0, horizon - capacity.AvailableTick) / minimumDuration
                );
                int[] activations = facilities
                    .Select(group => group.Facility.ActivationTicks)
                    .Distinct()
                    .ToArray();
                var counters = activations
                    .Select(_ => model.NewIntVar(0, 0, "initial-active-ticks"))
                    .ToArray();
                IntVar previousEnd = model.NewIntVar(
                    capacity.AvailableTick,
                    capacity.AvailableTick,
                    "lane-available"
                );
                BoolVar previousPresent = null;
                var workAssigned = new List<LinearExpr>();
                for (int position = 0; position < slotCount; position++)
                {
                    var present = model.NewBoolVar("slot-present");
                    if (previousPresent != null)
                        model.Add(present <= previousPresent);
                    previousPresent = present;
                    var choices = eligible.ToDictionary(
                        index => index,
                        index => model.NewBoolVar($"product-{index}")
                    );
                    model.Add(LinearExpr.Sum(choices.Values) == present);
                    var work = LinearExpr.WeightedSum(
                        choices.Values,
                        eligible.Select(index =>
                            (long)Math.Max(0, groups[index].Product.GetConstructionCost())
                        )
                    );
                    workAssigned.Add(work);
                    var start = model.NewIntVar(capacity.AvailableTick, horizon, "start");
                    var end = model.NewIntVar(capacity.AvailableTick, horizon, "end");
                    model.Add(start >= previousEnd);
                    model.Add(end == start).OnlyEnforceIf(present.Not());
                    model.Add(end >= start + minimumDuration).OnlyEnforceIf(present);
                    model
                        .Add(start >= facilities.Min(group => group.Facility.ActivationTicks))
                        .OnlyEnforceIf(present);
                    int interval = Math.Max(1, context.Game.Config.AI.TickInterval);
                    var decision = model.NewIntVar(0, horizon / interval, "decision");
                    model.Add(start == decision * interval).OnlyEnforceIf(present);
                    foreach (int index in eligible)
                    {
                        var group = groups[index];
                        selected[index].Add(choices[index]);
                        model
                            .Add(
                                end
                                    >= start
                                        + GetSchedulingDurationBound(
                                            facilities.Select(facility =>
                                                (
                                                    facility.Facility.Rate,
                                                    facility.Facility.Ready,
                                                    facility.Count
                                                )
                                            ),
                                            capacity.Increment,
                                            group.Product.GetConstructionCost()
                                        )
                            )
                            .OnlyEnforceIf(choices[index]);
                        int? transit = EstimateTransit(context, group.Options[producer.Key]);
                        if (!transit.HasValue)
                        {
                            model.Add(choices[index] == 0);
                            continue;
                        }
                        var arrival = model.NewIntVar(0, horizon + (long)transit.Value, "arrival");
                        model.Add(arrival == end + transit.Value).OnlyEnforceIf(choices[index]);
                        model.Add(arrival == 0).OnlyEnforceIf(choices[index].Not());
                        model.Add(completion[group.Fleet] >= arrival);
                        arrivals.Add(arrival);
                        if (group.Product is CapitalShip ship)
                        {
                            var unavailable = model.NewOptionalIntervalVar(
                                0,
                                arrival,
                                arrival,
                                choices[index],
                                "carrier-unavailable"
                            );
                            if (ship.StarfighterCapacity > 0)
                                fighterCapacity[group.Fleet]
                                    .AddDemand(
                                        unavailable,
                                        LinearExpr.Constant(ship.StarfighterCapacity)
                                    );
                            if (ship.RegimentCapacity > 0)
                                regimentCapacity[group.Fleet]
                                    .AddDemand(
                                        unavailable,
                                        LinearExpr.Constant(ship.RegimentCapacity)
                                    );
                        }
                        else if (group.Product is Starfighter)
                            fighterCapacity[group.Fleet]
                                .AddDemand(
                                    model.NewOptionalIntervalVar(
                                        start,
                                        horizon + 1L - start,
                                        horizon + 1L,
                                        choices[index],
                                        "reserved-fighter-berth"
                                    ),
                                    LinearExpr.Constant(1)
                                );
                        else if (group.Product is Regiment)
                            regimentCapacity[group.Fleet]
                                .AddDemand(
                                    model.NewOptionalIntervalVar(
                                        start,
                                        horizon + 1L - start,
                                        horizon + 1L,
                                        choices[index],
                                        "reserved-regiment-berth"
                                    ),
                                    LinearExpr.Constant(1)
                                );
                    }
                    var after = new IntVar[activations.Length];
                    var previousCounters = new IntVar[activations.Length];
                    for (int activation = 0; activation < activations.Length; activation++)
                    {
                        var firstActive = model.NewIntVar(0, horizon, "first-active");
                        model.AddMaxEquality(
                            firstActive,
                            new LinearExpr[] { start, LinearExpr.Constant(activations[activation]) }
                        );
                        var duration = model.NewIntVar(0, horizon, "active-duration");
                        model.AddMaxEquality(
                            duration,
                            new LinearExpr[] { LinearExpr.Constant(0), end - firstActive }
                        );
                        after[activation] = model.NewIntVar(0, horizon, "active-after");
                        model.Add(after[activation] == counters[activation] + duration);
                        var priorDuration = model.NewIntVar(0, horizon, "prior-duration");
                        model.AddMaxEquality(
                            priorDuration,
                            new LinearExpr[] { LinearExpr.Constant(0), end - 1 - firstActive }
                        );
                        previousCounters[activation] = model.NewIntVar(
                            0,
                            horizon,
                            "active-before-last-tick"
                        );
                        model.Add(
                            previousCounters[activation] == counters[activation] + priorDuration
                        );
                    }
                    var supplied = new List<LinearExpr>();
                    var suppliedBeforeLastTick = new List<LinearExpr>();
                    foreach (var facility in facilities)
                    {
                        int activation = Array.IndexOf(
                            activations,
                            facility.Facility.ActivationTicks
                        );
                        var beforePoints = AddSchedulingPoints(
                            model,
                            counters[activation],
                            facility.Facility,
                            capacity.Increment,
                            horizon
                        );
                        var afterPoints = AddSchedulingPoints(
                            model,
                            after[activation],
                            facility.Facility,
                            capacity.Increment,
                            horizon
                        );
                        var previousPoints = AddSchedulingPoints(
                            model,
                            previousCounters[activation],
                            facility.Facility,
                            capacity.Increment,
                            horizon
                        );
                        supplied.Add((afterPoints - beforePoints) * facility.Count);
                        suppliedBeforeLastTick.Add(
                            (previousPoints - beforePoints) * facility.Count
                        );
                    }
                    model.Add(LinearExpr.Sum(supplied) >= work).OnlyEnforceIf(present);
                    var positiveWork = model.NewBoolVar("positive-work");
                    model.Add(work > 0).OnlyEnforceIf(positiveWork);
                    model.Add(work == 0).OnlyEnforceIf(positiveWork.Not());
                    model
                        .Add(LinearExpr.Sum(suppliedBeforeLastTick) < work)
                        .OnlyEnforceIf(new ILiteral[] { present, positiveWork });
                    for (int index = 0; index < eligible.Length; index++)
                        if (groups[eligible[index]].Product.GetConstructionCost() <= 0)
                            model.Add(end == start + 1).OnlyEnforceIf(choices[eligible[index]]);
                    if (position < hints[producer.Key].Count)
                    {
                        var hint = hints[producer.Key][position];
                        model.AddHint(present, 1);
                        model.AddHint(start, hint.Start);
                        model.AddHint(end, hint.Completion);
                        foreach (var choice in choices)
                            model.AddHint(choice.Value, choice.Key == hint.Request ? 1 : 0);
                    }
                    else
                    {
                        model.AddHint(present, 0);
                        foreach (var choice in choices)
                            model.AddHint(choice.Value, 0);
                    }
                    slots.Add((producer.Key, start, end, choices));
                    previousEnd = end;
                    counters = after;
                }
                var maximumOutput = new List<LinearExpr>();
                foreach (var facility in facilities)
                {
                    var active = model.NewIntVar(0, horizon, "maximum-active-ticks");
                    model.AddMaxEquality(
                        active,
                        new LinearExpr[]
                        {
                            LinearExpr.Constant(0),
                            previousEnd
                                - Math.Max(
                                    capacity.AvailableTick,
                                    facility.Facility.ActivationTicks
                                ),
                        }
                    );
                    maximumOutput.Add(
                        AddSchedulingPoints(
                            model,
                            active,
                            facility.Facility,
                            capacity.Increment,
                            horizon
                        ) * facility.Count
                    );
                }
                model.Add(LinearExpr.Sum(workAssigned) <= LinearExpr.Sum(maximumOutput));
            }

            for (int index = 0; index < groups.Length; index++)
                model.Add(LinearExpr.Sum(selected[index]) == 1);
            Solution result = new();
            Stopwatch watch = Stopwatch.StartNew();
            for (int stage = 0; stage <= fleets.Length; stage++)
            {
                double remaining = (budget - watch.Elapsed.TotalMilliseconds) / 1000;
                if (remaining <= 0)
                {
                    result.Statuses.Add("Budget exhausted before next priority level");
                    break;
                }
                if (stage < fleets.Length)
                    model.Minimize(completion[stage]);
                else
                    model.Minimize(LinearExpr.Sum(arrivals));
                CpSolver solver = new()
                {
                    StringParameters =
                        $"num_search_workers:1 max_time_in_seconds:{remaining.ToString(CultureInfo.InvariantCulture)} random_seed:12345",
                };
                CpSolverStatus status = solver.Solve(model);
                result.Statuses.Add(status.ToString());
                if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
                {
                    if (status == CpSolverStatus.ModelInvalid)
                        throw new InvalidOperationException(solver.Response.SolutionInfo);
                    break;
                }
                result.Orders = new();
                foreach (var slot in slots)
                {
                    var choice = slot.Choices.FirstOrDefault(choice =>
                        solver.BooleanValue(choice.Value)
                    );
                    if (choice.Value == null)
                        continue;
                    var action = groups[choice.Key].Options[slot.Lane];
                    result.Orders.Add(
                        new Delivery
                        {
                            Request = choice.Key,
                            Lane = slot.Lane,
                            Start = (int)solver.Value(slot.Start),
                            Completion = (int)solver.Value(slot.End),
                            Arrival =
                                (int)solver.Value(slot.End)
                                + EstimateTransit(context, action).Value,
                        }
                    );
                }
                if (status != CpSolverStatus.Optimal)
                    break;
                if (stage < fleets.Length)
                {
                    result.ProvenPriorityLevels++;
                    model.Add(completion[stage] == solver.Value(completion[stage]));
                }
                model.Model.SolutionHint = new PartialVariableAssignment();
                for (int index = 0; index < solver.Response.Solution.Count; index++)
                {
                    model.Model.SolutionHint.Vars.Add(index);
                    model.Model.SolutionHint.Values.Add(solver.Response.Solution[index]);
                }
            }
            result.Seconds = watch.Elapsed.TotalSeconds;
            return result;
        }

        /// <summary>Counts unoccupied berths and dates at which committed carriers supply them.</summary>
        /// <param name="context">The frozen faction state.</param>
        /// <param name="fleet">The receiving fleet.</param>
        /// <param name="fighter">Whether to inspect starfighter rather than regiment capacity.</param>
        /// <returns>Committed berth capacities and availability dates.</returns>
        private static List<(int Ready, int Capacity)> GetCarrierCapacity(
            AITurnContext context,
            Fleet fleet,
            bool fighter
        )
        {
            List<(int Ready, int Capacity)> capacities = new();
            foreach (CapitalShip ship in fleet.GetChildren<CapitalShip>())
            {
                int capacity = fighter
                    ? ship.GetExcessStarfighterCapacity()
                    : ship.GetExcessRegimentCapacity();
                if (capacity <= 0)
                    continue;
                int ready = 0;
                if (ship.Movement != null)
                    ready = ship.Movement.TicksRemaining();
                else if (ship.ManufacturingStatus != ManufacturingStatus.Complete)
                {
                    Planet producer = context.Game.GetSceneNodeByInstanceID<Planet>(
                        ship.ProducerPlanetID
                    );
                    int? production = ManufacturingQueries.EstimateCompletionTicks(producer, ship);
                    if (
                        !production.HasValue
                        || !context.MovementQueries.TryEstimateManufacturedTransitTicks(
                            ship,
                            producer,
                            fleet,
                            out int transit,
                            context.Faction.InstanceID
                        )
                    )
                        continue;
                    ready = production.Value + Math.Max(0, transit - 1);
                }
                capacities.Add((ready, capacity));
            }
            return capacities;
        }

        /// <summary>Accounts for the movement tick which follows manufacturing completion.</summary>
        /// <param name="context">The frozen world state.</param>
        /// <param name="action">The exact producer and destination.</param>
        /// <returns>Additional movement ticks after completion, or null when delivery is unavailable.</returns>
        private static int? EstimateTransit(AITurnContext context, AIManufactureProposal action)
        {
            return context.MovementQueries.TryEstimateManufacturedTransitTicks(
                action.Product.GetReference(),
                action.ProducerPlanet,
                action.Destination,
                out int ticks,
                context.Faction.InstanceID
            )
                ? Math.Max(0, ticks - 1)
                : null;
        }

        /// <summary>Maps cumulative active ticks to facility output without rounding its cycle increment.</summary>
        /// <param name="model">The native scheduling model.</param>
        /// <param name="counter">Active manufacturing ticks.</param>
        /// <param name="facility">The projected phase and ready state.</param>
        /// <param name="increment">Cycle progress per active tick.</param>
        /// <param name="horizon">The active-time upper bound.</param>
        /// <returns>The cumulative production-point variable.</returns>
        private static IntVar AddSchedulingPoints(
            CpModel model,
            IntVar counter,
            (int Rate, double CycleProgress, bool Ready, int ActivationTicks) facility,
            double increment,
            int horizon
        )
        {
            var output = model.NewIntVar(
                0,
                ManufacturingQueries.GetFacilityProductionPoints(
                    facility.Rate,
                    facility.CycleProgress,
                    facility.Ready,
                    increment,
                    horizon
                ),
                "points"
            );
            if (
                !facility.Ready
                && facility.CycleProgress >= 0
                && facility.CycleProgress < facility.Rate
                && increment == (int)increment
                && facility.CycleProgress == (int)facility.CycleProgress
            )
                model.AddDivisionEquality(
                    output,
                    counter * (long)increment + (long)facility.CycleProgress,
                    facility.Rate
                );
            else
            {
                var key = (
                    facility.Rate,
                    facility.CycleProgress,
                    facility.Ready,
                    increment,
                    horizon
                );
                if (!_schedulingPointForecasts.TryGetValue(key, out long[] points))
                {
                    points = ManufacturingQueries
                        .GetProductionPointForecast(
                            facility.Rate,
                            facility.CycleProgress,
                            facility.Ready,
                            increment,
                            horizon
                        )
                        .ToArray();
                    _schedulingPointForecasts.Add(key, points);
                }
                model.AddElement(counter, points, output);
            }
            return output;
        }

        /// <summary>Bounds work duration using the best possible facility phases.</summary>
        /// <param name="facilities">Facility rates, initial ready flags, and multiplicities.</param>
        /// <param name="increment">Production progress per tick.</param>
        /// <param name="work">Required manufacturing points.</param>
        /// <returns>A positive lower bound on active work duration.</returns>
        private static int GetSchedulingDurationBound(
            IEnumerable<(int Rate, bool Ready, int Count)> facilities,
            double increment,
            int work
        )
        {
            int duration = 1;
            while (
                facilities.Sum(facility =>
                    (
                        (long)Math.Ceiling(duration * increment / facility.Rate)
                        + (facility.Ready ? 1 : 0)
                    ) * facility.Count
                ) < work
            )
                duration++;
            return duration;
        }
    }
}
