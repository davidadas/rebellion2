using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.ContextMenus
{
    [TestFixture]
    public class StrategyContextMenuProviderTests
    {
        private GameObject _windowObject;

        [TearDown]
        public void TearDown()
        {
            if (_windowObject != null)
                Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void MenuData_CommandSourceChange_PreservesSnapshot()
        {
            UIWindow window = CreateWindow();
            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyMenuAction.Status,
                "Status",
                true
            );
            List<StrategyMenuCommand> commands = new List<StrategyMenuCommand> { command };

            StrategyContextMenuData data = new StrategyContextMenuData(
                window,
                20,
                30,
                140,
                commands
            );
            commands.Clear();

            Assert.AreEqual(1, data.Commands.Count);
            Assert.AreSame(command, data.Commands[0]);
        }

        [Test]
        public void MenuData_NullCommands_UsesEmptyCollection()
        {
            StrategyContextMenuData data = new StrategyContextMenuData(null, 0, 0, 0, null);

            Assert.IsEmpty(data.Commands);
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
