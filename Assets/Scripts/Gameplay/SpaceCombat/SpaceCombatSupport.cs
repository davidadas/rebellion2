using System.Linq;
using Rebellion.Content;
using Rebellion.Game;

namespace Rebellion.Gameplay
{
    internal static class SpaceCombatSupport
    {
        /// <summary>
        /// Resolves a complete authored profile from saved launch settings or multiplayer identity.
        /// Missing or ambiguous faction mappings, campaign factions, contexts, or values fail without
        /// selecting another profile.
        /// </summary>
        /// <param name="config">The authored battle support configuration.</param>
        /// <param name="game">The campaign supplying persisted setup and active faction identity.</param>
        /// <param name="tacticalCombat">The shared authored faction-index map.</param>
        /// <param name="profile">The selected complete profile, or null when resolution fails.</param>
        /// <returns>True when the exact saved context has a complete profile.</returns>
        internal static bool TryResolveProfile(
            this SpaceCombatSupportConfig config,
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
                || config?.Profiles == null
                || !config.Profiles.TryGetValue(
                    context.Value,
                    out SpaceCombatSupportProfile selected
                )
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
        internal static bool TryResolveFactionIDs(
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
}
