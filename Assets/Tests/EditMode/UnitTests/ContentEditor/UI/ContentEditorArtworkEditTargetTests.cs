using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorArtworkEditTargetTests
    {
        private GameObject gameObject;
        private ContentEditorArtworkEditTarget target;

        /// <summary>
        /// Creates a target that has a rectangle and no graphic.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Selection outline", typeof(RectTransform));
            target = gameObject.AddComponent<ContentEditorArtworkEditTarget>();
        }

        /// <summary>
        /// Destroys the target and any outline created by the test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Bind_TargetWithoutGraphic_ReportsMissingComponent()
        {
            XElement layout = new(
                nameof(GalaxyBackground),
                new XElement(nameof(GalaxyBackground.ImagePath), "Pack/Assets/background")
            );
            ContentPackEditorRecord record = new(null, layout);
            ContentPackEditorField field = record.GetFields().Single();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                target.Bind(field, _ => { }, default)
            );

            StringAssert.Contains("Graphic component", error.Message);
        }

        [Test]
        public void BindSelection_TargetWithoutGraphic_CreatesSelectionOutline()
        {
            target.BindSelection();

            Assert.AreEqual(1, gameObject.transform.childCount);
        }
    }
}
