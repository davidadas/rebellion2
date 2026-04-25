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

        /// <summary>
        /// Finds the first capital ship in this fleet with free starfighter-carrier capacity.
        /// </summary>
        /// <returns>A capital ship with excess starfighter capacity, or null if none has room.</returns>
        public CapitalShip FindShipForStarfighter()
        {
            return CapitalShips.FirstOrDefault(s => s.GetExcessStarfighterCapacity() > 0);
        }

        /// <summary>
        /// Finds the first capital ship in this fleet with free regiment-transport capacity.
        /// </summary>
        /// <returns>A capital ship with excess regiment capacity, or null if none has room.</returns>
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
                CapitalShips.Remove(capitalShip);
            }
        }

        /// <summary>
        /// Sum of damage-adjusted attack ratings across completed, non-in-transit
        /// capital ships and starfighters.
        /// </summary>
        public int GetCombatValue()
        {
            int capitalShipCombat = 0;
            foreach (CapitalShip s in CapitalShips)
            {
                if (s.ManufacturingStatus != ManufacturingStatus.Complete || s.Movement != null)
                    continue;

                int attackStrength = s.PrimaryWeapons.Values.Sum(arcs => arcs.Sum());
                if (s.MaxHullStrength > 0)
                {
                    capitalShipCombat +=
                        (attackStrength * s.CurrentHullStrength) / s.MaxHullStrength;
                }
                else
                {
                    capitalShipCombat += attackStrength;
                }
            }

            int starfighterCombat = 0;
            foreach (Starfighter f in GetStarfighters())
            {
                if (f.ManufacturingStatus != ManufacturingStatus.Complete || f.Movement != null)
                    continue;

                int weaponStrength = f.LaserCannon + f.IonCannon + f.Torpedoes;
                if (f.MaxSquadronSize > 0)
                {
                    starfighterCombat +=
                        (weaponStrength * f.CurrentSquadronSize) / f.MaxSquadronSize;
                }
                else
                {
                    starfighterCombat += weaponStrength;
                }
            }

            return capitalShipCombat + starfighterCombat;
        }

        /// <summary>
        /// Planetary assault strength: <c>(personnel / divisor + 1) * combat_value</c>.
        /// Personnel comes from the fleet commander's Leadership skill. The commander
        /// must be a General — only ground officers contribute to assault personnel.
        /// Fleets without a General get a baseline strength equal to the combat value.
        /// </summary>
        /// <param name="assaultPersonnelDivisor">
        /// Divisor from <see cref="GameConfig.CombatConfig.AssaultPersonnelDivisor"/>.
        /// </param>
        /// <returns>The fleet's assault strength.</returns>
        public int GetAssaultStrength(int assaultPersonnelDivisor)
        {
            Officer commander = GetOfficers()
                .FirstOrDefault(o => o.CurrentRank == OfficerRank.General);
            int personnel = commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;
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
        /// Enumerates the fleet's direct children (its capital ships) as scene nodes.
        /// </summary>
        /// <returns>An enumerable over the fleet's capital ships.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return CapitalShips.Cast<ISceneNode>();
        }
    }
}
