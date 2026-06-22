using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

public sealed class ConstructionWindowController
{
    private const int _fleetBuildPanel = 1;
    private const int _troopBuildPanel = 2;

    private readonly GameManager gameManager;

    public ConstructionWindowController(GameManager gameManager)
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
    }

    public List<IManufacturable> GetBuildSelection(int buildPanel, string playerFactionId)
    {
        if (buildPanel == _fleetBuildPanel)
        {
            return ResourceManager
                .GetGameData<CapitalShip>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetGameData<Starfighter>())
                .Where(item => BelongsToPlayerSide(item, playerFactionId))
                .OrderBy(item => item.GetResearchOrder())
                .ThenBy(item => item.GetDisplayName())
                .ToList();
        }

        if (buildPanel == _troopBuildPanel)
        {
            return ResourceManager
                .GetGameData<Regiment>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetGameData<SpecialForces>())
                .Where(item => BelongsToPlayerSide(item, playerFactionId))
                .OrderBy(item => item.GetResearchOrder())
                .ThenBy(item => item.GetDisplayName())
                .ToList();
        }

        return ResourceManager
            .GetGameData<Building>()
            .Where(building =>
                building.GetBuildingType()
                    is BuildingType.Mine
                        or BuildingType.Refinery
                        or BuildingType.Shipyard
                        or BuildingType.TrainingFacility
                        or BuildingType.ConstructionFacility
            )
            .Cast<IManufacturable>()
            .Where(item => BelongsToPlayerSide(item, playerFactionId))
            .OrderBy(item => item.GetResearchOrder())
            .ThenBy(item => item.GetDisplayName())
            .ToList();
    }

    public HashSet<int> GetCanStartSelections(
        UIWindow window,
        IReadOnlyList<IManufacturable> items,
        int buildCount,
        string playerFactionId
    )
    {
        HashSet<int> canStartSelections = new HashSet<int>();
        if (items == null)
            return canStartSelections;

        for (int i = 0; i < items.Count; i++)
        {
            if (CanStartConstruction(window, items[i], buildCount, playerFactionId))
                canStartSelections.Add(i);
        }

        return canStartSelections;
    }

    public bool TryStartConstruction(
        UIWindow window,
        int buildPanel,
        int buildSelection,
        int buildCount,
        string playerFactionId
    )
    {
        List<IManufacturable> items = GetBuildSelection(buildPanel, playerFactionId);
        if (items.Count == 0)
            return false;

        buildSelection = Math.Max(0, Math.Min(buildSelection, items.Count - 1));
        IManufacturable selected = items[buildSelection];
        if (!CanStartConstruction(window, selected, buildCount, playerFactionId))
            return false;

        Planet producer = GetAuthoritativePlanet(GetConstructionWindowPlanet(window));
        ISceneNode destination = GetConstructionDestination(window);
        if (producer == null || destination == null)
            return false;

        return gameManager.StartManufacturing(producer, selected, destination, buildCount);
    }

    public Dictionary<ManufacturingType, string> GetManufacturingDestinationNames(UIWindow window)
    {
        Dictionary<ManufacturingType, string> names = new Dictionary<ManufacturingType, string>();
        foreach (
            ManufacturingType type in new[]
            {
                ManufacturingType.Ship,
                ManufacturingType.Troop,
                ManufacturingType.Building,
            }
        )
        {
            string planetId = GetManufacturingDestinationPlanetId(window, type);
            string itemId = GetManufacturingDestinationItemId(window, type);
            ISceneNode item = GetAuthoritativeNode(itemId);
            if (item != null)
            {
                names[type] = item.GetDisplayName();
                continue;
            }

            Planet planet = GetAuthoritativePlanet(planetId);
            if (planet != null)
                names[type] = planet.GetDisplayName();
        }

        return names;
    }

    public bool TryGetConstructionDestinationIds(
        UIWindow window,
        int buildPanel,
        out string destinationPlanetId,
        out string destinationItemId
    )
    {
        destinationPlanetId = null;
        destinationItemId = null;

        ManufacturingType? manufacturingType = GetManufacturingTypeFromBuildPanel(buildPanel);
        if (!manufacturingType.HasValue)
            return false;

        destinationPlanetId = GetManufacturingDestinationPlanetId(window, manufacturingType.Value);
        destinationItemId = GetManufacturingDestinationItemId(window, manufacturingType.Value);
        return true;
    }

    public void SetManufacturingDestination(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        int buildPanel,
        IEnumerable<UIWindow> windows
    )
    {
        FacilityWindowView sourceView = GetFacilityWindowView(sourceWindow);
        if (sourceView == null || target?.Planet?.Planet == null)
            return;

        ManufacturingType? manufacturingType = GetManufacturingTypeFromBuildPanel(buildPanel);
        if (!manufacturingType.HasValue)
            return;

        string destinationPlanetId = target.Planet.Planet.InstanceID;
        if (string.IsNullOrEmpty(destinationPlanetId))
            return;

        string destinationItemId = target.Item?.GetInstanceID();
        sourceView.ManufacturingDestinationPlanetIds[manufacturingType.Value] = destinationPlanetId;
        if (string.IsNullOrEmpty(destinationItemId))
            sourceView.ManufacturingDestinationItemIds.Remove(manufacturingType.Value);
        else
            sourceView.ManufacturingDestinationItemIds[manufacturingType.Value] = destinationItemId;

        UpdateOpenConstructionDestination(
            sourceWindow,
            manufacturingType.Value,
            destinationPlanetId,
            destinationItemId,
            windows
        );
    }

    public static ManufacturingType? GetManufacturingTypeFromBuildPanel(int buildPanel)
    {
        return buildPanel switch
        {
            1 => ManufacturingType.Ship,
            2 => ManufacturingType.Troop,
            3 => ManufacturingType.Building,
            _ => null,
        };
    }

    private bool CanStartConstruction(
        UIWindow window,
        IManufacturable selected,
        int buildCount,
        string playerFactionId
    )
    {
        if (
            GetConstructionWindowPlanet(window)?.Planet == null
            || selected == null
            || buildCount <= 0
        )
            return false;

        Planet producer = GetAuthoritativePlanet(GetConstructionWindowPlanet(window));
        ISceneNode destination = GetConstructionDestination(window);
        if (producer == null || destination == null)
            return false;

        if (!string.Equals(producer.OwnerInstanceID, playerFactionId, StringComparison.Ordinal))
            return false;

        if (!selected.HasAllowedOwnerInstanceID(playerFactionId))
            return false;

        if (!HasConstructionQueueCapacity(producer, selected))
            return false;

        if (
            !HasConstructionDestinationCapacity(
                destination,
                selected,
                buildCount,
                producer.GetOwnerInstanceID()
            )
        )
            return false;

        if (!HasConstructionMaintenanceHeadroom(selected, buildCount))
            return false;

        return true;
    }

    private static bool BelongsToPlayerSide(IManufacturable item, string playerFactionId)
    {
        return item.HasAllowedOwnerInstanceID(playerFactionId);
    }

    private static bool HasConstructionQueueCapacity(Planet planet, IManufacturable selected)
    {
        return planet.GetProductionFacilityCount(selected.GetManufacturingType()) > 0;
    }

    private static bool HasConstructionDestinationCapacity(
        ISceneNode destination,
        IManufacturable selected,
        int buildCount,
        string ownerInstanceId
    )
    {
        if (destination is Planet planet)
        {
            if (selected is CapitalShip)
                return true;

            ISceneNode planetCandidate = CreateConstructionCandidate(ownerInstanceId, selected);
            if (planetCandidate == null || !planet.CanAcceptChild(planetCandidate))
                return false;

            return selected is not Building || planet.GetAvailableEnergy() >= buildCount;
        }

        if (destination is Fleet fleet)
            return HasFleetConstructionDestinationCapacity(
                fleet,
                selected,
                buildCount,
                ownerInstanceId
            );

        if (destination is CapitalShip capitalShip)
            return HasShipConstructionDestinationCapacity(
                capitalShip,
                selected,
                buildCount,
                ownerInstanceId
            );

        return false;
    }

    private static bool HasFleetConstructionDestinationCapacity(
        Fleet fleet,
        IManufacturable selected,
        int buildCount,
        string ownerInstanceId
    )
    {
        if (!string.Equals(fleet.GetOwnerInstanceID(), ownerInstanceId, StringComparison.Ordinal))
            return false;

        if (selected is CapitalShip)
            return true;

        ISceneNode candidate = CreateConstructionCandidate(ownerInstanceId, selected);
        if (candidate == null)
            return false;

        if (candidate is Starfighter)
            return GetFleetConstructionStarfighterCapacity(fleet) >= buildCount;

        if (candidate is Regiment)
            return GetFleetConstructionRegimentCapacity(fleet) >= buildCount;

        return false;
    }

    private static bool HasShipConstructionDestinationCapacity(
        CapitalShip capitalShip,
        IManufacturable selected,
        int buildCount,
        string ownerInstanceId
    )
    {
        if (
            !string.Equals(
                capitalShip.GetOwnerInstanceID(),
                ownerInstanceId,
                StringComparison.Ordinal
            )
        )
            return false;

        ISceneNode candidate = CreateConstructionCandidate(ownerInstanceId, selected);
        if (candidate == null)
            return false;

        if (!IsConstructionCarrierAvailable(capitalShip))
            return false;

        if (candidate is Starfighter)
            return capitalShip.GetExcessStarfighterCapacity() >= buildCount;

        if (candidate is Regiment)
            return capitalShip.GetExcessRegimentCapacity() >= buildCount;

        return false;
    }

    private static int GetFleetConstructionStarfighterCapacity(Fleet fleet)
    {
        return GetConstructionCarrierShips(fleet).Sum(ship => ship.GetExcessStarfighterCapacity());
    }

    private static int GetFleetConstructionRegimentCapacity(Fleet fleet)
    {
        return GetConstructionCarrierShips(fleet).Sum(ship => ship.GetExcessRegimentCapacity());
    }

    private static IEnumerable<CapitalShip> GetConstructionCarrierShips(Fleet fleet)
    {
        if (fleet?.Movement != null)
            return Enumerable.Empty<CapitalShip>();

        return fleet.CapitalShips.Where(IsConstructionCarrierAvailable);
    }

    private static bool IsConstructionCarrierAvailable(CapitalShip ship)
    {
        return ship?.ManufacturingStatus == ManufacturingStatus.Complete && ship.Movement == null;
    }

    private static ISceneNode CreateConstructionCandidate(
        string ownerInstanceId,
        IManufacturable template
    )
    {
        IManufacturable item = template.GetDeepCopy();
        if (item is not ISceneNode sceneNode)
            return null;

        sceneNode.OwnerInstanceID = ownerInstanceId;
        item.ManufacturingStatus = ManufacturingStatus.Building;
        item.ManufacturingProgress = 0;
        if (item is IMovable movable)
            movable.Movement = null;

        return sceneNode;
    }

    private bool HasConstructionMaintenanceHeadroom(IManufacturable selected, int buildCount)
    {
        int maintenanceCost = selected.GetMaintenanceCost();
        if (maintenanceCost <= 0)
            return true;

        Faction faction = gameManager.GetPlayerFaction();
        if (faction == null)
            return false;

        int minimumHeadroom = gameManager
            .GetGame()
            .Config.AI.Selection.MaintenanceHeadroomHardFloor;
        return faction.ProjectedMaintenanceHeadroom - maintenanceCost * buildCount
            >= minimumHeadroom;
    }

    private string GetManufacturingDestinationPlanetId(
        UIWindow window,
        ManufacturingType manufacturingType
    )
    {
        FacilityWindowView view = GetFacilityWindowView(window);
        if (
            view != null
            && view.ManufacturingDestinationPlanetIds.TryGetValue(
                manufacturingType,
                out string planetId
            )
            && !string.IsNullOrEmpty(planetId)
        )
            return planetId;

        return view?.GalaxyMapPlanet?.Planet?.InstanceID;
    }

    private static string GetManufacturingDestinationItemId(
        UIWindow window,
        ManufacturingType manufacturingType
    )
    {
        FacilityWindowView view = GetFacilityWindowView(window);
        if (
            view != null
            && view.ManufacturingDestinationItemIds.TryGetValue(
                manufacturingType,
                out string itemId
            )
            && !string.IsNullOrEmpty(itemId)
        )
            return itemId;

        return null;
    }

    private static void UpdateOpenConstructionDestination(
        UIWindow facilityWindow,
        ManufacturingType manufacturingType,
        string destinationPlanetId,
        string destinationItemId,
        IEnumerable<UIWindow> windows
    )
    {
        foreach (UIWindow window in windows ?? Enumerable.Empty<UIWindow>())
        {
            ConstructionWindowView view = GetConstructionWindowView(window);
            if (
                view == null
                || view.SourceWindow != facilityWindow
                || GetManufacturingTypeFromBuildPanel(view.GetBuildPanel()) != manufacturingType
            )
                continue;

            view.ConstructionDestinationPlanetId = destinationPlanetId;
            view.ConstructionDestinationItemId = destinationItemId;
        }
    }

    private Planet GetConstructionDestinationPlanet(UIWindow window)
    {
        ConstructionWindowView view = GetConstructionWindowView(window);
        string planetId = !string.IsNullOrEmpty(view?.ConstructionDestinationPlanetId)
            ? view.ConstructionDestinationPlanetId
            : view?.GalaxyMapPlanet?.Planet?.InstanceID;
        return GetAuthoritativePlanet(planetId);
    }

    private ISceneNode GetConstructionDestination(UIWindow window)
    {
        ConstructionWindowView view = GetConstructionWindowView(window);
        ISceneNode item = GetAuthoritativeNode(view?.ConstructionDestinationItemId);
        return item ?? GetConstructionDestinationPlanet(window);
    }

    private static GalaxyMapPlanet GetConstructionWindowPlanet(UIWindow window)
    {
        return GetConstructionWindowView(window)?.GalaxyMapPlanet;
    }

    private static ConstructionWindowView GetConstructionWindowView(UIWindow window)
    {
        if (window == null)
            return null;

        return window.TryGetContent(out ConstructionWindowView view) ? view : null;
    }

    private static FacilityWindowView GetFacilityWindowView(UIWindow window)
    {
        if (window == null)
            return null;

        return window.TryGetContent(out FacilityWindowView view) ? view : null;
    }

    private Planet GetAuthoritativePlanet(GalaxyMapPlanet planet)
    {
        return GetAuthoritativePlanet(planet?.Planet?.InstanceID);
    }

    private Planet GetAuthoritativePlanet(string planetId)
    {
        return gameManager.GetGame()?.GetSceneNodeByInstanceID<Planet>(planetId);
    }

    private ISceneNode GetAuthoritativeNode(string instanceId)
    {
        return string.IsNullOrEmpty(instanceId)
            ? null
            : gameManager.GetGame()?.GetSceneNodeByInstanceID<ISceneNode>(instanceId);
    }
}
