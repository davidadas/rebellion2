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
    /// <summary>Evaluates mission eligibility and odds without starting or advancing missions.</summary>
    public sealed class MissionQueries
    {
        private readonly GameRoot _game;
        private readonly MissionFactory _missionFactory;

        /// <summary>Creates mission queries for the supplied game state.</summary>
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
                    : (mission, planet, phase, mainParticipants) =>
                        planet?.InstanceID == target?.InstanceID
                            ? FilterEncounterDetectors(
                                mission,
                                planet,
                                phase,
                                mainParticipants,
                                observedDetectors
                            )
                            : Array.Empty<ISceneNode>()
            );
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
                IReadOnlyList<IMissionParticipant>,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource
        )
        {
            if (observedDetectorSource == null)
                throw new ArgumentNullException(nameof(observedDetectorSource));

            return GetMissionOddsCore(context, observedDetectorSource);
        }

        /// <summary>
        /// Calculates objective and encounter odds without mutating the mission or its participants.
        /// </summary>
        /// <param name="context">The mission configuration to evaluate.</param>
        /// <param name="observedDetectorSource">
        /// Optional source of detector candidates from the caller's observed state.
        /// </param>
        /// <returns>The complete mission odds, or null when the request cannot create a mission.</returns>
        private MissionOdds GetMissionOddsCore(
            MissionContext context,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                IReadOnlyList<IMissionParticipant>,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource
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
            (double foilProbability, double personnelLossProbability) = EstimateEncounterOdds(
                mission,
                context.Location as Planet,
                observedDetectorSource
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
        /// <param name="observedDetectorSource">
        /// Optional source of detector candidates from the caller's observed state.
        /// </param>
        /// <returns>The cumulative foil and personnel-loss percentages.</returns>
        private (double FoilProbability, double PersonnelLossProbability) EstimateEncounterOdds(
            Mission mission,
            Planet target,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                IReadOnlyList<IMissionParticipant>,
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
            Dictionary<BigInteger, double> survivingByDecoyPool = new Dictionary<BigInteger, double>
            {
                { availableDecoys, 1d },
            };
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
            MissionEncounterPhase phase,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<IMissionParticipant> allDecoys,
            IReadOnlyList<int> encounterDecoyIndexes,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                IReadOnlyList<IMissionParticipant>,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource,
            ref Dictionary<BigInteger, double> survivingByDecoyPool,
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
                mainParticipants,
                observedDetectorSource
            );
            if (detectors.Count == 0)
                return;

            Dictionary<BigInteger, double> nextSurvivors = new Dictionary<BigInteger, double>();
            foreach (
                (BigInteger availableDecoys, double survivingProbability) in survivingByDecoyPool
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
                    nextSurvivors,
                    ref foilProbability,
                    ref personnelLossProbability
                );
            }

            survivingByDecoyPool = nextSurvivors;
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
            Dictionary<BigInteger, double> nextSurvivors,
            ref double foilProbability,
            ref double personnelLossProbability
        )
        {
            Dictionary<(BigInteger AvailableDecoys, BigInteger ActiveDetectors), double> states =
                new Dictionary<(BigInteger, BigInteger), double>
                {
                    { (availableDecoys, BigInteger.Zero), stateProbability },
                };
            for (int detectorIndex = 0; detectorIndex < detectors.Count; detectorIndex++)
            {
                Dictionary<(BigInteger, BigInteger), double> nextStates =
                    new Dictionary<(BigInteger, BigInteger), double>();
                foreach (
                    (
                        (BigInteger decoyPool, BigInteger activeDetectors),
                        double probability
                    ) in states
                )
                {
                    List<int> selectableDecoys = encounterDecoyIndexes
                        .Where(index => IsBitSet(decoyPool, index))
                        .ToList();
                    if (selectableDecoys.Count == 0)
                    {
                        AddProbability(
                            nextStates,
                            (decoyPool, SetBit(activeDetectors, detectorIndex)),
                            probability
                        );
                        continue;
                    }

                    foreach (int decoyIndex in selectableDecoys)
                    {
                        double selectedProbability = probability / selectableDecoys.Count;
                        double diversionProbability = Math.Clamp(
                            mission.GetDecoyProbability(
                                allDecoys[decoyIndex],
                                detectors[detectorIndex],
                                _game,
                                planet
                            ) / 100d,
                            0,
                            1
                        );
                        AddProbability(
                            nextStates,
                            (decoyPool, activeDetectors),
                            selectedProbability * diversionProbability
                        );
                        AddProbability(
                            nextStates,
                            (
                                ClearBit(decoyPool, decoyIndex),
                                SetBit(activeDetectors, detectorIndex)
                            ),
                            selectedProbability * (1d - diversionProbability)
                        );
                    }
                }

                states = nextStates;
            }

            int foilChanceModifier = GetFoilChanceModifier(mission);
            foreach (
                ((BigInteger decoyPool, BigInteger activeDetectors), double probability) in states
            )
            {
                List<ISceneNode> remainingDetectors = Enumerable
                    .Range(0, detectors.Count)
                    .Where(index => IsBitSet(activeDetectors, index))
                    .Select(index => detectors[index])
                    .ToList();
                double noFoilProbability = remainingDetectors.Aggregate(
                    1d,
                    (current, detector) =>
                        current
                        * (
                            1d
                            - Math.Clamp(
                                GetFoilProbability(
                                    mission,
                                    detector,
                                    foilChanceModifier,
                                    mainParticipants,
                                    planet
                                ) / 100d,
                                0,
                                1
                            )
                        )
                );
                double foiledProbability = probability * (1d - noFoilProbability);
                foilProbability += foiledProbability;
                personnelLossProbability +=
                    foiledProbability
                    * GetPersonnelLossProbability(mission, mainParticipants, remainingDetectors);
                AddProbability(nextSurvivors, decoyPool, probability * noFoilProbability);
            }
        }

        /// <summary>
        /// Returns the chance that a foil removes at least one primary officer.
        /// </summary>
        /// <param name="mission">The mission whose participants are exposed.</param>
        /// <param name="mainParticipants">The primary participants present.</param>
        /// <param name="detectors">The detectors remaining after diversions.</param>
        /// <returns>The conditional personnel-loss probability from zero to one.</returns>
        private double GetPersonnelLossProbability(
            Mission mission,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<ISceneNode> detectors
        )
        {
            if (!mission.AppliesFoiledParticipantConsequences || detectors.Count == 0)
                return 0;

            double noOfficerLossProbability = 1d;
            foreach (Officer officer in mainParticipants.OfType<Officer>())
            {
                noOfficerLossProbability *= detectors.Average(detector =>
                    GetParticipantEvasionProbability(mission, officer, detector)
                );
            }

            return 1d - noOfficerLossProbability;
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
        /// <param name="mainParticipants">The primary participants present.</param>
        /// <param name="observedDetectorSource">Optional observed detector source.</param>
        /// <returns>The eligible detector units in traversal order.</returns>
        private IReadOnlyList<ISceneNode> GetEncounterDetectors(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            Func<
                Mission,
                Planet,
                MissionEncounterPhase,
                IReadOnlyList<IMissionParticipant>,
                IReadOnlyList<ISceneNode>
            > observedDetectorSource
        )
        {
            if (observedDetectorSource != null)
                return observedDetectorSource(mission, planet, phase, mainParticipants);

            IReadOnlyList<ISceneNode> candidates = GetDetectorCandidates(planet);
            return FilterEncounterDetectors(mission, planet, phase, mainParticipants, candidates);
        }

        /// <summary>
        /// Filters observed detector candidates for one encounter checkpoint.
        /// </summary>
        /// <param name="mission">The mission being estimated.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="phase">The encounter checkpoint.</param>
        /// <param name="mainParticipants">The primary participants present.</param>
        /// <param name="candidates">The observed detector candidates.</param>
        /// <returns>The eligible detector units in traversal order.</returns>
        private static IReadOnlyList<ISceneNode> FilterEncounterDetectors(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase,
            IReadOnlyList<IMissionParticipant> mainParticipants,
            IReadOnlyList<ISceneNode> candidates
        )
        {
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

            bool includePlanetaryDetectors =
                phase == MissionEncounterPhase.PreObjective
                || mainParticipants.Any(participant =>
                    participant.GetParentOfType<Planet>()?.InstanceID != planet.InstanceID
                );
            return candidates
                .Where(candidate =>
                    mission.IsEligibleDetector(candidate)
                    && (includePlanetaryDetectors || candidate.GetParentOfType<Fleet>() != null)
                )
                .ToList();
        }

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
            candidates.AddRange(planet.GetChildren<Starfighter>());
            candidates.AddRange(planet.GetChildren<Regiment>());
            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
                {
                    candidates.AddRange(capitalShip.GetChildren<Starfighter>());
                    candidates.AddRange(capitalShip.GetChildren<Regiment>());
                }
            }

            return candidates;
        }

        /// <summary>Adds probability to an existing or new state outcome.</summary>
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

        /// <summary>Returns whether the specified bit is set.</summary>
        /// <param name="value">The bit set.</param>
        /// <param name="index">The zero-based bit index.</param>
        /// <returns>True when the bit is set.</returns>
        private static bool IsBitSet(BigInteger value, int index) =>
            (value & (BigInteger.One << index)) != BigInteger.Zero;

        /// <summary>Returns a bit set with the specified bit enabled.</summary>
        /// <param name="value">The bit set.</param>
        /// <param name="index">The zero-based bit index.</param>
        /// <returns>The updated bit set.</returns>
        private static BigInteger SetBit(BigInteger value, int index) =>
            value | (BigInteger.One << index);

        /// <summary>Returns a bit set with the specified bit disabled.</summary>
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

            Officer commander = mission.FindDetectorCommander(detector);
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

            int score = CalculateFoilScore(
                mission,
                detector,
                participants ?? mission.GetMainParticipants(),
                encounterPlanet
            );
            int probability = LookupProbability(GetMissionTables().Foil, score);
            return Math.Clamp(probability + foilChanceModifier, 0, 100);
        }

        /// <summary>
        /// Calculates one detector's score against a mission team.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <param name="participants">The primary team present at this encounter.</param>
        /// <param name="encounterPlanet">The planet where the encounter occurs.</param>
        /// <returns>The score used to look up the foiling probability.</returns>
        private int CalculateFoilScore(
            Mission mission,
            ISceneNode detector,
            IReadOnlyList<IMissionParticipant> participants,
            Planet encounterPlanet
        )
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            Officer commander = mission.FindDetectorCommander(detector, encounterPlanet);
            return GetAverageEspionage(participants)
                - GetScaledCommanderEspionage(commander, missionTables.FoilDefenderScalingPercent)
                - GetDetectorRating(detector)
                - participants.OfType<SpecialForces>().Count()
                - missionTables.FoilFlatScoreAdjustment;
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
        internal static List<ISceneNode> GetDetectors(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase
        )
        {
            List<ISceneNode> detectors = new List<ISceneNode>();
            if (mission == null || planet == null || phase == MissionEncounterPhase.DepartureStart)
                return detectors;

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

            if (phase == MissionEncounterPhase.PreObjective || mission.HasRemoteOrigin(planet))
            {
                AddEligibleDetectors(mission, planet.GetChildren<Starfighter>(), detectors);
                AddEligibleDetectors(mission, planet.GetChildren<Regiment>(), detectors);
            }

            foreach (Fleet fleet in hostileFleets)
            {
                foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
                {
                    AddEligibleDetectors(
                        mission,
                        capitalShip.GetChildren<Starfighter>(),
                        detectors
                    );
                    AddEligibleDetectors(mission, capitalShip.GetChildren<Regiment>(), detectors);
                }
            }

            return detectors;
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
        /// Returns the authored detection rating for a detector unit.
        /// </summary>
        /// <param name="detector">The detector unit.</param>
        /// <returns>The detector's authored rating.</returns>
        private static int GetDetectorRating(ISceneNode detector) =>
            detector switch
            {
                Regiment regiment => regiment.DetectionRating,
                Starfighter starfighter => starfighter.DetectionRating,
                CapitalShip capitalShip => capitalShip.DetectionRating,
                _ => 0,
            };

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
