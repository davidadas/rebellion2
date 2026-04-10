using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Jedi training mission where a trainer trains a student in the Force.
/// The student (mission leader) gains ForceTrainingAdjustment based on the rank gap
/// to the trainer.
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

    /// <summary>
    /// Returns a new JediTrainingMission if an eligible trainer exists on an own planet, or null.
    /// Selects the highest-ranked available trainer automatically.
    /// </summary>
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
            null
        )
    {
        TrainerInstanceID = trainerInstanceId;
        DisplayName = "Jedi Training";
    }

    /// <summary>
    /// Cancels if any main participant or the trainer is captured or killed.
    /// </summary>
    public override bool IsCanceled(GameRoot game)
    {
        if (base.IsCanceled(game))
            return true;

        Officer trainer = game.GetSceneNodeByInstanceID<Officer>(TrainerInstanceID);
        return trainer?.IsCaptured != false || trainer?.IsKilled != false;
    }

    /// <summary>
    /// Validates that the target planet is still owned by this faction.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the planet is still owned by this faction.</returns>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }

    /// <summary>
    /// Success probability uses the student's ForceRank as the score.
    /// The original uses teacher.force_rank_level - trainee.force_rank_level as the gap,
    /// but that requires game access not available here. Full gap mechanic is applied in OnSuccess.
    /// </summary>
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
    /// Jedi training does not award mission skill improvements. Force progression
    /// is handled directly via ForceTrainingAdjustment in OnSuccess.
    /// </summary>
    protected override void ImproveMissionParticipantsSkill() { }

    /// <summary>
    /// On success, applies the training catch-up mechanic. The student gains
    /// ForceTrainingAdjustment based on the rank gap to the trainer.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for rolling the catch-up bonus.</param>
    /// <returns>Empty list; training results are applied directly.</returns>
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
    /// Training continues as long as the planet remains owned by this faction
    /// and the student has not yet reached force qualification.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if training should continue.</returns>
    public override bool CanContinue(GameRoot game)
    {
        if (!(GetParent() is Planet p) || p.GetOwnerInstanceID() != OwnerInstanceID)
            return false;

        int threshold = game.Config.Jedi.ForceQualifiedThreshold;
        bool allQualified = MainParticipants.OfType<Officer>().All(s => s.ForceRank >= threshold);

        return !allQualified;
    }
}
