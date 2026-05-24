using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Game.Missions
{
    public class RecruitmentMission : Mission
    {
        public string TargetOfficerInstanceID { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public RecruitmentMission()
            : base()
        {
            ConfigKey = "Recruitment";
            DisplayName = ConfigKey;
            ParticipantSkill = MissionParticipantSkill.Leadership;
        }

        private RecruitmentMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string targetOfficerInstanceId
        )
            : base(
                "Recruitment",
                ownerInstanceId,
                target.GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                MissionParticipantSkill.Leadership,
                null
            )
        {
            TargetOfficerInstanceID = targetOfficerInstanceId;
        }

        /// <summary>
        /// Returns a new RecruitmentMission when this faction has at least one recruitable officer.
        /// </summary>
        /// <param name="ctx">Mission context; must include a valid target.</param>
        /// <returns>A configured mission, or null if no unrecruited officers exist.</returns>
        public static RecruitmentMission TryCreate(MissionContext ctx)
        {
            List<Officer> unrecruited = ctx.Game.GetUnrecruitedOfficers(ctx.OwnerInstanceId);
            bool areMainCharacters = ctx.MainParticipants.Any(o => o.IsMainCharacter());
            if (ctx.MainParticipants.Count > 0 && !areMainCharacters)
                return null;

            if (unrecruited.Count == 0)
                return null;

            return new RecruitmentMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                null
            );
        }

        /// <summary>
        /// Returns false when this faction no longer has an unrecruited officer available.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if at least one unrecruited officer is available.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
        }

        /// <summary>
        /// Recruitment missions are never foiled — they target unaffiliated officers, not enemy planets.
        /// </summary>
        /// <param name="defenseScore">Ignored.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore) => 0;

        /// <summary>
        /// Looks up the recruitment success chance for a participant at the mission planet.
        /// </summary>
        /// <param name="agent">The participant whose leadership skill is evaluated.</param>
        /// <returns>Success probability from the recruitment table.</returns>
        protected override double GetAgentProbability(IMissionParticipant agent)
        {
            if (!(GetParent() is Planet planet))
                return base.GetAgentProbability(agent);

            int score =
                agent.GetMissionSkillValue(MissionParticipantSkill.Leadership)
                - planet.GetPopularSupport(OwnerInstanceID);
            return SuccessProbabilityTable.Lookup(score);
        }

        /// <summary>
        /// Transfers the target officer to this faction and moves them to the mission planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider used to select the recruited officer.</param>
        /// <returns>One OfficerRecruitedResult, or an empty list if the target or planet is missing.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            if (provider == null || planet == null)
                return new List<GameResult>();

            Officer target = game.GetUnrecruitedOfficers(OwnerInstanceID).RandomElement(provider);
            if (target == null)
                return new List<GameResult>();

            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            TargetOfficerInstanceID = target.InstanceID;
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
        public override bool CanContinue(GameRoot game)
        {
            return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
        }
    }
}
