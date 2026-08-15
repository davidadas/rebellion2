using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowRenderDataTests
    {
        private Texture2D _firstTexture;
        private Texture2D _secondTexture;

        [SetUp]
        public void SetUp()
        {
            _firstTexture = new Texture2D(1, 1);
            _secondTexture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_firstTexture);
            UnityEngine.Object.DestroyImmediate(_secondTexture);
        }

        [Test]
        public void WindowState_NullSearchText_NormalizesCompleteState()
        {
            FinderWindowState state = new FinderWindowState(FinderMode.Fleets, true, 2, 4, null);

            Assert.AreEqual(FinderMode.Fleets, state.Mode);
            Assert.IsTrue(state.Panel);
            Assert.AreEqual(2, state.ActiveTab);
            Assert.AreEqual(4, state.SelectedIndex);
            Assert.AreEqual(string.Empty, state.SearchText);
        }

        [Test]
        public void DialogButtonRenderData_Values_PreservesPresentation()
        {
            RectInt sourceRect = new RectInt(1, 2, 3, 4);

            FinderWindowDialogButtonRenderData data = new FinderWindowDialogButtonRenderData(
                FinderWindowCommand.Target,
                _firstTexture,
                _secondTexture,
                sourceRect
            );

            Assert.AreEqual(FinderWindowCommand.Target, data.Command);
            Assert.AreSame(_firstTexture, data.Texture);
            Assert.AreSame(_secondTexture, data.PressedTexture);
            Assert.AreEqual(sourceRect, data.SourceRect);
        }

        [Test]
        public void TabRenderData_Textures_PreservesPresentation()
        {
            FinderWindowTabRenderData data = new FinderWindowTabRenderData(
                _firstTexture,
                _secondTexture
            );

            Assert.AreSame(_firstTexture, data.Texture);
            Assert.AreSame(_secondTexture, data.PressedTexture);
        }

        [Test]
        public void RowRenderData_SourceChanges_PreservesNormalizedSnapshot()
        {
            string[] counts = { "1", "2" };

            FinderWindowRowRenderData data = new FinderWindowRowRenderData(
                null,
                null,
                true,
                counts
            );
            counts[0] = "9";

            Assert.AreEqual(string.Empty, data.RowId);
            Assert.AreEqual(string.Empty, data.Name);
            Assert.IsTrue(data.Selected);
            CollectionAssert.AreEqual(new[] { "1", "2" }, data.Counts);
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<string>)data.Counts)[0] = "3"
            );
        }

        [Test]
        public void FrameRenderData_SourceChanges_PreservesCompleteSnapshot()
        {
            FinderWindowDialogButtonRenderData button = new FinderWindowDialogButtonRenderData(
                FinderWindowCommand.Close,
                _firstTexture,
                _secondTexture,
                null
            );
            FinderWindowDialogButtonRenderData[] buttons = { button };

            FinderWindowFrameRenderData data = new FinderWindowFrameRenderData(
                1,
                2,
                3,
                4,
                true,
                false,
                _firstTexture,
                _secondTexture,
                null,
                buttons
            );
            buttons[0] = null;

            Assert.AreEqual(1, data.X);
            Assert.AreEqual(2, data.Y);
            Assert.AreEqual(3, data.Width);
            Assert.AreEqual(4, data.Height);
            Assert.IsTrue(data.ActiveWindow);
            Assert.IsFalse(data.UseUpperButtonLayout);
            Assert.AreSame(_firstTexture, data.BackgroundTexture);
            Assert.AreSame(_secondTexture, data.OverlayFrameTexture);
            Assert.IsNull(data.ButtonStripTexture);
            Assert.AreSame(button, data.DialogButtons[0]);
        }

        [Test]
        public void WindowRenderData_NullFrame_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FinderWindowRenderData(
                    FinderMode.Systems,
                    false,
                    0,
                    -1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    null,
                    null,
                    null
                )
            );
        }

        [Test]
        public void WindowRenderData_SourceChanges_PreservesNormalizedSnapshot()
        {
            FinderWindowFrameRenderData frame = new FinderWindowFrameRenderData(
                1,
                2,
                3,
                4,
                false,
                true,
                null,
                null,
                null,
                null
            );
            FinderWindowTabRenderData tab = new FinderWindowTabRenderData(
                _firstTexture,
                _secondTexture
            );
            FinderWindowRowRenderData row = new FinderWindowRowRenderData(
                "row",
                "Row",
                false,
                null
            );
            FinderWindowTabRenderData[] tabs = { tab };
            FinderWindowRowRenderData[] rows = { row };

            FinderWindowRenderData data = new FinderWindowRenderData(
                FinderMode.Personnel,
                true,
                1,
                2,
                null,
                null,
                null,
                null,
                frame,
                tabs,
                rows
            );
            tabs[0] = null;
            rows[0] = null;

            Assert.AreEqual(FinderMode.Personnel, data.Mode);
            Assert.IsTrue(data.Panel);
            Assert.AreEqual(1, data.ActiveTab);
            Assert.AreEqual(2, data.SelectedIndex);
            Assert.AreEqual(string.Empty, data.SearchText);
            Assert.AreEqual(string.Empty, data.Title);
            Assert.AreEqual(string.Empty, data.Label);
            Assert.AreEqual(string.Empty, data.ActiveTabText);
            Assert.AreSame(frame, data.Frame);
            Assert.AreSame(tab, data.Tabs[0]);
            Assert.AreSame(row, data.Rows[0]);
        }
    }
}
