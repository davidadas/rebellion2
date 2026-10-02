using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Base state shared by physical components installed on a ship.</summary>
    [PersistableObject]
    public abstract class ShipComponent : BaseGameEntity
    {
        public int Health { get; set; }

        /// <summary>Creates an independent copy of the component.</summary>
        /// <returns>The copied component.</returns>
        public abstract ShipComponent CreateCopy();
    }
}
