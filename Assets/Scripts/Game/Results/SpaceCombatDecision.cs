using Rebellion.Util.Serialization;

namespace Rebellion.Game.Results
{
    /// <summary>
    /// Identifies a fleet encounter waiting for space-combat resolution.
    /// </summary>
    [PersistableObject]
    public sealed class SpaceCombatDecision
    {
        /// <summary>Gets or sets the attacking fleet identifier.</summary>
        public string AttackerFleetInstanceID { get; set; }

        /// <summary>Gets or sets the defending fleet identifier.</summary>
        public string DefenderFleetInstanceID { get; set; }

        /// <summary>Gets or sets the attacking faction identifier.</summary>
        public string AttackerOwnerInstanceID { get; set; }

        /// <summary>Gets or sets the defending faction identifier.</summary>
        public string DefenderOwnerInstanceID { get; set; }

        /// <summary>Gets or sets the combat planet identifier.</summary>
        public string PlanetInstanceID { get; set; }
    }
}
