using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

/// <summary>
/// Contains the evaluated marker intensity and faction presentation for one visible planet.
/// </summary>
public readonly struct GalacticInformationMarker
{
    /// <summary>
    /// Creates an immutable evaluated marker result.
    /// </summary>
    /// <param name="index">The zero-based marker intensity.</param>
    /// <param name="factionInstanceId">The faction whose marker artwork is shown.</param>
    /// <param name="mixed">Whether mixed-faction marker artwork is shown.</param>
    public GalacticInformationMarker(int index, string factionInstanceId, bool mixed)
    {
        Index = index;
        FactionInstanceId = factionInstanceId;
        Mixed = mixed;
    }

    public int Index { get; }

    public string FactionInstanceId { get; }

    public bool Mixed { get; }
}

/// <summary>
/// Evaluates configured galactic-information values and thresholds for visible planets.
/// </summary>
public static class GalacticInformationFilterEvaluator
{
    private const int _lowestMarkerIndex = 0;
    private const int _lowMarkerIndex = 1;
    private const int _mediumMarkerIndex = 2;
    private const int _highestMarkerIndex = 3;

    /// <summary>
    /// Evaluates one visible planet for a configured galactic-information filter.
    /// </summary>
    /// <param name="game">The active game containing visible factions.</param>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="viewerFactionId">The viewing player's faction identifier.</param>
    /// <param name="filter">The configured filter and marker thresholds.</param>
    /// <returns>The evaluated marker presentation.</returns>
    public static GalacticInformationMarker Evaluate(
        GameRoot game,
        Planet planet,
        string viewerFactionId,
        GalacticInformationFilterTheme filter
    )
    {
        if (planet == null || filter == null)
            return new GalacticInformationMarker(_lowestMarkerIndex, null, false);

        return filter.Mode switch
        {
            GalacticInformationFilterMode.IdleFleets => EvaluateFactionCounts(
                game,
                planet,
                viewerFactionId,
                CountFleets(planet, false),
                filter
            ),
            GalacticInformationFilterMode.FleetsEnroute => EvaluateFactionCounts(
                game,
                planet,
                viewerFactionId,
                CountFleets(planet, true),
                filter
            ),
            GalacticInformationFilterMode.IdlePersonnel => EvaluateFactionCounts(
                game,
                planet,
                viewerFactionId,
                CountPersonnel(planet, true),
                filter
            ),
            GalacticInformationFilterMode.ActivePersonnel => EvaluateFactionCounts(
                game,
                planet,
                viewerFactionId,
                CountPersonnel(planet, false),
                filter
            ),
            _ => new GalacticInformationMarker(
                GetMarkerIndex(GetValue(planet, viewerFactionId, filter.Mode), filter),
                planet.OwnerInstanceID,
                false
            ),
        };
    }

