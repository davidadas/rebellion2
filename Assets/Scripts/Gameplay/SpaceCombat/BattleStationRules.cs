using System;
using Rebellion.Game;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Holds the rules a planet-destroying battle station follows in a space battle. The
    /// interactive tactical battle and automatic resolution both read them, so a battle is decided
    /// the same way whether or not the player commands it.
    /// </summary>
    internal static class BattleStationRules
    {
        // The rounds a superlaser beam burns before its target is destroyed.
        internal const double StrikeRounds = 2;

        // The rounds a station under automatic control holds its first shot.
        internal const double AutomaticHoldRounds = 10;

        // The rounds a station needs to make ready before it can jump to hyperspace.
        internal const double JumpRounds = 12;

        // How far squadrons fly to reach a station under automatic resolution, in starting
        // distances: about what the same flight covers across the tactical battlefield.
        internal const double TrenchRunDistanceFactor = 6;

        /// <summary>
        /// Returns the rounds a superlaser takes to recharge fully.
        /// </summary>
        /// <param name="config">The space-combat parameters.</param>
        /// <returns>The recharge time in combat rounds.</returns>
        internal static double GetRechargeRounds(GameConfig.SpaceCombatConfig config)
        {
            return Math.Max(config.BattleStationSuperlaserRechargeRounds, 0);
        }

        /// <summary>
        /// Returns the chance that a trench run succeeds. A run needs torpedoes; with them it
        /// succeeds at the configured base chance while the fighters flying it are at least as
        /// many as the enemy fighters still in the fight, and less in proportion when they are
        /// outnumbered, down to a configured floor.
        /// </summary>
        /// <param name="attackers">The fighters flying the run.</param>
        /// <param name="armed">Whether any squadron flying the run carries torpedoes.</param>
        /// <param name="defenders">The station side's fighters still in the fight.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <returns>The chance from 0 to 1.</returns>
        internal static double GetTrenchRunChance(
            int attackers,
            bool armed,
            int defenders,
            GameConfig.SpaceCombatConfig config
        )
        {
            if (!armed || attackers <= 0)
                return 0;

            double baseChance = Math.Max(Math.Min(config.BattleStationTrenchRunBaseChance, 1), 0);
            double minimumChance = Math.Min(
                Math.Max(config.BattleStationTrenchRunMinimumChance, 0),
                baseChance
            );
            return defenders <= attackers
                ? baseChance
                : Math.Max(baseChance * attackers / defenders, minimumChance);
        }
    }
}
