using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuDataBuilderTests
    {
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly Dictionary<string, int> _loadCounts = new Dictionary<string, int>();

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in _textures)
                UnityEngine.Object.DestroyImmediate(texture);

            _textures.Clear();
            _loadCounts.Clear();
        }

        [Test]
        public void Constructor_NullThemeLibrary_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuDataBuilder(
                    null,
                    SaveGameManager.Instance,
                    LoadTexture,
                    _ => true,
                    "Version"
                )
            );
        }

        [Test]
        public void Constructor_NullSaveGameManager_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuDataBuilder(
                    TestContent.CreateThemeLibrary(),
                    null,
                    LoadTexture,
                    _ => true,
                    "Version"
                )
            );
        }

        [Test]
        public void Constructor_NullTextureLoader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuDataBuilder(
                    TestContent.CreateThemeLibrary(),
                    SaveGameManager.Instance,
                    null,
                    _ => true,
                    "Version"
                )
            );
        }

        [Test]
        public void Constructor_NullContentCompatibilityCheck_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuDataBuilder(
                    TestContent.CreateThemeLibrary(),
                    SaveGameManager.Instance,
                    LoadTexture,
                    null,
                    "Version"
                )
            );
        }

        [Test]
        public void CreateRenderData_MenuState_ProjectsWindowAndSlots()
        {
            SaveMenuDataBuilder builder = CreateBuilder("Version 1");
            IReadOnlyDictionary<UserTacticalOption, bool> options = CreateTacticalOptions();

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                true,
                0.25f,
                0.75f,
                options,
                "Confirm",
                Array.Empty<SaveGameEntry>()
            );

            Assert.AreEqual(0.25f, data.MusicVolume);
            Assert.AreEqual(0.75f, data.SfxVolume);
            Assert.AreEqual("Version 1", data.VersionText);
            Assert.AreEqual(options.Count, data.TacticalOptions.Count);
            Assert.AreEqual(SaveGameManager.Instance.SaveSlotCount, data.Slots.Count);
            Assert.AreEqual("Confirm", data.ConfirmationMessage);
            for (int slot = 0; slot < data.Slots.Count; slot++)
            {
                Assert.AreEqual(slot, data.Slots[slot].Slot);
                Assert.IsTrue(data.Slots[slot].CanSave);
            }
        }

        [Test]
        public void CreateRenderData_SavingDisabled_DisablesEverySlot()
        {
            SaveMenuDataBuilder builder = CreateBuilder("Version");

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                false,
                0f,
                0f,
                CreateTacticalOptions(),
                null,
                Array.Empty<SaveGameEntry>()
            );

            Assert.IsTrue(data.Slots.All(slot => !slot.CanSave));
        }

        [Test]
        public void CreateRenderData_RepeatedPaths_LoadsEachTextureOnce()
        {
            SaveMenuDataBuilder builder = CreateBuilder("Version");
            SaveGameEntry firstSave = CreateSaveEntry(0, "First", "FNALL1");
            SaveGameEntry secondSave = CreateSaveEntry(1, "Second", "FNALL1");

            builder.CreateRenderData(
                true,
                0f,
                0f,
                CreateTacticalOptions(),
                null,
                new[] { firstSave, secondSave }
            );
            builder.CreateRenderData(
                true,
                0f,
                0f,
                CreateTacticalOptions(),
                null,
                new[] { firstSave, secondSave }
            );

            Assert.AreEqual(1, _loadCounts.Count);
            Assert.AreEqual(1, _loadCounts.Single().Value);
        }

        [Test]
        public void CreateRenderData_NullVersion_UsesEmptyVersionText()
        {
            SaveMenuDataBuilder builder = CreateBuilder(null);

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                true,
                0f,
                0f,
                CreateTacticalOptions(),
                null,
                Array.Empty<SaveGameEntry>()
            );

            Assert.AreEqual(string.Empty, data.VersionText);
        }

        [Test]
        public void CreateRenderData_SaveEntries_ProjectsMatchingSlots()
        {
            SaveMenuDataBuilder builder = CreateBuilder("Version");
            SaveGameEntry save = new SaveGameEntry(
                SaveGameManager.Instance.GetSaveSlotFileName(1),
                new GameMetadata
                {
                    SaveDisplayName = "Outer Rim",
                    PlayerFactionID = "FNALL1",
                }
            );

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                true,
                0f,
                0f,
                CreateTacticalOptions(),
                null,
                new[] { save }
            );

            Assert.IsFalse(data.Slots[0].CanLoad);
            Assert.IsTrue(data.Slots[1].CanLoad);
            Assert.AreEqual("Outer Rim", data.Slots[1].Label);
            Assert.IsNotNull(data.Slots[1].FactionIconTexture);
        }

        [Test]
        public void CreateRenderData_IncompatibleSave_DisablesLoadWithoutResolvingFactionTheme()
        {
            SaveMenuDataBuilder builder = new SaveMenuDataBuilder(
                TestContent.CreateThemeLibrary(),
                SaveGameManager.Instance,
                LoadTexture,
                _ => false,
                "Version"
            );
            SaveGameEntry save = new SaveGameEntry(
                SaveGameManager.Instance.GetSaveSlotFileName(1),
                new GameMetadata
                {
                    SaveDisplayName = "Other Pack",
                    PlayerFactionID = "unknown-faction",
                }
            );

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                true,
                0f,
                0f,
                CreateTacticalOptions(),
                null,
                new[] { save }
            );

            Assert.IsFalse(data.Slots[1].CanLoad);
            Assert.AreEqual("Other Pack", data.Slots[1].Label);
            Assert.IsNull(data.Slots[1].FactionIconTexture);
        }

        private SaveMenuDataBuilder CreateBuilder(string version)
        {
            return new SaveMenuDataBuilder(
                TestContent.CreateThemeLibrary(),
                SaveGameManager.Instance,
                LoadTexture,
                _ => true,
                version
            );
        }

        private static SaveGameEntry CreateSaveEntry(int slot, string displayName, string factionId)
        {
            return new SaveGameEntry(
                SaveGameManager.Instance.GetSaveSlotFileName(slot),
                new GameMetadata
                {
                    SaveDisplayName = displayName,
                    PlayerFactionID = factionId,
                }
            );
        }

        private Texture2D LoadTexture(string path)
        {
            _loadCounts.TryGetValue(path, out int count);
            _loadCounts[path] = count + 1;
            Texture2D texture = new Texture2D(4, 4) { name = path };
            _textures.Add(texture);
            return texture;
        }

        private static IReadOnlyDictionary<UserTacticalOption, bool> CreateTacticalOptions()
        {
            Dictionary<UserTacticalOption, bool> options =
                new Dictionary<UserTacticalOption, bool>();
            foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
                options.Add(option, (int)option % 2 == 0);

            return options;
        }
    }
}
