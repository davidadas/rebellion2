using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
                new SaveMenuDataBuilder(null, SaveGameManager.Instance, LoadTexture, "Version")
            );
        }

        [Test]
        public void Constructor_NullSaveGameManager_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuDataBuilder(new FactionThemeLibrary(), null, LoadTexture, "Version")
            );
        }

        [Test]
        public void Constructor_NullTextureLoader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuDataBuilder(
                    new FactionThemeLibrary(),
                    SaveGameManager.Instance,
                    null,
                    "Version"
                )
            );
        }

        [Test]
        public void CreateRenderData_PlayerTheme_ProjectsMenuAndSlotState()
        {
            SaveMenuDataBuilder builder = CreateBuilder("Version 1");
            IReadOnlyDictionary<UserTacticalOption, bool> options = CreateTacticalOptions();

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                "FNALL1",
                true,
                0.25f,
                0.75f,
                options,
                "Confirm"
            );

            Assert.IsNotNull(data.ReturnStrategyButtonUpTexture);
            Assert.IsNotNull(data.ReturnStrategyButtonDownTexture);
            Assert.AreNotSame(
                data.ReturnStrategyButtonUpTexture,
                data.ReturnStrategyButtonDownTexture
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
                "FNALL1",
                false,
                0f,
                0f,
                CreateTacticalOptions(),
                null
            );

            Assert.IsTrue(data.Slots.All(slot => !slot.CanSave));
        }

        [Test]
        public void CreateRenderData_RepeatedPaths_LoadsEachTextureOnce()
        {
            SaveMenuDataBuilder builder = CreateBuilder("Version");

            builder.CreateRenderData("FNALL1", true, 0f, 0f, CreateTacticalOptions(), null);
            builder.CreateRenderData("FNALL1", true, 0f, 0f, CreateTacticalOptions(), null);

            Assert.GreaterOrEqual(_loadCounts.Count, 2);
            Assert.IsTrue(_loadCounts.Values.All(count => count == 1));
        }

        [Test]
        public void CreateRenderData_NullVersion_UsesEmptyVersionText()
        {
            SaveMenuDataBuilder builder = CreateBuilder(null);

            SaveMenuWindowRenderData data = builder.CreateRenderData(
                "FNALL1",
                true,
                0f,
                0f,
                CreateTacticalOptions(),
                null
            );

            Assert.AreEqual(string.Empty, data.VersionText);
        }

        private SaveMenuDataBuilder CreateBuilder(string version)
        {
            return new SaveMenuDataBuilder(
                new FactionThemeLibrary(),
                SaveGameManager.Instance,
                LoadTexture,
                version
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
