using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

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
        /// Returns a new JediTrainingMission if an eligible trainer exists on an own planet, or null.
        /// Selects the highest-ranked available trainer automatically.
        /// </summary>
        /// <param name="ctx">Mission context providing owner, target planet, and participants.</param>
        /// <returns>A configured mission, or null if the planet is not owned by this faction or no eligible trainer is available.</returns>
        public static JediTrainingMission TryCreate(MissionContext ctx)
        {
            if (!(ctx.Location is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            if (!TryGetTrainingStudents(ctx.MainParticipants, out List<Officer> students))
                return null;

            Officer trainer = SelectTrainer(ctx.Game, ctx.OwnerInstanceId, planet, students);
            if (
                trainer == null
                || !students.All(student =>
                    CanStudentImproveWithTrainer(student, trainer, ctx.Game)
                )
            )
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
        /// Selects the strongest eligible trainer on the target planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="ownerInstanceId">Faction requesting training.</param>
        /// <param name="planet">Planet where training would occur.</param>
        /// <param name="students">The officers selected as training students.</param>
        /// <returns>The selected trainer, or null if none are eligible.</returns>
        private static Officer SelectTrainer(
            GameRoot game,
            string ownerInstanceId,
            Planet planet,
            IEnumerable<Officer> students
        )
        {
            HashSet<string> studentIds = new HashSet<string>(
                students.Select(student => student.InstanceID)
            );
            return game.GetSceneNodesByType<Officer>()
                .Where(o =>
                    o.GetOwnerInstanceID() == ownerInstanceId
                    && o.IsJedi
                    && o.IsJediTrainer
                    && o.IsForceEligible
                    && o.GetParentOfType<Planet>() == planet
                    && !o.IsCaptured
                    && !o.IsKilled
                    && !o.IsOnMission()
                    && !studentIds.Contains(o.InstanceID)
                )
                .OrderByDescending(o => o.ForceRank)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns every selected officer when all selected participants can receive Jedi training.
        /// </summary>
        /// <param name="participants">Selected mission participants to train.</param>
        /// <param name="students">The selected Jedi students.</param>
        /// <returns>True when at least one eligible Jedi student was selected and no ineligible participants were selected.</returns>
        private static bool TryGetTrainingStudents(
            List<IMissionParticipant> participants,
            out List<Officer> students
        )
        {
            students = new List<Officer>();
            if (participants == null || participants.Count == 0)
                return false;

            foreach (IMissionParticipant participant in participants)
            {
                if (
                    participant is not Officer student
                    || student.IsJedi != true
                    || !student.IsForceEligible
                    || student.IsCaptured
                    || student.IsKilled
                )
                    return false;

                students.Add(student);
            }

            return true;
        }

        /// <summary>
        /// Returns whether the selected trainer can improve the selected student.
        /// </summary>
        /// <param name="student">The student to inspect.</param>
        /// <param name="trainer">The trainer to inspect.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>True when the trainer can improve the student's Force rank.</returns>
        private static bool CanStudentImproveWithTrainer(
            Officer student,
            Officer trainer,
            GameRoot game
        )
        {
            return student?.IsJedi == true
                && student.IsForceEligible
                && !student.IsCaptured
                && !student.IsKilled
                && trainer?.IsForceEligible == true
                && !trainer.IsCaptured
                && !trainer.IsKilled
                && student.ForceRank < trainer.ForceRank
                && student.ForceRank < game.Config.Jedi.ForceQualifiedThreshold;
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

            Officer trainer = game.GetSceneNodeByInstanceID<Officer>(TrainerInstanceID);
            if (trainer == null)
                return MissionCompletionReason.Failure;

            return trainer.IsCaptured || trainer.IsKilled ? MissionCompletionReason.Failure : null;
        }

        /// <summary>
        /// Returns the student's training success probability.
        /// </summary>
        /// <param name="agent">The mission participant (student) to evaluate.</param>
        /// <param name="game">The current game state.</param>
        /// <returns>The student's training success probability.</returns>
        protected override double GetAgentProbability(IMissionParticipant agent, GameRoot game)
        {
            if (agent is Officer officer)
                return LookupSuccessProbability(game, officer.ForceRank);
            return 0;
        }

        /// <summary>
        /// Jedi training targets own planets and is never foiled.
        /// </summary>
        /// <param name="defenseScore">The defense score, unused because training cannot be foiled.</param>
        /// <param name="game">The current game state, unused because training cannot be foiled.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore, GameRoot game) => 0;

        /// <summary>
        /// Jedi training does not award mission rating improvements.
        /// </summary>
        protected override void ImproveMissionParticipantRatings() { }

        /// <summary>
        /// Applies training progress to eligible students.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">RNG provider for training progress.</param>
        /// <returns>Results for students who gained training progress.</returns>
        protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            Officer trainer = game.GetSceneNodeByInstanceID<Officer>(TrainerInstanceID);

            if (trainer?.IsForceEligible != true)
                return results;

            foreach (Officer student in MainParticipants.OfType<Officer>())
            {
                if (!CanStudentImproveWithTrainer(student, trainer, game))
                    continue;

                int gap = trainer.ForceRank - student.ForceRank;
                int catchUpRange = gap * game.Config.Jedi.TrainingCatchUpPercent / 100;

                if (catchUpRange <= 0)
                    continue;

                int bonus = provider.NextInt(0, catchUpRange + 1);
                student.ForceTrainingAdjustment += bonus;

                results.Add(
                    new ForceTrainingResult
                    {
                        Officer = student,
                        Progress = bonus,
                        Detail = trainer.ForceRank,
                        Tick = game.CurrentTick,
                    }
                );

                GameLogger.Log(
                    $"{student.GetDisplayName()} gained {bonus} training adjustment from {trainer.GetDisplayName()} (rank {student.ForceRank})"
                );
            }

            return results;
        }

        /// <summary>
        /// Returns true after completion while at least one student still needs training.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if training should repeat.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game)
        {
            if (GetParent() is not Planet planet || planet.GetOwnerInstanceID() != OwnerInstanceID)
                return false;

            Officer trainer = game.GetSceneNodeByInstanceID<Officer>(TrainerInstanceID);
            return MainParticipants
                .OfType<Officer>()
                .Any(student => CanStudentImproveWithTrainer(student, trainer, game));
        }
    }
}
