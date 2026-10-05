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
        [PersistableMember(Name = "Hardpoints")]
        private List<Hardpoint> _hardpoints = new List<Hardpoint>();

        /// <summary>
        /// Returns the weapon mounts in this group.
        /// </summary>
        /// <returns>The group's hardpoints.</returns>
        public List<Hardpoint> GetHardpoints()
        {
            return _hardpoints;
        }

        /// <summary>
        /// Creates an independent copy of the hardpoint group and its mounts.
        /// </summary>
        /// <returns>The copied hardpoint group.</returns>
        public HardpointGroup CreateCopy()
        {
            return new HardpointGroup
            {
                _hardpoints = _hardpoints?.ConvertAll(hardpoint =>
                    (Hardpoint)hardpoint?.CreateCopy()
                ),
            };
        }
    }
}
