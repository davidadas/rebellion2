using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class DiplomacyMission : Mission
{
    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public DiplomacyMission()
        : base()
    // @TODO: Move the success probability variables to configs.
    {
        Name = "Diplomacy";
        ParticipantSkill = MissionParticipantSkill.Diplomacy;
        QuadraticCoefficient = 0.005558;
        LinearCoefficient = 0.7656;
        ConstantTerm = 20.15;
        MinSuccessProbability = 1;
        MaxSuccessProbability = 100;
        MinTicks = 5;
        MaxTicks = 10;
    }

    public DiplomacyMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Diplomacy",
            ownerInstanceId,
            target.GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Diplomacy,
            successProbabilityTable,
            quadraticCoefficient: 0.005558,
            linearCoefficient: 0.7656,
            constantTerm: 20.15,
            minSuccessProbability: 1,
            maxSuccessProbability: 100,
            minTicks: 15,
            maxTicks: 20
        )
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target), "The target cannot be null.");

        if (!(target is Planet))
            throw new InvalidOperationException(
                $"The target must be a planet. Target type: {target.GetType().Name}"
            );

        Planet planet = (Planet)target;

        if (!planet.IsColonized)
            throw new InvalidOperationException(
                $"The target planet '{planet.DisplayName}' cannot perform diplomacy. The planet is not colonized."
            );

        if (planet.GetPopularSupport(ownerInstanceId) == 100)
            throw new InvalidOperationException(
                $"The target planet '{planet.DisplayName}' already has maximum popular support."
            );

        string planetOwner = planet.GetOwnerInstanceID();
        if (planetOwner != null && planetOwner != ownerInstanceId)
            throw new InvalidOperationException(
                $"The target planet '{planet.DisplayName}' is owned by another faction and cannot perform diplomacy."
            );

        if (planet.IsInUprising)
            throw new InvalidOperationException(
                $"The target planet '{planet.DisplayName}' is in an uprising and cannot perform diplomacy."
            );
    }

    protected override List<GameResult> OnSuccess(GameRoot game)
    {
        if (!(GetParent() is Planet planet))
            return new List<GameResult>();

        List<GameResult> results = new List<GameResult>();

        int currentSupport = planet.GetPopularSupport(OwnerInstanceID);
        int increment = 1;

        if (SuccessProbabilityTable != null && MainParticipants.Count > 0)
        {
            Officer officer = MainParticipants[0] as Officer;
            if (officer != null)
            {
                int score =
                    (int)officer.CurrentRank
                    - currentSupport
                    + officer.Skills[MissionParticipantSkill.Diplomacy];
                int tableValue = SuccessProbabilityTable.Lookup(score);
                if (tableValue > 0)
                    increment = tableValue;
            }
        }

        int newSupport = Math.Min(100, currentSupport + increment);
        game.SetPlanetPopularSupport(planet, OwnerInstanceID, newSupport);

        // Ownership changes only when support crosses 60 and the planet isn't already ours
        string previousOwner = planet.GetOwnerInstanceID();
        if (planet.GetPopularSupport(OwnerInstanceID) > 60 && previousOwner != OwnerInstanceID)
        {
            game.ChangeUnitOwnership(planet, OwnerInstanceID);

            results.Add(
                new PlanetOwnershipChangedResult
                {
                    PlanetInstanceID = planet.InstanceID,
                    PreviousOwnerInstanceID = previousOwner,
                    NewOwnerInstanceID = OwnerInstanceID,
                    Tick = game.CurrentTick,
                }
            );
        }

        return results;
    }

    protected override double GetFoilProbability(double defenseScore) => 0;

    public override bool CanContinue(GameRoot game)
    {
        if (GetParent() is Planet planet)
        {
            return (
                    planet.GetOwnerInstanceID() == GetOwnerInstanceID()
                    || planet.GetOwnerInstanceID() == null
                )
                && planet.GetPopularSupport(GetOwnerInstanceID()) < 100;
        }
        return false;
    }
}
