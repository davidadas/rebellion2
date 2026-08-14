using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public class ConfirmDialogWindowRenderDataTests
    {
        [Test]
        public void Constructor_NullLines_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogWindowRenderData(10, 20, null, null, null)
            );
        }

        [Test]
        public void Constructor_CompletePresentation_StoresValuesAndCopiesLines()
        {
            Texture2D background = new Texture2D(4, 4);
            Texture2D title = new Texture2D(2, 2);
            List<string> sourceLines = new List<string> { "Confirm?", "Coruscant" };

            ConfirmDialogWindowRenderData data = new ConfirmDialogWindowRenderData(
                10,
                20,
                background,
                title,
                sourceLines
            );
            sourceLines[0] = "Changed";

            Assert.AreEqual(10, data.X);
            Assert.AreEqual(20, data.Y);
            Assert.AreSame(background, data.BackgroundTexture);
            Assert.AreSame(title, data.TitleTexture);
            CollectionAssert.AreEqual(new[] { "Confirm?", "Coruscant" }, data.Lines);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)data.Lines).Add("Additional")
            );

            UnityEngine.Object.DestroyImmediate(title);
            UnityEngine.Object.DestroyImmediate(background);
        }
    }
}
