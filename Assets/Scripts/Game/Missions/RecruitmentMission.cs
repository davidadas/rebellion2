using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to recruit an unrecruited officer.
    /// </summary>
    public class RecruitmentMission : Mission
    {
        public const string MissionTypeID = "Recruitment";

        /// <summary>
        /// Instance ID of the officer produced by the most recent successful recruitment attempt.
        /// The mission target itself is always the planet identified by LocationInstanceID.
        /// </summary>
        public string RecruitedOfficerInstanceID { get; set; }

        /// <summary>Creates an empty recruitment mission copy.</summary>
        /// <returns>An empty recruitment mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new RecruitmentMission();

        /// <summary>Copies recruitment-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((RecruitmentMission)destination).RecruitedOfficerInstanceID =
                RecruitedOfficerInstanceID;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public RecruitmentMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Leadership;
        }

        /// <summary>
        /// Initializes a recruitment mission at its target planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private RecruitmentMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                target.GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Leadership
            ) { }

        /// <summary>
        /// Returns a new RecruitmentMission when this faction has at least one recruitable officer on an owned planet.
        /// </summary>
        /// <param name="ctx">Mission context containing the target planet and participants.</param>
        /// <returns>A configured mission, or null if no unrecruited officers exist.</returns>
        public static RecruitmentMission TryCreate(MissionContext ctx)
        {
            if (
                ctx.Location is not Planet planet
                || planet.GetOwnerInstanceID() != ctx.OwnerInstanceId
            )
                return null;

            List<Officer> unrecruited = ctx.Game.GetUnrecruitedOfficers(ctx.OwnerInstanceId);
            if (!HasOnlyMainOfficerParticipants(ctx.MainParticipants))
                return null;

            if (unrecruited.Count == 0)
                return null;

            return new RecruitmentMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Returns whether every selected participant is a main officer.
        /// </summary>
        /// <param name="participants">Selected mission participants to validate.</param>
        /// <returns>True when at least one main officer was selected and no ineligible participants were selected.</returns>
        private static bool HasOnlyMainOfficerParticipants(List<IMissionParticipant> participants)
        {
            if (participants == null || participants.Count == 0)
                return false;

            return participants.All(participant => participant is Officer { IsMain: true });
        }

        /// <summary>
        /// Returns false when this faction no longer has an unrecruited officer available.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if at least one unrecruited officer is available.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            return
                GetParent() is Planet planet
                && planet.GetOwnerInstanceID() == OwnerInstanceID
                && game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0
                ? null
                : MissionCompletionReason.TargetUnavailable;
        }

        /// <summary>
        /// Returns the participant's raw recruitment score at the mission planet.
        /// </summary>
        /// <param name="agent">The participant whose leadership rating is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's raw recruitment score.</returns>
        protected override int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            if (!(GetParent() is Planet planet))
                return base.GetAgentScore(agent, game);

            int opposingSupport = planet.GetOpposingPopularSupport(OwnerInstanceID);
            return agent.GetEffectiveRating(OfficerRating.Leadership) - opposingSupport;
        }

        /// <summary>
        /// Attempts recruiters from lowest to highest success probability and stops when one
        /// successfully recruits a candidate.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for success and candidate-selection rolls.</param>
        /// <returns>The recruitment result followed by the terminal mission result.</returns>
        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            MissionCompletionReason? invalidationReason = GetMissionInvalidationReason(game);
            if (invalidationReason.HasValue)
                return BuildInvalidatedResults(game, provider, invalidationReason.Value);

            RecruitedOfficerInstanceID = null;
            List<IMissionParticipant> successfulParticipants = ResolveSuccessfulParticipants(
                provider,
                game,
                _ =>
                {
                    List<Officer> targets = game.GetUnrecruitedOfficers(OwnerInstanceID);
                    if (targets.Count == 0)
                        return false;

                    Officer recruitedOfficer = targets.RandomElement(provider);
                    RecruitedOfficerInstanceID = recruitedOfficer.InstanceID;
                    return true;
                },
                stopAfterFirstSuccess: true
            );

            List<GameResult> results;
            MissionOutcome outcome;
            MissionCompletionReason completionReason;
            if (!string.IsNullOrEmpty(RecruitedOfficerInstanceID))
            {
                outcome = MissionOutcome.Success;
                completionReason = MissionCompletionReason.Success;
                results = OnSuccess(game, provider, successfulParticipants[0]);
            }
            else
            {
                outcome = MissionOutcome.Failed;
                completionReason =
                    successfulParticipants.Count > 0
                        ? MissionCompletionReason.TargetUnavailable
                        : MissionCompletionReason.Failure;
                results = OnFailed(game, provider);
            }

            results.Add(BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        /// <summary>
        /// Transfers the recruited officer to this faction and moves them to the target planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider used during mission execution.</param>
        /// <param name="successfulParticipant">The participant whose recruitment attempt succeeded.</param>
        /// <returns>One OfficerRecruitedResult, or an empty list if the target or planet is missing.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return new List<GameResult>();

            Officer target = game.GetUnrecruitedOfficers(OwnerInstanceID)
                .FirstOrDefault(officer => officer.InstanceID == RecruitedOfficerInstanceID);
            if (target == null)
                return new List<GameResult>();

            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            target.OwnerInstanceID = OwnerInstanceID;
            game.RemoveUnrecruitedOfficer(target);
            game.AttachNode(target, planet);

            GameLogger.Log($"Recruited {target.GetDisplayName()} to {OwnerInstanceID}");

            return new List<GameResult>
            {
                new OfficerRecruitedResult
                {
                    Officer = target,
                    Faction = faction,
                    Planet = planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Returns true while there are still unrecruited officers available for this faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if at least one unrecruited officer is available for this faction.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
        }
    }
}
