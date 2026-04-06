using Rebellion.Util.Attributes;
using Rebellion.Util.Extensions;

namespace Rebellion.Game
{
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
        /// <returns>The manufacturable item this technology node references.</returns>
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

            // Set directly on the property to bypass the Complete→Building guard,
            // which is meant for live game objects, not freshly cloned templates.
            clonedManufacturable.ManufacturingStatus = ManufacturingStatus.Building;
            if (clonedManufacturable is IMovable movable)
            {
                // New manufactured items start at rest (no movement)
                movable.Movement = null;
            }

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
}
