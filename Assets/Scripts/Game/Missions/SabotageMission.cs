using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to destroy or damage a selected enemy target.
    /// </summary>
    public class SabotageMission : Mission
    {
        public const string MissionTypeID = "Sabotage";
        private const string _deathStarSabotageConfigKey = "DeathStarSabotage";

        /// <summary>
        /// Instance ID of the selected sabotage target.
        /// </summary>
        public string SabotageTargetInstanceID { get; set; }

        /// <summary>Creates an empty sabotage mission copy.</summary>
        /// <returns>An empty sabotage mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new SabotageMission();

        /// <summary>Copies sabotage-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((SabotageMission)destination).SabotageTargetInstanceID = SabotageTargetInstanceID;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public SabotageMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
        }

        /// <summary>
        /// Initializes a sabotage mission with its selected target object.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="missionPlanet">Planet where the mission occurs.</param>
        /// <param name="selectedTarget">Object selected as the sabotage target.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private SabotageMission(
            string ownerInstanceId,
            Planet missionPlanet,
            ISceneNode selectedTarget,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                missionPlanet.GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat
            )
        {
            SabotageTargetInstanceID = selectedTarget.GetInstanceID();
        }

        /// <summary>
        /// Returns a new SabotageMission if the target can be sabotaged.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and optional concrete target.</param>
        /// <returns>A configured mission, or null if the target is not eligible.</returns>
        public static SabotageMission TryCreate(MissionContext ctx)
        {
            if (ctx.Location == null)
                return null;

            ISceneNode selectedTarget = ctx.SelectedTarget ?? ctx.Location;
            if (selectedTarget is not IManufacturable)
                return null;

            if (!IsOperationalTarget(selectedTarget))
                return null;

            Planet missionPlanet =
                ctx.Location as Planet ?? selectedTarget.GetParentOfType<Planet>();
            if (missionPlanet == null)
                return null;

            if (
                ctx.SelectedTarget != null
                && selectedTarget.GetParentOfType<Planet>()?.InstanceID != missionPlanet.InstanceID
            )
                return null;

            return new SabotageMission(
                ctx.OwnerInstanceId,
                missionPlanet,
                selectedTarget,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Resolves whether sabotage can execute after participants arrive.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>TargetUnavailable when the target is no longer valid; otherwise null.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            return HasValidTarget(game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        /// <summary>
        /// Returns the participant's raw sabotage score from averaged espionage and combat.
        /// </summary>
        /// <param name="agent">The participant whose espionage and combat ratings are evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's raw sabotage score.</returns>
        protected override int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            return (
                    agent.GetEffectiveRating(OfficerRating.Espionage)
                    + agent.GetEffectiveRating(OfficerRating.Combat)
                ) / 2;
        }

        /// <summary>
        /// Returns the configured sabotage probability for the participant's raw score.
        /// Planet-destroying capital ships use their dedicated probability table.
        /// </summary>
        /// <param name="agent">The participant whose score is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The configured sabotage success probability.</returns>
        protected override double GetAgentProbability(IMissionParticipant agent, GameRoot game)
        {
            int? score = GetAgentScore(agent, game);
            if (!score.HasValue)
                return 0;

            ISceneNode target = GetSabotageTarget(game);
            if (target is not CapitalShip { CanDestroyPlanets: true })
                return LookupSuccessProbability(game, score.Value);

            Dictionary<int, int> table = game
                ?.Config
                ?.ProbabilityTables
                ?.Mission
                ?.DeathStarSabotage;
            if (table == null || table.Count == 0)
                return LookupSuccessProbability(game, score.Value);

            return LookupSuccessProbability(game, score.Value, _deathStarSabotageConfigKey);
        }

        /// <summary>
        /// Returns whether the selected sabotage target is still present at the mission planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the selected target can still be sabotaged.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(SabotageTargetInstanceID);
            if (target is not IManufacturable)
                return false;

            if (!IsOperationalTarget(target))
                return false;

            return target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Improves both ratings used by a successful officer's sabotage attempt.
        /// </summary>
        /// <param name="participant">The participant whose sabotage attempt succeeded.</param>
        internal override void ImproveMissionParticipantRating(IMissionParticipant participant)
        {
            if (participant is not Officer officer || !participant.CanImproveMissionRating)
                return;

            officer.IncrementBaseRating(OfficerRating.Espionage);
            officer.IncrementBaseRating(OfficerRating.Combat);
        }

        /// <summary>
        /// Destroys the selected sabotage target.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for sabotage).</param>
        /// <param name="successfulParticipant">The participant whose sabotage attempt succeeded.</param>
        /// <returns>One GameObjectSabotagedResult.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Planet planet = GetParent() as Planet;
            ISceneNode target = GetSabotageTarget(game);
            if (target == null)
                return new List<GameResult>();

            bool garrisonChanged =
                target is Regiment regiment
                && regiment.GetParent() == planet
                && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null;
            Fleet targetFleet = target is CapitalShip ? target.GetParentOfType<Fleet>() : null;
            game.DetachNode(target);
            if (targetFleet?.GetChildren<CapitalShip>().Count == 0)
                game.DetachNode(targetFleet);

            List<GameResult> results = new List<GameResult>
            {
                new GameObjectSabotagedResult
                {
                    SabotagedObject = target,
                    Saboteur = successfulParticipant,
                    Context = planet,
                    Tick = game.CurrentTick,
                },
            };
            if (garrisonChanged)
            {
                results.Add(
                    new PlanetGarrisonChangedResult { Planet = planet, Tick = game.CurrentTick }
                );
            }

            return results;
        }

        /// <summary>
        /// Returns the concrete object that should be destroyed by the sabotage mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The selected target.</returns>
        private ISceneNode GetSabotageTarget(GameRoot game)
        {
            return game.GetSceneNodeByInstanceID<ISceneNode>(SabotageTargetInstanceID);
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
