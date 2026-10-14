using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Assigns ranked production requests to lanes and selects immediately executable work.
    /// </summary>
    internal sealed class AIProductionSelector
    {
        private const int _buildingEnergyCost = 1;
        private const string _mixedProductType = "*";
        private static readonly AIProductionProposalScorer _scorer = new();
        private readonly AISelectionState _selectionState;
        private readonly Dictionary<string, int> _reservedProducerCapacity = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _availableProducerCapacity = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, string> _selectedProducerProducts = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, string> _currentProducerProducts = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<
            string,
            Dictionary<BuildingType, int>
        > _reservedDestinationBuildings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildingType> _reservedFacilityRoles = new(
            StringComparer.Ordinal
        );
        private readonly HashSet<string> _drainingProducerStreams = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<IManufacturable>> _plannedProducerOrders = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<
            (Planet Producer, IManufacturable Product, string PrecedingProducts, int EarliestStart),
            ManufacturingQueries.ProductionForecast?
        > _productionForecasts = new();
        private readonly Dictionary<
            (Planet Producer, ManufacturingType Type),
            ManufacturingQueries.ProductionForecast?
        > _initialProductionForecasts = new();
        private readonly Dictionary<
            AIManufactureProposal,
            (AIManufactureProposal Action, bool StartsNow)
        > _fleetAssignments = new();
        private readonly HashSet<AIManufactureProposal> _scheduledActions = new();
        private readonly Dictionary<AIManufactureProposal, int> _requiredFleetCapacity = new();
        private readonly Dictionary<AIManufactureProposal, int> _assignedCapacityTicks = new();
        private readonly Dictionary<AIManufactureProposal, int> _capacityReadyTicks = new();
        private readonly Dictionary<
            (Fleet Fleet, bool Fighter),
            List<(int Ready, int Capacity)>
        > _fleetCapacity = new();
        private readonly Dictionary<
            (Fleet Fleet, bool Fighter),
            List<(int Ready, int Capacity)>
        > _committedFleetCapacity = new();
        private readonly Dictionary<
            (Planet Producer, ContainerNode Destination, string Schedule, int PriorCount, bool Gap),
            (
                int LastArrival,
                long ArrivalTotal,
                IReadOnlyList<int> Arrivals,
                ManufacturingQueries.ProductionForecast? Forecast
            )
        > _laneArrivalMetrics = new();
        private readonly Dictionary<string, List<int>> _plannedProducerStartTicks = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<
            (Planet Producer, IManufacturable Product, int PlannedCount),
            bool
        > _gapCompatibility = new();
        private readonly Dictionary<
            AIManufactureProposal,
            List<AIManufactureProposal>
        > _producerOptions = new();
        private readonly Dictionary<
            (Planet Producer, IManufacturable Product, ContainerNode Destination),
            int?
        > _transitTicks = new();
        private int _selectedMaintenanceCost;
        private int _selectedPlanetaryStarfighterCount;

        internal int ScheduleSearchNodes { get; private set; }
        internal bool ScheduleSearchCompleted { get; private set; } = true;

        /// <summary>
        /// Creates a production selector backed by the current turn's allocation ledger.
        /// </summary>
        /// <param name="selectionState">Shared typed reservation state.</param>
        internal AIProductionSelector(AISelectionState selectionState)
        {
            _selectionState = selectionState;
        }

        /// <summary>Plans fleet production jointly while preserving ranked proposal execution order.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="rankedProposals">Proposals ordered by existing strategic priority and score.</param>
        /// <param name="minimumScore">The minimum selectable optional score.</param>
        /// <returns>The original ranked proposals with turn-local fleet assignments.</returns>
        internal IEnumerable<AIProposal> ScheduleProduction(
            AITurnContext context,
            IEnumerable<AIProposal> rankedProposals,
            float minimumScore
        )
        {
            List<AIProposal> ranked = rankedProposals.ToList();
            List<AIManufactureProposal> fleetRequests = ranked
                .OfType<AIManufactureProposal>()
                .Where(proposal =>
                    proposal.HasScore
                    && proposal.Destination is Fleet
                    && proposal.Product.GetReference() is not Building
                    && (
                        proposal.Priority == AIProposalPriority.Mandatory
                        || proposal.Score > minimumScore
                    )
                )
                .ToList();
            HashSet<AIManufactureProposal> eligible = fleetRequests.ToHashSet();
            bool scheduled = false;
            foreach (AIProposal proposal in ranked)
            {
                if (proposal is not AIManufactureProposal request || !eligible.Contains(request))
                {
                    yield return proposal;
                    continue;
                }
                if (!scheduled)
                {
                    ScheduleFleets(context, fleetRequests);
                    scheduled = true;
                }
                yield return proposal;
            }
        }

        /// <summary>Assigns fleet build lists and optimizes their readiness in strategic priority order.</summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="requests">The complete scored requests in ranked fleet order.</param>
        private void ScheduleFleets(AITurnContext context, List<AIManufactureProposal> requests)
        {
            InvalidateFleetCapacity();
            _producerOptions.Clear();
            var lanes =
                new Dictionary<
                    string,
                    List<(AIManufactureProposal Request, AIManufactureProposal Action)>
                >();
            var fleetIndices = requests
                .Select(request => request.Destination)
                .Distinct()
                .Select((fleet, index) => (fleet, index))
                .ToDictionary(item => item.fleet, item => item.index);
            List<AIManufactureProposal> ordered = requests
                .OrderBy(request => fleetIndices[request.Destination])
                .ThenByDescending(request => request.Product.GetReference() is CapitalShip)
                .ThenByDescending(request =>
                    GetProducerOptions(context, request)
                        .Select(option => GetManufacturedArrivalTicks(context, option))
                        .DefaultIfEmpty(0)
                        .Min()
                )
                .ThenByDescending(request => request.Score)
                .ThenBy(
                    request => request.Product.GetReference().GetTypeID(),
                    StringComparer.Ordinal
                )
                .ToList();
            foreach (AIManufactureProposal request in ordered)
            {
                IManufacturable product = request.Product.GetReference();
                if (product is Starfighter or Regiment)
                    _requiredFleetCapacity[request] =
                        1
                        + _requiredFleetCapacity.Keys.Count(previous =>
                            previous.Destination == request.Destination
                            && previous.Product.GetReference().GetType() == product.GetType()
                        );
                AIManufactureProposal best = null;
                int earliest = int.MaxValue;
                foreach (AIManufactureProposal option in GetProducerOptions(context, request))
                {
                    int arrival = GetScheduledArrivalTicks(context, request, option, lanes);
                    if (arrival >= earliest)
                        continue;
                    earliest = arrival;
                    best = option;
                }
                if (best == null)
                {
                    ScheduleSearchCompleted = false;
                    continue;
                }
                string key = best.GetProducerCapacityKey();
                if (!lanes.TryGetValue(key, out var orders))
                    lanes.Add(key, orders = new());
                orders.Add((request, best));
            }

            OptimizeFleetSchedules(context, lanes, fleetIndices);
            var readyTicks = lanes
                .SelectMany(lane => lane.Value)
                .ToDictionary(
                    order => order.Action,
                    order => GetFleetCapacityReadyTicks(context, order.Request, lanes)
                );
            var gapStarts = lanes.ToDictionary(
                lane => lane.Key,
                lane =>
                    lane.Value.Count > 0
                    && readyTicks[lane.Value[0].Action] == 0
                    && CanStartBeforePlannedWork(context, lane.Value[0].Action)
            );
            foreach (var lane in lanes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                bool first = !_plannedProducerOrders.ContainsKey(lane.Key) || gapStarts[lane.Key];
                bool prepend = gapStarts[lane.Key];
                foreach (var order in lane.Value)
                {
                    _fleetAssignments[order.Request] = (
                        order.Action,
                        first && readyTicks[order.Action] == 0
                    );
                    _scheduledActions.Add(order.Action);
                    PlanProducerOrder(order.Action, readyTicks[order.Action], prepend);
                    first = false;
                    prepend = false;
                }
            }
        }

        /// <summary>Searches producer assignments and lane orderings, pruning branches that cannot improve delivery.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="lanes">The initial complete assignments, replaced only by a better schedule.</param>
        /// <param name="fleetIndices">Fleet readiness objectives in strategic priority order.</param>
        private void OptimizeFleetSchedules(
            AITurnContext context,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes,
            Dictionary<ContainerNode, int> fleetIndices
        )
        {
            if (lanes.Count == 0)
                return;

            List<AIManufactureProposal> requests = lanes
                .Values.SelectMany(orders => orders)
                .Select(order => order.Request)
                .OrderByDescending(request => request.Product.GetReference() is CapitalShip)
                .ThenByDescending(request => request.Product.GetReference().GetConstructionCost())
                .ThenByDescending(request => request.Score)
                .ThenBy(
                    request => request.Product.GetReference().GetTypeID(),
                    StringComparer.Ordinal
                )
                .ToList();
            var best = CopyFleetSchedule(lanes);
            var bestMetrics = GetFleetScheduleMetrics(context, best, fleetIndices);
            int[] bestCompletion = bestMetrics.Ready;
            long bestArrivalTotal = bestMetrics.Total;
            var ranked = CreateRankedFleetSchedule(context, requests);
            if (ranked != null)
            {
                InvalidateFleetCapacity();
                var metrics = GetFleetScheduleMetrics(context, ranked, fleetIndices);
                int[] completion = metrics.Ready;
                long total = metrics.Total;
                int comparison = CompareFleetReadiness(completion, bestCompletion);
                if (comparison < 0 || comparison == 0 && total < bestArrivalTotal)
                {
                    best = ranked;
                    bestCompletion = completion;
                    bestArrivalTotal = total;
                }
            }

            InvalidateFleetCapacity();
            var bestStarts = best
                .Values.SelectMany(orders => orders)
                .ToDictionary(
                    order => order.Request,
                    order => GetFleetCapacityReadyTicks(context, order.Request, best)
                );
            var search = new Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            >(StringComparer.Ordinal);
            HashSet<AIManufactureProposal> placed = new();
            Dictionary<string, long> visited = new(StringComparer.Ordinal);
            var indices = requests
                .Select((request, index) => (request, index))
                .ToDictionary(item => item.request, item => item.index);
            var optimistic = requests.ToDictionary(
                request => request,
                request =>
                    GetProducerOptions(context, request)
                        .Select(option =>
                            GetManufacturedArrivalTicks(context, option, new(), 0, new())
                        )
                        .DefaultIfEmpty(int.MaxValue)
                        .Min()
            );
            var optionsByRequest = requests.ToDictionary(
                request => request,
                request => GetProducerOptions(context, request).ToList()
            );
            var signatures = requests.ToDictionary(
                request => request,
                request =>
                    request.Destination.InstanceID
                    + ":"
                    + request.Product.GetReference().GetTypeID()
                    + ":"
                    + string.Join(
                        ",",
                        optionsByRequest[request]
                            .Select(option => option.GetProducerCapacityKey())
                            .OrderBy(key => key, StringComparer.Ordinal)
                    )
            );
            Dictionary<(ContainerNode Fleet, Type Type), int> personnelCounts = new();
            var capacityDependencies = requests
                .Where(request => request.Product.GetReference() is Starfighter or Regiment)
                .GroupBy(request =>
                    (
                        Fleet: request.Destination,
                        Fighter: request.Product.GetReference() is Starfighter
                    )
                )
                .Where(group =>
                    GetCapacityReadyTicks(
                        GetCommittedFleetCapacity(
                            context,
                            (Fleet)group.Key.Fleet,
                            group.Key.Fighter
                        ),
                        group.Count()
                    ) != 0
                )
                .Select(group => group.Key)
                .ToHashSet();
            int limit = Math.Max(0, context.Game.Config.AI.Selection.ProductionScheduleSearchNodes);

            /// <summary>Enumerates feasible next jobs without modifying manufacturing queues.</summary>
            void Search()
            {
                if (ScheduleSearchNodes >= limit)
                {
                    ScheduleSearchCompleted = false;
                    return;
                }
                ScheduleSearchNodes++;
                var metrics = GetFleetScheduleMetrics(context, search, fleetIndices);
                int[] completion = metrics.Ready;
                long total = metrics.Total;
                foreach (AIManufactureProposal request in requests)
                {
                    if (placed.Contains(request))
                        continue;
                    int fleetIndex = fleetIndices[request.Destination];
                    completion[fleetIndex] = Math.Max(completion[fleetIndex], optimistic[request]);
                    total += optimistic[request];
                }
                int comparison = CompareFleetReadiness(completion, bestCompletion);
                if (comparison > 0 || comparison == 0 && total >= bestArrivalTotal)
                    return;
                if (placed.Count == requests.Count)
                {
                    best = CopyFleetSchedule(search);
                    bestCompletion = completion;
                    bestArrivalTotal = total;
                    bestStarts = requests.ToDictionary(
                        request => request,
                        request => _assignedCapacityTicks[request]
                    );
                    return;
                }
                string state =
                    string.Join(
                        ",",
                        requests
                            .Where(request => !placed.Contains(request))
                            .Select(request => indices[request])
                    )
                    + "|"
                    + string.Join(",", completion)
                    + "|"
                    + string.Join(
                        "|",
                        search
                            .OrderBy(lane => lane.Key, StringComparer.Ordinal)
                            .Select(lane =>
                                $"{lane.Key}:{GetProductionStateKey(GetLaneArrivalMetrics(context, lane.Key, lane.Value, search).Forecast)}"
                            )
                    );
                List<string> carriers = new();
                foreach (var lane in search)
                {
                    var arrivals = GetLaneArrivalMetrics(
                        context,
                        lane.Key,
                        lane.Value,
                        search
                    ).Arrivals;
                    for (int index = 0; index < lane.Value.Count; index++)
                    {
                        var order = lane.Value[index];
                        if (order.Action.Product.GetReference() is CapitalShip ship)
                        {
                            int fighters = capacityDependencies.Contains(
                                (order.Request.Destination, true)
                            )
                                ? ship.StarfighterCapacity
                                : 0;
                            int regiments = capacityDependencies.Contains(
                                (order.Request.Destination, false)
                            )
                                ? ship.RegimentCapacity
                                : 0;
                            if (fighters > 0 || regiments > 0)
                                carriers.Add(
                                    $"{fleetIndices[order.Request.Destination]}:{arrivals[index]}:{fighters}:{regiments}"
                                );
                        }
                    }
                }
                state +=
                    "|"
                    + string.Join(
                        "|",
                        carriers.OrderBy(carrier => carrier, StringComparer.Ordinal)
                    );
                if (visited.TryGetValue(state, out long earlierTotal) && earlierTotal <= total)
                    return;
                visited[state] = total;
                HashSet<string> equivalent = new(StringComparer.Ordinal);
                var choices =
                    new List<(
                        AIManufactureProposal Request,
                        AIManufactureProposal Action,
                        int Ready,
                        int Arrival
                    )>();
                foreach (AIManufactureProposal request in requests)
                {
                    if (placed.Contains(request))
                        continue;
                    List<AIManufactureProposal> options = optionsByRequest[request];
                    if (!equivalent.Add(signatures[request]))
                        continue;
                    int original = _requiredFleetCapacity.TryGetValue(request, out int previous)
                        ? previous
                        : 0;
                    IManufacturable product = request.Product.GetReference();
                    if (product is Starfighter or Regiment)
                        _requiredFleetCapacity[request] =
                            1
                            + personnelCounts.GetValueOrDefault(
                                (request.Destination, product.GetType())
                            );
                    InvalidateFleetCapacity();
                    int ready = GetFleetCapacityReadyTicks(context, request, search);
                    if (ready < int.MaxValue)
                    {
                        _assignedCapacityTicks[request] = ready;
                        foreach (AIManufactureProposal option in options)
                        {
                            int arrival = GetScheduledArrivalTicks(
                                context,
                                request,
                                option,
                                search
                            );
                            if (arrival < int.MaxValue)
                                choices.Add((request, option, ready, arrival));
                        }
                        _assignedCapacityTicks.Remove(request);
                    }
                    if (original == 0)
                        _requiredFleetCapacity.Remove(request);
                    else
                        _requiredFleetCapacity[request] = original;
                }
                foreach (
                    var choice in choices
                        .OrderBy(choice => fleetIndices[choice.Request.Destination])
                        .ThenBy(choice => choice.Arrival)
                        .ThenBy(choice => indices[choice.Request])
                        .ThenBy(
                            choice => choice.Action.GetProducerCapacityKey(),
                            StringComparer.Ordinal
                        )
                )
                {
                    if (ScheduleSearchNodes >= limit)
                    {
                        ScheduleSearchCompleted = false;
                        break;
                    }
                    string key = choice.Action.GetProducerCapacityKey();
                    if (!search.TryGetValue(key, out var orders))
                        search.Add(key, orders = new());
                    orders.Add((choice.Request, choice.Action));
                    placed.Add(choice.Request);
                    var personnelKey = (
                        choice.Request.Destination,
                        choice.Request.Product.GetReference().GetType()
                    );
                    personnelCounts[personnelKey] =
                        personnelCounts.GetValueOrDefault(personnelKey) + 1;
                    _assignedCapacityTicks[choice.Request] = choice.Ready;
                    InvalidateFleetCapacity();
                    Search();
                    orders.RemoveAt(orders.Count - 1);
                    if (orders.Count == 0)
                        search.Remove(key);
                    placed.Remove(choice.Request);
                    personnelCounts[personnelKey]--;
                    _assignedCapacityTicks.Remove(choice.Request);
                    InvalidateFleetCapacity();
                }
            }

            Search();
            foreach (var start in bestStarts)
                _assignedCapacityTicks[start.Key] = start.Value;

            lanes.Clear();
            foreach (var lane in best)
                lanes.Add(lane.Key, lane.Value);
            InvalidateFleetCapacity();
        }

        /// <summary>Identifies equivalent projected capacity using complete facility phases and activation dates.</summary>
        /// <param name="forecast">The projected lane state.</param>
        /// <returns>A lossless state key, or an invalid-state marker.</returns>
        private static string GetProductionStateKey(
            ManufacturingQueries.ProductionForecast? forecast
        )
        {
            if (!forecast.HasValue)
                return "invalid";
            var capacity = forecast.Value;
            return $"{(int)capacity.Type}:{capacity.AvailableTick}:{capacity.Increment.ToString("R", CultureInfo.InvariantCulture)}:"
                + string.Join(
                    ";",
                    capacity
                        .Facilities.Select(facility =>
                            $"{facility.Rate},{facility.CycleProgress.ToString("R", CultureInfo.InvariantCulture)},{facility.Ready},{facility.ActivationTicks}"
                        )
                        .OrderBy(facility => facility, StringComparer.Ordinal)
                );
        }

        /// <summary>Builds the ranked single-request assignment as a complete scheduling candidate.</summary>
        /// <param name="context">The current production context.</param>
        /// <param name="requests">The fleet's fixed product requests.</param>
        /// <returns>The complete ranked assignment, or null when a request cannot be placed.</returns>
        private Dictionary<
            string,
            List<(AIManufactureProposal Request, AIManufactureProposal Action)>
        > CreateRankedFleetSchedule(AITurnContext context, List<AIManufactureProposal> requests)
        {
            var lanes = new Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            >(StringComparer.Ordinal);
            List<AIManufactureProposal> pending = requests
                .OrderByDescending(request => request.Score)
                .ToList();
            while (pending.Count > 0)
            {
                bool placed = false;
                foreach (AIManufactureProposal request in pending.ToList())
                {
                    if (
                        request.Product.GetReference() is Starfighter or Regiment
                        && pending.Any(other => other.Product.GetReference() is CapitalShip)
                        && !HasCommittedFleetCapacity(context, request)
                    )
                        continue;
                    InvalidateFleetCapacity();
                    AIManufactureProposal option = GetProducerOptions(context, request)
                        .Select(action =>
                            (
                                Action: action,
                                Arrival: GetScheduledArrivalTicks(context, request, action, lanes)
                            )
                        )
                        .Where(choice => choice.Arrival < int.MaxValue)
                        .OrderBy(choice => choice.Arrival)
                        .ThenBy(
                            choice => choice.Action.ProducerPlanet.InstanceID,
                            StringComparer.Ordinal
                        )
                        .Select(choice => choice.Action)
                        .FirstOrDefault();
                    if (option == null)
                        continue;
                    string key = option.GetProducerCapacityKey();
                    if (!lanes.TryGetValue(key, out var orders))
                        lanes.Add(key, orders = new());
                    orders.Add((request, option));
                    pending.Remove(request);
                    placed = true;
                }
                if (!placed)
                    return null;
            }
            return lanes;
        }

        /// <summary>Copies tentative lane sequences without cloning or changing any game object.</summary>
        /// <param name="lanes">The producer sequences to preserve.</param>
        /// <returns>An independent copy of the assignment lists.</returns>
        private static Dictionary<
            string,
            List<(AIManufactureProposal Request, AIManufactureProposal Action)>
        > CopyFleetSchedule(
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes
        )
        {
            return lanes.ToDictionary(
                lane => lane.Key,
                lane => lane.Value.ToList(),
                StringComparer.Ordinal
            );
        }

        /// <summary>Measures each fleet's last delivery and the total delivery time of a complete or partial schedule.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="lanes">The temporary lane assignments.</param>
        /// <param name="fleetIndices">The fleet readiness objectives in priority order.</param>
        /// <returns>Readiness by fleet and summed arrival ticks.</returns>
        private (int[] Ready, long Total) GetFleetScheduleMetrics(
            AITurnContext context,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes,
            Dictionary<ContainerNode, int> fleetIndices
        )
        {
            int[] ready = new int[fleetIndices.Count];
            long total = 0;
            foreach (var lane in lanes)
            {
                var metrics = GetLaneArrivalMetrics(context, lane.Key, lane.Value, lanes);
                total += metrics.ArrivalTotal;
                for (int index = 0; index < lane.Value.Count; index++)
                {
                    int fleetIndex = fleetIndices[lane.Value[index].Request.Destination];
                    ready[fleetIndex] = Math.Max(ready[fleetIndex], metrics.Arrivals[index]);
                }
            }
            return (ready, total);
        }

        /// <summary>Compares readiness in fleet priority order without trading away a higher-priority completion.</summary>
        /// <param name="left">The first schedule's readiness values.</param>
        /// <param name="right">The second schedule's readiness values.</param>
        /// <returns>A negative value when the first schedule is earlier, zero when tied, or a positive value when later.</returns>
        private static int CompareFleetReadiness(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            for (int index = 0; index < left.Count; index++)
            {
                int comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                    return comparison;
            }
            return 0;
        }

        /// <summary>Forecasts all deliveries in a lane together instead of rebuilding each prefix.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="key">The producer lane identifier.</param>
        /// <param name="orders">The current fleet's orders.</param>
        /// <param name="lanes">The fleet's tentative carrier assignments.</param>
        /// <returns>The last arrival and total arrival ticks for this fleet's items.</returns>
        private (
            int LastArrival,
            long ArrivalTotal,
            IReadOnlyList<int> Arrivals,
            ManufacturingQueries.ProductionForecast? Forecast
        ) GetLaneArrivalMetrics(
            AITurnContext context,
            string key,
            List<(AIManufactureProposal Request, AIManufactureProposal Action)> orders,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes
        )
        {
            if (orders.Count == 0)
                return (0, 0, Array.Empty<int>(), null);
            bool gap =
                GetFleetCapacityReadyTicks(context, orders[0].Request, lanes) == 0
                && CanStartBeforePlannedWork(context, orders[0].Action);
            var prefix = GetLanePrefix(context, key, orders, gap ? 1 : 0, lanes);
            int priorCount = prefix.Products.Count - (gap ? 1 : 0);
            for (int index = gap ? 1 : 0; index < orders.Count; index++)
            {
                prefix.Products.Add(orders[index].Action.Product.GetReference());
                prefix.Starts.Add(
                    GetFleetCapacityReadyTicks(context, orders[index].Request, lanes)
                );
            }
            string schedule =
                string.Join(
                    ":",
                    prefix.Products.Select(
                        (product, index) => $"{product.GetTypeID()}@{prefix.Starts[index]}"
                    )
                )
                + "|"
                + string.Join(":", orders.Select(order => order.Action.Destination.InstanceID));
            var cacheKey = (
                orders[0].Action.ProducerPlanet,
                orders[0].Action.Destination,
                schedule,
                priorCount,
                gap
            );
            if (_laneArrivalMetrics.TryGetValue(cacheKey, out var cached))
                return cached;
            IReadOnlyList<int> completion = GetProjectedCompletions(
                context,
                orders[0].Action.ProducerPlanet,
                prefix.Products,
                prefix.Starts,
                out var finalForecast
            );
            int last = 0;
            long total = 0;
            List<int> arrivals = new();
            for (int index = 0; index < orders.Count; index++)
            {
                int position = gap && index == 0 ? 0 : priorCount + index;
                int? transit = GetTransitTicks(context, orders[index].Action);
                int arrival =
                    completion == null || !transit.HasValue
                        ? int.MaxValue
                        : (int)Math.Min(int.MaxValue, (long)completion[position] + transit.Value);
                last = Math.Max(last, arrival);
                total += arrival;
                arrivals.Add(arrival);
            }
            var result = (last, total, (IReadOnlyList<int>)arrivals, finalForecast);
            _laneArrivalMetrics.Add(cacheKey, result);
            return result;
        }

        /// <summary>Estimates one appended assignment behind this and higher-priority fleets.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="request">The fleet unit request and its capacity position.</param>
        /// <param name="option">The prospective producer assignment.</param>
        /// <param name="lanes">Assignments already made for this fleet.</param>
        /// <returns>The projected arrival tick.</returns>
        private int GetScheduledArrivalTicks(
            AITurnContext context,
            AIManufactureProposal request,
            AIManufactureProposal option,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes
        )
        {
            string key = option.GetProducerCapacityKey();
            int ready = GetFleetCapacityReadyTicks(context, request, lanes);
            lanes.TryGetValue(key, out var orders);
            if (
                (orders == null || orders.Count == 0)
                && ready == 0
                && CanStartBeforePlannedWork(context, option)
            )
                return GetManufacturedArrivalTicks(context, option, new(), 0, new());
            var prefix = GetLanePrefix(context, key, orders, orders?.Count ?? 0, lanes);
            return GetManufacturedArrivalTicks(
                context,
                option,
                prefix.Products,
                ready,
                prefix.Starts
            );
        }

        /// <summary>Builds the lane prefix, placing safe gap-filling work before later reserved starts.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="key">The producer lane identifier.</param>
        /// <param name="orders">The current fleet's lane assignments.</param>
        /// <param name="count">The number of current-fleet orders preceding the item being estimated.</param>
        /// <param name="lanes">The complete tentative fleet assignments.</param>
        /// <returns>The preceding templates and their destination-ready ticks.</returns>
        private (List<IManufacturable> Products, List<int> Starts) GetLanePrefix(
            AITurnContext context,
            string key,
            List<(AIManufactureProposal Request, AIManufactureProposal Action)> orders,
            int count,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes
        )
        {
            List<IManufacturable> products = _plannedProducerOrders.TryGetValue(key, out var prior)
                ? new(prior)
                : new();
            List<int> starts = _plannedProducerStartTicks.TryGetValue(key, out var priorStarts)
                ? new(priorStarts)
                : new();
            bool gap =
                orders?.Count > 0
                && GetFleetCapacityReadyTicks(context, orders[0].Request, lanes) == 0
                && CanStartBeforePlannedWork(context, orders[0].Action);
            if (gap && count == 0)
                return (new(), new());
            for (int index = 0; index < count; index++)
            {
                var order = orders[index];
                int ready = GetFleetCapacityReadyTicks(context, order.Request, lanes);
                if (gap && index == 0)
                {
                    products.Insert(0, order.Action.Product.GetReference());
                    starts.Insert(0, ready);
                }
                else
                {
                    products.Add(order.Action.Product.GetReference());
                    starts.Add(ready);
                }
            }
            return (products, starts);
        }

        /// <summary>Checks idle time can be used without delaying any higher-priority planned item.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="proposal">The lower-priority single-unit action.</param>
        /// <returns>True when it can start now and all prior completion times are preserved.</returns>
        private bool CanStartBeforePlannedWork(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            string key = proposal.GetProducerCapacityKey();
            if (
                !_plannedProducerOrders.TryGetValue(key, out var orders)
                || orders.Count == 0
                || !_plannedProducerStartTicks.TryGetValue(key, out var starts)
                || starts[0] <= 0
                || !CanSelect(context, proposal)
            )
                return false;
            IManufacturable product = proposal.Product.GetReference();
            var cacheKey = (proposal.ProducerPlanet, product, orders.Count);
            if (_gapCompatibility.TryGetValue(cacheKey, out bool compatible))
                return compatible;
            List<IManufacturable> before = new();
            List<int> beforeStarts = new();
            List<IManufacturable> after = new() { product };
            List<int> afterStarts = new() { 0 };
            compatible = true;
            for (int index = 0; index < orders.Count; index++)
            {
                int? original = context.ManufacturingQueries.EstimateScheduledCompletionTicks(
                    proposal.ProducerPlanet,
                    orders[index],
                    context.Game.Config.AI.TickInterval,
                    before,
                    starts[index],
                    beforeStarts
                );
                int? revised = context.ManufacturingQueries.EstimateScheduledCompletionTicks(
                    proposal.ProducerPlanet,
                    orders[index],
                    context.Game.Config.AI.TickInterval,
                    after,
                    starts[index],
                    afterStarts
                );
                if (!original.HasValue || !revised.HasValue || revised.Value > original.Value)
                {
                    compatible = false;
                    break;
                }
                before.Add(orders[index]);
                beforeStarts.Add(starts[index]);
                after.Add(orders[index]);
                afterStarts.Add(starts[index]);
            }
            _gapCompatibility.Add(cacheKey, compatible);
            return compatible;
        }

        /// <summary>Finds when committed and planned carriers can receive this fleet unit.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="request">The carried unit request.</param>
        /// <param name="lanes">Temporary assignments for this fleet.</param>
        /// <returns>The earliest capacity-ready tick, or the maximum value when capacity is missing.</returns>
        private int GetFleetCapacityReadyTicks(
            AITurnContext context,
            AIManufactureProposal request,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes
        )
        {
            if (_assignedCapacityTicks.TryGetValue(request, out int assigned))
                return assigned;
            if (!_requiredFleetCapacity.TryGetValue(request, out int required))
                return 0;
            if (HasCommittedFleetCapacity(context, request))
                return 0;
            if (_capacityReadyTicks.TryGetValue(request, out int cached))
                return cached;
            int ready = CalculateFleetCapacityReadyTicks(context, request, required, lanes);
            _capacityReadyTicks.Add(request, ready);
            return ready;
        }

        /// <summary>Calculates carrier availability for a fixed hull assignment.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="request">The carried unit request.</param>
        /// <param name="required">The cumulative free capacity required by this request.</param>
        /// <param name="lanes">The temporary fleet assignments.</param>
        /// <returns>The earliest capacity-ready tick, or the maximum value when unavailable.</returns>
        private int CalculateFleetCapacityReadyTicks(
            AITurnContext context,
            AIManufactureProposal request,
            int required,
            Dictionary<
                string,
                List<(AIManufactureProposal Request, AIManufactureProposal Action)>
            > lanes
        )
        {
            Fleet fleet = request.Demand.DestinationFleet;
            bool fighter = request.Product.GetReference() is Starfighter;
            if (_fleetCapacity.TryGetValue((fleet, fighter), out var cached))
                return GetCapacityReadyTicks(cached, required);
            List<(int Ready, int Capacity)> capacity = new(
                GetCommittedFleetCapacity(context, fleet, fighter)
            );
            foreach (var lane in lanes)
            {
                for (int index = lane.Value.Count - 1; index >= 0; index--)
                {
                    var order = lane.Value[index];
                    if (
                        order.Request.Destination != fleet
                        || order.Action.Product.GetReference() is not CapitalShip ship
                    )
                        continue;
                    var prefix = GetLanePrefix(context, lane.Key, lane.Value, index, lanes);
                    capacity.Add(
                        (
                            GetManufacturedArrivalTicks(
                                context,
                                order.Action,
                                prefix.Products,
                                0,
                                prefix.Starts
                            ),
                            fighter ? ship.StarfighterCapacity : ship.RegimentCapacity
                        )
                    );
                }
            }
            capacity.Sort((left, right) => left.Ready.CompareTo(right.Ready));
            _fleetCapacity.Add((fleet, fighter), capacity);
            return GetCapacityReadyTicks(capacity, required);
        }

        /// <summary>Collects current carrier availability once for the unchanged turn state.</summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="fleet">The fleet receiving personnel.</param>
        /// <param name="fighter">Whether to inspect fighter capacity instead of regiment capacity.</param>
        /// <returns>Committed carrier capacities ordered by arrival.</returns>
        private List<(int Ready, int Capacity)> GetCommittedFleetCapacity(
            AITurnContext context,
            Fleet fleet,
            bool fighter
        )
        {
            if (_committedFleetCapacity.TryGetValue((fleet, fighter), out var cached))
                return cached;
            List<(int Ready, int Capacity)> capacity = new();
            foreach (CapitalShip ship in fleet.GetChildren<CapitalShip>())
            {
                int excess = fighter
                    ? ship.GetExcessStarfighterCapacity()
                    : ship.GetExcessRegimentCapacity();
                if (excess <= 0)
                    continue;
                int ready = 0;
                MovementState movement = ((IMovable)ship).GetTransitMovement();
                if (movement != null)
                    ready = movement.TicksRemaining();
                else if (ship.ManufacturingStatus != ManufacturingStatus.Complete)
                {
                    Planet producer = context.Game.GetSceneNodeByInstanceID<Planet>(
                        ship.ProducerPlanetID
                    );
                    int? completion = ManufacturingQueries.EstimateCompletionTicks(producer, ship);
                    if (
                        !completion.HasValue
                        || !context.MovementQueries.TryEstimateManufacturedTransitTicks(
                            ship,
                            producer,
                            fleet,
                            out int transit,
                            context.Faction.InstanceID
                        )
                    )
                        continue;
                    ready = (int)
                        Math.Min(int.MaxValue, (long)completion.Value + Math.Max(0, transit - 1));
                }
                capacity.Add((ready, excess));
            }
            capacity.Sort((left, right) => left.Ready.CompareTo(right.Ready));
            _committedFleetCapacity.Add((fleet, fighter), capacity);
            return capacity;
        }

        /// <summary>Discards availability forecasts after changing tentative carrier assignments.</summary>
        private void InvalidateFleetCapacity()
        {
            _capacityReadyTicks.Clear();
            _fleetCapacity.Clear();
        }

        /// <summary>Checks personnel can be produced without waiting for a tentative hull.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="request">The personnel request and its capacity position.</param>
        /// <returns>True when committed stationary carriers already provide its capacity.</returns>
        private bool HasCommittedFleetCapacity(AITurnContext context, AIManufactureProposal request)
        {
            return !_requiredFleetCapacity.TryGetValue(request, out int required)
                || GetCapacityReadyTicks(
                    GetCommittedFleetCapacity(
                        context,
                        request.Demand.DestinationFleet,
                        request.Product.GetReference() is Starfighter
                    ),
                    required
                ) == 0;
        }

        /// <summary>Finds the first delivery providing enough cumulative carrier capacity.</summary>
        /// <param name="capacity">Carrier capacities ordered by their arrival tick.</param>
        /// <param name="required">The cumulative capacity needed by the unit request.</param>
        /// <returns>The capacity-ready tick, or the maximum value when unavailable.</returns>
        private static int GetCapacityReadyTicks(
            IEnumerable<(int Ready, int Capacity)> capacity,
            int required
        )
        {
            foreach (var carrier in capacity)
            {
                required -= carrier.Capacity;
                if (required <= 0)
                    return carrier.Ready;
            }
            return int.MaxValue;
        }

        /// <summary>Enumerates valid single-unit producer alternatives without claiming a live lane.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="request">The strategic production request.</param>
        /// <returns>Options compatible with existing non-lane reservations and maintenance limits.</returns>
        private IEnumerable<AIManufactureProposal> GetProducerOptions(
            AITurnContext context,
            AIManufactureProposal request
        )
        {
            if (_producerOptions.TryGetValue(request, out List<AIManufactureProposal> cached))
                return cached;
            List<AIManufactureProposal> options = new[] { request }
                .Concat(request.ProducerAlternatives)
                .Select(option => option.WithManufacturingCount(1))
                .Where(option =>
                    option.CanPlan(context)
                    && _selectionState.CanReserve(
                        GetReservations(option)
                            .Where(reservation =>
                                reservation.Kind != AISelectionReservationKind.ManufacturingLane
                            )
                    )
                    && !WouldExceedMaintenanceHeadroom(context, option)
                    && CanReserveIdleShipyardCapacity(context, option)
                )
                .ToList();
            _producerOptions.Add(request, options);
            return options;
        }

        /// <summary>
        /// Attempts to select a facility-removal proposal.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Facility-removal proposal.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIFacilityRemovalProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (!_selectionState.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            _selectionState.Reserve(reservations);
            return true;
        }

        /// <summary>
        /// Attempts to select a headquarters relocation and reserves its destination capacity.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Headquarters-relocation proposal.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIHeadquartersRelocationProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (!_selectionState.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            _selectionState.Reserve(reservations);
            ReserveDestinationEnergy(
                proposal.Destination,
                BuildingType.Headquarters,
                _buildingEnergyCost
            );
            ReserveMaintenanceCost(proposal.MaintenanceHeadroomCost);
            return true;
        }

        /// <summary>
        /// Resolves the first viable producer and quantity without changing global ordering.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production decision.</param>
        /// <param name="selectedProposal">The exact executable manufacturing action.</param>
        /// <returns>True when the production decision has a selectable exact action.</returns>
        internal bool TryResolve(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            if (proposal?.Product?.GetReference() is not Building)
                return TryResolveUnit(context, proposal, out selectedProposal);

            int equivalentProducerCount = proposal.ManufacturingCount;
            if (TryResolveQuantity(context, proposal, out selectedProposal))
                return true;

            if (proposal.CarriesReducedCountAcrossAlternatives)
                equivalentProducerCount = selectedProposal.ManufacturingCount;

            foreach (AIManufactureProposal alternative in proposal.ProducerAlternatives)
            {
                AIManufactureProposal candidate = alternative;
                if (
                    proposal.CarriesReducedCountAcrossAlternatives
                    && equivalentProducerCount < candidate.ManufacturingCount
                )
                    candidate = alternative.WithManufacturingCount(equivalentProducerCount);
                if (TryResolveQuantity(context, candidate, out selectedProposal))
                    return true;

                if (proposal.CarriesReducedCountAcrossAlternatives)
                    equivalentProducerCount = selectedProposal.ManufacturingCount;
            }

            selectedProposal = proposal;
            return false;
        }

        /// <summary>
        /// Assigns the next unit by projected delivery after higher-ranked production choices.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production request.</param>
        /// <param name="selectedProposal">The executable one-unit action, or the waiting plan.</param>
        /// <returns>True only when the assigned lane can start the unit now.</returns>
        private bool TryResolveUnit(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            if (proposal.Demand.Kind == AIProductionDemandKind.IdleSpecialForces)
            {
                selectedProposal = new[] { proposal }
                    .Concat(proposal.ProducerAlternatives)
                    .Select(option => option.WithManufacturingCount(1))
                    .FirstOrDefault(option =>
                        CanSelect(context, option) && CanReserve(context, option)
                    );
                return selectedProposal != null;
            }
            if (_fleetAssignments.TryGetValue(proposal, out var assignment))
            {
                selectedProposal = assignment.Action;
                return assignment.StartsNow && CanSelect(context, selectedProposal);
            }
            selectedProposal = null;
            int earliestArrival = int.MaxValue;
            foreach (
                AIManufactureProposal option in new[] { proposal }.Concat(
                    proposal.ProducerAlternatives
                )
            )
            {
                AIManufactureProposal candidate = option.WithManufacturingCount(1);
                if (
                    proposal.Demand.Kind == AIProductionDemandKind.IdleSpecialForces
                    && !CanSelect(context, candidate)
                )
                    continue;
                if (
                    !candidate.CanPlan(context)
                    || !_selectionState.CanReserve(
                        GetReservations(candidate)
                            .Where(reservation =>
                                reservation.Kind != AISelectionReservationKind.ManufacturingLane
                            )
                    )
                    || WouldExceedMaintenanceHeadroom(context, candidate)
                    || !CanReserveIdleShipyardCapacity(context, candidate)
                )
                    continue;

                bool gap = CanStartBeforePlannedWork(context, candidate);
                int arrival = gap
                    ? GetManufacturedArrivalTicks(context, candidate, new(), 0, new())
                    : GetManufacturedArrivalTicks(context, candidate);
                if (
                    arrival < earliestArrival
                    || arrival == earliestArrival
                        && arrival < int.MaxValue
                        && string.CompareOrdinal(
                            candidate.ProducerPlanet.InstanceID,
                            selectedProposal?.ProducerPlanet.InstanceID
                        ) < 0
                )
                {
                    selectedProposal = candidate;
                    earliestArrival = arrival;
                }
            }

            if (selectedProposal == null)
                return false;

            bool startsBeforePlan = CanStartBeforePlannedWork(context, selectedProposal);
            if (
                (
                    !_plannedProducerOrders.ContainsKey(selectedProposal.GetProducerCapacityKey())
                    || startsBeforePlan
                ) && CanSelect(context, selectedProposal)
            )
            {
                if (startsBeforePlan)
                {
                    PlanProducerOrder(selectedProposal, prepend: true);
                    _scheduledActions.Add(selectedProposal);
                }
                return true;
            }

            PlanProducerOrder(selectedProposal);
            return false;
        }

        /// <summary>
        /// Estimates delivery using queued work, upcoming facilities, and the next AI decision.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The one-unit producer option.</param>
        /// <param name="precedingOrders">Earlier templates assigned to this lane.</param>
        /// <param name="earliestStartTicks">The destination's capacity-ready tick.</param>
        /// <param name="precedingStartTicks">Destination-ready ticks for the preceding lane choices.</param>
        /// <returns>The estimated arrival ticks, or the maximum value when unavailable.</returns>
        private int GetManufacturedArrivalTicks(
            AITurnContext context,
            AIManufactureProposal proposal,
            List<IManufacturable> precedingOrders = null,
            int earliestStartTicks = 0,
            List<int> precedingStartTicks = null
        )
        {
            if (precedingOrders == null)
            {
                _plannedProducerOrders.TryGetValue(
                    proposal.GetProducerCapacityKey(),
                    out precedingOrders
                );
                _plannedProducerStartTicks.TryGetValue(
                    proposal.GetProducerCapacityKey(),
                    out precedingStartTicks
                );
            }
            IManufacturable product = proposal.Product.GetReference();
            (
                Planet Producer,
                IManufacturable Product,
                string PrecedingProducts,
                int EarliestStart
            ) key = (
                proposal.ProducerPlanet,
                product,
                precedingOrders == null
                    ? string.Empty
                    : string.Join(
                        ":",
                        precedingOrders.Select(
                            (order, index) =>
                                $"{order.GetTypeID()}@{(precedingStartTicks != null && index < precedingStartTicks.Count ? precedingStartTicks[index] : 0)}"
                        )
                    ),
                earliestStartTicks
            );
            if (!_productionForecasts.TryGetValue(key, out var forecast))
            {
                List<IManufacturable> orders = precedingOrders?.ToList() ?? new();
                List<int> starts = orders
                    .Select(
                        (_, index) =>
                            precedingStartTicks != null && index < precedingStartTicks.Count
                                ? precedingStartTicks[index]
                                : 0
                    )
                    .ToList();
                orders.Add(product);
                starts.Add(earliestStartTicks);
                IReadOnlyList<int> completion = GetProjectedCompletions(
                    context,
                    proposal.ProducerPlanet,
                    orders,
                    starts,
                    out forecast
                );
                if (completion == null)
                    return int.MaxValue;
                forecast = _productionForecasts[key];
            }
            if (!forecast.HasValue)
                return int.MaxValue;
            int? transit = GetTransitTicks(context, proposal);
            if (!transit.HasValue)
                return int.MaxValue;

            return (int)Math.Min(int.MaxValue, (long)forecast.Value.AvailableTick + transit.Value);
        }

        /// <summary>Reuses immutable prefix forecasts so each new branch projects only its appended work.</summary>
        /// <param name="context">The unchanged faction turn.</param>
        /// <param name="producer">The manufacturing planet.</param>
        /// <param name="orders">The temporary sequence of products.</param>
        /// <param name="starts">The earliest start for each product.</param>
        /// <param name="finalForecast">Receives the lane state after the complete prefix.</param>
        /// <returns>Completion dates, or null when a product cannot be forecast.</returns>
        private IReadOnlyList<int> GetProjectedCompletions(
            AITurnContext context,
            Planet producer,
            IReadOnlyList<IManufacturable> orders,
            IReadOnlyList<int> starts,
            out ManufacturingQueries.ProductionForecast? finalForecast
        )
        {
            var laneKey = (producer, orders[0].GetManufacturingType());
            if (!_initialProductionForecasts.TryGetValue(laneKey, out var capacity))
            {
                capacity = context.ManufacturingQueries.GetProductionCapacityForecast(
                    producer,
                    laneKey.Item2,
                    ignoreInterruptibleWork: true
                );
                _initialProductionForecasts.Add(laneKey, capacity);
            }
            List<int> completions = new(orders.Count);
            finalForecast = capacity;
            string prefix = string.Empty;
            for (int index = 0; index < orders.Count; index++)
            {
                int start = starts != null && index < starts.Count ? starts[index] : 0;
                var key = (producer, orders[index], prefix, start);
                if (!_productionForecasts.TryGetValue(key, out var next))
                {
                    next = capacity.HasValue
                        ? ManufacturingQueries
                            .EstimateNextProduction(
                                capacity.Value,
                                orders[index],
                                context.Game.Config.AI.TickInterval,
                                start
                            )
                            ?.Forecast
                        : null;
                    _productionForecasts.Add(key, next);
                }
                capacity = next;
                finalForecast = capacity;
                if (!capacity.HasValue)
                    return null;
                completions.Add(capacity.Value.AvailableTick);
                string item = $"{orders[index].GetTypeID()}@{start}";
                prefix = prefix.Length == 0 ? item : $"{prefix}:{item}";
            }
            return completions;
        }

        /// <summary>Reuses delivery estimates while the turn's world state remains unchanged.</summary>
        /// <param name="context">The active faction turn.</param>
        /// <param name="proposal">The producer, product, and destination to inspect.</param>
        /// <returns>The delivery ticks, or null when no delivery is valid.</returns>
        private int? GetTransitTicks(AITurnContext context, AIManufactureProposal proposal)
        {
            IManufacturable product = proposal.Product.GetReference();
            var transitKey = (proposal.ProducerPlanet, product, proposal.Destination);
            if (!_transitTicks.TryGetValue(transitKey, out int? transit))
            {
                transit = context.MovementQueries.TryEstimateManufacturedTransitTicks(
                    product,
                    proposal.ProducerPlanet,
                    proposal.Destination,
                    out int transitTicks,
                    context.Faction.InstanceID
                )
                    ? Math.Max(0, transitTicks - 1)
                    : null;
                _transitTicks.Add(transitKey, transit);
            }
            return transit;
        }

        /// <summary>
        /// Records a hypothetical lane order without creating or enqueuing any unit.
        /// </summary>
        /// <param name="proposal">The producer choice to include in later forecasts.</param>
        /// <param name="earliestStartTicks">The destination-ready tick for this future order.</param>
        /// <param name="prepend">Whether the action fits an idle gap before prior planned work.</param>
        private void PlanProducerOrder(
            AIManufactureProposal proposal,
            int earliestStartTicks = 0,
            bool prepend = false
        )
        {
            string key = proposal.GetProducerCapacityKey();
            if (!_plannedProducerOrders.TryGetValue(key, out List<IManufacturable> orders))
            {
                orders = new List<IManufacturable>();
                _plannedProducerOrders.Add(key, orders);
            }
            if (!_plannedProducerStartTicks.TryGetValue(key, out List<int> starts))
                _plannedProducerStartTicks.Add(key, starts = new());
            if (prepend)
            {
                orders.Insert(0, proposal.Product.GetReference());
                starts.Insert(0, earliestStartTicks);
            }
            else
            {
                orders.Add(proposal.Product.GetReference());
                starts.Add(earliestStartTicks);
            }
        }

        /// <summary>
        /// Scores a resolved fallback after shared resources have been reserved.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production decision.</param>
        /// <param name="selectedProposal">The selected exact action.</param>
        internal void ScoreResolved(
            AITurnContext context,
            AIProposal proposal,
            AIProposal selectedProposal
        )
        {
            if (
                selectedProposal is AIManufactureProposal selectedManufactureProposal
                && !ReferenceEquals(selectedProposal, proposal)
            )
                selectedManufactureProposal.SetScore(
                    _scorer.Score(context, selectedManufactureProposal)
                );
        }

        /// <summary>
        /// Reserves production resources consumed by an accepted manufacturing action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        internal void Reserve(AIManufactureProposal proposal)
        {
            _selectionState.Reserve(GetReservations(proposal));
            if (
                proposal.Product.GetReference() is not Building
                && !_scheduledActions.Contains(proposal)
            )
                PlanProducerOrder(proposal);
            ReserveProducerCapacity(proposal);
            ReserveDestinationEnergy(proposal);
            ReserveFacilityRole(proposal);
            _selectedMaintenanceCost +=
                proposal.GetMaintenanceCost() - proposal.ReplacedMaintenanceCost;
            if (proposal.Demand?.Kind == AIProductionDemandKind.PlanetaryStarfighterReserve)
                _selectedPlanetaryStarfighterCount += proposal.GetManufacturingCount();
        }

        /// <summary>
        /// Returns whether an exact manufacturing action fits its maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The exact manufacturing action.</param>
        /// <returns>True when the action can reserve its maintenance cost.</returns>
        internal bool CanReserve(AITurnContext context, AIManufactureProposal proposal)
        {
            return _selectionState.CanReserve(GetReservations(proposal))
                && !WouldExceedMaintenanceHeadroom(context, proposal)
                && CanReserveIdleShipyardCapacity(context, proposal)
                && CanReserveFacilityRole(proposal);
        }

        /// <summary>
        /// Returns whether opportunistic fighter work fits the faction-wide concurrency limit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the proposal is not opportunistic or a filler slot remains.</returns>
        private bool CanReserveIdleShipyardCapacity(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (proposal?.Demand?.UsesIdleShipyardCapacity != true)
                return true;

            int limit = context.Game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount;
            return context.Assessment.UnfinishedPlanetStarfighterCount
                    + _selectedPlanetaryStarfighterCount
                    + proposal.GetManufacturingCount()
                <= limit;
        }

        /// <summary>
        /// Resolves the largest currently available quantity for one exact producer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The exact-producer manufacturing option.</param>
        /// <param name="selectedProposal">The exact available quantity.</param>
        /// <returns>True when at least one item is selectable.</returns>
        private bool TryResolveQuantity(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            proposal = proposal.WithAdoptedFiller();
            int requestedCount = proposal.GetManufacturingCount();
            if (requestedCount <= 1)
            {
                selectedProposal = proposal;
                return CanSelect(context, proposal);
            }

            int maximumCount = GetAvailableCount(context, proposal, requestedCount);
            if (maximumCount <= 0)
            {
                selectedProposal = proposal;
                return false;
            }

            selectedProposal = proposal.WithManufacturingCount(maximumCount);
            return CanSelect(context, selectedProposal);
        }

        /// <summary>
        /// Returns whether a manufacturing action is currently feasible.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when its claims and production resources are available.</returns>
        private bool CanSelect(AITurnContext context, AIManufactureProposal proposal)
        {
            return proposal?.CanSelect(context) == true
                && _selectionState.CanReserve(GetReservations(proposal))
                && HasProducerCapacity(proposal)
                && GetAvailableDestinationEnergy(context, proposal)
                    >= proposal.GetManufacturingCount()
                && CanReserveFacilityRole(proposal)
                && ContinuesProductionStream(proposal);
        }

        /// <summary>
        /// Returns whether a facility proposal preserves the destination's role for this turn.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the destination is unreserved or reserved for the same facility category.</returns>
        private bool CanReserveFacilityRole(AIManufactureProposal proposal)
        {
            if (
                proposal?.IsProductionFacilityExpansion != true
                || proposal.Destination is not Planet destination
            )
                return true;

            return !_reservedFacilityRoles.TryGetValue(
                    destination.InstanceID,
                    out BuildingType role
                )
                || role == proposal.Demand.BuildingType;
        }

        /// <summary>
        /// Reserves a destination planet for the selected production-facility category.
        /// </summary>
        /// <param name="proposal">The selected manufacturing action.</param>
        private void ReserveFacilityRole(AIManufactureProposal proposal)
        {
            if (
                proposal?.IsProductionFacilityExpansion == true
                && proposal.Destination is Planet destination
            )
                _reservedFacilityRoles[destination.InstanceID] = proposal.Demand.BuildingType;
        }

        /// <summary>
        /// Returns the quantity allowed by production capacity, energy, and maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <param name="requestedCount">The requested manufacturing count.</param>
        /// <returns>The currently feasible count.</returns>
        private int GetAvailableCount(
            AITurnContext context,
            AIManufactureProposal proposal,
            int requestedCount
        )
        {
            int availableCount = requestedCount;
            if (proposal.UsesSharedProducerCapacity)
            {
                string capacityKey = proposal.GetProducerCapacityKey();
                int reservedCapacity = _reservedProducerCapacity.TryGetValue(
                    capacityKey,
                    out int reserved
                )
                    ? reserved
                    : 0;
                if (!_availableProducerCapacity.TryGetValue(capacityKey, out int capacity))
                {
                    capacity = proposal.AvailableProducerCapacity;
                    _availableProducerCapacity[capacityKey] = capacity;
                }

                availableCount = Math.Min(availableCount, Math.Max(0, capacity - reservedCapacity));
            }

            availableCount = Math.Min(
                availableCount,
                GetAvailableDestinationEnergy(context, proposal)
            );
            int unitMaintenanceCost = proposal.GetUnitMaintenanceCost();
            if (unitMaintenanceCost <= 0)
                return availableCount;

            int availableMaintenance =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - proposal.GetMinimumMaintenanceHeadroom(context)
                + proposal.ReplacedMaintenanceCost;
            return Math.Min(
                availableCount,
                Math.Max(0, availableMaintenance / unitMaintenanceCost)
            );
        }

        /// <summary>
        /// Returns whether the selected producer has capacity remaining this turn.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the producer has unreserved capacity.</returns>
        private bool HasProducerCapacity(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            if (!proposal.UsesSharedProducerCapacity)
                return !_reservedProducerCapacity.ContainsKey(capacityKey);

            int reservedCapacity = _reservedProducerCapacity.TryGetValue(
                capacityKey,
                out int reserved
            )
                ? reserved
                : 0;
            if (!_availableProducerCapacity.TryGetValue(capacityKey, out int capacity))
            {
                capacity = proposal.AvailableProducerCapacity;
                _availableProducerCapacity[capacityKey] = capacity;
            }

            return reservedCapacity < capacity;
        }

        /// <summary>
        /// Returns whether a proposal preserves the producer's current product stream.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the stream is idle or already produces the proposed item.</returns>
        private bool ContinuesProductionStream(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            string proposedTypeId = proposal.Product?.GetReference()?.GetTypeID();
            if (string.IsNullOrEmpty(proposedTypeId))
                return false;

            if (_selectedProducerProducts.TryGetValue(capacityKey, out string selectedTypeId))
                return selectedTypeId == proposedTypeId;
            if (proposal.InterruptsFillerProduction)
                return true;
            if (_drainingProducerStreams.Contains(capacityKey))
                return false;

            string currentTypeId = GetCurrentProductTypeId(proposal, capacityKey);
            if (string.IsNullOrEmpty(currentTypeId) || currentTypeId == proposedTypeId)
                return true;

            _drainingProducerStreams.Add(capacityKey);
            return false;
        }

        /// <summary>
        /// Returns the product type currently queued by a producer stream.
        /// </summary>
        /// <param name="proposal">A proposal targeting the producer stream.</param>
        /// <param name="capacityKey">The producer stream key.</param>
        /// <returns>The queued type, an empty value, or the mixed-type marker.</returns>
        private string GetCurrentProductTypeId(AIManufactureProposal proposal, string capacityKey)
        {
            if (_currentProducerProducts.TryGetValue(capacityKey, out string currentTypeId))
                return currentTypeId;

            currentTypeId = string.Empty;
            if (
                proposal
                    .ProducerPlanet.GetManufacturingQueue()
                    .TryGetValue(proposal.Demand.ManufacturingType, out List<IManufacturable> queue)
                && queue != null
            )
            {
                foreach (IManufacturable item in queue)
                {
                    string itemTypeId = item?.GetTypeID();
                    if (string.IsNullOrEmpty(currentTypeId))
                    {
                        currentTypeId = itemTypeId ?? string.Empty;
                        continue;
                    }

                    if (currentTypeId != itemTypeId)
                    {
                        currentTypeId = _mixedProductType;
                        break;
                    }
                }
            }

            _currentProducerProducts.Add(capacityKey, currentTypeId);
            return currentTypeId;
        }

        /// <summary>
        /// Reserves the producer capacity consumed by an accepted action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        private void ReserveProducerCapacity(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            _selectedProducerProducts[capacityKey] = proposal.Product.GetReference().GetTypeID();
            if (!proposal.UsesSharedProducerCapacity)
            {
                _reservedProducerCapacity[capacityKey] = int.MaxValue;
                return;
            }

            int reservedCapacity = _reservedProducerCapacity.TryGetValue(
                capacityKey,
                out int reserved
            )
                ? reserved
                : 0;
            long updatedCapacity =
                (long)reservedCapacity + Math.Max(1, proposal.GetManufacturingCount());
            _reservedProducerCapacity[capacityKey] =
                updatedCapacity > int.MaxValue ? int.MaxValue : (int)updatedCapacity;
        }

        /// <summary>
        /// Returns destination energy remaining after accepted building actions.
        /// </summary>
        /// <param name="context">The current turn's finished layout allocations.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>Remaining energy, or an unbounded value for non-building work.</returns>
        private int GetAvailableDestinationEnergy(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (
                proposal?.Product?.GetReference() is not Building building
                || proposal.Demand?.Kind == AIProductionDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
                return int.MaxValue;

            _reservedDestinationBuildings.TryGetValue(
                destination.InstanceID,
                out var selectedBuildings
            );
            int reserved = selectedBuildings?.Values.Sum() ?? 0;
            int actualAvailable =
                destination.GetAvailableEnergy() + proposal.ReleasedDestinationSpace - reserved;
            int plannedAvailable = context.StrategicPlan.GetAvailableConstructionSpace(
                destination,
                building.BuildingType,
                proposal.Demand.IsEmergencyDefense,
                selectedBuildings
            );
            return Math.Max(0, Math.Min(actualAvailable, plannedAvailable));
        }

        /// <summary>
        /// Reserves destination energy consumed by an accepted building action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        private void ReserveDestinationEnergy(AIManufactureProposal proposal)
        {
            if (
                proposal.Product?.GetReference() is not Building building
                || proposal.Demand?.Kind == AIProductionDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
                return;

            ReserveDestinationEnergy(
                destination,
                building.BuildingType,
                proposal.GetManufacturingCount()
            );
        }

        /// <summary>
        /// Reserves building slots of one type at a destination planet.
        /// </summary>
        /// <param name="destination">Planet whose building capacity is reserved.</param>
        /// <param name="buildingType">The building type occupying the slots.</param>
        /// <param name="count">Number of building slots to reserve.</param>
        private void ReserveDestinationEnergy(
            Planet destination,
            BuildingType buildingType,
            int count
        )
        {
            if (
                !_reservedDestinationBuildings.TryGetValue(
                    destination.InstanceID,
                    out var buildings
                )
            )
            {
                buildings = new Dictionary<BuildingType, int>();
                _reservedDestinationBuildings.Add(destination.InstanceID, buildings);
            }
            buildings.TryGetValue(buildingType, out int reserved);
            buildings[buildingType] = reserved + count;
        }

        /// <summary>
        /// Reserves maintenance headroom consumed by an accepted action.
        /// </summary>
        /// <param name="maintenanceCost">The non-negative maintenance cost to reserve.</param>
        private void ReserveMaintenanceCost(int maintenanceCost)
        {
            long reserved = (long)_selectedMaintenanceCost + Math.Max(0, maintenanceCost);
            _selectedMaintenanceCost = (int)Math.Min(int.MaxValue, reserved);
        }

        /// <summary>
        /// Returns whether an action would exceed its maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the action exceeds its required reserve.</returns>
        private bool WouldExceedMaintenanceHeadroom(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (proposal.Demand?.RestoresMaintenanceCapacity == true)
                return false;

            int maintenanceCost = proposal.GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return false;

            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - maintenanceCost
                + proposal.ReplacedMaintenanceCost;
            return projectedHeadroom < proposal.GetMinimumMaintenanceHeadroom(context);
        }

        /// <summary>
        /// Returns typed reservations for a production proposal.
        /// </summary>
        /// <param name="proposal">Production proposal to inspect.</param>
        /// <returns>Reservations required by the proposal.</returns>
        private static List<AISelectionReservation> GetReservations(AIProposal proposal)
        {
            List<AISelectionReservation> reservations = new();
            if (proposal is AIFacilityRemovalProposal removal)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    removal.Planet?.InstanceID,
                    removal.BuildingType.ToString()
                );
                return reservations;
            }

            if (proposal is AIHeadquartersRelocationProposal relocation)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    relocation.Destination?.InstanceID,
                    relocation.BuildingToRemove?.GetBuildingType().ToString()
                );
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionBuildingReplacement,
                    relocation.BuildingToRemove?.InstanceID
                );
                foreach (Fleet fleet in relocation.DefensiveFleets)
                {
                    Add(reservations, AISelectionReservationKind.FleetOrder, fleet.InstanceID);
                    Add(reservations, AISelectionReservationKind.FleetMovement, fleet.InstanceID);
                }
                Add(
                    reservations,
                    AISelectionReservationKind.MissionActor,
                    relocation.EspionageAgent?.InstanceID
                );
                Add(
                    reservations,
                    AISelectionReservationKind.Mission,
                    relocation.EspionageMission?.InstanceID
                );
                return reservations;
            }

            if (proposal is not AIManufactureProposal manufacture)
                return reservations;

            if (manufacture.Demand != null && !manufacture.DistributesDemand)
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionDemand,
                    manufacture.Demand.Id
                );
            if (manufacture.ProducerPlanet != null && !manufacture.UsesSharedProducerCapacity)
                Add(
                    reservations,
                    AISelectionReservationKind.ManufacturingLane,
                    manufacture.GetProducerCapacityKey()
                );
            if (
                manufacture.Product?.GetReference() is Building building
                && manufacture.Destination is Planet destinationPlanet
                && building.GetBuildingType()
                    is BuildingType.Shipyard
                        or BuildingType.ConstructionFacility
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    destinationPlanet.InstanceID,
                    building.GetBuildingType().ToString()
                );
            Add(
                reservations,
                AISelectionReservationKind.ProductionBuildingReplacement,
                manufacture.Demand?.BuildingToReplace?.InstanceID
            );
            if (manufacture.Destination is Fleet destinationFleet && !manufacture.DistributesDemand)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FleetReinforcement,
                    destinationFleet.InstanceID,
                    manufacture.Demand?.Kind.ToString()
                );
                if (manufacture.Demand?.Kind == AIProductionDemandKind.FleetCapitalShip)
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetCapitalReinforcement,
                        destinationFleet.InstanceID
                    );
            }
            if (
                manufacture.Demand?.Kind
                is AIProductionDemandKind.FleetSeedCapitalShip
                    or AIProductionDemandKind.ColonizationFleetSeedCapitalShip
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FleetCreation,
                    manufacture.Destination?.GetOwnerInstanceID()
                );
            return reservations;
        }

        /// <summary>
        /// Adds a reservation only when its primary identity exists.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="kind">Reservation category.</param>
        /// <param name="primaryId">Primary resource identity.</param>
        /// <param name="secondaryId">Optional secondary identity.</param>
        private static void Add(
            List<AISelectionReservation> reservations,
            AISelectionReservationKind kind,
            string primaryId,
            string secondaryId = null
        )
        {
            if (!string.IsNullOrEmpty(primaryId))
                reservations.Add(new AISelectionReservation(kind, primaryId, secondaryId));
        }
    }
}
