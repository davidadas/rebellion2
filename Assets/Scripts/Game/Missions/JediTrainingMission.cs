using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Jedi training mission where a teacher trains a student in the Force.
/// The student (mission leader) gains ForceTrainingAdjustment based on the rank gap
/// to the teacher.
/// </summary>
public class JediTrainingMission : Mission
{
    public string TeacherInstanceID { get; set; }

    public JediTrainingMission()
        : base()
    {
        ConfigKey = "JediTraining";
        DisplayName = "Jedi Training";
        ParticipantSkill = MissionParticipantSkill.Diplomacy;
    }

    public JediTrainingMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        string teacherInstanceId
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
        TeacherInstanceID = teacherInstanceId;

        Planet planet = (Planet)target;
        if (planet.GetOwnerInstanceID() != ownerInstanceId)
            throw new InvalidOperationException(
                $"Jedi Training target planet '{planet.DisplayName}' is not owned by this faction."
            );
    }

    /// <summary>
    /// Success probability is based on the student's jedi probability value.
    /// </summary>
    protected override double GetAgentProbability(IMissionParticipant agent)
    {
        if (agent is Officer officer)
            return officer.JediProbability;
        return 0;
    }

    /// <summary>
    /// Jedi training targets own planets and is never foiled.
    /// </summary>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// On success, applies the training catch-up mechanic.
    /// </summary>
    protected override List<GameResult> OnSuccess(
        GameRoot game,
        IRandomNumberProvider provider
    )
    {
        Officer student = MainParticipants.OfType<Officer>().FirstOrDefault();
        Officer teacher = game.GetSceneNodeByInstanceID<Officer>(TeacherInstanceID);

        if (student == null || teacher == null)
            return new List<GameResult>();

        if (!student.IsForceEligible || !teacher.IsForceEligible)
            return new List<GameResult>();

        if (student.ForceRank < teacher.ForceRank)
        {
            int gap = teacher.ForceRank - student.ForceRank;
            int catchUpRange = gap * game.Config.Jedi.TrainingCatchUpPercent / 100;

            if (catchUpRange > 0)
            {
                int bonus = provider.NextInt(0, catchUpRange + 1);
                student.ForceTrainingAdjustment += bonus;

                GameLogger.Log(
                    $"{student.GetDisplayName()} gained {bonus} training adjustment from {teacher.GetDisplayName()} (rank {student.ForceRank})"
                );
            }
        }

        return new List<GameResult>();
    }

    /// <summary>
    /// Training continues as long as the planet remains owned by this faction.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }

    /// <summary>
    /// Validates that the target planet is still owned by this faction.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.GetOwnerInstanceID() == OwnerInstanceID;
    }
}
