using System;
using Rebellion.Game;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Holds the rules for a capital ship's damaged shield generator in a space battle. The
    /// interactive tactical battle and automatic resolution both read them, so a ship whose
    /// generator is under repair fights without recharging shields whether or not the player
    /// commands the battle.
    /// </summary>
    internal static class ShieldGeneratorRules
    {
        // The combat rounds of generator downtime that one strategic repair day stands for.
        private const double _roundsPerRepairDay = 20;

        /// <summary>
        /// Returns the downtime a ship brings into a battle for the repair its generator owes.
        /// </summary>
        /// <param name="repairDays">The strategic repair days still owed.</param>
        /// <returns>The downtime in combat rounds.</returns>
        internal static double GetDowntime(int repairDays)
        {
            return Math.Max(repairDays, 0) * _roundsPerRepairDay;
        }

        /// <summary>
        /// Returns the strategic repair days a ship carries out of a battle for the downtime
        /// its generator still has.
        /// </summary>
        /// <param name="downtime">The remaining downtime in combat rounds.</param>
        /// <returns>The repair days still owed, rounded up.</returns>
        internal static int GetRepairDays(double downtime)
        {
            return (int)Math.Ceiling(Math.Max(downtime, 0) / _roundsPerRepairDay);
        }

        /// <summary>
        /// Returns the downtime left after a stretch of battle. A knocked-out generator takes
        /// far longer to bring back than other components, so the loss shapes the rest of the
        /// battle rather than a few volleys.
        /// </summary>
        /// <param name="downtime">The downtime before the stretch, in combat rounds.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <returns>The remaining downtime in combat rounds.</returns>
        internal static double Recover(
            double downtime,
            double rounds,
            GameConfig.SpaceCombatConfig config
        )
        {
            double recovery =
                Math.Max(config.AutoResolveComponentDelayRecovery, 1)
                * rounds
                / Math.Max(config.TacticalShieldGeneratorRepairMultiplier, 1);
            return Math.Max(downtime - recovery, 0);
        }
    }
}
