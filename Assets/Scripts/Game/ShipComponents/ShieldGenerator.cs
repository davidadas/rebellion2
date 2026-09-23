using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Produces and recharges a ship's shields.</summary>
    [PersistableObject]
    public sealed class ShieldGenerator : ShipComponent
    {
        public int Capacity { get; set; }
        public int RechargeRate { get; set; }

        public override ShipComponent CreateCopy()
        {
            ShieldGenerator copy = new ShieldGenerator
            {
                Health = Health,
                Capacity = Capacity,
                RechargeRate = RechargeRate,
            };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
