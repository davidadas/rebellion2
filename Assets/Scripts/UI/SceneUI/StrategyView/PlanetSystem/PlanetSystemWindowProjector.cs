using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Projects planet-system game state into immutable presentation data.
/// </summary>
internal sealed class PlanetSystemWindowProjector
{
    private static readonly Color32 _barBackgroundColor = new Color32(160, 160, 160, 255);
    private static readonly Color32 _energyAvailableColor = new Color32(64, 132, 255, 255);
    private static readonly Color32 _energyCapacityColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 _energyEmptyColor = new Color32(0, 0, 255, 255);
    private static readonly Color32 _rawAvailableColor = new Color32(236, 106, 46, 255);
    private static readonly Color32 _rawCapacityColor = new Color32(255, 255, 84, 255);

    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a planet-system presentation projector.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    public PlanetSystemWindowProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Creates the complete presentation for one planet-system window.
    /// </summary>
    /// <param name="sector">The represented galaxy-map sector.</param>
    /// <param name="selectedPlanetInstanceId">The selected planet identifier.</param>
    /// <param name="selectedIcon">The selected planet icon.</param>
    /// <param name="hoveredPlanetInstanceId">The hovered planet identifier.</param>
    /// <param name="hoveredIcon">The hovered planet icon.</param>
    /// <returns>The immutable planet-system presentation.</returns>
    public PlanetSystemWindowRenderData CreateRenderData(
        GalaxyMapSector sector,
        string selectedPlanetInstanceId,
        PlanetIcon selectedIcon,
        string hoveredPlanetInstanceId,
        PlanetIcon hoveredIcon
    )
    {
        UIContext uiContext = GetUIContext();
        PlanetSystem system = sector?.System;
        IReadOnlyList<GalaxyMapPlanet> planets = sector?.Planets ?? Array.Empty<GalaxyMapPlanet>();
        List<PlanetSystemPlanetRenderData> presentations = new List<PlanetSystemPlanetRenderData>(
            planets.Count
        );
        for (int planetIndex = 0; planetIndex < planets.Count; planetIndex++)
        {
            presentations.Add(
                CreatePlanetData(
                    uiContext,
                    system,
                    planets[planetIndex],
                    planetIndex,
                    selectedPlanetInstanceId,
                    selectedIcon,
                    hoveredPlanetInstanceId,
                    hoveredIcon
                )
            );
        }

        return new PlanetSystemWindowRenderData(system?.GetDisplayName(), presentations);
    }

    /// <summary>
    /// Creates one planet presentation.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="system">The represented planet system.</param>
    /// <param name="strategyPlanet">The represented strategy planet.</param>
    /// <param name="planetIndex">The planet's stable position in the rendered sector.</param>
    /// <param name="selectedPlanetInstanceId">The selected planet identifier.</param>
    /// <param name="selectedIcon">The selected planet icon.</param>
    /// <param name="hoveredPlanetInstanceId">The hovered planet identifier.</param>
    /// <param name="hoveredIcon">The hovered planet icon.</param>
    /// <returns>The immutable planet presentation.</returns>
    private static PlanetSystemPlanetRenderData CreatePlanetData(
        UIContext uiContext,
        PlanetSystem system,
        GalaxyMapPlanet strategyPlanet,
        int planetIndex,
        string selectedPlanetInstanceId,
        PlanetIcon selectedIcon,
        string hoveredPlanetInstanceId,
        PlanetIcon hoveredIcon
    )
    {
        Planet planet = strategyPlanet?.Planet;
        string planetInstanceId = planet?.InstanceID;
        string ownerFactionId = planet?.OwnerInstanceID;
        bool unexplored = planet?.IsUnexploredView == true;
        string fleetFactionId = SelectPresentFactionId(
            GetFleetOwnerFactionIds(planet),
            ownerFactionId
        );
        string missionFactionId = SelectPresentFactionId(
            GetMissionOwnerFactionIds(planet),
            ownerFactionId
        );
        bool showUprising = !unexplored && planet?.IsInUprising == true;
        int popularSupport = GetPlayerSupport(uiContext, planet);
        Texture2D fleetTexture = string.IsNullOrEmpty(fleetFactionId)
            ? null
            : GetOverlayTexture(uiContext, fleetFactionId, PlanetIcon.Fleet, false);
        Texture2D fleetPressedTexture = string.IsNullOrEmpty(fleetFactionId)
            ? null
            : GetOverlayTexture(uiContext, fleetFactionId, PlanetIcon.Fleet, true);
        Texture2D missionTexture = string.IsNullOrEmpty(missionFactionId)
            ? null
            : GetOverlayTexture(uiContext, missionFactionId, PlanetIcon.Mission, false);
        Texture2D missionPressedTexture = string.IsNullOrEmpty(missionFactionId)
            ? null
            : GetOverlayTexture(uiContext, missionFactionId, PlanetIcon.Mission, true);

        return new PlanetSystemPlanetRenderData(
            planetIndex,
            CreatePlanetOffset(system, planet),
            uiContext.GetPlanetTexture(planet, strategyPlanet?.PlanetIconPath),
            showUprising
                ? uiContext.GetTexture(
                    uiContext
                        .GetPlayerFactionTheme()
                        ?.PlanetOverlayTheme?.PlanetSystemUprisingImagePath
                )
                : null,
            unexplored || !HasFacilities(planet)
                ? null
                : GetOverlayTexture(uiContext, ownerFactionId, PlanetIcon.Facility, false),
            unexplored || !HasFacilities(planet)
                ? null
                : GetOverlayTexture(uiContext, ownerFactionId, PlanetIcon.Facility, true),
            unexplored || !HasDefenses(planet)
                ? null
                : GetOverlayTexture(uiContext, ownerFactionId, PlanetIcon.Defense, false),
            unexplored || !HasDefenses(planet)
                ? null
                : GetOverlayTexture(uiContext, ownerFactionId, PlanetIcon.Defense, true),
            fleetTexture,
            fleetPressedTexture,
            missionTexture,
            missionPressedTexture,
            !unexplored && planet?.IsHeadquarters == true
                ? uiContext.GetTexture(
                    uiContext
                        .GetTheme(ownerFactionId)
                        ?.PlanetOverlayTheme?.PlanetSystemHeadquartersImagePath
                )
                : null,
            planet?.GetDisplayName(),
            uiContext.GetTheme(ownerFactionId)?.GetPrimaryColor() ?? Color.white,
            string.Equals(selectedPlanetInstanceId, planetInstanceId, StringComparison.Ordinal)
                ? selectedIcon
                : PlanetIcon.None,
            string.Equals(hoveredPlanetInstanceId, planetInstanceId, StringComparison.Ordinal)
                ? hoveredIcon
                : PlanetIcon.None,
            unexplored ? CreateHiddenBar() : CreateEnergyBar(planet),
            unexplored ? CreateHiddenBar() : CreateRawResourceBar(planet),
            unexplored ? CreateHiddenBar() : CreateSupportBar(uiContext, planet, popularSupport)
        );
    }

