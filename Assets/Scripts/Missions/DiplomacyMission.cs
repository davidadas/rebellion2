using System;
using System.Collections.Generic;

public class DiplomacyMission : Mission
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public DiplomacyMission() : base()
    // @TODO: Move the success probability variables to configs.
    {
        Name = "Diplomacy";
        ParticipantSkill = MissionParticipantSkill.Diplomacy;
        QuadraticCoefficient = 0.005558;
        LinearCoefficient = 0.7656;
        ConstantTerm = 20.15;
        MinSuccessProbability = 1;
        MaxSuccessProbability = 100;
        MinTicks = 15;
        MaxTicks = 20;
    }

    /// <summary>
    /// Constructor used when initializing the DiplomacyMission with participants and owner.
    /// </summary>
    public DiplomacyMission(
        string ownerTypeID,
        List<MissionParticipant> mainParticipants,
        List<MissionParticipant> covertParticipants
    // @TODO: Move the success probability variables to configs.
    ) : base(
        "Diplomacy", 
        ownerTypeID, 
        mainParticipants, 
        covertParticipants, 
        MissionParticipantSkill.Diplomacy, 
        quadraticCoefficient: 0.005558, 
        linearCoefficient: 0.7656, 
        constantTerm: 20.15, 
        minSuccessProbability: 1, 
        maxSuccessProbability: 100, 
        minTicks: 15, 
        maxTicks: 20) { }

    /// <summary>
    /// Increases the planetary support for the owning faction.
    /// </summary>
    protected override void OnSuccess(Game game)
    {
        if (GetParent() is Planet planet)
        {
            planet.SetPopularSupport(OwnerTypeID, planet.GetPopularSupport(OwnerTypeID) + 1);
        }
    }
}