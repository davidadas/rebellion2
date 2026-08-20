using System.Collections.Generic;
using System.Drawing;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Galaxy
{
    /// <summary>
    /// The galactic region containing a planet sector.
    /// </summary>
    public enum PlanetSectorType
    {
        Core,
        OuterRim,
    }

    /// <summary>
    /// Classifies the strategic importance of a planet sector.
    /// </summary>
    public enum PlanetSectorImportance
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// Represents a named galactic sector containing a collection of planets.
    /// </summary>
    [PersistableObject]
    public class PlanetSector : ContainerNode
    {
        // Planet sector properties.
        public int SectorDataId { get; set; }
        public GameSize Visibility { get; set; }
        public PlanetSectorType SectorType { get; set; }
        public PlanetSectorImportance Importance { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        // Child Nodes.
        public List<Planet> Planets { get; set; } = new List<Planet>();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public PlanetSector() { }

        /// <summary>
        /// Returns the galactic region containing the planet sector.
        /// </summary>
        /// <returns>The sector's galactic region.</returns>
        public PlanetSectorType GetSectorType()
        {
            return SectorType;
        }

        /// <summary>
        /// Returns the position of the planet sector.
        /// </summary>
        /// <returns>The position of the planet sector as a Point.</returns>
        public Point GetPosition()
        {
            return new Point(PositionX, PositionY);
        }

        /// <summary>
        /// Returns true if the child is a Planet.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if child is a Planet; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child) => child is Planet;

        /// <summary>
        /// Adds a planet to the planet sector.
        /// </summary>
        /// <param name="child">The planet to add.</param>
        public override void AddChild(ISceneNode child)
        {
            if (child is Planet planet)
            {
                Planets.Add(planet);
            }
        }

        /// <summary>
        /// Removes a planet from the planet sector.
        /// </summary>
        /// <param name="child">The planet to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is Planet planet)
            {
                Planets.Remove(planet);
            }
        }

        /// <summary>
        /// Returns the planets in the planet sector.
        /// </summary>
        /// <returns>The planets in the planet sector.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return Planets.ToArray();
        }
    }
}
