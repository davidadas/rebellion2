using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Identifies the behavior assigned to a tactical group.
    /// </summary>
    public enum TacticalCombatOrder
    {
        None,
        HoldFire,
        Recover,
        Withdraw,
        AttackFighters,
        AttackCapitalShips,
        AttackBattleStation,
        LeftHook,
        RightHook,
        Hammer,
        Anvil,
        Stop,
    }

    /// <summary>
    /// Distinguishes general command groups from deployed fighter categories.
    /// </summary>
    public enum TacticalCombatGroupKind
    {
        Command,
        Fighter,
    }

    /// <summary>
    /// Identifies a group's spatial formation.
    /// </summary>
    public enum TacticalCombatFormationKind
    {
        Surround,
        Line,
        Direct,
    }

    /// <summary>
    /// Identifies a fighter group's progress through a battle-station attack.
    /// </summary>
    public enum TacticalCombatAttackRunState
    {
        Approaching,
        Starting,
        Succeeding,
        Failing,
        Succeeded,
        Failed,
        Cancelled,
    }

    /// <summary>
    /// Identifies the command context represented by the tactical selection.
    /// </summary>
    public enum TacticalCombatSelectionKind
    {
        Empty,
        SingleObject,
        EnemyInspection,
        WaypointInspection,
        MultipleObjects,
        ObjectGroup,
        FighterGroup,
        StationTarget,
    }

    /// <summary>
    /// Identifies the current stage of a side's tactical departure.
    /// </summary>
    public enum CombatWithdrawalPhase
    {
        Waiting = 2,
        Departing = 3,
        Scuttling = 4,
    }

    /// <summary>
    /// Persistent state for a battle that can continue independently of the campaign scene graph.
    /// </summary>
    [PersistableObject]
    public sealed class ActiveBattle
    {
        [PersistableMember(Name = nameof(Combatants))]
        private List<CombatUnit> _combatants = new List<CombatUnit>();

        public BattleKind Kind { get; set; }
        public string PlanetInstanceID { get; set; }

        [PersistableMember]
        internal ClockState Clock { get; set; }

        [PersistableMember]
        internal ResumeState Resume { get; set; }

        [PersistableMember]
        [PersistableCollectionItem(Name = "Delivery")]
        internal List<DeferredMessage> DeferredMessages { get; set; } = new();

        [PersistableCollectionItem(Name = "FactionID")]
        public List<string> AttackingFactionIDs { get; set; } = new();

        [PersistableCollectionItem(Name = "FactionID")]
        public List<string> DefendingFactionIDs { get; set; } = new();

        [PersistableIgnore]
        public IReadOnlyList<CombatUnit> Combatants => _combatants;

        // Older XML stored only one faction ID per side.
        [PersistableMember(Name = "AttackerOwnerInstanceID")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private string LegacyAttackerOwnerInstanceID
        {
            get => null;
            set
            {
                if (AttackingFactionIDs.Count == 0 && !string.IsNullOrEmpty(value))
                    AttackingFactionIDs.Add(value);
            }
        }

        [PersistableMember(Name = "DefenderOwnerInstanceID")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private string LegacyDefenderOwnerInstanceID
        {
            get => null;
            set
            {
                if (DefendingFactionIDs.Count == 0 && !string.IsNullOrEmpty(value))
                    DefendingFactionIDs.Add(value);
            }
        }

        // Older XML grouped combatants by owner.
        [PersistableMember(Name = "Participants")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private List<LegacyBattleParticipant> LegacyParticipants
        {
            get => null;
            set
            {
                foreach (LegacyBattleParticipant participant in value)
                    _combatants.AddRange(participant.Combatants);
            }
        }

        [PersistableObject(Name = "BattleParticipant")]
        private sealed class LegacyBattleParticipant
        {
            public List<CombatUnit> Combatants { get; set; } = new List<CombatUnit>();
            public string FactionInstanceID { get; set; }
        }

        /// <summary>
        /// Stores the live battle clock and its position within a dispatch interval.
        /// </summary>
        [PersistableObject]
        internal sealed class ClockState
        {
            internal const uint InitialSystemTime = 3;

            public bool Initialized { get; internal set; }
            public uint CoarseBase { get; internal set; }
            public uint MovementBase { get; internal set; }
            public uint LastSample { get; internal set; }
            public ClockPhase Phase { get; internal set; }
            public uint Progress { get; internal set; }
            public uint SystemTime { get; internal set; } = InitialSystemTime;
        }

        internal enum ClockPhase
        {
            Commands = 0,
            Systems = 1,
            Movement = 5,
        }

        /// <summary>
        /// Holds the tactical state needed to continue with the existing combatants.
        /// </summary>
        [PersistableObject]
        internal sealed class ResumeState
        {
            public uint RandomState { get; set; }
            public CombatSide PlayerSide { get; set; }
            public float BattleDepth { get; set; }
            public int? ScenerySeed { get; set; }
            public float StationCharge { get; set; }
            public bool StationDeparted { get; set; }
            public bool DirectCommandsSuspended { get; set; }
            public SpaceCombatResult.BattleOutcome Winner { get; set; }
            public EndingState Ending { get; set; }
            public BriefingState Briefing { get; set; }
            public SelectionState Selection { get; set; }
            public NotificationQueue Notifications { get; set; }
            public int? LatestAttackGroupID { get; set; }

            [PersistableCollectionItem(Name = "FleetID")]
            public List<string> AttackerFleetIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "FleetID")]
            public List<string> DefenderFleetIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> PersonnelHostIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> InitiallyDockedFighterIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> DepartedUnitIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "Side")]
            public List<SideState> Sides { get; set; } = new();

            [PersistableCollectionItem(Name = "Group")]
            public List<GroupState> Groups { get; set; } = new();

            [PersistableCollectionItem(Name = "Carrier")]
            public List<CarrierState> Carriers { get; set; } = new();

            [PersistableCollectionItem(Name = "Command")]
            public List<CommandState> Commands { get; set; } = new();

            [PersistableCollectionItem(Name = "GroupID")]
            public List<int> PendingManeuverGroupIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "GroupID")]
            public List<int> PendingAttackGroupIDs { get; set; } = new();

            public Vector3 AttackerCenter { get; set; }
            public Vector3 DefenderCenter { get; set; }
        }

        /// <summary>
        /// Records one side's command support, withdrawal, and assigned categories.
        /// </summary>
        [PersistableObject]
        internal sealed class SideState
        {
            public CombatSide Side { get; set; }
            public int FactionIndex { get; set; }
            public Vector3 Forward { get; set; }
            public bool HasWithdrawalDestination { get; set; }
            public float InitialWithdrawalMilliseconds { get; set; }
            public float RetreatStrengthRatio { get; set; }
            public string CapitalCommandCarrierID { get; set; }
            public string FighterCommandCarrierID { get; set; }
            public float AdmiralLeadership { get; set; }
            public float CommanderCombat { get; set; }
            public bool Automated { get; set; }
            public bool CapitalGroupsPartitioned { get; set; }
            public int AutomationCountdown { get; set; }

            [PersistableCollectionItem(Name = "Ship")]
            public List<CapitalDeployment> InitialCapitalDeployment { get; set; } = new();
            public float AttackBudget { get; set; }
            public CombatWithdrawalPhase? WithdrawalPhase { get; set; }
            public float WithdrawalMilliseconds { get; set; }
            public int NextGroupID { get; set; }

            [PersistableCollectionItem(Name = "GroupID")]
            public List<int> ObjectCategoryGroupIDs { get; set; } = new();

            [PersistableCollectionItem(Name = "GroupID")]
            public List<int> FighterCategoryGroupIDs { get; set; } = new();

            public SpaceCombatResult.SideOutcome Outcome { get; set; }

            /// <summary>
            /// Records a ship's weapon bucket in the original deployment order.
            /// </summary>
            [PersistableObject]
            internal sealed class CapitalDeployment
            {
                public string UnitID { get; set; }
                public int Category { get; set; }
            }
        }

        /// <summary>
        /// Records the applied and requested orders of one registered group.
        /// </summary>
        [PersistableObject]
        internal sealed class GroupState
        {
            public int ID { get; set; }
            public CombatSide Side { get; set; }
            public TacticalCombatGroupKind Kind { get; set; }
            public bool Deployed { get; set; }
            public int FighterCategory { get; set; }
            public string PrimaryMemberID { get; set; }
            public string RequestedLeaderID { get; set; }
            public TacticalCombatOrder Order { get; set; }
            public TacticalCombatOrder ActiveOrder { get; set; }
            public bool CloseAttack { get; set; }
            public bool ActiveCloseAttack { get; set; }
            public TacticalCombatFormationKind RequestedFormation { get; set; }
            public float OrbitPhase { get; set; }
            public float MinimumRange { get; set; }
            public float MaximumRange { get; set; }
            public float MovementDistance { get; set; }
            public FormationState Formation { get; set; }
            public AttackRunState AttackRun { get; set; }

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> Members { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> RequestedMembers { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> Targets { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> RequestedTargets { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> FiringTargets { get; set; } = new();

            [PersistableCollectionItem(Name = "Waypoint")]
            public List<Vector3> Waypoints { get; set; } = new();

            [PersistableCollectionItem(Name = "Waypoint")]
            public List<Vector3> RequestedWaypoints { get; set; } = new();
        }

        /// <summary>
        /// Records the marker and each unit's assigned offsets, which depend on join order.
        /// </summary>
        [PersistableObject]
        internal sealed class FormationState
        {
            public TacticalCombatFormationKind Kind { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 PreviousPosition { get; set; }
            public Vector3 Heading { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3 LastMovement { get; set; }
            public float SpacingFloor { get; set; }
            public float SurroundScale { get; set; }
            public float MinimumMovementSpeed { get; set; }
            public float TurnRate { get; set; }
            public int CurrentCount { get; set; }
            public int MaximumCount { get; set; }
            public int StableUpdates { get; set; }

            [PersistableCollectionItem(Name = "Member")]
            public List<FormationMember> Members { get; set; } = new();

            /// <summary>
            /// Records one formation offset and its current collision recovery.
            /// </summary>
            [PersistableObject]
            internal sealed class FormationMember
            {
                public string UnitID { get; set; }
                public Vector3 Offset { get; set; }
                public Vector3 Recovery { get; set; }
                public bool HasRecovery { get; set; }
            }
        }

        /// <summary>
        /// Records a ship's ordered fighter queues and distinct occupied and reserved berth counts.
        /// </summary>
        [PersistableObject]
        internal sealed class CarrierState
        {
            public string UnitID { get; set; }
            public int Occupied { get; set; }
            public int Reserved { get; set; }
            public float NextLaunch { get; set; }

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> LaunchQueue { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> Recovered { get; set; } = new();

            [PersistableCollectionItem(Name = "Recovery")]
            public List<RecoveryState> Recoveries { get; set; } = new();
        }

        /// <summary>
        /// Records which half of a recovery assignment has applied at the save boundary.
        /// </summary>
        [PersistableObject]
        internal sealed class RecoveryState
        {
            public string FighterID { get; set; }
            public RecoveryPhase Phase { get; set; }
            public bool HoldsReservation { get; set; }
        }

        internal enum RecoveryPhase
        {
            Reserved,
            Approaching,
            LandingQueued,
        }

        /// <summary>
        /// Stores a pending battle mutation at its admitted dispatch time.
        /// </summary>
        [PersistableObject]
        internal sealed class CommandState
        {
            public CommandKind Kind { get; set; }
            public int Iteration { get; set; }
            public string SourceID { get; set; }
            public string TargetID { get; set; }
            public int? GroupID { get; set; }
            public bool Append { get; set; }
            public int Value { get; set; }
            public Vector3 Position { get; set; }
            public HardpointWeaponType Weapon { get; set; }
            public float Strength { get; set; }
            public TacticalShipSystem System { get; set; }
            public bool IsRepair { get; set; }
            public float? AttackDelay { get; set; }
            public float? MovementDelay { get; set; }
            public int DischargedArcMask { get; set; }
            public TacticalCombatAttackRunState? AttackRunState { get; set; }
        }

        /// <summary>
        /// Records a fighter group's current attack phase and their pools on entry.
        /// </summary>
        [PersistableObject]
        internal sealed class AttackRunState
        {
            public TacticalCombatAttackRunState Phase { get; set; }
            public float RemainingMilliseconds { get; set; }
            public bool TransitionPending { get; set; }
            public bool ApproachAnnounced { get; set; }
            public bool CandidateDecayed { get; set; }
            public int ProgressNotice { get; set; }

            [PersistableCollectionItem(Name = "Fighter")]
            public List<FighterPools> EntryPools { get; set; } = new();

            /// <summary>
            /// Records the hull and shield pools restored when one fighter leaves an attack run.
            /// </summary>
            [PersistableObject]
            internal sealed class FighterPools
            {
                public string UnitID { get; set; }
                public float Hull { get; set; }
                public float Shields { get; set; }
            }
        }

        /// <summary>
        /// Records the terminal assessment and remaining delay after a winner is determined.
        /// </summary>
        [PersistableObject]
        internal sealed class EndingState
        {
            public bool AllianceWithdrew { get; set; }
            public bool EmpireWithdrew { get; set; }
            public bool AllianceOutlastedEmpire { get; set; }
            public bool EmpireOutlastedAlliance { get; set; }
            public float RemainingMilliseconds { get; set; }
        }

        /// <summary>
        /// Records the next tactical briefing and the last attack eligibility it observed.
        /// </summary>
        [PersistableObject]
        internal sealed class BriefingState
        {
            public BriefingStage Stage { get; set; }
            public float RemainingMilliseconds { get; set; }
            public bool PreviousAdmission { get; set; }
        }

        /// <summary>
        /// Records the local command context and its requested display chains.
        /// </summary>
        [PersistableObject]
        internal sealed class SelectionState
        {
            public TacticalCombatSelectionKind Kind { get; set; }
            public int? GroupID { get; set; }
            public int Category { get; set; }
            public Vector3? PrimaryWaypoint { get; set; }
            public string PrimaryID { get; set; }
            public string InspectedID { get; set; }
            public uint NotificationID { get; set; }

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> Members { get; set; } = new();

            [PersistableCollectionItem(Name = "UnitID")]
            public List<string> Targets { get; set; } = new();

            [PersistableCollectionItem(Name = "Waypoint")]
            public List<Vector3> Waypoints { get; set; } = new();

            [PersistableCollectionItem(Name = "GroupID")]
            public List<int> Groups { get; set; } = new();
        }

        /// <summary>
        /// Records tactical notification order and active playback positions.
        /// </summary>
        [PersistableObject]
        internal sealed class NotificationQueue
        {
            public bool Enabled { get; set; }
            public uint NextID { get; set; }
            public uint ActiveIDSeed { get; set; }
            public uint PendingIDSeed { get; set; }

            [PersistableCollectionItem(Name = "Notification")]
            public List<NotificationState> Active { get; set; } = new();

            [PersistableCollectionItem(Name = "Notification")]
            public List<NotificationState> Pending { get; set; } = new();
        }

        /// <summary>
        /// Records an addressed audio bank and the variant already chosen for an active cue.
        /// </summary>
        [PersistableObject]
        internal sealed class NotificationState
        {
            public uint QueueID { get; set; }
            public int MessageID { get; set; }
            public uint AgeMilliseconds { get; set; }
            public bool Loop { get; set; }
            public int VariantIndex { get; set; }
            public int? PlaybackSample { get; set; }
        }

        /// <summary>
        /// Keeps a campaign message waiting for the battle's aftermath to release delivery.
        /// </summary>
        [PersistableObject]
        internal sealed class DeferredMessage
        {
            public string RecipientID { get; set; }

            [PersistableMember(UseTypeName = true)]
            public Message Content { get; set; }

            public AdvisorNotificationType NotificationType { get; set; }
            public AdvisorSubjectNotification AdvisorSubjectNotification { get; set; }
            public string AdvisorSubjectTypeID { get; set; }
            public AdvisorNotification AdvisorNotification { get; set; }
            public string SourceEventInstanceID { get; set; }
        }

        /// <summary>
        /// Adds an independently mutable copy of a strategic unit.
        /// </summary>
        /// <param name="sourceUnit">The campaign unit entering battle.</param>
        /// <returns>The detached combat unit, or null for a destroyed squadron.</returns>
        public CombatUnit AddCombatant(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (string.IsNullOrWhiteSpace(sourceUnit.OwnerInstanceID))
                throw new ArgumentException(
                    "A combatant must have an owner before entering battle.",
                    nameof(sourceUnit)
                );
            if (Kind != BattleKind.Space)
                throw new InvalidOperationException(
                    "Capital ships and starfighters can only enter space battles."
                );
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );
            if (sourceUnit is Starfighter { CurrentSquadronSize: <= 0 })
                return null;

            CombatUnit combatant = CombatUnit.Create(sourceUnit);
            _combatants.Add(combatant);
            return combatant;
        }

        /// <summary>
        /// Finds a detached combatant by the stable campaign unit that produced it.
        /// </summary>
        /// <param name="sourceUnitInstanceID">The campaign unit identity.</param>
        /// <returns>The matching combatant, or null when it is not in this battle.</returns>
        public CombatUnit GetCombatant(string sourceUnitInstanceID)
        {
            if (string.IsNullOrEmpty(sourceUnitInstanceID))
                return null;
            foreach (CombatUnit combatant in _combatants)
                if (
                    string.Equals(
                        combatant.SourceUnitInstanceID,
                        sourceUnitInstanceID,
                        StringComparison.Ordinal
                    )
                )
                    return combatant;
            return null;
        }

        internal enum CommandKind
        {
            DeleteGroup,
            CreateGroup,
            AddGroupMember,
            RemoveGroupMember,
            Order,
            AttackMode,
            Formation,
            Waypoint,
            RemoveWaypoint,
            RemoveTarget,
            Superlaser,
            AttackRun,
            Target,
            Fire,
            Hit,
            System,
            Launch,
            SetRecovery,
            Recover,
            TractorLock,
            TractorUnlock,
            Hyperspace,
            Withdraw,
            Destroy,
            Scuttle,
        }

        internal enum BriefingStage
        {
            Opening,
            Station,
            Protection,
            FighterBalance,
            ObservingAdmission,
            WaitingForAdmission,
        }
    }
}
