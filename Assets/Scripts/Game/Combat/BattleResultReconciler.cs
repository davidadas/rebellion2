using System;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Copies the campaign-relevant result of a detached battle unit back to its stable source.
    /// Scene placement, orders, and other campaign relationships remain owned by the source graph.
    /// </summary>
    public static class BattleResultReconciler
    {
        public static void Apply(CombatUnit combatant, BaseSceneNode sourceUnit)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (
                !string.Equals(
                    combatant.SourceUnitInstanceID,
                    sourceUnit.InstanceID,
                    StringComparison.Ordinal
                )
            )
                throw new ArgumentException(
                    "The source unit does not match the combatant identity.",
                    nameof(sourceUnit)
                );

            switch (combatant.GetUnit(), sourceUnit)
            {
                case (CapitalShip battleShip, CapitalShip sourceShip):
                    int repairDueTick = sourceShip.SystemDamage?.RepairDueTick ?? 0;
                    int repairInterval = sourceShip.SystemDamage?.RepairInterval ?? 0;
                    sourceShip.CurrentHullStrength = battleShip.CurrentHullStrength;
                    sourceShip.SystemDamage =
                        battleShip.SystemDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
                    sourceShip.SystemDamage.RepairDueTick = repairDueTick;
                    sourceShip.SystemDamage.RepairInterval = repairInterval;
                    break;
                case (Starfighter battleFighter, Starfighter sourceFighter):
                    sourceFighter.CurrentSquadronSize = battleFighter.CurrentSquadronSize;
                    break;
                default:
                    throw new ArgumentException(
                        "The source unit type does not match the battle copy.",
                        nameof(sourceUnit)
                    );
            }
        }
    }
}
