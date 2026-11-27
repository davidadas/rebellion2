using System;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;

namespace Rebellion.Tactical.Synchronization
{
    /// <summary>
    /// Captures one complete authoritative view update and the presentation edges since its predecessor.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatSnapshot
    {
        public long Sequence;
        public double TimestampMilliseconds;
        public int SystemTime;
        public bool Paused;
        public bool Complete;
        public SpaceCombatResult.BattleOutcome Winner;
        public SpaceCombatResult.SideOutcome AttackerOutcome;
        public SpaceCombatResult.SideOutcome DefenderOutcome;
        public float StationCharge;
        public bool StationDeparted;
        public int MovieID;
        public int MovieGroupID;
        public CombatSide MovieSide;
        public string MovieUnitID;
        public int MovieCategory;
        public TacticalCombatAttackRunState MovieState;
        public TacticalCombatUnitSnapshot[] Units = Array.Empty<TacticalCombatUnitSnapshot>();
        public TacticalCombatGroupSnapshot[] Groups = Array.Empty<TacticalCombatGroupSnapshot>();
        public TacticalCombatSideSnapshot[] Sides = Array.Empty<TacticalCombatSideSnapshot>();
        public TacticalCombatSelectionSnapshot[] Selections =
            Array.Empty<TacticalCombatSelectionSnapshot>();
        public TacticalCombatEvent[] Effects = Array.Empty<TacticalCombatEvent>();
    }

    /// <summary>
    /// Captures a participant's visible tactical state independently of campaign ownership.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatUnitSnapshot
    {
        public string UnitID;
        public TacticalCombatSnapshotVector Position;
        public TacticalCombatSnapshotVector Heading;
        public TacticalCombatSnapshotVector Velocity;
        public TacticalCombatSnapshotVector DesiredHeading;
        public TacticalCombatSnapshotVector DesiredMovement;
        public CombatUnitStatus Status;
        public string CarrierID;
        public float Hull;
        public float Shields;
        public float ShieldLimit;
        public float AttackDelay;
        public float MovementDelay;
        public float WeaponRechargeSample;
        public float ShieldRechargeSample;
        public uint[] ComponentDamage = new uint[5];
        public int[] HyperdriveDamage;
        public float[] Charge = new float[4];
        public bool[] Ready = new bool[4];
        public float HyperspaceMilliseconds;
        public float TractorRestraint;
        public bool HasTractorSources;
        public float CommandBudget;
        public float TurnRate;
        public bool HasCollision;
    }

    /// <summary>
    /// Captures requested and applied group state used by local command and inspection views.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatGroupSnapshot
    {
        public int GroupID;
        public CombatSide Side;
        public TacticalCombatGroupKind Kind;
        public bool Applied;
        public string[] Members = Array.Empty<string>();
        public string[] RequestedMembers = Array.Empty<string>();
        public string PrimaryMemberID;
        public string RequestedLeaderID;
        public string[] Targets = Array.Empty<string>();
        public string[] RequestedTargets = Array.Empty<string>();
        public string[] ComputedTargets = Array.Empty<string>();
        public TacticalCombatSnapshotVector[] Waypoints =
            Array.Empty<TacticalCombatSnapshotVector>();
        public TacticalCombatSnapshotVector[] RequestedWaypoints =
            Array.Empty<TacticalCombatSnapshotVector>();
        public TacticalCombatOrder Order;
        public TacticalCombatOrder ActiveOrder;
        public bool CloseAttack;
        public bool ActiveCloseAttack;
        public TacticalCombatFormationKind Formation;
        public TacticalCombatFormationKind RequestedFormation;
        public TacticalCombatSnapshotVector Position;
        public TacticalCombatSnapshotVector Heading;
        public TacticalCombatSnapshotVector Velocity;
        public float MovementSpeed;
        public float TurnRate;
        public int StableUpdates;
        public int FighterCategory;
        public int FighterSlot = -1;
        public int ObjectCategory = -1;
        public bool HasAttackRun;
        public TacticalCombatAttackRunState AttackRunState;
        public float AttackRunMilliseconds;
    }

    /// <summary>
    /// Captures one faction's authority and withdrawal facts for its local HUD.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatSideSnapshot
    {
        public CombatSide Side;
        public bool Automated;
        public bool CommandsSuspended;
        public bool Withdrawing;
        public float WithdrawalMilliseconds;
        public bool HasWithdrawalPhase;
        public CombatWithdrawalPhase WithdrawalPhase;
        public float AttackBudget;
    }

    /// <summary>
    /// Captures one faction's selection without coupling it to the other faction's camera.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatSelectionSnapshot
    {
        public CombatSide Side;
        public TacticalCombatSelectionKind Kind;
        public bool HasGroup;
        public int GroupID;
        public int Category;
        public bool HasWaypoint;
        public TacticalCombatSnapshotVector PrimaryWaypoint;
        public string PrimaryID;
        public string InspectedID;
        public string[] Members = Array.Empty<string>();
        public string[] Targets = Array.Empty<string>();
        public TacticalCombatSnapshotVector[] Waypoints =
            Array.Empty<TacticalCombatSnapshotVector>();
        public int[] Groups = Array.Empty<int>();
    }

    /// <summary>
    /// Stores a serializable encounter-local vector without engine object references.
    /// </summary>
    [Serializable]
    public struct TacticalCombatSnapshotVector
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>
        /// Captures an encounter-local vector.
        /// </summary>
        /// <param name="value">The vector being captured.</param>
        public TacticalCombatSnapshotVector(Vector3 value)
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
        }

        /// <summary>
        /// Restores a vector for a local presentation consumer.
        /// </summary>
        /// <returns>The encounter-local vector.</returns>
        public Vector3 ToVector() => new(X, Y, Z);
    }
}
