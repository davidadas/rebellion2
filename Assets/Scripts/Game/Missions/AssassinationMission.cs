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

        /// <summary>
        /// Returns whether this mission should cancel when the target planet changes owner.
        /// </summary>
        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public AssassinationMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
            DecoyParticipantRating = OfficerRating.Espionage;
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
            DecoyParticipantRating = OfficerRating.Espionage;
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

            Officer target = ctx.TargetOfficer;
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
        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            return HasValidTarget(game) ? null : MissionCompletionReason.TargetUnavailable;
        }

        /// <summary>
        /// Returns false if the target officer has already been killed or has moved
        /// away from the mission's planet before execution.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the target is still alive and on the mission planet.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return HasValidTarget(game);
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
        /// Applies assassination injury to the target. The target may survive with injury
        /// or die outright based on a probabilistic kill check.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for injury dice and kill check.</param>
        /// <returns>An OfficerInjuredResult and optionally an OfficerKilledResult.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
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

            if (RollKillCheck(game.Config.Assassination, provider))
            {
                target.IsKilled = true;
                game.DetachNode(target);
                results.Add(
                    new OfficerKilledResult
                    {
                        TargetOfficer = target,
                        Assassin = MainParticipants.Count > 0 ? MainParticipants[0] : null,
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
        /// Rolls whether the assassination hit kills the target outright.
        /// </summary>
        /// <param name="config">Assassination configuration.</param>
        /// <param name="provider">RNG provider.</param>
        /// <returns>True if the target is killed.</returns>
        private static bool RollKillCheck(
            GameConfig.AssassinationConfig config,
            IRandomNumberProvider provider
        )
        {
            return provider.NextDouble() * 100 <= config.KillProbability;
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