    /// <summary>
    /// Gets the scalar value represented by a non-faction-count filter.
    /// </summary>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="viewerFactionId">The viewing player's faction identifier.</param>
    /// <param name="mode">The requested filter mode.</param>
    /// <returns>The scalar value evaluated against configured thresholds.</returns>
    private static int GetValue(
        Planet planet,
        string viewerFactionId,
        GalacticInformationFilterMode mode
    )
    {
        return mode switch
        {
            GalacticInformationFilterMode.PopularSupport => planet.GetPopularSupport(
                viewerFactionId
            ),
            GalacticInformationFilterMode.Uprisings => Convert.ToInt32(planet.IsInUprising),
            GalacticInformationFilterMode.AvailableEnergy => planet.GetAvailableEnergy(),
            GalacticInformationFilterMode.AvailableRawMaterial =>
                planet.GetUnminedResourceNodeCount(),
            GalacticInformationFilterMode.Mines => planet.GetBuildingTypeCount(BuildingType.Mine),
            GalacticInformationFilterMode.Refineries => planet.GetBuildingTypeCount(
                BuildingType.Refinery
            ),
            GalacticInformationFilterMode.Shipyards => planet.GetProductionFacilityCount(
                ManufacturingType.Ship
            ),
            GalacticInformationFilterMode.TrainingFacilities => planet.GetProductionFacilityCount(
                ManufacturingType.Troop
            ),
            GalacticInformationFilterMode.ConstructionYards => planet.GetProductionFacilityCount(
                ManufacturingType.Building
            ),
            GalacticInformationFilterMode.IdleShipyards => Convert.ToInt32(
                IsOwnedIdleManufacturingPlanet(planet, viewerFactionId, ManufacturingType.Ship)
            ),
            GalacticInformationFilterMode.IdleTrainingFacilities => Convert.ToInt32(
                IsOwnedIdleManufacturingPlanet(planet, viewerFactionId, ManufacturingType.Troop)
            ),
            GalacticInformationFilterMode.IdleConstructionYards => Convert.ToInt32(
                IsOwnedIdleManufacturingPlanet(planet, viewerFactionId, ManufacturingType.Building)
            ),
            GalacticInformationFilterMode.Troopers => planet.Regiments.Count(IsActive),
            GalacticInformationFilterMode.FighterSquadrons => planet.Starfighters.Count(IsActive),
            GalacticInformationFilterMode.DeathStarShields => Convert.ToInt32(
                planet.Buildings.Any(building =>
                    building.DefenseFacilityClass == DefenseFacilityClass.DeathStarShield
                    && IsActive(building)
                )
            ),
            GalacticInformationFilterMode.PlanetaryShieldGenerators => planet.Buildings.Count(
                building =>
                    building.DefenseFacilityClass == DefenseFacilityClass.Shield
                    && IsActive(building)
            ),
            GalacticInformationFilterMode.PlanetaryDefenseBatteries => planet.Buildings.Count(
                building =>
                    (
                        building.DefenseFacilityClass == DefenseFacilityClass.KDY
                        || building.DefenseFacilityClass == DefenseFacilityClass.LNR
                    ) && IsActive(building)
            ),
            _ => 0,
        };
    }

    /// <summary>
    /// Determines whether the viewer owns idle capacity of one manufacturing type.
    /// </summary>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="viewerFactionId">The viewing player's faction identifier.</param>
    /// <param name="type">The requested manufacturing type.</param>
    /// <returns>True when the viewer owns at least one idle matching facility.</returns>
    private static bool IsOwnedIdleManufacturingPlanet(
        Planet planet,
        string viewerFactionId,
        ManufacturingType type
    )
    {
        return string.Equals(planet.OwnerInstanceID, viewerFactionId, StringComparison.Ordinal)
            && planet.GetIdleManufacturingFacilities(type) > 0;
    }

