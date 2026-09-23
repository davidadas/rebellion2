using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that attempts to capture an enemy officer and return them with the mission team.
    /// </summary>
    public class AbductionMission : Mission
    {
        public const string MissionTypeID = "Abduction";

        /// <summary>
        /// Instance ID of the officer selected as the abduction target.
        /// </summary>
        public string TargetOfficerInstanceID { get; set; }

        /// <summary>Creates an empty abduction mission copy.</summary>
        /// <returns>An empty abduction mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new AbductionMission();

        /// <summary>Copies abduction-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((AbductionMission)destination).TargetOfficerInstanceID = TargetOfficerInstanceID;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public AbductionMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = SkillRating.Combat;
        }

        /// <summary>
        /// Initializes an abduction mission with its selected officer target.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="targetOfficerInstanceId">Officer selected as the abduction target.</param>
        private AbductionMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Abduction").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                SkillRating.Combat
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
        }

        /// <summary>
        /// Returns a new AbductionMission for the specified target officer, or null if the
        /// target is not a valid abduction target (not an enemy, already captured, wrong planet).
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and the target officer.</param>
        /// <returns>A configured mission, or null if the target is ineligible.</returns>
        public static AbductionMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            Officer target = ctx.SelectedTarget as Officer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == ctx.OwnerInstanceId
                || target.IsCaptured
                || !IsOperationalTarget(target)
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new AbductionMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        /// <summary>
        /// Resolves whether abduction can execute after participants arrive.
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
        /// Returns the attacker's raw combat advantage over the abduction target.
        /// </summary>
        /// <param name="agent">The participant attempting the abduction.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The raw combat advantage, or null when the target cannot be resolved.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            Officer target =
                context.Target is Officer observedOfficer
                && observedOfficer.InstanceID == TargetOfficerInstanceID
                    ? observedOfficer
                    : context.Game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return null;

            return agent.GetEffectiveRating(SkillRating.Combat)
                - target.GetEffectiveRating(SkillRating.Combat);
        }

        /// <summary>
        /// Returns whether the selected officer can still be abducted.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the target is still free at the mission planet.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return target?.IsCaptured == false
                && IsOperationalTarget(target)
                && target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Resolves every participant attempt while applying the capture operation immediately
        /// after each successful attempt, as in the original mission dispatcher.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for success, injury, and death rolls.</param>
        /// <returns>The abduction effects followed by the terminal mission result.</returns>
        internal override List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
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
                    targetKilled = attemptResults.Exists(result => result is OfficerKilledResult);
                    return true;
                }
            );

            MissionOutcome outcome;
            MissionCompletionReason completionReason;
            if (successfulParticipants.Count == 0)
            {
                outcome = MissionOutcome.Failed;
                completionReason = MissionCompletionReason.Failure;
                results.AddRange(OnFailed(game, provider));
            }
            else
            {
                outcome = MissionOutcome.Success;
                completionReason = MissionCompletionReason.Success;
            }

            results.Add(BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        /// <summary>
        /// Applies the original capture injury check, then captures the target if they survive.
        /// Minor personnel can die from the injury; main characters cannot.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for injury and post-injury death rolls.</param>
        /// <param name="successfulParticipant">The participant whose abduction attempt succeeded.</param>
        /// <returns>The injury, death, and capture results produced by the attempt.</returns>
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
            if (
                ApplyCaptureEvasionInjury(
                    target,
                    successfulParticipant,
                    GetParent() as Planet,
                    game,
                    provider,
                    results
                )
            )
                return results;

            if (!target.TryCapture(OwnerInstanceID))
                return results;

            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    CapturingUnit = successfulParticipant,
                    Context = GetParent() as Planet,
                    Tick = game.CurrentTick,
                }
            );
            return results;
        }

        /// <summary>
        /// Returns the abducted officer when the mission owner now holds them captive.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abducted officer when eligible to return with the mission group.</returns>
        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target?.IsCaptured == true && target.CaptorInstanceID == OwnerInstanceID)
                yield return target;
        }

        /// <summary>
        /// Abduction missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }

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
            ParticipantRating = SkillRating.Combat;
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
                SkillRating.Combat
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
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The raw combat advantage, or null when the target cannot be resolved.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            Officer target = GetEvaluationTarget(context);
            if (target == null)
                return null;

            return agent.GetEffectiveRating(SkillRating.Combat)
                - target.GetEffectiveRating(SkillRating.Combat);
        }

        /// <summary>
        /// Includes the post-hit death roll required for an assassination to report success.
        /// </summary>
        /// <param name="participants">The participants attempting the assassination.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The probability that at least one participant both hits and kills the target.</returns>
        protected override double GetObjectiveSuccessProbability(
            IEnumerable<IMissionParticipant> participants,
            MissionEvaluationContext context
        )
        {
            Officer target = GetEvaluationTarget(context);
            int killProbability = GetPostInjuryDeathProbability(
                target,
                context.Game?.Config?.Assassination?.KillProbability ?? 0
            );
            if (killProbability == 0)
                return 0;

            IEnumerable<double> probabilities = (
                participants ?? Enumerable.Empty<IMissionParticipant>()
            )
                .Where(participant => participant != null)
                .Select(participant =>
                    GetAgentProbability(participant, context) * killProbability / 100d
                );
            return CombineSuccessProbabilities(probabilities);
        }

        /// <summary>
        /// Resolves the assassination target from observed state when available, otherwise from
        /// the authoritative game state.
        /// </summary>
        /// <param name="context">The state available while evaluating the mission.</param>
        /// <returns>The matching target officer, or null when the target cannot be resolved.</returns>
        private Officer GetEvaluationTarget(MissionEvaluationContext context)
        {
            return
                context.Target is Officer observedOfficer
                && observedOfficer.InstanceID == TargetOfficerInstanceID
                ? observedOfficer
                : context.Game?.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
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
        internal override List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
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
                    new OfficerAssassinatedResult
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

    /// <summary>
    /// Mission that shifts popular support on an owned or neutral planet.
    /// </summary>
    public class DiplomacyMission : Mission
    {
        public const string MissionTypeID = "Diplomacy";

        /// <summary>
        /// Returns whether successful participants remain on the target planet.
        /// </summary>
        internal override bool SuccessfulParticipantsRemainAtLocation => true;

        /// <summary>Creates an empty diplomacy mission copy.</summary>
        /// <returns>An empty diplomacy mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new DiplomacyMission();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public DiplomacyMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = SkillRating.Diplomacy;
        }

        /// <summary>
        /// Initializes a diplomacy mission for the selected planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private DiplomacyMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Diplomacy").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                SkillRating.Diplomacy
            ) { }

        /// <summary>
        /// Returns a new DiplomacyMission if the target is a valid planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is ineligible.</returns>
        public static DiplomacyMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (
                !planet.IsColonized
                || planet.IsInUprising
                || !planet.WasVisitedBy(ctx.OwnerInstanceId)
                || planet.GetPopularSupport(ctx.OwnerInstanceId) >= 100
            )
                return null;

            string planetOwner = planet.GetOwnerInstanceID();
            if (planetOwner != null && planetOwner != ctx.OwnerInstanceId)
                return null;

            return new DiplomacyMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Extends base cancellation to also cancel when the target planet enters uprising or
        /// is taken by a third faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            if (GetParent() is Planet planet)
            {
                if (planet.IsInUprising)
                    return MissionCompletionReason.Failure;
                string owner = planet.GetOwnerInstanceID();
                if (owner != null && owner != OwnerInstanceID)
                    return MissionCompletionReason.Failure;
            }
            return null;
        }

        /// <summary>
        /// Returns the participant's raw diplomacy mission score for the current target.
        /// </summary>
        /// <param name="agent">The participant whose diplomacy rating is evaluated.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The participant's raw diplomacy mission score.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            Planet planet = GetMissionPlanet(context);
            if (planet == null)
                return base.GetAgentScore(agent, context);

            return agent.GetEffectiveRating(SkillRating.Diplomacy)
                - planet.GetOpposingPopularSupport(OwnerInstanceID);
        }

        /// <summary>
        /// Reports diplomacy popular support movement for planetary control to apply.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for configured support rolls.</param>
        /// <param name="successfulParticipant">The participant whose diplomacy attempt succeeded.</param>
        /// <returns>The requested popular-support shift.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return new List<GameResult>();

            GameConfig.SupportShiftConfig config = game.Config.SupportShift;
            int supportShift = GetFactionSupportShift(planet, config, provider);
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            return new List<GameResult>
            {
                new PopularSupportShiftResult
                {
                    Planet = planet,
                    Faction = faction,
                    Shift = supportShift,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Returns the rolled support shift for an owned or neutral diplomacy target.
        /// </summary>
        /// <param name="planet">The mission target planet.</param>
        /// <param name="config">Support shift configuration values.</param>
        /// <param name="provider">RNG provider for configured support rolls.</param>
        /// <returns>The support shift to apply to the mission faction.</returns>
        private int GetFactionSupportShift(
            Planet planet,
            GameConfig.SupportShiftConfig config,
            IRandomNumberProvider provider
        )
        {
            string planetOwner = planet.GetOwnerInstanceID();
            if (planetOwner == OwnerInstanceID)
            {
                return RollSupportShift(
                    config.DiplomacyOwnedPlanetSupportBase,
                    config.DiplomacyOwnedPlanetSupportRange,
                    provider
                );
            }

            if (string.IsNullOrEmpty(planetOwner))
            {
                return RollSupportShift(
                    config.DiplomacyNeutralPlanetSupportBase,
                    config.DiplomacyNeutralPlanetSupportRange,
                    provider
                );
            }

            return 0;
        }

        /// <summary>
        /// Rolls a support shift from the configured base value and random range.
        /// </summary>
        /// <param name="baseShift">The configured base support shift.</param>
        /// <param name="range">The configured random support range.</param>
        /// <param name="provider">RNG provider for the support roll.</param>
        /// <returns>The rolled support shift.</returns>
        private static int RollSupportShift(
            int baseShift,
            int range,
            IRandomNumberProvider provider
        )
        {
            return baseShift + (range > 0 ? provider.NextInt(0, range + 1) : 0);
        }

        /// <summary>
        /// Returns true while the planet remains eligible for further diplomacy attempts.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the planet is owned or neutral, not in uprising, and support is below 100.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            if (GetParent() is Planet planet)
            {
                return (
                        planet.GetOwnerInstanceID() == GetOwnerInstanceID()
                        || planet.GetOwnerInstanceID() == null
                    )
                    && planet.GetPopularSupport(GetOwnerInstanceID()) < 100
                    && !planet.IsInUprising;
            }
            return false;
        }

        /// <summary>
        /// Returns whether diplomacy remains eligible after applying its pending support shift.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="objectiveResults">The objective results awaiting system processing.</param>
        /// <returns>True when another diplomacy attempt remains eligible.</returns>
        protected override bool ShouldRepeatAfterCompletion(
            GameRoot game,
            IReadOnlyList<GameResult> objectiveResults
        )
        {
            if (GetParent() is not Planet planet)
                return false;

            int pendingShift =
                objectiveResults
                    ?.OfType<PopularSupportShiftResult>()
                    .Where(result =>
                        result.Planet == planet && result.Faction?.InstanceID == OwnerInstanceID
                    )
                    .Sum(result => result.Shift)
                ?? 0;
            return (
                    planet.GetOwnerInstanceID() == OwnerInstanceID
                    || planet.GetOwnerInstanceID() == null
                )
                && planet.GetPopularSupport(OwnerInstanceID) + pendingShift < 100
                && !planet.IsInUprising;
        }
    }

    /// <summary>
    /// Mission that refreshes fog-of-war information for a visited planet.
    /// </summary>
    public class EspionageMission : Mission
    {
        public const string MissionTypeID = "Espionage";

        /// <summary>Creates an empty espionage mission copy.</summary>
        /// <returns>An empty espionage mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new EspionageMission();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public EspionageMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = SkillRating.Espionage;
        }

        /// <summary>
        /// Initializes an espionage mission for the selected planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private EspionageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Espionage").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                SkillRating.Espionage
            ) { }

        /// <summary>
        /// Returns a new EspionageMission if the target is a visited planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and fog-of-war.</param>
        /// <returns>A configured mission, or null if the planet has not been visited.</returns>
        public static EspionageMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (!planet.WasVisitedBy(ctx.OwnerInstanceId))
                return null;

            return new EspionageMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Improves the successful officer's espionage rating when operating against another faction.
        /// </summary>
        /// <param name="participant">The participant whose espionage attempt succeeded.</param>
        internal override void ImproveMissionParticipantRating(IMissionParticipant participant)
        {
            if (CanImproveRatingsAgainstTarget())
                base.ImproveMissionParticipantRating(participant);
        }

        /// <summary>
        /// Returns whether this mission target allows participant rating improvement.
        /// </summary>
        /// <returns>True when the target planet is owned by another faction.</returns>
        private bool CanImproveRatingsAgainstTarget()
        {
            return GetParent() is Planet planet
                && !string.IsNullOrEmpty(planet.GetOwnerInstanceID())
                && planet.GetOwnerInstanceID() != OwnerInstanceID;
        }

        /// <summary>
        /// Captures full intelligence for the target planet and any bonus planets.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider used to select bonus planets.</param>
        /// <param name="successfulParticipant">The participant whose espionage attempt succeeded.</param>
        /// <returns>A result identifying any additional planets revealed by the mission.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Planet planet = GetParent() as Planet;
            Faction faction = game?.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSector sector = planet?.GetParentOfType<PlanetSector>();

            if (game == null || faction == null || planet == null || sector == null)
                return new List<GameResult>();

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(faction, planet, sector, game.CurrentTick);

            List<Planet> additionalPlanets = new List<Planet>();
            if (!IsOpposingFactionPlanet(game, planet))
                return new List<GameResult>();

            foreach (Planet bonusPlanet in SelectBonusPlanets(game, provider, planet, sector))
            {
                PlanetSector bonusSector = bonusPlanet.GetParentOfType<PlanetSector>();
                recorder.RecordEspionageSnapshot(
                    faction,
                    bonusPlanet,
                    bonusSector,
                    game.CurrentTick
                );

                additionalPlanets.Add(bonusPlanet);
            }

            if (additionalPlanets.Count == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new PlanetsRevealedResult
                {
                    Tick = game.CurrentTick,
                    MissionInstanceID = InstanceID,
                    SourceEventInstanceID = SourceEventInstanceID,
                    AdditionalPlanets = additionalPlanets,
                },
            };
        }

        /// <summary>
        /// Returns whether the target belongs to a faction other than the mission owner.
        /// Neutral and owner-controlled planets still produce their direct intelligence snapshot,
        /// but do not grant the original game's additional-system bonus.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when the opposing faction planet condition is met; otherwise false.</returns>
        private bool IsOpposingFactionPlanet(GameRoot game, Planet targetPlanet)
        {
            if (string.IsNullOrEmpty(targetPlanet?.OwnerInstanceID))
                return false;

            return targetPlanet.OwnerInstanceID != OwnerInstanceID
                && game.GetFactionByOwnerInstanceID(targetPlanet.OwnerInstanceID) != null;
        }

        /// <summary>
        /// Selects distinct bonus planets using the mission's target-specific pools.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <param name="targetSector">The target sector.</param>
        /// <returns>The selected bonus planets.</returns>
        private IEnumerable<Planet> SelectBonusPlanets(
            GameRoot game,
            IRandomNumberProvider provider,
            Planet targetPlanet,
            PlanetSector targetSector
        )
        {
            if (targetSector.SectorType != PlanetSectorType.Core)
                return Enumerable.Empty<Planet>();

            GameConfig.EspionageConfig config =
                game.Config?.Espionage ?? new GameConfig.EspionageConfig();
            GameConfig.RandomCountConfig countConfig = config.CoreSectorBonus;
            bool includeOuterRim = false;

            if (IsOpposingHeadquartersTarget(game, targetPlanet))
            {
                countConfig = config.HeadquartersBonus;
                includeOuterRim = true;
            }

            List<Planet> candidates = game
                .Galaxy.GetChildren<PlanetSector>()
                .Where(sector => includeOuterRim || sector.SectorType == PlanetSectorType.Core)
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Where(candidate => candidate != targetPlanet)
                .Where(candidate => candidate.OwnerInstanceID == targetPlanet.OwnerInstanceID)
                .ToList();
            int count = countConfig.Base;
            if (countConfig.Spread > 0)
                count += provider.NextInt(0, countConfig.Spread);

            List<Planet> selected = new List<Planet>();
            while (selected.Count < count && candidates.Count > 0)
            {
                int index = provider.NextInt(0, candidates.Count);
                selected.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return selected;
        }

        /// <summary>
        /// Returns whether the target is currently another faction's headquarters planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>True when the opposing headquarters target condition is met; otherwise false.</returns>
        private bool IsOpposingHeadquartersTarget(GameRoot game, Planet targetPlanet)
        {
            Faction owner = game.GetFactionByOwnerInstanceID(targetPlanet.OwnerInstanceID);
            return owner != null
                && owner.InstanceID != OwnerInstanceID
                && owner.HQInstanceID == targetPlanet.InstanceID;
        }

        /// <summary>
        /// Espionage missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }

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
            ParticipantRating = SkillRating.Leadership;
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
                SkillRating.Leadership,
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
            if (string.IsNullOrEmpty(owner) || owner == ctx.OwnerInstanceId || planet.IsInUprising)
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
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The participant's raw incite-uprising score.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            Planet planet = GetMissionPlanet(context);
            if (planet == null)
                throw new InvalidOperationException(
                    "InciteUprisingMission must be attached to a Planet."
                );

            int leadershipSkill = agent.GetEffectiveRating(SkillRating.Leadership);
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

    /// <summary>
    /// Jedi training mission where a trainer trains a student in the Force.
    /// </summary>
    public class JediTrainingMission : Mission
    {
        public const string MissionTypeID = "JediTraining";

        /// <summary>
        /// Instance ID of the officer selected as the trainer.
        /// </summary>
        public string TrainerInstanceID { get; set; }

        /// <summary>
        /// Gets the selected trainer from the mission's current participants.
        /// </summary>
        [PersistableIgnore]
        public Officer Trainer =>
            GetMainParticipants()
                .OfType<Officer>()
                .FirstOrDefault(officer => officer.InstanceID == TrainerInstanceID);

        /// <summary>Creates an empty Jedi-training mission copy.</summary>
        /// <returns>An empty Jedi-training mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new JediTrainingMission();

        /// <summary>Copies Jedi-training-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((JediTrainingMission)destination).TrainerInstanceID = TrainerInstanceID;
        }

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        public JediTrainingMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Jedi Training";
        }

        /// <summary>
        /// Initializes a Jedi training mission with the selected trainer.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="trainerInstanceId">Officer selected as the trainer.</param>
        private JediTrainingMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string trainerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Jedi Training").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                SkillRating.Diplomacy,
                displayName: "Jedi Training"
            )
        {
            TrainerInstanceID = trainerInstanceId;
        }

        /// <summary>
        /// Returns a new JediTrainingMission if the selected team contains an eligible trainer and student on an own planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is not owned by this faction or no eligible trainer is available.</returns>
        public static JediTrainingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            if (!TryGetTrainingTeam(ctx.MainParticipants, ctx.Game, out Officer trainer))
                return null;

            return new JediTrainingMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                trainer.InstanceID
            );
        }

        /// <summary>
        /// Validates the selected participants and chooses the highest-ranked qualified trainer.
        /// </summary>
        /// <param name="participants">The officers selected for Jedi training.</param>
        /// <param name="game">The game state containing Jedi qualification thresholds.</param>
        /// <param name="trainer">The selected trainer when the team is valid.</param>
        /// <returns>True when every participant can train under the selected trainer.</returns>
        private static bool TryGetTrainingTeam(
            List<IMissionParticipant> participants,
            GameRoot game,
            out Officer trainer
        )
        {
            trainer = null;
            if (participants == null || participants.Count < 2 || game?.Config?.Jedi == null)
                return false;

            List<Officer> officers = new List<Officer>();
            foreach (IMissionParticipant participant in participants)
            {
                if (participant is not Officer officer || !CanParticipate(officer))
                    return false;

                officers.Add(officer);
            }

            Officer selectedTrainer = officers
                .Where(officer => CanLeadTraining(officer, game))
                .OrderByDescending(officer => officer.ForceRank)
                .FirstOrDefault();
            if (selectedTrainer == null)
                return false;

            trainer = selectedTrainer;
            return officers
                .Where(officer => officer != selectedTrainer)
                .All(officer => officer.ForceRank < selectedTrainer.ForceRank);
        }

        /// <summary>
        /// Returns whether an officer can participate in Jedi training.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <returns>True when the officer is a known, active Jedi.</returns>
        private static bool CanParticipate(Officer officer)
        {
            return officer?.IsForceSensitive == true
                && officer.IsForceEligible
                && !officer.IsCaptured
                && !officer.IsKilled;
        }

        /// <summary>
        /// Returns whether an officer is qualified to lead Jedi training.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="game">The game state containing the trainer rank threshold.</param>
        /// <returns>True when the officer can participate and qualifies as a trainer.</returns>
        internal static bool CanLeadTraining(Officer officer, GameRoot game)
        {
            return game != null
                && CanParticipate(officer)
                && officer.IsJediTrainer
                && officer.ForceRank >= game.Config.Jedi.ForceQualifiedThreshold;
        }

        /// <summary>
        /// Returns why Jedi training must stop before advancing.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when training may advance.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            if (GetParent() is not Planet planet || planet.GetOwnerInstanceID() != OwnerInstanceID)
                return MissionCompletionReason.TargetUnavailable;

            Officer trainer = Trainer;
            if (!CanLeadTraining(trainer, game))
                return MissionCompletionReason.Failure;

            int officerCount = GetMainParticipants().OfType<Officer>().Count();
            return
                officerCount == GetMainParticipants().Count
                && officerCount >= 2
                && GetMainParticipants().OfType<Officer>().All(CanParticipate)
                ? null
                : MissionCompletionReason.Failure;
        }

        /// <summary>
        /// Calculates the probability that at least one student makes Force training progress.
        /// </summary>
        /// <param name="participants">The training participants to evaluate.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The calculated training progress probability.</returns>
        protected override double GetObjectiveSuccessProbability(
            IEnumerable<IMissionParticipant> participants,
            MissionEvaluationContext context
        )
        {
            List<Officer> officers = (participants ?? Enumerable.Empty<IMissionParticipant>())
                .OfType<Officer>()
                .ToList();
            Officer trainer = officers.FirstOrDefault(officer =>
                officer.InstanceID == TrainerInstanceID
            );
            if (trainer == null || context.Game?.Config?.Jedi == null)
                return 0;

            IEnumerable<double> probabilities = officers
                .Where(officer => officer != trainer)
                .Select(officer => GetTrainingProgressProbability(officer, trainer, context.Game));
            return CombineSuccessProbabilities(probabilities);
        }

        /// <summary>
        /// Calculates one student's probability of receiving a positive training adjustment.
        /// </summary>
        /// <param name="officer">The student to evaluate.</param>
        /// <param name="trainer">The selected trainer.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The student's training progress probability.</returns>
        private static double GetTrainingProgressProbability(
            Officer officer,
            Officer trainer,
            GameRoot game
        )
        {
            int forceRankGap = trainer.ForceRank - officer.ForceRank;
            int catchUpRange = forceRankGap * game.Config.Jedi.TrainingCatchUpPercent / 100;
            if (forceRankGap <= 0 || catchUpRange <= 0)
                return 0;

            double rankRollProbability = Math.Min(100, forceRankGap) / 100.0;
            double positiveBonusProbability = (double)catchUpRange / (catchUpRange + 1);
            return rankRollProbability * positiveBonusProbability * 100;
        }

        /// <summary>
        /// Resolves one training attempt for each selected officer and completes the mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used for training rolls.</param>
        /// <returns>The training progress results followed by the mission completion result.</returns>
        internal override List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
            Officer trainer = Trainer;

            if (trainer != null)
            {
                foreach (
                    Officer officer in GetMainParticipants()
                        .OfType<Officer>()
                        .OrderBy(officer => trainer.ForceRank - officer.ForceRank)
                )
                {
                    ForceTrainingResult trainingResult = TrainOfficer(
                        officer,
                        trainer,
                        game,
                        provider
                    );
                    if (trainingResult != null)
                        results.Add(trainingResult);
                }
            }

            MissionOutcome outcome =
                results.Count > 0 ? MissionOutcome.Success : MissionOutcome.Failed;
            results.Add(BuildCompletedResult(outcome, game));
            return results;
        }

        /// <summary>
        /// Attempts to improve one officer's Force training adjustment toward the trainer's rank.
        /// </summary>
        /// <param name="officer">The officer receiving training.</param>
        /// <param name="trainer">The officer leading the training.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used for the training rolls.</param>
        /// <returns>The training result when progress was made; otherwise, null.</returns>
        private static ForceTrainingResult TrainOfficer(
            Officer officer,
            Officer trainer,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            int forceRankGap = trainer.ForceRank - officer.ForceRank;
            if (provider.NextInt(0, 100) >= forceRankGap)
                return null;

            int catchUpRange = forceRankGap * game.Config.Jedi.TrainingCatchUpPercent / 100;
            int bonus = provider.NextInt(0, catchUpRange + 1);
            if (bonus <= 0)
                return null;

            officer.ForceTrainingAdjustment += bonus;
            GameLogger.Log(
                $"{officer.GetDisplayName()} gained {bonus} training adjustment from {trainer.GetDisplayName()} (rank {officer.ForceRank})"
            );

            return new ForceTrainingResult
            {
                Officer = officer,
                Progress = bonus,
                Detail = trainer.ForceRank,
                Tick = game.CurrentTick,
            };
        }

        /// <summary>
        /// Returns whether Jedi training should repeat after completion.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false because Jedi training completes after one execution.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;
    }

    /// <summary>
    /// Mission that marks an unvisited planet as visited.
    /// </summary>
    public class ReconnaissanceMission : Mission
    {
        public const string MissionTypeID = "Reconnaissance";

        /// <summary>Creates an empty reconnaissance mission copy.</summary>
        /// <returns>An empty reconnaissance mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new ReconnaissanceMission();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public ReconnaissanceMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = SkillRating.Espionage;
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
                SkillRating.Espionage
            ) { }

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
        /// Reconnaissance always completes successfully after surviving the foiling phase.
        /// </summary>
        /// <param name="participants">The reconnaissance participants.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>One hundred percent when at least one participant is assigned; otherwise zero.</returns>
        protected override double GetObjectiveSuccessProbability(
            IEnumerable<IMissionParticipant> participants,
            MissionEvaluationContext context
        ) => participants?.Any() == true ? 100 : 0;

        /// <summary>
        /// Resolves reconnaissance without a success roll.
        /// </summary>
        /// <param name="game">Current game state.</param>
        /// <param name="provider">RNG provider.</param>
        /// <returns>All results produced by the mission.</returns>
        internal override List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();

            results.AddRange(OnSuccess(game, provider, GetMainParticipants().FirstOrDefault()));
            results.Add(
                BuildCompletedResult(MissionOutcome.Success, MissionCompletionReason.Success, game)
            );
            return results;
        }

        /// <summary>
        /// Marks the target as visited for the mission owner and records the observed planet state.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider.</param>
        /// <param name="successfulParticipant">The participant assigned to the reconnaissance mission.</param>
        /// <returns>An empty result list.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Planet planet = GetParent() as Planet;
            if (planet == null)
                return new List<GameResult>();

            planet.AddVisitor(OwnerInstanceID);

            Faction faction = game?.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(faction, planet, sector, game?.CurrentTick ?? 0);

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
            ParticipantRating = SkillRating.Leadership;
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
                SkillRating.Leadership
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
        /// Returns why recruitment can no longer continue at the mission planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>
        /// The target-unavailable reason when the planet is no longer friendly or no candidate
        /// remains; otherwise null.
        /// </returns>
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
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The participant's raw recruitment score.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            Planet planet = GetMissionPlanet(context);
            if (planet == null)
                return base.GetAgentScore(agent, context);

            int opposingSupport = planet.GetOpposingPopularSupport(OwnerInstanceID);
            return agent.GetEffectiveRating(SkillRating.Leadership) - opposingSupport;
        }

        /// <summary>
        /// Attempts recruiters from lowest to highest success probability and stops when one
        /// successfully recruits a candidate.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for success and candidate-selection rolls.</param>
        /// <returns>The recruitment result followed by the terminal mission result.</returns>
        internal override List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
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

    /// <summary>
    /// Mission that attempts to free a captured friendly officer.
    /// </summary>
    public class RescueMission : Mission
    {
        public const string MissionTypeID = "Rescue";

        /// <summary>
        /// Instance ID of the officer selected as the rescue target.
        /// </summary>
        public string TargetOfficerInstanceID { get; set; }

        /// <summary>Creates an empty rescue mission copy.</summary>
        /// <returns>An empty rescue mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new RescueMission();

        /// <summary>Copies rescue-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((RescueMission)destination).TargetOfficerInstanceID = TargetOfficerInstanceID;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public RescueMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = SkillRating.Combat;
        }

        /// <summary>
        /// Initializes a rescue mission with its selected officer target.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="targetOfficerInstanceId">Officer selected as the rescue target.</param>
        private RescueMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Rescue").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                SkillRating.Combat
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
        }

        /// <summary>
        /// Returns a new RescueMission for the specified captured friendly officer, or null if the
        /// target is not a valid rescue target (not friendly, not captured, wrong planet).
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, participants, and the target officer.</param>
        /// <returns>A configured mission, or null if the target is ineligible.</returns>
        public static RescueMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            Officer target = ctx.SelectedTarget as Officer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() != ctx.OwnerInstanceId
                || !target.IsCaptured
                || !IsOperationalTarget(target)
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new RescueMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        /// <summary>
        /// Resolves whether rescue can execute after participants arrive.
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
        /// Returns whether the selected officer can still be rescued.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the target remains captured at the mission planet.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            Officer captive = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return captive?.IsCaptured == true
                && IsOperationalTarget(captive)
                && captive.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Clears the captured state and captor from the rescued officer.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for rescue).</param>
        /// <param name="successfulParticipant">The participant whose rescue attempt succeeded.</param>
        /// <returns>An OfficerCaptureStateResult and an OfficerRescuedResult, or an empty list if the target was already removed.</returns>
        protected override List<GameResult> OnSuccess(
            GameRoot game,
            IRandomNumberProvider provider,
            IMissionParticipant successfulParticipant
        )
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                return new List<GameResult>();
            target.IsCaptured = false;
            target.CaptorInstanceID = null;
            target.CanEscape = false;

            return new List<GameResult>
            {
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = false,
                    Context = GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
                new OfficerRescuedResult
                {
                    Officer = target,
                    RescuingFaction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                    Location = GetParent() as Planet,
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Returns the rescued officer when they are free and belong to the mission owner.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The rescued officer when eligible to return with the mission group.</returns>
        internal override IEnumerable<IMovable> GetSuccessfulReturnPassengers(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target?.IsCaptured == false && target.GetOwnerInstanceID() == OwnerInstanceID)
                yield return target;
        }

        /// <summary>
        /// Rescue missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }

    /// <summary>
    /// Research mission that awards side research capacity for one discipline.
    /// The targeted <see cref="ResearchDiscipline"/> is carried as data on the mission.
    /// </summary>
    public class ResearchMission : Mission
    {
        public const string MissionTypeID = "Research";

        /// <summary>
        /// Research discipline advanced by this mission.
        /// </summary>
        public ResearchDiscipline Discipline { get; set; }

        /// <summary>Creates an empty research mission copy.</summary>
        /// <returns>An empty research mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new ResearchMission();

        /// <summary>Copies research-specific state into an empty destination.</summary>
        /// <param name="destination">The destination mission.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            ((ResearchMission)destination).Discipline = Discipline;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public ResearchMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = SkillRating.None;
        }

        /// <summary>
        /// Initializes a research mission for the selected discipline.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="discipline">Research discipline advanced by the mission.</param>
        private ResearchMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ResearchDiscipline discipline
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Research").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                Officer.GetRatingForResearchDiscipline(discipline),
                displayName: GetMissionName(discipline)
            )
        {
            Discipline = discipline;
        }

        /// <summary>
        /// Returns a new ResearchMission if the target is an owned planet and the
        /// selected officer can perform the discipline.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <param name="discipline">The research discipline this mission advances.</param>
        /// <returns>A configured mission, or null if the mission is not eligible.</returns>
        public static ResearchMission TryCreate(MissionContext ctx, ResearchDiscipline discipline)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            if (!HasResearchRemaining(ctx.Game, ctx.OwnerInstanceId, discipline))
                return null;

            if (!HasResearchFacility(planet, discipline))
                return null;

            List<IMissionParticipant> actingParticipants = ctx.MainParticipants;
            if (
                actingParticipants == null
                || actingParticipants.Count == 0
                || actingParticipants.Any(participant =>
                    participant is not Officer officer || officer.GetBaseRating(discipline) <= 0
                )
            )
                return null;

            return new ResearchMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                new List<IMissionParticipant>(actingParticipants),
                ctx.DecoyParticipants,
                discipline
            );
        }

        /// <summary>
        /// Returns the display name for a research discipline mission.
        /// </summary>
        /// <param name="discipline">The research discipline.</param>
        /// <returns>The mission display name.</returns>
        private static string GetMissionName(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => "Ship Design",
                ResearchDiscipline.TroopTraining => "Troop Training",
                ResearchDiscipline.FacilityDesign => "Facility Design",
                _ => "Research",
            };
        }

        /// <summary>
        /// Resolves whether research can execute after participants arrive.
        /// A matching facility is required to issue the mission, but the original game does not
        /// cancel active research if that facility is subsequently destroyed.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The failure reason, or null when research can advance.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID
                ? null
                : MissionCompletionReason.TargetUnavailable;
        }

        /// <summary>
        /// Returns whether a planet has a facility that can support the research discipline.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="discipline">The research discipline being performed.</param>
        /// <returns>True when the planet has a matching completed facility.</returns>
        internal static bool HasResearchFacility(Planet planet, ResearchDiscipline? discipline)
        {
            if (planet == null || !discipline.HasValue)
                return false;

            return discipline.Value switch
            {
                ResearchDiscipline.ShipDesign => planet
                    .GetProductionFacilities(ManufacturingType.Ship)
                    .Count > 0,
                ResearchDiscipline.TroopTraining => planet
                    .GetProductionFacilities(ManufacturingType.Troop)
                    .Count > 0,
                ResearchDiscipline.FacilityDesign => planet
                    .GetProductionFacilities(ManufacturingType.Building)
                    .Count > 0,
                _ => false,
            };
        }

        /// <summary>
        /// Returns whether a faction has another advance available in a research discipline.
        /// </summary>
        /// <param name="game">The game containing the faction research state.</param>
        /// <param name="ownerInstanceID">The faction performing the research.</param>
        /// <param name="discipline">The research discipline to inspect.</param>
        /// <returns>False when the faction's initialized catalog has no remaining advance.</returns>
        private static bool HasResearchRemaining(
            GameRoot game,
            string ownerInstanceID,
            ResearchDiscipline discipline
        )
        {
            Faction faction = game?.GetFactionByOwnerInstanceID(ownerInstanceID);
            return faction?.ResearchCatalog.ContainsKey(discipline) != true
                || !faction.IsResearchExhausted(discipline);
        }

        /// <summary>
        /// Calculates the probability that at least one researcher produces research progress.
        /// </summary>
        /// <param name="participants">The researchers to evaluate.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The calculated research progress probability.</returns>
        protected override double GetObjectiveSuccessProbability(
            IEnumerable<IMissionParticipant> participants,
            MissionEvaluationContext context
        )
        {
            GameConfig.ResearchConfig config = context.Game?.Config?.Research;
            double rewardProbability = GetPositiveRewardProbability(config);
            IEnumerable<double> probabilities = (
                participants ?? Enumerable.Empty<IMissionParticipant>()
            )
                .OfType<Officer>()
                .Select(officer => officer.GetBaseRating(Discipline) * rewardProbability);
            return CombineSuccessProbabilities(probabilities);
        }

        /// <summary>
        /// Returns the probability that a successful research roll awards at least one point.
        /// </summary>
        /// <param name="config">The research reward configuration.</param>
        /// <returns>The positive reward probability as a multiplier from 0 through 1.</returns>
        private static double GetPositiveRewardProbability(GameConfig.ResearchConfig config)
        {
            if (config == null)
                return 0;
            if (config.BaseResearchPoints > 0)
                return 1;
            if (config.BaseResearchPoints < 0 || config.ResearchDiceRange <= 0)
                return 0;

            return (double)config.ResearchDiceRange / (config.ResearchDiceRange + 1);
        }

        /// <summary>
        /// Resolves one mission execution: each main participant rolls independently;
        /// each success accumulates a reward and bumps that officer's research rating.
        /// The total is then applied to the faction and any transitions are emitted.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for chance rolls and reward rolls.</param>
        /// <returns>Transition results, with a MissionCompletedResult appended.</returns>
        internal override List<GameResult> ResolveObjective(
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> results = new List<GameResult>();
            MissionOutcome outcome = MissionOutcome.Failed;
            MissionCompletionReason completionReason = MissionCompletionReason.TargetUnavailable;
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            if (faction != null)
            {
                int earnedPoints = AccumulatePointsFromParticipants(game.Config.Research, provider);
                if (earnedPoints > 0)
                {
                    outcome = MissionOutcome.Success;
                    AwardAccumulatedPoints(faction, earnedPoints, game, results);
                    completionReason = results.OfType<ResearchOrderedResult>().Any()
                        ? MissionCompletionReason.ResearchBreakthrough
                        : MissionCompletionReason.ResearchProgress;
                }
                else
                {
                    completionReason = MissionCompletionReason.Failure;
                }
            }

            results.Add(BuildCompletedResult(outcome, completionReason, game));
            return results;
        }

        /// <summary>
        /// Rolls each officer's success chance; on success, rolls a reward and bumps that
        /// officer's research rating. Returns the total points earned across all participants.
        /// </summary>
        /// <param name="config">Research configuration providing reward parameters.</param>
        /// <param name="provider">RNG provider for chance and reward rolls.</param>
        /// <returns>Total research points earned this execution.</returns>
        private int AccumulatePointsFromParticipants(
            GameConfig.ResearchConfig config,
            IRandomNumberProvider provider
        )
        {
            int earnedPoints = 0;
            foreach (IMissionParticipant participant in GetMainParticipants())
            {
                if (!(participant is Officer officer) || !RollSuccess(officer, provider))
                    continue;

                earnedPoints += RollReward(config, provider);
                ImproveMissionParticipantRating(participant);
            }
            return earnedPoints;
        }

        /// <summary>
        /// Improves the successful officer's rating for this mission's research discipline.
        /// </summary>
        /// <param name="participant">The research participant whose attempt succeeded.</param>
        internal override void ImproveMissionParticipantRating(IMissionParticipant participant)
        {
            if (participant is Officer officer && participant.CanImproveMissionRating)
                officer.IncrementBaseRating(Discipline);
        }

        /// <summary>
        /// Returns true when the officer's roll comes in strictly under their research chance.
        /// </summary>
        /// <param name="officer">The officer attempting the research.</param>
        /// <param name="provider">RNG provider for the chance roll.</param>
        /// <returns>True if the participant succeeded this attempt.</returns>
        private bool RollSuccess(Officer officer, IRandomNumberProvider provider)
        {
            int chance = officer.GetBaseRating(Discipline);
            return provider.NextDouble() * 100 < chance;
        }

        /// <summary>
        /// Rolls one successful participant's reward.
        /// </summary>
        /// <param name="config">Research configuration providing reward parameters.</param>
        /// <param name="provider">RNG provider for the reward roll.</param>
        /// <returns>The number of research points awarded for this success.</returns>
        private static int RollReward(
            GameConfig.ResearchConfig config,
            IRandomNumberProvider provider
        )
        {
            return config.BaseResearchPoints + provider.NextInt(0, config.ResearchDiceRange + 1);
        }

        /// <summary>
        /// Applies the earned points to the faction and emits an ordered result if the
        /// order advanced, plus an exhausted result if the discipline now has no further advances.
        /// </summary>
        /// <param name="faction">The owning faction whose research state advances.</param>
        /// <param name="earnedPoints">The total research points earned this execution.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="results">Result list to append transition results to.</param>
        private void AwardAccumulatedPoints(
            Faction faction,
            int earnedPoints,
            GameRoot game,
            List<GameResult> results
        )
        {
            Technology unlocked = faction.ApplyResearchProgress(Discipline, earnedPoints);
            if (unlocked == null)
                return;

            results.Add(BuildOrderedResult(faction, unlocked, game));
            if (faction.IsResearchExhausted(Discipline))
                results.Add(BuildExhaustedResult(faction, game));
        }

        /// <summary>
        /// Builds a <see cref="ResearchOrderedResult"/> capturing the just-advanced
        /// research order and the technology that became available.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="unlocked">The technology that just became available.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>A populated ordered result.</returns>
        private ResearchOrderedResult BuildOrderedResult(
            Faction faction,
            Technology unlocked,
            GameRoot game
        )
        {
            return new ResearchOrderedResult
            {
                Tick = game.CurrentTick,
                Faction = faction,
                Discipline = Discipline,
                ResearchOrder = faction.GetHighestUnlockedOrder(Discipline),
                Capacity = faction.GetResearchCapacityRemaining(Discipline),
                Technology = unlocked,
            };
        }

        /// <summary>
        /// Builds a <see cref="ResearchExhaustedResult"/> for a discipline that now
        /// has no further advances available.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>A populated exhausted result.</returns>
        private ResearchExhaustedResult BuildExhaustedResult(Faction faction, GameRoot game)
        {
            return new ResearchExhaustedResult
            {
                Tick = game.CurrentTick,
                Faction = faction,
                Discipline = Discipline,
                PreviousState = 0,
                NewState = 1,
            };
        }

        /// <summary>
        /// Research missions repeat while the target remains valid and advances remain available.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should repeat.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            if (GetMissionInvalidationReason(game).HasValue)
                return false;

            return HasResearchRemaining(game, OwnerInstanceID, Discipline);
        }
    }

    /// <summary>
    /// Mission that attempts to destroy or damage a selected enemy target.
    /// </summary>
    public class SabotageMission : Mission
    {
        public const string MissionTypeID = "Sabotage";

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
            ParticipantRating = SkillRating.Combat;
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
                SkillRating.Combat
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
            if (!IsValidTarget(selectedTarget, ctx.OwnerInstanceId))
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
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The participant's raw sabotage score.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            return (
                    agent.GetEffectiveRating(SkillRating.Espionage)
                    + agent.GetEffectiveRating(SkillRating.Combat)
                ) / 2;
        }

        /// <summary>
        /// Returns whether the selected sabotage target is still present at the mission planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the selected target can still be sabotaged.</returns>
        private bool HasValidTarget(GameRoot game)
        {
            ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(SabotageTargetInstanceID);
            if (!IsValidTarget(target, OwnerInstanceID))
                return false;

            return target.GetParentOfType<Planet>() == GetParent() as Planet;
        }

        /// <summary>
        /// Returns whether a scene node is an eligible regular sabotage target.
        /// </summary>
        /// <param name="target">The scene node selected for sabotage.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <returns>True when the target is an operational enemy manufacturable other than a planet-destroying ship.</returns>
        private static bool IsValidTarget(ISceneNode target, string ownerInstanceId)
        {
            if (
                target is not IManufacturable
                || target is CapitalShip { CanDestroyPlanets: true }
                || string.IsNullOrEmpty(target.GetOwnerInstanceID())
                || target.GetOwnerInstanceID() == ownerInstanceId
            )
                return false;

            return IsOperationalTarget(target);
        }

        /// <summary>
        /// Improves both ratings used by a successful officer's sabotage attempt.
        /// </summary>
        /// <param name="participant">The participant whose sabotage attempt succeeded.</param>
        internal override void ImproveMissionParticipantRating(IMissionParticipant participant)
        {
            if (participant is not Officer officer || !participant.CanImproveMissionRating)
                return;

            officer.IncrementBaseRating(SkillRating.Espionage);
            officer.IncrementBaseRating(SkillRating.Combat);
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
                    DestroyedObject = target,
                    DestroyedBy = successfulParticipant,
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

    /// <summary>
    /// Mission that attempts to reduce an uprising on a faction-owned planet.
    /// </summary>
    public class SubdueUprisingMission : Mission
    {
        public const string MissionTypeID = "SubdueUprising";

        /// <summary>
        /// Creates an empty subdue-uprising mission copy.
        /// </summary>
        /// <returns>An empty subdue-uprising mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new SubdueUprisingMission();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public SubdueUprisingMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Subdue Uprising";
            ParticipantRating = SkillRating.Leadership;
        }

        /// <summary>
        /// Initializes a subdue uprising mission for the selected planet.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        private SubdueUprisingMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Subdue Uprising").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                SkillRating.Leadership,
                displayName: "Subdue Uprising"
            ) { }

        /// <summary>
        /// Returns a new SubdueUprisingMission if the target is an own planet in uprising, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is not owned by this faction or not in uprising.</returns>
        public static SubdueUprisingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (!planet.IsInUprising || planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            return new SubdueUprisingMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Extends base cancellation to also cancel when the uprising ends or the target changes
        /// ownership before execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        protected override MissionCompletionReason? GetMissionInvalidationReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetMissionInvalidationReason(game);
            if (reason.HasValue)
                return reason;

            return
                GetParent() is Planet p
                && p.IsInUprising
                && p.GetOwnerInstanceID() == OwnerInstanceID
                ? null
                : MissionCompletionReason.Failure;
        }

        /// <summary>
        /// Returns a participant's raw score for subduing the target uprising.
        /// </summary>
        /// <param name="agent">The participant attempting to subdue the uprising.</param>
        /// <param name="context">The authoritative or observed state used for evaluation.</param>
        /// <returns>The participant's raw subdue-uprising score.</returns>
        protected override int? GetAgentScore(
            IMissionParticipant agent,
            MissionEvaluationContext context
        )
        {
            Planet planet = GetMissionPlanet(context);
            if (planet == null)
                throw new InvalidOperationException(
                    "SubdueUprisingMission must be attached to a Planet."
                );

            return agent.GetEffectiveRating(SkillRating.Leadership)
                - planet.GetOpposingPopularSupport(OwnerInstanceID);
        }

        /// <summary>
        /// Subdue Uprising missions continue until the uprising has ended.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True while the owned target planet remains in uprising.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return !GetMissionInvalidationReason(game).HasValue
                && GetParent() is Planet planet
                && planet.GetOwnerInstanceID() == OwnerInstanceID;
        }
    }
}
