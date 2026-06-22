using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class RescueMission : Mission
    {
        public string TargetOfficerInstanceID { get; set; }

        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public RescueMission()
            : base()
        {
            ConfigKey = "Rescue";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        private RescueMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                "Rescue",
                ownerInstanceId,
                RequirePlanetTarget(target, "Rescue").GetInstanceID(),
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

        public override bool CanStart(GameRoot game) => HasValidTarget(game);

        protected override bool IsMissionSatisfied(GameRoot game) => HasValidTarget(game);

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
        /// Rescue missions do not repeat — one attempt per mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool CanContinue(GameRoot game)
        {
            return false;
        }
    }
}
