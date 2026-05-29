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
        public string TrainerInstanceID { get; set; }

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        public JediTrainingMission()
            : base()
        {
            ConfigKey = "JediTraining";
            DisplayName = "Jedi Training";
        }

        private JediTrainingMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            string trainerInstanceId
        )
            : base(
                "JediTraining",
                ownerInstanceId,
                RequirePlanetTarget(target, "Jedi Training").GetInstanceID(),
                mainParticipants,
                decoyParticipants,
                MissionParticipantSkill.Diplomacy,
                null,
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
            if (!(ctx.Target is Planet planet))
                return null;

            if (planet.GetOwnerInstanceID() != ctx.OwnerInstanceId)
                return null;

            string trainerId = SelectTrainer(ctx.Game, ctx.OwnerInstanceId, planet);
            if (trainerId == null)
                return null;

            return new JediTrainingMission(
                ctx.OwnerInstanceId,
                ctx.Target,
                ctx.MainParticipants,
                ctx.DecoyParticipants,
                trainerId
            );
        }

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
        /// Cancels if any main participant or the trainer is captured or killed.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if the mission should be aborted.</returns>
        public override bool ShouldAbort(GameRoot game)
        {
            if (base.ShouldAbort(game))
                return true;

            Officer trainer = game.GetSceneNodeByInstanceID<Officer>(TrainerInstanceID);
            if (trainer == null)
                return true;

            return trainer.IsCaptured || trainer.IsKilled;
        }

        /// <summary>
        /// Returns the student's training success probability.
        /// </summary>
        /// <param name="agent">The mission participant (student) to evaluate.</param>
        /// <returns>The student's training success probability.</returns>
        protected override double GetAgentProbability(IMissionParticipant agent)
        {
            if (agent is Officer officer)
                return SuccessProbabilityTable.Lookup(officer.ForceRank);
            return 0;
        }

        /// <summary>
        /// Jedi training targets own planets and is never foiled.
        /// </summary>
        /// <param name="defenseScore">Ignored.</param>
        /// <returns>Always 0.</returns>
        protected override double GetFoilProbability(double defenseScore) => 0;

        /// <summary>
        /// Jedi training does not award mission skill improvements.
        /// </summary>
        protected override void ImproveMissionParticipantsSkill() { }

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
        /// Returns true while at least one student has not yet reached force qualification.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if training should continue.</returns>
        public override bool CanContinue(GameRoot game)
        {
            int threshold = game.Config.Jedi.ForceQualifiedThreshold;
            return MainParticipants.OfType<Officer>().Any(s => s.ForceRank < threshold);
        }
    }
}
