using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Contains calculated mission probabilities without resolving an outcome.
    /// </summary>
    public sealed class MissionOdds
    {
        public double SuccessProbability { get; }

        public double PersonnelLossProbability { get; }

        /// <summary>
        /// Creates a mission probability result.
        /// </summary>
        /// <param name="successProbability">Probability that at least one participant succeeds.</param>
        /// <param name="personnelLossProbability">Probability that known defenses capture or kill an officer.</param>
        internal MissionOdds(double successProbability, double personnelLossProbability = 0)
        {
            SuccessProbability = successProbability;
            PersonnelLossProbability = personnelLossProbability;
        }
    }

    /// <summary>
    /// Provides the external operations needed while a mission executes its post-arrival lifecycle.
    /// </summary>
    internal interface IMissionExecutionRuntime
    {
        /// <summary>
        /// Resolves this tick's detection attempt.
        /// </summary>
        /// <param name="mission">The mission executing its lifecycle.</param>
        /// <param name="results">The result collection receiving detection consequences.</param>
        /// <returns>True when detection foils the mission.</returns>
        bool ResolveDetection(Mission mission, List<GameResult> results);

        /// <summary>
        /// Resolves betrayal or the completed mission objective.
        /// </summary>
        /// <param name="mission">The mission whose progress is complete.</param>
        /// <returns>The results produced by objective resolution.</returns>
        List<GameResult> ResolveCompletedObjective(Mission mission);

        /// <summary>
        /// Applies repeat or teardown infrastructure after mission completion.
        /// </summary>
        /// <param name="mission">The mission that reached a terminal state.</param>
        /// <param name="completedResult">The terminal result, or null for an invalid mission.</param>
        /// <param name="results">The result collection receiving teardown consequences.</param>
        void FinishMission(
            Mission mission,
            MissionCompletedResult completedResult,
            List<GameResult> results
        );
    }

    /// <summary>
    /// Describes one hostile unit that can detect a mission or confront a detected participant.
    /// </summary>
    internal sealed class MissionDetector
    {
        internal ISceneNode Unit { get; }

        internal Officer Commander { get; }

        internal int Rating { get; }

        internal bool IsFleetBased { get; }

        /// <summary>
        /// Creates a detector with its original detection context.
        /// </summary>
        /// <param name="unit">The detecting regiment, starfighter, or capital ship.</param>
        /// <param name="commander">The matching local commander, when one is present.</param>
        /// <param name="rating">The unit's authored detection rating.</param>
        /// <param name="isFleetBased">Whether the detector belongs to a fleet.</param>
        internal MissionDetector(ISceneNode unit, Officer commander, int rating, bool isFleetBased)
        {
            Unit = unit;
            Commander = commander;
            Rating = rating;
            IsFleetBased = isFleetBased;
        }
    }

    /// <summary>
    /// Captures the hostile detectors visible at one mission target for reuse during a planning
    /// turn.
    /// </summary>
    internal sealed class MissionDetectionSnapshot
    {
        internal IReadOnlyList<MissionDetector> Detectors { get; }

        /// <summary>
        /// Creates a target detection snapshot.
        /// </summary>
        /// <param name="detectors">The target's hostile mission detectors.</param>
        internal MissionDetectionSnapshot(IReadOnlyList<MissionDetector> detectors)
        {
            Detectors = detectors ?? Array.Empty<MissionDetector>();
        }
    }

    /// <summary>
    /// Base scene node for missions and their assigned participants.
    /// </summary>
    public abstract class Mission : ContainerNode
    {
        private const int _ratingPercentScale = 100;

        private string configKey;

        // Mission identity.
        public string ConfigKey
        {
            get => configKey;
            set
            {
                configKey = value;
                TypeID = value;
            }
        }

        public string LocationInstanceID { get; set; }

        /// <summary>
        /// Gets or sets the content event that authored this mission, when applicable.
        /// Results inherit this identity so data-defined reactions can replace or extend
        /// default presentation without coupling systems to particular event IDs.
        /// </summary>
        public string SourceEventInstanceID { get; set; }

        // Participants.
        [PersistableMember(Name = "MainParticipants")]
        private protected List<IMissionParticipant> _mainParticipants;

        [PersistableMember(Name = "DecoyParticipants")]
        private protected List<IMissionParticipant> _decoyParticipants;

        [PersistableIgnore]
        private HashSet<string> _mainParticipantInstanceIds = new HashSet<string>(
            StringComparer.Ordinal
        );

        [PersistableIgnore]
        private bool _hasCapturedMainParticipantIds;

        // Mission configuration.
        public OfficerRating ParticipantRating { get; set; }
        public bool HasInitiated;

        // Mission progress.
        public int MaxProgress { get; set; }
        public int CurrentProgress { get; set; }

        internal virtual bool AppliesFoiledParticipantConsequences => true;

        internal virtual bool SuccessfulParticipantsRemainAtLocation => false;

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        protected Mission()
        {
            _mainParticipants = new List<IMissionParticipant>();
            _decoyParticipants = new List<IMissionParticipant>();
        }

        /// <summary>
        /// Initializes a mission with all required parameters.
        /// </summary>
        /// <param name="configKey">Mission configuration key.</param>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="locationInstanceId">Mission location instance ID.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="participantRating">Rating used by primary participants.</param>
        /// <param name="displayName">Display name to show for this mission.</param>
        protected Mission(
            string configKey,
            string ownerInstanceId,
            string locationInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            OfficerRating participantRating,
            string displayName = null
        )
        {
            ConfigKey = configKey ?? throw new ArgumentNullException(nameof(configKey));
            DisplayName = displayName ?? configKey;
            OwnerInstanceID = ownerInstanceId;
            LocationInstanceID = locationInstanceId;

            _mainParticipants =
                mainParticipants != null
                    ? new List<IMissionParticipant>(mainParticipants)
                    : new List<IMissionParticipant>();
            _decoyParticipants =
                decoyParticipants != null
                    ? new List<IMissionParticipant>(decoyParticipants)
                    : new List<IMissionParticipant>();
            ParticipantRating = participantRating;
        }

        /// <summary>
        /// Copies shared mission state into an empty mission destination.
        /// </summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Mission copy = (Mission)destination;
            copy.ConfigKey = ConfigKey;
            copy.LocationInstanceID = LocationInstanceID;
            copy.SourceEventInstanceID = SourceEventInstanceID;
            copy._mainParticipantInstanceIds = new HashSet<string>(
                _mainParticipantInstanceIds,
                StringComparer.Ordinal
            );
            copy._hasCapturedMainParticipantIds = _hasCapturedMainParticipantIds;
            copy.ParticipantRating = ParticipantRating;
            copy.HasInitiated = HasInitiated;
            copy.MaxProgress = MaxProgress;
            copy.CurrentProgress = CurrentProgress;
        }

        /// <summary>
        /// Attaches a copied participant to the same primary or decoy role as its source.
        /// </summary>
        /// <param name="destination">The copied mission receiving the participant.</param>
        /// <param name="sourceChild">The source participant.</param>
        /// <param name="copiedChild">The copied participant.</param>
        protected override void AttachCopiedChild(
            BaseSceneNode destination,
            ISceneNode sourceChild,
            ISceneNode copiedChild
        )
        {
            if (copiedChild is not IMissionParticipant participant)
                return;

            if (
                sourceChild is IMissionParticipant sourceParticipant
                && _decoyParticipants.Contains(sourceParticipant)
            )
                ((Mission)destination).AddDecoyParticipant(participant);
            else
                destination.AddChild(copiedChild);

            copiedChild.SetParent(destination);
        }

        /// <summary>
        /// Validates that <paramref name="target"/> is non-null and is a Planet, then returns it.
        /// Call at the top of each mission constructor before mission-specific validation.
        /// </summary>
        /// <param name="target">The scene node to validate as a Planet.</param>
        /// <param name="missionName">Human-readable mission name used in the error message.</param>
        /// <exception cref="ArgumentNullException">target is null.</exception>
        /// <exception cref="InvalidOperationException">target is not a Planet.</exception>
        /// <returns>The validated Planet instance.</returns>
        protected static Planet RequirePlanetTarget(ISceneNode target, string missionName)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!(target is Planet planet))
                throw new InvalidOperationException(
                    $"{missionName} target must be a planet. Got: {target.GetType().Name}"
                );
            return planet;
        }

        /// <summary>
        /// Returns whether a mission target remains attached to the playable scene graph.
        /// </summary>
        internal static bool IsOperationalTarget(ISceneNode target)
        {
            if (target == null)
                return false;

            if (target is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building })
                return false;

            return target is not IMovable movable || movable.GetTransitMovement() == null;
        }

        /// <summary>
        /// Returns why this mission must stop before advancing.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        public MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            if (_mainParticipants.Count == 0 || HaveMainParticipantsChanged())
                return MissionCompletionReason.Failure;

            return GetMissionInvalidationReason(game);
        }

        /// <summary>
        /// Returns whether the mission should repeat after completing one execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should repeat; false to finish the mission.</returns>
        public abstract bool ShouldRepeatAfterCompletion(GameRoot game);

        /// <summary>
        /// Produces mission-specific state changes when the mission ends before objective resolution.
        /// The execution runtime remains responsible for participant teardown.
        /// </summary>
        internal virtual List<GameResult> ResolveInterruption(
            GameRoot game,
            IRandomNumberProvider provider
        ) => new List<GameResult>();

        /// <summary>
        /// Starts the mission and chooses its duration.
        /// </summary>
        /// <param name="maxProgress">The rolled mission duration.</param>
        public void Initiate(int maxProgress)
        {
            CurrentProgress = 0;
            MaxProgress = maxProgress;
            CaptureMainParticipantIDs();
            HasInitiated = true;
        }

        /// <summary>
        /// Increments progress by 1 unless any participant is in transit.
        /// </summary>
        public void IncrementProgress()
        {
            List<IMissionParticipant> all = GetAllParticipants();
            bool anyParticipantInTransit = all.Any(participant => participant.Movement != null);
            if (CurrentProgress < MaxProgress && !anyParticipantInTransit)
                CurrentProgress++;
        }

        /// <summary>
        /// Returns true when CurrentProgress has reached or exceeded MaxProgress.
        /// </summary>
        /// <returns>True if the mission has completed.</returns>
        public bool IsComplete() => CurrentProgress >= MaxProgress;

        /// <summary>
        /// Forces MaxProgress to a specific tick count, bypassing randomization. Used in tests.
        /// </summary>
        /// <param name="tick">The exact tick count to assign as MaxProgress.</param>
        public void SetExecutionTick(int tick) => MaxProgress = tick;

        /// <summary>
        /// Returns all main and decoy participants as a single list.
        /// </summary>
        /// <param name="includeDisabled">Whether disabled participants may be returned.</param>
        /// <returns>Combined list of main and decoy participants.</returns>
        public List<IMissionParticipant> GetAllParticipants(bool includeDisabled = false) =>
            GetMainParticipants(includeDisabled)
                .Concat(GetDecoyParticipants(includeDisabled))
                .ToList();

        /// <summary>
        /// Gets the mission's primary participants.
        /// </summary>
        /// <returns>The primary participants.</returns>
        public IReadOnlyList<IMissionParticipant> GetMainParticipants(
            bool includeDisabled = false
        ) =>
            includeDisabled
                ? _mainParticipants
                : _mainParticipants.Where(participant => participant.IsActive()).ToList();

        /// <summary>
        /// Gets the mission's decoy participants.
        /// </summary>
        /// <returns>The decoy participants.</returns>
        public IReadOnlyList<IMissionParticipant> GetDecoyParticipants(
            bool includeDisabled = false
        ) =>
            includeDisabled
                ? _decoyParticipants
                : _decoyParticipants.Where(participant => participant.IsActive()).ToList();

        /// <summary>
        /// Returns whether any mission participant is still travelling to the mission.
        /// </summary>
        /// <returns>True if any participant has active movement.</returns>
        public bool IsWaitingForParticipants() =>
            GetAllParticipants().Any(participant => participant.Movement != null);

        /// <summary>
        /// Captures the current required main-participant IDs.
        /// </summary>
        private void CaptureMainParticipantIDs()
        {
            _mainParticipantInstanceIds = GetMainParticipantIDs();
            _hasCapturedMainParticipantIds = true;
        }

        /// <summary>
        /// Returns whether the required main-participant list differs from mission start.
        /// </summary>
        /// <returns>True if a main participant was added or removed.</returns>
        private bool HaveMainParticipantsChanged()
        {
            if (!_hasCapturedMainParticipantIds)
            {
                CaptureMainParticipantIDs();
                return false;
            }

            HashSet<string> currentParticipantIds = GetMainParticipantIDs();
            if (currentParticipantIds.Count != _mainParticipantInstanceIds.Count)
                return true;

            return currentParticipantIds.Any(id => !_mainParticipantInstanceIds.Contains(id));
        }

        /// <summary>
        /// Returns all current main-participant IDs.
        /// </summary>
        /// <returns>The current main-participant ID set.</returns>
        private HashSet<string> GetMainParticipantIDs() =>
            _mainParticipants
                .Where(participant => !string.IsNullOrEmpty(participant.InstanceID))
                .Select(participant => participant.InstanceID)
                .ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Returns the participant's raw mission score before table lookup.
        /// </summary>
        /// <param name="agent">The participant whose rating is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's raw mission score, or null when it cannot be resolved.</returns>
        protected virtual int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            return agent?.GetEffectiveRating(ParticipantRating);
        }

        /// <summary>
        /// Returns the mission planet whether the mission is active or only being evaluated.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The mission planet, or null when it cannot be resolved.</returns>
        protected Planet GetMissionPlanet(GameRoot game)
        {
            return GetParent() as Planet
                ?? game?.GetSceneNodeByInstanceID<Planet>(LocationInstanceID);
        }

        /// <summary>
        /// Returns the participant's mission success probability.
        /// </summary>
        /// <param name="agent">The participant whose raw score is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The configured success probability, or zero when no score can be resolved.</returns>
        protected virtual double GetAgentProbability(IMissionParticipant agent, GameRoot game)
        {
            int? score = GetAgentScore(agent, game);
            return score.HasValue ? LookupSuccessProbability(game, score.Value) : 0;
        }

        /// <summary>
        /// Calculates success probability for a participant set without resolving the mission.
        /// </summary>
        /// <param name="participants">The participants to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The probability that at least one participant succeeds.</returns>
        internal virtual MissionOdds GetMissionOdds(
            IEnumerable<IMissionParticipant> participants,
            GameRoot game
        )
        {
            List<IMissionParticipant> evaluatedParticipants = (
                participants ?? Enumerable.Empty<IMissionParticipant>()
            )
                .Where(participant => participant != null)
                .ToList();
            IEnumerable<double> probabilities = evaluatedParticipants
                .Where(participant => participant != null)
                .Select(participant => GetAgentProbability(participant, game));
            return new MissionOdds(CombineSuccessProbabilities(probabilities));
        }

        /// <summary>
        /// Calculates mission success and officer-loss probabilities from the faction's observed
        /// target state without resolving an outcome.
        /// </summary>
        /// <param name="participants">The primary participants being evaluated.</param>
        /// <param name="observedPlanet">The faction-visible mission target.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The calculated mission odds.</returns>
        internal MissionOdds GetMissionOdds(
            IEnumerable<IMissionParticipant> participants,
            Planet observedPlanet,
            GameRoot game
        )
        {
            return GetMissionOdds(participants, GetDetectionSnapshot(observedPlanet), game);
        }

        /// <summary>
        /// Calculates mission odds using a previously captured target-detection snapshot.
        /// </summary>
        /// <param name="participants">The primary participants being evaluated.</param>
        /// <param name="detectionSnapshot">The target's cached hostile detectors.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The calculated mission odds.</returns>
        internal MissionOdds GetMissionOdds(
            IEnumerable<IMissionParticipant> participants,
            MissionDetectionSnapshot detectionSnapshot,
            GameRoot game
        )
        {
            List<IMissionParticipant> evaluatedParticipants = (
                participants ?? Enumerable.Empty<IMissionParticipant>()
            )
                .Where(participant => participant != null)
                .ToList();
            MissionOdds successOdds = GetMissionOdds(evaluatedParticipants, game);
            double lossPerDetectionPass = GetProjectedPersonnelLossProbability(
                evaluatedParticipants,
                detectionSnapshot?.Detectors,
                game
            );
            return new MissionOdds(
                successOdds.SuccessProbability,
                GetPersonnelLossProbabilityBeforeSuccess(
                    lossPerDetectionPass,
                    successOdds.SuccessProbability
                )
            );
        }

        /// <summary>
        /// Returns the probability that repeated detection passes remove an officer before the
        /// mission records its first successful objective roll.
        /// </summary>
        /// <param name="lossPerDetectionPass">Officer-loss probability for one detection pass.</param>
        /// <param name="successPerSurvivedPass">Objective success probability after surviving detection.</param>
        /// <returns>The eventual officer-loss probability as a percentage.</returns>
        private static double GetPersonnelLossProbabilityBeforeSuccess(
            double lossPerDetectionPass,
            double successPerSurvivedPass
        )
        {
            double loss = Math.Clamp(lossPerDetectionPass, 0, 100) / 100d;
            double success = Math.Clamp(successPerSurvivedPass, 0, 100) / 100d;
            double terminalProbability = loss + (1d - loss) * success;
            return terminalProbability > 0 ? loss / terminalProbability * 100d : 0;
        }

        /// <summary>
        /// Estimates the chance that known defenses capture or kill an officer during one
        /// detection pass, including the protection supplied by assigned decoys.
        /// </summary>
        /// <param name="participants">The primary participants being evaluated.</param>
        /// <param name="detectors">The faction-visible detectors at the mission target.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The projected officer-loss probability as a percentage.</returns>
        private double GetProjectedPersonnelLossProbability(
            IReadOnlyList<IMissionParticipant> participants,
            IReadOnlyList<MissionDetector> detectors,
            GameRoot game
        )
        {
            List<Officer> officers = participants.OfType<Officer>().ToList();
            if (officers.Count == 0 || detectors == null || detectors.Count == 0)
                return 0;

            List<IMissionParticipant> decoys = GetDecoyParticipants().ToList();
            double noLossProbability = 1d;
            foreach (MissionDetector detector in detectors)
            {
                double decoyProbability =
                    decoys.Count == 0
                        ? 0
                        : decoys.Average(decoy => GetDecoyProbability(decoy, detector, game));
                double foilProbability = GetFoilProbability(
                    detector.Rating,
                    detector.Commander,
                    game
                );
                double lossAfterConfrontation = officers.Average(officer =>
                    100d
                    - GetEvasionProbability(
                        officer.GetEffectiveRating(OfficerRating.Combat)
                            - (detector.Commander?.GetEffectiveRating(OfficerRating.Combat) ?? 0),
                        game
                    )
                );
                double detectorLossProbability =
                    (100d - decoyProbability)
                    / 100d
                    * foilProbability
                    / 100d
                    * lossAfterConfrontation
                    / 100d;
                noLossProbability *= 1d - detectorLossProbability;
            }

            return (1d - noLossProbability) * 100d;
        }

        /// <summary>
        /// Returns the configured evasion probability for an officer confronting a detector.
        /// </summary>
        /// <param name="score">The officer's combat rating minus commander combat rating.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The evasion probability as a percentage.</returns>
        private static double GetEvasionProbability(int score, GameRoot game)
        {
            GameConfig.MissionProbabilityTablesConfig tables = GetMissionTables(game);
            return LookupProbability(tables.Evasion, score, tables.DefaultEvasionProbability);
        }

        /// <summary>
        /// Combines independent success probabilities into one success chance.
        /// </summary>
        /// <param name="probabilities">The individual percentage probabilities.</param>
        /// <returns>The combined percentage probability.</returns>
        protected static double CombineSuccessProbabilities(IEnumerable<double> probabilities)
        {
            double failureProbability = (probabilities ?? Enumerable.Empty<double>())
                .Select(probability => Math.Clamp(probability, 0, 100) / 100d)
                .Aggregate(1d, (combined, probability) => combined * (1d - probability));
            return (1d - failureProbability) * 100d;
        }

        /// <summary>
        /// Returns the decoy participant's success probability.
        /// </summary>
        /// <param name="decoy">The decoy participant to evaluate.</param>
        /// <param name="detector">The detector being diverted.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The decoy success probability.</returns>
        private double GetDecoyProbability(
            IMissionParticipant decoy,
            MissionDetector detector,
            GameRoot game
        )
        {
            int decoyEspionage = decoy.GetEffectiveRating(OfficerRating.Espionage);
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables(game);
            int scaledDefender =
                (detector.Commander?.GetEffectiveRating(OfficerRating.Espionage) ?? 0)
                * missionTables.DecoyDefenderScalingPercent
                / _ratingPercentScale;
            int score = decoyEspionage - detector.Rating - scaledDefender;
            Dictionary<int, int> table = detector.IsFleetBased
                ? missionTables.FleetDecoy
                : missionTables.PlanetaryDecoy;
            return LookupProbability(table, score);
        }

        /// <summary>
        /// Returns the probability that enemy forces detect the mission.
        /// </summary>
        /// <param name="detectorRating">The selected enemy detector's detection rating.</param>
        /// <param name="defender">The commander paired with the selected detector, if present.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The foil probability.</returns>
        protected virtual double GetFoilProbability(
            int detectorRating,
            Officer defender,
            GameRoot game
        )
        {
            int defenderEspionage = defender?.GetEffectiveRating(OfficerRating.Espionage) ?? 0;
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables(game);
            int scaledDefender =
                defenderEspionage * missionTables.FoilDefenderScalingPercent / _ratingPercentScale;
            int specialForcesPenalty = GetMainParticipants().OfType<SpecialForces>().Count();
            int score =
                GetAveragedRating(OfficerRating.Espionage)
                - scaledDefender
                - detectorRating
                - specialForcesPenalty
                - missionTables.FoilFlatScoreAdjustment;
            return LookupProbability(missionTables.Foil, score);
        }

        /// <summary>
        /// Returns the success probability for this mission's configured table.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="score">The mission success score.</param>
        /// <returns>The configured success probability.</returns>
        protected double LookupSuccessProbability(GameRoot game, int score)
        {
            return LookupSuccessProbability(game, score, ConfigKey);
        }

        /// <summary>
        /// Returns the success probability from an explicitly selected mission table.
        /// </summary>
        /// <param name="game">The game state containing probability configuration.</param>
        /// <param name="score">The mission score to look up.</param>
        /// <param name="probabilityTableKey">The authored mission probability-table key.</param>
        /// <returns>The configured success probability.</returns>
        protected double LookupSuccessProbability(
            GameRoot game,
            int score,
            string probabilityTableKey
        )
        {
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables(game);
            return missionTables.GetSuccessProbability(probabilityTableKey, score);
        }

        /// <summary>
        /// Returns the mission probability table config for the current game.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The configured mission probability tables.</returns>
        private static GameConfig.MissionProbabilityTablesConfig GetMissionTables(GameRoot game)
        {
            return game?.Config?.ProbabilityTables?.Mission
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

        /// <summary>
        /// Returns the average effective rating for the mission's main participants.
        /// </summary>
        /// <param name="rating">The rating to average.</param>
        /// <returns>The averaged effective rating, or 0 when no main participants exist.</returns>
        private int GetAveragedRating(OfficerRating rating)
        {
            if (GetMainParticipants().Count == 0)
                return 0;

            return GetMainParticipants().Sum(participant => participant.GetEffectiveRating(rating))
                / GetMainParticipants().Count;
        }

        /// <summary>
        /// Returns whether a participant can command a detector during a detection check.
        /// </summary>
        /// <param name="candidate">The support candidate.</param>
        /// <returns>True when the candidate can command a detector.</returns>
        private static bool IsEligibleDetectorCommander(IMissionParticipant candidate)
        {
            if (candidate is Officer officer)
            {
                return officer.Movement == null
                    && officer.GetParent() is not Mission
                    && !officer.IsCaptured
                    && !officer.IsKilled
                    && officer.InjuryPoints == 0;
            }

            return candidate is SpecialForces specialForces && specialForces.IsMovable();
        }

        /// <summary>
        /// Returns whether a probability roll succeeds.
        /// </summary>
        /// <param name="rolledValue">The rolled value.</param>
        /// <param name="successThreshold">The success threshold.</param>
        /// <returns>True if the roll succeeds.</returns>
        protected virtual bool IsSuccessfulProbabilityRoll(
            double rolledValue,
            double successThreshold
        )
        {
            return rolledValue < successThreshold;
        }

        /// <summary>
        /// Resolves main participants using the original character-first attempt order.
        /// Officer probabilities are calculated before any attempts and ordered from lowest to
        /// highest. Special forces then attempt the mission in their selected order. Resolution
        /// can stop after the first success for missions whose objective permits only one winner.
        /// </summary>
        /// <param name="provider">RNG provider for rolling against the success probability.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="resolveSuccessfulAttempt">
        /// Optional mission-specific resolution applied immediately after each successful roll.
        /// Return true when that attempt earned the mission's normal participant improvement.
        /// </param>
        /// <param name="stopAfterFirstSuccess">Whether resolution stops after the first successful attempt.</param>
        /// <returns>The participants whose attempts succeeded, in attempt order.</returns>
        protected internal List<IMissionParticipant> ResolveSuccessfulParticipants(
            IRandomNumberProvider provider,
            GameRoot game,
            Func<IMissionParticipant, bool> resolveSuccessfulAttempt = null,
            bool stopAfterFirstSuccess = false
        )
        {
            List<(Officer Participant, double Probability)> officerAttempts = GetMainParticipants()
                .OfType<Officer>()
                .Select(officer =>
                    (Participant: officer, Probability: GetAgentProbability(officer, game))
                )
                .OrderBy(attempt => attempt.Probability)
                .ToList();
            List<IMissionParticipant> successfulParticipants = new List<IMissionParticipant>();

            foreach ((Officer participant, double probability) in officerAttempts)
            {
                if (!RollProbability(provider, probability))
                    continue;

                successfulParticipants.Add(participant);
                if (resolveSuccessfulAttempt?.Invoke(participant) == true)
                    ImproveMissionParticipantRating(participant);
                if (stopAfterFirstSuccess)
                    return successfulParticipants;
            }

            foreach (SpecialForces specialForces in GetMainParticipants().OfType<SpecialForces>())
            {
                if (!RollParticipantSuccess(specialForces, provider, game))
                    continue;

                successfulParticipants.Add(specialForces);
                if (resolveSuccessfulAttempt?.Invoke(specialForces) == true)
                    ImproveMissionParticipantRating(specialForces);
                if (stopAfterFirstSuccess)
                    return successfulParticipants;
            }

            return successfulParticipants;
        }

        /// <summary>
        /// Rolls the original post-injury death check, which applies only to minor personnel.
        /// Main characters survive mission injuries regardless of the configured probability.
        /// </summary>
        /// <param name="officer">The injured officer.</param>
        /// <param name="provider">RNG provider for the percentage roll.</param>
        /// <param name="deathProbability">Configured probability that minor personnel die.</param>
        /// <returns>True when the injured minor officer dies.</returns>
        protected static bool RollPostInjuryDeath(
            Officer officer,
            IRandomNumberProvider provider,
            int deathProbability
        )
        {
            if (officer?.IsMain != false)
                return false;

            int clampedProbability = Math.Min(100, Math.Max(0, deathProbability));
            return provider.NextInt(0, 100) < clampedProbability;
        }

        /// <summary>
        /// Applies the injury and minor-character death checks used when capture is attempted.
        /// </summary>
        /// <param name="officer">The officer attempting to avoid capture.</param>
        /// <param name="opponent">The entity responsible for the capture attempt.</param>
        /// <param name="planet">The planet where the confrontation occurs.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for injury and death rolls.</param>
        /// <param name="results">The result collection receiving injury or death results.</param>
        /// <returns>True when the injury kills the officer.</returns>
        internal static bool ApplyCaptureEvasionInjury(
            Officer officer,
            IGameEntity opponent,
            Planet planet,
            GameRoot game,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            int injuryChance = Math.Max(
                game.Config.DuelResolution.MinimumInjuryChance,
                game.Config.DuelResolution.CaptureEvasionInjuryBaseChance
                    - officer.GetEffectiveRating(OfficerRating.Combat)
            );
            if (provider.NextInt(0, 100) >= Math.Min(100, injuryChance))
                return false;

            int injury =
                game.Config.DuelResolution.InjuryBase
                + provider.NextInt(0, injuryChance + 1)
                + provider.NextInt(0, game.Config.DuelResolution.InjurySecondaryRollMaximum + 1);
            officer.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
            results.Add(
                new OfficerInjuredResult
                {
                    Officer = officer,
                    Severity = injury,
                    Tick = game.CurrentTick,
                }
            );

            if (!RollPostInjuryDeath(officer, provider, game.Config.Assassination.KillProbability))
                return false;

            results.Add(
                new OfficerKilledResult
                {
                    TargetOfficer = officer,
                    Assassin = opponent,
                    Context = planet,
                    Tick = game.CurrentTick,
                }
            );
            return true;
        }

        /// <summary>
        /// Rolls one main participant against this mission's success probability.
        /// </summary>
        /// <param name="participant">The participant attempting the mission.</param>
        /// <param name="provider">The random number provider for the roll.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the participant succeeds.</returns>
        internal bool RollParticipantSuccess(
            IMissionParticipant participant,
            IRandomNumberProvider provider,
            GameRoot game
        )
        {
            double successThreshold = GetAgentProbability(participant, game);
            return RollProbability(provider, successThreshold);
        }

        /// <summary>
        /// Rolls once against a previously calculated success probability.
        /// </summary>
        /// <param name="provider">RNG provider for the percentage roll.</param>
        /// <param name="successThreshold">Probability required for success.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollProbability(IRandomNumberProvider provider, double successThreshold)
        {
            return IsSuccessfulProbabilityRoll(provider.NextDouble() * 100, successThreshold);
        }

        /// <summary>
        /// Rolls one selected decoy participant against one detector.
        /// </summary>
        /// <param name="decoy">The decoy participant making the attempt.</param>
        /// <param name="provider">RNG provider for selection and probability roll.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="detector">The detector the decoy is attempting to divert.</param>
        /// <returns>True if the selected decoy succeeds.</returns>
        private bool CheckDecoySuccessful(
            IMissionParticipant decoy,
            IRandomNumberProvider provider,
            GameRoot game,
            MissionDetector detector
        )
        {
            if (decoy == null || detector == null)
                return false;

            return IsSuccessfulProbabilityRoll(
                provider.NextDouble() * 100,
                GetDecoyProbability(decoy, detector, game)
            );
        }

        /// <summary>
        /// Returns hostile detector units in the original foil traversal order.
        /// </summary>
        /// <returns>The ordered detector collection.</returns>
        internal List<MissionDetector> GetDetectors()
        {
            if (GetParent() is not Planet planet)
                return new List<MissionDetector>();

            return GetDetectors(planet);
        }

        /// <summary>
        /// Captures the hostile detectors currently visible at a prospective mission target.
        /// </summary>
        /// <param name="planet">The observed mission target.</param>
        /// <returns>A reusable target-detection snapshot.</returns>
        internal MissionDetectionSnapshot GetDetectionSnapshot(Planet planet)
        {
            return new MissionDetectionSnapshot(GetDetectors(planet));
        }

        /// <summary>
        /// Returns hostile detector units visible at the supplied mission target.
        /// </summary>
        /// <param name="planet">The observed mission target.</param>
        /// <returns>The ordered detector collection.</returns>
        private List<MissionDetector> GetDetectors(Planet planet)
        {
            if (planet == null)
                return new List<MissionDetector>();

            List<MissionDetector> detectors = new List<MissionDetector>();
            foreach (Starfighter starfighter in planet.GetChildren<Starfighter>())
            {
                if (IsEligibleDetector(starfighter))
                    detectors.Add(CreateDetector(planet, starfighter));
            }

            foreach (Regiment regiment in planet.GetChildren<Regiment>())
            {
                if (IsEligibleDetector(regiment))
                    detectors.Add(CreateDetector(planet, regiment));
            }

            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
                {
                    if (IsEligibleDetector(capitalShip))
                        detectors.Add(CreateDetector(planet, capitalShip));

                    foreach (Starfighter starfighter in capitalShip.GetChildren<Starfighter>())
                    {
                        if (IsEligibleDetector(starfighter))
                            detectors.Add(CreateDetector(planet, starfighter));
                    }

                    foreach (Regiment regiment in capitalShip.GetChildren<Regiment>())
                    {
                        if (IsEligibleDetector(regiment))
                            detectors.Add(CreateDetector(planet, regiment));
                    }
                }
            }

            return detectors;
        }

        /// <summary>
        /// Creates a detector and resolves its matching local commander.
        /// </summary>
        /// <param name="planet">The mission planet.</param>
        /// <param name="unit">The detecting unit.</param>
        /// <returns>The resolved detector.</returns>
        private static MissionDetector CreateDetector(Planet planet, ISceneNode unit)
        {
            return new MissionDetector(
                unit,
                FindDetectorCommander(planet, unit),
                GetDetectorRating(unit),
                unit.GetParentOfType<Fleet>() != null
            );
        }

        /// <summary>
        /// Selects one detector uniformly for a post-foil participant encounter.
        /// </summary>
        /// <param name="detectors">The remaining active detectors.</param>
        /// <param name="provider">RNG provider used for selection.</param>
        /// <returns>The selected detector, or null when none remain.</returns>
        internal static MissionDetector SelectDetector(
            IReadOnlyList<MissionDetector> detectors,
            IRandomNumberProvider provider
        )
        {
            if (detectors == null || detectors.Count == 0)
                return null;

            return detectors[provider.NextInt(0, detectors.Count)];
        }

        /// <summary>
        /// Returns whether a scene object may attempt to detect this mission.
        /// </summary>
        /// <param name="candidate">The potential hostile detector.</param>
        /// <returns>True for a completed, stationary hostile unit with a detection rating.</returns>
        private bool IsEligibleDetector(ISceneNode candidate)
        {
            string candidateOwnerId = candidate?.GetOwnerInstanceID();
            if (
                string.IsNullOrEmpty(candidateOwnerId)
                || candidateOwnerId == OwnerInstanceID
                || candidate
                    is not IManufacturable { ManufacturingStatus: ManufacturingStatus.Complete }
                || candidate is IMovable movable && movable.GetTransitMovement() != null
                || candidate.GetParentOfType<Fleet>()?.GetTransitMovement() != null
            )
                return false;

            return candidate is Regiment or Starfighter or CapitalShip;
        }

        /// <summary>
        /// Returns the authored detection rating for a detector unit.
        /// </summary>
        /// <param name="detector">The selected detector.</param>
        /// <returns>The unit's detection rating.</returns>
        private static int GetDetectorRating(ISceneNode detector) =>
            detector switch
            {
                Regiment regiment => regiment.DetectionRating,
                Starfighter starfighter => starfighter.DetectionRating,
                CapitalShip capitalShip => capitalShip.DetectionRating,
                _ => 0,
            };

        /// <summary>
        /// Finds the commander type paired with the selected detector in its local container.
        /// </summary>
        /// <param name="planet">The mission planet.</param>
        /// <param name="detector">The selected hostile detector.</param>
        /// <returns>The matching commander, or null when none is assigned.</returns>
        private static Officer FindDetectorCommander(Planet planet, ISceneNode detector)
        {
            OfficerRank requiredRank = detector switch
            {
                Starfighter => OfficerRank.Commander,
                CapitalShip => OfficerRank.Admiral,
                Regiment => OfficerRank.General,
                _ => OfficerRank.None,
            };
            if (requiredRank == OfficerRank.None)
                return null;

            Fleet fleet = detector.GetParentOfType<Fleet>();
            IEnumerable<Officer> candidates =
                fleet != null
                    ? fleet.GetChildren<Officer>(recursive: true)
                    : planet
                        .GetChildren<Officer>(recursive: true)
                        .Where(officer => officer.GetParentOfType<Fleet>() == null);
            string defenderOwnerId = detector.GetOwnerInstanceID();
            return candidates.FirstOrDefault(officer =>
                officer.GetOwnerInstanceID() == defenderOwnerId
                && officer.CurrentRank == requiredRank
                && IsEligibleDetectorCommander(officer)
            );
        }

        /// <summary>
        /// Rolls one detector's mission foil check.
        /// </summary>
        /// <param name="provider">RNG provider for the foil roll.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="detector">The detector making this attempt.</param>
        /// <returns>True if the mission is detected this tick.</returns>
        internal bool RollFoilCheck(
            IRandomNumberProvider provider,
            GameRoot game,
            MissionDetector detector
        )
        {
            if (detector == null)
                return false;

            double foilProbability = GetFoilProbability(detector.Rating, detector.Commander, game);

            if (foilProbability <= 0)
                return false;

            return IsSuccessfulProbabilityRoll(provider.NextDouble() * 100, foilProbability);
        }

        /// <summary>
        /// Rolls the decoy response check.
        /// </summary>
        /// <param name="provider">RNG provider for decoy rolls.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="decoy">The decoy selected for this attempt.</param>
        /// <param name="detector">The detector being diverted.</param>
        /// <returns>True if the decoy diverts the detector.</returns>
        internal bool RollDecoyCheck(
            IRandomNumberProvider provider,
            GameRoot game,
            IMissionParticipant decoy,
            MissionDetector detector
        )
        {
            return CheckDecoySuccessful(decoy, provider, game, detector);
        }

        /// <summary>
        /// Executes one post-arrival lifecycle step for this mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for all probability rolls.</param>
        /// <param name="runtime">External mission operations supplied by the mission system.</param>
        /// <returns>All results produced by validation, detection, or objective resolution.</returns>
        internal List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionExecutionRuntime runtime
        )
        {
            List<GameResult> results = new List<GameResult>();
            MissionCompletionReason? abortReason = GetAbortReason(game);
            if (abortReason.HasValue)
            {
                AddMissionResults(ResolveInterruption(game, provider), results);
                results.Add(
                    BuildTerminatingResult(
                        MissionOutcome.Failed,
                        abortReason.Value,
                        game,
                        GetAllParticipants()
                    )
                );
                runtime.FinishMission(this, null, results);
                return results;
            }

            List<IMissionParticipant> participantsBeforeDetection = GetAllParticipants();
            if (runtime.ResolveDetection(this, results))
            {
                AddMissionResults(ResolveInterruption(game, provider), results);
                MissionCompletedResult completed = BuildTerminatingResult(
                    MissionOutcome.Foiled,
                    MissionCompletionReason.Foiled,
                    game,
                    participantsBeforeDetection
                );
                results.Add(completed);
                runtime.FinishMission(this, completed, results);
                return results;
            }

            IncrementProgress();
            if (!IsComplete())
                return results;

            results.AddRange(runtime.ResolveCompletedObjective(this));
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .LastOrDefault();
            if (completedResult != null)
                runtime.FinishMission(this, completedResult, results);

            return results;
        }

        /// <summary>
        /// Resolves the completed mission objective and returns its terminal results.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for all probability rolls.</param>
        /// <returns>All objective results, with a MissionCompletedResult appended last.</returns>
        internal virtual List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
            MissionOutcome outcome;
            MissionCompletionReason completionReason;

            List<IMissionParticipant> successfulParticipants = ResolveSuccessfulParticipants(
                provider,
                game
            );
            if (successfulParticipants.Count > 0)
            {
                outcome = MissionOutcome.Success;
                completionReason = MissionCompletionReason.Success;
                results.AddRange(OnSuccess(game, provider, successfulParticipants[0]));
                ImproveMissionParticipants(successfulParticipants);
            }
            else
            {
                outcome = MissionOutcome.Failed;
                completionReason = GetFailedCompletionReason(game);
                results.AddRange(OnFailed(game, provider));
            }

            results.Add(BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        /// <summary>
        /// Adds mission-origin metadata to interruption results before appending them.
        /// </summary>
        /// <param name="source">The results produced by the interruption.</param>
        /// <param name="destination">The lifecycle result collection.</param>
        private void AddMissionResults(
            IEnumerable<GameResult> source,
            ICollection<GameResult> destination
        )
        {
            if (source == null)
                return;

            foreach (GameResult result in source.Where(result => result != null))
            {
                result.MissionInstanceID = InstanceID;
                if (string.IsNullOrEmpty(result.SourceEventInstanceID))
                    result.SourceEventInstanceID = SourceEventInstanceID;
                destination.Add(result);
            }
        }

        /// <summary>
        /// Resolves a mission that an assigned officer deliberately betrayed.
        /// Betrayal foils the objective without applying enemy-detection consequences.
        /// </summary>
        internal List<GameResult> ResolveBetrayedMission(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = OnFailed(game, provider);
            results.Add(
                BuildCompletedResult(MissionOutcome.Foiled, MissionCompletionReason.Foiled, game)
            );
            return results;
        }

        /// <summary>
        /// Returns the completion reason for a failed mission success roll.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The failed mission completion reason.</returns>
        protected virtual MissionCompletionReason GetFailedCompletionReason(GameRoot game) =>
            MissionCompletionReason.Failure;

        /// <summary>
        /// Builds the <see cref="MissionCompletedResult"/> that terminates an Execute call.
        /// Shared by the base implementation and any subclass that overrides Execute.
        /// </summary>
        /// <param name="outcome">The resolved mission outcome.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="participants">Optional participant snapshot to include in the result.</param>
        /// <returns>A populated MissionCompletedResult.</returns>
        protected internal MissionCompletedResult BuildCompletedResult(
            MissionOutcome outcome,
            GameRoot game,
            List<IMissionParticipant> participants = null
        )
        {
            return new MissionCompletedResult
            {
                Mission = this,
                MissionInstanceID = InstanceID,
                MissionName = DisplayName,
                MissionTypeID = TypeID,
                TargetName = (GetParent() as Planet)?.GetDisplayName() ?? string.Empty,
                Location = GetParent() as Planet,
                Participants = participants ?? GetAllParticipants(),
                Outcome = outcome,
                CompletionReason = GetDefaultCompletionReason(outcome),
                CanContinue = ShouldRepeatAfterCompletion(game),
                Tick = game.CurrentTick,
                SourceEventInstanceID = SourceEventInstanceID,
            };
        }

        /// <summary>
        /// Builds the <see cref="MissionCompletedResult"/> with an explicit completion reason.
        /// </summary>
        /// <param name="outcome">The resolved mission outcome.</param>
        /// <param name="completionReason">The completion reason to include.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="participants">Optional participant snapshot to include in the result.</param>
        /// <returns>A populated MissionCompletedResult.</returns>
        protected internal MissionCompletedResult BuildCompletedResult(
            MissionOutcome outcome,
            MissionCompletionReason completionReason,
            GameRoot game,
            List<IMissionParticipant> participants = null
        )
        {
            MissionCompletedResult result = BuildCompletedResult(outcome, game, participants);
            result.CompletionReason = completionReason;
            return result;
        }

        /// <summary>
        /// Builds a terminal mission result that cannot repeat.
        /// </summary>
        /// <param name="outcome">The terminal mission outcome.</param>
        /// <param name="completionReason">The reason the mission terminated.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="participants">The participants captured before terminal side effects.</param>
        /// <returns>A non-continuing mission completion result.</returns>
        private MissionCompletedResult BuildTerminatingResult(
            MissionOutcome outcome,
            MissionCompletionReason completionReason,
            GameRoot game,
            List<IMissionParticipant> participants
        )
        {
            MissionCompletedResult result = BuildCompletedResult(
                outcome,
                completionReason,
                game,
                participants
            );
            result.CanContinue = false;
            return result;
        }

        /// <summary>
        /// Returns the default completion reason for a mission outcome.
        /// </summary>
        /// <param name="outcome">The mission outcome.</param>
        /// <returns>The default completion reason for the outcome.</returns>
        private static MissionCompletionReason GetDefaultCompletionReason(MissionOutcome outcome)
        {
            return outcome switch
            {
                MissionOutcome.Success => MissionCompletionReason.Success,
                MissionOutcome.Foiled => MissionCompletionReason.Foiled,
                _ => MissionCompletionReason.Failure,
            };
        }

        /// <summary>
        /// Improves an eligible officer's base rating after that participant succeeds.
        /// </summary>
        /// <param name="participant">The successful mission participant.</param>
        internal virtual void ImproveMissionParticipantRating(IMissionParticipant participant)
        {
            if (participant is Officer officer && participant.CanImproveMissionRating)
                officer.IncrementBaseRating(ParticipantRating);
        }

        /// <summary>
        /// Applies this mission's configured improvement to each eligible successful participant.
        /// </summary>
        /// <param name="participants">The successful participants to improve.</param>
        private void ImproveMissionParticipants(IEnumerable<IMissionParticipant> participants)
        {
            foreach (IMissionParticipant participant in participants)
                ImproveMissionParticipantRating(participant);
        }

        /// <summary>
        /// Returns why the mission's current objective state prevents it from advancing.
        /// The shared implementation requires every mission to remain attached to a live planet;
        /// subclasses extend this with mission-specific rules.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The invalidation reason, or null while the mission may advance.</returns>
        protected virtual MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            return GetParent() is Planet { IsDestroyed: false }
                ? null
                : MissionCompletionReason.TargetUnavailable;
        }

        /// <summary>
        /// Applies successful mission effects.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for any randomized effects.</param>
        /// <param name="successfulParticipant">The participant whose objective roll succeeded.</param>
        /// <returns>Results produced by the success outcome; empty by default.</returns>
        protected virtual List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        ) => new List<GameResult>();

        /// <summary>
        /// Applies failed mission effects.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for any randomized effects.</param>
        /// <returns>Results produced by the failed outcome; empty by default.</returns>
        protected virtual List<GameResult> OnFailed(
            GameRoot game,
            IRandomNumberProvider provider
        ) => new List<GameResult>();

        /// <summary>
        /// Returns extra movable units that must travel with participants after a successful mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Additional units that must accompany the mission participants.</returns>
        internal virtual IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game) =>
            Enumerable.Empty<IMovable>();

        /// <summary>
        /// Returns all mission participants as children of the mission.
        /// </summary>
        /// <returns>Assigned participants currently parented to the mission.</returns>
        protected override IEnumerable<ISceneNode> EnumerateChildren() =>
            EnumerateParticipants()
                .Where(participant => participant.ParentInstanceID == InstanceID);

        /// <summary>
        /// Enumerates all primary and decoy participants without applying lifecycle visibility.
        /// </summary>
        /// <returns>All primary and decoy participants.</returns>
        private IEnumerable<ISceneNode> EnumerateParticipants()
        {
            return _mainParticipants
                .Cast<ISceneNode>()
                .Concat(_decoyParticipants.Cast<ISceneNode>());
        }

        /// <summary>
        /// Only mission participants may be moved into a mission node.
        /// </summary>
        /// <param name="child">The node to test.</param>
        /// <returns>True if child is an IMissionParticipant.</returns>
        public override bool CanAcceptChild(ISceneNode child) => child is IMissionParticipant;

        /// <summary>
        /// Adds a mission participant to its authored primary or decoy collection.
        /// </summary>
        /// <param name="child">The participant to add.</param>
        public override void AddChild(ISceneNode child)
        {
            if (child is not IMissionParticipant participant)
                return;

            if (
                !_mainParticipants.Contains(participant)
                && !_decoyParticipants.Contains(participant)
            )
                _mainParticipants.Add(participant);
        }

        /// <summary>
        /// Adds a participant to the mission's decoy team.
        /// </summary>
        /// <param name="participant">The decoy participant to add.</param>
        internal void AddDecoyParticipant(IMissionParticipant participant)
        {
            if (
                participant != null
                && !_mainParticipants.Contains(participant)
                && !_decoyParticipants.Contains(participant)
            )
                _decoyParticipants.Add(participant);
        }

        /// <summary>
        /// Removes the child from participant lists (called by GameRoot.MoveNode/DetachNode).
        /// </summary>
        /// <param name="child">The node to remove from participant lists.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is IMissionParticipant participant)
            {
                _mainParticipants.Remove(participant);
                _decoyParticipants.Remove(participant);
            }
        }
    }
}
