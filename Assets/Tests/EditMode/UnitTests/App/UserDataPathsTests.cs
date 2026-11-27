using System;
using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class UserDataPathsTests
    {
        [TestCase(UnityEngine.RuntimePlatform.WindowsPlayer)]
        [TestCase(UnityEngine.RuntimePlatform.LinuxPlayer)]
        public void ResolveProductProfile_CompanionApplication_UsesGameSibling(
            UnityEngine.RuntimePlatform platform
        )
        {
            string company = Path.Combine(Path.GetTempPath(), "Company");
            Assert.That(
                UserDataPaths.ResolveProductProfile(
                    Path.Combine(company, "Toolkit"),
                    platform,
                    "Company",
                    "Toolkit",
                    "Game"
                ),
                Is.EqualTo(Path.Combine(company, "Game"))
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolveProductProfile_MacPlayer_UsesExistingGameEditorProfileWhenPresent(
            bool editorExists
        )
        {
            string support = Path.Combine(
                Path.GetTempPath(),
                "profile-" + Guid.NewGuid().ToString("N")
            );
            string editor = Path.Combine(support, "Company", "Game");
            try
            {
                if (editorExists)
                    Directory.CreateDirectory(editor);
                foreach (
                    string current in new[]
                    {
                        Path.Combine(support, "unity.Company.Toolkit"),
                        Path.Combine(support, "Company", "Toolkit"),
                    }
                )
                    Assert.That(
                        UserDataPaths.ResolveProductProfile(
                            current,
                            UnityEngine.RuntimePlatform.OSXPlayer,
                            "Company",
                            "Toolkit",
                            "Game"
                        ),
                        Is.EqualTo(
                            editorExists ? editor : Path.Combine(support, "unity.Company.Game")
                        )
                    );
            }
            finally
            {
                if (Directory.Exists(support))
                    Directory.Delete(support, true);
            }
        }

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
