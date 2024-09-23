using System;
using System.Collections.Generic;
using IEnumerableExtensions;

public class RecruitmentMission : Mission
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public RecruitmentMission() : base()
    // @TODO: Move the success probability variables to configs.
    {
        Name = "Recruitment";
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
    /// Creates a new RecruitmentMission with the specified owner and participants.
    /// </summary>
    public RecruitmentMission(
        string ownerGameID,
        List<MissionParticipant> mainParticipants,
        List<MissionParticipant> covertParticipants
    // @TODO: Move the success probability variables to configs.
    ) : base(
        "Recruitment", 
        ownerGameID,
        mainParticipants, 
        covertParticipants, 
        MissionParticipantSkill.Leadership, 
        quadraticCoefficient: -0.001748, 
        linearCoefficient: 0.8657, 
        constantTerm: 11.923, 
        minSuccessProbability: 1, 
        maxSuccessProbability: 100, 
        minTicks: 15, 
        maxTicks: 20)
    {
    }

    /// <summary>
    /// Adds a new officer to the factions's roster.
    /// </summary>
    /// <param name="game"></param>
    protected override void OnSuccess(Game game)
    {
        Planet planet = GetParent() as Planet;

        List<Officer> unrecruitedOfficers = game.GetUnrecruitedOfficers(OwnerGameID);
        Officer recruitedOfficer = unrecruitedOfficers.RandomElement();

        game.RemoveUnrecruitedOfficer(recruitedOfficer);

        // Attach the recruited officer to the planet.
        game.AttachNode(planet, recruitedOfficer);
    }
}
