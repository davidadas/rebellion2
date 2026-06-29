using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that marks an unvisited planet as visited.
    /// </summary>
    public class ReconnaissanceMission : Mission
    {
        public const string MissionTypeID = "Reconnaissance";

        /// <summary>
        /// Returns whether this mission should cancel when the target planet changes owner.
        /// </summary>
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
        /// Returns a reconnaissance mission for an unvisited location planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, location, participants, and fog-of-war.</param>
        /// <returns>A configured mission, or null if the location is invalid.</returns>
        public static ReconnaissanceMission TryCreate(MissionContext ctx)
        {
            if (ctx?.Location is not Planet planet)
                return null;

            if (planet.GetOwnerInstanceID() == ctx.OwnerInstanceId)
                return null;

            if (planet.WasVisitedBy(ctx.OwnerInstanceId))
                return null;

            List<IMissionParticipant> mainParticipants =
                ctx.MainParticipants ?? new List<IMissionParticipant>();
            if (mainParticipants.Count == 0 || !mainParticipants.All(CanPerformReconnaissance))
                return null;

            List<IMissionParticipant> decoyParticipants =
                ctx.DecoyParticipants ?? new List<IMissionParticipant>();

            return new ReconnaissanceMission(
                ctx.OwnerInstanceId,
                planet,
                mainParticipants,
                decoyParticipants
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
        /// Returns whether a primary participant can perform reconnaissance.
        /// </summary>
        /// <param name="participant">The participant to evaluate.</param>
        /// <returns>True if the participant can perform reconnaissance.</returns>
        private static bool CanPerformReconnaissance(IMissionParticipant participant)
        {
            return participant?.CanPerformMission(MissionTypeID) == true;
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
        /// Reconnaissance is not interrupted by detection checks.
        /// </summary>
        /// <param name="defenseScore">Unused defense score.</param>
        /// <param name="game">Current game state.</param>
        /// <returns>Always zero.</returns>
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <summary>
        /// Resolves reconnaissance without a success roll.
        /// </summary>
        /// <param name="game">Current game state.</param>
        /// <param name="provider">RNG provider.</param>
        /// <returns>All results produced by the mission.</returns>
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();

            if (!IsMissionSatisfied(game))
            {
                results.Add(
                    BuildCompletedResult(
                        MissionOutcome.Failed,
                        MissionCompletionReason.TargetUnavailable,
                        game
                    )
                );
                return results;
            }

            results.AddRange(OnSuccess(game, provider));
            results.Add(
                BuildCompletedResult(MissionOutcome.Success, MissionCompletionReason.Success, game)
            );
            return results;
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
