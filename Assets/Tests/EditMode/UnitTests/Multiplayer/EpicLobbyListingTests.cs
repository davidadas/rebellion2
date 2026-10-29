using System;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class EpicLobbyListingTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ValidateForPublication_MissingRoomName_RejectsBeforeUpdatingServiceMetadata(
            string name
        )
        {
            Assert.Throws<ArgumentException>(() =>
                EpicLobbyListing.ValidateForPublication(new MatchmakerGame { name = name })
            );
        }

        [Test]
        public void IsJoinable_WaitingGameWithSlot_ReturnsTrue()
        {
            MatchmakerGame game = new MatchmakerGame
            {
                transport = EpicLobbyListing.Transport,
                state = "waiting",
                currentPlayers = 1,
                maxPlayers = 2,
            };
            Assert.That(EpicLobbyListing.IsJoinable(game), Is.True);
        }

        [Test]
        public void IsJoinable_StartedGameLosesPlayer_RemainsClosedToNewJoins()
        {
            MatchmakerGame game = new MatchmakerGame
            {
                transport = EpicLobbyListing.Transport,
                state = "in-progress",
                currentPlayers = 1,
                maxPlayers = 2,
            };
            Assert.That(EpicLobbyListing.IsJoinable(game), Is.False);
        }

        [Test]
        public void IsJoinable_FullWaitingGame_ReturnsFalse()
        {
            MatchmakerGame game = new MatchmakerGame
            {
                transport = EpicLobbyListing.Transport,
                state = "waiting",
                currentPlayers = 2,
                maxPlayers = 2,
            };
            Assert.That(EpicLobbyListing.IsJoinable(game), Is.False);
        }

        [Test]
        public void HashContent_KnownIdentity_ReturnsSha256()
        {
            Assert.That(
                EpicLobbyListing.HashContent("abc"),
                Is.EqualTo("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")
            );
        }

        [Test]
        public void HashContent_LongModIdentity_PreservesDifferencesAfterAttributeLimit()
        {
            string prefix = new('x', 2000);
            Assert.That(
                EpicLobbyListing.HashContent(prefix + "a"),
                Is.Not.EqualTo(EpicLobbyListing.HashContent(prefix + "b"))
            );
        }
    }
}
