using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class ContentEditorModManagerControllerTests : ContentEditorViewTestBase
    {
        private ContentEditorModManagerController controller;
        private string settingsPath;
        private string previousSettings;
        private const string _eraId = "editor-fixture";
        private const string _duplicateDefinition =
            "<ContentPack><ID>duplicate</ID><DisplayName>Duplicate</DisplayName><Version>0.9.0</Version></ContentPack>";

        /// <summary>
        /// Releases the controller and restores settings before the shared view fixture is removed.
        /// </summary>
        [TearDown]
        public void RestoreLibrary()
        {
            controller?.Dispose();
            if (settingsPath == null)
                return;
            if (previousSettings == null)
                File.Delete(settingsPath);
            else
                File.WriteAllText(settingsPath, previousSettings);
        }

        [UnityTest]
        public IEnumerator Activate_InvalidSavedSettings_LeavesDocumentUntouched()
        {
            yield return PrepareLibrary();
            const string invalid = "{broken";
            File.WriteAllText(settingsPath, invalid);

            Toggle("Parent");

            Assert.That(File.ReadAllText(settingsPath), Is.EqualTo(invalid));
        }

        [UnityTest]
        public IEnumerator Activate_EnablingDependent_EnablesRequiredAddOn()
        {
            yield return PrepareLibrary();
            Toggle("Parent");

            Assert.That(
                UserSettingsManager.ReadContentSelection().GetModLoadOrder(_eraId),
                Is.EqualTo(new[] { "Dependency", "Parent" })
            );
        }

        [UnityTest]
        public IEnumerator Activate_EnabledDependent_RejectsDisablingItsDependency()
        {
            yield return PrepareLibrary();
            Toggle("Parent");

            Toggle("Dependency");

            string[] disabled = UserSettingsManager.ReadContentSelection().DisabledModIDs;
            CollectionAssert.DoesNotContain(disabled, "Dependency");
        }

        [UnityTest]
        public IEnumerator Activate_DependentDisabled_AllowsDisablingItsDependency()
        {
            yield return PrepareLibrary();
            Toggle("Parent");
            Toggle("Parent");

            Toggle("Dependency");

            Assert.That(
                UserSettingsManager.ReadContentSelection().GetModLoadOrder(_eraId),
                Is.Empty
            );
        }

        [UnityTest]
        public IEnumerator Show_LibraryContainsModsAndEras_PlacesEraSectionSecond()
        {
            yield return PrepareLibrary();

            TMP_Text[] labels = view.ModManager.GetComponentsInChildren<TMP_Text>();
            TMP_Text mods = labels.Single(label => label.name == "ModsHeading");
            TMP_Text eras = labels.Single(label => label.name == "ErasHeading");
            var cards = view.ModManager.GetComponentsInChildren<ContentEditorModCardView>();

            Assert.That(
                eras.rectTransform.anchoredPosition.y,
                Is.LessThan(mods.rectTransform.anchoredPosition.y)
            );
            CollectionAssert.AreEqual(
                new[] { false, false, true },
                cards.Select(card => card.Entry.IsEra)
            );
        }

        [UnityTest]
        public IEnumerator InstallLatest_BrowseCard_InstallsWithoutOpeningDetails()
        {
            string root = Path.Combine(folder, "inline-install", "Content");
            TestPackFixture.Install(root);
            ModPackageStore store = new(root, new ThunderstoreClient());
            view.ClearProject();
            view.SetWorkspaceNavigation(ContentEditorPage.Browse);
            yield return OpenCachedBrowse(
                root,
                store,
                () => view.Initialize(ContentPackLoader.DiscoverPacks(root), root)
            );
            ContentEditorModCardView card = view
                .ModManager.GetComponentsInChildren<ContentEditorModCardView>()
                .Single(item => item.Entry?.ID == "Fixture-Duplicate");
            Button install = card.GetComponentsInChildren<Button>()
                .Single(button => button.name == "PrimaryAction");
            var details = view
                .ModManager.GetComponentsInChildren<UnityEngine.Transform>(true)
                .Single(item => item.name == "PackageDetailsOverlay")
                .gameObject;

            install.onClick.Invoke();
            bool openedDetails = details.activeSelf;
            while (controller.IsBusy)
                yield return null;

            Assert.That(openedDetails, Is.False);
            Assert.That(view.ModManager.IsOpen, Is.True);
            Assert.That(store.GetInstalled().Single().PackageID, Is.EqualTo("Fixture-Duplicate"));
        }

        [UnityTest]
        public IEnumerator Install_ExistingPackCopy_RequiresExplicitDeletion()
        {
            string root = Path.Combine(folder, "duplicate-library", "Content");
            TestPackFixture.Install(root);
            string existing = Path.Combine(root, "Packs", "ExistingFolder");
            Directory.CreateDirectory(existing);
            File.WriteAllText(Path.Combine(existing, "pack.xml"), _duplicateDefinition);
            ModPackageStore store = new(root, new ThunderstoreClient());
            yield return OpenCachedBrowse(root, store);
            ContentEditorModCardView card = view
                .ModManager.GetComponentsInChildren<ContentEditorModCardView>()
                .Single(item => item.Entry?.ID == "Fixture-Duplicate");
            card.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
            Button install = view
                .ModManager.GetComponentsInChildren<Button>()
                .Single(button => button.name == "installButton");
            ActionDialogView dialog = view.GetComponentsInChildren<ActionDialogView>(true)
                .Single(item => item.name == "Confirmation");

            install.onClick.Invoke();
            while (controller.IsBusy)
                yield return null;

            Assert.That(dialog.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                dialog.GetComponentsInChildren<TMP_Text>().Select(label => label.text),
                Does.Contain("Delete existing copy")
            );
            Assert.That(dialog.TryCancel(), Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(existing, "pack.xml")),
                Is.EqualTo(_duplicateDefinition)
            );
        }

        /// <summary>
        /// Opens Browse with an isolated cached listing.
        /// </summary>
        /// <param name="root">The fixture content root.</param>
        /// <param name="store">The fixture package store.</param>
        /// <param name="contentChanged">The optional installation callback.</param>
        /// <returns>The asynchronous fixture setup.</returns>
        private IEnumerator OpenCachedBrowse(
            string root,
            ModPackageStore store,
            Action contentChanged = null
        )
        {
            CacheDuplicateRelease(store);
            controller = new ContentEditorModManagerController(
                view.ModManager,
                root,
                () => Array.Empty<string>(),
                view.ConfirmModRemoval,
                contentChanged ?? (() => { }),
                store
            );
            controller.Show(showInstalled: false);
            controller.Cancel();
            while (controller.IsBusy)
                yield return null;
        }

        /// <summary>
        /// Writes a cached release that conflicts with the local fixture.
        /// </summary>
        /// <param name="store">The fixture package store.</param>
        private static void CacheDuplicateRelease(ModPackageStore store)
        {
            string state = Path.GetDirectoryName(store.Catalog.CachePath);
            string archives = Path.Combine(state, "archives");
            Directory.CreateDirectory(archives);
            using SHA256 hash = SHA256.Create();
            string key = BitConverter
                .ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("Fixture-Duplicate\0" + "1.0.0")))
                .Replace("-", string.Empty);
            string path = Path.Combine(archives, key + ".zip");
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var manifest = new StreamWriter(archive.CreateEntry("manifest.json").Open()))
                    manifest.Write("{\"name\":\"Duplicate\",\"version_number\":\"1.0.0\"}");
                using (var definition = new StreamWriter(archive.CreateEntry("pack.xml").Open()))
                    definition.Write(_duplicateDefinition.Replace("0.9.0", "1.0.0"));
            }
            File.WriteAllText(
                store.Catalog.CachePath,
                "[{\"name\":\"Duplicate\",\"owner\":\"Fixture\",\"full_name\":\"Fixture-Duplicate\",\"versions\":[{"
                    + "\"version_number\":\"1.0.0\",\"download_url\":\"https://thunderstore.io/fixture.zip\",\"file_size\":"
                    + new FileInfo(path).Length
                    + "}]}]"
            );
        }

        /// <summary>
        /// Installs a dependent add-on pair in an isolated content root and opens its library.
        /// </summary>
        /// <returns>The asynchronous fixture preparation.</returns>
        private IEnumerator PrepareLibrary()
        {
            settingsPath = Path.Combine(UserDataPaths.GameRoot, "user-settings.json");
            previousSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            string root = Path.Combine(folder, "library", "Content");
            ContentPack fixture = TestPackFixture.Install(root);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath, "{\"Content\":{\"HasModLoadOrderPreferences\":true}}");
            ModPackageStore store = new(root, new ThunderstoreClient());
            Task install = InstallAddOnsAsync(store, fixture.Definition);
            while (!install.IsCompleted)
                yield return null;
            install.GetAwaiter().GetResult();
            UserSettingsManager.UpdateContentSelection(
                root,
                fixture.Definition.ID,
                fixture.Scenario.ID,
                store.GetInstalled().SelectMany(package => package.Content).ToArray(),
                false
            );
            controller = new ContentEditorModManagerController(
                view.ModManager,
                root,
                () => Array.Empty<string>(),
                (_, _, action) => action(),
                () => { },
                store
            );
            controller.Show();
            controller.Cancel();
            while (controller.IsBusy)
                yield return null;
        }

        /// <summary>
        /// Selects a package card and invokes its displayed enable or disable action.
        /// </summary>
        /// <param name="id">The installed add-on identity.</param>
        private void Toggle(string id)
        {
            ContentEditorModCardView card = view
                .ModManager.GetComponentsInChildren<ContentEditorModCardView>()
                .Single(item =>
                    item.Entry?.Installation?.Content.Any(content => content.ID == id) == true
                );
            card.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
            Button activate = view
                .ModManager.GetComponentsInChildren<Button>()
                .Single(button => button.name == "activateButton");
            Assert.IsTrue(activate.interactable);
            activate.onClick.Invoke();
        }

        /// <summary>
        /// Creates cached add-on releases and records their dependency through installation.
        /// </summary>
        /// <param name="store">The fixture-owned package store.</param>
        /// <param name="era">The compatible era for the test add-ons.</param>
        /// <returns>The installation completion.</returns>
        private async Task InstallAddOnsAsync(ModPackageStore store, ContentPackDefinition era)
        {
            ThunderstorePackage[] listings = new ThunderstorePackage[2];
            string[] names = { "Dependency", "Parent" };
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                string source = Path.Combine(folder, name + "-source");
                Directory.CreateDirectory(source);
                File.WriteAllText(
                    Path.Combine(source, "mod.xml"),
                    "<ContentModDefinition><ID>"
                        + name
                        + "</ID><DisplayName>"
                        + name
                        + "</DisplayName><Version>1.0.0</Version>"
                        + $"<BasePackID>{era.ID}</BasePackID><BasePackVersion>{era.Version}</BasePackVersion></ContentModDefinition>"
                );
                File.WriteAllText(
                    Path.Combine(source, "manifest.json"),
                    "{\"name\":\"" + name + "\",\"version_number\":\"1.0.0\"}"
                );
                string archive = Path.Combine(folder, name + ".zip");
                ZipFile.CreateFromDirectory(source, archive);
                await store.InstallLocalAsync(
                    archive,
                    Array.Empty<string>(),
                    null,
                    CancellationToken.None
                );
                InstalledModPackage receipt = store
                    .GetInstalled()
                    .Single(item => item.Name == name);
                listings[index] = new ThunderstorePackage
                {
                    name = name,
                    owner = "Fixture",
                    full_name = receipt.PackageID,
                    versions = new[]
                    {
                        new ThunderstorePackageVersion
                        {
                            version_number = "1.0.0",
                            file_size = new FileInfo(archive).Length,
                            dependencies =
                                index == 0
                                    ? Array.Empty<string>()
                                    : new[] { listings[0].full_name + "-1.0.0" },
                        },
                    },
                };
            }
            await store.InstallAsync(
                listings[1],
                listings[1].Latest,
                listings,
                Array.Empty<string>(),
                null,
                CancellationToken.None
            );
        }
    }
}
