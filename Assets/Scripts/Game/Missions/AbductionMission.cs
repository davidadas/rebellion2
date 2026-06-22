using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class AbductionMission : Mission
    {
        public string TargetOfficerInstanceID { get; set; }

        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public AbductionMission()
            : base()
        {
            ConfigKey = "Abduction";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        private AbductionMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                "Abduction",
                ownerInstanceId,
                RequirePlanetTarget(target, "Abduction").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat,
                null
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        /// <summary>
        /// Returns a new AbductionMission for the specified target officer, or null if the
        /// target is not a valid abduction target (not an enemy, already captured, wrong planet).
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and the target officer.</param>
        /// <returns>A configured mission, or null if the target is ineligible.</returns>
        public static AbductionMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Target is Planet planet))
                return null;

            Officer target = ctx.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == ctx.OwnerInstanceId
                || target.IsCaptured
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new AbductionMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        public override bool CanStart(GameRoot game) => HasValidTarget(game);

        protected override bool IsMissionSatisfied(GameRoot game) => HasValidTarget(game);

        private bool HasValidTarget(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return target?.IsCaptured == false
                && target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Marks the target officer as captured and assigns the captor faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for abduction).</param>
        /// <returns>One OfficerCaptureStateResult, or an empty list if the target was already removed.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return new List<GameResult>();
            target.IsCaptured = true;
            target.CaptorInstanceID = OwnerInstanceID;
            target.CanEscape = true;

            return new List<GameResult>
            {
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    Context = GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Abduction missions do not repeat — one attempt per mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool CanContinue(GameRoot game)
        {
            return false;
        }
    }
}
