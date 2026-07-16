using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

/// <summary>
/// Evaluates and starts construction orders against the current game state.
/// </summary>
public sealed class ConstructionOrderController
{
    private readonly Func<Rebellion.Game.GameRoot> getGame;
    private readonly Func<ManufacturingSystem> getManufacturingSystem;
    private readonly Func<MovementSystem> getMovementSystem;

    /// <summary>
    /// Creates a construction order controller.
    /// </summary>
    /// <param name="getGame">Returns the active game.</param>
    /// <param name="getManufacturingSystem">Returns the active manufacturing system.</param>
    /// <param name="getMovementSystem">Returns the active movement system.</param>
    public ConstructionOrderController(
        Func<Rebellion.Game.GameRoot> getGame,
        Func<ManufacturingSystem> getManufacturingSystem,
        Func<MovementSystem> getMovementSystem
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.getManufacturingSystem =
            getManufacturingSystem
            ?? throw new ArgumentNullException(nameof(getManufacturingSystem));
        this.getMovementSystem =
            getMovementSystem ?? throw new ArgumentNullException(nameof(getMovementSystem));
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
        Faction faction = GetFaction(playerFactionId);
        ManufacturingType? manufacturingType = GetManufacturingType(manufacturingTab);
        if (faction == null || !manufacturingType.HasValue)
            return Array.Empty<IManufacturable>();

        return faction
            .GetUnlockedTechnologies(manufacturingType.Value)
            .Select(technology => technology.GetReference())
            .Where(item => BelongsToManufacturingTab(item, manufacturingTab))
            .Where(item => item.HasAllowedOwnerInstanceID(playerFactionId))
            .OrderBy(item => item.GetResearchOrder())
            .ThenBy(item => item.GetDisplayName())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets the selectable item indexes that can currently begin construction.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="items">The available build templates.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The indexes whose templates can begin construction.</returns>
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
            if (
                getManufacturingSystem()
                    .CanStartManufacturing(
                        producer,
                        items[index],
                        destination,
                        buildCount,
                        playerFactionId
                    )
            )
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
    /// Starts an eligible construction order.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="selected">The selected build template.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>True when the order was accepted by the game manager.</returns>
    public bool TryStartConstruction(
        Planet producer,
        ISceneNode destination,
        IManufacturable selected,
        int buildCount,
        string playerFactionId
    )
    {
        return getManufacturingSystem()
            .StartManufacturing(producer, selected, destination, buildCount, playerFactionId);
    }

    /// <summary>
    /// Maps an authored construction panel to its manufacturing category.
    /// </summary>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <returns>The matching manufacturing category, or null for an unknown panel.</returns>
    public static ManufacturingType? GetManufacturingType(FacilityWindowTab manufacturingTab)
    {
        return manufacturingTab switch
        {
            FacilityWindowTab.Shipyards => ManufacturingType.Ship,
            FacilityWindowTab.Training => ManufacturingType.Troop,
            FacilityWindowTab.Construction => ManufacturingType.Building,
            _ => null,
        };
    }

    /// <summary>
    /// Creates a build estimate when both completion and deployment can be calculated.
    /// </summary>
    /// <param name="producer">The planet performing the manufacturing.</param>
    /// <param name="destination">The node that will receive manufactured units.</param>
    /// <param name="selected">The selected build template.</param>
    /// <param name="buildCount">The requested quantity.</param>
    /// <returns>The completed estimate, or null when either value is unavailable.</returns>
    private ConstructionBuildEstimate CreateBuildEstimate(
        Planet producer,
        ISceneNode destination,
        IManufacturable selected,
        int buildCount
    )
    {
        int? completionTicks = CalculateCompletionTicks(producer, selected, buildCount);
        int? deploymentTicks = CalculateDeploymentTicks(producer, destination, selected);
        return completionTicks.HasValue && deploymentTicks.HasValue
            ? new ConstructionBuildEstimate(completionTicks.Value, deploymentTicks.Value)
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
        return ManufacturingSystem.EstimateManufacturingTicks(producer, selected, buildCount);
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
        if (selected is not IMovable movable)
            return null;

        if (destination is not ContainerNode destinationContainer)
            return null;

        return getMovementSystem()
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
            FacilityWindowTab.Construction => item is Building building
                && IsBuildableFacility(building),
            _ => false,
        };
    }

    /// <summary>
    /// Determines whether a building template is available from the construction window.
    /// </summary>
    /// <param name="building">The building template.</param>
    /// <returns>True when the template is a manufacturable facility.</returns>
    private static bool IsBuildableFacility(Building building)
    {
        return building.GetBuildingType()
            is BuildingType.Mine
                or BuildingType.Refinery
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.ConstructionFacility;
    }

    /// <summary>
    /// Gets a faction from the active game by its instance identifier.
    /// </summary>
    /// <param name="factionId">The faction instance identifier.</param>
    /// <returns>The matching faction, or null.</returns>
    private Faction GetFaction(string factionId)
    {
        return getGame()
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
    /// <summary>
    /// Creates a construction estimate.
    /// </summary>
    /// <param name="completionTicks">The manufacturing duration.</param>
    /// <param name="deploymentTicks">The deployment duration.</param>
    public ConstructionBuildEstimate(int completionTicks, int deploymentTicks)
    {
        CompletionTicks = completionTicks;
        DeploymentTicks = deploymentTicks;
    }

    /// <summary>
    /// Gets the completion ticks.
    /// </summary>
    public int CompletionTicks { get; }

    /// <summary>
    /// Gets the deployment ticks.
    /// </summary>
    public int DeploymentTicks { get; }
}
