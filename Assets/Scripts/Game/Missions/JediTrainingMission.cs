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

            if (!AreStudentsEligible(ctx.MainParticipants))
                return null;

            string trainerId = SelectTrainer(ctx.Game, ctx.OwnerInstanceId, planet);
            if (trainerId == null)
                return null;

            return new JediTrainingMission(
                ctx.OwnerInstanceId,
                ctx.Location,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                trainerId
            );
        }

        /// <summary>
        /// Selects the strongest eligible trainer on the target planet.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="ownerInstanceId">Faction requesting training.</param>
        /// <param name="planet">Planet where training would occur.</param>
        /// <returns>The selected trainer instance ID, or null if none are eligible.</returns>
        private static string SelectTrainer(GameRoot game, string ownerInstanceId, Planet planet)
        {
            Officer trainer = game.GetSceneNodesByType<Officer>()
                .Where(o =>
                    o.GetOwnerInstanceID() == ownerInstanceId
                    && o.IsJedi
                    && o.IsJediTrainer
                    && o.IsForceEligible
                    && o.GetParentOfType<Planet>() == planet
                    && !o.IsCaptured
                    && !o.IsOnMission()
                )
                .OrderByDescending(o => o.ForceRank)
                .FirstOrDefault();
            return trainer?.InstanceID;
        }

        /// <summary>
        /// Returns whether every selected student can receive Jedi training.
        /// </summary>
        /// <param name="students">Selected mission participants to train.</param>
        /// <returns>True when at least one eligible Jedi student was selected and no ineligible participants were selected.</returns>
        private static bool AreStudentsEligible(List<IMissionParticipant> students)
        {
            if (students == null || students.Count == 0)
                return false;

            return students.All(participant =>
                participant is Officer { IsJedi: true, IsForceEligible: true }
            );
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
                if (!student.IsForceEligible)
                    continue;

                if (student.ForceRank >= trainer.ForceRank)
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

            int threshold = game.Config.Jedi.ForceQualifiedThreshold;
            return MainParticipants.OfType<Officer>().Any(s => s.ForceRank < threshold);
        }
    }
}
