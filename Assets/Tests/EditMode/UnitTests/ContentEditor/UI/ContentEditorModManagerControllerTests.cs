using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
        public IEnumerator Activate_EnablingDependent_EnablesRequiredAddOn()
        {
            yield return PrepareLibrary();
            Toggle("Parent");

            UserSettingsManager.TryReadContentSelection(out _, out _, out string[] disabled);
            CollectionAssert.DoesNotContain(disabled, "Parent");
            CollectionAssert.DoesNotContain(disabled, "Dependency");
        }

        [UnityTest]
        public IEnumerator Activate_EnabledDependent_RejectsDisablingItsDependency()
        {
            yield return PrepareLibrary();
            Toggle("Parent");

            Toggle("Dependency");

            UserSettingsManager.TryReadContentSelection(out _, out _, out string[] disabled);
            CollectionAssert.DoesNotContain(disabled, "Dependency");
        }

        [UnityTest]
        public IEnumerator Activate_DependentDisabled_AllowsDisablingItsDependency()
        {
            yield return PrepareLibrary();
            Toggle("Parent");
            Toggle("Parent");

            Toggle("Dependency");

            UserSettingsManager.TryReadContentSelection(out _, out _, out string[] disabled);
            CollectionAssert.Contains(disabled, "Dependency");
        }

        /// <summary>
        /// Installs a dependent add-on pair in an isolated content root and opens its library.
        /// </summary>
        /// <returns>The asynchronous fixture preparation.</returns>
        private IEnumerator PrepareLibrary()
        {
            settingsPath = Path.Combine(UserDataPaths.Root, "user-settings.json");
            previousSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            string root = Path.Combine(folder, "library", "Content");
            ContentDefinitionSerializer.Serialize(
                Path.Combine(root, "catalog.xml"),
                new ContentCatalogDefinition { ActivePackID = "Fixture" }
            );
            ContentDefinitionSerializer.Serialize(
                Path.Combine(root, "Packs", "Fixture", "pack.xml"),
                new ContentPackDefinition
                {
                    ID = "Fixture",
                    DisplayName = "Fixture",
                    Version = "1.0.0",
                }
            );
            ModPackageStore store = new(root, new ThunderstoreClient());
            Task install = InstallAddOnsAsync(store);
            while (!install.IsCompleted)
                yield return null;
            install.GetAwaiter().GetResult();
            UserSettingsManager.UpdateContentSelection(
                "Fixture",
                null,
                new[] { "Dependency", "Parent" },
                false
            );
            controller = new ContentEditorModManagerController(
                view.ModManager,
                root,
                () => Array.Empty<string>(),
                (_, action) => action(),
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
            card.GetComponent<Button>().onClick.Invoke();
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
        /// <returns>The installation completion.</returns>
        private async Task InstallAddOnsAsync(ModPackageStore store)
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
                        + "<BasePackID>Fixture</BasePackID><BasePackVersion>1.0.0</BasePackVersion></ContentModDefinition>"
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
