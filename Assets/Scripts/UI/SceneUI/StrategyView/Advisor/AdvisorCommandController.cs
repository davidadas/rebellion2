using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

/// <summary>
/// Owns advisor construction targeting and routes valid destinations to construction windows.
/// </summary>
public sealed class AdvisorCommandController : ITargetingReceiver
{
    private readonly GameManager gameManager;
    private readonly TargetingController targetingController;
    private readonly Func<IReadOnlyList<GalaxyMapSector>> getSectors;
    private readonly Action<
        GalaxyMapPlanet,
        GalaxyMapPlanet,
        FacilityWindowTab
    > openConstructionWindow;

    /// <summary>
    /// Creates the advisor command controller.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    /// <param name="targetingController">Owns the active strategy targeting request.</param>
    /// <param name="getSectors">Returns the current visible galaxy sectors.</param>
    /// <param name="openConstructionWindow">Opens construction for a producer and destination.</param>
    public AdvisorCommandController(
        GameManager gameManager,
        TargetingController targetingController,
        Func<IReadOnlyList<GalaxyMapSector>> getSectors,
        Action<GalaxyMapPlanet, GalaxyMapPlanet, FacilityWindowTab> openConstructionWindow
    )
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.getSectors = getSectors ?? throw new ArgumentNullException(nameof(getSectors));
        this.openConstructionWindow =
            openConstructionWindow
            ?? throw new ArgumentNullException(nameof(openConstructionWindow));
    }

    /// <summary>
    /// Begins construction-destination targeting for one manufacturing category.
    /// </summary>
    /// <param name="manufacturingType">The requested manufacturing category.</param>
    /// <param name="sourceX">The source-space horizontal hotspot coordinate.</param>
    /// <param name="sourceY">The source-space vertical hotspot coordinate.</param>
    public void BeginConstruction(ManufacturingType manufacturingType, int sourceX, int sourceY)
    {
        targetingController.Begin(
            new TargetingRequest("Select construction destination", manufacturingType, this),
            sourceX,
            sourceY
        );
    }

    /// <summary>
    /// Resolves a selected destination and opens construction from its nearest idle producer.
    /// </summary>
    /// <param name="request">The completed targeting request.</param>
    /// <param name="target">The selected target.</param>
    public void OnTargetSelected(TargetingRequest request, object target)
    {
        if (
            request?.Source is not ManufacturingType manufacturingType
            || target is not StrategyMissionTarget missionTarget
            || missionTarget.Planet?.Planet == null
        )
            return;

        Faction faction = gameManager.GetPlayerFaction();
        Planet producer = FindProducerPlanet(
            faction,
            manufacturingType,
            missionTarget.Planet.Planet
        );
        GalaxyMapPlanet producerView = FindGalaxyMapPlanet(producer?.InstanceID);
        if (producerView == null)
            return;

        FacilityWindowTab? manufacturingTab = manufacturingType switch
        {
            ManufacturingType.Ship => FacilityWindowTab.Shipyards,
            ManufacturingType.Troop => FacilityWindowTab.Training,
            ManufacturingType.Building => FacilityWindowTab.Construction,
            _ => null,
        };
        if (!manufacturingTab.HasValue)
            return;

        openConstructionWindow(producerView, missionTarget.Planet, manufacturingTab.Value);
    }

    /// <summary>
    /// Handles targeting cancellation without retaining advisor command state.
    /// </summary>
    /// <param name="request">The cancelled targeting request.</param>
    public void OnTargetingCancelled(TargetingRequest request) { }

    /// <summary>
    /// Finds the nearest owned planet with idle capacity for one manufacturing category.
    /// </summary>
    /// <param name="faction">The player faction.</param>
    /// <param name="manufacturingType">The required manufacturing category.</param>
    /// <param name="destination">The requested destination planet.</param>
    /// <returns>The nearest eligible producer, or null when none exists.</returns>
    internal static Planet FindProducerPlanet(
        Faction faction,
        ManufacturingType manufacturingType,
        Planet destination
    )
    {
        if (faction == null || destination == null)
            return null;

        return faction
            .GetOwnedUnitsByType<Planet>()
            .Where(planet =>
                planet != null
                && !planet.IsDestroyed
                && string.Equals(
                    planet.GetOwnerInstanceID(),
                    faction.InstanceID,
                    StringComparison.Ordinal
                )
                && planet.GetIdleManufacturingFacilities(manufacturingType) > 0
            )
            .OrderBy(planet => planet.GetRawDistanceTo(destination))
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds one current galaxy-map projection by persistent planet identifier.
    /// </summary>
    /// <param name="planetInstanceId">The persistent planet identifier.</param>
    /// <returns>The current galaxy-map planet, or null when it is not visible.</returns>
    private GalaxyMapPlanet FindGalaxyMapPlanet(string planetInstanceId)
    {
        if (string.IsNullOrEmpty(planetInstanceId))
            return null;

        return getSectors()
            .SelectMany(sector => sector.Planets)
            .FirstOrDefault(planet => planet.Planet?.InstanceID == planetInstanceId);
    }
}
