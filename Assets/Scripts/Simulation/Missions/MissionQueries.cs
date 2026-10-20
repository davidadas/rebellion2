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

        /// <summary>
        /// Creates mission queries for the supplied game state.
        /// </summary>
        /// <param name="game">The game used to resolve participants and mission rules.</param>
        public MissionQueries(GameRoot game)
        {
            _game = game;
            _missionFactory = new MissionFactory(game);
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
        /// <returns>The percentage chance that the next execution reports objective success if reached.</returns>
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
        /// Estimates objective success and encounter risks from the caller's observed state.
        /// Success means the next execution reports a successful objective. Hidden betrayal and
        /// changes from travel, hidden missions, or later ticks are excluded.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectors">
        /// Optional detector snapshot already filtered for the mission owner.
        /// </param>
        /// <param name="includePersonnelLoss">Whether to calculate the additional personnel-loss risk.</param>
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
        /// Estimates mission odds from detector snapshots supplied for every encounter planet.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectorSource">
        /// Supplies detector candidates from the caller's observed state for a planet.
        /// </param>
        /// <param name="includePersonnelLoss">Whether to calculate the additional personnel-loss risk.</param>
        /// <param name="evaluationState">The optional turn-scoped observed-planet assessment.</param>
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
                evaluationState
            );
        }

        /// <summary>
        /// Calculates objective and encounter odds without mutating the mission or its participants.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectorSource">
        /// Optional source of detector candidates from the caller's observed state.
        /// </param>
        /// <param name="includePersonnelLoss">
        /// Whether to calculate the additional personnel-loss odds.
        /// </param>
        /// <param name="evaluationState">The optional turn-scoped observed-planet assessment.</param>
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
            Mission.MissionEvaluationState evaluationState = null
        )
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
            (double foilProbability, double personnelLossProbability) = EstimateEncounterOdds(
                mission,
                context.Location as Planet,
                context.SelectedTarget ?? context.Location,
                observedDetectorSource,
                includePersonnelLoss
            );
            return new MissionOdds(
                objectiveSuccessProbability,
                foilProbability,
                personnelLossProbability
            );
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
        /// <param name="includePersonnelLoss">Whether to calculate primary-officer loss risk.</param>
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
            > observedDetectorSource,
            bool includePersonnelLoss
        )
        {
            if (mission == null || target == null)
                return (0, 0);

            IReadOnlyList<IMissionParticipant> allDecoys = mission.GetDecoyParticipants();
            BigInteger availableDecoys =
                allDecoys.Count == 0
                    ? BigInteger.Zero
                    : (BigInteger.One << allDecoys.Count) - BigInteger.One;
            Dictionary<BigInteger, double> survivingByDecoyPool = new Dictionary<BigInteger, double>
            {
                { availableDecoys, 1d },
            };
            double foilProbability = 0;
            double personnelLossProbability = 0;
            DetectorCommanderCache commanderCache = new DetectorCommanderCache(mission);

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
                    includePersonnelLoss,
                    commanderCache,
                    survivingByDecoyPool,
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
                    includePersonnelLoss,
                    commanderCache,
                    survivingByDecoyPool,
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
                includePersonnelLoss,
                commanderCache,
                survivingByDecoyPool,
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
                includePersonnelLoss,
                commanderCache,
                survivingByDecoyPool,
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
        /// <param name="includePersonnelLoss">Whether to calculate primary-officer loss risk.</param>
        /// <param name="commanderCache">Commander resolutions shared across checkpoints.</param>
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
            bool includePersonnelLoss,
            DetectorCommanderCache commanderCache,
            Dictionary<BigInteger, double> survivingByDecoyPool,
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

            IReadOnlyList<MissionEncounterOdds.DefenderProbabilities> probabilities =
                GetEncounterProbabilities(
                    mission,
                    planet,
                    mainParticipants,
                    allDecoys,
                    encounterDecoyIndexes,
                    detectors,
                    includePersonnelLoss,
                    commanderCache
                );
            MissionEncounterOdds.Result result = MissionEncounterOdds.Apply(
                probabilities,
                encounterDecoyIndexes,
                survivingByDecoyPool
            );
            survivingByDecoyPool.Clear();
            foreach ((BigInteger pool, double probability) in result.Survivors)
                survivingByDecoyPool.Add(pool, probability);
            foilProbability += result.FoilProbability;
            personnelLossProbability += result.PersonnelLossProbability;
        }

        /// <summary>
        /// Resolves each defender's fixed probabilities once before combining outcome states.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="mainParticipants">Primary participants present at this checkpoint.</param>
        /// <param name="allDecoys">All mission decoys.</param>
        /// <param name="decoyIndexes">Decoys present at this checkpoint.</param>
        /// <param name="detectors">Ordered observed defenders.</param>
        /// <param name="includePersonnelLoss">Whether to include officer evasion statistics.</param>
        /// <param name="commanderCache">Commander resolutions shared across checkpoints.</param>
        /// <returns>Immutable numerical inputs for exact probability aggregation.</returns>
        private IReadOnlyList<MissionEncounterOdds.DefenderProbabilities> GetEncounterProbabilities(
            Mission mission,
            Planet planet,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> decoyIndexes,
            IReadOnlyList<ISceneNode> detectors,
            bool includePersonnelLoss,
            DetectorCommanderCache commanderCache
        )
        {
            List<Officer> officers =
                includePersonnelLoss && mission.AppliesFoiledParticipantConsequences
                    ? mainParticipants.OfType<Officer>().ToList()
                    : new List<Officer>();
            int foilModifier = GetFoilChanceModifier(mission);
            List<MissionEncounterOdds.DefenderProbabilities> probabilities =
                new List<MissionEncounterOdds.DefenderProbabilities>(detectors.Count);
            foreach (ISceneNode detector in detectors)
            {
                Officer commander = commanderCache.Get(detector, planet);
                double noFoil =
                    1d
                    - Math.Clamp(
                        CalculateFoilProbability(
                            detector,
                            foilModifier,
                            mainParticipants,
                            commander
                        ) / 100d,
                        0,
                        1
                    );
                double[] diversion = new double[allDecoys.Count];
                foreach (int index in decoyIndexes)
                    diversion[index] = Math.Clamp(
                        CalculateDecoyProbability(allDecoys[index], detector, commander) / 100d,
                        0,
                        1
                    );
                int[] evasion = new int[officers.Count];
                if (officers.Count > 0)
                {
                    Officer evasionCommander = commanderCache.Get(detector);
                    int defenderCombat =
                        evasionCommander?.GetEffectiveRating(SkillRating.Combat) ?? 0;
                    for (int index = 0; index < officers.Count; index++)
                        evasion[index] = (int)
                            Math.Clamp(
                                GetEvasionProbability(
                                    officers[index].GetEffectiveRating(SkillRating.Combat)
                                        - defenderCombat
                                ),
                                0,
                                100
                            );
                }
                probabilities.Add(
                    new MissionEncounterOdds.DefenderProbabilities(noFoil, diversion, evasion)
                );
            }
            return probabilities;
        }

        /// <summary>
        /// Caches commander lookup by the scope and defender category that determine the result.
        /// </summary>
        private sealed class DetectorCommanderCache
        {
            private readonly Mission _mission;
            private readonly Dictionary<(ISceneNode, Type, string), Officer> _commanders = new();

            /// <summary>
            /// Creates a cache for one mission-odds evaluation.
            /// </summary>
            /// <param name="mission">The mission whose commander rules are applied.</param>
            internal DetectorCommanderCache(Mission mission)
            {
                _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            }

            /// <summary>
            /// Returns the commander for a detector without repeatedly traversing its container.
            /// </summary>
            /// <param name="detector">The detector whose commander is requested.</param>
            /// <param name="planet">The explicit encounter planet, or null for mission-default resolution.</param>
            /// <returns>The matching commander, or null.</returns>
            internal Officer Get(ISceneNode detector, Planet planet = null)
            {
                Planet resolvedPlanet =
                    planet ?? _mission.GetParent() as Planet ?? detector?.GetParentOfType<Planet>();
                ISceneNode scope = detector?.GetParentOfType<Fleet>();
                scope ??= resolvedPlanet;
                if (scope == null || detector == null)
                    return null;

                (ISceneNode, Type, string) key = (
                    scope,
                    detector.GetType(),
                    detector.GetOwnerInstanceID()
                );
                if (!_commanders.TryGetValue(key, out Officer commander))
                {
                    commander = _mission.FindDetectorCommander(detector, planet);
                    _commanders.Add(key, commander);
                }
                return commander;
            }
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

            return CalculateDecoyProbability(
                decoy,
                detector,
                mission.FindDetectorCommander(detector, encounterPlanet)
            );
        }

        /// <summary>
        /// Calculates diversion odds using an already resolved defender commander.
        /// </summary>
        /// <param name="decoy">The decoy to evaluate.</param>
        /// <param name="detector">The defender being distracted.</param>
        /// <param name="commander">The local defender commander, or null.</param>
        /// <returns>The configured diversion percentage.</returns>
        private int CalculateDecoyProbability(
            IMissionParticipant decoy,
            ISceneNode detector,
            Officer commander
        )
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            long score =
                (long)decoy.GetEffectiveRating(SkillRating.Espionage)
                - GetEffectiveDetectionRating(detector)
                - GetScaledCommanderEspionage(commander, missionTables.DecoyDefenderScalingPercent);
            Dictionary<int, int> table =
                detector.GetParentOfType<Fleet>() != null
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

            return CalculateFoilProbability(
                detector,
                foilChanceModifier,
                participants ?? mission.GetMainParticipants(),
                mission.FindDetectorCommander(detector, encounterPlanet)
            );
        }

        /// <summary>
        /// Calculates foil odds using an already resolved defender commander.
        /// </summary>
        /// <param name="detector">The active defender.</param>
        /// <param name="foilChanceModifier">The faction's difficulty adjustment.</param>
        /// <param name="participants">Primary participants present at the checkpoint.</param>
        /// <param name="commander">The resolved defender commander, or null.</param>
        /// <returns>The adjusted foiling percentage.</returns>
        private int CalculateFoilProbability(
            ISceneNode detector,
            int foilChanceModifier,
            IReadOnlyList<IMissionParticipant> participants,
            Officer commander
        )
        {
            int score = CalculateFoilScore(detector, participants, commander);
            int probability = LookupProbability(GetMissionTables().Foil, score);
            return Math.Clamp(probability + foilChanceModifier, 0, 100);
        }

        /// <summary>
        /// Calculates one detector's score against a mission team.
        /// </summary>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <param name="participants">The primary team present at this encounter.</param>
        /// <param name="commander">The resolved local commander, or null.</param>
        /// <returns>The score used to look up the foiling probability.</returns>
        private int CalculateFoilScore(
            ISceneNode detector,
            IReadOnlyList<IMissionParticipant> participants,
            Officer commander
        )
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            long score =
                (long)GetAverageEspionage(participants)
                - GetScaledCommanderEspionage(commander, missionTables.FoilDefenderScalingPercent)
                - GetEffectiveDetectionRating(detector)
                - participants.OfType<SpecialForces>().Count()
                - missionTables.FoilFlatScoreAdjustment;
            return IntegerMath.SaturateToInt(score);
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