    /// <summary>
    /// Gets a planet's projected galaxy offset from its parent system.
    /// </summary>
    /// <param name="system">The represented planet system.</param>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The projected galaxy offset.</returns>
    private static Vector2Int CreatePlanetOffset(PlanetSystem system, Planet planet)
    {
        System.Drawing.Point systemPosition = system?.GetPosition() ?? System.Drawing.Point.Empty;
        System.Drawing.Point planetPosition = planet?.GetPosition() ?? System.Drawing.Point.Empty;
        return new Vector2Int(
            planetPosition.X - systemPosition.X,
            planetPosition.Y - systemPosition.Y
        );
    }

    /// <summary>
    /// Resolves one faction's normal or pressed planet overlay image.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="factionId">The represented faction identifier.</param>
    /// <param name="icon">The represented overlay icon.</param>
    /// <param name="pressed">Whether to resolve the pressed image.</param>
    /// <returns>The resolved image, or null.</returns>
    private static Texture2D GetOverlayTexture(
        UIContext uiContext,
        string factionId,
        PlanetIcon icon,
        bool pressed
    )
    {
        PlanetOverlayIcons icons = uiContext
            .GetTheme(factionId)
            ?.PlanetOverlayTheme?.PlanetOverlayIcons;
        OverlayIconTheme theme = icon switch
        {
            PlanetIcon.Facility => icons?.Buildings,
            PlanetIcon.Defense => icons?.Defenses,
            PlanetIcon.Fleet => icons?.Fleets,
            PlanetIcon.Mission => icons?.Missions,
            _ => null,
        };
        return uiContext.GetTexture(pressed ? theme?.HoverImagePath : theme?.NormalImagePath);
    }

    /// <summary>
    /// Creates the energy-capacity bar presentation.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The energy bar presentation.</returns>
    private static PlanetSystemBarRenderData CreateEnergyBar(Planet planet)
    {
        if (planet == null || planet.EnergyCapacity <= 0)
        {
            return new PlanetSystemBarRenderData(
                true,
                0,
                0,
                1f,
                _energyEmptyColor,
                Color.clear,
                Color.clear
            );
        }

        return new PlanetSystemBarRenderData(
            true,
            planet.EnergyCapacity,
            Mathf.Min(planet.Buildings.Count, planet.EnergyCapacity),
            0f,
            _energyCapacityColor,
            _energyAvailableColor,
            _barBackgroundColor
        );
    }

    /// <summary>
    /// Creates the raw-resource bar presentation.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The raw-resource bar presentation.</returns>
    private static PlanetSystemBarRenderData CreateRawResourceBar(Planet planet)
    {
        if (planet == null || planet.NumRawResourceNodes <= 0)
        {
            return new PlanetSystemBarRenderData(
                true,
                0,
                0,
                1f,
                _rawAvailableColor,
                Color.clear,
                Color.clear
            );
        }

        return new PlanetSystemBarRenderData(
            true,
            planet.NumRawResourceNodes,
            Mathf.Min(planet.GetRawMinedResources(), planet.NumRawResourceNodes),
            0f,
            _rawCapacityColor,
            _rawAvailableColor,
            _barBackgroundColor
        );
    }

