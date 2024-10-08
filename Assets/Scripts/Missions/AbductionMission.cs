using System;
using System.Collections.Generic;

public class AbductionMission : Mission
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public AbductionMission() : base()
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

    /// <summary>
    /// Constructor used when initializing the AbductionMission with participants and owner.
    /// </summary>
    public AbductionMission(
        string ownerGameID,
        List<MissionParticipant> mainParticipants,
        List<MissionParticipant> covertParticipants
    // @TODO: Move the success probability variables to configs.
    ) : base(
        "Abduction", 
        ownerGameID, 
        mainParticipants, 
        covertParticipants, 
        MissionParticipantSkill.Combat, 
        quadraticCoefficient: 0.0002622, 
        linearCoefficient: 0.4955, 
        constantTerm: 49.76, 
        minSuccessProbability: 1, 
        maxSuccessProbability: 100, 
        minTicks: 15, 
        maxTicks: 20)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void OnSuccess(Game game)
    {
        // Logic for mission success
    }
}
