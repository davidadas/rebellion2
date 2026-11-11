using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Multiplayer;

namespace Rebellion.UI.SceneUI.MainMenu.Multiplayer
{
    /// <summary>
    /// Identifies a sortable discovery column.
    /// </summary>
    internal enum MultiplayerGameColumn
    {
        Game,
        Content,
        Players,
        Access,
    }

    /// <summary>
    /// Orders discovery results by their displayed values with stable game-identity ties.
    /// </summary>
    internal static class MultiplayerGameBrowser
    {
        /// <summary>
        /// Sorts the displayed games using the selected column and direction.
        /// </summary>
        /// <param name="games">The filtered discovery results.</param>
        /// <param name="column">The selected header.</param>
        /// <param name="descending">Whether higher values appear first.</param>
        /// <returns>The ordered results.</returns>
        public static IEnumerable<MultiplayerGameListing> Sort(
            IEnumerable<MultiplayerGameListing> games,
            MultiplayerGameColumn column,
            bool descending
        )
        {
            IComparer<MultiplayerGameListing> comparer = Comparer<MultiplayerGameListing>.Create(
                (left, right) =>
                {
                    int comparison = column switch
                    {
                        MultiplayerGameColumn.Content => StringComparer.OrdinalIgnoreCase.Compare(
                            left.packName ?? left.packID,
                            right.packName ?? right.packID
                        ),
                        MultiplayerGameColumn.Players => left.currentPlayers.CompareTo(
                            right.currentPlayers
                        ),
                        MultiplayerGameColumn.Access => left.passwordProtected.CompareTo(
                            right.passwordProtected
                        ),
                        _ => StringComparer.OrdinalIgnoreCase.Compare(left.name, right.name),
                    };
                    return descending ? -comparison : comparison;
                }
            );
            return games
                .OrderBy(game => game, comparer)
                .ThenBy(game => game.id, StringComparer.Ordinal);
        }
    }
}
