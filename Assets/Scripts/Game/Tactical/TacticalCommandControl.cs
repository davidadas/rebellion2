using System;
using System.Collections.Generic;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Tracks whether each tactical side accepts player orders or computer control.
    /// </summary>
    internal sealed class TacticalCommandControl
    {
        private readonly Dictionary<TacticalBattleSide, bool> computerControlledSides =
            new Dictionary<TacticalBattleSide, bool>();
        private bool playerControlConfigured;

        /// <summary>
        /// Initializes command control for both tactical sides.
        /// </summary>
        public TacticalCommandControl()
        {
            foreach (TacticalBattleSide side in Enum.GetValues(typeof(TacticalBattleSide)))
                computerControlledSides.Add(side, false);
        }

        /// <summary>
        /// Assigns player command to the played side and computer command to its opponent.
        /// A retained session preserves command-mode changes after initial configuration.
        /// </summary>
        /// <param name="playerSide">The side controlled by the local player.</param>
        public void ConfigurePlayerControl(TacticalBattleSide playerSide)
        {
            ValidateSide(playerSide);
            if (playerControlConfigured)
                return;

            playerControlConfigured = true;
            SetComputerControlled(playerSide, false);
            SetComputerControlled(GetOpposingSide(playerSide), true);
        }

        /// <summary>
        /// Gets whether one side is under computer command.
        /// </summary>
        /// <param name="side">The tactical side to inspect.</param>
        /// <returns>True when the computer controls the side.</returns>
        public bool IsComputerControlled(TacticalBattleSide side)
        {
            ValidateSide(side);
            return computerControlledSides[side];
        }

        /// <summary>
        /// Changes whether one tactical side is under computer command.
        /// Existing group orders remain active across the control change.
        /// </summary>
        /// <param name="side">The tactical side whose control mode changes.</param>
        /// <param name="computerControlled">Whether the computer controls the side.</param>
        public void SetComputerControlled(TacticalBattleSide side, bool computerControlled)
        {
            ValidateSide(side);
            computerControlledSides[side] = computerControlled;
        }

        /// <summary>
        /// Restores both tactical sides' control modes without repeating initial configuration.
        /// </summary>
        /// <param name="attackerComputerControlled">Whether the attacker uses computer commands.</param>
        /// <param name="defenderComputerControlled">Whether the defender uses computer commands.</param>
        /// <param name="configured">Whether initial player command assignment already occurred.</param>
        internal void Restore(
            bool attackerComputerControlled,
            bool defenderComputerControlled,
            bool configured
        )
        {
            computerControlledSides[TacticalBattleSide.Attacker] = attackerComputerControlled;
            computerControlledSides[TacticalBattleSide.Defender] = defenderComputerControlled;
            playerControlConfigured = configured;
        }

        /// <summary>
        /// Gets whether initial player command assignment already occurred.
        /// </summary>
        internal bool IsPlayerControlConfigured => playerControlConfigured;

        /// <summary>
        /// Returns the opposing member of the two-sided tactical battle.
        /// </summary>
        /// <param name="side">The known side.</param>
        /// <returns>The opposing tactical side.</returns>
        private static TacticalBattleSide GetOpposingSide(TacticalBattleSide side)
        {
            return side == TacticalBattleSide.Attacker
                ? TacticalBattleSide.Defender
                : TacticalBattleSide.Attacker;
        }

        /// <summary>
        /// Rejects an undefined tactical side before indexing fixed command state.
        /// </summary>
        /// <param name="side">The side to validate.</param>
        private static void ValidateSide(TacticalBattleSide side)
        {
            if (!Enum.IsDefined(typeof(TacticalBattleSide), side))
                throw new ArgumentOutOfRangeException(nameof(side));
        }
    }
}
