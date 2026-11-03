using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorXmlViewTests
    {
        private string folder;
        private GameObject canvas;
        private ContentEditorXmlView view;

        /// <summary>
        /// Binds one XML control to an independent authored record.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "editor-xml-" + Guid.NewGuid().ToString("N"));
            ContentPackEditorProject project = ContentEditorTestData.CreateProject(folder);
            canvas = new GameObject("XML test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(800, 900);
            view = UIComponentTestHelper.InstantiatePrefabComponent<ContentEditorXmlView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
            RectTransform rect = (RectTransform)view.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            view.Bind(
                project.GetCatalogs().First(catalog => catalog.Name == "Officers").Records.First(),
                null
            );
        }

        /// <summary>
        /// Releases the authored XML control and fixture files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(canvas);
            Directory.Delete(folder, true);
        }

        [Test]
        public void Bind_Officer_ShowsOnlyTheSelectedOfficer()
        {
            view.Show();
            Canvas.ForceUpdateCanvases();
            TMP_InputField input = view.GetComponentsInChildren<TMP_InputField>()
                .Single(field => field.name == "AdvancedXml");
            Assert.AreEqual("Officer", System.Xml.Linq.XElement.Parse(input.text).Name.LocalName);
        }

        [Test]
        public void Show_Xml_ProvidesAConnectedScrollbar()
        {
            view.Show();
            Canvas.ForceUpdateCanvases();
            TMP_InputField xml = view.GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.name == "AdvancedXml");
            Assert.IsNotNull(xml.verticalScrollbar);
            Assert.IsTrue(xml.verticalScrollbar.gameObject.activeInHierarchy);
            Assert.Less(xml.textViewport.rect.width, ((RectTransform)xml.transform).rect.width);
        }
    }
}
