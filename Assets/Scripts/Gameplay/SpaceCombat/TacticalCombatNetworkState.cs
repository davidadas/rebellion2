using System;
using System.Numerics;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies a faction-owned semantic input at the shared combat boundary.
    /// </summary>
    public enum TacticalCombatNetworkCommandKind
    {
        Object,
        Waypoint,
        Clear,
        DisplayMode,
        ObjectCategory,
        FighterCategory,
        NearestTarget,
        Order,
        AttackMode,
        Formation,
        ToggleAutomation,
        Withdrawal,
        StationTarget,
        Pause,
        ImmediateResult,
    }

    /// <summary>
    /// Carries semantic input without granting the sender ownership of its authenticated side.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkCommand
    {
        public TacticalCombatNetworkCommandKind Kind;
        public string UnitID;
        public TacticalCombatNetworkVector Waypoint;
        public bool Extend;
        public bool Next;
        public int Category;
        public TacticalCombatOrder Order;
        public bool CloseAttack;
        public TacticalCombatFormationKind Formation;
        public bool Paused;
    }

    /// <summary>
    /// Stores a serializable encounter-local vector without engine object references.
    /// </summary>
    [Serializable]
    public struct TacticalCombatNetworkVector
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>
        /// Captures an encounter-local vector.
        /// </summary>
        /// <param name="value">The vector being captured.</param>
        public TacticalCombatNetworkVector(Vector3 value)
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

    /// <summary>
    /// Captures a participant's visible tactical state independently of campaign ownership.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkUnitState
    {
        public string UnitID;
        public TacticalCombatNetworkVector Position;
        public TacticalCombatNetworkVector Heading;
        public TacticalCombatNetworkVector Velocity;
        public TacticalCombatNetworkVector DesiredHeading;
        public TacticalCombatNetworkVector DesiredMovement;
        public TacticalCombatUnitStatus Status;
        public string CarrierID;
        public string TargetID;
        public float Hull;
        public float Shields;
        public float ShieldLimit;
        public float AttackDelay;
        public float MovementDelay;
        public float WeaponRechargeSample;
        public float ShieldRechargeSample;
        public uint[] ComponentDamage = new uint[5];
        public float[] Charge = new float[4];
        public bool[] Ready = new bool[4];
        public float HyperspaceMilliseconds;
        public float TractorRestraint;
        public bool HasTractorSources;
        public float CommandBudget;
        public float TurnRate;
        public bool HasCollision;
        public bool HasGroup;
        public int GroupID;
        public bool HasRequestedGroup;
        public int RequestedGroupID;
    }

    /// <summary>
    /// Captures requested and applied group state used by local command and inspection views.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkGroupState
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
        public TacticalCombatNetworkVector[] Waypoints = Array.Empty<TacticalCombatNetworkVector>();
        public TacticalCombatNetworkVector[] RequestedWaypoints =
            Array.Empty<TacticalCombatNetworkVector>();
        public TacticalCombatOrder Order;
        public TacticalCombatOrder ActiveOrder;
        public bool CloseAttack;
        public bool ActiveCloseAttack;
        public TacticalCombatFormationKind Formation;
        public TacticalCombatFormationKind RequestedFormation;
        public TacticalCombatNetworkVector Position;
        public TacticalCombatNetworkVector Heading;
        public TacticalCombatNetworkVector Velocity;
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
    public sealed class TacticalCombatNetworkSideState
    {
        public CombatSide Side;
        public bool Automated;
        public bool CommandsSuspended;
        public bool Withdrawing;
        public float WithdrawalMilliseconds;
        public bool HasWithdrawalPhase;
        public TacticalCombatWithdrawalPhase WithdrawalPhase;
        public float AttackBudget;
    }

    /// <summary>
    /// Captures one faction's selection without coupling it to the other faction's camera.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkSelectionState
    {
        public CombatSide Side;
        public TacticalCombatSelectionKind Kind;
        public bool HasGroup;
        public int GroupID;
        public int Category;
        public bool HasWaypoint;
        public TacticalCombatNetworkVector PrimaryWaypoint;
        public string PrimaryID;
        public string InspectedID;
        public string[] Members = Array.Empty<string>();
        public string[] Highlights = Array.Empty<string>();
        public string[] Targets = Array.Empty<string>();
        public TacticalCombatNetworkVector[] Waypoints = Array.Empty<TacticalCombatNetworkVector>();
        public int[] Groups = Array.Empty<int>();
    }

    /// <summary>
    /// Identifies a one-shot presentation edge emitted by the authoritative simulation.
    /// </summary>
    public enum TacticalCombatNetworkEffectKind
    {
        Shot,
        Impact,
        Destruction,
        Destroyed,
        Withdrawn,
        Recovered,
        Superlaser,
        TractorSources,
        GravityWell,
        Movie,
        Notification,
    }

    /// <summary>
    /// Captures an ordered presentation edge without asking the receiving peer to simulate it.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkEffect
    {
        public TacticalCombatNetworkEffectKind Kind;
        public string SourceID;
        public string TargetID;
        public TacticalWeaponType Weapon;
        public TacticalCombatImpactKind Impact;
        public float Strength;
        public bool Enabled;
        public int MessageID;
        public int MovieID;
        public bool HasRecipient;
        public CombatSide Recipient;
    }

    /// <summary>
    /// Captures one complete authoritative view update and the presentation edges since its predecessor.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkFrame
    {
        public long Sequence;
        public int SystemTime;
        public bool Paused;
        public bool Complete;
        public CombatSide Winner;
        public SpaceCombatSideOutcome AttackerOutcome;
        public SpaceCombatSideOutcome DefenderOutcome;
        public bool[] TerminalFlags = new bool[7];
        public float StationCharge;
        public bool StationDeparted;
        public int MovieID;
        public int MovieGroupID;
        public CombatSide MovieSide;
        public string MovieUnitID;
        public int MovieCategory;
        public TacticalCombatAttackRunState MovieState;
        public TacticalCombatNetworkUnitState[] Units =
            Array.Empty<TacticalCombatNetworkUnitState>();
        public TacticalCombatNetworkGroupState[] Groups =
            Array.Empty<TacticalCombatNetworkGroupState>();
        public TacticalCombatNetworkSideState[] Sides =
            Array.Empty<TacticalCombatNetworkSideState>();
        public TacticalCombatNetworkSelectionState[] Selections =
            Array.Empty<TacticalCombatNetworkSelectionState>();
        public TacticalCombatNetworkEffect[] Effects = Array.Empty<TacticalCombatNetworkEffect>();
    }
}
