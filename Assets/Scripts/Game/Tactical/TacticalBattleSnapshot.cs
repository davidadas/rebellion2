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
}
