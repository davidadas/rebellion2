using System;
using System.Collections.Generic;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Identifies the ship capabilities represented by a space battle member.
    /// </summary>
    public enum SpaceCombatUnitKind
    {
        None,
        CapitalShip,
        Starfighter,
    }

    /// <summary>
    /// Describes a fighter's carrier deployment phase independently of its rendered model.
    /// </summary>
    public enum FighterFlightState
    {
        Deployed,
        Docked,
        Queued,
        Launching,
        Returning,
        Docking,
    }

    /// <summary>
    /// Stores a three-dimensional value in battle coordinates.
    /// </summary>
    [PersistableObject]
    public sealed class BattleVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// A combat-specific record of one strategic ship participating in an active battle.
    /// </summary>
    [PersistableObject]
    public sealed class SpaceCombatUnit : BaseGameEntity
    {
        public string SourceUnitInstanceID { get; set; }
        public bool HasRetreated { get; set; }
        public SpaceCombatUnitKind Kind { get; set; }
        public string OwnerInstanceID { get; set; }
        public string ModelPath { get; set; }
        public float ModelScale { get; set; } = 1f;
        public int MaxHullStrength { get; set; }
        public int MaxShieldStrength { get; set; }
        public int ShieldRechargeRate { get; set; }
        public int SublightSpeed { get; set; }
        public int Hyperdrive { get; set; }
        public int StarfighterCapacity { get; set; }
        public int MaxSquadronSize { get; set; }

        /// <summary>
        /// Gets or sets geometry measured from the loaded model and rebuilt when a battle is loaded.
        /// </summary>
        [PersistableIgnore]
        public ModelDimensions ModelSize { get; set; }

        [PersistableMember(Name = "Components")]
        private List<ShipComponent> _components = new List<ShipComponent>();

        [PersistableMember(Name = "HardpointGroups")]
        private List<HardpointGroup> _hardpointGroups = new List<HardpointGroup>();

        public CombatUnitCondition Condition { get; set; } = new CombatUnitCondition();
        public BattleVector3 Position { get; set; } = new BattleVector3();
        public BattleVector3 Forward { get; set; } = new BattleVector3 { Z = 1f };
        public CombatMoveOrder MoveOrder { get; set; } = new CombatMoveOrder();
        public CombatFlightState Flight { get; set; } = new CombatFlightState();
        public CombatCarrierState Carrier { get; set; } = new CombatCarrierState();
        public CombatWeaponState Weapons { get; set; } = new CombatWeaponState();
        public List<Officer> Officers { get; set; } = new List<Officer>();

        /// <summary>
        /// Gets whether the captured hull and squadron condition still represents a living combatant.
        /// </summary>
        [PersistableIgnore]
        public bool IsAlive =>
            Kind switch
            {
                SpaceCombatUnitKind.CapitalShip => MaxHullStrength <= 0
                    || Condition.CurrentHullStrength > 0,
                SpaceCombatUnitKind.Starfighter => Condition.CurrentSquadronSize > 0
                    && (MaxHullStrength <= 0 || Condition.CurrentHullStrength > 0),
                _ => false,
            };

        /// <summary>
        /// Captures combat capabilities and independently mutable condition without copying strategic queues or movement.
        /// </summary>
        /// <param name="sourceUnit">The strategic ship entering battle.</param>
        /// <returns>The battle member with its original strategic identity.</returns>
        public static SpaceCombatUnit Create(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );
            CapitalShip ship = sourceUnit as CapitalShip;
            Starfighter fighter = sourceUnit as Starfighter;
            SpaceCombatUnit combatant = new SpaceCombatUnit
            {
                SourceUnitInstanceID = sourceUnit.InstanceID,
                Kind =
                    ship != null
                        ? SpaceCombatUnitKind.CapitalShip
                        : SpaceCombatUnitKind.Starfighter,
                TypeID = sourceUnit.TypeID,
                OwnerInstanceID = sourceUnit.OwnerInstanceID,
                DisplayName = sourceUnit.DisplayName,
                DisplayImagePath = sourceUnit.DisplayImagePath,
                ModelPath = sourceUnit.ModelPath,
                ModelScale = sourceUnit.ModelScale,
                ModelSize = sourceUnit.ModelSize?.CreateCopy(),
                MaxHullStrength = ship != null ? ship.MaxHullStrength : fighter.MaxHullStrength,
                MaxShieldStrength = ship != null ? ship.MaxShieldStrength : fighter.ShieldStrength,
                ShieldRechargeRate = ship != null ? ship.ShieldRechargeRate : 0,
                SublightSpeed = ship != null ? ship.SublightSpeed : fighter.SublightSpeed,
                Hyperdrive = ship != null ? ship.Hyperdrive : fighter.Hyperdrive,
                StarfighterCapacity = ship != null ? ship.StarfighterCapacity : 0,
                MaxSquadronSize = fighter != null ? fighter.MaxSquadronSize : 0,
                _components = (
                    ship != null ? ship.GetComponents() : fighter.GetComponents()
                ).ConvertAll(component => component?.CreateCopy()),
                _hardpointGroups = (
                    ship != null ? ship.GetHardpointGroups() : fighter.GetHardpointGroups()
                ).ConvertAll(group => group?.CreateCopy()),
                Condition = new CombatUnitCondition
                {
                    CurrentHullStrength =
                        ship != null ? ship.CurrentHullStrength : fighter.CurrentHullStrength,
                    CurrentShieldStrength =
                        ship != null ? ship.MaxShieldStrength : fighter.ShieldStrength,
                    CurrentSquadronSize = fighter != null ? fighter.CurrentSquadronSize : 0,
                },
                Flight = new CombatFlightState
                {
                    CarrierSourceUnitInstanceID =
                        sourceUnit is Starfighter
                            ? (sourceUnit.ParentNode as CapitalShip)?.InstanceID
                            : null,
                    Phase =
                        sourceUnit is Starfighter && sourceUnit.ParentNode is CapitalShip
                            ? FighterFlightState.Docked
                            : FighterFlightState.Deployed,
                },
            };
            if (sourceUnit is CapitalShip sourceShip)
                foreach (Officer officer in sourceShip.GetChildren<Officer>(includeDisabled: true))
                    combatant.Officers.Add((Officer)officer.CreateCopy());
            return combatant;
        }

        /// <summary>
        /// Returns the non-negative configured sublight speed.
        /// </summary>
        /// <returns>The ship's sublight speed.</returns>
        public int GetSublightSpeed() => Math.Max(0, SublightSpeed);

        /// <summary>
        /// Returns the configured carrier capacity.
        /// </summary>
        /// <returns>The maximum number of carried squadrons.</returns>
        public int GetStarfighterCapacity() => StarfighterCapacity;

        /// <summary>
        /// Returns independently mutable battle components.
        /// </summary>
        /// <returns>The installed components.</returns>
        public List<ShipComponent> GetComponents() => _components;

        /// <summary>
        /// Returns independently owned battle weapon mounts.
        /// </summary>
        /// <returns>The installed hardpoint groups.</returns>
        public List<HardpointGroup> GetHardpointGroups() => _hardpointGroups;
    }

    /// <summary>
    /// Preserves the battle member's sole mutable hull, shield, squadron, and fractional-damage state.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnitCondition
    {
        public int CurrentHullStrength { get; set; }
        public int? CurrentShieldStrength { get; set; }
        public int CurrentSquadronSize { get; set; }
        public float ShieldRechargeRemainder { get; set; }
        public float HullDamageRemainder { get; set; }
        public float ShieldDamageRemainder { get; set; }
    }

    /// <summary>
    /// Preserves the destination and optional speed ceiling of a pending movement order.
    /// </summary>
    [PersistableObject]
    public sealed class CombatMoveOrder
    {
        public BattleVector3 Destination { get; set; }
        public float? SpeedLimit { get; set; }
    }

    /// <summary>
    /// Preserves a fighter's carrier assignment and current launch or recovery segment.
    /// </summary>
    [PersistableObject]
    public sealed class CombatFlightState
    {
        public string CarrierSourceUnitInstanceID { get; set; }
        public FighterFlightState Phase { get; set; }
        public int FlightBayIndex { get; set; }
        public int FlightWaypointIndex { get; set; }
        public float FlightSegmentDistance { get; set; }
        public float FlightDelay { get; set; }
        public bool RecallRequested { get; set; }
        public BattleVector3 LaunchFormationOffset { get; set; }
    }

    /// <summary>
    /// Preserves a carrier's ordered launch requests and departure spacing.
    /// </summary>
    [PersistableObject]
    public sealed class CombatCarrierState
    {
        public List<string> LaunchQueue { get; set; } = new List<string>();
        public float LaunchCountdown { get; set; }
    }

    /// <summary>
    /// Preserves the explicit attack order and independent state of each installed gun.
    /// </summary>
    [PersistableObject]
    public sealed class CombatWeaponState
    {
        public string AttackTargetInstanceID { get; set; }
        public List<WeaponBankState> Banks { get; set; } = new List<WeaponBankState>();
    }

    /// <summary>
    /// Organizes saved weapon state by its authored hardpoint group.
    /// </summary>
    [PersistableObject]
    public sealed class WeaponBankState
    {
        public string GroupID { get; set; }
        public List<WeaponMountState> Mounts { get; set; } = new List<WeaponMountState>();
    }

    /// <summary>
    /// Preserves one gun's independent target, firing cadence, and articulated orientation across reloads.
    /// </summary>
    [PersistableObject]
    public sealed class WeaponMountState
    {
        public float Cooldown { get; set; }
        public int ShotsFired { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public string TargetInstanceID { get; set; }
    }
}