    /// <summary>
    /// Creates the popular-support bar presentation.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="planet">The represented planet.</param>
    /// <param name="support">The player's popular support percentage.</param>
    /// <returns>The popular-support bar presentation.</returns>
    private static PlanetSystemBarRenderData CreateSupportBar(
        UIContext uiContext,
        Planet planet,
        int support
    )
    {
        if (planet?.IsPopulated() != true)
            return CreateHiddenBar();

        return new PlanetSystemBarRenderData(
            true,
            0,
            0,
            support / 100f,
            uiContext.GetPlayerFactionTheme()?.GetPrimaryColor() ?? Color.white,
            Color.clear,
            GetOpposingSupportColor(uiContext)
        );
    }

    /// <summary>
    /// Creates a hidden bar presentation.
    /// </summary>
    /// <returns>The hidden bar presentation.</returns>
    private static PlanetSystemBarRenderData CreateHiddenBar()
    {
        return new PlanetSystemBarRenderData(
            false,
            0,
            0,
            0f,
            Color.clear,
            Color.clear,
            Color.clear
        );
    }

    /// <summary>
    /// Gets the player's popular support for one planet.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The displayed popular support percentage.</returns>
    private static int GetPlayerSupport(UIContext uiContext, Planet planet)
    {
        string playerFactionId = uiContext.GetPlayerFactionInstanceID();
        return !string.IsNullOrEmpty(playerFactionId) && planet != null
            ? planet.GetPopularSupport(playerFactionId)
            : 50;
    }

    /// <summary>
    /// Gets the opposing faction's support color.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <returns>The opposing faction color.</returns>
    private static Color32 GetOpposingSupportColor(UIContext uiContext)
    {
        string playerFactionId = uiContext.GetPlayerFactionInstanceID();
        string opposingFactionId = uiContext
            .Game?.GetFactions()
            ?.FirstOrDefault(faction =>
                !string.Equals(faction.InstanceID, playerFactionId, StringComparison.Ordinal)
            )
            ?.InstanceID;
        return uiContext.GetTheme(opposingFactionId)?.GetPrimaryColor() ?? Color.white;
    }

    /// <summary>
    /// Determines whether a planet has a facility overlay.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>True when a facility overlay should be displayed.</returns>
    private static bool HasFacilities(Planet planet)
    {
        return planet?.Buildings?.Any(building =>
                building.GetBuildingType()
                    is BuildingType.Mine
                        or BuildingType.Refinery
                        or BuildingType.Shipyard
                        or BuildingType.TrainingFacility
                        or BuildingType.ConstructionFacility
            ) == true;
    }

    /// <summary>
    /// Determines whether a planet has a defense overlay.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>True when a defense overlay should be displayed.</returns>
    private static bool HasDefenses(Planet planet)
    {
        return planet != null
            && (
                planet.Buildings.Any(building =>
                    building.GetBuildingType() is BuildingType.Defense or BuildingType.Weapon
                )
                || planet.Regiments.Count > 0
                || planet.Starfighters.Count > 0
            );
    }

    /// <summary>
    /// Gets the distinct factions with fleets at one planet.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The present fleet-owner identifiers.</returns>
    private static List<string> GetFleetOwnerFactionIds(Planet planet)
    {
        return planet
                ?.Fleets?.Select(fleet => fleet.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    /// <summary>
    /// Gets the distinct factions with missions at one planet.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The present mission-owner identifiers.</returns>
    private static List<string> GetMissionOwnerFactionIds(Planet planet)
    {
        return planet
                ?.Missions?.Select(mission => mission.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    /// <summary>
    /// Selects the faction whose overlay represents a mixed-presence icon.
    /// </summary>
    /// <param name="presentFactionIds">The factions present at the planet.</param>
    /// <param name="ownerFactionId">The planet owner faction identifier.</param>
    /// <returns>The faction whose overlay should be shown, or null.</returns>
    private static string SelectPresentFactionId(
        IReadOnlyList<string> presentFactionIds,
        string ownerFactionId
    )
    {
        if (presentFactionIds == null || presentFactionIds.Count == 0)
            return null;
        if (presentFactionIds.Count == 1)
            return presentFactionIds[0];

        return presentFactionIds.FirstOrDefault(factionId =>
                !string.Equals(factionId, ownerFactionId, StringComparison.Ordinal)
            ) ?? presentFactionIds[0];
    }

    /// <summary>
    /// Gets the current strategy presentation context.
    /// </summary>
    /// <returns>The current presentation context.</returns>
    private UIContext GetUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The strategy UI context is unavailable.");
    }
}
