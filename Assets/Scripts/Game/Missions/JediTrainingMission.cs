using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Jedi training mission where a trainer trains a student in the Force.
    /// </summary>
    public class JediTrainingMission : Mission
    {
        public const string MissionTypeID = "JediTraining";

        /// <summary>
        /// Instance ID of the officer selected as the trainer.
        /// </summary>
        public string TrainerInstanceID { get; set; }

        /// <summary>
        /// Gets the selected trainer from the mission's current participants.
        /// </summary>
        [PersistableIgnore]
        public Officer Trainer =>
            MainParticipants
                .OfType<Officer>()
                .FirstOrDefault(officer => officer.InstanceID == TrainerInstanceID);

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        public JediTrainingMission()
            : base()
        {
            ConfigKey = MissionTypeID;
            DisplayName = "Jedi Training";
        }

        /// <summary>
        /// Initializes a Jedi training mission with the selected trainer.
        /// </summary>
        /// <param name="ownerInstanceId">Faction that owns the mission.</param>
        /// <param name="target">Planet where the mission occurs.</param>
        /// <param name="mainParticipants">Primary mission participants.</param>
        /// <param name="decoyParticipants">Decoy mission participants.</param>
        /// <param name="trainerInstanceId">Officer selected as the trainer.</param>
        private JediTrainingMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string trainerInstanceId
        )
            : base(
                MissionTypeID,
                ownerInstanceId,
                RequirePlanetTarget(target, "Jedi Training").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                OfficerRating.Diplomacy,
                displayName: "Jedi Training"
            )
        {
            TrainerInstanceID = trainerInstanceId;
        }

        /// <summary>
        /// Returns a new JediTrainingMission if the selected team contains an eligible trainer and student on an own planet, or null.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is not owned by this faction or no eligible trainer is available.</returns>
        public static JediTrainingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            if (!TryGetTrainingTeam(ctx.MainParticipants, ctx.Game, out Officer trainer))
                return null;

            return new JediTrainingMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                trainer.InstanceID
            );
        }

        /// <summary>
        /// Validates the selected participants and chooses the highest-ranked qualified trainer.
        /// </summary>
        /// <param name="participants">The officers selected for Jedi training.</param>
        /// <param name="game">The game state containing Jedi qualification thresholds.</param>
        /// <param name="trainer">The selected trainer when the team is valid.</param>
        /// <returns>True when every participant can train under the selected trainer.</returns>
        private static bool TryGetTrainingTeam(
            List<IMissionParticipant> participants,
            GameRoot game,
            out Officer trainer
        )
        {
            trainer = null;
            if (participants == null || participants.Count < 2 || game?.Config?.Jedi == null)
                return false;

            List<Officer> officers = new List<Officer>();
            foreach (IMissionParticipant participant in participants)
            {
                if (participant is not Officer officer || !CanParticipateInTraining(officer))
                    return false;

                officers.Add(officer);
            }

            Officer selectedTrainer = officers
                .Where(officer => CanLeadTraining(officer, game))
                .OrderByDescending(officer => officer.ForceRank)
                .FirstOrDefault();
            if (selectedTrainer == null)
                return false;

            trainer = selectedTrainer;
            return officers
                .Where(officer => officer != selectedTrainer)
                .All(officer => officer.ForceRank < selectedTrainer.ForceRank);
        }

        /// <summary>
        /// Returns whether an officer can participate in Jedi training.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <returns>True when the officer is a known, active Jedi.</returns>
        private static bool CanParticipateInTraining(Officer officer)
        {
            return officer?.IsJedi == true
                && officer.IsForceEligible
                && !officer.IsCaptured
                && !officer.IsKilled;
        }

        /// <summary>
        /// Returns whether an officer is qualified to lead Jedi training.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="game">The game state containing the trainer rank threshold.</param>
        /// <returns>True when the officer can participate and qualifies as a trainer.</returns>
        internal static bool CanLeadTraining(Officer officer, GameRoot game)
        {
            return game != null
                && CanParticipateInTraining(officer)
                && officer.IsJediTrainer
                && officer.ForceRank >= game.Config.Jedi.ForceQualifiedThreshold;
        }

        /// <summary>
        /// Returns why Jedi training must stop before advancing.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>The abort reason, or null when training may advance.</returns>
        public override MissionCompletionReason? GetAbortReason(GameRoot game)
        {
            MissionCompletionReason? reason = base.GetAbortReason(game);
            if (reason.HasValue)
                return reason;

            if (GetParent() is not Planet planet || planet.GetOwnerInstanceID() != OwnerInstanceID)
                return MissionCompletionReason.TargetUnavailable;

            Officer trainer = Trainer;
            if (!CanLeadTraining(trainer, game))
                return MissionCompletionReason.Failure;

            int officerCount = MainParticipants.OfType<Officer>().Count();
            return
                officerCount == MainParticipants.Count
                && officerCount >= 2
                && MainParticipants.OfType<Officer>().All(CanParticipateInTraining)
                ? null
                : MissionCompletionReason.Failure;
        }

        /// <summary>
        /// Jedi training targets own planets and is never foiled.
        /// </summary>
        /// <param name="defenseScore">The defense score, unused because training cannot be foiled.</param>
        /// <param name="game">The current game state, unused because training cannot be foiled.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <summary>
        /// Resolves one training attempt for each selected officer and completes the mission.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used for training rolls.</param>
        /// <returns>The training progress results followed by the mission completion result.</returns>
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            Officer trainer = Trainer;

            if (trainer != null)
            {
                foreach (Officer officer in MainParticipants.OfType<Officer>())
                {
                    ForceTrainingResult trainingResult = TrainOfficer(
                        officer,
                        trainer,
                        game,
                        provider
                    );
                    if (trainingResult != null)
                        results.Add(trainingResult);
                }
            }

            MissionOutcome outcome =
                results.Count > 0 ? MissionOutcome.Success : MissionOutcome.Failed;
            results.Add(BuildCompletedResult(outcome, game));
            return results;
        }

        /// <summary>
        /// Attempts to improve one officer's Force training adjustment toward the trainer's rank.
        /// </summary>
        /// <param name="officer">The officer receiving training.</param>
        /// <param name="trainer">The officer leading the training.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random number provider used for the training rolls.</param>
        /// <returns>The training result when progress was made; otherwise, null.</returns>
        private static ForceTrainingResult TrainOfficer(
            Officer officer,
            Officer trainer,
            GameRoot game,
            IRandomNumberProvider provider
        )
        {
            int forceRankGap = trainer.ForceRank - officer.ForceRank;
            if (provider.NextInt(0, 100) >= forceRankGap)
                return null;

            int catchUpRange = forceRankGap * game.Config.Jedi.TrainingCatchUpPercent / 100;
            int bonus = provider.NextInt(0, catchUpRange + 1);
            if (bonus <= 0)
                return null;

            officer.ForceTrainingAdjustment += bonus;
            GameLogger.Log(
                $"{officer.GetDisplayName()} gained {bonus} training adjustment from {trainer.GetDisplayName()} (rank {officer.ForceRank})"
            );

            return new ForceTrainingResult
            {
                Officer = officer,
                Progress = bonus,
                Detail = trainer.ForceRank,
                Tick = game.CurrentTick,
            };
        }

        /// <summary>
        /// Returns whether Jedi training should repeat after completion.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Always false because Jedi training completes after one execution.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;
    }
}
