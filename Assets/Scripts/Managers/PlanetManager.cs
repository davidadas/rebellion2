using System;
using System.Collections.Generic;
using System.Linq;
using ObjectExtensions;

/// <summary>
/// Manages updates to planets, including uprising status and manufacturing progress.
/// </summary>
public class PlanetManager
{
    private Game game;

    /// <summary>
    /// Initializes a new instance of the PlanetManager with the specified game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public PlanetManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    ///
    /// </summary>
    public void Update()
    {
        List<Planet> planets = game.GetSceneNodesByType<Planet>();

        foreach (Planet planet in planets)
        {
            UpdateUprisingStatus(planet);
            UpdateManufacturing(planet);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="planet"></param>
    /// <param name="target"></param>
    /// <param name="manufacturable"></param>
    /// <param name="quantity"></param>
    public void AddToManufacturingQueue(
        Planet planet,
        ISceneNode target,
        IManufacturable manufacturable,
        int quantity
    )
    {
        for (int i = 0; i < quantity; i++)
        {
            GameLogger.Log("Adding manufacturable to queue: " + manufacturable.GetDisplayName());
            IManufacturable clonedNode = manufacturable.GetDeepCopy();
            game.AttachNode(clonedNode, target);
            planet.AddToManufacturingQueue(clonedNode);
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

        foreach (ManufacturingType type in manufacturingQueue.Keys)
        {
            int remainingProgress = planet.GetProductionRate(type);
            List<IManufacturable> productionQueue = manufacturingQueue[type];

            for (int i = 0; i < productionQueue.Count && remainingProgress > 0; i++)
            {
                IManufacturable manufacturable = productionQueue[i];
                remainingProgress = ApplyProgress(
                    manufacturable,
                    remainingProgress,
                    completedManufacturables
                );

                // Remove completed items from queue.
                if (
                    manufacturable.GetConstructionCost()
                    <= manufacturable.GetManufacturingProgress()
                )
                {
                    GameLogger.Log(
                        "Manufacturable completed: "
                            + manufacturable.DisplayName
                            + " "
                            + manufacturable.GetParent().DisplayName
                    );
                    // Decrement index to account for removed item.
                    productionQueue.RemoveAt(i--);

                    manufacturable.SetManufacturingStatus(ManufacturingStatus.Complete);

                    // Send completed item to target.
                    (manufacturable as IMovable).MoveTo(manufacturable.GetParent());
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
    private int ApplyProgress(
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

        if (manufacturable.GetConstructionCost() <= manufacturable.GetManufacturingProgress())
        {
            completedManufacturables.Add(manufacturable);
        }

        return remainingProgress;
    }

    /// <summary>
    /// Updates the uprising status of a planet.
    /// </summary>
    /// <param name="planet">The planet to update.</param>
    private void UpdateUprisingStatus(Planet planet)
    {
        // @TODO: Implement uprising status update logic.
    }
}
