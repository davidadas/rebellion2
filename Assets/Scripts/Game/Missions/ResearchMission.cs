using System.Collections.Generic;
using System.Linq;
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
            ParticipantRating = OfficerRating.None;
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
        /// Calculates the probability that at least one researcher produces research progress.
        /// </summary>
        /// <param name="participants">The researchers to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="observedPlanet">Optional player-visible planet state used for planning.</param>
        /// <param name="observedTarget">Optional player-visible mission target used for planning.</param>
        /// <returns>The calculated research progress probability.</returns>
        internal override double GetObjectiveSuccessProbability(
            IEnumerable<IMissionParticipant> participants,
            GameRoot game,
            Planet observedPlanet = null,
            ISceneNode observedTarget = null
        )
        {
            GameConfig.ResearchConfig config = game?.Config?.Research;
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

            Faction faction = game?.GetFactionByOwnerInstanceID(OwnerInstanceID);
            if (faction?.ResearchCatalog.ContainsKey(Discipline) != true)
                return true;

            return !faction.IsResearchExhausted(Discipline);
        }
    }
}
