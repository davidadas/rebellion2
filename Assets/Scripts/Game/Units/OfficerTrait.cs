using System.Collections.Generic;
using Rebellion.Game.Scripting;
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

        [PersistableCollectionItem(Name = "Behavior")]
        public List<OfficerTraitBehavior> Behaviors { get; set; } =
            new List<OfficerTraitBehavior>();
    }

    /// <summary>
    /// Defines an ordered script activated by a trait holder's tick or a matching game result.
    /// </summary>
    [PersistableObject(Name = "Behavior")]
    public sealed class OfficerTraitBehavior
    {
        [PersistableAttribute]
        public string ID { get; set; }

        [PersistableAttribute]
        public bool EveryTick { get; set; }

        [PersistableAttribute]
        public bool IncludeInactive { get; set; }

        public List<GameTrigger> Triggers { get; set; } = new List<GameTrigger>();
        public List<GameBinding> Bindings { get; set; } = new List<GameBinding>();
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Records one script-owned trait assignment on an officer.
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
