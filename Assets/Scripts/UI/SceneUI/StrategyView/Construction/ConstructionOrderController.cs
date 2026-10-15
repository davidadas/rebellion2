using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.DependencyInjection;

/// <summary>
/// Adapts installed buildings and their authored replacement into a construction selection.
/// </summary>
internal sealed class BuildingUpgradeSelection : Building
{
    private const string _selectionPrefix = "upgrade";

    public IReadOnlyList<Building> Sources { get; }

    public Building Source => Sources[0];

    public Building Target { get; }

    /// <summary>
    /// Creates an in-place building upgrade selection.
    /// </summary>
    /// <param name="source">The installed building that will be replaced.</param>
    /// <param name="target">The authored replacement definition.</param>
    public BuildingUpgradeSelection(Building source, Building target)
        : this(new[] { source }, target) { }

    /// <summary>
    /// Creates a grouped in-place building upgrade selection.
    /// </summary>
    /// <param name="sources">The installed buildings available to the order.</param>
    /// <param name="target">The authored replacement definition.</param>
    public BuildingUpgradeSelection(IReadOnlyList<Building> sources, Building target)
    {
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        if (sources.Count == 0 || sources.Any(source => source == null))
            throw new ArgumentException(
                "At least one installed building is required.",
                nameof(sources)
            );

        Sources = new List<Building>(sources).AsReadOnly();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        TypeID = target.TypeID;
        DisplayName = target.DisplayName;
        DisplayImagePath = target.DisplayImagePath;
        SmallDisplayImagePath = target.SmallDisplayImagePath;
        MessageImagePath = target.MessageImagePath;
        ConstructionCost = target.GetConstructionCost();
        MaintenanceCost = Math.Max(0, target.GetMaintenanceCost() - Source.GetMaintenanceCost());
        BaseBuildSpeed = target.BaseBuildSpeed;
        ManufacturingFactionInstanceIDs = target.ManufacturingFactionInstanceIDs;
        ResearchOrder = target.ResearchOrder;
        ResearchDifficulty = target.ResearchDifficulty;
        BuildingType = target.BuildingType;
        ProductionType = ManufacturingType.Building;
        ManufacturingStatus = ManufacturingStatus.Complete;
        OwnerInstanceID = Source.OwnerInstanceID;
    }

    /// <summary>
    /// Gets the stable identity for this source and target pair.
    /// </summary>
    /// <returns>The construction selection identifier.</returns>
    public string GetSelectionID()
    {
        return GetSelectionID(Source.TypeID, Target.TypeID);
    }

    /// <summary>
    /// Creates a stable identity for one active or available upgrade.
    /// </summary>
    /// <param name="sourceTypeID">The installed building type being upgraded.</param>
    /// <param name="targetTypeID">The authored replacement type.</param>
    /// <returns>The construction selection identifier, or null for incomplete input.</returns>
    public static string GetSelectionID(string sourceTypeID, string targetTypeID)
    {
        return string.IsNullOrEmpty(sourceTypeID) || string.IsNullOrEmpty(targetTypeID)
            ? null
            : $"{_selectionPrefix}:{sourceTypeID}:{targetTypeID}";
    }
}

/// <summary>
/// Evaluates and starts construction orders against the current game state.
/// </summary>
public sealed class ConstructionOrderController
{
    private readonly IServiceLocator services;

