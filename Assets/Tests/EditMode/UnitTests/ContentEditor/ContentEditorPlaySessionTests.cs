using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorPlaySessionTests
    {
        private string folder;
        private ContentPackEditorProject project;

        /// <summary>
        /// Creates an independent authored project over installed base content.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            string root = ContentPackLoader.ResolveContentRootPath();
            ContentPackDefinition baseline = ContentPackLoader
                .DiscoverPacks(root)
                .First(pack => string.IsNullOrEmpty(pack.BasePackID));
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-play-project-" + Guid.NewGuid().ToString("N")
            );
            project = ContentPackEditorStorage.CreateInFolder(
                root,
                baseline.ID,
                folder,
                "play-test",
                "Play test",
                "1.0.0"
            );
        }

        /// <summary>
        /// Removes only the fixture-owned source project.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(folder, true);

        [Test]
        public async Task PrepareAsync_UnsavedChanges_UsesTheSnapshotWithoutSavingTheProjectAsync()
        {
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Officers")
                .Records.First();
            project.SetField(officer.GetField("DisplayName"), "Unsaved test face");
            string faction = project.GetFactionOptions().Keys.First();
            using ContentEditorPlaySession session = await ContentEditorPlaySession.PrepareAsync(
                project,
                new ContentEditorPlayOptions(
                    project.Definition.DefaultScenarioID,
                    faction,
                    GameSize.Large,
                    GameDifficulty.Medium
                )
            );
            NewGameLaunchRequest request = NewGameLaunchRequest.Read(
                new[] { "-newGame", session.RequestPath }
            );
            ContentPack pack = request.OpenContent(project.ContentRootPath);
            Assert.IsTrue(
                pack.GameData.Officers.Any(item => item.DisplayName == "Unsaved test face")
            );
            Assert.IsTrue(project.IsDirty);
            Assert.IsFalse(File.Exists(officer.Catalog.Document.TargetPath));
            Assert.AreEqual(session.PackPath, pack.PackRootPath);
            Assert.AreEqual(faction, request.Summary.PlayerFactionID);
            Assert.AreEqual(GameSize.Large, request.Summary.GalaxySize);
            Assert.AreEqual(GameDifficulty.Medium, request.Summary.Difficulty);
            Assert.AreEqual(GameResourceAvailability.Normal, request.Summary.ResourceAvailability);
            Assert.AreEqual(
                session.ProfilePath,
                UserDataPaths.Resolve(new[] { "-userDataPath", session.ProfilePath }, "unused")
            );
        }

        [Test]
        public async Task Dispose_PreparedSession_RemovesOnlyItsSnapshotAsync()
        {
            string faction = project.GetFactionOptions().Keys.First();
            ContentEditorPlaySession session = await ContentEditorPlaySession.PrepareAsync(
                project,
                new ContentEditorPlayOptions(
                    project.Definition.DefaultScenarioID,
                    faction,
                    GameSize.Small,
                    GameDifficulty.Easy
                )
            );
            string snapshot = session.DirectoryPath;
            Assert.IsTrue(Directory.Exists(snapshot));
            session.Dispose();
            session.Dispose();
            Assert.IsFalse(Directory.Exists(snapshot));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "pack.xml")));
        }

        [Test]
        public void PrepareAsync_CanceledRequest_LeavesTheAuthoredProjectUnchanged()
        {
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await ContentEditorPlaySession.PrepareAsync(
                    project,
                    new ContentEditorPlayOptions(
                        project.Definition.DefaultScenarioID,
                        project.GetFactionOptions().Keys.First(),
                        GameSize.Large,
                        GameDifficulty.Medium
                    ),
                    new CancellationToken(true)
                )
            );
            Assert.IsFalse(project.IsDirty);
            Assert.AreEqual(1, Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public async Task PrepareAsync_UnknownFaction_RejectsTheTestBeforeLaunchingAsync()
        {
            Exception failure = null;
            try
            {
                await ContentEditorPlaySession.PrepareAsync(
                    project,
                    new ContentEditorPlayOptions(
                        project.Definition.DefaultScenarioID,
                        "missing-faction",
                        GameSize.Large,
                        GameDifficulty.Medium
                    )
                );
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            Assert.IsInstanceOf<ArgumentException>(failure);
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase(RuntimePlatform.WindowsPlayer, "rebellion2.exe")]
        [TestCase(RuntimePlatform.LinuxPlayer, "rebellion2.x86_64")]
        [TestCase(RuntimePlatform.WindowsPlayer, "Custom game.exe")]
        [TestCase(RuntimePlatform.LinuxPlayer, "Rebellion2.x86_64")]
        public async Task CreateStartInfo_DesktopLayout_PassesSeparateArgumentsAndAnIsolatedProfileAsync(
            RuntimePlatform platform,
            string executable
        )
        {
            string install = Path.Combine(folder, "installation with spaces");
            Directory.CreateDirectory(install);
            File.WriteAllText(Path.Combine(install, executable), "fixture");
            string managed = Path.Combine(
                install,
                Path.GetFileNameWithoutExtension(executable) + "_Data",
                "Managed"
            );
            Directory.CreateDirectory(managed);
            File.WriteAllText(Path.Combine(managed, "GameAssembly.dll"), "fixture");
            string editorManaged = Path.Combine(install, "rebellion2-editor_Data", "Managed");
            Directory.CreateDirectory(editorManaged);
            File.WriteAllText(Path.Combine(editorManaged, "GameAssembly.dll"), "fixture");
            File.WriteAllText(
                Path.Combine(install, "rebellion2-editor" + Path.GetExtension(executable)),
                "fixture"
            );
            File.WriteAllText(
                Path.Combine(install, "uninstaller" + Path.GetExtension(executable)),
                "fixture"
            );
            using ContentEditorPlaySession session = await ContentEditorPlaySession.PrepareAsync(
                project,
                new ContentEditorPlayOptions(
                    project.Definition.DefaultScenarioID,
                    project.GetFactionOptions().Keys.First(),
                    GameSize.Small,
                    GameDifficulty.Easy
                )
            );
            var info = session.CreateStartInfo(
                Path.Combine(install, "rebellion2-editor_Data"),
                platform
            );
            Assert.AreEqual(Path.Combine(install, executable), info.FileName);
            Assert.IsFalse(info.UseShellExecute);
            CollectionAssert.Contains(info.ArgumentList, session.RequestPath);
            CollectionAssert.Contains(info.ArgumentList, session.ProfilePath);
            CollectionAssert.Contains(info.ArgumentList, project.ContentRootPath);
        }
    }
}
