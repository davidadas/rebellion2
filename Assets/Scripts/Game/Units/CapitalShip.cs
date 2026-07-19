using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Movement;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    public enum PrimaryWeaponType
    {
        Turbolaser,
        IonCannon,
        LaserCannon,
    }

    public enum CapitalShipRole
    {
        PrimaryLine,
        SecondaryLine,
        Escort,
        Interdictor,
        Transport,
        Carrier,
        Flagship,
    }

    /// <summary>
    /// Represents a capital ship in the game.
    /// </summary>
    public class CapitalShip : ContainerNode, IManufacturable, IMovable
    {
        public string BattleResultImagePath { get; set; }
        public string BattleResultInTransitImagePath { get; set; }
        public string BattleResultDamagedImagePath { get; set; }

        // Manufacture Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // Hull, Shield, and Repair Info.
        public int MaxHullStrength;
        public int CurrentHullStrength;
        public int DamageControl;
        public int MaxShieldStrength;
        public int ShieldRechargeRate;

        // Movement Info.
        public int Hyperdrive;
        public int SublightSpeed;
        public int Maneuverability;

        // Capacity Info.
        public int StarfighterCapacity;
        public int RegimentCapacity;
        public List<CapitalShipRole> Roles = new List<CapitalShipRole>();

        // Unit Info.
        public List<Officer> Officers = new List<Officer>();
        public List<Regiment> Regiments = new List<Regiment>();
        public List<SpecialForces> SpecialForces = new List<SpecialForces>();
        public List<Starfighter> Starfighters = new List<Starfighter>();

        // Weapon Info.
        public Dictionary<PrimaryWeaponType, int[]> PrimaryWeapons = new Dictionary<
            PrimaryWeaponType,
            int[]
        >()
        {
            { PrimaryWeaponType.Turbolaser, new int[5] },
            { PrimaryWeaponType.IonCannon, new int[5] },
            { PrimaryWeaponType.LaserCannon, new int[5] },
        };
        public int WeaponRecharge;
        public int Bombardment;

        // Manufacturing Info.
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        public int RefinedMaterialProgress { get; set; }
        public int ProductionCapacity { get; set; }
        public int ProductionCapacityUsed { get; set; }
        public int KdyPool { get; set; }
        public int LnrPool { get; set; }

        // Movement Info.
        public MovementState Movement { get; set; }

        // Misc Info.
        public int TractorBeamPower;
        public int TractorBeamnRange;
        public bool HasGravityWell;
        public int DetectionRating;

        // Owner Info.
        public string InitialParentInstanceID { get; set; }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public CapitalShip() { }

        /// <summary>
        /// Returns the maximum number of starfighters this ship can carry.
        /// </summary>
        /// <returns>The starfighter capacity of this ship.</returns>
        public int GetStarfighterCapacity()
        {
            return StarfighterCapacity;
        }

        /// <summary>
        /// Returns the number of starfighters currently assigned to this ship.
        /// </summary>
        /// <returns>The count of starfighters currently on board.</returns>
        public int GetCurrentStarfighterCount()
        {
            return Starfighters.Count;
        }

        /// <summary>
        /// Returns the maximum number of regiments this ship can carry.
        /// </summary>
        /// <returns>The regiment capacity of this ship.</returns>
        public int GetRegimentCapacity()
        {
            return RegimentCapacity;
        }

        /// <summary>
        /// Returns the ship roles used by fleet and production planning.
        /// </summary>
        /// <returns>The roles assigned to this ship type.</returns>
        public IReadOnlyList<CapitalShipRole> GetRoles()
        {
            return Roles;
        }

        /// <summary>
        /// Returns whether this ship has the requested role.
        /// </summary>
        /// <param name="role">The role to inspect.</param>
        /// <returns>True if this ship has the requested role.</returns>
        public bool HasRole(CapitalShipRole role)
        {
            return GetRoles().Contains(role);
        }

        /// <summary>
        /// Returns whether this ship has any of the requested roles.
        /// </summary>
        /// <param name="roles">The roles to inspect.</param>
        /// <returns>True if this ship has at least one requested role.</returns>
        public bool HasAnyRole(params CapitalShipRole[] roles)
        {
            IReadOnlyList<CapitalShipRole> shipRoles = GetRoles();
            return roles.Any(shipRoles.Contains);
        }

        /// <summary>
        /// Returns the ship's total primary weapon strength.
        /// </summary>
        /// <returns>The summed primary weapon strength.</returns>
        public int GetPrimaryWeaponStrength()
        {
            return PrimaryWeapons.Values.Sum(GetWeaponStrength);
        }

        /// <summary>
        /// Returns primary weapon strength from weapon values.
        /// </summary>
        /// <param name="weaponValues">The weapon values to inspect.</param>
        /// <returns>The summed weapon strength.</returns>
        private static int GetWeaponStrength(int[] weaponValues)
        {
            if (weaponValues == null)
                return 0;

            int strength = 0;
            int weaponStrengthValueCount = Math.Min(4, weaponValues.Length);
            for (int i = 0; i < weaponStrengthValueCount; i++)
                strength += weaponValues[i];

            return strength;
        }

        /// <summary>
        /// Returns active combat value adjusted for current hull condition.
        /// </summary>
        /// <returns>The ship's active combat value, or zero when unavailable.</returns>
        public int GetCombatValue()
        {
            if (ManufacturingStatus != ManufacturingStatus.Complete || Movement != null)
                return 0;

            int attackStrength = GetPrimaryWeaponStrength();
            if (MaxHullStrength <= 0)
                return attackStrength;

            return attackStrength * CurrentHullStrength / MaxHullStrength;
        }

        /// <summary>
        /// Returns the number of regiments currently assigned to this ship.
        /// </summary>
        /// <returns>The count of regiments currently on board.</returns>
        public int GetCurrentRegimentCount()
        {
            return Regiments.Count;
        }

        /// <summary>
        /// Returns the number of additional starfighters this ship can carry.
        /// </summary>
        /// <returns>Remaining starfighter berths (capacity minus current count). Zero if full.</returns>
        public int GetExcessStarfighterCapacity()
        {
            return StarfighterCapacity - Starfighters.Count;
        }

        /// <summary>
        /// Returns the number of additional regiments this ship can carry.
        /// </summary>
        /// <returns>Remaining regiment berths (capacity minus current count). Zero if full.</returns>
        public int GetExcessRegimentCapacity()
        {
            return RegimentCapacity - Regiments.Count;
        }

        /// <summary>
        /// Adds a starfighter to the capital ship.
        /// </summary>
        /// <param name="starfighter">The starfighter to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when adding the starfighter would exceed the capacity.</exception>
        public void AddStarfighter(Starfighter starfighter)
        {
            if (Starfighters.Count >= StarfighterCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding starfighters to \"{this.GetDisplayName()}\" would exceed its capacity."
                );
            }
            Starfighters.Add(starfighter);
        }

        /// <summary>
        /// Adds a regiment to the capital ship.
        /// </summary>
        /// <param name="regiment">The regiment to add.</param>
        /// <exception cref="InvalidOperationException">Thrown when adding the regiment would exceed the capacity limit.</exception>
        public void AddRegiment(Regiment regiment)
        {
            if (Regiments.Count >= RegimentCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding regiments to \"{this.GetDisplayName()}\" would exceed its capacity."
                );
            }
            Regiments.Add(regiment);
        }

        /// <summary>
        /// Adds an officer to the capital ship.
        /// </summary>
        /// <param name="officer">The officer to add.</param>
        /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
        public void AddOfficer(Officer officer)
        {
            if (!officer.IsCaptured && this.OwnerInstanceID != officer.OwnerInstanceID)
            {
                throw new SceneAccessException(officer, this);
            }

            Officers.Add(officer);
        }

        /// <summary>
        /// Adds special forces to this ship.
        /// </summary>
        /// <param name="specialForces">The special forces unit to add.</param>
        public void AddSpecialForces(SpecialForces specialForces)
        {
            if (this.OwnerInstanceID != specialForces.OwnerInstanceID)
            {
                throw new SceneAccessException(specialForces, this);
            }

            SpecialForces.Add(specialForces);
        }

        /// <summary>
        /// Returns true if this ship can accept the child: Starfighters and Regiments require spare
        /// capacity; Officers must share the ship's owner or be captured.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if AddChild would succeed; otherwise false.</returns>
        public override bool CanAcceptChild(ISceneNode child)
        {
            if (child is Starfighter)
                return GetExcessStarfighterCapacity() > 0;
            if (child is Regiment)
                return GetExcessRegimentCapacity() > 0;
            if (child is Officer officer)
                return officer.IsCaptured || officer.GetOwnerInstanceID() == GetOwnerInstanceID();
            if (child is SpecialForces specialForces)
                return specialForces.GetOwnerInstanceID() == GetOwnerInstanceID();
            return false;
        }

        /// <summary>
        /// Adds a child to the capital ship.
        /// </summary>
        /// <param name="child">The child to add.</param>
        /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
        public override void AddChild(ISceneNode child)
        {
            if (child is Starfighter starfighter)
            {
                AddStarfighter(starfighter);
            }
            else if (child is Regiment regiment)
            {
                AddRegiment(regiment);
            }
            else if (child is Officer officer)
            {
                AddOfficer(officer);
            }
            else if (child is SpecialForces specialForces)
            {
                AddSpecialForces(specialForces);
            }
        }

        /// <summary>
        /// Removes a child from the capital ship.
        /// </summary>
        /// <param name="child">The child to remove.</param>
        public override void RemoveChild(ISceneNode child)
        {
            if (child is Starfighter starfighter)
            {
                Starfighters.Remove(starfighter);
            }
            else if (child is Regiment regiment)
            {
                Regiments.Remove(regiment);
            }
            else if (child is Officer officer)
            {
                Officers.Remove(officer);
            }
            else if (child is SpecialForces specialForces)
            {
                SpecialForces.Remove(specialForces);
            }
        }

        /// <summary>
        /// Returns true if this ship has hull damage that can be repaired.
        /// </summary>
        /// <returns>True if current hull strength is below maximum hull strength.</returns>
        public bool IsDamaged() => CurrentHullStrength < MaxHullStrength;

        /// <summary>
        /// Repairs hull damage by the specified amount, capped at MaxHullStrength.
        /// </summary>
        /// <param name="amount">Hull points to restore.</param>
        public void RepairHull(int amount)
        {
            CurrentHullStrength = Math.Min(MaxHullStrength, CurrentHullStrength + amount);
        }

        /// <summary>
        /// Returns the manufacturing type of this ship.
        /// </summary>
        /// <returns>The ship manufacturing type.</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Ship;
        }

        /// <summary>
        /// Returns whether this ship can start movement.
        /// </summary>
        /// <returns>True if this ship is not currently moving.</returns>
        public bool IsMovable()
        {
            return Movement == null;
        }

        /// <summary>
        /// Returns the ship's carried units and officers.
        /// </summary>
        /// <returns>The children carried by this ship.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            return Officers
                .Cast<ISceneNode>()
                .Concat(Starfighters.Cast<ISceneNode>())
                .Concat(Regiments.Cast<ISceneNode>())
                .Concat(SpecialForces.Cast<ISceneNode>());
        }
    }
}
