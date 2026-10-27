using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
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
    public sealed class CombatUnit : BaseGameEntity
    {
        public string SourceUnitInstanceID { get; set; }
        public bool HasRetreated { get; set; }
        public CombatUnitData Data { get; set; } = new CombatUnitData();
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
            Data.Kind switch
            {
                CombatUnitKind.CapitalShip => Data.MaxHullStrength <= 0
                    || Condition.CurrentHullStrength > 0,
                CombatUnitKind.Starfighter => Condition.CurrentSquadronSize > 0
                    && (Data.MaxHullStrength <= 0 || Condition.CurrentHullStrength > 0),
                _ => false,
            };

        /// <summary>
        /// Captures combat capabilities and independently mutable condition without copying strategic queues or movement.
        /// </summary>
        /// <param name="sourceUnit">The strategic ship entering battle.</param>
        /// <returns>The battle member with its original strategic identity.</returns>
        public static CombatUnit Create(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );
            CombatUnitData data = CombatUnitData.Create(sourceUnit);
            CombatUnit combatant = new CombatUnit
            {
                SourceUnitInstanceID = sourceUnit.InstanceID,
                Data = data,
                Condition = new CombatUnitCondition
                {
                    CurrentHullStrength = sourceUnit is CapitalShip ship
                        ? ship.CurrentHullStrength
                        : ((Starfighter)sourceUnit).CurrentHullStrength,
                    CurrentShieldStrength = data.MaxShieldStrength,
                    CurrentSquadronSize = sourceUnit is Starfighter fighter
                        ? fighter.CurrentSquadronSize
                        : 0,
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
