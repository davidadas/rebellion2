using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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
        {
            _game = game;
            _missionFactory = new MissionFactory(game);
            _detectorCommander =
                detectorCommander ?? throw new ArgumentNullException(nameof(detectorCommander));
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
        /// <returns>The complete mission odds, or null when the request cannot create a mission.</returns>
        public MissionOdds GetMissionOdds(
            MissionContext context,
            IReadOnlyList<ISceneNode> observedDetectors = null
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
                true
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
        /// <returns>The complete mission odds, or null when the request cannot create a mission.</returns>
        internal MissionOdds GetMissionOdds(
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

            return GetMissionOddsCore(context, observedDetectorSource, true);
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
            bool reusableDecoys = false
        )
        {
            if (!TryCreateMission(context, out Mission mission))
                return null;

            double objectiveSuccessProbability = mission.GetObjectiveSuccessProbability(
                mission.GetMainParticipants(),
                _game,
                context.Location as Planet,
                context.SelectedTarget
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

            Officer[] mainOfficers = mission.AppliesFoiledParticipantConsequences
                ? mainParticipants.OfType<Officer>().ToArray()
                : Array.Empty<Officer>();
            if (
                mainOfficers.Length == 1
                && (BigInteger.One << encounterDecoyIndexes.Count) <= detectors.Count + 1
            )
            {
                survivingByDecoyPool = AccumulateSingleOfficerEncounterOdds(
                    mission,
                    planet,
                    mainParticipants,
                    mainOfficers[0],
                    allDecoys,
                    encounterDecoyIndexes,
                    detectors,
                    survivingByDecoyPool,
                    ref foilProbability,
                    ref personnelLossProbability
                );
                return;
            }

            Dictionary<
                BigInteger,
                (double Probability, double NoOfficerLossProbability)
            > nextSurvivors = new();
            foreach (
                (
                    BigInteger availableDecoys,
                    (double survivingProbability, double noOfficerLossProbability)
                ) in survivingByDecoyPool
            )
            {
                ApplyEncounterStateOdds(
                    mission,
                    planet,
                    mainParticipants,
                    allDecoys,
                    encounterDecoyIndexes,
                    detectors,
                    availableDecoys,
                    survivingProbability,
                    noOfficerLossProbability,
                    nextSurvivors,
                    ref foilProbability,
                    ref personnelLossProbability
                );
            }

            survivingByDecoyPool = nextSurvivors;
        }

        /// <summary>
        /// Accumulates one primary officer's encounter risk by active-defender count and weighted
        /// survival sum, without retaining combinations of defender identities.
        /// </summary>
        /// <param name="mission">The mission being evaluated.</param>
        /// <param name="planet">The encounter location.</param>
        /// <param name="mainParticipants">The main participants present at this encounter.</param>
        /// <param name="officer">The sole primary officer exposed to foiling consequences.</param>
        /// <param name="allDecoys">The mission's complete decoy roster.</param>
        /// <param name="encounterDecoyIndexes">The decoys present at this encounter.</param>
        /// <param name="detectors">The observed defenders in encounter order.</param>
        /// <param name="survivingByDecoyPool">The incoming probabilities by remaining decoys.</param>
        /// <param name="foilProbability">The accumulated probability of being foiled.</param>
        /// <param name="personnelLossProbability">The accumulated officer-loss probability.</param>
        /// <returns>The surviving probabilities by remaining decoys for the next checkpoint.</returns>
        private Dictionary<
            BigInteger,
            (double Probability, double NoOfficerLossProbability)
        > AccumulateSingleOfficerEncounterOdds(
            Mission mission,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            Officer officer,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> encounterDecoyIndexes,
            IReadOnlyList<ISceneNode> detectors,
            Dictionary<
                BigInteger,
                (double Probability, double NoOfficerLossProbability)
            > survivingByDecoyPool,
            ref double foilProbability,
            ref double personnelLossProbability
        )
        {
            var (pools, remainingPools, selectableCounts) = GetDecoyPoolTransitions(
                survivingByDecoyPool.Keys,
                allDecoys.Count,
                encounterDecoyIndexes
            );
            int stride = detectors.Count + 1;
            (
                double Total,
                double NoLoss,
                double Unfoiled,
                double UnfoiledNoLoss,
                double SurvivalSum,
                double UnfoiledSurvivalSum
            )[] states = new (double, double, double, double, double, double)[pools.Count * stride];
            (
                double Total,
                double NoLoss,
                double Unfoiled,
                double UnfoiledNoLoss,
                double SurvivalSum,
                double UnfoiledSurvivalSum
            )[] next = new (double, double, double, double, double, double)[states.Length];
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
                if (survivingByDecoyPool.TryGetValue(pools[poolIndex], out var probability))
                    states[poolIndex * stride] = (
                        probability.Probability,
                        probability.NoOfficerLossProbability,
                        probability.Probability,
                        probability.NoOfficerLossProbability,
                        0,
                        0
                    );
            int foilChanceModifier = GetFoilChanceModifier(mission);
            int participantScore = GetFoilParticipantScore(mainParticipants);
            double[] diversions = new double[allDecoys.Count];
            double[] decoyLosses = new double[allDecoys.Count];
            for (int detectorIndex = 0; detectorIndex < detectors.Count; detectorIndex++)
            {
                ISceneNode detector = detectors[detectorIndex];
                Planet defenderPlanet = detector.GetParentOfType<Planet>() ?? planet;
                double unfoiled =
                    1d
                    - Math.Clamp(
                        GetFoilProbabilityForScore(
                            detector,
                            foilChanceModifier,
                            participantScore,
                            defenderPlanet
                        ) / 100d,
                        0,
                        1
                    );
                double survival =
                    1d - GetOfficerEncounterLossProbability(mission, officer, detector);
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
                Array.Clear(next, 0, next.Length);
                for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
                for (int count = 0; count <= detectorIndex; count++)
                {
                    int position = poolIndex * stride + count;
                    var probability = states[position];
                    if (probability.Total <= 0)
                        continue;
                    int selectable = selectableCounts[poolIndex];
                    if (selectable == 0)
                    {
                        AddSingleOfficerEncounterProbability(
                            ref next[position + 1],
                            probability.Total,
                            probability.NoLoss,
                            probability.Unfoiled * unfoiled,
                            probability.UnfoiledNoLoss * unfoiled,
                            probability.SurvivalSum + probability.NoLoss * survival,
                            (
                                probability.UnfoiledSurvivalSum
                                + probability.UnfoiledNoLoss * survival
                            ) * unfoiled
                        );
                        continue;
                    }
                    foreach (int index in encounterDecoyIndexes)
                    {
                        int remainingPool = remainingPools[poolIndex, index];
                        if (remainingPool == poolIndex)
                            continue;
                        double diverted = diversions[index] / selectable;
                        double confronted = (1d - diversions[index]) / selectable;
                        double survived = 1d - decoyLosses[index];
                        AddSingleOfficerEncounterProbability(
                            ref next[position],
                            probability.Total * diverted,
                            probability.NoLoss * diverted,
                            probability.Unfoiled * diverted,
                            probability.UnfoiledNoLoss * diverted,
                            probability.SurvivalSum * diverted,
                            probability.UnfoiledSurvivalSum * diverted
                        );
                        personnelLossProbability +=
                            probability.NoLoss * confronted * decoyLosses[index];
                        AddSingleOfficerEncounterProbability(
                            ref next[remainingPool * stride + count + 1],
                            probability.Total * confronted,
                            probability.NoLoss * confronted * survived,
                            probability.Unfoiled * confronted * unfoiled,
                            probability.UnfoiledNoLoss * confronted * survived * unfoiled,
                            (probability.SurvivalSum + probability.NoLoss * survival)
                                * confronted
                                * survived,
                            (
                                probability.UnfoiledSurvivalSum
                                + probability.UnfoiledNoLoss * survival
                            )
                                * confronted
                                * survived
                                * unfoiled
                        );
                    }
                }
                (states, next) = (next, states);
            }
            Dictionary<
                BigInteger,
                (double Probability, double NoOfficerLossProbability)
            > nextSurvivors = new();
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
            for (int count = 0; count <= detectors.Count; count++)
            {
                var probability = states[poolIndex * stride + count];
                if (probability.Total <= 0)
                    continue;
                foilProbability += Math.Max(0, probability.Total - probability.Unfoiled);
                if (count > 0)
                    personnelLossProbability += Math.Max(
                        0,
                        probability.NoLoss
                            - probability.UnfoiledNoLoss
                            - (probability.SurvivalSum - probability.UnfoiledSurvivalSum) / count
                    );
                AddEncounterProbability(
                    nextSurvivors,
                    pools[poolIndex],
                    probability.Unfoiled,
                    probability.UnfoiledNoLoss
                );
            }
            return nextSurvivors;
        }

        /// <summary>
        /// Merges probability weights and survival sums for equivalent single-officer states.
        /// </summary>
        /// <param name="state">The accumulated probabilities for one decoy pool and defender count.</param>
        /// <param name="total">The total probability.</param>
        /// <param name="noLoss">The probability with no officer losses.</param>
        /// <param name="unfoiled">The probability with no foiling.</param>
        /// <param name="unfoiledNoLoss">The probability with neither foiling nor officer losses.</param>
        /// <param name="survivalSum">The no-loss-weighted sum of primary-officer survival chances.</param>
        /// <param name="unfoiledSurvivalSum">The corresponding sum on unfoiled paths.</param>
        private static void AddSingleOfficerEncounterProbability(
            ref (
                double Total,
                double NoLoss,
                double Unfoiled,
                double UnfoiledNoLoss,
                double SurvivalSum,
                double UnfoiledSurvivalSum
            ) state,
            double total,
            double noLoss,
            double unfoiled,
            double unfoiledNoLoss,
            double survivalSum,
            double unfoiledSurvivalSum
        )
        {
            state.Total += total;
            state.NoLoss += noLoss;
            state.Unfoiled += unfoiled;
            state.UnfoiledNoLoss += unfoiledNoLoss;
            state.SurvivalSum += survivalSum;
            state.UnfoiledSurvivalSum += unfoiledSurvivalSum;
        }

        /// <summary>
        /// Resolves one available-decoy state through a complete encounter checkpoint.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="mainParticipants">The primary participants present.</param>
        /// <param name="allDecoys">Every decoy assigned to the mission.</param>
        /// <param name="encounterDecoyIndexes">Indexes of decoys present at this encounter.</param>
        /// <param name="detectors">The detectors active at the checkpoint.</param>
        /// <param name="availableDecoys">The available-decoy bit set.</param>
        /// <param name="stateProbability">The probability of reaching this state.</param>
        /// <param name="noOfficerLossProbability">The probability of reaching this state without losing an officer.</param>
        /// <param name="nextSurvivors">The next checkpoint's surviving states.</param>
        /// <param name="foilProbability">Accumulated foil probability.</param>
        /// <param name="personnelLossProbability">Accumulated personnel-loss probability.</param>
        private void ApplyEncounterStateOdds(
            Mission mission,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> encounterDecoyIndexes,
            IReadOnlyList<ISceneNode> detectors,
            BigInteger availableDecoys,
            double stateProbability,
            double noOfficerLossProbability,
            Dictionary<
                BigInteger,
                (double Probability, double NoOfficerLossProbability)
            > nextSurvivors,
            ref double foilProbability,
            ref double personnelLossProbability
        )
        {
            Officer[] mainOfficers = mission.AppliesFoiledParticipantConsequences
                ? mainParticipants.OfType<Officer>().ToArray()
                : Array.Empty<Officer>();
            List<double[]> survivalGroups = new();
            int[] detectorGroups = new int[detectors.Count];
            for (int index = 0; index < detectors.Count; index++)
            {
                double[] survival = mainOfficers
                    .Select(officer =>
                        1d - GetOfficerEncounterLossProbability(mission, officer, detectors[index])
                    )
                    .ToArray();
                int group = survivalGroups.FindIndex(existing => existing.SequenceEqual(survival));
                if (group < 0)
                {
                    group = survivalGroups.Count;
                    survivalGroups.Add(survival);
                }
                detectorGroups[index] = group;
            }

            bool uniform = survivalGroups.Count == 1;
            BigInteger radix = detectors.Count + 1;
            BigInteger[] groupWeights = Enumerable
                .Range(0, survivalGroups.Count)
                .Select(index => uniform ? BigInteger.Zero : BigInteger.Pow(radix, index))
                .ToArray();
            Dictionary<
                (BigInteger Decoys, BigInteger ActiveGroups),
                (double Total, double NoLoss, double Unfoiled, double UnfoiledNoLoss)
            > states = new()
            {
                {
                    (availableDecoys, BigInteger.Zero),
                    (
                        stateProbability,
                        noOfficerLossProbability,
                        stateProbability,
                        noOfficerLossProbability
                    )
                },
            };
            int foilChanceModifier = GetFoilChanceModifier(mission);
            int participantScore = GetFoilParticipantScore(mainParticipants);
            double[] diversions = new double[allDecoys.Count];
            double[] decoyLosses = new double[allDecoys.Count];
            for (int detectorIndex = 0; detectorIndex < detectors.Count; detectorIndex++)
            {
                ISceneNode detector = detectors[detectorIndex];
                double unfoiled =
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
                foreach (int index in encounterDecoyIndexes)
                {
                    diversions[index] = Math.Clamp(
                        GetDecoyProbability(
                            mission,
                            allDecoys[index],
                            detector,
                            detector.GetParentOfType<Planet>() ?? planet
                        ) / 100d,
                        0,
                        1
                    );
                    decoyLosses[index] = allDecoys[index] is Officer officer
                        ? GetOfficerEncounterLossProbability(mission, officer, detector)
                        : 0;
                }
                BigInteger activeGroupIncrement = groupWeights[detectorGroups[detectorIndex]];
                Dictionary<
                    (BigInteger Decoys, BigInteger ActiveGroups),
                    (double Total, double NoLoss, double Unfoiled, double UnfoiledNoLoss)
                > next = new();
                foreach (var ((pool, activeGroups), probability) in states)
                {
                    int selectable = encounterDecoyIndexes.Count(index => IsBitSet(pool, index));
                    if (selectable == 0)
                    {
                        AddEncounterStateProbability(
                            next,
                            pool,
                            activeGroups + activeGroupIncrement,
                            probability.Total,
                            probability.NoLoss,
                            probability.Unfoiled * unfoiled,
                            probability.UnfoiledNoLoss * unfoiled
                        );
                        continue;
                    }
                    foreach (int index in encounterDecoyIndexes)
                    {
                        if (!IsBitSet(pool, index))
                            continue;
                        double diverted = diversions[index] / selectable;
                        double confronted = (1d - diversions[index]) / selectable;
                        double survived = 1d - decoyLosses[index];
                        AddEncounterStateProbability(
                            next,
                            pool,
                            activeGroups,
                            probability.Total * diverted,
                            probability.NoLoss * diverted,
                            probability.Unfoiled * diverted,
                            probability.UnfoiledNoLoss * diverted
                        );
                        personnelLossProbability +=
                            probability.NoLoss * confronted * decoyLosses[index];
                        AddEncounterStateProbability(
                            next,
                            ClearBit(pool, index),
                            activeGroups + activeGroupIncrement,
                            probability.Total * confronted,
                            probability.NoLoss * confronted * survived,
                            probability.Unfoiled * confronted * unfoiled,
                            probability.UnfoiledNoLoss * confronted * survived * unfoiled
                        );
                    }
                }
                states = next;
            }
            foreach (var ((pool, activeGroups), probability) in states)
            {
                double mainSurvival = GetGroupedOfficerSurvival(
                    survivalGroups,
                    groupWeights,
                    radix,
                    activeGroups
                );
                foilProbability += Math.Max(0, probability.Total - probability.Unfoiled);
                personnelLossProbability +=
                    Math.Max(0, probability.NoLoss - probability.UnfoiledNoLoss)
                    * (1d - mainSurvival);
                AddEncounterProbability(
                    nextSurvivors,
                    pool,
                    probability.Unfoiled,
                    probability.UnfoiledNoLoss
                );
            }
        }

        /// <summary>
        /// Calculates primary-officer survival from the counts of equivalent active defenders.
        /// Counts use digits in a radix larger than the total number of defenders, so adding a
        /// defender cannot carry into another group's count. A single group needs no count.
        /// </summary>
        /// <param name="groups">Per-officer survival chances for each defender group.</param>
        /// <param name="weights">The place value of each group's count.</param>
        /// <param name="radix">One greater than the encounter's total defender count.</param>
        /// <param name="activeGroups">The encoded active-defender counts.</param>
        /// <returns>The chance that every primary officer survives free after foiling.</returns>
        private static double GetGroupedOfficerSurvival(
            IReadOnlyList<double[]> groups,
            IReadOnlyList<BigInteger> weights,
            BigInteger radix,
            BigInteger activeGroups
        )
        {
            if (groups.Count == 1)
                return groups[0].Aggregate(1d, (probability, survival) => probability * survival);
            double[] survivalSums = new double[groups[0].Length];
            int count = 0;
            for (int group = 0; group < groups.Count; group++)
            {
                int activeCount = (int)((activeGroups / weights[group]) % radix);
                count += activeCount;
                for (int officer = 0; officer < survivalSums.Length; officer++)
                    survivalSums[officer] += activeCount * groups[group][officer];
            }
            return count == 0
                ? 1d
                : survivalSums.Aggregate(1d, (probability, sum) => probability * (sum / count));
        }

        /// <summary>Merges total and unfoiled probabilities for equivalent encounter histories.</summary>
        /// <param name="states">The accumulated encounter states.</param>
        /// <param name="pool">The decoys still available.</param>
        /// <param name="activeGroups">The encoded active-defender group counts.</param>
        /// <param name="total">The total probability being merged.</param>
        /// <param name="noLoss">Its subset with no prior officer losses.</param>
        /// <param name="unfoiled">Its subset with no foiling detection.</param>
        /// <param name="unfoiledNoLoss">Its subset with neither foiling nor officer losses.</param>
        private static void AddEncounterStateProbability(
            Dictionary<
                (BigInteger Decoys, BigInteger ActiveGroups),
                (double Total, double NoLoss, double Unfoiled, double UnfoiledNoLoss)
            > states,
            BigInteger pool,
            BigInteger activeGroups,
            double total,
            double noLoss,
            double unfoiled,
            double unfoiledNoLoss
        )
        {
            if (total <= 0)
                return;
            var key = (pool, activeGroups);
            states.TryGetValue(key, out var existing);
            states[key] = (
                existing.Total + total,
                existing.NoLoss + noLoss,
                existing.Unfoiled + unfoiled,
                existing.UnfoiledNoLoss + unfoiledNoLoss
            );
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
        /// Merges encounter probability and its subset with no officer losses into a state.
        /// </summary>
        /// <param name="probabilities">The encounter distribution to update.</param>
        /// <param name="state">The state key.</param>
        /// <param name="probability">The probability reaching this state.</param>
        /// <param name="noOfficerLossProbability">The probability reaching it with every officer surviving free.</param>
        /// <typeparam name="T">The encounter-state key type.</typeparam>
        private static void AddEncounterProbability<T>(
            Dictionary<T, (double Probability, double NoOfficerLossProbability)> probabilities,
            T state,
            double probability,
            double noOfficerLossProbability
        )
        {
            if (probability <= 0)
                return;

            probabilities.TryGetValue(
                state,
                out (double Probability, double NoOfficerLossProbability) existing
            );
            probabilities[state] = (
                existing.Probability + probability,
                existing.NoOfficerLossProbability + noOfficerLossProbability
            );
        }

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
        /// Returns a bit set with the specified bit enabled.
        /// </summary>
        /// <param name="value">The bit set.</param>
        /// <param name="index">The zero-based bit index.</param>
        /// <returns>The updated bit set.</returns>
        private static BigInteger SetBit(BigInteger value, int index) =>
            value | (BigInteger.One << index);

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

            Officer commander = _detectorCommander(
                detector,
                mission.GetParent() as Planet ?? detector?.GetParentOfType<Planet>()
            );
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
            Officer commander = _detectorCommander(
                detector,
                encounterPlanet
                    ?? mission.GetParent() as Planet
                    ?? detector?.GetParentOfType<Planet>()
            );
            int score =
                decoy.GetEffectiveRating(SkillRating.Espionage)
                - GetEffectiveDetectionRating(detector)
                - GetScaledCommanderEspionage(commander, missionTables.DecoyDefenderScalingPercent);
            Dictionary<int, int> table =
                detector.GetParentOfType<Fleet>() != null
                    ? missionTables.FleetDecoy
                    : missionTables.PlanetaryDecoy;
            return LookupProbability(table, score);
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
            int score = participantScore - GetMissionDetectorStrength(detector, encounterPlanet);
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
            Officer commander = _detectorCommander(
                detector,
                planet ?? detector?.GetParentOfType<Planet>()
            );
            return GetEffectiveDetectionRating(detector)
                + GetScaledCommanderEspionage(
                    commander,
                    GetMissionTables().FoilDefenderScalingPercent
                );
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
            return (int)Math.Round(authoredRating * multiplier, MidpointRounding.AwayFromZero);
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
        private static int GetScaledCommanderEspionage(Officer commander, int scalingPercent)
        {
            return (commander?.GetEffectiveRating(SkillRating.Espionage) ?? 0)
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
        private static int LookupProbability(
            Dictionary<int, int> entries,
            int score,
            int defaultValue = 0
        )
        {
            if (entries == null || entries.Count == 0)
                return defaultValue;

            return new ProbabilityTable(entries).Lookup(score);
        }
    }
}
