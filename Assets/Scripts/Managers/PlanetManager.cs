using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

/// <summary>
/// Manages updates to planets, including uprising status and manufacturing progress.
/// </summary>
public class PlanetManager
{
    private GameRoot game;

    /// <summary>
    /// Initializes a new instance of the PlanetManager with the specified game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public PlanetManager(GameRoot game)
    {
        this.game = game;
    }

    /// <summary>
    /// Updates the planet's uprising status and manufacturing progress.
    /// </summary>
    /// <param name="planet">The planet to update.</param>
    public void UpdatePlanet(Planet planet)
    {
        UpdateUprisingStatus(planet);
        UpdateManufacturing(planet);
    }

    /// <summary>
    /// Adds items to the manufacturing queue of a planet.
    /// </summary>
    /// <param name="owner">The faction who will manufacture the provided unit.</param>
    /// <param name="source">The planet where manufacturing will occur.</param>
    /// <param name="target">The target scene node where the manufactured item will be delivered.</param>
    /// <param name="technology">The technology to be manufactured.</param>
    /// <param name="quantity">The number of items to add to the queue.</param>
    public void AddToManufacturingQueue(
        Faction owner,
        Planet source,
        ISceneNode target,
        Technology technology,
        int quantity = 1
    )
    {
        IManufacturable manufacturable = technology.GetReference();
        int totalCosts = manufacturable.GetConstructionCost() * quantity;

        if (totalCosts > game.GetRefinedMaterials(owner))
        {
            throw new InvalidSceneOperationException(
                $"Faction {owner.GetDisplayName()} does not have sufficient funds to craft {manufacturable.GetDisplayName()}"
            );
        }

        for (int i = 0; i < quantity; i++)
        {
            IManufacturable clonedNode = technology.GetReferenceCopy();
            clonedNode.SetOwnerInstanceID(owner.GetInstanceID());
            GameLogger.Log(
                $"{source.GetDisplayName()} adding {quantity} {clonedNode.GetDisplayName()}(s) to manufacturing queue on {source.GetDisplayName()}"
            );
            game.AttachNode(clonedNode, target);
            source.AddToManufacturingQueue(clonedNode);
        }
    }

    /// <summary>
    /// Increments manufacturing progress for all items on a given planet.
    /// </summary>
    /// <param name="planet">The planet on which manufacturing is taking place.</param>
    /// <returns>A list of completed manufacturables.</returns>
    private List<IManufacturable> UpdateManufacturing(Planet planet)
    {
        List<IManufacturable> completedManufacturables = new List<IManufacturable>();
        Dictionary<ManufacturingType, List<IManufacturable>> manufacturingQueue =
            planet.GetManufacturingQueue();

        foreach (var kvp in manufacturingQueue)
        {
            ManufacturingType type = kvp.Key;
            List<IManufacturable> productionQueue = kvp.Value;
            int remainingProgress = planet.GetProductionRate(type);

            for (int i = 0; i < productionQueue.Count && remainingProgress > 0; i++)
            {
                IManufacturable manufacturable = productionQueue[i];
                remainingProgress = ApplyManufacturingProgress(
                    manufacturable,
                    remainingProgress,
                    completedManufacturables
                );

                if (manufacturable.IsManufacturingComplete())
                {
                    CompleteManufacturing(manufacturable, productionQueue, ref i);
                }
            }
        }

        return completedManufacturables;
    }

    /// <summary>
    /// Applies progress to a manufacturable item, updating its state.
    /// </summary>
    /// <param name="manufacturable">The manufacturable item.</param>
    /// <param name="remainingProgress">The remaining progress to be applied.</param>
    /// <param name="completedManufacturables">List to collect completed items.</param>
    /// <returns>The remaining progress after application.</returns>
    private int ApplyManufacturingProgress(
        IManufacturable manufacturable,
        int remainingProgress,
        List<IManufacturable> completedManufacturables
    )
    {
        int requiredProgress =
            manufacturable.GetConstructionCost() - manufacturable.GetManufacturingProgress();
        int progressToApply = Math.Min(remainingProgress, requiredProgress);

        manufacturable.IncrementManufacturingProgress(progressToApply);
        remainingProgress -= progressToApply;

        if (manufacturable.IsManufacturingComplete())
        {
            completedManufacturables.Add(manufacturable);
        }

        return remainingProgress;
    }

    /// <summary>
    /// Completes the manufacturing process for an item.
    /// </summary>
    /// <param name="manufacturable">The completed manufacturable item.</param>
    /// <param name="productionQueue">The queue from which to remove the item.</param>
    /// <param name="index">The index of the item in the queue.</param>
    private void CompleteManufacturing(
        IManufacturable manufacturable,
        List<IManufacturable> productionQueue,
        ref int index
    )
    {
        GameLogger.Log(
            $"Manufacturable completed: {manufacturable.GetDisplayName()} {manufacturable.GetParent().GetDisplayName()}"
        );
        productionQueue.RemoveAt(index);
        index--; // Decrement index to account for the removed item.

        manufacturable.SetManufacturingStatus(ManufacturingStatus.Complete);

        // Send completed item to target.
        // NOTE: This is incorrect - movement should be initiated by MovementManager.
        // This code path is obsolete (PlanetManager no longer used).
        if (manufacturable is IMovable movable)
        {
            // Would need MovementManager.RequestMove() here for proper lifecycle
            // Placeholder: just mark as null for now
            movable.Movement = null;
        }
    }

    /// <summary>
    /// Updates the uprising status of a planet.
    /// </summary>
    /// <param name="planet">The planet to update.</param>
    private void UpdateUprisingStatus(Planet planet)
    {
        // TODO: Implement uprising status update logic.
    }
}
