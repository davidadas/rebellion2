using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Groups weapon mounts that operate together.
    /// </summary>
    [PersistableObject]
    public sealed class HardpointGroup
    {
        public List<Hardpoint> Hardpoints { get; set; } = new List<Hardpoint>();

        /// <summary>
        /// Creates an independent copy of the hardpoint group and its mounts.
        /// </summary>
        /// <returns>The copied hardpoint group.</returns>
        public HardpointGroup CreateCopy() =>
            new HardpointGroup
            {
                Hardpoints = Hardpoints?.ConvertAll(hardpoint =>
                    (Hardpoint)hardpoint?.CreateCopy()
                ),
            };
    }
}
