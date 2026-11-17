using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Retains accepted losses for one encounter until its consequences read current ownership and values.
    /// </summary>
    internal sealed class SpaceCombatLossLedger
    {
        private readonly List<ISceneNode> _destroyedUnits = new();

        /// <summary>
        /// Records an accepted ship or squadron destruction, or a completed stationary carried regiment.
        /// </summary>
        /// <param name="unit">The actual destroyed unit retained by the encounter.</param>
        public void RecordDestroyedUnit(ISceneNode unit)
        {
            if (
                unit is CapitalShip or Starfighter
                || unit
                    is Regiment
                    {
                        ManufacturingStatus: ManufacturingStatus.Complete,
                        Movement: null
                    }
            )
                _destroyedUnits.Add(unit);
        }

        /// <summary>
        /// Totals retained losses using each unit's ownership and full authored value at query time.
        /// </summary>
        /// <param name="ownerInstanceId">The owner identity to total, or null for neutral ownership.</param>
        /// <returns>The owner's lost unit value with signed 32-bit wrapping.</returns>
        public int GetLostValue(string ownerInstanceId)
        {
            int total = 0;
            foreach (ISceneNode unit in _destroyedUnits)
            {
                if (unit.OwnerInstanceID != ownerInstanceId)
                    continue;
                int value = unit switch
                {
                    CapitalShip ship => ship.UprisingDefense,
                    Starfighter fighter => fighter.UprisingDefense,
                    Regiment regiment => regiment.UprisingDefense,
                    _ => 0,
                };
                total = unchecked(total + value);
            }
            return total;
        }
    }
}
