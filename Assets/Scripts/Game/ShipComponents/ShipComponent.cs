using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Base state shared by physical components installed on a ship.</summary>
    [PersistableObject]
    public abstract class ShipComponent : BaseGameEntity
    {
        public int Health { get; set; }
        public bool Targetable { get; set; }
        public string ModelNodePath { get; set; }

        /// <summary>Copies state shared by every ship component.</summary>
        /// <param name="copy">The destination component.</param>
        protected void CopyComponentStateTo(ShipComponent copy)
        {
            copy.Health = Health;
            copy.Targetable = Targetable;
            copy.ModelNodePath = ModelNodePath;
            CopyEntityStateTo(copy);
        }

        /// <summary>Creates an independent copy of the component.</summary>
        /// <returns>The copied component.</returns>
        public abstract ShipComponent CreateCopy();
    }
}
