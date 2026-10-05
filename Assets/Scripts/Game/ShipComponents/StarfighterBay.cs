using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Houses starfighters aboard a ship.</summary>
    [PersistableObject]
    public sealed class StarfighterBay : ShipComponent
    {
        public int Capacity { get; set; }

        /// <summary>Creates an independent copy of this starfighter bay.</summary>
        /// <returns>The copied starfighter bay.</returns>
        public override ShipComponent CreateCopy()
        {
            StarfighterBay copy = new StarfighterBay { Capacity = Capacity };
            CopyComponentStateTo(copy);
            return copy;
        }
    }
}
