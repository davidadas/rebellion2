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

        public abstract Mission TryCreate(
            MissionStartRequest request,
            MissionDefinition definition
        );

        public virtual MissionCompletionReason? GetAbortReason(Mission mission, GameRoot game) =>
            null;

        public virtual bool IsMissionSatisfied(Mission mission, GameRoot game) => true;

        public virtual double GetAgentProbability(
            Mission mission,
            IMissionParticipant agent,
            GameRoot game
        ) => mission.GetDefaultAgentProbability(agent, game);

        public virtual double GetFoilProbability(
            Mission mission,
            double defenseScore,
            GameRoot game
        ) => mission.GetDefaultFoilProbability(defenseScore, game);

        public virtual MissionCompletionReason GetFailedCompletionReason(
            Mission mission,
            GameRoot game
        ) => MissionCompletionReason.Failure;

        public virtual List<GameResult> Execute(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        ) => mission.ExecuteDefault(game, provider);

        public virtual List<GameResult> OnSuccess(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        ) => new List<GameResult>();

        public virtual List<GameResult> OnFailed(
            Mission mission,
            GameRoot game,
            IRandomNumberProvider provider
        ) => new List<GameResult>();

        public virtual bool ShouldRepeatAfterCompletion(Mission mission, GameRoot game) => false;

        public virtual IEnumerable<IMovable> GetSuccessfulReturnPassengers(
            Mission mission,
            GameRoot game
        ) => Enumerable.Empty<IMovable>();

        protected static Planet RequirePlanet(ISceneNode target)
        {
            return target as Planet;
        }

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
