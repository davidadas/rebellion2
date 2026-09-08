using Rebellion.Util.Serialization;

namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Describes one authored adjustment independently of its source and consumer.
    /// </summary>
    [PersistableObject]
    public sealed class Modifier
    {
        public ModifierType Type { get; set; }

        public ModifierOperation Operation { get; set; } = ModifierOperation.Multiply;

        public decimal Value { get; set; } = 1m;

        public ModifierTarget Target { get; set; } = ModifierTarget.Self;

        public ModifierRecipient Recipient { get; set; } = ModifierRecipient.Any;

        /// <summary>
        /// Optionally restricts the target to one authored or runtime instance ID.
        /// </summary>
        public string TargetID { get; set; }
    }
}
