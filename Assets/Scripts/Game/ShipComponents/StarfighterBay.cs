using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Houses starfighters aboard a ship.</summary>
    [PersistableObject]
    public sealed class StarfighterBay : ShipComponent
    {
        public int Capacity { get; set; }

        public override ShipComponent CreateCopy()
        {
            StarfighterBay copy = new StarfighterBay { Health = Health, Capacity = Capacity };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
