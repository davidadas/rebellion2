using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class InciteUprisingMission : Mission
    {
        public const string MissionTypeID = "InciteUprising";

        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public InciteUprisingMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Incite Uprising";
            ParticipantRating = OfficerRating.Leadership;
            DecoyParticipantRating = OfficerRating.Espionage;
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
                OfficerRating.Leadership,
                displayName: "Incite Uprising"
            )
        {
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        /// <summary>
        /// Returns a new InciteUprisingMission if the target is an enemy planet not in uprising, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is neutral, owned by this faction, or already in uprising.</returns>
        public static InciteUprisingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Target is Planet planet))
                return null;

            string owner = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(owner) || owner == ctx.OwnerInstanceId || planet.IsInUprising)
                return null;

            return new InciteUprisingMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants
            );
        }

        /// <summary>
        /// Extends base cancellation to also cancel if an uprising starts before the mission executes.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when the mission may advance.</returns>
        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            return GetParent() is Planet p && p.IsInUprising
                ? MissionCompletionReason.Failure
                : null;
        }

        /// <summary>
        /// Returns the participant's chance to incite the target planet.
        /// </summary>
        /// <param name="agent">The participant whose leadership rating is evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The participant's uprising success probability.</returns>
        protected override double GetAgentProbability(IMissionParticipant agent, GameRoot game)
        {
            if (!(GetParent() is Planet planet))
                throw new InvalidOperationException(
                    "InciteUprisingMission must be attached to a Planet."
                );

            int leadershipSkill = agent.GetEffectiveRating(OfficerRating.Leadership);
            int enemySupport = planet.GetPopularSupport(planet.OwnerInstanceID);

            int regimentStrength = 0;
            foreach (ISceneNode child in planet.GetChildren())
            {
                if (child is Regiment regiment && regiment.OwnerInstanceID != OwnerInstanceID)
                    regimentStrength += regiment.DefenseRating;
            }

            int score = leadershipSkill - enemySupport - regimentStrength;
            return LookupSuccessProbability(game, score);
        }

        /// <summary>
        /// Starts an uprising on the target planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for incite uprising).</param>
        /// <returns>One PlanetUprisingStartedResult.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            planet.BeginUprising();

            return new List<GameResult>
            {
                new PlanetUprisingStartedResult
                {
                    Planet = planet,
                    InstigatorFaction = game.GetFactionByOwnerInstanceID(OwnerInstanceID),
                    Tick = game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Incite Uprising missions do not repeat after one attempt.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            return false;
        }
    }
}
