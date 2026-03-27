using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class AbductionMission : Mission
{
    public Officer TargetOfficer { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public AbductionMission()
        : base()
    // @TODO: Move the success probability variables to configs.
    {
        Name = "Abduction";
        ParticipantSkill = MissionParticipantSkill.Combat;
        QuadraticCoefficient = 0.005558;
        LinearCoefficient = 0.7656;
        ConstantTerm = 20.15;
        MinSuccessProbability = 1;
        MaxSuccessProbability = 100;
        MinTicks = 15;
        MaxTicks = 20;
    }

    public AbductionMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        Officer targetOfficer,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Abduction",
            ownerInstanceId,
            target.GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            successProbabilityTable,
            quadraticCoefficient: 0.0002622,
            linearCoefficient: 0.4955,
            constantTerm: 49.76,
            minSuccessProbability: 1,
            maxSuccessProbability: 100,
            minTicks: 15,
            maxTicks: 20
        )
    {
        TargetOfficer = targetOfficer;
    }

    /// <summary>
    /// Handles successful abduction mission.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">Random number provider (not used in this mission).</param>
    protected override void OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        // Logic for mission success
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
