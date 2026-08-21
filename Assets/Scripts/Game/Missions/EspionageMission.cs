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

            foreach (IMissionParticipant participant in GetMainParticipants())
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
        /// <returns>A result identifying any additional sectors revealed by the mission.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            Faction faction = game?.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSector sector = planet?.GetParentOfType<PlanetSector>();

            if (game == null || faction == null || planet == null || sector == null)
                return new List<GameResult>();

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(faction, planet, sector, game.CurrentTick);

            List<PlanetSector> additionalSectors = new List<PlanetSector>();
            foreach (Planet bonusPlanet in SelectBonusPlanets(game, provider, planet, sector))
            {
                PlanetSector bonusSector = bonusPlanet.GetParentOfType<PlanetSector>();
                recorder.RecordEspionageSnapshot(
                    faction,
                    bonusPlanet,
                    bonusSector,
                    game.CurrentTick
                );

                if (
                    bonusSector != null
                    && additionalSectors.All(candidate =>
                        candidate.InstanceID != bonusSector.InstanceID
                    )
                )
                    additionalSectors.Add(bonusSector);
            }

            if (additionalSectors.Count == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new PlanetSectorsRevealedResult
                {
                    Tick = game.CurrentTick,
                    MissionInstanceID = InstanceID,
                    SourceEventInstanceID = SourceEventInstanceID,
                    AdditionalSectors = additionalSectors,
                },
            };
        }

        /// <summary>
        /// Selects distinct bonus planets using the mission's target-specific pools.
        /// </summary>
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
