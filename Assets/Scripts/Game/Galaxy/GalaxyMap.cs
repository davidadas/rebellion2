using System.Collections.Generic;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Galaxy
{
    /// <summary>
    /// Represents a galaxy map in the game world. A galaxy map is a collection of planet sectors.
    /// </summary>
    public class GalaxyMap : ContainerNode
    {
        // Child Nodes.
        public List<PlanetSector> PlanetSectors { get; set; } = new List<PlanetSector>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public GalaxyMap() { }

        /// <summary>
        /// Returns true if the child is a PlanetSector.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if child is a PlanetSector; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child) => child is PlanetSector;

        /// <summary>
        /// Adds a child to the node.
        /// </summary>
        /// <param name="child">The child node to add.</param>
        public override void AddChild(ISceneNode child)
        {
            if (child is PlanetSector planetSector)
            {
                PlanetSectors.Add(planetSector);
            }
        }

        /// <summary>
        /// Removes a child from the node.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is PlanetSector planetSector)
            {
                PlanetSectors.Remove(planetSector);
            }
        }

        /// <summary>
        /// Retrieves the children of the node.
        /// </summary>
        /// <returns>An array of child nodes.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return PlanetSectors.ToArray();
        }
    }
}
