using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
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
        /// <returns>A result identifying any additional sectors revealed by the mission.</returns>
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

            List<PlanetSector> additionalSectors = new List<PlanetSector>();
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
        /// Returns whether the target belongs to a faction other than the mission owner.
        /// Neutral and owner-controlled planets still produce their direct intelligence snapshot,
        /// but do not grant the original game's additional-system bonus.
        /// </summary>
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
}
