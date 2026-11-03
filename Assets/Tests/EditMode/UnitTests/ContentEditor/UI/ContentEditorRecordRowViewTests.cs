using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorRecordRowViewTests
    {
        private string folder;
        private ContentPackEditorProject project;
        private ContentEditorRecordRowView row;

        /// <summary>
        /// Creates one navigation row over independent definition data.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "editor-row-" + Guid.NewGuid().ToString("N"));
            project = ContentEditorTestData.CreateProject(folder);
            row = UIComponentTestHelper.InstantiatePrefabComponent<ContentEditorRecordRowView>(
                "Assets/Prefabs/UI/ContentPackEditor/ContentPackEditorRoot.prefab"
            );
        }

        /// <summary>
        /// Releases the authored row and test documents.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(row.gameObject);
            Directory.Delete(folder, true);
        }

        [Test]
        public void Render_DifferentImageShapes_KeepTheirAuthoredAspectRatios()
        {
            int images = 0;
            foreach (
                ContentPackEditorRecord record in project
                    .GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Where(record => !string.IsNullOrEmpty(record.ImageAddress))
                    .GroupBy(record => record.Kind)
                    .Select(group => group.First())
            )
            {
                row.Render(
                    new ContentEditorTreeNode(record.Key, record.Name, 0, record),
                    0,
                    false,
                    false,
                    TestContent.Assets,
                    null
                );
                RawImage image = row.GetComponentsInChildren<RawImage>()
                    .Single(item => item.name == "Thumbnail");
                if (image.texture == null)
                    continue;
                images++;
                Assert.That(
                    image.GetComponent<AspectRatioFitter>().aspectRatio,
                    Is.EqualTo((float)image.texture.width / image.texture.height).Within(0.001),
                    record.Name
                );
                Assert.AreEqual(
                    AspectRatioFitter.AspectMode.FitInParent,
                    image.GetComponent<AspectRatioFitter>().aspectMode
                );
            }
            Assert.Greater(images, 0);
        }
    }
}
