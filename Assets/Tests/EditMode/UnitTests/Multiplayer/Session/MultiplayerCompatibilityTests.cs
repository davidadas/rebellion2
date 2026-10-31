using System.IO;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerCompatibilityTests
    {
        [Test]
        public void ValidateBuild_CompiledIdentity_AdmitsSameBuild()
        {
            string identity = MultiplayerCompatibility.SimulationBuild;

            Assert.AreEqual(64, identity.Length);
            Assert.DoesNotThrow(() => MultiplayerCompatibility.ValidateBuild(identity));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("different-build")]
        public void ValidateBuild_MissingOrDifferentIdentity_RejectsPeer(string identity)
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerCompatibility.ValidateBuild(identity)
            );
        }

        [Test]
        public void ValidateContent_MissingIdentity_RejectsUnverifiedContent()
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerCompatibility.ValidateContent(null, null)
            );
        }
    }
}
