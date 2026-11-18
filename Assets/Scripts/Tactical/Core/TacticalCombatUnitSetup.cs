using Rebellion.Game.Units;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Supplies captured participant values without retaining the campaign object or its scene graph.
    /// The encounter copies these values into its own mutable systems and batteries.
    /// </summary>
    public sealed class TacticalCombatUnitSetup
    {
        public string InstanceID { get; set; }
        public string TypeID { get; set; }
        public string DisplayName { get; set; }
        public bool IsFighter { get; set; }
        public bool IsBattleStation { get; set; }
        public bool IsUnderConstruction { get; set; }
        public bool HasPlanetaryProtection { get; set; }
        public bool HasPrimaryHyperdrive { get; set; }
        public int PayloadCount { get; set; }
        public float MaximumHull { get; set; }
        public float InitialHull { get; set; }
        public float MaximumShields { get; set; }
        public float WeaponRecharge { get; set; }
        public float ShieldRecharge { get; set; }
        public float SublightSpeed { get; set; }
        public float Maneuverability { get; set; }
        public float TractorBeamPower { get; set; }
        public float DamageControl { get; set; }
        public CapitalShipSystemDamage SystemDamage { get; set; }
        public int[,] WeaponStrengths { get; set; }
        public float[] WeaponRanges { get; set; }
        public float TractorRange { get; set; }
        public bool HasGravityWell { get; set; }
        public int FighterCapacity { get; set; }
        public int InitialSquadronSize { get; set; }
        public string InitialCarrierID { get; set; }
        public int TorpedoStrength { get; set; }
        public float TorpedoRange { get; set; }
    }
}
