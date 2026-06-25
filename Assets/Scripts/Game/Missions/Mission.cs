using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
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

        public string TargetInstanceID { get; set; }
        public string OriginInstanceID { get; set; }

        // Participants.
        [PersistableIgnore]
        public List<IMissionParticipant> MainParticipants { get; set; }

        [PersistableIgnore]
        public List<IMissionParticipant> DecoyParticipants { get; set; }

        [PersistableIgnore]
        private HashSet<string> _participantInstanceIds = new HashSet<string>(
            StringComparer.Ordinal
        );

        [PersistableIgnore]
        private bool _hasCapturedParticipantIds;

        // Mission configuration.
        public OfficerRating ParticipantRating { get; set; }
        public bool HasInitiated;

        [PersistableIgnore]
        public ProbabilityTable SuccessProbabilityTable { get; set; }

        [PersistableIgnore]
        public ProbabilityTable DecoyProbabilityTable { get; set; }

        [PersistableIgnore]
        public ProbabilityTable FoilProbabilityTable { get; set; }

        [PersistableIgnore]
        public ProbabilityTable KillOrCaptureProbabilityTable { get; set; }

        [PersistableIgnore]
        public int DecoyDefenderScalingPercent { get; set; }

        [PersistableIgnore]
        public int FoilDefenderScalingPercent { get; set; }

        [PersistableIgnore]
        public int FoilFlatScoreAdjustment { get; set; }

        [PersistableIgnore]
        public int BaseTicks;

        [PersistableIgnore]
        public int SpreadTicks;

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
            MainParticipants = new List<IMissionParticipant>();
            DecoyParticipants = new List<IMissionParticipant>();
        }

        /// <summary>
        /// Initializes a mission with all required parameters.
        /// </summary>
        /// <param name="configKey">Mission configuration key.</param>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="targetInstanceId">Mission target instance ID.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="participantRating">Rating used by primary participants.</param>
        /// <param name="successProbabilityTable">Mission success probability table.</param>
        /// <param name="displayName">Display name to show for this mission.</param>
        protected Mission(
            string configKey,
            string ownerInstanceId,
            string targetInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            OfficerRating participantRating,
            ProbabilityTable successProbabilityTable,
            string displayName = null
        )
        {
            ConfigKey = configKey ?? throw new ArgumentNullException(nameof(configKey));
            DisplayName = displayName ?? configKey;
            AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId };

            OwnerInstanceID = ownerInstanceId;
            TargetInstanceID = targetInstanceId;

            MainParticipants = mainParticipants ?? new List<IMissionParticipant>();
            DecoyParticipants = decoyParticipants ?? new List<IMissionParticipant>();
            ParticipantRating = participantRating;

            SuccessProbabilityTable =
                successProbabilityTable
                ?? new ProbabilityTable(new Dictionary<int, int> { { 0, 50 } });
            DecoyProbabilityTable = new ProbabilityTable(new Dictionary<int, int> { { 0, 0 } });
            FoilProbabilityTable = new ProbabilityTable(new Dictionary<int, int> { { 0, 0 } });
        }

        /// <summary>
        /// Applies resolved probability tables and duration settings.
        /// </summary>
        /// <param name="successProbabilityTable">The mission success probability table.</param>
        /// <param name="decoyProbabilityTable">The decoy success probability table.</param>
        /// <param name="foilProbabilityTable">The mission foil probability table.</param>
        /// <param name="killOrCaptureProbabilityTable">The kill-or-capture probability table.</param>
        /// <param name="decoyDefenderScalingPercent">The defender scaling percentage for decoy checks.</param>
        /// <param name="foilDefenderScalingPercent">The defender scaling percentage for foil checks.</param>
        /// <param name="foilFlatScoreAdjustment">The flat score adjustment for foil checks.</param>
        /// <param name="baseTicks">The minimum mission duration.</param>
        /// <param name="spreadTicks">The random mission duration spread.</param>
        public virtual void Configure(
            ProbabilityTable successProbabilityTable,
            ProbabilityTable decoyProbabilityTable,
            ProbabilityTable foilProbabilityTable,
            ProbabilityTable killOrCaptureProbabilityTable,
            int decoyDefenderScalingPercent,
            int foilDefenderScalingPercent,
            int foilFlatScoreAdjustment,
            int baseTicks,
            int spreadTicks
        )
        {
            SuccessProbabilityTable = successProbabilityTable;
            DecoyProbabilityTable = decoyProbabilityTable;
            FoilProbabilityTable = foilProbabilityTable;
            KillOrCaptureProbabilityTable = killOrCaptureProbabilityTable;
            DecoyDefenderScalingPercent = decoyDefenderScalingPercent;
            FoilDefenderScalingPercent = foilDefenderScalingPercent;
            FoilFlatScoreAdjustment = foilFlatScoreAdjustment;
            BaseTicks = baseTicks;
            SpreadTicks = spreadTicks;
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
        /// Returns whether this mission is canceled when target ownership changes.
        /// </summary>
        public virtual bool CanceledOnOwnershipChange => true;

        /// <summary>
        /// Returns whether detected mission participants suffer capture, death, or destruction.
        /// </summary>
        internal virtual bool AppliesFoiledParticipantConsequences => true;

        /// <summary>
        /// Returns why this mission must stop before advancing.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        public virtual MissionCompletionReason? GetAbortReason(GameRoot game) =>
            MainParticipants.Count == 0 || HaveParticipantsChanged()
                ? MissionCompletionReason.Failure
                : null;

        /// <summary>
        /// Returns whether the mission should repeat after completing one execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should repeat; false to tear down and send participants home.</returns>
        public abstract bool ShouldRepeatAfterCompletion(GameRoot game);

        /// <summary>
        /// Starts the mission and chooses its duration.
        /// </summary>
        /// <param name="provider">RNG provider for rolling the duration spread.</param>
        public void Initiate(IRandomNumberProvider provider)
        {
            CurrentProgress = 0;
            MaxProgress = BaseTicks + provider.NextInt(0, SpreadTicks + 1);
            CaptureParticipantIds();
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
        /// Returns the configured mission duration values.
        /// </summary>
        /// <returns>The base duration and random duration spread.</returns>
        public int[] GetTickRange() => new int[] { BaseTicks, SpreadTicks };

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
        /// Captures the current mission participant IDs.
        /// </summary>
        private void CaptureParticipantIds()
        {
            _participantInstanceIds = GetParticipantIds();
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
                CaptureParticipantIds();
                return false;
            }

            HashSet<string> currentParticipantIds = GetParticipantIds();
            if (currentParticipantIds.Count != _participantInstanceIds.Count)
                return true;

            return currentParticipantIds.Any(id => !_participantInstanceIds.Contains(id));
        }

        /// <summary>
        /// Returns all current participant IDs.
        /// </summary>
        /// <returns>The current participant ID set.</returns>
        private HashSet<string> GetParticipantIds() =>
            GetAllParticipants()
                .Where(participant => !string.IsNullOrEmpty(participant.InstanceID))
                .Select(participant => participant.InstanceID)
                .ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Returns the participant's mission success probability.
        /// </summary>
        /// <param name="agent">The participant whose rating is evaluated.</param>
        /// <returns>The participant's success probability.</returns>
        protected virtual double GetAgentProbability(IMissionParticipant agent)
        {
            int score = agent.GetEffectiveRating(ParticipantRating);
            return SuccessProbabilityTable.Lookup(score);
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
        /// <returns>The decoy success probability.</returns>
        protected double GetDecoyProbability(IMissionParticipant decoy)
        {
            int bestDefenderEspionage = 0;
            if (GetParent() is Planet planet)
            {
                foreach (Officer officer in planet.Officers)
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
            int scaledDefender = bestDefenderEspionage * DecoyDefenderScalingPercent / 100;
            int score = decoyEspionage - targetDefense - scaledDefender;
            return DecoyProbabilityTable.Lookup(score);
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
            int scaledDefender =
                defenderEspionage * FoilDefenderScalingPercent / _ratingPercentScale;
            int supportRating = GetSupportRating(game);
            int score =
                GetAveragedRating(ParticipantRating)
                - scaledDefender
                - (int)defenseScore
                - supportRating
                - FoilFlatScoreAdjustment;
            return FoilProbabilityTable.Lookup(score);
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

            HashSet<string> participantIds = GetParticipantIds();
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
            if (MainParticipants.Count == 0)
                return 0;

            return MainParticipants.Sum(participant => participant.GetEffectiveRating(rating))
                / MainParticipants.Count;
        }

        /// <summary>
        /// Returns the best available defensive support rating for the mission target.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The support rating, or 0 when no support applies.</returns>
        private int GetSupportRating(GameRoot game)
        {
            ISceneNode target = ResolveTarget(game);
            string targetOwnerId = target?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId))
                return 0;

            Planet planet = ResolveSupportPlanet(target);
            if (planet == null)
                return 0;

            return GetBestSupportRating(planet, targetOwnerId, GetContainerId(target));
        }

        /// <summary>
        /// Returns the live mission target used by detection support checks.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The live target node, or the mission parent when no target node is found.</returns>
        private ISceneNode ResolveTarget(GameRoot game)
        {
            return game?.GetSceneNodeByInstanceID<ISceneNode>(TargetInstanceID) ?? GetParent();
        }

        /// <summary>
        /// Returns the planet whose children can provide support for the target.
        /// </summary>
        /// <param name="target">The mission target.</param>
        /// <returns>The support planet, or null when none can be resolved.</returns>
        private Planet ResolveSupportPlanet(ISceneNode target)
        {
            if (target is Planet planet)
                return planet;

            return target?.GetParentOfType<Planet>() ?? GetParent() as Planet;
        }

        /// <summary>
        /// Returns the strongest support rating from candidates on the support planet.
        /// </summary>
        /// <param name="planet">The planet containing support candidates.</param>
        /// <param name="targetOwnerId">The faction that owns the target.</param>
        /// <param name="targetContainer">The target's containing node ID.</param>
        /// <returns>The selected support rating.</returns>
        private int GetBestSupportRating(
            Planet planet,
            string targetOwnerId,
            string targetContainer
        )
        {
            int sameContainerRating = 0;
            int otherContainerRating = 0;

            foreach (
                IMissionParticipant candidate in planet.GetChildren<IMissionParticipant>(_ => true)
            )
            {
                if (candidate.GetOwnerInstanceID() != targetOwnerId || !CanSupportFoil(candidate))
                    continue;

                int value = candidate.GetEffectiveRating(OfficerRating.Espionage);
                if (GetContainerId(candidate) == targetContainer)
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
        private static string GetContainerId(ISceneNode node)
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
        /// <returns>True if at least one participant succeeds.</returns>
        protected bool CheckMissionSuccess(IRandomNumberProvider provider)
        {
            foreach (IMissionParticipant participant in MainParticipants)
            {
                double successThreshold = GetAgentProbability(participant);
                double rolledValue = provider.NextDouble() * 100;
                if (IsSuccessfulProbabilityRoll(rolledValue, successThreshold))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Picks one random decoy participant and rolls their decoy probability.
        /// Returns false if no decoys are assigned.
        /// </summary>
        /// <param name="provider">RNG provider for selection and probability roll.</param>
        /// <returns>True if the selected decoy succeeds.</returns>
        protected bool CheckDecoySuccessful(IRandomNumberProvider provider)
        {
            if (DecoyParticipants.Count == 0)
                return false;

            IMissionParticipant decoy = DecoyParticipants[
                provider.NextInt(0, DecoyParticipants.Count)
            ];
            return IsSuccessfulProbabilityRoll(
                provider.NextDouble() * 100,
                GetDecoyProbability(decoy)
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
        /// <returns>True if a decoy prevents capture.</returns>
        internal bool RollDecoyCheck(IRandomNumberProvider provider)
        {
            return CheckDecoySuccessful(provider);
        }

        /// <summary>
        /// Executes the mission and returns all generated results.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for all probability rolls.</param>
        /// <returns>All results produced by the outcome, with a MissionCompletedResult appended last.</returns>
        public virtual List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            MissionOutcome outcome;
            MissionCompletionReason completionReason;

            if (CheckMissionSuccess(provider))
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
                MissionName = DisplayName,
                MissionTypeID = TypeID,
                TargetName = (GetParent() as Planet)?.GetDisplayName() ?? string.Empty,
                Participants = participants ?? GetAllParticipants(),
                Outcome = outcome,
                CompletionReason = GetDefaultCompletionReason(outcome),
                CanContinue = ShouldRepeatAfterCompletion(game),
                Tick = game.CurrentTick,
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
            foreach (IMissionParticipant participant in MainParticipants.Concat(DecoyParticipants))
            {
                if (participant is Officer officer && participant.CanImproveMissionRating)
                    officer.IncrementBaseRating(ParticipantRating);
            }
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
        /// Returns extra movable units that should travel home with mission participants after a successful mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Additional units to return after successful mission completion.</returns>
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
