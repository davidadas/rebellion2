using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerLobbyPasswordTests
    {
        [Test]
        public void CreateProof_DifferentLobby_ProducesDifferentProof()
        {
            Assert.That(
                MultiplayerLobbyPassword.CreateProof("first", "secret"),
                Is.Not.EqualTo(MultiplayerLobbyPassword.CreateProof("second", "secret"))
            );
        }

        [Test]
        public void CreateProof_IncorrectPassword_DoesNotMatch()
        {
            Assert.That(
                MultiplayerLobbyPassword.CreateProof("room", "secret"),
                Is.Not.EqualTo(MultiplayerLobbyPassword.CreateProof("room", "wrong"))
            );
        }

        [Test]
        public void CreateProof_EmptyPassword_ReturnsEmptyProof()
        {
            Assert.That(MultiplayerLobbyPassword.CreateProof("room", null), Is.Empty);
        }
    }
}
