using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.ContextMenus
{
    [TestFixture]
    public class StrategyContextMenuPresenterTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private StrategyContextMenuPresenter _presenter;
        private ContextMenuView _view;
        private GameObject _windowObject;
        private UIWindow _window;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _presenter = _rootObject.GetComponentInChildren<StrategyContextMenuPresenter>(true);
            _view = _rootObject.GetComponentInChildren<ContextMenuView>(true);
            _windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _presenter.Initialize(CreateContext());
        }

        [TearDown]
        public void TearDown()
        {
            if (_windowObject != null)
                UnityEngine.Object.DestroyImmediate(_windowObject);
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void AuthoredLayout_InitializedPresenter_ExposesPositiveWidths()
        {
            StrategyContextMenuLayout layout = _presenter.Layout;

            Assert.Greater(_presenter.SpeedMenuWidth, 0);
            Assert.Greater(layout.FacilityMenuWidth, 0);
            Assert.Greater(layout.FleetMenuWidth, 0);
            Assert.Greater(layout.FleetBombardmentMenuWidth, 0);
            Assert.Greater(layout.PlanetSystemMenuWidth, 0);
            Assert.Greater(layout.DefenseMenuWidth, 0);
            Assert.Greater(layout.MissionsMenuWidth, 0);
            Assert.Greater(layout.FallbackMenuWidth, 0);
        }

        [Test]
        public void Show_Commands_FiltersNullAndRendersNestedPresentation()
        {
            StrategyMenuCommand child = new StrategyMenuCommand(
                StrategyMenuAction.Status,
                "Child",
                true
            );
            StrategyMenuCommand parent = new StrategyMenuCommand("Parent", true, new[] { child });
            StrategyMenuCommand speed = new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedFast,
                "Fast",
                true,
                StrategyContextMenuIconKeys.FastSpeed
            );
            StrategyMenuCommand checkedCommand = new StrategyMenuCommand(
                StrategyMenuAction.Encyclopedia,
                "Checked",
                true,
                StrategyContextMenuIconKeys.CheckMark
            );

            _presenter.Show(
                new StrategyContextMenuData(
                    _window,
                    20,
                    30,
                    100,
                    new[] { null, parent, speed, checkedCommand }
                )
            );
            _presenter.RenderCurrent();
            ContextMenuPanelView rootPanel = FindRenderedPanels().Single();
            ContextMenuCommandView[] rootRows = FindRenderedRows(rootPanel);
            rootRows[0].OnPointerEnter(new PointerEventData(null));
            ContextMenuPanelView[] panels = FindRenderedPanels();
            ContextMenuCommandView childRow = FindRenderedRows(panels[1]).Single();
            RawImage parentIcon = rootRows[0]
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "IconImage");
            RectInt parentIconRect = UILayout.GetSourceRect(parentIcon.rectTransform);

            Assert.IsTrue(_presenter.Open);
            Assert.AreSame(_window, _presenter.Window);
            Assert.AreEqual(3, rootRows.Length);
            Assert.AreEqual("Parent", FindCommandText(rootRows[0]).text);
            Assert.AreEqual("Fast", FindCommandText(rootRows[1]).text);
            Assert.AreEqual("Checked", FindCommandText(rootRows[2]).text);
            Assert.AreEqual("Child", FindCommandText(childRow).text);
            Assert.AreEqual(new RectInt(6, 0, 17, 20), parentIconRect);
        }

        [Test]
        public void Show_NullMenu_ResetsCurrentPresentation()
        {
            _presenter.Show(
                new StrategyContextMenuData(
                    _window,
                    10,
                    20,
                    100,
                    Array.Empty<StrategyMenuCommand>()
                )
            );

            _presenter.Show(null);
            _presenter.RenderCurrent();

            Assert.IsFalse(_presenter.Open);
            Assert.IsNull(_presenter.Window);
            Assert.IsFalse(_view.Open);
            Assert.IsEmpty(FindRenderedPanels());
        }

        [Test]
        public void CommandSelection_EnabledLeaf_ForwardsStrategyCommand()
        {
            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyMenuAction.Status,
                "Status",
                true
            );
            StrategyMenuCommand selected = null;
            _presenter.CommandSelected += value => selected = value;
            _presenter.Show(new StrategyContextMenuData(_window, 10, 20, 100, new[] { command }));
            _presenter.RenderCurrent();
            ContextMenuCommandView row = FindRenderedRows(FindRenderedPanels().Single()).Single();
            PointerEventData pointer = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            row.OnPointerClick(pointer);

            Assert.AreSame(command, selected);
        }

        [Test]
        public void DismissBoundary_OpenMenu_ForwardsPointerEvent()
        {
            PointerEventData pointer = new PointerEventData(null);
            PointerEventData received = null;
            _presenter.DismissRequested += value => received = value;
            _presenter.Show(
                new StrategyContextMenuData(
                    _window,
                    10,
                    20,
                    100,
                    new[] { new StrategyMenuCommand(StrategyMenuAction.Status, "Status", true) }
                )
            );
            ContextMenuDismissBoundary boundary =
                _rootObject.GetComponentInChildren<ContextMenuDismissBoundary>(true);

            boundary.OnPointerDown(pointer);

            Assert.AreSame(pointer, received);
        }

        [Test]
        public void TryCancel_OpenThenClosedMenu_ReturnsMatchingState()
        {
            _presenter.Show(
                new StrategyContextMenuData(
                    _window,
                    10,
                    20,
                    100,
                    Array.Empty<StrategyMenuCommand>()
                )
            );

            bool first = _presenter.TryCancel();
            bool second = _presenter.TryCancel();

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.IsFalse(_presenter.Open);
        }

        [Test]
        public void GetMenuWidth_LongCommandAndNullList_ReturnsRequiredWidths()
        {
            int emptyWidth = _presenter.GetMenuWidth(80, null);
            int textWidth = _presenter.GetMenuWidth(
                80,
                new[]
                {
                    new StrategyMenuCommand(
                        StrategyMenuAction.Status,
                        "A command label wider than the authored menu",
                        true
                    ),
                }
            );

            Assert.GreaterOrEqual(emptyWidth, 80);
            Assert.Greater(textWidth, emptyWidth);
        }

        private UIContext CreateContext()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
        }

        private ContextMenuPanelView[] FindRenderedPanels()
        {
            return _rootObject
                .GetComponentsInChildren<ContextMenuPanelView>(true)
                .Where(panel =>
                    panel.name.StartsWith("Panel", StringComparison.Ordinal)
                    && panel.name != "PanelTemplate"
                    && panel.gameObject.activeSelf
                )
                .OrderBy(panel => panel.name)
                .ToArray();
        }

        private static ContextMenuCommandView[] FindRenderedRows(ContextMenuPanelView panel)
        {
            return panel
                .GetComponentsInChildren<ContextMenuCommandView>(true)
                .Where(row =>
                    row.name.StartsWith("Command", StringComparison.Ordinal)
                    && row.name != "CommandTemplate"
                    && row.gameObject.activeSelf
                )
                .OrderBy(row => row.name)
                .ToArray();
        }

        private static TextMeshProUGUI FindCommandText(ContextMenuCommandView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "CommandTextField");
        }
    }
}
