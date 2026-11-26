namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Identifies a ship system that can suffer damage or receive repairs in combat.
    /// </summary>
    public enum TacticalShipSystem
    {
        Shields,
        Weapons,
        TractorBeam,
        Engines,
        Hyperdrive,
    }

    /// <summary>
    /// Identifies the damage delivered by a tactical weapon.
    /// </summary>
    public enum TacticalWeaponType
    {
        LaserCannon,
        IonCannon,
        Turbolaser,
        Torpedo,
    }

    /// <summary>
    /// Identifies the surface reached by an applied tactical weapon impact.
    /// </summary>
    public enum TacticalCombatImpactKind
    {
        None,
        Shield,
        Hull,
        Destroyed,
    }

    /// <summary>
    /// Identifies a persistent component change or temporary ion disruption.
    /// </summary>
    public enum TacticalSystemEventKind
    {
        Damage,
        Repair,
        Disruption,
    }

    /// <summary>
    /// Describes a system event awaiting application by the battle's command queue.
    /// </summary>
    public readonly struct TacticalSystemEvent
    {
        public TacticalShipSystem System { get; }
        public TacticalSystemEventKind Kind { get; }
        public bool IsRepair => Kind == TacticalSystemEventKind.Repair;

        /// <summary>
        /// Gets the attack timer increment for disruption, or null for a component change.
        /// </summary>
        public float? AttackDelay { get; }

        /// <summary>
        /// Gets the movement timer increment for disruption, or null for a component change.
        /// </summary>
        public float? MovementDelay { get; }
        public int DischargedArcMask { get; }

        /// <summary>
        /// Creates a persistent component damage or repair event.
        /// </summary>
        /// <param name="system">The affected ship system.</param>
        /// <param name="isRepair">Whether the event repairs one damage level.</param>
        public TacticalSystemEvent(TacticalShipSystem system, bool isRepair)
        {
            System = system;
            Kind = isRepair ? TacticalSystemEventKind.Repair : TacticalSystemEventKind.Damage;
            AttackDelay = null;
            MovementDelay = null;
            DischargedArcMask = 0;
        }

        /// <summary>
        /// Creates a disruption event with explicit timer increments and discharged batteries.
        /// </summary>
        /// <param name="attackDelay">The additional attack delay in seconds.</param>
        /// <param name="movementDelay">The additional movement delay in seconds.</param>
        /// <param name="dischargedArcMask">The batteries whose charge must be cleared.</param>
        public TacticalSystemEvent(float attackDelay, float movementDelay, int dischargedArcMask)
        {
            System = TacticalShipSystem.Weapons;
            Kind = TacticalSystemEventKind.Disruption;
            AttackDelay = attackDelay;
            MovementDelay = movementDelay;
            DischargedArcMask = dischargedArcMask;
        }
    }
}
