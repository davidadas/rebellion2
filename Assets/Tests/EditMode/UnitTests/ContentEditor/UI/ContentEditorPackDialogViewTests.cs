using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorPackDialogViewTests
    {
        private string folder;
        private ContentPackEditorProject project;
        private ContentEditorPackDialogView view;
        private GameObject canvas;

        /// <summary>
        /// Opens only the creation dialog over a small independent project.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-dialog-" + Guid.NewGuid().ToString("N")
            );
            project = ContentEditorTestData.CreateProject(folder);
            canvas = new GameObject("Dialog test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600, 900);
            view = UIComponentTestHelper.InstantiatePrefabComponent<ContentEditorPackDialogView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            view.transform.SetParent(canvas.transform, false);
            view.Initialize(
                new[] { TestContent.Pack.Definition },
                ContentFileResolver.ResolveModsRootPath(project.ContentRootPath)
            );
        }

        /// <summary>
        /// Releases the isolated dialog and authored test files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(canvas);
            Directory.Delete(folder, true);
        }

        [Test]
        public void Show_LongError_KeepsTheCompleteMessageAboveItsActions()
        {
            view.Show();
            ContentEditorPackDialogView dialog =
                view.GetComponentInChildren<ContentEditorPackDialogView>();
            string message = string.Join(
                " ",
                Enumerable.Repeat("Choose another destination for this content pack.", 10)
            );

            dialog.ShowError(message);

            TMP_Text error = dialog
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Error");
            RectInt bounds = UILayout.GetSourceRect(error.rectTransform);
            RectInt confirm = UILayout.GetSourceRect(
                (RectTransform)
                    dialog
                        .GetComponentsInChildren<Button>()
                        .Single(button => button.name == "Confirm")
                        .transform
            );
            Assert.GreaterOrEqual(
                bounds.height,
                error.GetPreferredValues(message, bounds.width, 0).y
            );
            Assert.Greater(confirm.y, bounds.yMax);
        }

        [Test]
        public void Show_SelectingABasePack_UsesTheSelectedSourceWhenCreating()
        {
            view.Initialize(
                new[]
                {
                    new ContentPackDefinition { ID = "first", DisplayName = "First" },
                    new ContentPackDefinition { ID = "second", DisplayName = "Second" },
                },
                ContentFileResolver.ResolveModsRootPath(project.ContentRootPath)
            );
            view.Show();
            view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "DisplayName")
                .text = "New pack";
            view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "PackID")
                .text = "new-pack";
            MenuDropdown selector = view.GetComponentsInChildren<MenuDropdown>()
                .Single(item => item.name == "PackChoice");
            selector.value = 1;
            string chosen = null;
            view.CreateRequested += (source, _, _, _, _) => chosen = source;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Confirm")
                .onClick.Invoke();
            Assert.AreEqual("second", chosen);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void Show_WindowsLocation_DisplaysTheLiteralPath()
        {
            view.Show();
            TMP_InputField location = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "Location");
            location.text = @"C:\new\rebellion2\Mods";
            Canvas.ForceUpdateCanvases();
            location.textComponent.ForceMeshUpdate();
            Assert.AreEqual(
                location.text,
                location.textComponent.GetParsedText().TrimEnd('\u200B')
            );
        }

        [Test]
        public void Show_EnteringAName_GeneratesThePackFolderUnderMods()
        {
            view.Show();
            TMP_InputField name = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "DisplayName");
            name.text = "My New Pack!";
            TMP_InputField id = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "PackID");
            TMP_InputField location = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "Location");
            Assert.AreEqual("my-new-pack", id.text);
            Assert.AreEqual(
                Path.Combine(Directory.GetParent(project.ContentRootPath).FullName, "Mods"),
                location.text
            );
            string destination = null;
            view.CreateRequested += (_, _, _, _, folderPath) => destination = folderPath;
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Confirm")
                .onClick.Invoke();
            Assert.AreEqual(location.text, destination);
            StringAssert.Contains(
                Path.Combine(location.text, "my-new-pack"),
                view.GetComponentsInChildren<TMP_Text>()
                    .Single(label => label.name == "PackFolder")
                    .text
            );
            id.text = "custom-id";
            name.text = "Different display name";
            Assert.AreEqual("custom-id", id.text);
        }

        [Test]
        public void Show_RequiredFieldsEmpty_DisablesCreation()
        {
            view.Show();
            Button confirm = view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "Confirm");
            Assert.IsFalse(confirm.interactable);
        }
    }
}
