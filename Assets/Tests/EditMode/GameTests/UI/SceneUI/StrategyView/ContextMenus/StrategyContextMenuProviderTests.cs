using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

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
        public void ProviderContext_CompleteInvocation_StoresAllValues()
        {
            UIWindow window = CreateWindow();
            StrategyContextMenuLayout layout = new StrategyContextMenuLayout(1, 2, 3, 4, 5, 6, 7);
            PointerEventData eventData = new PointerEventData(null);

            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                layout,
                eventData,
                8,
                9
            );

            Assert.AreSame(window, context.Window);
            Assert.AreEqual(1, context.Layout.FacilityMenuWidth);
            Assert.AreSame(eventData, context.EventData);
            Assert.AreEqual(8, context.X);
            Assert.AreEqual(9, context.Y);
        }

        [Test]
        public void Layout_CompleteGeometry_StoresEveryWidth()
        {
            StrategyContextMenuLayout layout = new StrategyContextMenuLayout(
                11,
                12,
                13,
                14,
                15,
                16,
                17
            );

            Assert.AreEqual(11, layout.FacilityMenuWidth);
            Assert.AreEqual(12, layout.FleetMenuWidth);
            Assert.AreEqual(13, layout.FleetBombardmentMenuWidth);
            Assert.AreEqual(14, layout.PlanetSystemMenuWidth);
            Assert.AreEqual(15, layout.DefenseMenuWidth);
            Assert.AreEqual(16, layout.MissionsMenuWidth);
            Assert.AreEqual(17, layout.FallbackMenuWidth);
        }

        [Test]
        public void MenuData_CommandSource_CopiesCommandsAndStoresPlacement()
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

            Assert.AreSame(window, data.Window);
            Assert.AreEqual(20, data.X);
            Assert.AreEqual(30, data.Y);
            Assert.AreEqual(140, data.Width);
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
