using System;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Copies campaign-relevant results from a detached battle unit back to its stable source.
    /// </summary>
    public static class BattleResultReconciler
    {
        /// <summary>
        /// Applies damage and recovery results without replacing the campaign scene node.
        /// </summary>
        /// <param name="combatant">The independently simulated battle unit.</param>
        /// <param name="sourceUnit">The stable campaign unit that produced it.</param>
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

            switch (combatant.Unit, sourceUnit)
            {
                case (CapitalShip battleShip, CapitalShip sourceShip):
                    sourceShip.CurrentHullStrength = battleShip.CurrentHullStrength;
                    sourceShip.SystemDamage =
                        battleShip.SystemDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
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
