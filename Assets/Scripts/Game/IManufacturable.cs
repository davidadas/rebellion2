using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ManufacturingStatus
{
    Building,
    Complete,
}

public enum ManufacturingType
{
    None,
    Ship,
    Building,
    Troop,
}

/// <summary>
/// Interface for manufacturable objects with default properties and methods.
/// <remarks>
/// Manufacturable objects can be constructed, maintained, and researched and include
/// objects like ships, buildings, and troops.
/// </remarks>
/// </summary>
public interface IManufacturable : IMovable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }

    // Research Info
    public int RequiredResearchLevel { get; set; }

    // Manufacturing Info
    public string ProducerOwnerID { get; set; }
    public int ManufacturingProgress { get; set; }
    public ManufacturingStatus ManufacturingStatus { get; set; }

    /// <summary>
    /// Returns the owner ID of the producer.
    /// </summary>
    /// <returns>The owner ID of the producer.</returns>
    public string GetProducerOwnerID()
    {
        return ProducerOwnerID;
    }

    /// <summary>
    /// Returns the construction cost of the manufacturable.
    /// </summary>
    /// <returns>The construction cost of the manufacturable.</returns>
    public int GetConstructionCost()
    {
        return ConstructionCost;
    }

    /// <summary>
    /// Returns the maintenance cost of the manufacturable.
    /// </summary>
    /// <returns>The maintenance cost of the manufacturable.</returns>
    public int GetMaintenanceCost()
    {
        return MaintenanceCost;
    }

    /// <summary>
    /// Returns the research level required to manufacture the manufacturable.
    /// </summary>
    /// <returns>The research level required to manufacture the manufacturable.</returns>
    public int GetRequiredResearchLevel()
    {
        return RequiredResearchLevel;
    }

    /// <summary>
    /// Returns the manufacturing progress of the manufacturable.
    /// </summary>
    /// <returns>The manufacturing progress of the manufacturable.</returns>
    public int GetManufacturingProgress()
    {
        return ManufacturingProgress;
    }

    /// <summary>
    /// Increments the manufacturing progress of the manufacturable.
    /// </summary>
    /// <param name="progress">The amount to increment the progress by.</param>
    /// <returns>The new manufacturing progress of the manufacturable.</returns>
    public int IncrementManufacturingProgress(int progress)
    {
        ManufacturingProgress += progress;
        return ManufacturingProgress;
    }

    /// <summary>
    /// Returns the manufacturing type of the manufacturable.
    /// </summary>
    /// <returns>The manufacturing type of the manufacturable.</returns>
    public ManufacturingType GetManufacturingType();

    /// <summary>
    /// Returns the manufacturing status of the manufacturable.
    /// </summary>
    /// <returns>The manufacturing status of the manufacturable.</returns>
    public ManufacturingStatus GetManufacturingStatus()
    {
        return ManufacturingStatus;
    }

    /// <summary>
    /// Sets the manufacturing status of the manufacturable.
    /// </summary>
    /// <param name="status">The new manufacturing status.</param>
    /// <exception cref="InvalidSceneOperationException">Thrown when the status is invalid.</exception>
    public void SetManufacturingStatus(ManufacturingStatus status)
    {
        // Check for invalid status.
        if (status == ManufacturingStatus.Complete && status == ManufacturingStatus.Building)
        {
            throw new InvalidSceneOperationException(
                "Invalid manufacturing status. Cannot set to 'Building' once 'Complete'."
            );
        }

        ManufacturingStatus = status;
    }
}
