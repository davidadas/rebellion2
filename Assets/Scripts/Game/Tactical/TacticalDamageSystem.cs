namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Identifies a capital-ship subsystem that can suffer persistent tactical damage.
    /// </summary>
    public enum TacticalDamageSystem
    {
        /// <summary>Reduces sublight movement and disables it at maximum damage.</summary>
        SublightDrive,

        /// <summary>Reduces shield recharge.</summary>
        ShieldGenerator,

        /// <summary>Reduces hyperdrive capability and prevents withdrawal at maximum damage.</summary>
        Hyperdrive,

        /// <summary>Reduces tractor-beam strength.</summary>
        TractorBeam,

        /// <summary>Reduces weapon recharge.</summary>
        WeaponSystems,
    }
}
