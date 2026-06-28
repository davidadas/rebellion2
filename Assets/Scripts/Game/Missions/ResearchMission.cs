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
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The failure reason, or null when research can advance.</returns>
        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            Planet planet = GetParent() as Planet;
            if (IsMissionSatisfied(game) && HasResearchFacility(planet, Discipline))
                return null;

            if (
                planet != null
                && planet.GetOwnerInstanceID() == OwnerInstanceID
                && !HasResearchFacility(planet, Discipline)
            )
            {
                return MissionCompletionReason.NoResearchFacilities;
            }

            return MissionCompletionReason.TargetUnavailable;
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
                    .Count > 0
                    || planet.GetProductionFacilities(ManufacturingType.Troop).Count > 0,
                ResearchDiscipline.TroopTraining => planet
                    .GetProductionFacilities(ManufacturingType.Troop)
                    .Count > 0
                    || planet.GetProductionFacilities(ManufacturingType.Building).Count > 0,
                ResearchDiscipline.FacilityDesign => planet
                    .GetProductionFacilities(ManufacturingType.Building)
                    .Count > 0
                    || planet
                        .GetAllBuildings()
                        .Any(building =>
                            building.BuildingType == BuildingType.Mine
                            && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                        ),
                _ => false,
            };
        }

        /// <summary>
        /// Checks whether the mission target planet is still owned by the mission's faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the parent planet is owned by this faction.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
        }

        /// <summary>
        /// Research missions target own planets and are never foiled.
        /// </summary>
        /// <param name="defenseScore">The defense score (unused).</param>
        /// <param name="game">The current game state, unused because research cannot be foiled.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

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
            MissionCompletionReason completionReason =
                GetAbortReason(game) ?? MissionCompletionReason.TargetUnavailable;
            Faction faction = game.GetFactionByOwnerInstanceID(OwnerInstanceID);
            Planet planet = GetParent() as Planet;

            if (
                faction != null
                && IsMissionSatisfied(game)
                && HasResearchFacility(planet, Discipline)
            )
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
        /// Research missions repeat after completion as long as the mission target is still satisfied.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should repeat.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => IsMissionSatisfied(game);
    }
}
