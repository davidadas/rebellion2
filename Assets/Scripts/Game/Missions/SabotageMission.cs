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

        /// <summary>
        /// Initializes a sabotage mission with its selected target object.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="missionTarget">Planet where the mission occurs.</param>
        /// <param name="sabotageTargetInstanceId">Object selected as the sabotage target.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
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

        /// <summary>
        /// Returns a new SabotageMission if the target can be sabotaged.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and optional concrete target.</param>
        /// <returns>A configured mission, or null if the target is not eligible.</returns>
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

            if (
                ctx.SpecificTarget != null
                && sabotageTarget.GetParentOfType<Planet>() != missionPlanet
            )
                return null;

            return new SabotageMission(
                ctx.OwnerInstanceId,
                missionPlanet,
                sabotageTarget.GetInstanceID(),
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Resolves whether sabotage can execute after participants arrive.
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
        /// Returns false if the target planet has no buildings remaining before execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the planet still has at least one building.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return HasValidTarget(game);
        }

        /// <summary>
        /// Returns whether the selected sabotage target is still present at the mission planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the selected target can still be sabotaged.</returns>
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

        /// <summary>
        /// Destroys the first building on the target planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for sabotage).</param>
        /// <returns>One GameObjectSabotagedResult.</returns>
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

        /// <summary>
        /// Returns the concrete object that should be destroyed by the sabotage mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The selected target, or the first building on a planet target.</returns>
        private ISceneNode GetSabotageTarget(GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(TargetInstanceID);
            if (target is not Planet planet)
                return target;

            List<Building> buildings = planet.GetAllBuildings();
            return buildings.Count > 0 ? buildings[0] : null;
        }

        /// <summary>
        /// Sabotage missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }
}
