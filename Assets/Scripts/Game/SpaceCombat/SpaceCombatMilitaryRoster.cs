using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Retains registered military units for current force-value captures without scene traversal.
    /// </summary>
    internal sealed class SpaceCombatMilitaryRoster
    {
        private readonly Dictionary<string, ISceneNode> _units = new();

        /// <summary>
        /// Retains an eligible military family after its registry insertion succeeds.
        /// </summary>
        /// <param name="node">The newly registered scene node.</param>
        public void Register(ISceneNode node)
        {
            if (node is Regiment or CapitalShip or Starfighter)
                _units.Add(node.InstanceID, node);
        }

        /// <summary>
        /// Releases the military entry using the key removed from the scene registry.
        /// </summary>
        /// <param name="instanceID">The removed registry key.</param>
        public void Unregister(string instanceID) => _units.Remove(instanceID);

        /// <summary>
        /// Releases all entries before the scene registry is rebuilt or replaced.
        /// </summary>
        public void Clear() => _units.Clear();

        /// <summary>
        /// Captures both requested owners' current completed or delivering military values in one pass.
        /// </summary>
        /// <param name="firstOwnerInstanceId">The first owner identity, or null for neutral ownership.</param>
        /// <param name="secondOwnerInstanceId">The second owner identity, or null for neutral ownership.</param>
        /// <returns>The two current full-value totals with signed 32-bit wrapping.</returns>
        public (int FirstSide, int SecondSide) CaptureValues(
            string firstOwnerInstanceId,
            string secondOwnerInstanceId
        )
        {
            int first = 0;
            int second = 0;
            foreach (KeyValuePair<string, ISceneNode> entry in _units)
            {
                ISceneNode unit = entry.Value;
                if (
                    ((IManufacturable)unit).ManufacturingStatus
                    is not (ManufacturingStatus.Complete or ManufacturingStatus.Delivering)
                )
                    continue;
                int value = unit switch
                {
                    Regiment regiment => regiment.UprisingDefense,
                    CapitalShip ship => ship.UprisingDefense,
                    Starfighter fighter => fighter.UprisingDefense,
                    _ => 0,
                };
                string owner = unit.OwnerInstanceID;
                if (owner == firstOwnerInstanceId)
                    first = unchecked(first + value);
                if (owner == secondOwnerInstanceId)
                    second = unchecked(second + value);
            }
            return (first, second);
        }
    }
}
