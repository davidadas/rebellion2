using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Stores the mutable simulation state required to resume one tactical battle.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalBattleSnapshot
    {
        /// <summary>Gets or sets the active tactical battle phase.</summary>
        public TacticalBattlePhase Phase { get; set; }

        /// <summary>Gets or sets the elapsed fleet-arrival time.</summary>
        public float ArrivalElapsedTime { get; set; }

        /// <summary>Gets or sets the number of systems currently holding the battle paused.</summary>
        public int PauseCount { get; set; }

        /// <summary>Gets or sets whether attacker commands are computer controlled.</summary>
        public bool AttackerComputerControlled { get; set; }

        /// <summary>Gets or sets whether defender commands are computer controlled.</summary>
        public bool DefenderComputerControlled { get; set; }

        /// <summary>Gets or sets whether initial player command assignment already occurred.</summary>
        public bool PlayerControlConfigured { get; set; }

        /// <summary>Gets or sets waypoint-set visibility from the inner to outer shell.</summary>
        public List<bool> NavigationVisibility { get; set; } = new List<bool>();

        /// <summary>Gets or sets the saved tactical unit states.</summary>
        public List<TacticalUnitStateSnapshot> Units { get; set; } =
            new List<TacticalUnitStateSnapshot>();

        /// <summary>Gets or sets the tactical command groups in their stable HUD order.</summary>
        public List<TacticalShipGroupSnapshot> Groups { get; set; } =
            new List<TacticalShipGroupSnapshot>();

        /// <summary>Gets or sets the carrier fighter-launch state.</summary>
        public TacticalFighterDeploymentSnapshot FighterDeployment { get; set; }

        /// <summary>Gets or sets Death Star superlaser charge and pending shot state.</summary>
        public TacticalSuperlaserSnapshot Superlaser { get; set; }

        /// <summary>Gets or sets active and completed fighter attacks against a Death Star.</summary>
        public TacticalDeathStarAttackSnapshot DeathStarAttack { get; set; }

        /// <summary>Gets or sets elapsed simulation time used by delayed tactical behavior.</summary>
        public float TacticalTime { get; set; }

        /// <summary>Gets or sets each unit's retained combat target.</summary>
        public List<TacticalTargetSnapshot> Targets { get; set; } =
            new List<TacticalTargetSnapshot>();

        /// <summary>Gets or sets resolved group maneuver anchors.</summary>
        public List<TacticalManeuverOrderSnapshot> ManeuverOrders { get; set; } =
            new List<TacticalManeuverOrderSnapshot>();

        /// <summary>Gets or sets persistent capital-ship collision detours.</summary>
        public List<TacticalCollisionAvoidanceSnapshot> CollisionAvoidance { get; set; } =
            new List<TacticalCollisionAvoidanceSnapshot>();

        /// <summary>Gets or sets marker-stability counters used to select collision detours.</summary>
        public List<TacticalMarkerStabilitySnapshot> MarkerStability { get; set; } =
            new List<TacticalMarkerStabilitySnapshot>();

        /// <summary>Gets or sets active unit withdrawal curves.</summary>
        public List<TacticalWithdrawalSnapshot> Withdrawals { get; set; } =
            new List<TacticalWithdrawalSnapshot>();
    }

    /// <summary>
    /// Stores one tactical-space vector without depending on a runtime vector serializer.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalVectorSnapshot
    {
        /// <summary>Gets or sets the horizontal component.</summary>
        public float X { get; set; }

        /// <summary>Gets or sets the vertical component.</summary>
        public float Y { get; set; }

        /// <summary>Gets or sets the depth component.</summary>
        public float Z { get; set; }
    }

    /// <summary>
    /// Stores the mutable combat state of one tactical unit.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalUnitStateSnapshot
    {
        /// <summary>Gets or sets the represented strategic unit identifier.</summary>
        public string UnitInstanceID { get; set; }

        /// <summary>Gets or sets the current hull strength.</summary>
        public int Hull { get; set; }

        /// <summary>Gets or sets the current shield strength.</summary>
        public int Shields { get; set; }

        /// <summary>Gets or sets the current tactical position.</summary>
        public TacticalVectorSnapshot Position { get; set; }

        /// <summary>Gets or sets the current facing direction.</summary>
        public TacticalVectorSnapshot Forward { get; set; }

        /// <summary>Gets or sets whether the unit has entered tactical space.</summary>
        public bool IsDeployed { get; set; }

        /// <summary>Gets or sets whether the unit is leaving tactical space.</summary>
        public bool IsWithdrawing { get; set; }

        /// <summary>Gets or sets whether the unit completed its withdrawal.</summary>
        public bool HasWithdrawn { get; set; }

        /// <summary>Gets or sets the remaining component disruption time.</summary>
        public float ComponentDisruptionTime { get; set; }

        /// <summary>Gets or sets the remaining movement disruption time.</summary>
        public float MovementDisruptionTime { get; set; }

        /// <summary>Gets or sets fractional shield recharge retained between updates.</summary>
        public float ShieldRechargeRemainder { get; set; }

        /// <summary>Gets or sets elapsed damage-control time retained between repair attempts.</summary>
        public float DamageControlTime { get; set; }

        /// <summary>Gets or sets each firing arc's accumulated recharge.</summary>
        public List<float> ArcCharge { get; set; } = new List<float>();

        /// <summary>Gets or sets each firing arc's required recharge.</summary>
        public List<float> ArcChargeRequired { get; set; } = new List<float>();

        /// <summary>Gets or sets persistent damage for each tactical subsystem.</summary>
        public List<int> SystemDamage { get; set; } = new List<int>();

        /// <summary>Gets or sets the firing arcs waiting to recharge, in queue order.</summary>
        public List<TacticalWeaponArc> RechargingArcs { get; set; } = new List<TacticalWeaponArc>();
    }

    /// <summary>
    /// Stores one tactical command group's ordered membership and active command state.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalShipGroupSnapshot
    {
        /// <summary>Gets or sets the group's index in the battle's stable group order.</summary>
        public int GroupIndex { get; set; }

        /// <summary>Gets or sets the identifiers of units assigned to the group.</summary>
        public List<string> UnitInstanceIDs { get; set; } = new List<string>();

        /// <summary>Gets or sets the active tactical behavior.</summary>
        public TacticalBehavior Behavior { get; set; }

        /// <summary>Gets or sets the active capital-ship formation.</summary>
        public TacticalFormation Formation { get; set; }

        /// <summary>Gets or sets the formation marker position.</summary>
        public TacticalVectorSnapshot MarkerPosition { get; set; }

        /// <summary>Gets or sets the exact command revision used by maneuver state.</summary>
        public int CommandRevision { get; set; }

        /// <summary>Gets or sets ordered opposing target identifiers.</summary>
        public List<string> TargetInstanceIDs { get; set; } = new List<string>();

        /// <summary>Gets or sets the friendly escort target identifier.</summary>
        public string EscortTargetInstanceID { get; set; }

        /// <summary>Gets or sets the ordered tactical navigation route.</summary>
        public List<TacticalVectorSnapshot> NavigationPoints { get; set; } =
            new List<TacticalVectorSnapshot>();
    }

    /// <summary>
    /// Stores the timing and ordered carrier queues for fighter deployment.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalFighterDeploymentSnapshot
    {
        /// <summary>Gets or sets elapsed fighter-deployment time.</summary>
        public float ElapsedTime { get; set; }

        /// <summary>Gets or sets the active carrier launch queues.</summary>
        public List<TacticalFighterLaunchQueueSnapshot> LaunchQueues { get; set; } =
            new List<TacticalFighterLaunchQueueSnapshot>();
    }

    /// <summary>
    /// Stores one carrier's remaining fighter launch order and next launch time.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalFighterLaunchQueueSnapshot
    {
        /// <summary>Gets or sets the carrier strategic unit identifier.</summary>
        public string CarrierInstanceID { get; set; }

        /// <summary>Gets or sets remaining fighter identifiers in launch order.</summary>
        public List<string> FighterInstanceIDs { get; set; } = new List<string>();

        /// <summary>Gets or sets the tactical time at which the next fighter launches.</summary>
        public float NextLaunchTime { get; set; }
    }

    /// <summary>
    /// Stores Death Star superlaser charge, delayed shots, and undrained notifications.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalSuperlaserSnapshot
    {
        /// <summary>Gets or sets each participating Death Star's current charge.</summary>
        public List<TacticalSuperlaserChargeSnapshot> Charges { get; set; } =
            new List<TacticalSuperlaserChargeSnapshot>();

        /// <summary>Gets or sets shots waiting for their delayed resolution.</summary>
        public List<TacticalSuperlaserShotSnapshot> PendingShots { get; set; } =
            new List<TacticalSuperlaserShotSnapshot>();

        /// <summary>Gets or sets ready notifications not yet drained by the simulator.</summary>
        public List<string> ReadyDeathStarInstanceIDs { get; set; } = new List<string>();

        /// <summary>Gets or sets resolved shots not yet drained by the simulator.</summary>
        public List<TacticalSuperlaserShotSnapshot> ResolvedShots { get; set; } =
            new List<TacticalSuperlaserShotSnapshot>();
    }

    /// <summary>
    /// Stores one participating Death Star's superlaser charge.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalSuperlaserChargeSnapshot
    {
        /// <summary>Gets or sets the Death Star strategic unit identifier.</summary>
        public string DeathStarInstanceID { get; set; }

        /// <summary>Gets or sets its current charge from zero through one hundred.</summary>
        public float Charge { get; set; }
    }

    /// <summary>
    /// Stores one delayed or newly resolved superlaser shot.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalSuperlaserShotSnapshot
    {
        /// <summary>Gets or sets the firing Death Star identifier.</summary>
        public string SourceInstanceID { get; set; }

        /// <summary>Gets or sets the targeted tactical unit identifier.</summary>
        public string TargetInstanceID { get; set; }

        /// <summary>Gets or sets the remaining delay before resolution.</summary>
        public float RemainingTime { get; set; }
    }

    /// <summary>
    /// Stores active and completed fighter attack runs against a Death Star.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalDeathStarAttackSnapshot
    {
        /// <summary>Gets or sets the attack runs whose timed reports are still active.</summary>
        public List<TacticalDeathStarAttackRunSnapshot> ActiveRuns { get; set; } =
            new List<TacticalDeathStarAttackRunSnapshot>();

        /// <summary>Gets or sets group indexes that already committed to an attack run.</summary>
        public List<int> CompletedGroupIndexes { get; set; } = new List<int>();
    }

    /// <summary>
    /// Stores one resolved Death Star attack outcome while its report sequence is active.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalDeathStarAttackRunSnapshot
    {
        /// <summary>Gets or sets the attacking fighter group index.</summary>
        public int GroupIndex { get; set; }

        /// <summary>Gets or sets the attack leader's strategic unit identifier.</summary>
        public string LeaderInstanceID { get; set; }

        /// <summary>Gets or sets the targeted Death Star identifier.</summary>
        public string DeathStarInstanceID { get; set; }

        /// <summary>Gets or sets whether the resolved run succeeds.</summary>
        public bool Succeeded { get; set; }

        /// <summary>Gets or sets whether approach fire damaged the attackers.</summary>
        public bool TookApproachDamage { get; set; }

        /// <summary>Gets or sets fighters scheduled to be lost when the run completes.</summary>
        public List<string> CompletionCasualtyInstanceIDs { get; set; } = new List<string>();

        /// <summary>Gets or sets elapsed attack-run time.</summary>
        public float ElapsedTime { get; set; }

        /// <summary>Gets or sets the number of report checkpoints already emitted.</summary>
        public int ReportsEmitted { get; set; }
    }

    /// <summary>
    /// Stores one unit's retained tactical combat target.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalTargetSnapshot
    {
        /// <summary>Gets or sets the acting unit identifier.</summary>
        public string SourceInstanceID { get; set; }

        /// <summary>Gets or sets the targeted unit identifier.</summary>
        public string TargetInstanceID { get; set; }
    }

    /// <summary>
    /// Stores one resolved group maneuver for the lifetime of its command revision.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalManeuverOrderSnapshot
    {
        /// <summary>Gets or sets the tactical group index.</summary>
        public int GroupIndex { get; set; }

        /// <summary>Gets or sets the command revision that created the maneuver.</summary>
        public int CommandRevision { get; set; }

        /// <summary>Gets or sets the opposing target identifier.</summary>
        public string TargetInstanceID { get; set; }

        /// <summary>Gets or sets the group center used to calculate the maneuver.</summary>
        public TacticalVectorSnapshot Origin { get; set; }

        /// <summary>Gets or sets the resolved navigation anchor.</summary>
        public TacticalVectorSnapshot Marker { get; set; }
    }

    /// <summary>
    /// Stores one capital ship's persistent collision-detour state.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCollisionAvoidanceSnapshot
    {
        /// <summary>Gets or sets the capital-ship identifier.</summary>
        public string UnitInstanceID { get; set; }

        /// <summary>Gets or sets whether vertical clearance failed on the previous update.</summary>
        public bool VerticalClearanceBlocked { get; set; }

        /// <summary>Gets or sets whether a temporary detour is active.</summary>
        public bool HasTemporaryOffset { get; set; }

        /// <summary>Gets or sets the active temporary destination offset.</summary>
        public TacticalVectorSnapshot TemporaryOffset { get; set; }

        /// <summary>Gets or sets the next detour phase.</summary>
        public int Phase { get; set; }

        /// <summary>Gets or sets when the temporary offset last changed.</summary>
        public float LastChangeTime { get; set; }
    }

    /// <summary>
    /// Stores the stable-marker count for one tactical group.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalMarkerStabilitySnapshot
    {
        /// <summary>Gets or sets the tactical group index.</summary>
        public int GroupIndex { get; set; }

        /// <summary>Gets or sets the marker position observed on the previous update.</summary>
        public TacticalVectorSnapshot Position { get; set; }

        /// <summary>Gets or sets consecutive refreshes at the same position.</summary>
        public int RefreshCount { get; set; }
    }

    /// <summary>
    /// Stores one unit's active withdrawal curve.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalWithdrawalSnapshot
    {
        /// <summary>Gets or sets the withdrawing unit identifier.</summary>
        public string UnitInstanceID { get; set; }

        /// <summary>Gets or sets the position where withdrawal began.</summary>
        public TacticalVectorSnapshot Origin { get; set; }

        /// <summary>Gets or sets the fixed exit direction.</summary>
        public TacticalVectorSnapshot Direction { get; set; }

        /// <summary>Gets or sets the stable flight-curve lane.</summary>
        public int Lane { get; set; }

        /// <summary>Gets or sets elapsed withdrawal time.</summary>
        public float ElapsedTime { get; set; }
    }
}
