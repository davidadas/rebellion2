using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class ReconnaissanceMission : Mission
    {
        public const string MissionTypeID = "Reconnaissance";

        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public ReconnaissanceMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Espionage;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        /// <summary>
        /// Returns a reconnaissance mission for an unvisited target planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target, participants, and fog-of-war.</param>
        /// <returns>A configured mission, or null if the target is invalid.</returns>
        public static ReconnaissanceMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Target is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() == ctx.OwnerInstanceId)
                return null;

            if (planet.WasVisitedBy(ctx.OwnerInstanceId))
                return null;

            if (
                ctx.MainParticipants.Any()
                && !ctx
                    .MainParticipants.OfType<SpecialForces>()
                    .Any(sf => sf.AllowedMissionTypeIDs.Contains(MissionTypeID))
            )
                return null;

            return new ReconnaissanceMission(
                ctx.OwnerInstanceId,
                planet,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Initializes a reconnaissance mission for the selected planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private ReconnaissanceMission(
            string ownerInstanceId,
            Planet target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                "Reconnaissance",
                ownerInstanceId,
                target.GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Espionage
            )
        {
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        /// <summary>
        /// Returns true while the mission remains attached to a planet target.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission is still attached to a planet.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return GetParent() is Planet;
        }

        /// <summary>
        /// Reconnaissance does not award mission rating improvements.
        /// </summary>
        protected override void ImproveMissionParticipantRatings() { }

        /// <summary>
        /// Marks the target as visited for the mission owner.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider.</param>
        /// <returns>An empty result list.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return new List<GameResult>();

            planet.AddVisitor(OwnerInstanceID);

            return new List<GameResult>();
        }

        /// <summary>
        /// Reconnaissance missions do not repeat.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }
}
