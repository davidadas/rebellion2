using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to capture an enemy officer and return them with the mission team.
    /// </summary>
    public class AbductionMission : Mission
    {
        public const string MissionTypeID = "Abduction";

        /// <summary>
        /// Instance ID of the officer selected as the abduction target.
        /// </summary>
        public string TargetOfficerInstanceID { get; set; }

        /// <summary>Creates an empty abduction mission copy.</summary>
        /// <returns>An empty abduction mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new AbductionMission();

        /// <summary>Copies abduction-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((AbductionMission)destination).TargetOfficerInstanceID = TargetOfficerInstanceID;
        }

        /// <summary>
        /// Returns whether this mission should cancel when the target planet changes owner.
        /// </summary>
        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public AbductionMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
        }

        /// <summary>
        /// Initializes an abduction mission with its selected officer target.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="targetOfficerInstanceId">Officer selected as the abduction target.</param>
        private AbductionMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Abduction").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
        }

        /// <summary>
        /// Returns a new AbductionMission for the specified target officer, or null if the
        /// target is not a valid abduction target (not an enemy, already captured, wrong planet).
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and the target officer.</param>
        /// <returns>A configured mission, or null if the target is ineligible.</returns>
        public static AbductionMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            Officer target = ctx.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == ctx.OwnerInstanceId
                || target.IsCaptured
                || !IsOperationalTarget(target)
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new AbductionMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        /// <summary>
        /// Resolves whether abduction can execute after participants arrive.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>TargetUnavailable when the target is no longer valid; otherwise null.</returns>
        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            return HasValidTarget(game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        /// <summary>
        /// Returns false if the target officer has already been captured or has moved
        /// away from the mission's planet before execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the target is still free and on the mission planet.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return HasValidTarget(game);
        }

        /// <summary>
        /// Returns the attacker's raw combat advantage over the abduction target.
        /// </summary>
        /// <param name="agent">The participant attempting the abduction.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The raw combat advantage, or null when the target cannot be resolved.</returns>
        protected override int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return null;

            return agent.GetEffectiveRating(OfficerRating.Combat)
                - target.GetEffectiveRating(OfficerRating.Combat);
        }

        /// <summary>
        /// Returns whether the selected officer can still be abducted.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the target is still free at the mission planet.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return target?.IsCaptured == false
                && IsOperationalTarget(target)
                && target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Resolves every participant attempt while applying the capture operation immediately
        /// after each successful attempt, as in the original mission dispatcher.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for success, injury, and death rolls.</param>
        /// <returns>The abduction effects followed by the terminal mission result.</returns>
        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            bool targetAvailable = IsMissionSatisfied(game);
            bool targetKilled = false;
            List<IMissionParticipant> successfulParticipants = ResolveSuccessfulParticipants(
                provider,
                game,
                participant =>
                {
                    ImproveMissionParticipantRating(participant);
                    if (!targetAvailable || targetKilled)
                        return;

                    List<GameResult> attemptResults = OnSuccess(game, provider, participant);
                    results.AddRange(attemptResults);
                    targetKilled = attemptResults.Exists(result => result is OfficerKilledResult);
                }
            );

            MissionOutcome outcome;
            MissionCompletionReason completionReason;
            if (successfulParticipants.Count == 0)
            {
                outcome = MissionOutcome.Failed;
                completionReason = MissionCompletionReason.Failure;
                results.AddRange(OnFailed(game, provider));
            }
            else if (!targetAvailable)
            {
                outcome = MissionOutcome.Failed;
                completionReason = MissionCompletionReason.TargetUnavailable;
                results.AddRange(OnFailed(game, provider));
            }
            else
            {
                outcome = MissionOutcome.Success;
                completionReason = MissionCompletionReason.Success;
            }

            results.Add(BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        /// <summary>
        /// Applies the original capture injury check, then captures the target if they survive.
        /// Minor personnel can die from the injury; main characters cannot.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for injury and post-injury death rolls.</param>
        /// <param name="successfulParticipant">The participant whose abduction attempt succeeded.</param>
        /// <returns>The injury, death, and capture results produced by the attempt.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return new List<GameResult>();

            List<GameResult> results = new List<GameResult>();
            if (
                ApplyCaptureEvasionInjury(
                    target,
                    successfulParticipant,
                    GetParent() as Planet,
                    game,
                    provider,
                    results
                )
            )
                return results;

            target.IsCaptured = true;
            target.CaptorInstanceID = OwnerInstanceID;
            target.CanEscape = true;

            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    Context = GetParent() as Planet,
                    Tick = game.CurrentTick,
                }
            );
            return results;
        }

        /// <summary>
        /// Returns the abducted officer when the mission owner now holds them captive.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abducted officer when eligible to return with the mission group.</returns>
        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target?.IsCaptured == true && target.CaptorInstanceID == OwnerInstanceID)
                yield return target;
        }

        /// <summary>
        /// Abduction missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }
}
