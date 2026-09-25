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
        /// intentionally excluded. Foiling uses the caller's observed planet state.
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
            if (!TryCreateMission(context, out Mission mission))
                return null;

            double objectiveSuccessProbability = mission.GetObjectiveSuccessProbability(
                mission.GetMainParticipants(),
                _game,
                context.Location as Planet,
                context.SelectedTarget
            );
            Planet observedPlanet = context.Location as Planet;
            IReadOnlyList<ISceneNode> detectors =
                observedPlanet == null
                    ? Array.Empty<ISceneNode>()
                    : observedDetectors ?? GetDetectors(mission, observedPlanet);
            double foilProbability = EstimateFoilProbability(mission, detectors);
            double personnelLossProbability = EstimatePersonnelLossProbability(
                mission,
                detectors,
                foilProbability
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
        /// Estimates the chance that at least one observed detector foils the mission.
        /// </summary>
        /// <param name="mission">The unstarted or active mission to evaluate.</param>
        /// <param name="detectors">The observed units that can confront mission participants.</param>
        /// <returns>The estimated foiling percentage.</returns>
        private double EstimateFoilProbability(Mission mission, IReadOnlyList<ISceneNode> detectors)
        {
            if (mission == null || detectors == null || detectors.Count == 0)
                return 0;

            IReadOnlyList<IMissionParticipant> decoys = mission.GetDecoyParticipants();
            if (decoys.Count == 0)
            {
                double unfoiledProbability = 1d;
                foreach (ISceneNode detector in detectors)
                {
                    unfoiledProbability *=
                        1d - Math.Clamp(GetFoilProbability(mission, detector) / 100d, 0, 1);
                }

                return (1d - unfoiledProbability) * 100d;
            }

            var decoyGroups = decoys
                .GroupBy(decoy => new
                {
                    Espionage = decoy.GetEffectiveRating(SkillRating.Espionage),
                    Combat = decoy.GetEffectiveRating(SkillRating.Combat),
                    CanBeRemoved = decoy is Officer or SpecialForces,
                })
                .Select(group => new { Decoy = group.First(), Count = group.Count() })
                .ToList();

            // Encode each group's surviving count as one digit in a mixed-radix number.
            BigInteger[] groupPlaceValues = new BigInteger[decoyGroups.Count];
            BigInteger allDecoys = BigInteger.Zero;
            BigInteger nextPlaceValue = BigInteger.One;
            for (int index = 0; index < decoyGroups.Count; index++)
            {
                groupPlaceValues[index] = nextPlaceValue;
                allDecoys += decoyGroups[index].Count * nextPlaceValue;
                nextPlaceValue *= decoyGroups[index].Count + 1;
            }

            Dictionary<BigInteger, double> unfoiledByDecoyPool = new Dictionary<BigInteger, double>
            {
                { allDecoys, 1d },
            };
            foreach (ISceneNode detector in detectors)
            {
                double noFoilProbability =
                    1d - Math.Clamp(GetFoilProbability(mission, detector) / 100d, 0, 1);
                Dictionary<BigInteger, double> next = new Dictionary<BigInteger, double>();
                foreach ((BigInteger availableDecoys, double probability) in unfoiledByDecoyPool)
                {
                    int[] availableCounts = new int[decoyGroups.Count];
                    int availableCount = 0;
                    for (int index = 0; index < decoyGroups.Count; index++)
                    {
                        availableCounts[index] = (int)(
                            availableDecoys
                            / groupPlaceValues[index]
                            % (decoyGroups[index].Count + 1)
                        );
                        availableCount += availableCounts[index];
                    }

                    if (availableCount == 0)
                    {
                        AddProbability(next, availableDecoys, probability * noFoilProbability);
                        continue;
                    }

                    for (int index = 0; index < decoyGroups.Count; index++)
                    {
                        if (availableCounts[index] == 0)
                            continue;

                        IMissionParticipant decoy = decoyGroups[index].Decoy;
                        double selectionProbability =
                            probability * availableCounts[index] / availableCount;
                        double diversionProbability = Math.Clamp(
                            mission.GetDecoyProbability(decoy, detector, _game) / 100d,
                            0,
                            1
                        );
                        double evasionProbability = GetParticipantEvasionProbability(
                            mission,
                            decoy,
                            detector
                        );

                        // A diversion or successful evasion leaves this decoy available.
                        AddProbability(
                            next,
                            availableDecoys,
                            selectionProbability
                                * (
                                    diversionProbability
                                    + (1d - diversionProbability)
                                        * evasionProbability
                                        * noFoilProbability
                                )
                        );

                        // A failed evasion removes this specific decoy from later checks.
                        AddProbability(
                            next,
                            availableDecoys - groupPlaceValues[index],
                            selectionProbability
                                * (1d - diversionProbability)
                                * (1d - evasionProbability)
                                * noFoilProbability
                        );
                    }
                }

                unfoiledByDecoyPool = next;
                if (unfoiledByDecoyPool.Count == 0)
                    break;
            }

            return (1d - unfoiledByDecoyPool.Values.Sum()) * 100d;
        }

        /// <summary>
        /// Estimates the chance that foiling removes at least one main officer.
        /// </summary>
        /// <param name="mission">The mission whose officers are exposed to detection.</param>
        /// <param name="detectors">The observed units that can confront mission participants.</param>
        /// <param name="foilProbability">The estimated chance that detection foils the mission.</param>
        /// <returns>The estimated personnel-loss percentage.</returns>
        private double EstimatePersonnelLossProbability(
            Mission mission,
            IReadOnlyList<ISceneNode> detectors,
            double foilProbability
        )
        {
            if (
                mission?.AppliesFoiledParticipantConsequences != true
                || detectors == null
                || detectors.Count == 0
                || foilProbability <= 0
            )
                return 0;

            double noOfficerLossProbability = 1d;
            foreach (Officer officer in mission.GetMainParticipants().OfType<Officer>())
            {
                double averageEvasionProbability = detectors.Average(detector =>
                    GetParticipantEvasionProbability(mission, officer, detector)
                );
                noOfficerLossProbability *= averageEvasionProbability;
            }

            return foilProbability * (1d - noOfficerLossProbability);
        }

        /// <summary>Adds probability to an existing or new decoy-pool outcome.</summary>
        /// <param name="probabilities">The probabilities.</param>
        /// <param name="decoyPool">The decoy pool.</param>
        /// <param name="probability">The probability.</param>
        private static void AddProbability(
            Dictionary<BigInteger, double> probabilities,
            BigInteger decoyPool,
            double probability
        )
        {
            if (probability <= 0)
                return;

            probabilities.TryGetValue(decoyPool, out double existingProbability);
            probabilities[decoyPool] = existingProbability + probability;
        }

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
            if (mission == null || detector == null)
                return 0;

            int score = CalculateFoilScore(mission, detector);
            return LookupProbability(GetMissionTables().Foil, score);
        }

        /// <summary>
        /// Calculates one detector's score against a mission team.
        /// </summary>
        /// <param name="mission">The mission attempting to remain undetected.</param>
        /// <param name="detector">The hostile unit making the detection attempt.</param>
        /// <returns>The score used to look up the foiling probability.</returns>
        private int CalculateFoilScore(Mission mission, ISceneNode detector)
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables();
            IReadOnlyList<IMissionParticipant> participants = mission.GetMainParticipants();
            Officer commander = mission.FindDetectorCommander(detector);
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
        /// Returns hostile detector units in the original traversal order.
        /// </summary>
        /// <param name="mission">The mission being checked for detection.</param>
        /// <param name="planet">The planet where the mission is operating.</param>
        /// <returns>The ordered detector units.</returns>
        internal static List<ISceneNode> GetDetectors(Mission mission, Planet planet)
        {
            List<ISceneNode> detectors = new List<ISceneNode>();
            AddEligibleDetectors(mission, planet.GetChildren<Starfighter>(), detectors);
            AddEligibleDetectors(mission, planet.GetChildren<Regiment>(), detectors);

            bool blocksFleetDetection = planet
                .GetChildren<Building>()
                .Any(building =>
                    building.IsDetectionBlocker
                    && building.OwnerInstanceID == mission.OwnerInstanceID
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                    && building.Movement == null
                );
            if (blocksFleetDetection)
                return detectors;

            foreach (Fleet fleet in planet.GetChildren<Fleet>())
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
                    AddEligibleDetectors(mission, capitalShip.GetChildren<Regiment>(), detectors);
                }
            }

            return detectors;
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
