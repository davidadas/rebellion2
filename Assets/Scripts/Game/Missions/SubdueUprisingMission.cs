using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to reduce an uprising on a faction-owned planet.
    /// </summary>
    public class SubdueUprisingMission : Mission
    {
        public const string MissionTypeID = "SubdueUprising";

        /// <summary>Creates an empty subdue-uprising mission copy.</summary>
        /// <returns>An empty subdue-uprising mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new SubdueUprisingMission();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public SubdueUprisingMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Subdue Uprising";
            ParticipantRating = OfficerRating.Leadership;
        }

        /// <summary>
        /// Initializes a subdue uprising mission for the selected planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private SubdueUprisingMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Subdue Uprising").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Leadership,
                displayName: "Subdue Uprising"
            ) { }

        /// <summary>
        /// Returns a new SubdueUprisingMission if the target is an own planet in uprising, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is not owned by this faction or not in uprising.</returns>
        public static SubdueUprisingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (!planet.IsInUprising || planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            return new SubdueUprisingMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Extends base cancellation to also cancel when the uprising ends before execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            return GetParent() is Planet p && p.IsInUprising
                ? null
                : MissionCompletionReason.Failure;
        }

        /// <summary>
        /// Subdue Uprising missions are never foiled — they target own planets.
        /// </summary>
        /// <param name="detectorRating">The detector rating, unused because subdue uprising cannot be foiled.</param>
        /// <param name="defender">The defender, unused because subdue uprising cannot be foiled.</param>
        /// <param name="game">The current game state, unused because subdue uprising cannot be foiled.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(
            int detectorRating,
            Officer defender,
            GameRoot game
        ) => 0;

        /// <summary>
        /// Returns a participant's raw score for subduing the target uprising.
        /// </summary>
        /// <param name="agent">The participant attempting to subdue the uprising.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's raw subdue-uprising score.</returns>
        protected override int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            Planet planet = GetParent() as Planet;

            return agent.GetEffectiveRating(OfficerRating.Leadership)
                - planet.GetOpposingPopularSupport(OwnerInstanceID);
        }

        /// <summary>
        /// Subdue Uprising missions continue until the uprising has ended.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True while the owned target planet remains in uprising.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return !GetMissionInvalidationReason(game).HasValue
                && GetParent() is Planet planet
                && planet.GetOwnerInstanceID() == OwnerInstanceID;
        }
    }
}
