using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class SabotageMission : Mission
    {
        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public SabotageMission()
            : base()
        {
            ConfigKey = "Sabotage";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        private SabotageMission(
            string ownerInstanceId,
            ISceneNode missionTarget,
            string sabotageTargetInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                "Sabotage",
                ownerInstanceId,
                missionTarget.GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat,
                null
            )
        {
            TargetInstanceID = sabotageTargetInstanceId;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        public static SabotageMission TryCreate(MissionContext ctx)
        {
            if (ctx.Target == null)
                return null;

            ISceneNode sabotageTarget = ctx.SpecificTarget ?? ctx.Target;
            if (sabotageTarget == null || sabotageTarget is Officer)
                return null;

            if (sabotageTarget is IManufacturable manufacturable)
            {
                if (manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building)
                    return null;
            }

            if (sabotageTarget is IMovable movable && movable.Movement != null)
                return null;

            Planet missionPlanet = ctx.Target as Planet ?? sabotageTarget.GetParentOfType<Planet>();
            if (missionPlanet == null)
                return null;

            return new SabotageMission(
                ctx.OwnerInstanceId,
                missionPlanet,
                sabotageTarget.GetInstanceID(),
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        public override bool CanStart(GameRoot game) => HasValidTarget(game);

        protected override bool IsMissionSatisfied(GameRoot game) => HasValidTarget(game);

        private bool HasValidTarget(GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(TargetInstanceID);
            if (target is Planet planet)
                return planet.GetAllBuildings().Count > 0;

            return target != null && target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Sabotage does not award mission skill improvements.
        /// </summary>
        protected override void ImproveMissionParticipantRatings() { }

        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            ISceneNode target = GetSabotageTarget(game);
            if (target == null)
                return new List<GameResult>();

            game.DetachNode(target);

            return new List<GameResult>
            {
                new GameObjectSabotagedResult
                {
                    SabotagedObject = target,
                    Saboteur = MainParticipants.Count > 0 ? MainParticipants[0] : null,
                    Context = planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        private ISceneNode GetSabotageTarget(GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(TargetInstanceID);
            if (target is not Planet planet)
                return target;

            List<Building> buildings = planet.GetAllBuildings();
            return buildings.Count > 0 ? buildings[0] : null;
        }

        /// <summary>
        /// Sabotage missions do not repeat — one attempt per mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool CanContinue(GameRoot game)
        {
            return false;
        }
    }
}
