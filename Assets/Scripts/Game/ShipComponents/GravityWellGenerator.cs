using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Projects a gravity well that prevents hyperspace travel.</summary>
    [PersistableObject]
    public sealed class GravityWellGenerator : ShipComponent
    {
        public override ShipComponent CreateCopy()
        {
            GravityWellGenerator copy = new GravityWellGenerator { Health = Health };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