    /// <summary>
    /// Creates a construction order controller.
    /// </summary>
    /// <param name="services">Resolves the active game's commands and queries.</param>
    public ConstructionOrderController(IServiceLocator services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the build templates currently available for a manufacturing panel.
    /// </summary>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The available build templates in display order.</returns>
    public IReadOnlyList<IManufacturable> GetBuildSelection(
        FacilityWindowTab manufacturingTab,
        string playerFactionId
    )
    {
        return GetBuildSelection(manufacturingTab, playerFactionId, null);
    }

    /// <summary>
    /// Gets build templates and rebuildable facilities available for a manufacturing panel.
    /// </summary>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="destination">The node that will receive completed work.</param>
    /// <returns>The available build and rebuild selections in display order.</returns>
    public IReadOnlyList<IManufacturable> GetBuildSelection(
        FacilityWindowTab manufacturingTab,
        string playerFactionId,
        ISceneNode destination
    )
    {
        return GetBuildSelection(manufacturingTab, playerFactionId, destination, null);
    }

    /// <summary>
    /// Gets build templates and rebuildable facilities available for a manufacturing panel.
    /// </summary>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="destination">The node that will receive completed work.</param>
    /// <param name="producer">The planet whose active reconstruction order remains selectable.</param>
    /// <returns>The available build and rebuild selections in display order.</returns>
    public IReadOnlyList<IManufacturable> GetBuildSelection(
        FacilityWindowTab manufacturingTab,
        string playerFactionId,
        ISceneNode destination,
        Planet producer
    )
    {
        Faction faction = GetFaction(playerFactionId);
        ManufacturingType? manufacturingType =
            FacilityManufacturingLaneCatalog.GetManufacturingType(manufacturingTab);
        if (faction == null || !manufacturingType.HasValue)
            return Array.Empty<IManufacturable>();

        List<IManufacturable> selections = faction
            .GetUnlockedTechnologies(manufacturingType.Value)
            .Select(technology => technology.GetReference())
            .Where(item => BelongsToManufacturingTab(item, manufacturingTab))
            .Where(item => IManufacturable.CanBeManufacturedBy(item, playerFactionId))
            .OrderBy(item => item.GetResearchOrder())
            .ThenBy(item => item.GetDisplayName())
            .ToList();
        if (
            manufacturingType == ManufacturingType.Building
            && destination is Planet destinationPlanet
        )
        {
            IReadOnlyList<Building> unlockedBuildings = selections.OfType<Building>().ToList();
            selections.AddRange(
                destinationPlanet
                    .GetChildren<Building>()
                    .Where(building =>
                        building.IsDestroyed
                        && building.OwnerInstanceID == playerFactionId
                        && (
                            building.ManufacturingStatus == ManufacturingStatus.Complete
                            || building.ManufacturingStatus == ManufacturingStatus.Building
                                && producer != null
                                && string.Equals(
                                    building.ProducerPlanetID,
                                    producer.InstanceID,
                                    StringComparison.Ordinal
                                )
                        )
                    )
                    .GroupBy(building => building.TypeID)
                    .Select(group =>
                        group
                            .OrderByDescending(building =>
                                building.ManufacturingStatus == ManufacturingStatus.Building
                            )
                            .ThenBy(building => building.ManufacturingQueueSequence)
                            .ThenBy(building => building.InstanceID)
                            .First()
                    )
                    .OrderBy(building => building.ResearchOrder)
                    .ThenBy(building => building.GetDisplayName())
            );
            if (producer != null)
            {
                selections.AddRange(
                    GetUpgradeSelections(
                        destinationPlanet,
                        producer,
                        playerFactionId,
                        unlockedBuildings
                    )
                );
            }
        }

        return selections.AsReadOnly();
    }

    /// <summary>
    /// Gets the selectable item indexes that can currently be assigned to the production lane.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="items">The available build templates.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The indexes whose templates can be assigned to the production lane.</returns>
    public HashSet<int> GetCanStartSelections(
        Planet producer,
        ISceneNode destination,
        IReadOnlyList<IManufacturable> items,
        int buildCount,
        string playerFactionId
    )
    {
        HashSet<int> selections = new HashSet<int>();
        if (items == null)
            return selections;

        for (int index = 0; index < items.Count; index++)
        {
            IManufacturable item = items[index];
            bool canStart = item is BuildingUpgradeSelection upgrade
                ? upgrade.Sources.All(source => ReferenceEquals(source.GetParent(), destination))
                    && IsOwnedUpgrade(producer, upgrade, playerFactionId)
                    && services
                        .GetService<ManufacturingQueries>()
                        .CanSetBuildingUpgradeOrder(
                            producer,
                            upgrade.Sources,
                            upgrade.Target,
                            buildCount,
                            playerFactionId
                        )
                : services
                    .GetService<ManufacturingQueries>()
                    .CanSetManufacturingOrder(
                        producer,
                        item,
                        destination,
                        buildCount,
                        playerFactionId
                    );
            if (canStart)
                selections.Add(index);
        }

        return selections;
    }

    /// <summary>
    /// Calculates completion and deployment estimates for selectable build templates.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="items">The available build templates.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <param name="canStartSelections">The indexes currently eligible to start.</param>
    /// <returns>One estimate per build template, with null entries for unavailable items.</returns>
    public IReadOnlyList<ConstructionBuildEstimate> GetBuildEstimates(
        Planet producer,
        ISceneNode destination,
        IReadOnlyList<IManufacturable> items,
        int buildCount,
        IReadOnlyCollection<int> canStartSelections
    )
    {
        if (items == null)
            return Array.Empty<ConstructionBuildEstimate>();

        List<ConstructionBuildEstimate> estimates = new List<ConstructionBuildEstimate>(
            items.Count
        );
        for (int index = 0; index < items.Count; index++)
        {
            estimates.Add(
                canStartSelections?.Contains(index) == true
                    ? CreateBuildEstimate(producer, destination, items[index], buildCount)
                    : null
            );
        }

        return estimates.AsReadOnly();
    }

    /// <summary>
    /// Sets an eligible construction order.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="selected">The selected build template.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>True when the production lane accepted the order.</returns>
    public bool TrySetConstructionOrder(
        Planet producer,
        ISceneNode destination,
        IManufacturable selected,
        int buildCount,
        string playerFactionId
    )
    {
        if (selected is BuildingUpgradeSelection upgrade)
        {
            if (
                !upgrade.Sources.All(source => ReferenceEquals(source.GetParent(), destination))
                || !IsOwnedUpgrade(producer, upgrade, playerFactionId)
            )
            {
                return false;
            }

            return services
                .GetService<ManufacturingCommands>()
                .SetBuildingUpgradeOrder(
                    producer,
                    upgrade.Sources,
                    upgrade.Target,
                    buildCount,
                    playerFactionId
                );
        }

        return services
            .GetService<ManufacturingCommands>()
            .SetManufacturingOrder(producer, selected, destination, buildCount, playerFactionId);
    }

    /// <summary>
    /// Creates a build estimate when either completion or deployment can be calculated.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="selected">The selected build template.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <returns>The available estimates, or null when neither value can be calculated.</returns>
    private ConstructionBuildEstimate CreateBuildEstimate(
        Planet producer,
        ISceneNode destination,
        IManufacturable selected,
        int buildCount
    )
    {
        int? completionTicks = CalculateCompletionTicks(producer, selected, buildCount);
        int? deploymentTicks = CalculateDeploymentTicks(producer, destination, selected);
        return completionTicks.HasValue || deploymentTicks.HasValue
            ? new ConstructionBuildEstimate(completionTicks, deploymentTicks)
            : null;
    }

    /// <summary>
    /// Calculates the manufacturing duration for a requested quantity.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="selected">The selected build template.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <returns>The completion duration, or null when no facility can manufacture the item.</returns>
    private static int? CalculateCompletionTicks(
        Planet producer,
        IManufacturable selected,
        int buildCount
    )
    {
        if (selected is BuildingUpgradeSelection upgrade)
        {
            Building activeSource = upgrade.Sources.FirstOrDefault(source =>
                IsActiveUpgrade(producer, source, upgrade.Target.TypeID)
            );
            return ManufacturingQueries.EstimateManufacturingOrderTicks(
                producer,
                activeSource ?? upgrade,
                buildCount
            );
        }

        return ManufacturingQueries.EstimateManufacturingOrderTicks(producer, selected, buildCount);
    }

    /// <summary>
    /// Calculates the transit duration from the producer to the selected destination.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="selected">The selected build template.</param>
    /// <returns>The deployment duration, or null for stationary or unreachable items.</returns>
    private int? CalculateDeploymentTicks(
        Planet producer,
        ISceneNode destination,
        IManufacturable selected
    )
    {
        if (selected is BuildingUpgradeSelection or Building { IsDestroyed: true })
            return null;

        if (selected is not IMovable movable)
            return null;

        if (destination is not ContainerNode destinationContainer)
            return null;

        return services
            .GetService<MovementQueries>()
            .TryEstimateManufacturedTransitTicks(
                movable,
                producer,
                destinationContainer,
                out int transitTicks
            )
            ? transitTicks
            : null;
    }

    /// <summary>
    /// Determines whether a build template belongs to an authored manufacturing panel.
    /// </summary>
    /// <param name="item">The build template.</param>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <returns>True when the item belongs to the panel.</returns>
    private static bool BelongsToManufacturingTab(
        IManufacturable item,
        FacilityWindowTab manufacturingTab
    )
    {
        return manufacturingTab switch
        {
            FacilityWindowTab.Shipyards => item is CapitalShip or Starfighter,
            FacilityWindowTab.Training => item is Regiment or SpecialForces,
            FacilityWindowTab.Construction => item is Building,
            _ => false,
        };
    }

    /// <summary>
    /// Gets available in-place upgrades for the selected destination.
    /// </summary>
    /// <param name="destination">The planet containing the installed buildings.</param>
    /// <param name="producer">The planet operating the construction lane.</param>
    /// <param name="ownerInstanceId">The faction requesting construction.</param>
    /// <param name="unlockedBuildings">The faction's unlocked building definitions.</param>
    /// <returns>The available upgrade selections in display order.</returns>
    private static IEnumerable<BuildingUpgradeSelection> GetUpgradeSelections(
        Planet destination,
        Planet producer,
        string ownerInstanceId,
        IReadOnlyList<Building> unlockedBuildings
    )
    {
        return destination
            .GetChildren<Building>()
            .Where(source =>
                !source.IsDestroyed
                && string.Equals(source.OwnerInstanceID, ownerInstanceId, StringComparison.Ordinal)
                && (
                    source.IsOperational()
                    || source.IsUpgrading()
                        && string.Equals(
                            source.ProducerPlanetID,
                            producer.InstanceID,
                            StringComparison.Ordinal
                        )
                )
            )
            .SelectMany(source =>
                unlockedBuildings
                    .Where(target =>
                        source.CanUpgradeTo(target)
                        && (
                            !source.IsUpgrading()
                            || string.Equals(
                                source.UpgradeTargetTypeID,
                                target.TypeID,
                                StringComparison.Ordinal
                            )
                        )
                    )
                    .Select(target => (Source: source, Target: target))
            )
            .GroupBy(selection =>
                (SourceTypeID: selection.Source.TypeID, TargetTypeID: selection.Target.TypeID)
            )
            .Select(group => new BuildingUpgradeSelection(
                group
                    .Select(selection => selection.Source)
                    .OrderByDescending(source =>
                        IsActiveUpgrade(producer, source, group.Key.TargetTypeID)
                    )
                    .ThenBy(source => source.InstanceID, StringComparer.Ordinal)
                    .ToList(),
                group.First().Target
            ))
            .OrderBy(selection => selection.Target.ResearchOrder)
            .ThenBy(selection => selection.Target.GetDisplayName(), StringComparer.Ordinal)
            .ThenBy(selection => selection.Source.GetDisplayName(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns whether a selection represents the producer's current in-place upgrade.
    /// </summary>
    /// <param name="producer">The planet operating the construction lane.</param>
    /// <param name="source">The installed building.</param>
    /// <param name="targetTypeID">The authored upgrade target type.</param>
    /// <returns>True when the exact source and target are currently queued.</returns>
    private static bool IsActiveUpgrade(Planet producer, Building source, string targetTypeID)
    {
        if (
            producer == null
            || source?.IsUpgrading() != true
            || !string.Equals(source.UpgradeTargetTypeID, targetTypeID, StringComparison.Ordinal)
            || !producer
                .GetManufacturingQueue()
                .TryGetValue(ManufacturingType.Building, out List<IManufacturable> queue)
        )
        {
            return false;
        }

        return queue?.Any(item => ReferenceEquals(item, source)) == true;
    }

    /// <summary>
    /// Returns whether the player owns both sides of an upgrade operation.
    /// </summary>
    /// <param name="producer">The planet operating the construction lane.</param>
    /// <param name="selection">The upgrade selection.</param>
    /// <param name="ownerInstanceId">The faction requesting the operation.</param>
    /// <returns>True when the producer and installed building have the requested owner.</returns>
    private static bool IsOwnedUpgrade(
        Planet producer,
        BuildingUpgradeSelection selection,
        string ownerInstanceId
    )
    {
        return producer != null
            && selection?.Sources?.Count > 0
            && !string.IsNullOrEmpty(ownerInstanceId)
            && string.Equals(
                producer.GetOwnerInstanceID(),
                ownerInstanceId,
                StringComparison.Ordinal
            )
            && selection.Sources.All(source =>
                string.Equals(
                    source.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
            );
    }

    /// <summary>
    /// Gets a faction from the active game by its instance identifier.
    /// </summary>
    /// <param name="factionId">The faction instance identifier.</param>
    /// <returns>The matching faction, or null.</returns>
    private Faction GetFaction(string factionId)
    {
        return services
            .GetService<GameRoot>()
            ?.GetFactions()
            .FirstOrDefault(faction =>
                string.Equals(faction.InstanceID, factionId, StringComparison.Ordinal)
            );
    }
}

/// <summary>
/// Contains immutable completion and deployment estimates for one build template.
/// </summary>
public sealed class ConstructionBuildEstimate
{
    public int? CompletionTicks { get; }

    public int? DeploymentTicks { get; }

    /// <summary>
    /// Creates a construction estimate.
    /// </summary>
    /// <param name="completionTicks">The manufacturing duration, or null when unavailable.</param>
    /// <param name="deploymentTicks">The deployment duration, or null when unavailable.</param>
    public ConstructionBuildEstimate(int? completionTicks, int? deploymentTicks)
    {
        CompletionTicks = completionTicks;
        DeploymentTicks = deploymentTicks;
    }
}
