using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

public class DiplomacyMission : Mission
{
    public DiplomacyMission()
        : base()
    {
        Name = "Diplomacy";
        DisplayName = Name;
        ParticipantSkill = MissionParticipantSkill.Diplomacy;
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
            RequirePlanetTarget(target, "Diplomacy").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Diplomacy,
            successProbabilityTable
        )
    {
        Planet planet = (Planet)target;

        if (!planet.IsColonized)
            throw new InvalidOperationException(
                $"Diplomacy target planet '{planet.DisplayName}' is not colonized."
            );

        if (planet.GetPopularSupport(ownerInstanceId) == 100)
            throw new InvalidOperationException(
                $"Diplomacy target planet '{planet.DisplayName}' already has maximum popular support."
            );

        string planetOwner = planet.GetOwnerInstanceID();
        if (planetOwner != null && planetOwner != ownerInstanceId)
            throw new InvalidOperationException(
                $"Diplomacy target planet '{planet.DisplayName}' is owned by another faction."
            );

        if (planet.IsInUprising)
            throw new InvalidOperationException(
                $"Diplomacy target planet '{planet.DisplayName}' is in an uprising."
            );
    }

    /// <summary>
    /// Returns false if the planet's state makes further diplomacy invalid at execution time.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p
            && p.IsColonized
            && !p.IsInUprising
            && (p.GetOwnerInstanceID() == null || p.GetOwnerInstanceID() == OwnerInstanceID)
            && p.GetPopularSupport(OwnerInstanceID) < 100;
    }

    /// <summary>
    /// Increments popular support and emits a PlanetOwnershipChangedResult when support
    /// crosses 60 and the planet is not yet owned by this faction.
    /// </summary>
    protected override List<GameResult> OnSuccess(
        GameRoot game,
        IRandomNumberProvider provider
    )
    {
        Planet planet = GetParent() as Planet;
        if (planet == null)
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

        // Emit an ownership-change result when support crosses 60 and planet isn't already ours.
        // MissionSystem will call OwnershipSystem.TransferPlanet to action the transfer.
        string previousOwner = planet.GetOwnerInstanceID();
        if (planet.GetPopularSupport(OwnerInstanceID) > 60 && previousOwner != OwnerInstanceID)
        {
            GameLogger.Log($"Planet {planet.InstanceID} ownership changed to {OwnerInstanceID}.");
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

    /// <summary>
    /// Diplomacy missions are never foiled — they target own or neutral planets.
    /// </summary>
    protected override double GetFoilProbability(double defenseScore) => 0;

    /// <summary>
    /// Extends base cancellation to also cancel when the target planet enters uprising.
    /// </summary>
    public override bool IsCanceled(GameRoot game)
    {
        return base.IsCanceled(game) || (GetParent() is Planet planet && planet.IsInUprising);
    }

    public override void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        base.Configure(tables);
        SuccessProbabilityTable = new ProbabilityTable(tables.Diplomacy);
        BaseTicks = tables.TickRanges.Diplomacy.Base;
        SpreadTicks = tables.TickRanges.Diplomacy.Spread;
    }

    /// <summary>
    /// Returns true while the planet remains eligible for further diplomacy attempts.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        if (GetParent() is Planet planet)
        {
            return (
                    planet.GetOwnerInstanceID() == GetOwnerInstanceID()
                    || planet.GetOwnerInstanceID() == null
                )
                && planet.GetPopularSupport(GetOwnerInstanceID()) < 100
                && !planet.IsInUprising;
        }
        return false;
    }
}
