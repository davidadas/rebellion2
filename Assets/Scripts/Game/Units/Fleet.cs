using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Defines how a fleet is used by combat, defense, and AI planning.
    /// </summary>
    public enum FleetRoleType
    {
        None,
        Battle,
        Colonization,
    }

    /// <summary>
    /// Represents a movable fleet node that carries capital ships and durable fleet orders.
    /// Fleets are scene graph containers used for travel, combat, blockade, bombardment,
    /// assaults, and system presence.
    /// </summary>
    public class Fleet : ContainerNode, IMovable
    {
        // Movement Info.
        public MovementState Movement
        {
            get => _movement;
            set
            {
                if (ReferenceEquals(_movement, value))
                    return;
                _movement = value;
                OnAvailabilityChanged();
            }
        }

        private MovementState _movement;

        public FleetRoleType RoleType { get; set; } = FleetRoleType.None;

        public FleetOrder Order { get; set; }

        public List<string> Waypoints { get; set; } = new List<string>();

        [PersistableIgnore]
        public bool IsInCombat { get; private set; }

        // Child Nodes.
        [PersistableMember(Name = "CapitalShips")]
        private List<CapitalShip> _capitalShips = new List<CapitalShip>();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Fleet() { }

        /// <summary>
        /// Creates a fleet with an optional initial collection of capital ships.
        /// </summary>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <param name="displayName">The displayed fleet name.</param>
        /// <param name="capitalShips">The initial capital ships, or null.</param>
        public Fleet(
            string ownerInstanceId,
            string displayName,
            List<CapitalShip> capitalShips = null
        )
        {
            OwnerInstanceID = ownerInstanceId;
            DisplayName = displayName;
            _capitalShips = capitalShips ?? new List<CapitalShip>();
        }

        /// <summary>
        /// Creates an empty fleet copy.
        /// </summary>
        /// <returns>The created node copy.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new Fleet();

        /// <summary>
        /// Copies fleet state into an empty destination.
        /// </summary>
        /// <param name="destination">The destination.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Fleet copy = (Fleet)destination;
            copy.Movement = Movement?.CreateCopy();
            copy.RoleType = RoleType;
            copy.Order = Order?.CreateCopy();
            copy.Waypoints = new List<string>(Waypoints ?? new List<string>());
            copy.IsInCombat = IsInCombat;
        }

        /// <summary>
        /// Returns whether the fleet has an active or queued waypoint.
        /// </summary>
        /// <returns>True when at least one waypoint remains.</returns>
        public bool HasWaypoints()
        {
            return Waypoints?.Count > 0;
        }

        /// <summary>
        /// Sets whether the fleet is engaged in combat. Entering combat cancels its remaining
        /// waypoint route.
        /// </summary>
        /// <param name="isInCombat">Whether the fleet is engaged in combat.</param>
        public void SetCombatState(bool isInCombat)
        {
            IsInCombat = isInCombat;
            if (isInCombat)
                Waypoints.Clear();
        }

        /// <summary>
        /// Replaces the fleet's child collection while constructing a detached projection.
        /// </summary>
        /// <param name="capitalShips">The capital ships to retain in the projection.</param>
        internal void SetCapitalShips(IEnumerable<CapitalShip> capitalShips)
        {
            _capitalShips = capitalShips?.ToList() ?? new List<CapitalShip>();
        }

        /// <summary>
        /// Returns the total starfighter capacity of the fleet.
        /// </summary>
        /// <returns>Sum of starfighter capacity across all capital ships.</returns>
        public int GetStarfighterCapacity()
        {
            return GetChildren<CapitalShip>().Sum(ship => ship.GetStarfighterCapacity());
        }

        /// <summary>
        /// Returns the total starfighters currently assigned.
        /// </summary>
        /// <returns>Total starfighter count across all capital ships.</returns>
        public int GetCurrentStarfighterCount()
        {
            return GetChildren<CapitalShip>().Sum(ship => ship.GetCurrentStarfighterCount());
        }

        /// <summary>
        /// Returns unused starfighter capacity.
        /// </summary>
        /// <returns>Available starfighter slots remaining.</returns>
        public int GetExcessStarfighterCapacity()
        {
            return GetStarfighterCapacity() - GetCurrentStarfighterCount();
        }

        /// <summary>
        /// Returns total regiment capacity.
        /// </summary>
        /// <returns>Sum of regiment capacity across all capital ships.</returns>
        public int GetRegimentCapacity()
        {
            return GetChildren<CapitalShip>().Sum(ship => ship.GetRegimentCapacity());
        }

        /// <summary>
        /// Returns current regiment count.
        /// </summary>
        /// <returns>Total regiment count across all capital ships.</returns>
        public int GetCurrentRegimentCount()
        {
            return GetChildren<CapitalShip>().Sum(ship => ship.GetCurrentRegimentCount());
        }

        /// <summary>
        /// Returns unused regiment capacity.
        /// </summary>
        /// <returns>Available regiment slots remaining.</returns>
        public int GetExcessRegimentCapacity()
        {
            return GetRegimentCapacity() - GetCurrentRegimentCount();
        }

        /// <summary>
        /// Returns all starfighters across the fleet (both in capital ships and as transport passengers).
        /// </summary>
        /// <returns>All starfighters in the fleet.</returns>
        public IEnumerable<Starfighter> GetStarfighters()
        {
            return GetChildren<CapitalShip>().SelectMany(ship => ship.GetChildren<Starfighter>());
        }

        /// <summary>
        /// Finds the first capital ship in this fleet with free starfighter-carrier capacity.
        /// </summary>
        /// <returns>A capital ship with excess starfighter capacity, or null if none has room.</returns>
        public CapitalShip FindShipForStarfighter()
        {
            if (Movement != null)
                return null;

            return GetChildren<CapitalShip>()
                .FirstOrDefault(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                    && ship.GetExcessStarfighterCapacity() > 0
                );
        }

        /// <summary>
        /// Finds the first capital ship in this fleet with free regiment-transport capacity.
        /// </summary>
        /// <returns>A capital ship with excess regiment capacity, or null if none has room.</returns>
        public CapitalShip FindShipForRegiment()
        {
            if (Movement != null)
                return null;

            return GetChildren<CapitalShip>()
                .FirstOrDefault(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                    && ship.GetExcessRegimentCapacity() > 0
                );
        }

        /// <summary>
        /// Returns all regiments across the fleet.
        /// </summary>
        /// <returns>All regiments in the fleet.</returns>
        public IEnumerable<Regiment> GetRegiments()
        {
            return GetChildren<CapitalShip>().SelectMany(ship => ship.GetChildren<Regiment>());
        }

        /// <summary>
        /// Returns all special forces carried by the fleet's capital ships.
        /// </summary>
        /// <returns>Special forces currently assigned to ships in this fleet.</returns>
        public IEnumerable<SpecialForces> GetSpecialForces()
        {
            return GetChildren<CapitalShip>().SelectMany(ship => ship.GetChildren<SpecialForces>());
        }

        /// <summary>
        /// Returns all officers across the fleet.
        /// </summary>
        /// <returns>All officers in the fleet.</returns>
        public IEnumerable<Officer> GetOfficers()
        {
            return GetChildren<CapitalShip>().SelectMany(ship => ship.GetChildren<Officer>());
        }

        /// <summary>
        /// Returns the number of complete, stationary capital ships in this fleet.
        /// </summary>
        /// <returns>The operational capital ship count.</returns>
        public int GetOperationalCapitalShipCount()
        {
            return GetChildren<CapitalShip>()
                .Count(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                );
        }

        /// <summary>
        /// Returns true when this fleet has complete, stationary capital ships.
        /// </summary>
        /// <returns>True when at least one operational capital ship is present.</returns>
        public bool HasOperationalCapitalShips()
        {
            return GetOperationalCapitalShipCount() > 0;
        }

        /// <summary>
        /// Adds a capital ship to the fleet.
        /// </summary>
        /// <param name="capitalShip">The capital ship to add.</param>
        private void AddCapitalShip(CapitalShip capitalShip)
        {
            if (this.OwnerInstanceID != capitalShip.OwnerInstanceID)
            {
                throw new SceneAccessException(capitalShip, this);
            }

            // A system holds only one planet destroyer, however the ship comes to be here.
            CapitalShip present = FindConflictingPlanetDestroyer(capitalShip);
            if (present != null)
                throw new SceneAccessException(
                    $"Cannot add \"{capitalShip.GetDisplayName()}\" to \"{GetDisplayName()}\": "
                        + $"\"{present.GetDisplayName()}\" is already in the system, and a system "
                        + "holds only one planet destroyer."
                );

            _capitalShips.Add(capitalShip);
        }

        /// <summary>
        /// Gets whether the fleet carries a planet-destroying capital ship, complete or still
        /// under construction.
        /// </summary>
        public bool HasPlanetDestroyer => _capitalShips.Any(ship => ship.CanDestroyPlanets);

        /// <summary>
        /// Returns the planet destroyer that keeps a ship out of this fleet. A system holds only
        /// one planet destroyer, so a second is refused whether the first is in this fleet or in
        /// another fleet at, or on its way to, the same planet.
        /// </summary>
        /// <param name="ship">The ship that would join the fleet.</param>
        /// <returns>The planet destroyer already in the system, or null when the ship may join.</returns>
        public CapitalShip FindConflictingPlanetDestroyer(CapitalShip ship)
        {
            if (ship?.CanDestroyPlanets != true)
                return null;

            return _capitalShips.FirstOrDefault(other =>
                    other.CanDestroyPlanets && !ReferenceEquals(other, ship)
                ) ?? GetParentOfType<Planet>()?.FindPlanetDestroyer(excludedShip: ship);
        }

        /// <summary>
        /// Returns true if the child is a CapitalShip owned by the same faction as this fleet,
        /// and is not a second planet destroyer for the system the fleet is in.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if the fleet can take the ship; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child) =>
            child is CapitalShip cs
            && cs.GetOwnerInstanceID() == GetOwnerInstanceID()
            && FindConflictingPlanetDestroyer(cs) == null;

        /// <summary>
        /// Returns whether the fleet can accept a ship alongside other planned additions: two
        /// planet destroyers cannot be planned into one fleet together.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <param name="plannedChildren">The children already planned for addition.</param>
        /// <returns>True when the ship can be accepted.</returns>
        internal override bool CanAcceptChild(
            ISceneNode child,
            IReadOnlyCollection<ISceneNode> plannedChildren
        )
        {
            return CanAcceptChild(child)
                && (
                    child is not CapitalShip { CanDestroyPlanets: true }
                    || !plannedChildren
                        .OfType<CapitalShip>()
                        .Any(planned =>
                            planned.CanDestroyPlanets && !ReferenceEquals(planned, child)
                        )
                );
        }

        /// <summary>
        /// Adds a child node to the fleet. Only capital ships are accepted directly;
        /// any other scene-node type is rejected.
        /// </summary>
        /// <param name="child">The child node to add; must be a <see cref="CapitalShip"/>.</param>
        public override void AddChild(ISceneNode child)
        {
            if (child is CapitalShip capitalShip)
            {
                AddCapitalShip(capitalShip);
            }
            else
            {
                throw new SceneAccessException(child, this);
            }
        }

        /// <summary>
        /// Removes a child node.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is CapitalShip capitalShip)
            {
                _capitalShips.Remove(capitalShip);
            }
        }

        /// <summary>
        /// Sum of damage-adjusted attack ratings across completed, non-in-transit
        /// capital ships and starfighters.
        /// </summary>
        /// <returns>The requested combat value.</returns>
        public int GetCombatValue()
        {
            IEnumerable<CapitalShip> activeShips = GetChildren<CapitalShip>()
                .Where(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                );

            int capitalShipCombat = activeShips.Sum(ship => ship.GetCombatValue());

            int starfighterCombat = activeShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Sum(fighter => fighter.GetCombatValue());

            return capitalShipCombat + starfighterCombat;
        }

        /// <summary>
        /// Planetary assault strength: <c>(personnel / divisor + 1) * combat_value</c>.
        /// Personnel comes from the fleet commander's Leadership skill. The commander
        /// must be a General; only ground officers contribute to assault personnel.
        /// Fleets without a General get a baseline strength equal to the combat value.
        /// </summary>
        /// <param name="assaultPersonnelDivisor">
        /// Divisor from <see cref="GameConfig.PlanetaryAssaultConfig.PersonnelDivisor"/>.
        /// </param>
        /// <returns>The fleet's assault strength.</returns>
        public int GetAssaultStrength(int assaultPersonnelDivisor)
        {
            Officer commander = GetOfficers()
                .FirstOrDefault(o => o.CurrentRank == OfficerRank.General);
            int personnel = commander?.GetEffectiveRating(SkillRating.Leadership) ?? 0;
            return (personnel / assaultPersonnelDivisor + 1) * GetCombatValue();
        }

        /// <summary>
        /// Determines if the fleet can move.
        /// </summary>
        /// <returns>True if the fleet is not currently moving.</returns>
        public bool IsMovable()
        {
            // Movement == null means not moving (can be moved)
            return Movement == null;
        }

        /// <summary>
        /// Collects direct children without allocating a raw traversal iterator.
        /// </summary>
        /// <typeparam name="T">The child type retained in the snapshot.</typeparam>
        /// <param name="collector">The current query's validation and collection state.</param>
        protected override void CollectDirectChildren<T>(ref DirectChildCollector<T> collector)
        {
            if (GetType() != typeof(Fleet))
            {
                base.CollectDirectChildren(ref collector);
                return;
            }

            foreach (CapitalShip child in _capitalShips)
                collector.Add(child);
        }

        /// <summary>
        /// Enumerates the fleet's direct children (its capital ships) as scene nodes.
        /// </summary>
        /// <returns>An enumerable over the fleet's capital ships.</returns>
        protected override IEnumerable<ISceneNode> EnumerateChildren()
        {
            return _capitalShips;
        }
    }
}
