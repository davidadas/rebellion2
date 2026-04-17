using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    public enum FleetRoleType
    {
        None,
        Battle,
        Patrol,
    }

    public class Fleet : ContainerNode, IMovable
    {
        // Movement Info
        public MovementState Movement { get; set; }

        /// <summary>
        /// Designates whether this fleet is a battle fleet or a patrol/presence fleet.
        /// Battle fleets engage in combat and defend key systems.
        /// Patrol fleets provide system presence but are not sent on attack missions.
        /// </summary>
        public FleetRoleType RoleType { get; set; } = FleetRoleType.None;

        /// <summary>
        /// True while this fleet is engaged in a pending combat encounter.
        /// Cleared after combat is resolved. Not persisted to save files.
        /// </summary>
        [PersistableIgnore]
        public bool IsInCombat { get; set; }

        // Child Nodes
        public List<CapitalShip> CapitalShips { get; set; } = new List<CapitalShip>();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Fleet() { }

        public Fleet(
            string ownerInstanceId,
            string displayName,
            List<CapitalShip> capitalShips = null
        )
        {
            AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId };
            OwnerInstanceID = ownerInstanceId;
            DisplayName = displayName;
            CapitalShips = capitalShips ?? new List<CapitalShip>();
        }

        /// <summary>
        /// Returns the total starfighter capacity of the fleet.
        /// </summary>
        /// <returns>Sum of starfighter capacity across all capital ships.</returns>
        public int GetStarfighterCapacity()
        {
            return CapitalShips.Sum(ship => ship.GetStarfighterCapacity());
        }

        /// <summary>
        /// Returns the total starfighters currently assigned.
        /// </summary>
        /// <returns>Total starfighter count across all capital ships.</returns>
        public int GetCurrentStarfighterCount()
        {
            return CapitalShips.Sum(ship => ship.GetCurrentStarfighterCount());
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
            return CapitalShips.Sum(ship => ship.GetRegimentCapacity());
        }

        /// <summary>
        /// Returns current regiment count.
        /// </summary>
        /// <returns>Total regiment count across all capital ships.</returns>
        public int GetCurrentRegimentCount()
        {
            return CapitalShips.Sum(ship => ship.GetCurrentRegimentCount());
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
            return CapitalShips.SelectMany(ship => ship.Starfighters);
        }

        public CapitalShip FindShipForStarfighter()
        {
            return CapitalShips.FirstOrDefault(s => s.GetExcessStarfighterCapacity() > 0);
        }

        public CapitalShip FindShipForRegiment()
        {
            return CapitalShips.FirstOrDefault(s => s.GetExcessRegimentCapacity() > 0);
        }

        /// <summary>
        /// Returns all regiments across the fleet.
        /// </summary>
        /// <returns>All regiments in the fleet.</returns>
        public IEnumerable<Regiment> GetRegiments()
        {
            return CapitalShips.SelectMany(ship => ship.Regiments);
        }

        /// <summary>
        /// Returns all officers across the fleet.
        /// </summary>
        /// <returns>All officers in the fleet.</returns>
        public IEnumerable<Officer> GetOfficers()
        {
            return CapitalShips.SelectMany(ship => ship.Officers);
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

            CapitalShips.Add(capitalShip);
        }

        /// <summary>
        /// Returns true if the child is a CapitalShip owned by the same faction as this fleet.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if child is a same-faction CapitalShip; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child) =>
            child is CapitalShip cs && cs.GetOwnerInstanceID() == GetOwnerInstanceID();

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
                CapitalShips.Remove(capitalShip);
            }
        }

        /// <summary>
        /// Calculates total fleet combat value by summing damage-adjusted capital ship and
        /// starfighter attack ratings. Matches the original REBEXE.EXE formula where damage
        /// proportionally reduces each unit's combat contribution using integer arithmetic.
        /// </summary>
        /// <returns>Combined damage-adjusted attack rating of all completed ships and starfighters.</returns>
        public int GetCombatValue()
        {
            int capitalShipCombat = 0;
            foreach (CapitalShip s in CapitalShips)
            {
                if (s.ManufacturingStatus != ManufacturingStatus.Complete)
                    continue;

                int attackStrength = s.PrimaryWeapons.Values.Sum(arcs => arcs.Sum());
                if (s.HullStrength > 0)
                {
                    capitalShipCombat +=
                        attackStrength - (attackStrength * s.HullDamage) / s.HullStrength;
                }
                else
                {
                    capitalShipCombat += attackStrength;
                }
            }

            int starfighterCombat = 0;
            foreach (Starfighter f in GetStarfighters())
            {
                if (f.ManufacturingStatus != ManufacturingStatus.Complete)
                    continue;

                int weaponStrength = f.LaserCannon + f.IonCannon + f.Torpedoes;
                if (f.SquadronSize > 0)
                {
                    starfighterCombat +=
                        weaponStrength - (weaponStrength * f.SquadronLosses) / f.SquadronSize;
                }
                else
                {
                    starfighterCombat += weaponStrength;
                }
            }

            return capitalShipCombat + starfighterCombat;
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

        public override IEnumerable<ISceneNode> GetChildren()
        {
            return CapitalShips.Cast<ISceneNode>();
        }
    }
}
