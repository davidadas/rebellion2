using System;
using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class UserDataPathsTests
    {
        [Test]
        public void Resolve_NormalLaunch_KeepsThePlatformUserDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "normal-profile");
            Assert.AreEqual(
                Path.GetFullPath(root),
                UserDataPaths.Resolve(new[] { "game", "-screen-width", "1280" }, root)
            );
        }

        [Test]
        public void Resolve_ExplicitProfile_UsesThatDirectoryWithoutChangingTheDefault()
        {
            string root = Path.Combine(Path.GetTempPath(), "normal-profile");
            string custom = Path.Combine(Path.GetTempPath(), "separate test profile");
            Assert.AreEqual(
                custom,
                UserDataPaths.Resolve(new[] { "game", "-userDataPath", custom }, root)
            );
            Assert.AreEqual(root, UserDataPaths.Resolve(Array.Empty<string>(), root));
        }

        [TestCase("-userDataPath")]
        [TestCase("-userDataPath -newGame")]
        [TestCase("-userDataPath one -userDataPath two")]
        public void Resolve_InvalidProfileArguments_RejectsThem(string arguments)
        {
            Assert.Throws<ArgumentException>(() =>
                UserDataPaths.Resolve(arguments.Split(' '), Path.GetTempPath())
            );
        }
    }
}
