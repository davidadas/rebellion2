using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to free a captured friendly officer.
    /// </summary>
    public class RescueMission : Mission
    {
        public const string MissionTypeID = "Rescue";

        /// <summary>
        /// Instance ID of the officer selected as the rescue target.
        /// </summary>
        public string TargetOfficerInstanceID { get; set; }

        /// <summary>
        /// Returns whether this mission should cancel when the target planet changes owner.
        /// </summary>
        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public RescueMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        /// <summary>
        /// Initializes a rescue mission with its selected officer target.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="targetOfficerInstanceId">Officer selected as the rescue target.</param>
        private RescueMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Rescue").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        /// <summary>
        /// Returns a new RescueMission for the specified captured friendly officer, or null if the
        /// target is not a valid rescue target (not friendly, not captured, wrong planet).
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and the target officer.</param>
        /// <returns>A configured mission, or null if the target is ineligible.</returns>
        public static RescueMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Target is Planet planet))
                return null;

            Officer target = ctx.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() != ctx.OwnerInstanceId
                || !target.IsCaptured
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new RescueMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        /// <summary>
        /// Resolves whether rescue can execute after participants arrive.
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
        /// Returns false if the target officer is no longer captured or has moved
        /// away from the mission's planet before execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the target is still captured and on the mission planet.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return HasValidTarget(game);
        }

        /// <summary>
        /// Returns whether the selected officer can still be rescued.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the target remains captured at the mission planet.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            Officer captive = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return captive?.IsCaptured == true
                && captive.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Clears the captured state and captor from the rescued officer.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for rescue).</param>
        /// <returns>An OfficerCaptureStateResult and an OfficerRescuedResult, or an empty list if the target was already removed.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return new List<GameResult>();
            target.IsCaptured = false;
            target.CaptorInstanceID = null;
            target.CanEscape = false;

            return new List<GameResult>
            {
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = false,
                    Context = GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
                new OfficerRescuedResult
                {
                    Officer = target,
                    RescuingFaction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                    Location = GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Returns the rescued officer when they are free and belong to the mission owner.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The rescued officer when eligible to return with the mission group.</returns>
        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target?.IsCaptured == false && target.GetOwnerInstanceID() == OwnerInstanceID)
                yield return target;
        }

        /// <summary>
        /// Rescue missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }
}
