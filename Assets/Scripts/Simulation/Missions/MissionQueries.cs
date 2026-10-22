using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Mathematics;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates mission eligibility and odds without starting or advancing missions.
    /// </summary>
    public sealed class MissionQueries
    {
        private readonly GameRoot _game;
        private readonly MissionFactory _missionFactory;
        private readonly Func<ISceneNode, Planet, Officer> _detectorCommander;
        private readonly MissionEncounterOdds.Cache _encounterCache;
        private Dictionary<
            (Dictionary<int, int> Table, int Score, int Default),
            int
        > _probabilityLookups;
        private Dictionary<
            (ISceneNode Detector, Planet Planet),
            (Officer Commander, int Detection, bool Orbital)
        > _encounterDefenders;
        private Dictionary<(ISceneNode Scope, Type Type, string Owner), Officer> _commanders;

        /// <summary>
        /// Creates mission queries for the supplied game state.
        /// </summary>
        /// <param name="game">The game used to resolve participants and mission rules.</param>
        public MissionQueries(GameRoot game)
            : this(game, Mission.GetDetectorCommander) { }

        /// <summary>
        /// Creates mission queries using the caller's encounter-commander assessment.
        /// </summary>
        /// <param name="game">The game used to resolve participants and mission rules.</param>
        /// <param name="detectorCommander">Resolves commanders from the caller's observed state.</param>
        internal MissionQueries(GameRoot game, Func<ISceneNode, Planet, Officer> detectorCommander)
            : this(game, detectorCommander, null) { }

        /// <summary>
        /// Creates queries with caller-scoped reuse of exact encounter calculations.
        /// </summary>
        /// <param name="game">The game whose current rules are evaluated.</param>
        /// <param name="detectorCommander">Resolves observed defending commanders.</param>
        /// <param name="encounterCache">Numerical results retained only for this planning scope.</param>
        internal MissionQueries(
            GameRoot game,
            Func<ISceneNode, Planet, Officer> detectorCommander,
            MissionEncounterOdds.Cache encounterCache
        )
        {
            _game = game;
            _missionFactory = new MissionFactory(game);
            _detectorCommander =
                detectorCommander ?? throw new ArgumentNullException(nameof(detectorCommander));
            _encounterCache = encounterCache;
        }

        /// <summary>
        /// Returns whether the supplied context can create a mission.
        /// </summary>
        /// <param name="context">The mission context to resolve and evaluate.</param>
        /// <returns>True when the mission can be created.</returns>
        public bool CanCreateMission(MissionContext context)
        {
            MissionContext resolvedContext = ResolveMissionContext(context);
            return resolvedContext != null
                && _missionFactory.TryCreateMission(resolvedContext, out _);
        }

        /// <summary>
        /// Creates a mission from a context without starting it.
        /// </summary>
        /// <param name="context">The mission context to resolve.</param>
        /// <param name="mission">The created mission when successful.</param>
        /// <returns>True when the context creates a valid mission.</returns>
        public bool TryCreateMission(MissionContext context, out Mission mission)
        {
            MissionContext resolvedContext = ResolveMissionContext(context);
            if (resolvedContext == null)
            {
                mission = null;
                return false;
            }

            return _missionFactory.TryCreateMission(resolvedContext, out mission);
        }

        /// <summary>
        /// Returns the mission options available for the supplied context.
        /// </summary>
        /// <param name="context">The mission context to resolve and evaluate.</param>
        /// <returns>The mission options that can be created from the resolved context.</returns>
        public List<MissionOption> GetAvailableMissionOptions(MissionContext context)
        {
            MissionContext resolvedContext = ResolveMissionContext(context);
            return resolvedContext != null
                ? _missionFactory.GetAvailableMissionOptions(resolvedContext)
                : new List<MissionOption>();
        }

        /// <summary>
        /// Calculates the objective success probability without resolving an outcome.
        /// </summary>
        /// <param name="mission">The mission whose probability rules apply.</param>
        /// <param name="participants">The participants to evaluate.</param>
        /// <returns>The chance that at least one participant succeeds if the objective is reached.</returns>
        public double GetObjectiveSuccessProbability(
            Mission mission,
            IEnumerable<IMissionParticipant> participants
        )
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));

            return mission.GetObjectiveSuccessProbability(participants, _game);
        }

        /// <summary>
        /// Estimates a mission's visible objective-roll and foiling chances without resolving an
        /// outcome. Hidden betrayal and state changes produced during uprising resolution are
        /// intentionally excluded. Foiling uses the caller's observed destination state.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectors">
        /// Optional detector snapshot already filtered for the mission owner.
        /// </param>
        /// <param name="includePersonnelLoss">Whether to calculate personnel-loss risk.</param>
        /// <returns>The complete mission odds, or null when the request cannot create a mission.</returns>
        public MissionOdds GetMissionOdds(
            MissionContext context,
            IReadOnlyList<ISceneNode> observedDetectors = null,
            bool includePersonnelLoss = true
        )
        {
            Planet target = context?.Location as Planet;
            return GetMissionOddsCore(
                context,
                observedDetectors == null
                    ? null
                    : (mission, planet, phase, objective) =>
                        planet?.InstanceID == target?.InstanceID
                            ? FilterEncounterDetectors(
                                mission,
                                planet,
                                phase,
                                objective,
                                observedDetectors
                            )
                            : Array.Empty<ISceneNode>(),
                includePersonnelLoss
            );
        }

        /// <summary>
        /// Estimates the objective and foiling chances used to compare mission alternatives.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <returns>
        /// The operational mission odds with personnel loss left at zero, or null when the request
        /// cannot create a mission.
        /// </returns>
        internal MissionOdds GetOperationalMissionOdds(MissionContext context)
        {
            return GetMissionOddsCore(context, null, false);
        }

        /// <summary>
        /// Estimates mission odds from detector snapshots supplied for every encounter planet.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectorSource">
        /// Supplies detector candidates from the caller's observed state for a planet.
        /// </param>
        /// <param name="includePersonnelLoss">Whether to calculate personnel-loss risk.</param>
        /// <param name="evaluationState">Optional turn-scoped objective assessment.</param>
        /// <returns>The complete mission odds, or null when the request cannot create a mission.</returns>
        internal MissionOdds GetMissionOdds(
            MissionContext context,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            bool includePersonnelLoss = true,
            Mission.MissionEvaluationState evaluationState = null
        )
        {
            if (observedDetectorSource == null)
                throw new ArgumentNullException(nameof(observedDetectorSource));

            return GetMissionOddsCore(
                context,
                observedDetectorSource,
                includePersonnelLoss,
                evaluationState: evaluationState
            );
        }

        /// <summary>
        /// Estimates objective and foiling chances from detector snapshots for every encounter planet.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectorSource">Supplies observed detector candidates by planet.</param>
        /// <returns>The operational mission odds, or null when the request cannot create a mission.</returns>
        internal MissionOdds GetOperationalMissionOdds(
            MissionContext context,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource
        )
        {
            if (observedDetectorSource == null)
                throw new ArgumentNullException(nameof(observedDetectorSource));

            return GetMissionOddsCore(context, observedDetectorSource, false);
        }

        /// <summary>
        /// Returns exact operational odds only when the team's success floor remains reachable.
        /// </summary>
        /// <param name="context">The team and objective to evaluate.</param>
        /// <param name="observedDetectorSource">The caller's observed encounter defenders.</param>
        /// <param name="minimumOverallProbability">The required overall success percentage.</param>
        /// <param name="odds">The complete operational odds when the team meets the floor.</param>
        /// <returns>True when the team meets the floor; otherwise false without partial odds.</returns>
        internal bool TryGetOperationalMissionOdds(
            MissionContext context,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            double minimumOverallProbability,
            out MissionOdds odds
        )
        {
            odds = GetMissionOddsCore(
                context,
                observedDetectorSource,
                false,
                minimumOverallProbability
            );
            if (odds != null && odds.OverallSuccessProbability >= minimumOverallProbability)
                return true;
            odds = null;
            return false;
        }

        /// <summary>
        /// Bounds success by allowing the best present decoy to divert every defender without exhaustion.
        /// </summary>
        /// <param name="context">The fixed primary team and complete candidate-decoy pool.</param>
        /// <param name="observedDetectorSource">The observed encounter defenders.</param>
        /// <param name="minimumOverallProbability">The floor below which evaluation may stop.</param>
        /// <returns>The optimistic success percentage, or null when even that bound misses the floor.</returns>
        internal double? GetMissionSuccessUpperBound(
            MissionContext context,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            double minimumOverallProbability
        ) =>
            GetMissionOddsCore(
                context,
                observedDetectorSource,
                false,
                minimumOverallProbability,
                true
            )?.OverallSuccessProbability;

        /// <summary>
        /// Calculates objective and encounter odds without mutating the mission or its participants.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectorSource">
        /// Optional source of detector candidates from the caller's observed state.
        /// </param>
        /// <param name="includePersonnelLoss">
        /// Whether to retain the additional encounter history required for personnel-loss odds.
        /// </param>
        /// <param name="minimumOverallProbability">The required success floor, or zero for an unbounded query.</param>
        /// <param name="reusableDecoys">Whether to calculate an optimistic bound rather than actual odds.</param>
        /// <param name="evaluationState">Optional turn-scoped objective assessment.</param>
        /// <returns>The complete mission odds, or null when the request cannot create a mission.</returns>
        private MissionOdds GetMissionOddsCore(
            MissionContext context,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            bool includePersonnelLoss,
            double minimumOverallProbability = 0,
            bool reusableDecoys = false,
            Mission.MissionEvaluationState evaluationState = null
        )
        {
            var previousLookups = _probabilityLookups;
            var previousDefenders = _encounterDefenders;
            var previousCommanders = _commanders;
            _probabilityLookups = new();
            _encounterDefenders = new();
            _commanders = new();
            try
            {
                if (!TryCreateMission(context, out Mission mission))
                    return null;

                double objectiveSuccessProbability = mission.GetObjectiveSuccessProbability(
                    mission.GetMainParticipants(),
                    _game,
                    context.Location as Planet,
                    context.SelectedTarget,
                    evaluationState
                );
                if (objectiveSuccessProbability < minimumOverallProbability)
                    return null;
                double foilProbability;
                double personnelLossProbability;
                if (includePersonnelLoss)
                {
                    (foilProbability, personnelLossProbability) = EstimateEncounterOdds(
                        mission,
                        context.Location as Planet,
                        context.SelectedTarget ?? context.Location,
                        observedDetectorSource
                    );
                }
                else
                {
                    double? estimatedFoil = EstimateFoilProbability(
                        mission,
                        context.Location as Planet,
                        context.SelectedTarget ?? context.Location,
                        observedDetectorSource,
                        minimumOverallProbability,
                        objectiveSuccessProbability,
                        reusableDecoys
                    );
                    if (!estimatedFoil.HasValue)
                        return null;
                    foilProbability = estimatedFoil.Value;
                    personnelLossProbability = 0;
                }
                return new MissionOdds(
                    objectiveSuccessProbability,
                    foilProbability,
                    personnelLossProbability
                );
            }
            finally
            {
                _probabilityLookups = previousLookups;
                _encounterDefenders = previousDefenders;
                _commanders = previousCommanders;
            }
        }

        /// <summary>
        /// Estimates cumulative foil probability without retaining defender-history states.
        /// </summary>
        /// <param name="mission">The unstarted mission to evaluate.</param>
        /// <param name="target">The mission destination.</param>
        /// <param name="objective">The observed objective whose container determines detector scope.</param>
        /// <param name="observedDetectorSource">Optional observed detector candidates by planet.</param>
        /// <param name="minimumOverallProbability">The required success percentage, or zero for an unbounded query.</param>
        /// <param name="objectiveSuccessProbability">The team's objective success percentage.</param>
        /// <param name="reusableDecoys">Whether decoys may be reused to calculate an optimistic bound.</param>
        /// <returns>The cumulative foil percentage, or null when the requested floor is unreachable.</returns>
        private double? EstimateFoilProbability(
            Mission mission,
            Planet target,
            ISceneNode objective,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            double minimumOverallProbability = 0,
            double objectiveSuccessProbability = 100,
            bool reusableDecoys = false
        )
        {
            if (mission == null || target == null)
                return 0;

            IReadOnlyList<IMissionParticipant> allDecoys = mission.GetDecoyParticipants();
            BigInteger availableDecoys =
                allDecoys.Count == 0
                    ? BigInteger.Zero
                    : (BigInteger.One << allDecoys.Count) - BigInteger.One;
            Dictionary<BigInteger, double> survivingByDecoyPool = new Dictionary<BigInteger, double>
            {
                { availableDecoys, 1d },
            };

            List<Planet> origins = mission
                .GetAllParticipants()
                .Select(participant => participant.GetParentOfType<Planet>())
                .Where(planet => planet != null)
                .Distinct()
                .ToList();
            foreach (Planet origin in origins)
            {
                IReadOnlyList<IMissionParticipant> mainParticipants = mission
                    .GetMainParticipants()
                    .Where(participant => participant.GetParentOfType<Planet>() == origin)
                    .ToList();
                if (
                    !ApplyFoilEncounterOdds(
                        mission,
                        origin,
                        objective,
                        MissionEncounterPhase.DepartureStart,
                        mainParticipants,
                        allDecoys,
                        GetDecoyIndexesAtPlanet(allDecoys, origin),
                        observedDetectorSource,
                        ref survivingByDecoyPool,
                        minimumOverallProbability,
                        objectiveSuccessProbability,
                        reusableDecoys
                    )
                )
                    return null;
                if (
                    !ApplyFoilEncounterOdds(
                        mission,
                        origin,
                        objective,
                        MissionEncounterPhase.DepartureComplete,
                        mainParticipants,
                        allDecoys,
                        GetDecoyIndexesAtPlanet(allDecoys, origin),
                        observedDetectorSource,
                        ref survivingByDecoyPool,
                        minimumOverallProbability,
                        objectiveSuccessProbability,
                        reusableDecoys
                    )
                )
                    return null;
            }

            IReadOnlyList<int> allDecoyIndexes = Enumerable.Range(0, allDecoys.Count).ToList();
            if (
                !ApplyFoilEncounterOdds(
                    mission,
                    target,
                    objective,
                    MissionEncounterPhase.Arrival,
                    mission.GetMainParticipants(),
                    allDecoys,
                    allDecoyIndexes,
                    observedDetectorSource,
                    ref survivingByDecoyPool,
                    minimumOverallProbability,
                    objectiveSuccessProbability,
                    reusableDecoys
                )
            )
                return null;
            if (
                !ApplyFoilEncounterOdds(
                    mission,
                    target,
                    objective,
                    MissionEncounterPhase.PreObjective,
                    mission.GetMainParticipants(),
                    allDecoys,
                    allDecoyIndexes,
                    observedDetectorSource,
                    ref survivingByDecoyPool,
                    minimumOverallProbability,
                    objectiveSuccessProbability,
                    reusableDecoys
                )
            )
                return null;

            double survivalProbability = survivingByDecoyPool.Values.Sum();
            return Math.Clamp((1d - survivalProbability) * 100d, 0, 100);
        }

        /// <summary>
        /// Advances exact no-foil probability through one encounter checkpoint.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="objective">The observed mission objective.</param>
        /// <param name="phase">The encounter checkpoint.</param>
        /// <param name="mainParticipants">The primary participants present.</param>
        /// <param name="allDecoys">Every decoy assigned to the mission.</param>
        /// <param name="encounterDecoyIndexes">Indexes of decoys present at this encounter.</param>
        /// <param name="observedDetectorSource">Optional observed detector candidates by planet.</param>
        /// <param name="survivingByDecoyPool">No-foil probability by available decoy pool.</param>
        /// <param name="minimumOverallProbability">The required overall success percentage.</param>
        /// <param name="objectiveSuccessProbability">The team's objective success percentage.</param>
        /// <param name="reusableDecoys">Whether decoys may be reused to calculate an optimistic bound.</param>
        /// <returns>True when the required floor remains reachable.</returns>
        private bool ApplyFoilEncounterOdds(
            Mission mission,
            Planet planet,
            ISceneNode objective,
            MissionEncounterPhase phase,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> encounterDecoyIndexes,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            ref Dictionary<BigInteger, double> survivingByDecoyPool,
            double minimumOverallProbability = 0,
            double objectiveSuccessProbability = 100,
            bool reusableDecoys = false
        )
        {
            if (survivingByDecoyPool.Count == 0)
                return minimumOverallProbability <= 0;
            if (mainParticipants.Count == 0)
                return true;

            IReadOnlyList<ISceneNode> detectors = GetEncounterDetectors(
                mission,
                planet,
                phase,
                objective,
                observedDetectorSource
            );
            if (detectors.Count == 0)
                return true;

            int participantScore = GetFoilParticipantScore(mainParticipants);
            if (reusableDecoys)
            {
                double incoming = survivingByDecoyPool.Values.Sum();
                double survival = 1d;
                int modifier = GetFoilChanceModifier(mission);
                IMissionParticipant[] distinctDecoys = encounterDecoyIndexes
                    .Select(index => allDecoys[index])
                    .GroupBy(decoy => decoy.GetEffectiveRating(SkillRating.Espionage))
                    .Select(group => group.First())
                    .ToArray();
                foreach (ISceneNode detector in detectors)
                {
                    Planet defenderPlanet = detector.GetParentOfType<Planet>() ?? planet;
                    double diversion = 0;
                    foreach (IMissionParticipant decoy in distinctDecoys)
                        diversion = Math.Max(
                            diversion,
                            Math.Clamp(
                                GetDecoyProbability(mission, decoy, detector, defenderPlanet)
                                    / 100d,
                                0,
                                1
                            )
                        );
                    double unfoiled =
                        1d
                        - Math.Clamp(
                            GetFoilProbabilityForScore(
                                detector,
                                modifier,
                                participantScore,
                                defenderPlanet
                            ) / 100d,
                            0,
                            1
                        );
                    survival *= diversion + (1d - diversion) * unfoiled;
                    if (
                        MissionOdds.CalculateOverallSuccessProbability(
                            objectiveSuccessProbability,
                            (1d - incoming * survival) * 100d
                        ) < minimumOverallProbability
                    )
                        return false;
                }
                foreach (BigInteger pool in survivingByDecoyPool.Keys.ToArray())
                    survivingByDecoyPool[pool] *= survival;
                return true;
            }

            if ((BigInteger.One << encounterDecoyIndexes.Count) <= detectors.Count + 1)
            {
                survivingByDecoyPool = AccumulateFoilEncounterOdds(
                    mission,
                    planet,
                    mainParticipants,
                    allDecoys,
                    encounterDecoyIndexes,
                    detectors,
                    survivingByDecoyPool,
                    minimumOverallProbability,
                    objectiveSuccessProbability
                );
                return survivingByDecoyPool != null;
            }

            int foilChanceModifier = GetFoilChanceModifier(mission);
            double[] foilSurvivalProbabilities = new double[detectors.Count];
            double[,] diversionProbabilities = new double[allDecoys.Count, detectors.Count];
            for (int detectorIndex = 0; detectorIndex < detectors.Count; detectorIndex++)
            {
                ISceneNode detector = detectors[detectorIndex];
                foilSurvivalProbabilities[detectorIndex] =
                    1d
                    - Math.Clamp(
                        GetFoilProbabilityForScore(
                            detector,
                            foilChanceModifier,
                            participantScore,
                            detector.GetParentOfType<Planet>() ?? planet
                        ) / 100d,
                        0,
                        1
                    );
                foreach (int decoyIndex in encounterDecoyIndexes)
                {
                    diversionProbabilities[decoyIndex, detectorIndex] = Math.Clamp(
                        GetDecoyProbability(
                            mission,
                            allDecoys[decoyIndex],
                            detector,
                            detector.GetParentOfType<Planet>() ?? planet
                        ) / 100d,
                        0,
                        1
                    );
                }
            }

            for (int detectorIndex = 0; detectorIndex < detectors.Count; detectorIndex++)
            {
                Dictionary<BigInteger, double> nextStates = new Dictionary<BigInteger, double>();
                foreach ((BigInteger decoyPool, double probability) in survivingByDecoyPool)
                {
                    int selectableDecoyCount = 0;
                    foreach (int decoyIndex in encounterDecoyIndexes)
                    {
                        if (IsBitSet(decoyPool, decoyIndex))
                            selectableDecoyCount++;
                    }

                    if (selectableDecoyCount == 0)
                    {
                        AddProbability(
                            nextStates,
                            decoyPool,
                            probability * foilSurvivalProbabilities[detectorIndex]
                        );
                        continue;
                    }

                    double selectedProbability = probability / selectableDecoyCount;
                    foreach (int decoyIndex in encounterDecoyIndexes)
                    {
                        if (!IsBitSet(decoyPool, decoyIndex))
                            continue;

                        double diversionProbability = diversionProbabilities[
                            decoyIndex,
                            detectorIndex
                        ];
                        AddProbability(
                            nextStates,
                            decoyPool,
                            selectedProbability * diversionProbability
                        );
                        AddProbability(
                            nextStates,
                            ClearBit(decoyPool, decoyIndex),
                            selectedProbability
                                * (1d - diversionProbability)
                                * foilSurvivalProbabilities[detectorIndex]
                        );
                    }
                }

                survivingByDecoyPool = nextStates;
                if (
                    minimumOverallProbability > 0
                    && MissionOdds.CalculateOverallSuccessProbability(
                        objectiveSuccessProbability,
                        (1d - nextStates.Values.Sum()) * 100d
                    ) < minimumOverallProbability
                )
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Accumulates encounter probabilities using compact indexes for the reachable decoy pools.
        /// </summary>
        /// <param name="mission">The mission being evaluated.</param>
        /// <param name="encounterPlanet">The fallback location for detached observed defenders.</param>
        /// <param name="mainParticipants">The primary team at this checkpoint.</param>
        /// <param name="allDecoys">The complete decoy roster.</param>
        /// <param name="encounterDecoyIndexes">The decoys present at this checkpoint.</param>
        /// <param name="detectors">The observed defending units.</param>
        /// <param name="incoming">The incoming no-foil probabilities by remaining decoys.</param>
        /// <param name="minimumOverallProbability">The required overall success percentage.</param>
        /// <param name="objectiveSuccessProbability">The team's objective success percentage.</param>
        /// <returns>The surviving probabilities, or null when the required floor is unreachable.</returns>
        private Dictionary<BigInteger, double> AccumulateFoilEncounterOdds(
            Mission mission,
            Planet encounterPlanet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> encounterDecoyIndexes,
            IReadOnlyList<ISceneNode> detectors,
            Dictionary<BigInteger, double> incoming,
            double minimumOverallProbability,
            double objectiveSuccessProbability
        )
        {
            var (pools, remainingPools, selectableCounts) = GetDecoyPoolTransitions(
                incoming.Keys,
                allDecoys.Count,
                encounterDecoyIndexes
            );
            double[] states = new double[pools.Count];
            double[] next = new double[pools.Count];
            for (int index = 0; index < pools.Count; index++)
                incoming.TryGetValue(pools[index], out states[index]);
            int foilChanceModifier = GetFoilChanceModifier(mission);
            int participantScore = GetFoilParticipantScore(mainParticipants);
            double[] diversions = new double[allDecoys.Count];
            for (int detector = 0; detector < detectors.Count; detector++)
            {
                ISceneNode node = detectors[detector];
                Planet planet = node.GetParentOfType<Planet>() ?? encounterPlanet;
                double survival =
                    1d
                    - Math.Clamp(
                        GetFoilProbabilityForScore(
                            node,
                            foilChanceModifier,
                            participantScore,
                            planet
                        ) / 100d,
                        0,
                        1
                    );
                foreach (int decoy in encounterDecoyIndexes)
                    diversions[decoy] = Math.Clamp(
                        GetDecoyProbability(mission, allDecoys[decoy], node, planet) / 100d,
                        0,
                        1
                    );
                Array.Clear(next, 0, next.Length);
                for (int pool = 0; pool < pools.Count; pool++)
                {
                    double probability = states[pool];
                    if (probability <= 0)
                        continue;
                    int selectable = selectableCounts[pool];
                    if (selectable == 0)
                    {
                        next[pool] += probability * survival;
                        continue;
                    }
                    double selected = probability / selectable;
                    foreach (int decoy in encounterDecoyIndexes)
                    {
                        int remaining = remainingPools[pool, decoy];
                        if (remaining == pool)
                            continue;
                        double diversion = diversions[decoy];
                        next[pool] += selected * diversion;
                        next[remaining] += selected * (1d - diversion) * survival;
                    }
                }
                (states, next) = (next, states);
                if (
                    minimumOverallProbability > 0
                    && MissionOdds.CalculateOverallSuccessProbability(
                        objectiveSuccessProbability,
                        (1d - states.Sum()) * 100d
                    ) < minimumOverallProbability
                )
                    return null;
            }
            Dictionary<BigInteger, double> result = new();
            for (int index = 0; index < pools.Count; index++)
                if (states[index] > 0)
                    result.Add(pools[index], states[index]);
            return result;
        }

        /// <summary>
        /// Indexes reachable decoy pools and the transition caused by confronting each decoy.
        /// </summary>
        /// <param name="incoming">The initial available-decoy pools.</param>
        /// <param name="decoyCount">The complete decoy roster size.</param>
        /// <param name="encounterDecoyIndexes">The decoys that can be confronted here.</param>
        /// <returns>The indexed pools, confrontation destinations, and available-decoy counts.</returns>
        private static (
            List<BigInteger> Pools,
            int[,] Remaining,
            int[] Selectable
        ) GetDecoyPoolTransitions(
            IEnumerable<BigInteger> incoming,
            int decoyCount,
            IReadOnlyList<int> encounterDecoyIndexes
        )
        {
            List<BigInteger> pools = incoming.ToList();
            Dictionary<BigInteger, int> poolIndexes = new();
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
                poolIndexes.Add(pools[poolIndex], poolIndex);
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
                foreach (int index in encounterDecoyIndexes)
                {
                    BigInteger remaining = ClearBit(pools[poolIndex], index);
                    if (!poolIndexes.ContainsKey(remaining))
                    {
                        poolIndexes.Add(remaining, pools.Count);
                        pools.Add(remaining);
                    }
                }
            int[,] remainingPools = new int[pools.Count, decoyCount];
            int[] selectableCounts = new int[pools.Count];
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
                foreach (int index in encounterDecoyIndexes)
                {
                    remainingPools[poolIndex, index] = poolIndexes[
                        ClearBit(pools[poolIndex], index)
                    ];
                    if (remainingPools[poolIndex, index] != poolIndex)
                        selectableCounts[poolIndex]++;
                }
            return (pools, remainingPools, selectableCounts);
        }

        /// <summary>
        /// Resolves mission participants while preserving the caller's observed target state.
        /// </summary>
        /// <param name="context">The mission context to resolve.</param>
        /// <returns>The resolved mission context, or null when any required object is missing.</returns>
        private MissionContext ResolveMissionContext(MissionContext context)
        {
            if (
                context == null
                || context.MainParticipants == null
                || context.MainParticipants.Count == 0
                || context.Location == null
            )
                return null;

            List<IMissionParticipant> mainParticipants = ResolveMissionParticipants(
                context.MainParticipants
            );
            List<IMissionParticipant> decoyParticipants = ResolveMissionParticipants(
                context.DecoyParticipants ?? new List<IMissionParticipant>()
            );

            if (mainParticipants == null || decoyParticipants == null)
                return null;

            return new MissionContext
            {
                Game = _game,
                MissionTypeID = context.MissionTypeID,
                OwnerInstanceId = mainParticipants[0].GetOwnerInstanceID(),
                Location = context.Location,
                SelectedTarget = context.SelectedTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                Discipline = context.Discipline,
            };
        }

        /// <summary>
        /// Resolves mission participants to their live scene graph instances.
        /// </summary>
        /// <param name="participants">The participant references to resolve.</param>
        /// <returns>Resolved participants, or null if any participant cannot be resolved.</returns>
        private List<IMissionParticipant> ResolveMissionParticipants(
            List<IMissionParticipant> participants
        )
        {
            List<IMissionParticipant> resolvedParticipants = new List<IMissionParticipant>();

            foreach (IMissionParticipant participant in participants)
            {
                ISceneNode node = participant;
                IMissionParticipant resolvedParticipant =
                    ResolveSceneNode(node) as IMissionParticipant;
                if (resolvedParticipant == null)
                    return null;

                resolvedParticipants.Add(resolvedParticipant);
            }

            return resolvedParticipants;
        }

        /// <summary>
        /// Resolves a scene node reference to its live scene graph instance.
        /// </summary>
        /// <param name="node">The scene node reference to resolve.</param>
        /// <returns>The live scene node, or null if it cannot be resolved.</returns>
        internal ISceneNode ResolveSceneNode(ISceneNode node)
        {
            if (node == null)
                return null;

            return _game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
        }

        /// <summary>
        /// Estimates cumulative foil and personnel-loss chances across mission encounters.
        /// </summary>
        /// <param name="mission">The unstarted mission to evaluate.</param>
        /// <param name="target">The mission destination.</param>
        /// <param name="objective">The observed mission objective.</param>
        /// <param name="observedDetectorSource">
        /// Optional source of detector candidates from the caller's observed state.
        /// </param>
        /// <returns>The cumulative foil and personnel-loss percentages.</returns>
        private (double FoilProbability, double PersonnelLossProbability) EstimateEncounterOdds(
            Mission mission,
            Planet target,
            ISceneNode objective,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource
        )
        {
            if (mission == null || target == null)
                return (0, 0);

            IReadOnlyList<IMissionParticipant> allDecoys = mission.GetDecoyParticipants();
            BigInteger availableDecoys =
                allDecoys.Count == 0
                    ? BigInteger.Zero
                    : (BigInteger.One << allDecoys.Count) - BigInteger.One;
            Dictionary<
                BigInteger,
                (double Probability, double NoOfficerLossProbability)
            > survivingByDecoyPool = new() { { availableDecoys, (1d, 1d) } };
            double foilProbability = 0;
            double personnelLossProbability = 0;

            List<Planet> origins = mission
                .GetAllParticipants()
                .Select(participant => participant.GetParentOfType<Planet>())
                .Where(planet => planet != null)
                .Distinct()
                .ToList();
            foreach (Planet origin in origins)
            {
                IReadOnlyList<IMissionParticipant> mainParticipants = mission
                    .GetMainParticipants()
                    .Where(participant => participant.GetParentOfType<Planet>() == origin)
                    .ToList();
                IReadOnlyList<int> decoyIndexes = GetDecoyIndexesAtPlanet(allDecoys, origin);
                ApplyEncounterOdds(
                    mission,
                    origin,
                    objective,
                    MissionEncounterPhase.DepartureStart,
                    mainParticipants,
                    allDecoys,
                    decoyIndexes,
                    observedDetectorSource,
                    ref survivingByDecoyPool,
                    ref foilProbability,
                    ref personnelLossProbability
                );
                ApplyEncounterOdds(
                    mission,
                    origin,
                    objective,
                    MissionEncounterPhase.DepartureComplete,
                    mainParticipants,
                    allDecoys,
                    decoyIndexes,
                    observedDetectorSource,
                    ref survivingByDecoyPool,
                    ref foilProbability,
                    ref personnelLossProbability
                );
            }

            IReadOnlyList<int> allDecoyIndexes = Enumerable.Range(0, allDecoys.Count).ToList();
            ApplyEncounterOdds(
                mission,
                target,
                objective,
                MissionEncounterPhase.Arrival,
                mission.GetMainParticipants(),
                allDecoys,
                allDecoyIndexes,
                observedDetectorSource,
                ref survivingByDecoyPool,
                ref foilProbability,
                ref personnelLossProbability
            );
            ApplyEncounterOdds(
                mission,
                target,
                objective,
                MissionEncounterPhase.PreObjective,
                mission.GetMainParticipants(),
                allDecoys,
                allDecoyIndexes,
                observedDetectorSource,
                ref survivingByDecoyPool,
                ref foilProbability,
                ref personnelLossProbability
            );

            return (foilProbability * 100d, personnelLossProbability * 100d);
        }

        /// <summary>
        /// Applies one encounter checkpoint to the surviving probability distribution.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="objective">The observed mission objective.</param>
        /// <param name="phase">The encounter checkpoint.</param>
        /// <param name="mainParticipants">The primary participants present.</param>
        /// <param name="allDecoys">Every decoy assigned to the mission.</param>
        /// <param name="encounterDecoyIndexes">Indexes of decoys present at this encounter.</param>
        /// <param name="observedDetectorSource">Optional observed detector source.</param>
        /// <param name="survivingByDecoyPool">Unfoiled probability by available decoy pool.</param>
        /// <param name="foilProbability">Accumulated foil probability.</param>
        /// <param name="personnelLossProbability">Accumulated personnel-loss probability.</param>
        private void ApplyEncounterOdds(
            Mission mission,
            Planet planet,
            ISceneNode objective,
            MissionEncounterPhase phase,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> encounterDecoyIndexes,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            ref Dictionary<
                BigInteger,
                (double Probability, double NoOfficerLossProbability)
            > survivingByDecoyPool,
            ref double foilProbability,
            ref double personnelLossProbability
        )
        {
            if (survivingByDecoyPool.Count == 0 || mainParticipants.Count == 0)
                return;

            IReadOnlyList<ISceneNode> detectors = GetEncounterDetectors(
                mission,
                planet,
                phase,
                objective,
                observedDetectorSource
            );
            if (detectors.Count == 0)
                return;

            Officer[] officers = mission.AppliesFoiledParticipantConsequences
                ? mainParticipants.OfType<Officer>().ToArray()
                : Array.Empty<Officer>();
            double[] survivalModifiers = officers
                .Select(officer => 1d - Mission.GetEvasionDeathProbability(officer, _game.Config))
                .ToArray();
            int foilModifier = GetFoilChanceModifier(mission);
            int participantScore = GetFoilParticipantScore(mainParticipants);
            List<MissionEncounterOdds.DefenderProbabilities> probabilities = new(detectors.Count);
            foreach (ISceneNode detector in detectors)
            {
                Planet defenderPlanet = detector.GetParentOfType<Planet>() ?? planet;
                double noFoil =
                    1d
                    - Math.Clamp(
                        GetFoilProbabilityForScore(
                            detector,
                            foilModifier,
                            participantScore,
                            defenderPlanet
                        ) / 100d,
                        0,
                        1
                    );
                double[] diversions = new double[allDecoys.Count];
                double[] decoyLosses = new double[allDecoys.Count];
                foreach (int index in encounterDecoyIndexes)
                {
                    diversions[index] = Math.Clamp(
                        GetDecoyProbability(mission, allDecoys[index], detector, defenderPlanet)
                            / 100d,
                        0,
                        1
                    );
                    decoyLosses[index] = allDecoys[index] is Officer decoy
                        ? GetOfficerEncounterLossProbability(mission, decoy, detector)
                        : 0;
                }
                Officer commander = GetEncounterDefender(
                    detector,
                    mission.GetParent() as Planet
                ).Commander;
                int defenderCombat = commander?.GetEffectiveRating(SkillRating.Combat) ?? 0;
                int[] evasion = officers
                    .Select(officer =>
                        (int)
                            Math.Clamp(
                                GetEvasionProbability(
                                    officer.GetEffectiveRating(SkillRating.Combat) - defenderCombat
                                ),
                                0,
                                100
                            )
                    )
                    .ToArray();
                probabilities.Add(
                    new MissionEncounterOdds.DefenderProbabilities(
                        noFoil,
                        diversions,
                        evasion,
                        decoyLosses,
                        survivalModifiers
                    )
                );
            }
            var survivors = survivingByDecoyPool.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Probability
            );
            var noLossSurvivors = survivingByDecoyPool.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.NoOfficerLossProbability
            );
            MissionEncounterOdds.Result result =
                _encounterCache == null
                    ? MissionEncounterOdds.Apply(
                        probabilities,
                        encounterDecoyIndexes,
                        survivors,
                        noLossSurvivors
                    )
                    : _encounterCache.Evaluate(
                        probabilities,
                        encounterDecoyIndexes,
                        survivors,
                        noLossSurvivors
                    );
            survivingByDecoyPool = result.Survivors.ToDictionary(
                entry => entry.Key,
                entry => (entry.Value, result.NoLossSurvivors[entry.Key])
            );
            foilProbability += result.FoilProbability;
            personnelLossProbability += result.PersonnelLossProbability;
        }

        /// <summary>
        /// Returns the chance that a confrontation kills or captures one officer.
        /// </summary>
        /// <param name="mission">The mission whose evasion rules apply.</param>
        /// <param name="officer">The officer exposed to the confrontation.</param>
        /// <param name="detector">The opposing detector.</param>
        /// <returns>The probability of death or capture from zero to one.</returns>
        private double GetOfficerEncounterLossProbability(
            Mission mission,
            Officer officer,
            ISceneNode detector
        ) =>
            1d
            - GetParticipantEvasionProbability(mission, officer, detector)
                * (1d - Mission.GetEvasionDeathProbability(officer, _game.Config));

        /// <summary>
        /// Returns the indexes of decoys currently stationed at a planet.
        /// </summary>
        /// <param name="decoys">Every mission decoy.</param>
        /// <param name="planet">The planet to match.</param>
        /// <returns>The matching indexes.</returns>
        private static IReadOnlyList<int> GetDecoyIndexesAtPlanet(
            IReadOnlyList<IMissionParticipant> decoys,
            Planet planet
        )
        {
            return Enumerable
                .Range(0, decoys.Count)
                .Where(index => decoys[index].GetParentOfType<Planet>() == planet)
                .ToList();
        }

        /// <summary>
        /// Returns the detector set for one estimated encounter.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="phase">The encounter checkpoint.</param>
        /// <param name="objective">The observed mission objective.</param>
        /// <param name="observedDetectorSource">Optional observed detector source.</param>
        /// <returns>The eligible detector units in traversal order.</returns>
        private IReadOnlyList<ISceneNode> GetEncounterDetectors(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase,
            ISceneNode objective,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                ISceneNode,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource
        )
        {
            if (observedDetectorSource != null)
                return observedDetectorSource(mission, planet, phase, objective);

            IReadOnlyList<ISceneNode> candidates = GetDetectorCandidates(planet);
            return FilterEncounterDetectors(mission, planet, phase, objective, candidates);
        }

        /// <summary>
        /// Filters observed detector candidates for one encounter checkpoint.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="phase">The encounter checkpoint.</param>
        /// <param name="objective">The observed mission objective.</param>
        /// <param name="candidates">The observed detector candidates.</param>
        /// <returns>The eligible detector units in traversal order.</returns>
        private static IReadOnlyList<ISceneNode> FilterEncounterDetectors(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase,
            ISceneNode objective,
            IReadOnlyList<ISceneNode> candidates
        )
        {
            if (UsesPlanetaryDetectors(phase, objective))
            {
                if (
                    string.IsNullOrEmpty(planet.GetOwnerInstanceID())
                    || planet.GetOwnerInstanceID() == mission.GetOwnerInstanceID()
                )
                    return Array.Empty<ISceneNode>();
                return candidates
                    .Where(candidate =>
                        candidate is Regiment
                        && candidate.GetParent() is Planet
                        && mission.IsEligibleDetector(candidate)
                    )
                    .ToList();
            }

            bool hasHostileFleet = planet
                .GetChildren<Fleet>()
                .Any(fleet =>
                    fleet.GetOwnerInstanceID() != mission.GetOwnerInstanceID()
                    && fleet.Movement == null
                );
            if (!hasHostileFleet && phase != MissionEncounterPhase.PreObjective)
                return Array.Empty<ISceneNode>();

            if (phase != MissionEncounterPhase.PreObjective && HasDetectionBlocker(mission, planet))
                return Array.Empty<ISceneNode>();

            return candidates
                .Where(candidate =>
                    mission.IsEligibleDetector(candidate)
                    && candidate is CapitalShip or Starfighter
                    && candidate.GetParentOfType<Fleet>() != null
                )
                .ToList();
        }

        /// <summary>
        /// Returns whether a checkpoint checks planetary rather than orbital defenders.
        /// </summary>
        /// <param name="phase">The encounter checkpoint.</param>
        /// <param name="objective">The authoritative or observed mission objective.</param>
        /// <returns>True when the checkpoint uses planetary defenders.</returns>
        internal static bool UsesPlanetaryDetectors(
            MissionEncounterPhase phase,
            ISceneNode objective
        ) =>
            phase == MissionEncounterPhase.DepartureStart
            || phase == MissionEncounterPhase.PreObjective
                && objective != null
                && objective is not Fleet
                && objective.GetParentOfType<Fleet>() == null;

        /// <summary>
        /// Returns all direct and fleet-contained detector candidates at a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The detector candidates in scene traversal order.</returns>
        private static IReadOnlyList<ISceneNode> GetDetectorCandidates(Planet planet)
        {
            if (planet == null)
                return Array.Empty<ISceneNode>();

            List<ISceneNode> candidates = new List<ISceneNode>();
            candidates.AddRange(planet.GetChildren<Regiment>());
            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
                {
                    candidates.Add(capitalShip);
                    candidates.AddRange(capitalShip.GetChildren<Starfighter>());
                }
            }

            return candidates;
        }

        /// <summary>
        /// Adds probability to an existing or new state outcome.
        /// </summary>
        /// <param name="probabilities">The probabilities.</param>
        /// <param name="state">The state key.</param>
        /// <param name="probability">The probability.</param>
        /// <typeparam name="T">The probability-state key type.</typeparam>
        private static void AddProbability<T>(
            Dictionary<T, double> probabilities,
            T state,
            double probability
        )
        {
            if (probability <= 0)
                return;

            probabilities.TryGetValue(state, out double existingProbability);
            probabilities[state] = existingProbability + probability;
        }

        /// <summary>
        /// Returns whether the specified bit is set.
        /// </summary>
        /// <param name="value">The bit set.</param>
        /// <param name="index">The zero-based bit index.</param>
        /// <returns>True when the bit is set.</returns>
        private static bool IsBitSet(BigInteger value, int index) =>
            (value & (BigInteger.One << index)) != BigInteger.Zero;

        /// <summary>
        /// Returns a bit set with the specified bit disabled.
        /// </summary>
        /// <param name="value">The bit set.</param>
        /// <param name="index">The zero-based bit index.</param>
        /// <returns>The updated bit set.</returns>
        private static BigInteger ClearBit(BigInteger value, int index) =>
            value & ~(BigInteger.One << index);

        /// <summary>
        /// Returns the chance that a mission participant evades a detector.
        /// </summary>
        /// <param name="mission">The mission whose evasion rules apply.</param>
        /// <param name="participant">The participant attempting to evade detection.</param>
        /// <param name="detector">The unit confronting the participant.</param>
        /// <returns>The evasion probability from zero to one.</returns>
        private double GetParticipantEvasionProbability(
            Mission mission,
            IMissionParticipant participant,
            ISceneNode detector
        )
        {
            if (participant is not Officer && participant is not SpecialForces)
                return 1d;

            Officer commander = GetEncounterDefender(
                detector,
                mission.GetParent() as Planet
            ).Commander;
            int defenderCombat = commander?.GetEffectiveRating(SkillRating.Combat) ?? 0;
            int score = participant.GetEffectiveRating(SkillRating.Combat) - defenderCombat;
            return Math.Clamp(GetEvasionProbability(score) / 100d, 0, 1);
        }

        /// <summary>
        /// Returns one detector's configured chance to foil a mission.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile detector.</param>
        /// <returns>The foiling percentage.</returns>
        internal int GetFoilProbability(Mission mission, ISceneNode detector)
        {
            return GetFoilProbability(mission, detector, GetFoilChanceModifier(mission));
        }

        /// <summary>
        /// Returns the probability that one decoy diverts one detector.
        /// </summary>
        /// <param name="mission">The mission assigning the decoy.</param>
        /// <param name="decoy">The decoy participant to evaluate.</param>
        /// <param name="detector">The detector being diverted.</param>
        /// <param name="encounterPlanet">The planet where the encounter occurs.</param>
        /// <returns>The decoy success probability.</returns>
        internal int GetDecoyProbability(
            Mission mission,
            IMissionParticipant decoy,
            ISceneNode detector,
            Planet encounterPlanet = null
        )
        {
            if (mission == null || decoy == null || detector == null)
                return 0;

            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            var defender = GetEncounterDefender(
                detector,
                encounterPlanet ?? mission.GetParent() as Planet
            );
            long score =
                (long)decoy.GetEffectiveRating(SkillRating.Espionage)
                - defender.Detection
                - GetScaledCommanderEspionage(
                    defender.Commander,
                    missionTables.DecoyDefenderScalingPercent
                );
            Dictionary<int, int> table = defender.Orbital
                ? missionTables.FleetDecoy
                : missionTables.PlanetaryDecoy;
            return LookupProbability(table, IntegerMath.SaturateToInt(score));
        }

        /// <summary>
        /// Returns the configured foil-chance adjustment for the mission owner.
        /// </summary>
        /// <param name="mission">The mission whose owner receives the adjustment.</param>
        /// <returns>The signed percentage-point adjustment.</returns>
        internal int GetFoilChanceModifier(Mission mission)
        {
            return mission == null
                ? 0
                : _game.GetDifficultyModifier(mission.GetOwnerInstanceID()).MissionFoilChancePoints;
        }

        /// <summary>
        /// Returns one detector's configured chance to foil a mission using a resolved adjustment.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile detector.</param>
        /// <param name="foilChanceModifier">The signed percentage-point adjustment.</param>
        /// <param name="participants">The primary team present at this encounter.</param>
        /// <param name="encounterPlanet">The planet where the encounter occurs.</param>
        /// <returns>The adjusted foiling percentage.</returns>
        internal int GetFoilProbability(
            Mission mission,
            ISceneNode detector,
            int foilChanceModifier,
            IReadOnlyList<IMissionParticipant> participants = null,
            Planet encounterPlanet = null
        )
        {
            if (mission == null || detector == null)
                return 0;

            return GetFoilProbabilityForScore(
                detector,
                foilChanceModifier,
                GetFoilParticipantScore(participants ?? mission.GetMainParticipants()),
                encounterPlanet
            );
        }

        /// <summary>
        /// Resolves foiling from one unchanged team's score and one defender's strength.
        /// </summary>
        /// <param name="detector">The defending unit.</param>
        /// <param name="foilChanceModifier">The mission owner's percentage-point adjustment.</param>
        /// <param name="participantScore">The main team's concealment contribution.</param>
        /// <param name="encounterPlanet">The observed encounter location.</param>
        /// <returns>The configured, adjusted foiling percentage.</returns>
        private int GetFoilProbabilityForScore(
            ISceneNode detector,
            int foilChanceModifier,
            int participantScore,
            Planet encounterPlanet
        )
        {
            int score = IntegerMath.SaturateToInt(
                (long)participantScore - GetMissionDetectorStrength(detector, encounterPlanet)
            );
            int probability = LookupProbability(GetMissionTables().Foil, score);
            return Math.Clamp(probability + foilChanceModifier, 0, 100);
        }

        /// <summary>
        /// Calculates the concealment contribution shared across a team's defender checks.
        /// </summary>
        /// <param name="participants">The primary team present at this encounter.</param>
        /// <returns>The team's contribution before subtracting a defender's strength.</returns>
        private int GetFoilParticipantScore(IReadOnlyList<IMissionParticipant> participants)
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            return GetAverageEspionage(participants)
                - participants.OfType<SpecialForces>().Count()
                - missionTables.FoilFlatScoreAdjustment;
        }

        /// <summary>
        /// Returns a detector's effective rating plus its commander's contribution to foiling.
        /// </summary>
        /// <param name="detector">The observed detector to evaluate.</param>
        /// <param name="planet">Optional observed encounter planet.</param>
        /// <returns>The defender contribution subtracted from a mission team's Espionage rating.</returns>
        internal int GetMissionDetectorStrength(ISceneNode detector, Planet planet = null)
        {
            var defender = GetEncounterDefender(detector, planet);
            return IntegerMath.SaturateToInt(
                (long)defender.Detection
                    + GetScaledCommanderEspionage(
                        defender.Commander,
                        GetMissionTables().FoilDefenderScalingPercent
                    )
            );
        }

        /// <summary>
        /// Reuses unchanged defender context only within the current odds evaluation.
        /// </summary>
        /// <param name="detector">The observed defending unit.</param>
        /// <param name="planet">The explicit encounter planet, or null to use its container.</param>
        /// <returns>The commander, effective detection rating, and orbital detector scope.</returns>
        private (Officer Commander, int Detection, bool Orbital) GetEncounterDefender(
            ISceneNode detector,
            Planet planet
        )
        {
            var key = (detector, planet);
            if (_encounterDefenders != null && _encounterDefenders.TryGetValue(key, out var cached))
                return cached;

            var defender = (
                GetEncounterCommander(detector, planet),
                GetEffectiveDetectionRating(detector),
                detector?.GetParentOfType<Fleet>() != null
            );
            _encounterDefenders?.Add(key, defender);
            return defender;
        }

        /// <summary>
        /// Reuses commander resolution for defenders sharing a container, category, and owner.
        /// </summary>
        /// <param name="detector">The observed defending unit.</param>
        /// <param name="planet">The explicit encounter planet, or null to use its container.</param>
        /// <returns>The eligible defending commander, or null.</returns>
        private Officer GetEncounterCommander(ISceneNode detector, Planet planet)
        {
            Planet resolvedPlanet = planet ?? detector?.GetParentOfType<Planet>();
            ISceneNode scope = detector?.GetParentOfType<Fleet>();
            scope ??= resolvedPlanet;
            if (_commanders == null || scope == null || detector == null)
                return _detectorCommander(detector, resolvedPlanet);

            var key = (scope, detector.GetType(), detector.GetOwnerInstanceID());
            if (!_commanders.TryGetValue(key, out Officer commander))
            {
                commander = _detectorCommander(detector, resolvedPlanet);
                _commanders.Add(key, commander);
            }
            return commander;
        }

        /// <summary>
        /// Returns a detector's authored rating after applying its faction's difficulty modifier.
        /// </summary>
        /// <param name="detector">The detector whose effective rating is requested.</param>
        /// <returns>The difficulty-adjusted detection rating.</returns>
        private int GetEffectiveDetectionRating(ISceneNode detector)
        {
            int authoredRating = detector switch
            {
                Regiment regiment => regiment.DetectionRating,
                Starfighter starfighter => starfighter.DetectionRating,
                CapitalShip capitalShip => capitalShip.DetectionRating,
                _ => 0,
            };
            double multiplier = _game
                .GetDifficultyModifier(detector?.GetOwnerInstanceID())
                .DetectionRatingMultiplier;
            double scaledRating = Math.Round(
                authoredRating * multiplier,
                MidpointRounding.AwayFromZero
            );
            if (scaledRating >= int.MaxValue)
                return int.MaxValue;
            if (scaledRating <= int.MinValue)
                return int.MinValue;
            return (int)scaledRating;
        }

        /// <summary>
        /// Returns the mission team's average effective Espionage rating.
        /// </summary>
        /// <param name="participants">The mission's main participants.</param>
        /// <returns>The average rating, or zero when the mission has no participants.</returns>
        private static int GetAverageEspionage(IReadOnlyList<IMissionParticipant> participants)
        {
            return participants.Count == 0
                ? 0
                : participants.Sum(participant =>
                    participant.GetEffectiveRating(SkillRating.Espionage)
                ) / participants.Count;
        }

        /// <summary>
        /// Returns the configured portion of a detector commander's Espionage rating.
        /// </summary>
        /// <param name="commander">The detector commander, if one is assigned.</param>
        /// <param name="scalingPercent">The percentage of the rating applied to detection.</param>
        /// <returns>The scaled commander contribution.</returns>
        private static long GetScaledCommanderEspionage(Officer commander, int scalingPercent)
        {
            return (long)(commander?.GetEffectiveRating(SkillRating.Espionage) ?? 0)
                * scalingPercent
                / 100;
        }

        /// <summary>
        /// Returns hostile detector units for a mission lifecycle checkpoint.
        /// </summary>
        /// <param name="mission">The mission being checked for detection.</param>
        /// <param name="planet">The planet where the mission is operating.</param>
        /// <param name="phase">The mission lifecycle checkpoint being evaluated.</param>
        /// <returns>The ordered detector units.</returns>
        internal List<ISceneNode> GetDetectors(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase
        )
        {
            List<ISceneNode> detectors = new List<ISceneNode>();
            if (mission == null || planet == null)
                return detectors;

            if (
                phase == MissionEncounterPhase.DepartureStart
                || (phase == MissionEncounterPhase.PreObjective && IsPlanetaryObjective(mission))
            )
            {
                if (
                    !string.IsNullOrEmpty(planet.GetOwnerInstanceID())
                    && planet.GetOwnerInstanceID() != mission.GetOwnerInstanceID()
                )
                    AddEligibleDetectors(mission, planet.GetChildren<Regiment>(), detectors);
                return detectors;
            }

            List<Fleet> hostileFleets = planet
                .GetChildren<Fleet>()
                .Where(fleet =>
                    fleet.GetOwnerInstanceID() != mission.GetOwnerInstanceID()
                    && fleet.Movement == null
                )
                .ToList();
            if (hostileFleets.Count == 0 && phase != MissionEncounterPhase.PreObjective)
                return detectors;

            bool blocksFleetDetection =
                phase != MissionEncounterPhase.PreObjective && HasDetectionBlocker(mission, planet);
            if (blocksFleetDetection)
                return detectors;

            foreach (Fleet fleet in hostileFleets)
            {
                foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
                {
                    if (mission.IsEligibleDetector(capitalShip))
                        detectors.Add(capitalShip);
                    AddEligibleDetectors(
                        mission,
                        capitalShip.GetChildren<Starfighter>(),
                        detectors
                    );
                }
            }

            return detectors;
        }

        /// <summary>
        /// Returns whether the mission's objective is on a planet rather than aboard a fleet.
        /// </summary>
        /// <param name="mission">The mission whose current target is evaluated.</param>
        /// <returns>True for a planetary objective.</returns>
        internal bool IsPlanetaryObjective(Mission mission)
        {
            string targetID = mission is IEntityTargetingMission targetedMission
                ? targetedMission.TargetEntityInstanceID
                : mission.LocationInstanceID;
            ISceneNode target = _game.GetSceneNodeByInstanceID<ISceneNode>(targetID);
            return UsesPlanetaryDetectors(MissionEncounterPhase.PreObjective, target);
        }

        /// <summary>
        /// Returns whether a completed friendly building suppresses approach encounters.
        /// </summary>
        /// <param name="mission">The mission whose owner receives protection.</param>
        /// <param name="planet">The planet containing candidate buildings.</param>
        /// <returns>True when an eligible building is present.</returns>
        internal static bool HasDetectionBlocker(Mission mission, Planet planet)
        {
            return mission != null
                && planet
                    ?.GetChildren<Building>()
                    .Any(building =>
                        building.IsDetectionBlocker
                        && building.OwnerInstanceID == mission.OwnerInstanceID
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                    ) == true;
        }

        /// <summary>
        /// Appends eligible detector units without changing their scene order.
        /// </summary>
        /// <param name="mission">The mission being checked for detection.</param>
        /// <param name="candidates">The candidate detector units.</param>
        /// <param name="detectors">The collection receiving eligible detectors.</param>
        private static void AddEligibleDetectors(
            Mission mission,
            IEnumerable<ISceneNode> candidates,
            ICollection<ISceneNode> detectors
        )
        {
            foreach (ISceneNode candidate in candidates)
            {
                if (mission.IsEligibleDetector(candidate))
                    detectors.Add(candidate);
            }
        }

        /// <summary>
        /// Returns the configured evasion probability for a confronted participant.
        /// </summary>
        /// <param name="score">The participant combat rating minus commander combat rating.</param>
        /// <returns>The configured evasion probability.</returns>
        internal double GetEvasionProbability(int score)
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            return LookupProbability(
                missionTables.Evasion,
                score,
                missionTables.DefaultEvasionProbability
            );
        }

        /// <summary>
        /// Returns the mission probability table config for the current game.
        /// </summary>
        /// <returns>The configured mission probability tables.</returns>
        private GameConfig.MissionProbabilityTablesConfig GetMissionTables()
        {
            return _game.Config?.ProbabilityTables?.Mission
                ?? new GameConfig.MissionProbabilityTablesConfig();
        }

        /// <summary>
        /// Returns the configured probability for a score.
        /// </summary>
        /// <param name="entries">The configured probability table entries.</param>
        /// <param name="score">The score to look up.</param>
        /// <param name="defaultValue">The value returned when the table is empty.</param>
        /// <returns>The configured probability value.</returns>
        private int LookupProbability(Dictionary<int, int> entries, int score, int defaultValue = 0)
        {
            if (entries == null || entries.Count == 0)
                return defaultValue;

            var key = (entries, score, defaultValue);
            if (_probabilityLookups != null && _probabilityLookups.TryGetValue(key, out int cached))
                return cached;

            int probability = new ProbabilityTable(entries).Lookup(score);
            _probabilityLookups?.Add(key, probability);
            return probability;
        }
    }
}
