using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Maintains per-side computer control and periodically assigns tactical targets.
    /// </summary>
    internal sealed class TacticalCommandAutomation
    {
        private const float _orderInterval = 1f;
        private readonly Dictionary<TacticalBattleSide, bool> automatedSides =
            new Dictionary<TacticalBattleSide, bool>();
        private readonly Dictionary<TacticalBattleSide, float> orderTimes =
            new Dictionary<TacticalBattleSide, float>();
        private readonly IReadOnlyList<TacticalShipGroup> groups;
        private readonly IReadOnlyList<TacticalUnitState> units;
        private bool playerControlConfigured;

        /// <summary>
        /// Initializes automated tactical command state for both sides.
        /// </summary>
        /// <param name="units">Every unit participating in the tactical battle.</param>
        /// <param name="groups">Every tactical command group.</param>
        public TacticalCommandAutomation(
            IReadOnlyList<TacticalUnitState> units,
            IReadOnlyList<TacticalShipGroup> groups
        )
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
            foreach (TacticalBattleSide side in Enum.GetValues(typeof(TacticalBattleSide)))
            {
                automatedSides.Add(side, false);
                orderTimes.Add(side, 0f);
            }
        }

        /// <summary>
        /// Configures the played side for manual commands and the opposing side for automated commands.
        /// A retained session preserves any command-mode changes made after this initial configuration.
        /// </summary>
        /// <param name="playerSide">The side controlled by the local player.</param>
        public void ConfigurePlayerControl(TacticalBattleSide playerSide)
        {
            ValidateSide(playerSide);
            if (playerControlConfigured)
                return;

            playerControlConfigured = true;
            SetAutomated(playerSide, false);
            SetAutomated(GetOpposingSide(playerSide), true);
        }

        /// <summary>
        /// Gets whether one side periodically receives computer-generated tactical orders.
        /// </summary>
        /// <param name="side">The tactical side to inspect.</param>
        /// <returns>True when the side is under automated command.</returns>
        public bool IsAutomated(TacticalBattleSide side)
        {
            ValidateSide(side);
            return automatedSides[side];
        }

        /// <summary>
        /// Enables or disables periodic computer-generated orders for one tactical side.
        /// Existing orders remain active when manual command is restored.
        /// </summary>
        /// <param name="side">The tactical side whose control mode changes.</param>
        /// <param name="automated">Whether the side should receive automated orders.</param>
        public void SetAutomated(TacticalBattleSide side, bool automated)
        {
            ValidateSide(side);
            automatedSides[side] = automated;
            orderTimes[side] = 0f;
        }

        /// <summary>
        /// Advances each automated side's periodic order cycle.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            foreach (TacticalBattleSide side in Enum.GetValues(typeof(TacticalBattleSide)))
            {
                if (!automatedSides[side])
                    continue;

                orderTimes[side] -= elapsedTime;
                if (orderTimes[side] > 0f)
                    continue;

                orderTimes[side] = _orderInterval;
                AssignTargets(side);
            }
        }

        /// <summary>
        /// Ranks active opposing units and distributes their priorities across one side's command groups.
        /// </summary>
        /// <param name="side">The side receiving automated orders.</param>
        private void AssignTargets(TacticalBattleSide side)
        {
            TacticalUnitState[] priorities = units
                .Where(unit => unit.Side != side && unit.IsActive)
                .OrderByDescending(GetTargetPriority)
                .ThenBy(unit => unit.Unit.TypeID, StringComparer.Ordinal)
                .ToArray();
            if (priorities.Length == 0)
                return;

            TacticalShipGroup[] activeGroups = groups
                .Where(group => group.Side == side && group.Units.Any(unit => unit.IsActive))
                .ToArray();
            for (int index = 0; index < activeGroups.Length; index++)
            {
                TacticalShipGroup group = activeGroups[index];
                int targetOffset = index % priorities.Length;
                TacticalUnitState[] orderedTargets = priorities
                    .Skip(targetOffset)
                    .Concat(priorities.Take(targetOffset))
                    .ToArray();
                group.ReplaceTargets(orderedTargets);
                group.SetBehavior(TacticalBehavior.PrimaryTarget);
            }
        }

        /// <summary>
        /// Scores an opposing unit from its remaining defenses and armed tactical strength.
        /// </summary>
        /// <param name="unit">The candidate target.</param>
        /// <returns>The target's descending priority score.</returns>
        private static int GetTargetPriority(TacticalUnitState unit)
        {
            int weaponStrength = unit.WeaponBatteries.Sum(battery =>
                battery.GetCount(TacticalWeaponArc.Fore)
                + battery.GetCount(TacticalWeaponArc.Aft)
                + battery.GetCount(TacticalWeaponArc.Port)
                + battery.GetCount(TacticalWeaponArc.Starboard)
            );
            return unit.Hull + unit.Shields + weaponStrength;
        }

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
