using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
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
    /// Owns authored battle support profiles and resolves them from saved campaign identity.
    /// Faction indices come from the shared tactical definition; current controllers and local
    /// player selection do not change this campaign rule.
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

        /// <summary>
        /// Resolves a complete authored profile from saved launch settings or multiplayer identity.
        /// Missing or ambiguous faction mappings, campaign factions, contexts, or values fail without
        /// selecting another profile.
        /// </summary>
        /// <param name="game">The campaign supplying persisted setup and active faction identity.</param>
        /// <param name="tacticalCombat">The shared authored faction-index map.</param>
        /// <param name="profile">The selected complete profile, or null when resolution fails.</param>
        /// <returns>True when the exact saved context has a complete profile.</returns>
        public bool TryResolveProfile(
            GameRoot game,
            TacticalCombatDefinition tacticalCombat,
            out SpaceCombatSupportProfile profile
        )
        {
            profile = null;
            if (
                game?.Summary == null
                || game.Metadata == null
                || !TryResolveFactionIDs(
                    tacticalCombat,
                    out string firstFactionID,
                    out string secondFactionID
                )
                || !game.GetFactions().Any(faction => faction.InstanceID == firstFactionID)
                || !game.GetFactions().Any(faction => faction.InstanceID == secondFactionID)
            )
                return false;

            SpaceCombatSupportContext? context = game.Metadata.IsMultiplayer
                ? SpaceCombatSupportContext.Multiplayer
                : ResolveSinglePlayerContext(game.Summary, firstFactionID, secondFactionID);
            if (
                !context.HasValue
                || Profiles == null
                || !Profiles.TryGetValue(context.Value, out SpaceCombatSupportProfile selected)
                || selected?.HasAllValues() != true
            )
                return false;

            profile = selected;
            return true;
        }

        /// <summary>
        /// Resolves each configured strategic side from its unique authored tactical index.
        /// </summary>
        /// <param name="definition">The shared faction-index definition.</param>
        /// <param name="firstFactionID">The faction bound to index zero.</param>
        /// <param name="secondFactionID">The faction bound to index one.</param>
        /// <returns>True when both indices have distinct, unambiguous faction identities.</returns>
        private static bool TryResolveFactionIDs(
            TacticalCombatDefinition definition,
            out string firstFactionID,
            out string secondFactionID
        )
        {
            firstFactionID = null;
            secondFactionID = null;
            if (definition?.Factions == null)
                return false;

            foreach (TacticalCombatFactionDefinition faction in definition.Factions)
            {
                if (faction == null || string.IsNullOrEmpty(faction.FactionInstanceID))
                    return false;
                if (faction.FactionIndex == 0 && firstFactionID == null)
                    firstFactionID = faction.FactionInstanceID;
                else if (faction.FactionIndex == 1 && secondFactionID == null)
                    secondFactionID = faction.FactionInstanceID;
                else
                    return false;
            }

            return firstFactionID != null
                && secondFactionID != null
                && firstFactionID != secondFactionID;
        }

        /// <summary>
        /// Selects one of the six single-player contexts using the campaign's saved launch faction.
        /// </summary>
        /// <param name="summary">The saved campaign setup.</param>
        /// <param name="firstFactionID">The faction bound to tactical index zero.</param>
        /// <param name="secondFactionID">The faction bound to tactical index one.</param>
        /// <returns>The exact setup context, or null for an unsupported faction or difficulty.</returns>
        private static SpaceCombatSupportContext? ResolveSinglePlayerContext(
            GameSummary summary,
            string firstFactionID,
            string secondFactionID
        )
        {
            if (summary.PlayerFactionID == firstFactionID)
                return summary.Difficulty switch
                {
                    GameDifficulty.Easy => SpaceCombatSupportContext.FirstFactionEasy,
                    GameDifficulty.Medium => SpaceCombatSupportContext.FirstFactionMedium,
                    GameDifficulty.Hard => SpaceCombatSupportContext.FirstFactionHard,
                    _ => null,
                };
            if (summary.PlayerFactionID == secondFactionID)
                return summary.Difficulty switch
                {
                    GameDifficulty.Easy => SpaceCombatSupportContext.SecondFactionEasy,
                    GameDifficulty.Medium => SpaceCombatSupportContext.SecondFactionMedium,
                    GameDifficulty.Hard => SpaceCombatSupportContext.SecondFactionHard,
                    _ => null,
                };
            return null;
        }
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
