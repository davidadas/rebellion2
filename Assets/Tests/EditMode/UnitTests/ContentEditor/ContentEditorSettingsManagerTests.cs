using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorSettingsManagerTests
    {
        private string folder;
        private string path;

        /// <summary>
        /// Creates a fixture-owned application profile.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-preferences-" + Guid.NewGuid().ToString("N")
            );
            path = Path.Combine(folder, "settings.json");
        }

        /// <summary>
        /// Removes the isolated settings profile.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void Load_MissingFile_ReturnsDefaultsWithoutCreatingFiles()
        {
            ContentEditorSettings settings = new ContentEditorSettingsManager(path).Load();
            Assert.AreEqual(1, settings.Zoom);
            Assert.AreEqual(ContentEditorColorMode.Dark, settings.ColorMode);
            Assert.IsFalse(Directory.Exists(folder));
        }

        [TestCase(ContentEditorColorMode.Dark)]
        [TestCase(ContentEditorColorMode.Light)]
        [TestCase(ContentEditorColorMode.Empire)]
        [TestCase(ContentEditorColorMode.Rebels)]
        [TestCase(ContentEditorColorMode.Dagobah)]
        public void Load_AfterApplicationRestart_RetainsThemeLayoutAndAcknowledgement(
            ContentEditorColorMode mode
        )
        {
            ContentEditorSettingsManager manager = new(path);
            manager.SaveColorMode(mode);
            manager.SaveLayout(0.75f, 0.3f);
            manager.AcknowledgeAlphaNotice();

            ContentEditorSettings settings = new ContentEditorSettingsManager(path).Load();
            Assert.AreEqual(mode, settings.ColorMode);
            Assert.AreEqual(0.75f, settings.Zoom);
            Assert.AreEqual(0.3f, settings.Split);
            Assert.IsTrue(settings.AlphaNoticeAcknowledged);
        }

        [Test]
        public void SaveColorMode_AnotherWindowSavedItsLayout_PreservesThatLayout()
        {
            ContentEditorSettingsManager first = new(path);
            ContentEditorSettingsManager second = new(path);
            second.Load();
            first.SaveLayout(0.8f, 0.4f);
            second.SaveColorMode(ContentEditorColorMode.Light);
            Assert.AreEqual(0.8f, first.Load().Zoom);
            Assert.AreEqual(0.4f, first.Load().Split);
        }

        [Test]
        public async Task SaveColorMode_ConcurrentLayoutUpdates_PreservesBothChangesAsync()
        {
            ContentEditorSettingsManager first = new(path);
            ContentEditorSettingsManager second = new(path);
            using Barrier start = new(2);
            for (int round = 0; round < 24; round++)
            {
                ContentEditorColorMode mode =
                    round % 2 == 0 ? ContentEditorColorMode.Light : ContentEditorColorMode.Dark;
                float zoom = round % 2 == 0 ? 0.8f : 1.2f;
                float split = round % 2 == 0 ? 0.3f : 0.4f;
                await Task.WhenAll(
                    Task.Run(() =>
                    {
                        start.SignalAndWait();
                        first.SaveLayout(zoom, split);
                    }),
                    Task.Run(() =>
                    {
                        start.SignalAndWait();
                        second.SaveColorMode(mode);
                    })
                );
                ContentEditorSettings settings = first.Load();
                Assert.AreEqual(mode, settings.ColorMode);
                Assert.AreEqual(zoom, settings.Zoom);
                Assert.AreEqual(split, settings.Split);
            }
        }

        [Test]
        public void Load_SeparateQaProfile_DoesNotChangeUserPreferences()
        {
            ContentEditorSettingsManager user = new(path);
            user.SaveColorMode(ContentEditorColorMode.Light);
            ContentEditorSettingsManager qa = new(Path.Combine(folder, "qa", "settings.json"));
            qa.SaveColorMode(ContentEditorColorMode.Dark);
            Assert.AreEqual(ContentEditorColorMode.Light, user.Load().ColorMode);
        }

        [Test]
        public void Load_EarlierPreferences_UsesThemUntilTheProfileHasSavedSettings()
        {
            ContentEditorSettingsManager manager = new(
                path,
                new ContentEditorSettings
                {
                    ColorMode = ContentEditorColorMode.Light,
                    AlphaNoticeAcknowledged = true,
                }
            );
            manager.SaveLayout(0.9f, 0.25f);
            ContentEditorSettings settings = new ContentEditorSettingsManager(path).Load();
            Assert.AreEqual(ContentEditorColorMode.Light, settings.ColorMode);
            Assert.IsTrue(settings.AlphaNoticeAcknowledged);
        }
    }
}
