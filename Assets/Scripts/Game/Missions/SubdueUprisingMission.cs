using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class SubdueUprisingMission : Mission
{
    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public SubdueUprisingMission()
        : base()
    {
        Name = "Subdue Uprising";
        ParticipantSkill = MissionParticipantSkill.Leadership;
        QuadraticCoefficient = 0.0;
        LinearCoefficient = 0.0;
        ConstantTerm = 0.0; // Probability comes from a probability table
        MinSuccessProbability = 1;
        MaxSuccessProbability = 100;
        MinTicks = 10;
        MaxTicks = 15;
    }

    public SubdueUprisingMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Subdue Uprising",
            ownerInstanceId,
            target.GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            successProbabilityTable,
            quadraticCoefficient: 0.0,
            linearCoefficient: 0.0,
            constantTerm: 0.0,
            minSuccessProbability: 1,
            maxSuccessProbability: 100,
            minTicks: 10,
            maxTicks: 15
        )
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target), "The target cannot be null.");

        if (!(target is Planet))
            throw new InvalidOperationException(
                $"The target must be a planet. Target type: {target.GetType().Name}"
            );

        Planet planet = (Planet)target;

        if (!planet.IsInUprising)
            throw new InvalidOperationException(
                $"The target planet '{planet.DisplayName}' is not in uprising."
            );

        if (planet.GetOwnerInstanceID() != ownerInstanceId)
            throw new InvalidOperationException(
                $"Cannot subdue uprising on planet owned by another faction."
            );
    }

    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        if (!(GetParent() is Planet planet) || !planet.IsInUprising)
            return new List<GameResult>();

        planet.EndUprising();

        return new List<GameResult>
        {
            new PlanetUprisingEndedResult
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = OwnerInstanceID,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Subdue uprising missions do not repeat.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
