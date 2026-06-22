using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class AssassinationMission : Mission
    {
        public string TargetOfficerInstanceID { get; set; }

        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public AssassinationMission()
            : base()
        {
            ConfigKey = "Assassination";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Combat;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        private AssassinationMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                "Assassination",
                ownerInstanceId,
                RequirePlanetTarget(target, "Assassination").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Combat,
                null
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
            if (!(ctx.Target is Planet planet))
                return null;

            Officer target = ctx.TargetOfficer;
            Planet targetPlanet = target?.GetParentOfType<Planet>();
            if (
                target == null
                || target.GetOwnerInstanceID() == ctx.OwnerInstanceId
                || target.IsCaptured
                || target.IsKilled
                || targetPlanet?.InstanceID != planet.InstanceID
            )
                return null;

            return new AssassinationMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                target.InstanceID
            );
        }

        public override bool CanStart(GameRoot game) => HasValidTarget(game);

        protected override bool IsMissionSatisfied(GameRoot game) => HasValidTarget(game);

        private bool HasValidTarget(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            return target?.IsKilled == false
                && !target.IsCaptured
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
        /// Assassination missions do not repeat — one attempt per mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool CanContinue(GameRoot game)
        {
            return false;
        }
    }
}
