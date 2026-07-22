using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Defines strategy-screen actions requested by the galaxy-map feature.
/// </summary>
public interface IGalaxyMapActions
{
    /// <summary>
    /// Opens the planet-system window requested from a galaxy-map cluster.
    /// </summary>
    /// <param name="system">The requested planet system.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    void OpenPlanetSystemWindow(PlanetSystem system, int sourceX, int sourceY);

    /// <summary>
    /// Requests a strategy render after galaxy-map interaction state changes.
    /// </summary>
    void RequestGalaxyMapRender();
}

/// <summary>
/// Owns galaxy-map projection, hover state, targeting hits, and semantic action routing.
/// </summary>
public sealed class GalaxyMapController
{
    private readonly GalaxyMapProjector projector;
    private readonly ReadOnlyCollection<GalaxyMapSector> readOnlySectors;
    private readonly List<GalaxyMapSector> sectors = new List<GalaxyMapSector>();
    private readonly Dictionary<string, GalaxyMapPlanet> planetsByInstanceId = new Dictionary<
        string,
        GalaxyMapPlanet
    >(StringComparer.Ordinal);
    private readonly Dictionary<string, PlanetSystem> systemsByInstanceId = new Dictionary<
        string,
        PlanetSystem
    >(StringComparer.Ordinal);

    private IGalaxyMapActions actions;
    private GalaxyMapView view;
    private GalaxyMap visibleGalaxyMap;
    private string hoveredSystemInstanceId;
    private string playerFactionId = string.Empty;

    public string PlayerFactionId => playerFactionId;

    public IReadOnlyList<GalaxyMapSector> Sectors => readOnlySectors;

    public GalaxyMap VisibleGalaxyMap => visibleGalaxyMap;

    /// <summary>
    /// Creates a galaxy-map controller backed by the current strategy UI context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    public GalaxyMapController(Func<UIContext> getUIContext)
    {
        projector = new GalaxyMapProjector(getUIContext);
        readOnlySectors = sectors.AsReadOnly();
    }

    /// <summary>
    /// Connects the controller to strategy-screen actions.
    /// </summary>
    /// <param name="nextActions">The strategy-screen action boundary.</param>
    public void Initialize(IGalaxyMapActions nextActions)
    {
        actions = nextActions ?? throw new ArgumentNullException(nameof(nextActions));
    }

    /// <summary>
    /// Subscribes the controller to an authored galaxy-map view exactly once.
    /// </summary>
    /// <param name="nextView">The authored galaxy-map view.</param>
    public void BindView(GalaxyMapView nextView)
    {
        if (nextView == null)
            throw new ArgumentNullException(nameof(nextView));

        EnsureInitialized();
        if (ReferenceEquals(view, nextView))
            return;

        ReleaseView();
        view = nextView;
        view.Destroyed += HandleViewDestroyed;
        view.SystemHoverCleared += HandleSystemHoverCleared;
        view.SystemHovered += HandleSystemHovered;
        view.SystemOpenRequested += HandleSystemOpenRequested;
    }

    /// <summary>
    /// Rebuilds the faction-filtered galaxy snapshot used by map and window projection.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    public void RebuildSnapshot(GameManager gameManager)
    {
        if (gameManager == null)
            throw new ArgumentNullException(nameof(gameManager));

        sectors.Clear();
        Faction playerFaction = gameManager.GetPlayerFaction();
        playerFactionId = playerFaction?.InstanceID ?? string.Empty;
        visibleGalaxyMap = null;
        if (playerFaction != null)
        {
            visibleGalaxyMap = gameManager.GetFogOfWarSystem().BuildFactionView(playerFaction);
            IReadOnlyList<PlanetSystem> visibleSystems = visibleGalaxyMap?.PlanetSystems;
            foreach (PlanetSystem system in visibleSystems ?? Array.Empty<PlanetSystem>())
            {
                List<GalaxyMapPlanet> planets = new List<GalaxyMapPlanet>();
                foreach (Planet planet in system.Planets)
                {
                    planets.Add(new GalaxyMapPlanet(system, planet, planet.GetPlanetIconPath()));
                }

                sectors.Add(new GalaxyMapSector(system, planets));
            }
        }

        ReconcileHoveredSystem();
    }

    /// <summary>
    /// Projects and renders the current visible galaxy state.
    /// </summary>
    /// <param name="sectors">The visible sectors in render order.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="filterMode">The active galactic-information filter.</param>
    public void Render(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        GalacticInformationFilterMode filterMode
    )
    {
        RebuildDomainLookups(sectors);
        GetRequiredView()
            .Render(
                projector.Project(sectors, playerFactionId, filterMode, hoveredSystemInstanceId)
            );
    }

    /// <summary>
    /// Resolves a galaxy-map pointer hit into a planet mission target.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="target">Receives the resolved mission target.</param>
    /// <returns>True when the pointer is over a rendered planet marker.</returns>
    public bool TryGetMissionTarget(PointerEventData eventData, out StrategyMissionTarget target)
    {
        target = null;
        if (
            view == null
            || !view.TryGetPlanetInstanceID(eventData, out string planetInstanceId)
            || !planetsByInstanceId.TryGetValue(planetInstanceId, out GalaxyMapPlanet planet)
        )
            return false;

        target = new StrategyMissionTarget(planet, null);
        return true;
    }

