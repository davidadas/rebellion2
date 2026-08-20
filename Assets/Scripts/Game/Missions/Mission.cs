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
        [PersistableMember(Name = "MainParticipants")]
        private protected List<IMissionParticipant> _mainParticipants;

        [PersistableMember(Name = "DecoyParticipants")]
        private protected List<IMissionParticipant> _decoyParticipants;

        [PersistableIgnore]
        private HashSet<string> _participantInstanceIds = new HashSet<string>(
            StringComparer.Ordinal
        );

        [PersistableIgnore]
        private bool _hasCapturedParticipantIds;

        // Mission configuration.
        public OfficerRating ParticipantRating { get; set; }
        public bool HasInitiated;

        // Mission progress.
        public int MaxProgress { get; set; }
        public int CurrentProgress { get; set; }

        [PersistableIgnore]
        public OfficerRating DecoyParticipantRating { get; set; }

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

            _mainParticipants = mainParticipants ?? new List<IMissionParticipant>();
            _decoyParticipants = decoyParticipants ?? new List<IMissionParticipant>();
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
            copy.OriginInstanceID = OriginInstanceID;
            copy.SourceEventInstanceID = SourceEventInstanceID;
            copy._participantInstanceIds = new HashSet<string>(
                _participantInstanceIds,
                StringComparer.Ordinal
            );
            copy._hasCapturedParticipantIds = _hasCapturedParticipantIds;
            copy.ParticipantRating = ParticipantRating;
            copy.HasInitiated = HasInitiated;
            copy.MaxProgress = MaxProgress;
            copy.CurrentProgress = CurrentProgress;
            copy.DecoyParticipantRating = DecoyParticipantRating;
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

            if (HasInitiated)
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
        /// Returns whether this mission is canceled when target ownership changes.
        /// </summary>
        public virtual bool CanceledOnOwnershipChange => true;

        /// <summary>
        /// Returns whether detected mission participants suffer capture, death, or destruction.
        /// </summary>
        internal virtual bool AppliesFoiledParticipantConsequences => true;

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
            _mainParticipants.Count == 0 || HaveParticipantsChanged()
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
            CaptureParticipantIDs();
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
        /// Captures the current mission participant IDs.
        /// </summary>
        private void CaptureParticipantIDs()
        {
            _participantInstanceIds = GetParticipantIDs();
            _hasCapturedParticipantIds = true;
        }

        /// <summary>
        /// Returns whether the mission participant list differs from mission start.
        /// </summary>
        /// <returns>True if a participant was added or removed.</returns>
        private bool HaveParticipantsChanged()
        {
            if (!_hasCapturedParticipantIds)
            {
                CaptureParticipantIDs();
                return false;
            }

            HashSet<string> currentParticipantIds = GetParticipantIDs();
            if (currentParticipantIds.Count != _participantInstanceIds.Count)
                return true;

            return currentParticipantIds.Any(id => !_participantInstanceIds.Contains(id));
        }

        /// <summary>
        /// Returns all current participant IDs.
        /// </summary>
        /// <returns>The current participant ID set.</returns>
        private HashSet<string> GetParticipantIDs() =>
            GetAllParticipants()
                .Where(participant => !string.IsNullOrEmpty(participant.InstanceID))
                .Select(participant => participant.InstanceID)
                .ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Returns the participant's mission success probability.
        /// </summary>
        /// <param name="agent">The participant whose rating is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's success probability.</returns>
        protected virtual double GetAgentProbability(IMissionParticipant agent, GameRoot game)
        {
            int score = agent.GetEffectiveRating(ParticipantRating);
            return LookupSuccessProbability(game, score);
        }

        /// <summary>
        /// Returns the sum of defense ratings of all enemy regiments on the target planet.
        /// </summary>
        /// <returns>Total defense rating, or 0 if no valid planet target.</returns>
        protected internal double GetDefenseScore()
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return 0;

            double score = 0;
            foreach (ISceneNode child in planet.GetChildren())
            {
                if (child is Regiment regiment && regiment.OwnerInstanceID != OwnerInstanceID)
                    score += regiment.DefenseRating;
            }
            return score;
        }

        /// <summary>
        /// Returns the decoy participant's success probability.
        /// </summary>
        /// <param name="decoy">The decoy participant to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The decoy success probability.</returns>
        protected double GetDecoyProbability(IMissionParticipant decoy, GameRoot game)
        {
            int bestDefenderEspionage = 0;
            if (GetParent() is Planet planet)
            {
                foreach (Officer officer in planet.GetChildren<Officer>())
                {
                    if (officer.OwnerInstanceID != OwnerInstanceID && !officer.IsCaptured)
                    {
                        int esp = officer.GetEffectiveRating(OfficerRating.Espionage);
                        if (esp > bestDefenderEspionage)
                            bestDefenderEspionage = esp;
                    }
                }
            }

            if (DecoyParticipantRating == OfficerRating.None)
                throw new InvalidOperationException(
                    $"{GetType().Name} cannot resolve a decoy check without a decoy participant rating."
                );

            int decoyEspionage = decoy.GetEffectiveRating(DecoyParticipantRating);
            int targetDefense = (int)GetDefenseScore();
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables(game);
            int scaledDefender =
                bestDefenderEspionage
                * missionTables.DecoyDefenderScalingPercent
                / _ratingPercentScale;
            int score = decoyEspionage - targetDefense - scaledDefender;
            return LookupProbability(missionTables.Decoy, score);
        }

        /// <summary>
        /// Returns the probability that enemy forces detect the mission.
        /// </summary>
        /// <param name="defenseScore">Sum of enemy regiment defense ratings on the target planet.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The foil probability.</returns>
        protected virtual double GetFoilProbability(double defenseScore, GameRoot game)
        {
            if (GetParent() is Planet planet && planet.OwnerInstanceID == OwnerInstanceID)
                return 0;

            Officer defender = FindDefender();
            if (defender == null)
                return 0;

            int defenderEspionage = defender.GetEffectiveRating(OfficerRating.Espionage);
            GameConfig.MissionProbabilityTablesConfig missionTables = GetMissionTables(game);
            int scaledDefender =
                defenderEspionage * missionTables.FoilDefenderScalingPercent / _ratingPercentScale;
            int supportRating = GetSupportRating(game);
            int score =
                GetAveragedRating(ParticipantRating)
                - scaledDefender
                - (int)defenseScore
                - supportRating
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
        /// Finds the first eligible enemy officer on the mission's target planet.
        /// Returns null if no eligible defender exists.
        /// </summary>
        /// <returns>A defending officer, or null.</returns>
        internal Officer FindDefender()
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return null;

            HashSet<string> participantIds = GetParticipantIDs();
            return planet
                .GetAllOfficers()
                .FirstOrDefault(o =>
                    o.GetOwnerInstanceID() != OwnerInstanceID
                    && !participantIds.Contains(o.InstanceID)
                    && !o.IsCaptured
                    && !o.IsKilled
                );
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
        /// Returns the best available defensive support rating for the mission location.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The support rating, or 0 when no support applies.</returns>
        private int GetSupportRating(GameRoot game)
        {
            ISceneNode location = ResolveLocation(game);
            string locationOwnerId = location?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(locationOwnerId))
                return 0;

            Planet planet = ResolveSupportPlanet(location);
            if (planet == null)
                return 0;

            return GetBestSupportRating(planet, locationOwnerId, GetContainerID(location));
        }

        /// <summary>
        /// Returns the live mission location used by detection support checks.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The live location node, or the mission parent when no location node is found.</returns>
        private ISceneNode ResolveLocation(GameRoot game)
        {
            return game?.GetSceneNodeByInstanceID<ISceneNode>(LocationInstanceID) ?? GetParent();
        }

        /// <summary>
        /// Returns the planet whose children can provide support for the location.
        /// </summary>
        /// <param name="location">The mission location.</param>
        /// <returns>The support planet, or null when none can be resolved.</returns>
        private Planet ResolveSupportPlanet(ISceneNode location)
        {
            if (location is Planet planet)
                return planet;

            return location?.GetParentOfType<Planet>() ?? GetParent() as Planet;
        }

        /// <summary>
        /// Returns the strongest support rating from candidates on the support planet.
        /// </summary>
        /// <param name="planet">The planet containing support candidates.</param>
        /// <param name="locationOwnerId">The faction that owns the location.</param>
        /// <param name="locationContainer">The location's containing node ID.</param>
        /// <returns>The selected support rating.</returns>
        private int GetBestSupportRating(
            Planet planet,
            string locationOwnerId,
            string locationContainer
        )
        {
            int sameContainerRating = 0;
            int otherContainerRating = 0;

            foreach (
                IMissionParticipant candidate in planet.GetChildren<IMissionParticipant>(
                    recursive: true
                )
            )
            {
                if (candidate.GetOwnerInstanceID() != locationOwnerId || !CanSupportFoil(candidate))
                    continue;

                int value = candidate.GetEffectiveRating(OfficerRating.Espionage);
                if (GetContainerID(candidate) == locationContainer)
                    sameContainerRating = Math.Max(sameContainerRating, value);
                else
                    otherContainerRating = Math.Max(otherContainerRating, value);
            }

            return sameContainerRating != 0 ? sameContainerRating : otherContainerRating;
        }

        /// <summary>
        /// Returns whether a participant can contribute defensive support to a detection check.
        /// </summary>
        /// <param name="candidate">The support candidate.</param>
        /// <returns>True when the candidate can support detection.</returns>
        private static bool CanSupportFoil(IMissionParticipant candidate)
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
        /// Returns the container ID used to group target and support candidate locations.
        /// </summary>
        /// <param name="node">The node to inspect.</param>
        /// <returns>The parent ID when present; otherwise the node ID or an empty string.</returns>
        private static string GetContainerID(ISceneNode node)
        {
            return node?.GetParent()?.GetInstanceID() ?? node?.GetInstanceID() ?? string.Empty;
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
        /// Returns whether any main participant succeeds.
        /// </summary>
        /// <param name="provider">RNG provider for rolling against the success probability.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True if at least one participant succeeds.</returns>
        protected bool CheckMissionSuccess(IRandomNumberProvider provider, GameRoot game)
        {
            foreach (IMissionParticipant participant in GetMainParticipants())
            {
                if (RollParticipantSuccess(participant, provider, game))
                    return true;
            }
            return false;
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
            double rolledValue = provider.NextDouble() * 100;
            return IsSuccessfulProbabilityRoll(rolledValue, successThreshold);
        }

        /// <summary>
        /// Picks one random decoy participant and rolls their decoy probability.
        /// Returns false if no decoys are assigned.
        /// </summary>
        /// <param name="provider">RNG provider for selection and probability roll.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the selected decoy succeeds.</returns>
        protected bool CheckDecoySuccessful(IRandomNumberProvider provider, GameRoot game)
        {
            if (GetDecoyParticipants().Count == 0)
                return false;

            IMissionParticipant decoy = GetDecoyParticipants()[
                provider.NextInt(0, GetDecoyParticipants().Count)
            ];
            return IsSuccessfulProbabilityRoll(
                provider.NextDouble() * 100,
                GetDecoyProbability(decoy, game)
            );
        }

        /// <summary>
        /// Rolls the mission detection check.
        /// </summary>
        /// <param name="provider">RNG provider for the foil roll.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission is detected this tick.</returns>
        internal bool RollFoilCheck(IRandomNumberProvider provider, GameRoot game)
        {
            double defenseScore = GetDefenseScore();
            double foilProbability = GetFoilProbability(defenseScore, game);

            if (foilProbability <= 0)
                return false;

            return IsSuccessfulProbabilityRoll(provider.NextDouble() * 100, foilProbability);
        }

        /// <summary>
        /// Rolls the decoy response check.
        /// </summary>
        /// <param name="provider">RNG provider for decoy rolls.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True if a decoy prevents capture.</returns>
        internal bool RollDecoyCheck(IRandomNumberProvider provider, GameRoot game)
        {
            return CheckDecoySuccessful(provider, game);
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

            if (CheckMissionSuccess(provider, game))
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
                    results.AddRange(OnSuccess(game, provider));
                    ImproveMissionParticipantRatings();
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
        /// Improves eligible mission participants' base ratings.
        /// </summary>
        protected virtual void ImproveMissionParticipantRatings()
        {
            foreach (
                IMissionParticipant participant in GetMainParticipants()
                    .Concat(GetDecoyParticipants())
            )
                ImproveMissionParticipantRating(participant);
        }

        /// <summary>
        /// Improves an eligible officer's base rating for this mission.
        /// </summary>
        /// <param name="participant">The successful mission participant.</param>
        internal void ImproveMissionParticipantRating(IMissionParticipant participant)
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
        /// <returns>Results produced by the success outcome; empty by default.</returns>
        protected virtual List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider
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
