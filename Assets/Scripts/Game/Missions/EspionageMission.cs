using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Mission that refreshes fog-of-war information for a visited planet.
    /// </summary>
    public class EspionageMission : Mission
    {
        public const string MissionTypeID = "Espionage";

        /// <summary>
        /// Returns whether this mission should cancel when the target planet changes owner.
        /// </summary>
        public override bool CanceledOnOwnershipChange => false;

        /// <summary>
        /// Returns whether detected participants receive the standard foiled-mission consequences.
        /// </summary>
        internal override bool AppliesFoiledParticipantConsequences => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public EspionageMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Espionage;
            DecoyParticipantRating = OfficerRating.Espionage;
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
                OfficerRating.Espionage
            )
        {
            DecoyParticipantRating = OfficerRating.Espionage;
        }

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
        /// Returns true as long as the mission is still attached to a planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission parent is a planet.</returns>
        protected override bool IsMissionSatisfied(GameRoot game)
        {
            return GetParent() is Planet;
        }

        /// <summary>
        /// Executes the espionage attempt and snapshots the target planet on success.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for success rolls.</param>
        /// <returns>All results produced by the outcome, with a MissionCompletedResult appended.</returns>
        internal override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            List<IMissionParticipant> successfulParticipants = new List<IMissionParticipant>();

            foreach (IMissionParticipant participant in MainParticipants)
            {
                double successThreshold = GetAgentProbability(participant, game);
                double rolledValue = provider.NextDouble() * 100;
                if (IsSuccessfulProbabilityRoll(rolledValue, successThreshold))
                    successfulParticipants.Add(participant);
            }

            MissionOutcome outcome;
            if (successfulParticipants.Count > 0 && IsMissionSatisfied(game))
            {
                outcome = MissionOutcome.Success;
                results.AddRange(OnSuccess(game, provider));
                ImproveSuccessfulParticipants(successfulParticipants);
            }
            else
            {
                outcome = MissionOutcome.Failed;
                results.AddRange(OnFailed(game, provider));
            }

            results.Add(BuildCompletedResult(outcome, game));
            return results;
        }

        /// <summary>
        /// Improves ratings for participants that succeeded in the espionage attempt.
        /// </summary>
        /// <param name="participants">Participants whose success rolls passed.</param>
        private void ImproveSuccessfulParticipants(List<IMissionParticipant> participants)
        {
            if (!CanImproveRatingsAgainstTarget())
                return;

            foreach (IMissionParticipant participant in participants)
            {
                if (participant is Officer officer && participant.CanImproveMissionRating)
                    officer.IncrementBaseRating(ParticipantRating);
            }
        }

        /// <summary>
        /// Returns whether this mission target allows participant rating improvement.
        /// </summary>
        /// <returns>True when the target planet is not owned by the mission faction.</returns>
        private bool CanImproveRatingsAgainstTarget()
        {
            return GetParent() is Planet planet && planet.GetOwnerInstanceID() != OwnerInstanceID;
        }

        /// <summary>
        /// Captures full intelligence for the target planet and any bonus planets.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider used to select bonus planets.</param>
        /// <returns>A result identifying any additional systems revealed by the mission.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            Faction faction = game?.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSystem system = planet?.GetParentOfType<PlanetSystem>();

            if (game == null || faction == null || planet == null || system == null)
                return new List<GameResult>();

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(faction, planet, system, game.CurrentTick);

            List<PlanetSystem> additionalSystems = new List<PlanetSystem>();
            foreach (Planet bonusPlanet in SelectBonusPlanets(game, provider, planet, system))
            {
                PlanetSystem bonusSystem = bonusPlanet.GetParentOfType<PlanetSystem>();
                recorder.RecordEspionageSnapshot(
                    faction,
                    bonusPlanet,
                    bonusSystem,
                    game.CurrentTick
                );

                if (
                    bonusSystem != null
                    && additionalSystems.All(candidate =>
                        candidate.InstanceID != bonusSystem.InstanceID
                    )
                )
                    additionalSystems.Add(bonusSystem);
            }

            if (additionalSystems.Count == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new MissionSystemIntelligenceResult
                {
                    Tick = game.CurrentTick,
                    MissionInstanceID = InstanceID,
                    SourceEventInstanceID = SourceEventInstanceID,
                    AdditionalSystems = additionalSystems,
                },
            };
        }

        /// <summary>
        /// Selects distinct bonus planets using the original mission's target-specific pools.
        /// </summary>
        private IEnumerable<Planet> SelectBonusPlanets(
            GameRoot game,
            IRandomNumberProvider provider,
            Planet targetPlanet,
            PlanetSystem targetSystem
        )
        {
            if (targetSystem.SystemType != PlanetSystemType.CoreSystem)
                return Enumerable.Empty<Planet>();

            GameConfig.EspionageConfig config =
                game.Config?.Espionage ?? new GameConfig.EspionageConfig();
            GameConfig.RandomCountConfig countConfig = config.CoreSystemBonus;
            bool includeOuterRim = false;

            if (
                OwnerInstanceID == config.CapitalObserverFactionInstanceID
                && targetPlanet.InstanceID == config.CapitalPlanetInstanceID
            )
            {
                countConfig = config.CapitalBonus;
                includeOuterRim = true;
            }
            else if (IsMobileHeadquartersTarget(game, targetPlanet))
            {
                countConfig = config.MobileHeadquartersBonus;
                includeOuterRim = true;
            }

            List<Planet> candidates = game
                .Galaxy.PlanetSystems.Where(system =>
                    includeOuterRim || system.SystemType == PlanetSystemType.CoreSystem
                )
                .SelectMany(system => system.Planets)
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
        /// Returns whether the target currently hosts its owner's opposing mobile headquarters.
        /// </summary>
        private bool IsMobileHeadquartersTarget(GameRoot game, Planet targetPlanet)
        {
            Faction owner = game.GetFactionByOwnerInstanceID(targetPlanet.OwnerInstanceID);
            return owner != null
                && owner.InstanceID != OwnerInstanceID
                && owner.Settings.Headquarters.IsMobile
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
}