    /// <summary>
    /// Counts stationary or enroute fleets by owning faction.
    /// </summary>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="enroute">Whether to count enroute fleets instead of stationary fleets.</param>
    /// <returns>Fleet counts keyed by faction identifier.</returns>
    private static Dictionary<string, int> CountFleets(Planet planet, bool enroute)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Fleet fleet in planet.Fleets)
        {
            if ((fleet.Movement != null) != enroute)
                continue;

            Increment(counts, fleet.OwnerInstanceID);
        }

        return counts;
    }

    /// <summary>
    /// Counts idle or active mission participants by owning faction.
    /// </summary>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="idle">Whether to count idle participants instead of active participants.</param>
    /// <returns>Participant counts keyed by faction identifier.</returns>
    private static Dictionary<string, int> CountPersonnel(Planet planet, bool idle)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (
            IMissionParticipant participant in planet.GetChildren<IMissionParticipant>(_ => true)
        )
        {
            if (IsIdle(participant) != idle)
                continue;

            Increment(counts, participant.GetOwnerInstanceID());
        }

        return counts;
    }

    /// <summary>
    /// Determines whether one visible mission participant is available and idle.
    /// </summary>
    /// <param name="participant">The visible mission participant.</param>
    /// <returns>True when the participant is available, stationary, and not assigned.</returns>
    private static bool IsIdle(IMissionParticipant participant)
    {
        if (participant == null || participant.Movement != null || participant.IsOnMission())
            return false;

        if (
            participant is Officer officer
            && (officer.IsCaptured || officer.IsKilled || officer.InjuryPoints > 0)
        )
            return false;

        return participant is not SpecialForces specialForces
            || specialForces.ManufacturingStatus == ManufacturingStatus.Complete;
    }

    /// <summary>
    /// Evaluates faction-count filters including mixed-faction marker presentation.
    /// </summary>
    /// <param name="game">The active game containing visible factions.</param>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="viewerFactionId">The viewing player's faction identifier.</param>
    /// <param name="counts">The evaluated entity counts keyed by faction identifier.</param>
    /// <param name="filter">The configured marker thresholds.</param>
    /// <returns>The evaluated marker presentation.</returns>
    private static GalacticInformationMarker EvaluateFactionCounts(
        GameRoot game,
        Planet planet,
        string viewerFactionId,
        IReadOnlyDictionary<string, int> counts,
        GalacticInformationFilterTheme filter
    )
    {
        int viewerCount = GetCount(counts, viewerFactionId);
        List<Faction> opposingFactions =
            game?.GetFactions()
                ?.Where(faction =>
                    faction != null
                    && !string.Equals(faction.InstanceID, viewerFactionId, StringComparison.Ordinal)
                )
                .ToList()
            ?? new List<Faction>();
        int opposingCount = opposingFactions.Sum(faction => GetCount(counts, faction.InstanceID));

        if (viewerCount > 0 && opposingCount > 0)
            return new GalacticInformationMarker(_highestMarkerIndex, viewerFactionId, true);

        if (viewerCount > 0)
            return new GalacticInformationMarker(
                GetMarkerIndex(viewerCount, filter),
                viewerFactionId,
                false
            );

        Faction opposingFaction = opposingFactions.FirstOrDefault(faction =>
            GetCount(counts, faction.InstanceID) > 0
        );
        if (opposingFaction != null)
            return new GalacticInformationMarker(
                GetMarkerIndex(opposingCount, filter),
                opposingFaction.InstanceID,
                false
            );

        return new GalacticInformationMarker(_lowestMarkerIndex, planet.OwnerInstanceID, false);
    }

    /// <summary>
    /// Maps one scalar value to a configured marker-intensity threshold.
    /// </summary>
    /// <param name="value">The evaluated scalar value.</param>
    /// <param name="filter">The configured marker thresholds.</param>
    /// <returns>The zero-based marker intensity.</returns>
    private static int GetMarkerIndex(int value, GalacticInformationFilterTheme filter)
    {
        if (value >= filter.HighThreshold)
            return _highestMarkerIndex;
        if (value >= filter.MediumThreshold)
            return _mediumMarkerIndex;
        if (value >= filter.LowThreshold)
            return _lowMarkerIndex;
        return _lowestMarkerIndex;
    }

    /// <summary>
    /// Reads one faction count without exposing missing-key handling to evaluation flow.
    /// </summary>
    /// <param name="counts">The evaluated counts keyed by faction identifier.</param>
    /// <param name="factionId">The requested faction identifier.</param>
    /// <returns>The configured count, or zero when the faction has no entry.</returns>
    private static int GetCount(IReadOnlyDictionary<string, int> counts, string factionId)
    {
        return !string.IsNullOrEmpty(factionId) && counts.TryGetValue(factionId, out int count)
            ? count
            : 0;
    }

    /// <summary>
    /// Increments one non-empty faction count.
    /// </summary>
    /// <param name="counts">The mutable counts keyed by faction identifier.</param>
    /// <param name="factionId">The faction identifier to increment.</param>
    private static void Increment(IDictionary<string, int> counts, string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
            return;

        counts[factionId] = counts.TryGetValue(factionId, out int count) ? count + 1 : 1;
    }

    /// <summary>
    /// Determines whether a manufacturable entity is complete and stationary.
    /// </summary>
    /// <param name="entity">The visible manufacturable entity.</param>
    /// <returns>True when the entity is complete and stationary.</returns>
    private static bool IsActive(IManufacturable entity)
    {
        return entity != null
            && entity.GetManufacturingStatus() == ManufacturingStatus.Complete
            && entity.Movement == null;
    }
}
