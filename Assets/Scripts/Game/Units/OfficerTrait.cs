using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Defines the gameplay effects supplied by one content-authored officer trait.
    /// </summary>
    [PersistableObject(Name = "Trait")]
    public sealed class OfficerTraitDefinition
    {
        [PersistableAttribute]
        public string ID { get; set; }

        public int LeadershipBonusPercent { get; set; }

        public int PersonnelTravelTimePercent { get; set; } = 100;

        public bool SharesPersonnelTravelTimeModifier { get; set; }
    }

    /// <summary>
    /// Records one event-owned trait assignment on an officer.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerTraitGrant
    {
        [PersistableAttribute]
        public string TraitID { get; set; }

        [PersistableAttribute]
        public string SourceID { get; set; }
    }
}
