using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Rebellion.Editor.Media;

namespace Rebellion.Tests.Editor.MediaSync
{
    [TestFixture]
    public sealed class MediaSyncServiceTests
    {
        private string _testRoot;

        [SetUp]
        public void SetUp()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-media-sync-tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(_testRoot))
                return;

            foreach (
                string file in Directory.EnumerateFiles(_testRoot, "*", SearchOption.AllDirectories)
            )
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_testRoot, true);
        }

        [Test]
        public void MirrorDirectory_ChangedTree_CopiesDeletesAndPreservesCurrentMetadata()
        {
            string source = Path.Combine(_testRoot, "source");
            string destination = Path.Combine(_testRoot, "destination");
            Directory.CreateDirectory(Path.Combine(source, "Nested"));
            Directory.CreateDirectory(Path.Combine(destination, "Nested"));
            File.WriteAllText(Path.Combine(source, "Nested", "current.png"), "current");
            File.WriteAllText(Path.Combine(source, "Nested", "current.png.meta"), "source-meta");
            File.WriteAllText(Path.Combine(destination, "Nested", "current.png"), "old");
            File.WriteAllText(
                Path.Combine(destination, "Nested", "current.png.meta"),
                "unity-meta"
            );
            File.WriteAllText(Path.Combine(destination, "stale.wav"), "stale");
            File.WriteAllText(Path.Combine(destination, "stale.wav.meta"), "stale-meta");

            MediaSyncResult result = MediaSyncService.MirrorDirectory(source, destination);

            Assert.AreEqual(
                "current",
                File.ReadAllText(Path.Combine(destination, "Nested", "current.png"))
            );
            Assert.AreEqual(
                "unity-meta",
                File.ReadAllText(Path.Combine(destination, "Nested", "current.png.meta"))
            );
            Assert.IsFalse(File.Exists(Path.Combine(destination, "stale.wav")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "stale.wav.meta")));
            Assert.AreEqual(1, result.CopiedFiles);
            Assert.AreEqual(2, result.DeletedFiles);
        }

        [Test]
        public void MirrorDirectory_MissingLfsContent_LeavesDestinationUnchanged()
        {
            string source = Path.Combine(_testRoot, "source");
            string destination = Path.Combine(_testRoot, "destination");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            File.WriteAllText(
                Path.Combine(source, "missing.png"),
                "version https://git-lfs.github.com/spec/v1\n"
                    + "oid sha256:0000000000000000000000000000000000000000000000000000000000000000\n"
                    + "size 1\n"
            );
            File.WriteAllText(Path.Combine(destination, "keep.txt"), "keep");

            Assert.Throws<InvalidDataException>(() =>
                MediaSyncService.MirrorDirectory(source, destination)
            );
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(destination, "keep.txt")));
        }

        [Test]
        public void RepositoryState_LocalBranches_SwitchesOnlyWhenClean()
        {
            string mediaRoot = Path.Combine(_testRoot, "rebellion2-media");
            string schemaPath = Path.Combine(
                mediaRoot,
                "Content",
                "Application",
                "Schemas",
                "game-config.xsd"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(schemaPath));
            Directory.CreateDirectory(Path.Combine(mediaRoot, "Models", "MainMenu"));
            File.WriteAllText(schemaPath, "schema");
            RunGit(mediaRoot, "init", "-b", "main");
            RunGit(mediaRoot, "config", "user.name", "Media Sync Tests");
            RunGit(mediaRoot, "config", "user.email", "media-sync@example.invalid");
            RunGit(mediaRoot, "add", ".");
            RunGit(mediaRoot, "commit", "-m", "Initial media");
            RunGit(mediaRoot, "branch", "feature-media");

            MediaRepositoryInfo repository = MediaSyncService.InspectRepository(mediaRoot);
            CollectionAssert.Contains(repository.Branches, "feature-media");
            Assert.AreEqual("main", repository.CurrentBranch);
            Assert.IsFalse(repository.IsDirty);

            MediaSyncService.SwitchBranch(repository, "feature-media");
            repository = MediaSyncService.InspectRepository(mediaRoot);
            Assert.AreEqual("feature-media", repository.CurrentBranch);

            File.AppendAllText(schemaPath, "dirty");
            repository = MediaSyncService.InspectRepository(mediaRoot);
            Assert.IsTrue(repository.IsDirty);
            Assert.Throws<InvalidOperationException>(() =>
                MediaSyncService.SwitchBranch(repository, "main")
            );
        }

        private static void RunGit(string workingDirectory, params string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using Process process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, output + error);
        }
    }
}
