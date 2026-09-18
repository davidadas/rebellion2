using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Research
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
            return CreateManufacturingCopy(Manufacturable);
        }

        /// <summary>
        /// Creates a detached manufacturing copy through the scene node's typed copy contract.
        /// </summary>
        /// <param name="template">The immutable manufacturable template.</param>
        /// <returns>A new manufacturing item with no identity, owner, parent, or movement.</returns>
        internal static IManufacturable CreateManufacturingCopy(IManufacturable template)
        {
            if (template is not ISceneNode templateNode)
                return template?.GetDeepCopy();

            if (templateNode.CreateCopy() is not IManufacturable clonedManufacturable)
                return null;

            ISceneNode clonedNode = (ISceneNode)clonedManufacturable;
            clonedNode.InstanceID = null;
            clonedNode.SetParent(null);
            clonedNode.SetOwnerInstanceID(null);

            // Set directly on the property to bypass the Complete->Building guard,
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
        /// Returns the research order of the referenced manufacturable.
        /// </summary>
        /// <returns>Research unlock order index from the referenced manufacturable.</returns>
        public int GetResearchOrder()
        {
            return Manufacturable.GetResearchOrder();
        }

        /// <summary>
        /// Returns the research difficulty of the referenced manufacturable.
        /// </summary>
        /// <returns>Research capacity cost from the referenced manufacturable.</returns>
        public int GetResearchDifficulty()
        {
            return Manufacturable.GetResearchDifficulty();
        }
    }
}
