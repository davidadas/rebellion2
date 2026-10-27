using System.Collections.Generic;
using System.Drawing;
using Rebellion.Game.Units;
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

        [PersistableMember(Name = "Planets")]
        private List<Planet> _planets = new List<Planet>();

        private readonly HashSet<CapitalShip> _suppressionSources = new HashSet<CapitalShip>();
        private string _projectedSectorSuppressionOwnerInstanceID;
        private int? _projectedSectorSuppressionDivisor;

        /// <summary>
        /// Returns the strongest operational, stationary suppression divisor owned by a faction.
        /// </summary>
        /// <param name="ownerInstanceID">The faction whose suppression sources are evaluated.</param>
        /// <returns>The strongest divisor, or one when the faction has no active source.</returns>
        internal int GetSectorSuppressionDivisor(string ownerInstanceID)
        {
            if (string.IsNullOrEmpty(ownerInstanceID))
                return 1;
            if (_projectedSectorSuppressionDivisor.HasValue)
                return ownerInstanceID == _projectedSectorSuppressionOwnerInstanceID
                    ? _projectedSectorSuppressionDivisor.Value
                    : 1;

            int divisor = 1;
            foreach (CapitalShip ship in _suppressionSources)
            {
                if (ship.OwnerInstanceID == ownerInstanceID && IsActiveSuppressionSource(ship))
                    divisor = System.Math.Max(divisor, ship.SectorSuppression);
            }
            return divisor;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public PlanetSector() { }

        /// <summary>
        /// Captures owner-scoped sector suppression for a view with partial descendants.
        /// </summary>
        /// <param name="ownerInstanceID">The faction whose suppression was observed.</param>
        /// <param name="divisor">The observed suppression divisor.</param>
        internal void SetProjectedSectorSuppression(string ownerInstanceID, int divisor)
        {
            _projectedSectorSuppressionOwnerInstanceID = ownerInstanceID;
            _projectedSectorSuppressionDivisor = divisor;
        }

        /// <summary>
        /// Determines whether a registered ship currently provides sector suppression.
        /// </summary>
        /// <param name="ship">The registered suppression source.</param>
        /// <returns>True when the ship is complete, stationary, and active.</returns>
        private static bool IsActiveSuppressionSource(CapitalShip ship)
        {
            return ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null
                && ship.GetParentOfType<Fleet>()?.Movement == null
                && ship.IsActive();
        }

        /// <summary>
        /// Registers a suppression source whose ancestor chain includes this sector.
        /// </summary>
        /// <param name="ship">The candidate ship.</param>
        internal void RegisterSuppressionSource(CapitalShip ship)
        {
            _suppressionSources.Add(ship);
        }

        /// <summary>
        /// Removes a ship that no longer supplies suppression-source presence in this sector.
        /// </summary>
        /// <param name="ship">The candidate ship.</param>
        internal void UnregisterSuppressionSource(CapitalShip ship)
        {
            _suppressionSources.Remove(ship);
        }

        /// <summary>
        /// Creates an empty planet-sector copy.
        /// </summary>
        /// <returns>An empty planet sector.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new PlanetSector();

        /// <summary>
        /// Copies planet-sector state into an empty destination.
        /// </summary>
        /// <param name="destination">The destination node.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            PlanetSector copy = (PlanetSector)destination;
            copy.SectorDataId = SectorDataId;
            copy.Visibility = Visibility;
            copy.SectorType = SectorType;
            copy.Importance = Importance;
            copy.PositionX = PositionX;
            copy.PositionY = PositionY;
        }

        /// <summary>
        /// Replaces the sector's planet collection while constructing a detached projection.
        /// </summary>
        /// <param name="planets">The planets to retain in the projection.</param>
        internal void SetPlanets(IEnumerable<Planet> planets)
        {
            _planets = planets == null ? new List<Planet>() : new List<Planet>(planets);
        }

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
                _planets.Add(planet);
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
                _planets.Remove(planet);
            }
        }

        /// <summary>
        /// Returns the planets in the planet sector.
        /// </summary>
        /// <returns>The planets in the planet sector.</returns>
        protected override IEnumerable<ISceneNode> EnumerateChildren()
        {
            return _planets;
        }
    }
}
