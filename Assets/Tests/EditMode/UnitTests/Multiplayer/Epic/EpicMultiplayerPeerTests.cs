using System;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class EpicMultiplayerPeerTests
    {
        [Test]
        public void CreateSocketName_SameLobby_ProducesSameSocket()
        {
            Assert.That(
                EpicMultiplayerPeer.CreateSocketName("lobby-123"),
                Is.EqualTo(EpicMultiplayerPeer.CreateSocketName("lobby-123"))
            );
        }

        [Test]
        public void CreateSocketName_DifferentLobbies_ProducesDifferentSockets()
        {
            Assert.That(
                EpicMultiplayerPeer.CreateSocketName("lobby-123"),
                Is.Not.EqualTo(EpicMultiplayerPeer.CreateSocketName("lobby-456"))
            );
        }

        [Test]
        public void CreateSocketName_ArbitraryLobbyIdentifier_ProducesValidBoundedName()
        {
            string name = EpicMultiplayerPeer.CreateSocketName(
                "lobby:with/slashes-and-unicode-\u2603"
            );
            Assert.That(name, Does.Match("^[A-Za-z0-9]{32}$"));
        }

        [Test]
        public void CreateSocketName_EmptyLobby_RejectsIdentifier()
        {
            Assert.Throws<ArgumentException>(() =>
                EpicMultiplayerPeer.CreateSocketName(string.Empty)
            );
        }
    }
}
