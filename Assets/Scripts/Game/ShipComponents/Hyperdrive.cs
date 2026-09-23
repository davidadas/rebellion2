using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Provides hyperspace travel.</summary>
    [PersistableObject]
    public sealed class Hyperdrive : ShipComponent
    {
        public int Rating { get; set; }

        public override ShipComponent CreateCopy()
        {
            Hyperdrive copy = new Hyperdrive { Health = Health, Rating = Rating };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
