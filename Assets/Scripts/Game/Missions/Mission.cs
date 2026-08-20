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
    /// Describes one hostile unit that can detect a mission or confront a detected participant.
    /// </summary>
    internal sealed class MissionDetector
    {
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

        internal ISceneNode Unit { get; }

        internal Officer Commander { get; }

        internal int Rating { get; }

        internal bool IsFleetBased { get; }
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
        public string OriginInstanceID { get; set; }

        /// <summary>
        /// Gets or sets the content event that authored this mission, when applicable.
        /// Results inherit this identity so data-defined reactions can replace or extend
        /// default presentation without coupling systems to particular event IDs.
        /// </summary>
        public string SourceEventInstanceID { get; set; }

        // Participants.
        public List<IMissionParticipant> MainParticipants { get; set; }

        public List<IMissionParticipant> DecoyParticipants { get; set; }

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

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        protected Mission()
        {
            MainParticipants = new List<IMissionParticipant>();
            DecoyParticipants = new List<IMissionParticipant>();
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

            MainParticipants = mainParticipants ?? new List<IMissionParticipant>();
            DecoyParticipants = decoyParticipants ?? new List<IMissionParticipant>();
            ParticipantRating = participantRating;
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
        /// Returns whether this mission is canceled when target ownership changes.
        /// </summary>
        public virtual bool CanceledOnOwnershipChange => true;

        /// <summary>
        /// Returns whether successful participants stay at the mission location regardless of ownership.
        /// </summary>
        internal virtual bool SuccessfulParticipantsRemainAtLocation => false;

        /// <summary>
        /// Returns why this mission must stop before advancing.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        public virtual MissionCompletionReason? GetAbortReason(GameRoot game) =>
            MainParticipants.Count == 0 || HaveMainParticipantsChanged()
                ? MissionCompletionReason.Failure
                : null;

        /// <summary>
        /// Returns whether the mission should repeat after completing one execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should repeat; false to finish the mission.</returns>
        public abstract bool ShouldRepeatAfterCompletion(GameRoot game);

        /// <summary>
        /// Produces mission-specific state changes when the mission ends before normal execution.
        /// The mission system remains responsible for participant teardown and terminal results.
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
        /// <returns>Combined list of main and decoy participants.</returns>
        public List<IMissionParticipant> GetAllParticipants() =>
            MainParticipants.Concat(DecoyParticipants).ToList();

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
            MainParticipants
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
            int specialForcesPenalty = MainParticipants.OfType<SpecialForces>().Count();
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
            if (MainParticipants.Count == 0)
                return 0;

            return MainParticipants.Sum(participant => participant.GetEffectiveRating(rating))
                / MainParticipants.Count;
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
        /// <param name="onParticipantSucceeded">Optional action applied immediately after each successful attempt.</param>
        /// <param name="stopAfterFirstSuccess">Whether resolution stops after the first successful attempt.</param>
        /// <returns>The participants whose attempts succeeded, in attempt order.</returns>
        protected internal List<IMissionParticipant> ResolveSuccessfulParticipants(
            IRandomNumberProvider provider,
            GameRoot game,
            Action<IMissionParticipant> onParticipantSucceeded = null,
            bool stopAfterFirstSuccess = false
        )
        {
            List<(Officer Participant, double Probability)> officerAttempts = MainParticipants
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
                onParticipantSucceeded?.Invoke(participant);
                if (stopAfterFirstSuccess)
                    return successfulParticipants;
            }

            foreach (SpecialForces specialForces in MainParticipants.OfType<SpecialForces>())
            {
                if (!RollParticipantSuccess(specialForces, provider, game))
                    continue;

                successfulParticipants.Add(specialForces);
                onParticipantSucceeded?.Invoke(specialForces);
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

            List<MissionDetector> detectors = new List<MissionDetector>();
            foreach (
                Starfighter starfighter in planet.GetChildren<Starfighter>(
                    IsEligibleDetector,
                    recurse: false
                )
            )
                detectors.Add(CreateDetector(planet, starfighter));

            foreach (
                Regiment regiment in planet.GetChildren<Regiment>(
                    IsEligibleDetector,
                    recurse: false
                )
            )
                detectors.Add(CreateDetector(planet, regiment));

            foreach (Fleet fleet in planet.GetChildren<Fleet>(_ => true, recurse: false))
            {
                foreach (CapitalShip capitalShip in fleet.CapitalShips)
                {
                    if (IsEligibleDetector(capitalShip))
                        detectors.Add(CreateDetector(planet, capitalShip));

                    foreach (
                        Starfighter starfighter in capitalShip.GetChildren<Starfighter>(
                            IsEligibleDetector,
                            recurse: false
                        )
                    )
                        detectors.Add(CreateDetector(planet, starfighter));

                    foreach (
                        Regiment regiment in capitalShip.GetChildren<Regiment>(
                            IsEligibleDetector,
                            recurse: false
                        )
                    )
                        detectors.Add(CreateDetector(planet, regiment));
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
                    ? fleet.GetChildren<Officer>(_ => true)
                    : planet
                        .GetChildren<Officer>(_ => true)
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
        /// Executes the mission and returns all generated results.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for all probability rolls.</param>
        /// <returns>All results produced by the outcome, with a MissionCompletedResult appended last.</returns>
        internal virtual List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
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
                if (!IsMissionSatisfied(game))
                {
                    outcome = MissionOutcome.Failed;
                    completionReason = MissionCompletionReason.TargetUnavailable;
                    results.AddRange(OnFailed(game, provider));
                }
                else
                {
                    outcome = MissionOutcome.Success;
                    completionReason = MissionCompletionReason.Success;
                    results.AddRange(OnSuccess(game, provider, successfulParticipants[0]));
                    foreach (IMissionParticipant participant in successfulParticipants)
                        ImproveMissionParticipantRating(participant);
                }
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
        /// Returns whether the mission can still complete successfully.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission target conditions are still valid; false to force a Failed outcome.</returns>
        protected virtual bool IsMissionSatisfied(GameRoot game) => true;

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
        /// <returns>All main and decoy participants as scene nodes.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            if (HasInitiated)
                return MainParticipants
                    .Cast<ISceneNode>()
                    .Concat(DecoyParticipants.Cast<ISceneNode>());

            return new List<ISceneNode>();
        }

        /// <summary>
        /// Only mission participants may be moved into a mission node.
        /// </summary>
        /// <param name="child">The node to test.</param>
        /// <returns>True if child is an IMissionParticipant.</returns>
        public override bool CanAcceptChild(ISceneNode child) => child is IMissionParticipant;

        /// <summary>
        /// Accepts mission participants already assigned to this mission.
        /// </summary>
        /// <param name="child">The node to add (ignored).</param>
        public override void AddChild(ISceneNode child) { }

        /// <summary>
        /// Removes the child from participant lists (called by GameRoot.MoveNode/DetachNode).
        /// </summary>
        /// <param name="child">The node to remove from participant lists.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is IMissionParticipant participant)
            {
                MainParticipants.Remove(participant);
                DecoyParticipants.Remove(participant);
            }
        }
    }
}
