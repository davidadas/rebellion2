using System.Linq;
using NUnit.Framework;
using Rebellion.Multiplayer;
using Rebellion.UI.SceneUI.MainMenu.Multiplayer;

namespace Rebellion.Tests.UI.SceneUI.MainMenu.Multiplayer
{
    public sealed class MultiplayerGameBrowserTests
    {
        [TestCase((int)MultiplayerGameColumn.Game, false, "a")]
        [TestCase((int)MultiplayerGameColumn.Game, true, "b")]
        [TestCase((int)MultiplayerGameColumn.Content, false, "b")]
        [TestCase((int)MultiplayerGameColumn.Players, false, "a")]
        [TestCase((int)MultiplayerGameColumn.Players, true, "b")]
        [TestCase((int)MultiplayerGameColumn.Access, false, "a")]
        [TestCase((int)MultiplayerGameColumn.Access, true, "b")]
        public void Sort_SelectedColumnAndDirection_OrdersDisplayedValues(
            int column,
            bool descending,
            string first
        )
        {
            MultiplayerGameListing[] games =
            {
                new()
                {
                    id = "b",
                    name = "Zulu",
                    packName = "Alpha pack",
                    currentPlayers = 2,
                    passwordProtected = true,
                },
                new()
                {
                    id = "a",
                    name = "alpha",
                    packName = "Zulu pack",
                    currentPlayers = 1,
                    passwordProtected = false,
                },
            };
            Assert.That(
                MultiplayerGameBrowser
                    .Sort(games, (MultiplayerGameColumn)column, descending)
                    .First()
                    .id,
                Is.EqualTo(first)
            );
        }

        [Test]
        public void Sort_EqualValues_KeepsStableIdentityOrder()
        {
            MultiplayerGameListing[] games =
            {
                new() { id = "b", name = "Same" },
                new() { id = "a", name = "Same" },
            };
            Assert.That(
                MultiplayerGameBrowser
                    .Sort(games, MultiplayerGameColumn.Game, false)
                    .Select(game => game.id),
                Is.EqualTo(new[] { "a", "b" })
            );
        }
    }
}
