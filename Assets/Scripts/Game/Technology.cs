using System;
using ObjectExtensions;

[PersistableObject]
public class Technology
{
    [PersistableInclude(typeof(Building))]
    [PersistableInclude(typeof(CapitalShip))]
    [PersistableInclude(typeof(SpecialForces))]
    [PersistableInclude(typeof(Starfighter))]
    public IManufacturable Manufacturable { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Technology() { }

    /// <summary>
    /// Initializes the technology with an <see cref="IManufacturable"/> reference.
    /// </summary>
    /// <param name="manufacturable">The <see cref="IManufacturable"/> to reference.</param>
    public Technology(IManufacturable manufacturable)
    {
        Manufacturable = manufacturable;
    }

    /// <summary>
    /// Returns the referenced manufacturable.
    /// </summary>
    /// <returns></returns>
    public IManufacturable GetReference()
    {
        return Manufacturable;
    }

    /// <summary>
    /// Returns a deep copy of the referenced manufacturable.
    /// </summary>
    /// <returns>The deep copy of the referenced manufacturable.</returns>
    public IManufacturable GetReferenceCopy()
    {
        IManufacturable clonedManufacturable = Manufacturable.GetDeepCopy();

        clonedManufacturable.SetManufacturingStatus(ManufacturingStatus.Building);
        clonedManufacturable.SetMovementStatus(MovementStatus.Idle);

        return clonedManufacturable;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public ManufacturingType GetManufacturingType()
    {
        return Manufacturable.GetManufacturingType();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public int GetRequiredResearchLevel()
    {
        return Manufacturable.GetRequiredResearchLevel();
    }
}
