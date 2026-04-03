using System;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// Defines faction-specific gameplay modifiers.
    /// These modifiers affect game mechanics for all players controlling this faction,
    /// not just AI behavior.
    /// </summary>
    [Serializable]
    [PersistableObject]
    public class FactionModifiers
    {
        private int garrisonEfficiency = 1;
        private int troopEffectiveness = 1;
        private int uprisingResistance = 1;

        /// <summary>
        /// Garrison requirement divisor on core systems.
        /// Higher values mean fewer troops needed (2 = half garrison required).
        /// Alliance: 1 (normal), Empire: 2 (halved on core worlds).
        /// Must be >= 1.
        /// </summary>
        public int GarrisonEfficiency
        {
            get => garrisonEfficiency;
            set => garrisonEfficiency = Math.Max(1, value);
        }

        /// <summary>
        /// Hostile troop weight multiplier in support calculations.
        /// Higher values mean enemy troops count for more when evaluating support.
        /// Alliance: 1 (normal), Empire: 2 (troops count double).
        /// Must be >= 1.
        /// </summary>
        public int TroopEffectiveness
        {
            get => troopEffectiveness;
            set => troopEffectiveness = Math.Max(1, value);
        }

        /// <summary>
        /// Uprising resistance multiplier.
        /// Higher values mean the faction is better at suppressing uprisings.
        /// Alliance: 1 (normal), Empire: 2 (double effectiveness).
        /// Must be >= 1.
        /// </summary>
        public int UprisingResistance
        {
            get => uprisingResistance;
            set => uprisingResistance = Math.Max(1, value);
        }

        /// <summary>
        /// Whether support shift calculations are inverted for this faction.
        /// Alliance: false (normal), Empire: true (inverted).
        /// </summary>
        public bool InvertSupportShift { get; set; } = false;

        /// <summary>
        /// Condition under which the weak support penalty triggers.
        /// Alliance: Positive (penalty when shift > 0), Empire: Negative (penalty when shift &lt; 0).
        /// </summary>
        public SupportShiftCondition WeakSupportPenaltyTrigger { get; set; } =
            SupportShiftCondition.Positive;
    }

    /// <summary>
    /// Determines when the weak support penalty applies to a faction.
    /// </summary>
    public enum SupportShiftCondition
    {
        /// <summary>Penalty triggers when support shift is positive.</summary>
        Positive,

        /// <summary>Penalty triggers when support shift is negative.</summary>
        Negative,
    }
}
