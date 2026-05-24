using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Stores manufactured unit totals for one faction.
    /// </summary>
    public sealed class ManufacturingTotals
    {
        public int CapitalShips { get; set; }
        public int Starfighters { get; set; }
        public int Regiments { get; set; }
        public int SpecialForces { get; set; }
    }

    /// <summary>
    /// Tracks aggregate game metrics that should survive save and load.
    /// </summary>
    [PersistableObject]
    public class GameMetrics
    {
        public Dictionary<string, ManufacturingTotals> ManufacturingTotalsByFaction =
            new Dictionary<string, ManufacturingTotals>();

        /// <summary>
        /// Records one manufactured unit in the owning faction's totals.
        /// </summary>
        /// <param name="entity">The manufactured entity to record.</param>
        public void RecordManufacturedUnit(IGameEntity entity)
        {
            if (entity is not ISceneNode node || string.IsNullOrEmpty(node.OwnerInstanceID))
                return;

            if (
                !ManufacturingTotalsByFaction.TryGetValue(
                    node.OwnerInstanceID,
                    out ManufacturingTotals totals
                )
            )
            {
                totals = new ManufacturingTotals();
                ManufacturingTotalsByFaction[node.OwnerInstanceID] = totals;
            }

            switch (entity)
            {
                case CapitalShip:
                    totals.CapitalShips++;
                    break;
                case Starfighter:
                    totals.Starfighters++;
                    break;
                case Regiment:
                    totals.Regiments++;
                    break;
                case SpecialForces:
                    totals.SpecialForces++;
                    break;
            }
        }

        /// <summary>
        /// Returns manufactured unit totals for a faction.
        /// </summary>
        /// <param name="faction">The faction to inspect.</param>
        /// <returns>The faction's manufactured unit totals.</returns>
        public ManufacturingTotals GetManufacturingTotals(Faction faction)
        {
            if (
                faction == null
                || !ManufacturingTotalsByFaction.TryGetValue(
                    faction.InstanceID,
                    out ManufacturingTotals totals
                )
            )
            {
                return new ManufacturingTotals();
            }

            return totals;
        }
    }
}
