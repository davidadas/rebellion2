using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to injure or kill an enemy officer.
    /// </summary>
    public class AssassinationMission : Mission
    {
        public const string MissionTypeID = "Assassination";

        /// <summary>
        /// Instance ID of the officer selected as the assassination target.
        /// </summary>
        public string TargetOfficerInstanceID { get; set; }

        /// <summary>Creates an empty assassination mission copy.</summary>
        /// <returns>An empty assassination mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new AssassinationMission();

        /// <summary>Copies assassination-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((AssassinationMission)destination).TargetOfficerInstanceID = TargetOfficerInstanceID;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public AssassinationMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
        }

        /// <summary>
        /// Initializes an assassination mission with its selected officer target.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="targetOfficerInstanceId">Officer selected as the assassination target.</param>
        private AssassinationMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Assassination").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
        }

        /// <summary>
        /// Returns a new AssassinationMission for the specified target officer, or null if the
        /// target is not a valid assassination target (not an enemy, captured, killed, wrong planet).
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and the target officer.</param>
        /// <returns>A configured mission, or null if the target is ineligible.</returns>
        public static AssassinationMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            Officer target = ctx.SelectedTarget as Officer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == ctx.OwnerInstanceId
                || target.IsCaptured
                || target.IsKilled
                || !IsOperationalTarget(target)
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new AssassinationMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        /// <summary>
        /// Resolves whether assassination can execute after participants arrive.
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
        /// Returns the attacker's raw combat advantage over the assassination target.
        /// </summary>
        /// <param name="agent">The participant attempting the assassination.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The raw combat advantage, or null when the target cannot be resolved.</returns>
        protected override int? GetAgentScore(IMissionParticipant agent, GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return null;

            return agent.GetEffectiveRating(OfficerRating.Combat)
                - target.GetEffectiveRating(OfficerRating.Combat);
        }

        /// <summary>
        /// Returns whether the selected officer can still be assassinated.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the target is alive, free, and at the mission planet.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return target?.IsKilled == false
                && !target.IsCaptured
                && IsOperationalTarget(target)
                && target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Resolves an assassination hit and reports success only when the target dies.
        /// Main characters survive the injury and therefore produce a failed mission report.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for all probability rolls.</param>
        /// <returns>The hit effects followed by the terminal mission result.</returns>
        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            MissionCompletionReason? invalidationReason = GetMissionInvalidationReason(game);
            if (invalidationReason.HasValue)
                return BuildInvalidatedResults(game, provider, invalidationReason.Value);

            List<GameResult> results = new List<GameResult>();
            MissionOutcome outcome = MissionOutcome.Failed;
            MissionCompletionReason completionReason = MissionCompletionReason.Failure;

            bool targetKilled = false;
            List<IMissionParticipant> successfulParticipants = ResolveSuccessfulParticipants(
                provider,
                game,
                participant =>
                {
                    if (targetKilled)
                        return true;

                    List<GameResult> attemptResults = OnSuccess(game, provider, participant);
                    results.AddRange(attemptResults);
                    if (attemptResults.Exists(result => result is OfficerKilledResult))
                        targetKilled = true;
                    return targetKilled;
                }
            );
            if (successfulParticipants.Count == 0)
            {
                results.AddRange(OnFailed(game, provider));
            }
            else if (targetKilled)
            {
                outcome = MissionOutcome.Success;
                completionReason = MissionCompletionReason.Success;
            }

            results.Add(BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        /// <summary>
        /// Applies assassination injury to the target. Only minor personnel receive the
        /// original post-injury death roll; main characters always survive the hit.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for injury dice and kill check.</param>
        /// <param name="successfulParticipant">The participant whose assassination attempt succeeded.</param>
        /// <returns>An OfficerInjuredResult and optionally an OfficerKilledResult.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return new List<GameResult>();

            List<GameResult> results = new List<GameResult>();
            Planet planet = GetParent() as Planet;

            int injury = RollInjury(game.Config.Assassination, provider);
            target.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
            results.Add(
                new OfficerInjuredResult
                {
                    Officer = target,
                    Severity = injury,
                    Tick = game.CurrentTick,
                }
            );

            if (RollPostInjuryDeath(target, provider, game.Config.Assassination.KillProbability))
            {
                results.Add(
                    new OfficerKilledResult
                    {
                        TargetOfficer = target,
                        Assassin = successfulParticipant,
                        Context = planet,
                        Tick = game.CurrentTick,
                    }
                );
            }

            return results;
        }

        /// <summary>
        /// Rolls the total injury from base + two random ranges.
        /// </summary>
        /// <param name="config">Assassination configuration.</param>
        /// <param name="provider">RNG provider.</param>
        /// <returns>Total injury amount.</returns>
        private static int RollInjury(
            GameConfig.AssassinationConfig config,
            IRandomNumberProvider provider
        )
        {
            return config.BaseInjury
                + provider.NextInt(0, config.PrimaryInjuryRange + 1)
                + provider.NextInt(0, config.SecondaryInjuryRange + 1);
        }

        /// <summary>
        /// Assassination missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }
}
