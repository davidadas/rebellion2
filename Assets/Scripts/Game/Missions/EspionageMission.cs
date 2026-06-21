using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class EspionageMission : Mission
    {
        public override bool CanceledOnOwnershipChange => false;
        internal override bool CanLoseParticipantsWhenFoiled => false;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public EspionageMission()
            : base()
        {
            ConfigKey = "Espionage";
            DisplayName = ConfigKey;
            ParticipantRating = OfficerRating.Espionage;
            DecoyParticipantRating = OfficerRating.Espionage;
        }

        private EspionageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
            : base(
                "Espionage",
                ownerInstanceId,
                RequirePlanetTarget(target, "Espionage").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Espionage,
                null
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
            if (!(ctx.Target is Planet planet))
                return null;

            if (!planet.WasVisitedBy(ctx.OwnerInstanceId))
                return null;

            return new EspionageMission(
                ctx.OwnerInstanceId,
                ctx.Target,
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

        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            List<IMissionParticipant> successfulParticipants = new List<IMissionParticipant>();

            foreach (IMissionParticipant participant in MainParticipants)
            {
                double successThreshold = GetAgentProbability(participant);
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

        private bool CanImproveRatingsAgainstTarget()
        {
            return GetParent() is Planet planet && planet.GetOwnerInstanceID() != OwnerInstanceID;
        }

        /// <summary>
        /// Captures a fog-of-war snapshot of the target planet for the owning faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider (unused for espionage).</param>
        /// <returns>An empty list; the snapshot is applied directly to faction fog state.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            Planet planet = GetParent() as Planet;
            Faction faction = game?.GetFactionByOwnerInstanceID(OwnerInstanceID);
            PlanetSystem system = planet?.GetParentOfType<PlanetSystem>();

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(faction, planet, system, game?.CurrentTick ?? 0);

            return new List<GameResult>();
        }

        /// <summary>
        /// Espionage missions do not repeat — one attempt per mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false.</returns>
        public override bool CanContinue(GameRoot game)
        {
            return false;
        }
    }
}
