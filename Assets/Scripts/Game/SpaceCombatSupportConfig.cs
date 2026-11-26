using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Identifies the saved campaign setup selecting an authored battle support profile.
    /// </summary>
    public enum SpaceCombatSupportContext
    {
        FirstFactionEasy = 1,
        FirstFactionMedium = 2,
        FirstFactionHard = 3,
        SecondFactionEasy = 4,
        SecondFactionMedium = 5,
        SecondFactionHard = 6,
        Multiplayer = 7,
    }

    /// <summary>
    /// Stores the authored battle support profiles for campaign setup.
    /// </summary>
    [PersistableObject]
    public sealed class SpaceCombatSupportConfig
    {
        public Dictionary<
            SpaceCombatSupportContext,
            SpaceCombatSupportProfile
        > Profiles { get; set; } = new();

        /// <summary>
        /// Creates an independent copy without supplying missing profiles or scalar values.
        /// </summary>
        /// <returns>The copied profile collection.</returns>
        public SpaceCombatSupportConfig CreateCopy() =>
            new()
            {
                Profiles = Profiles?.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value?.CreateCopy()
                ),
            };
    }

    /// <summary>
    /// Supplies one campaign context's divisor and weights for the two authored strategic sides.
    /// Null values remain explicitly missing until authored configuration supplies them.
    /// </summary>
    [PersistableObject]
    public sealed class SpaceCombatSupportProfile
    {
        public int? Divisor { get; set; }
        public SpaceCombatSupportSideConfig FirstSide { get; set; }
        public SpaceCombatSupportSideConfig SecondSide { get; set; }

        /// <summary>
        /// Creates an independent copy retaining all missing values.
        /// </summary>
        /// <returns>The copied profile.</returns>
        public SpaceCombatSupportProfile CreateCopy() =>
            new()
            {
                Divisor = Divisor,
                FirstSide = FirstSide?.CreateCopy(),
                SecondSide = SecondSide?.CreateCopy(),
            };

        /// <summary>
        /// Checks that all calculation inputs were supplied, including a nonzero divisor.
        /// </summary>
        /// <returns>True when the profile can supply every calculation input.</returns>
        internal bool HasAllValues() =>
            Divisor.HasValue
            && Divisor.Value != 0
            && FirstSide?.HasAllValues() == true
            && SecondSide?.HasAllValues() == true;
    }

    /// <summary>
    /// Defines one strategic side's authored force-order weights and support bounds.
    /// </summary>
    [PersistableObject]
    public sealed class SpaceCombatSupportSideConfig
    {
        public int? LeadingFlat { get; set; }
        public int? LeadingGapScale { get; set; }
        public int? TrailingFlat { get; set; }
        public int? TrailingGapScale { get; set; }
        public int? MagnitudeCap { get; set; }
        public uint? SupportThreshold { get; set; }

        /// <summary>
        /// Creates an independent copy retaining every authored or missing scalar value.
        /// </summary>
        /// <returns>The copied side settings.</returns>
        public SpaceCombatSupportSideConfig CreateCopy() =>
            new()
            {
                LeadingFlat = LeadingFlat,
                LeadingGapScale = LeadingGapScale,
                TrailingFlat = TrailingFlat,
                TrailingGapScale = TrailingGapScale,
                MagnitudeCap = MagnitudeCap,
                SupportThreshold = SupportThreshold,
            };

        /// <summary>
        /// Checks that all six side-specific calculation inputs were supplied.
        /// </summary>
        /// <returns>True when no side input is missing.</returns>
        internal bool HasAllValues() =>
            LeadingFlat.HasValue
            && LeadingGapScale.HasValue
            && TrailingFlat.HasValue
            && TrailingGapScale.HasValue
            && MagnitudeCap.HasValue
            && SupportThreshold.HasValue;
    }
}
