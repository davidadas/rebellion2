using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Research mission that awards side research capacity for one discipline.
    /// The targeted <see cref="ResearchDiscipline"/> is carried as data on the mission.
    /// </summary>
    public class ResearchMission : Mission
    {
        public ResearchDiscipline Discipline { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public ResearchMission()
            : base()
        {
            ConfigKey = "Research";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.None;
        }

        private ResearchMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ResearchDiscipline discipline
        )
            : base(
                "Research",
                ownerInstanceId,
                RequirePlanetTarget(target, "Research").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                Officer.GetRatingForResearchDiscipline(discipline),
                null,
                displayName: GetMissionName(discipline)
            )
        {
            Discipline = discipline;
        }

        /// <summary>
        /// Returns a new ResearchMission if the target is an own planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <param name="discipline">The research discipline this mission advances.</param>
        /// <returns>A configured mission, or null if the planet is not owned by this faction.</returns>
        public static ResearchMission TryCreate(MissionContext ctx, ResearchDiscipline discipline)
        {
            if (!(ctx.Target is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            List<IMissionParticipant> actingParticipants = new List<IMissionParticipant>();
            if (ctx.MainParticipants?.Count > 0)
                actingParticipants.Add(ctx.MainParticipants[0]);

            return new ResearchMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                actingParticipants,
                ctx.DecoyParticipants,
                discipline
            );
        }

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
        /// Checks whether the mission target planet is still owned by the mission's faction.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <returns>True if the parent planet is owned by this faction.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
        }

        /// <summary>
        /// Research missions target own planets and are never foiled.
        /// </summary>
        /// <param name="defenseScore">The defense score (unused).</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore) => 0;

        /// <summary>
        /// Resolves one mission execution: each main participant rolls independently;
        /// each success accumulates a reward and bumps that officer's research rating.
        /// The total is then applied to the faction and any transitions are emitted.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for chance rolls and reward rolls.</param>
        /// <returns>Transition results, with a MissionCompletedResult appended.</returns>
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            MissionOutcome outcome = MissionOutcome.Failed;
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);

            if (faction != null && IsMissionSatisfied(game))
            {
                int earnedPoints = AccumulatePointsFromParticipants(game.Config.Research, provider);
                if (earnedPoints > 0)
                {
                    outcome = MissionOutcome.Success;
                    AwardAccumulatedPoints(faction, earnedPoints, game, results);
                }
            }

            results.Add(BuildCompletedResult(outcome, game));
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
            foreach (IMissionParticipant participant in MainParticipants)
            {
                if (!(participant is Officer officer) || !RollSuccess(officer, provider))
                    continue;

                earnedPoints += RollReward(config, provider);
                officer.IncrementBaseRating(Discipline);
            }
            return earnedPoints;
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
        /// Research missions repeat as long as the mission target is still satisfied.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <returns>True if the mission should continue.</returns>
        public override bool CanContinue(GameRoot game) => IsMissionSatisfied(game);
    }
}
