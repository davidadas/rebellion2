using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public class RecruitmentMission : Mission
{
    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public RecruitmentMission()
        : base()
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

    public RecruitmentMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Recruitment",
            ownerInstanceId,
            target.GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Leadership,
            successProbabilityTable,
            quadraticCoefficient: -0.001748,
            linearCoefficient: 0.8657,
            constantTerm: 11.923,
            minSuccessProbability: 1,
            maxSuccessProbability: 100,
            minTicks: 15,
            maxTicks: 20
        ) { }

    /// <summary>
    /// Adds a new officer to the faction's roster.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">Random number provider for officer selection.</param>
    protected override void OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Planet planet = GetParent() as Planet;

        if (game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0)
        {
            List<Officer> unrecruitedOfficers = game.GetUnrecruitedOfficers(OwnerInstanceID);
            Officer recruitedOfficer = unrecruitedOfficers.RandomElement(provider);
            recruitedOfficer.OwnerInstanceID = OwnerInstanceID;

            game.RemoveUnrecruitedOfficer(recruitedOfficer);

            // Attach the recruited officer to the planet.
            game.AttachNode(recruitedOfficer, planet);

            GameLogger.Log(
                "Recruited officer "
                    + recruitedOfficer.GetDisplayName()
                    + " to "
                    + planet.GetDisplayName()
                    + " by "
                    + MainParticipants[0].GetDisplayName()
            );
        }
        else
        {
            // @TODO: No one left to recruit so abort the mission.
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public override bool CanContinue(GameRoot game)
    {
        return game.GetUnrecruitedOfficers(OwnerInstanceID).Count > 0;
    }
}