    /// <summary>
    /// Clears the currently revealed system label.
    /// </summary>
    /// <returns>True when hover state changed.</returns>
    public bool ClearHover()
    {
        return SetHoveredSystem(null);
    }

    /// <summary>
    /// Resolves a visible sector's absolute source-space map position.
    /// </summary>
    /// <param name="sector">The visible sector.</param>
    /// <returns>The source-space position of the sector.</returns>
    public Vector2Int GetSystemSourcePosition(GalaxyMapSector sector)
    {
        return projector.GetSystemSourcePosition(sector?.System);
    }

    /// <summary>
    /// Finds a scene node in the current visible galaxy snapshot.
    /// </summary>
    /// <param name="instanceId">The node instance identifier.</param>
    /// <returns>The matching visible node, or null.</returns>
    public ISceneNode FindVisibleNode(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || visibleGalaxyMap == null)
            return null;

        ISceneNode match = null;
        visibleGalaxyMap.Traverse(node =>
        {
            if (match == null && node?.InstanceID == instanceId)
                match = node;
        });
        return match;
    }

    /// <summary>
    /// Handles a cluster hover transition emitted by the authored map view.
    /// </summary>
    /// <param name="systemInstanceId">The hovered planet-system identifier.</param>
    private void HandleSystemHovered(string systemInstanceId)
    {
        if (!SetHoveredSystem(systemInstanceId))
            return;

        actions.RequestGalaxyMapRender();
    }

    /// <summary>
    /// Handles a cluster hover exit emitted by the authored map view.
    /// </summary>
    private void HandleSystemHoverCleared()
    {
        if (!ClearHover())
            return;

        actions.RequestGalaxyMapRender();
    }

    /// <summary>
    /// Routes a cluster open request to the strategy screen.
    /// </summary>
    /// <param name="systemInstanceId">The requested planet-system identifier.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    private void HandleSystemOpenRequested(string systemInstanceId, int sourceX, int sourceY)
    {
        if (
            !string.IsNullOrEmpty(systemInstanceId)
            && systemsByInstanceId.TryGetValue(systemInstanceId, out PlanetSystem system)
        )
            actions.OpenPlanetSystemWindow(system, sourceX, sourceY);
    }

    /// <summary>
    /// Releases subscriptions when the bound authored map view is destroyed.
    /// </summary>
    /// <param name="destroyedView">The destroyed map view.</param>
    private void HandleViewDestroyed(GalaxyMapView destroyedView)
    {
        if (ReferenceEquals(view, destroyedView))
            ReleaseView();
    }

    /// <summary>
    /// Replaces the currently revealed system label.
    /// </summary>
    /// <param name="systemInstanceId">The newly hovered system identifier, or null to clear hover.</param>
    /// <returns>True when hover state changed.</returns>
    private bool SetHoveredSystem(string systemInstanceId)
    {
        if (string.Equals(hoveredSystemInstanceId, systemInstanceId, StringComparison.Ordinal))
            return false;

        hoveredSystemInstanceId = systemInstanceId;
        return true;
    }

    /// <summary>
    /// Clears hover state when its system is absent from the refreshed snapshot.
    /// </summary>
    private void ReconcileHoveredSystem()
    {
        if (string.IsNullOrEmpty(hoveredSystemInstanceId))
            return;

        foreach (GalaxyMapSector sector in sectors)
        {
            if (
                string.Equals(
                    sector?.System?.InstanceID,
                    hoveredSystemInstanceId,
                    StringComparison.Ordinal
                )
            )
                return;
        }

        hoveredSystemInstanceId = null;
    }

    /// <summary>
    /// Rebuilds controller-owned domain lookups for semantic view identifiers.
    /// </summary>
    /// <param name="sectors">The current visible sectors.</param>
    private void RebuildDomainLookups(IReadOnlyList<GalaxyMapSector> sectors)
    {
        systemsByInstanceId.Clear();
        planetsByInstanceId.Clear();
        if (sectors == null)
            return;

        foreach (GalaxyMapSector sector in sectors)
        {
            if (!string.IsNullOrEmpty(sector?.System?.InstanceID))
                systemsByInstanceId[sector.System.InstanceID] = sector.System;

            if (sector?.Planets == null)
                continue;

            foreach (GalaxyMapPlanet planet in sector.Planets)
            {
                if (!string.IsNullOrEmpty(planet?.Planet?.InstanceID))
                    planetsByInstanceId[planet.Planet.InstanceID] = planet;
            }
        }
    }

    /// <summary>
    /// Releases subscriptions from the currently bound authored map view.
    /// </summary>
    private void ReleaseView()
    {
        if (ReferenceEquals(view, null))
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.SystemHoverCleared -= HandleSystemHoverCleared;
        view.SystemHovered -= HandleSystemHovered;
        view.SystemOpenRequested -= HandleSystemOpenRequested;
        view = null;
    }

    /// <summary>
    /// Verifies action routing is available before view binding or interaction.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GalaxyMapController)} must be initialized before use."
            );
        }
    }

    /// <summary>
    /// Gets the bound authored map view and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The bound authored galaxy-map view.</returns>
    private GalaxyMapView GetRequiredView()
    {
        EnsureInitialized();
        return view
            ?? throw new InvalidOperationException(
                $"{nameof(GalaxyMapController)} must bind a view before rendering."
            );
    }
}
