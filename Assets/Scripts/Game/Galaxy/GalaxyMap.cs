using System.Collections.Generic;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Galaxy
{
    /// <summary>
    /// Represents a galaxy map in the game world. A galaxy map is a collection of planet systems.
    /// </summary>
    public class GalaxyMap : ContainerNode
    {
        // Child Nodes.
        [PersistableMember(Name = "PlanetSystems")]
        private List<PlanetSystem> _planetSystems = new List<PlanetSystem>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public GalaxyMap() { }

        /// <summary>
        /// Creates an empty galaxy-map copy.
        /// </summary>
        /// <returns>An empty galaxy map.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new GalaxyMap();

        /// <summary>
        /// Returns true if the child is a PlanetSystem.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if child is a PlanetSystem; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child) => child is PlanetSystem;

        /// <summary>
        /// Adds a child to the node.
        /// </summary>
        /// <param name="child">The child node to add.</param>
        public override void AddChild(ISceneNode child)
        {
            if (child is PlanetSystem planetSystem)
            {
                _planetSystems.Add(planetSystem);
            }
        }

        /// <summary>
        /// Removes a child from the node.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is PlanetSystem planetSystem)
            {
                _planetSystems.Remove(planetSystem);
            }
        }

        /// <summary>
        /// Retrieves the children of the node.
        /// </summary>
        /// <returns>An array of child nodes.</returns>
        protected override IEnumerable<ISceneNode> EnumerateChildren()
        {
            return _planetSystems;
        }
    }
}
