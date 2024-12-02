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
    /// Default constructor used for deserialization.
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
    /// <seealso cref="IManufacturable"/>
    public IManufacturable GetReference()
    {
        return Manufacturable;
    }

    /// <summary>
    /// Returns a deep copy of the referenced manufacturable.
    /// </summary>
    /// <returns>The deep copy of the referenced manufacturable.</returns>
    /// <seealso cref="IManufacturable"/>
    public IManufacturable GetReferenceCopy()
    {
        IManufacturable clonedManufacturable = Manufacturable.GetDeepCopy();

        clonedManufacturable.SetManufacturingStatus(ManufacturingStatus.Building);
        clonedManufacturable.SetMovementStatus(MovementStatus.Idle);

        return clonedManufacturable;
    }

    /// <summary>
    /// Returns the manufacturing type of the referenced manufacturable.
    /// </summary>
    /// <returns>The manufacturing type of the referenced manufacturable.</returns>
    /// <seealso cref="ManufacturingType"/>
    public ManufacturingType GetManufacturingType()
    {
        return Manufacturable.GetManufacturingType();
    }

    /// <summary>
    /// Returns the required research level of the referenced manufacturable.
    /// </summary>
    /// <returns>The required research level of the referenced manufacturable.</returns>
    public int GetRequiredResearchLevel()
    {
        return Manufacturable.GetRequiredResearchLevel();
    }
}
