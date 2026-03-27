using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
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
        MinTicks = 15;
        MaxTicks = 20;
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
        {
            throw new ArgumentNullException(nameof(target), "The target cannot be null.");
        }

        if (!(target is Planet))
        {
            throw new InvalidSceneOperationException(
                $"The target must be a planet. Target type: {target.GetType().Name}"
            );
        }

        Planet planet = (Planet)target;

        if (!planet.IsColonized)
        {
            throw new InvalidSceneOperationException(
                $"The target planet '{planet.DisplayName}' cannot perform diplomacy. The planet is not colonized."
            );
        }

        if (planet.GetPopularSupport(ownerInstanceId) == 100)
        {
            throw new InvalidSceneOperationException(
                $"The target planet '{planet.DisplayName}' already has maximum popular support."
            );
        }

        string planetOwner = planet.GetOwnerInstanceID();
        if (planetOwner != null && planetOwner != ownerInstanceId)
        {
            throw new InvalidSceneOperationException(
                $"The target planet '{planet.DisplayName}' is owned by another faction and cannot perform diplomacy."
            );
        }

        if (planet.IsInUprising)
        {
            throw new InvalidSceneOperationException(
                $"The target planet '{planet.DisplayName}' is in an uprising and cannot perform diplomacy."
            );
        }
    }

    /// <summary>
    /// Increases the planetary support for the owning faction.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">Random number provider (not used in this mission).</param>
    protected override void OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        if (GetParent() is Planet planet)
        {
            int newSupport = planet.GetPopularSupport(OwnerInstanceID) + 1;
            game.SetPlanetPopularSupport(planet, OwnerInstanceID, newSupport);

            // If the popular support is greater than 60, set faction as owner.
            if (planet.GetPopularSupport(OwnerInstanceID) > 60)
            {
                GameLogger.Log($"{planet.GetDisplayName()} has joined {GetOwnerInstanceID()}.");
                game.ChangeUnitOwnership(this.GetParent(), this.GetOwnerInstanceID());
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    public override bool CanContinue(GameRoot game)
    {
        if (GetParent() is Planet planet)
        {
            // Only continue if the planet is owned by the player or unowned.
            if (
                planet.GetOwnerInstanceID() == GetOwnerInstanceID()
                || planet.GetOwnerInstanceID() == null
            )
            {
                GameLogger.Log(
                    $"{MainParticipants[0].GetDisplayName()} has increased popular support on {planet.GetDisplayName()}."
                );
                return planet.GetPopularSupport(GetOwnerInstanceID()) <= 100;
            }
        }
        return false;
    }
}
