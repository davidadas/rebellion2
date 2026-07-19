using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Targeting
{
    [TestFixture]
    public class StrategyWindowTargetingSourceTests
    {
        private GameObject _windowObject;

        [TearDown]
        public void TearDown()
        {
            if (_windowObject != null)
                Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_CompleteState_CopiesItemsAndStoresValues()
        {
            UIWindow window = CreateWindow();
            Officer officer = new Officer();
            List<ISceneNode> items = new List<ISceneNode> { officer };

            StrategyWindowTargetingSource source = new StrategyWindowTargetingSource(
                window,
                StrategyContextMenuActions.Move,
                12,
                34,
                items
            );
            items.Clear();

            Assert.AreSame(window, source.Window);
            Assert.AreEqual(StrategyContextMenuActions.Move, source.Action);
            Assert.AreEqual(12, source.SourceX);
            Assert.AreEqual(34, source.SourceY);
            Assert.AreEqual(1, source.Items.Count);
            Assert.AreSame(officer, source.Items[0]);
        }

        [Test]
        public void Constructor_NullItems_NormalizesToEmptyList()
        {
            StrategyWindowTargetingSource source = new StrategyWindowTargetingSource(
                null,
                StrategyContextMenuActions.Status,
                0,
                0,
                null
            );

            Assert.IsNotNull(source.Items);
            Assert.IsEmpty(source.Items);
        }

        [TestCase(StrategyContextMenuActions.CreateMission, "Select mission target")]
        [TestCase(StrategyContextMenuActions.Destination, "Select destination")]
        [TestCase(StrategyContextMenuActions.Move, "Select move destination")]
        [TestCase(StrategyContextMenuActions.MoveConfirm, "Select move destination")]
        [TestCase(StrategyContextMenuActions.Status, "Select target")]
        public void GetPrompt_Action_ReturnsExpectedPrompt(int action, string expectedPrompt)
        {
            string prompt = StrategyWindowTargetingSource.GetPrompt(action);

            Assert.AreEqual(expectedPrompt, prompt);
        }

        private UIWindow CreateWindow()
        {
            _windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            return _windowObject.GetComponent<UIWindow>();
        }
    }
}
