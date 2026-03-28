using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;
using Rebellion.Util.Extensions;

namespace Rebellion.Game
{
    public class Fleet : ContainerNode, IMovable
    {
        // Movement Info
        public MovementState Movement { get; set; }

        // Child Nodes
        public List<CapitalShip> CapitalShips { get; set; } = new List<CapitalShip>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Fleet() { }

        /// <summary>
        /// Returns the total starfighter capacity of the fleet.
        /// </summary>
        public int GetStarfighterCapacity()
        {
            return CapitalShips.Sum(ship => ship.GetStarfighterCapacity());
        }

        /// <summary>
        /// Returns the total starfighters currently assigned.
        /// </summary>
        public int GetCurrentStarfighterCount()
        {
            return CapitalShips.Sum(ship => ship.GetCurrentStarfighterCount());
        }

        /// <summary>
        /// Returns unused starfighter capacity.
        /// </summary>
        public int GetExcessStarfighterCapacity()
        {
            return GetStarfighterCapacity() - GetCurrentStarfighterCount();
        }

        /// <summary>
        /// Returns total regiment capacity.
        /// </summary>
        public int GetRegimentCapacity()
        {
            return CapitalShips.Sum(ship => ship.GetRegimentCapacity());
        }

        /// <summary>
        /// Returns current regiment count.
        /// </summary>
        public int GetCurrentRegimentCount()
        {
            return CapitalShips.Sum(ship => ship.GetCurrentRegimentCount());
        }

        /// <summary>
        /// Returns unused regiment capacity.
        /// </summary>
        public int GetExcessRegimentCapacity()
        {
            return GetRegimentCapacity() - GetCurrentRegimentCount();
        }

        /// <summary>
        /// Returns all starfighters across the fleet.
        /// </summary>
        public IEnumerable<Starfighter> GetStarfighters()
        {
            return CapitalShips.SelectMany(ship => ship.Starfighters);
        }

        /// <summary>
        /// Returns all regiments across the fleet.
        /// </summary>
        public IEnumerable<Regiment> GetRegiments()
        {
            return CapitalShips.SelectMany(ship => ship.Regiments);
        }

        /// <summary>
        /// Returns all officers across the fleet.
        /// </summary>
        public IEnumerable<Officer> GetOfficers()
        {
            return CapitalShips.SelectMany(ship => ship.Officers);
        }

        /// <summary>
        /// Adds a capital ship to the fleet.
        /// </summary>
        private void AddCapitalShip(CapitalShip capitalShip)
        {
            if (this.OwnerInstanceID != capitalShip.OwnerInstanceID)
            {
                throw new SceneAccessException(capitalShip, this);
            }

            CapitalShips.Add(capitalShip);
        }

        /// <summary>
        /// Adds an officer to the fleet (default to first ship).
        /// </summary>
        private void AddOfficer(Officer officer)
        {
            if (this.OwnerInstanceID != officer.OwnerInstanceID)
            {
                throw new SceneAccessException(officer, this);
            }

            if (CapitalShips.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot add officer to fleet with no capital ships."
                );
            }

            CapitalShips[0].AddOfficer(officer);
        }

        /// <summary>
        /// Adds a child node.
        /// </summary>
        public override void AddChild(ISceneNode child)
        {
            if (child is CapitalShip capitalShip)
            {
                AddCapitalShip(capitalShip);
            }
            else if (child is Officer officer)
            {
                AddOfficer(officer);
            }
        }

        /// <summary>
        /// Removes a child node.
        /// </summary>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is CapitalShip capitalShip)
            {
                CapitalShips.Remove(capitalShip);
            }
        }

        /// <summary>
        /// Determines if the fleet can move.
        /// </summary>
        public bool IsMovable()
        {
            // Movement == null means not moving (can be moved)
            return Movement == null;
        }

        /// <summary>
        /// Retrieves child nodes (capital ships only).
        /// </summary>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return CapitalShips.Cast<ISceneNode>();
        }
    }
}
