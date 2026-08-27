using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to trigger an uprising on an enemy planet.
    /// </summary>
    public class InciteUprisingMission : Mission
    {
        public const string MissionTypeID = "InciteUprising";

        /// <summary>Creates an empty incite-uprising mission copy.</summary>
        /// <returns>An empty incite-uprising mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new InciteUprisingMission();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public InciteUprisingMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Incite Uprising";
            ParticipantRating = OfficerRating.Leadership;
        }

        /// <summary>
        /// Initializes an incite uprising mission for the selected planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private InciteUprisingMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Incite Uprising").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Leadership,
                displayName: "Incite Uprising"
            ) { }

        /// <summary>
        /// Returns a new InciteUprisingMission if the target is an enemy planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is neutral or owned by this faction.</returns>
        public static InciteUprisingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            string owner = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(owner) || owner == ctx.OwnerInstanceId)
                return null;

            return new InciteUprisingMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Returns the participant's raw score for inciting the target planet.
        /// </summary>
        /// <param name="agent">The participant whose leadership rating is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's raw incite-uprising score.</returns>
        protected override int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            Planet planet = GetMissionPlanet(game);
            if (planet == null)
                throw new InvalidOperationException(
                    "InciteUprisingMission must be attached to a Planet."
                );

            int leadershipSkill = agent.GetEffectiveRating(OfficerRating.Leadership);
            int enemySupport = planet.GetOpposingPopularSupport(OwnerInstanceID);
            return leadershipSkill - enemySupport;
        }

        /// <summary>
        /// Incite Uprising missions continue while the opposing faction still controls the target
        /// or the mission faction has troops present there.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True while the original mission executor would leave the task active.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            if (GetParent() is not Planet planet)
                return false;

            bool opposingFactionControlsPlanet =
                !string.IsNullOrEmpty(planet.OwnerInstanceID)
                && planet.OwnerInstanceID != OwnerInstanceID;
            bool missionFactionHasTroops = planet
                .GetAllRegiments()
                .Any(regiment =>
                    regiment.OwnerInstanceID == OwnerInstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                );
            return opposingFactionControlsPlanet || missionFactionHasTroops;
        }
    }
}
