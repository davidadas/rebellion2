using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public abstract class MissionBehavior
    {
        public virtual bool CanceledOnOwnershipChange => true;
        public virtual bool AppliesFoiledParticipantConsequences => true;
        public virtual bool ImprovesParticipantRatings => true;

        /// <summary>
        /// Creates a runtime mission from a validated start request.
        /// </summary>
        /// <param name="request">The mission start request.</param>
        /// <param name="definition">The definition for the requested mission type.</param>
        /// <returns>The created mission, or null when the request cannot create this mission.</returns>
        public abstract Mission TryCreate(
            MissionStartRequest request,
            MissionDefinition definition
        );

        /// <summary>
        /// Returns why a running mission must stop before advancing.
        /// </summary>
        /// <param name="mission">The mission being checked.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The completion reason that stops the mission, or null when it may continue.</returns>
        public virtual MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game) =>
            null;

        /// <summary>
        /// Returns whether the mission still has a valid target.
        /// </summary>
        /// <param name="mission">The mission being checked.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the mission can resolve against its current target.</returns>
        public virtual bool IsMissionSatisfied(Mission mission, GameRoot game) => true;

        /// <summary>
        /// Returns the success probability for one mission participant.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="agent">The participant being evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant success probability.</returns>
        public virtual double GetAgentProbability(
            Mission mission,
            IMissionParticipant agent,
            GameRoot game
        ) => mission.GetDefaultAgentProbability(agent, game);

        /// <summary>
        /// Returns the probability that opposing forces detect the mission.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="defenseScore">The defensive score applied to the mission.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The detection probability.</returns>
        public virtual double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => mission.GetDefaultFoilProbability(defenseScore, game);

        /// <summary>
        /// Returns the completion reason used for a failed mission attempt.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The failed completion reason.</returns>
        public virtual MissionCompletionReason GetFailedCompletionReason(
            Mission mission,
            GameRoot game
        ) => MissionCompletionReason.Failure;

        /// <summary>
        /// Resolves one completed mission execution.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used by mission rolls.</param>
        /// <returns>The results produced by the mission execution.</returns>
        public virtual List<GameResult> Execute(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        ) => mission.ExecuteDefault(game, provider);

        /// <summary>
        /// Applies mission-specific effects for a successful mission attempt.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used by mission rolls.</param>
        /// <returns>The results produced by the success effects.</returns>
        public virtual List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        ) => new List<GameResult>();

        /// <summary>
        /// Applies mission-specific effects for a failed mission attempt.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used by mission rolls.</param>
        /// <returns>The results produced by the failure effects.</returns>
        public virtual List<GameResult> OnFailed(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        ) => new List<GameResult>();

        /// <summary>
        /// Returns whether the mission should start another execution after completing.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the mission should remain active.</returns>
        public virtual bool ShouldRepeatAfterCompletion(Mission mission, GameRoot game) => false;

        /// <summary>
        /// Returns additional units that should return home with successful mission participants.
        /// </summary>
        /// <param name="mission">The mission being resolved.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The additional return passengers.</returns>
        public virtual IEnumerable<IMovable> GetSuccessfulReturnPassengers(
            Mission mission,
            GameRoot game
        ) => Enumerable.Empty<IMovable>();

        /// <summary>
        /// Returns the target when it is a planet.
        /// </summary>
        /// <param name="target">The requested mission target.</param>
        /// <returns>The planet target, or null when the target is not a planet.</returns>
        protected static Planet RequirePlanet(ISceneNode target)
        {
            return target as Planet;
        }

        /// <summary>
        /// Creates a mission attached to a planet target.
        /// </summary>
        /// <param name="definition">The definition for the mission type.</param>
        /// <param name="ownerInstanceID">The faction that owns the mission.</param>
        /// <param name="target">The planet targeted by the mission.</param>
        /// <param name="mainParticipants">The primary mission participants.</param>
        /// <param name="decoyParticipants">The decoy mission participants.</param>
        /// <returns>The created mission.</returns>
        protected static Mission CreatePlanetMission(
            MissionDefinition definition,
            string ownerInstanceID,
            Planet target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            return new Mission(
                definition,
                ownerInstanceID,
                target.GetInstanceID(),
                mainParticipants,
                decoyParticipants
            );
        }
    }
}
