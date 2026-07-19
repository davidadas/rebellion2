using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components.ContextMenu
{
    [TestFixture]
    public class ContextMenuViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private Texture2D _activeTexture;
        private Texture2D _texture;
        private ContextMenuView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _view = UIComponentTestHelper.InstantiatePrefabComponent<ContextMenuView>(_prefabPath);
            _viewObject = _view.gameObject;
            _texture = new Texture2D(45, 45);
            _activeTexture = new Texture2D(45, 45);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_activeTexture);
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void CommandItem_NullCommand_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ContextMenuCommandItem(null));
        }

        [Test]
        public void CommandItem_IconsAndSubmenu_StoresImmutablePresentation()
        {
            TestCommand command = new TestCommand("Parent", true);
            List<ContextMenuCommandItem> children = new List<ContextMenuCommandItem>
            {
                new ContextMenuCommandItem(new TestCommand("Child", true)),
            };
            ContextMenuCommandItem item = new ContextMenuCommandItem(
                command,
                _texture,
                _activeTexture,
                false,
                true,
                children
            );
            children.Clear();

            Assert.AreSame(command, item.Command);
            Assert.AreEqual("Parent", item.Text);
            Assert.IsTrue(item.Enabled);
            Assert.IsTrue(item.UsesIconColumn);
            Assert.IsTrue(item.CenterNativeIcon);
            Assert.IsTrue(item.HasSubmenu);
            Assert.AreEqual(1, item.SubmenuCommands.Count);
            Assert.AreSame(_texture, item.GetIconTexture());
            item.Active = true;
            Assert.AreSame(_activeTexture, item.GetIconTexture());
        }

        [Test]
        public void Visuals_Constructor_StoresCommandColors()
        {
            Color32 enabled = new Color32(1, 2, 3, 4);
            Color32 active = new Color32(5, 6, 7, 8);
            Color32 disabled = new Color32(9, 10, 11, 12);

            ContextMenuView.ContextMenuVisuals visuals = new ContextMenuView.ContextMenuVisuals(
                enabled,
                active,
                disabled
            );

            Assert.AreEqual(enabled, visuals.EnabledColor);
            Assert.AreEqual(active, visuals.ActiveColor);
            Assert.AreEqual(disabled, visuals.DisabledColor);
        }

        [Test]
        public void Metrics_Dimensions_CalculatePanelWidthAndHeight()
        {
            ContextMenuMetrics metrics = new ContextMenuMetrics(20, 30, 2);

            Assert.AreEqual(100, metrics.GetPanelWidth(100, false));
            Assert.AreEqual(130, metrics.GetPanelWidth(100, true));
            Assert.AreEqual(64, metrics.GetPanelHeight(3));
            Assert.AreEqual(20, metrics.RowHeight);
            Assert.AreEqual(30, metrics.IconPanelWidth);
            Assert.AreEqual(2, metrics.BorderSize);
        }

        [Test]
        public void OpenAtAndRenderCurrent_Commands_RendersAuthoredPanelAndRows()
        {
            object owner = new object();
            ContextMenuCommandItem[] items =
            {
                new ContextMenuCommandItem(new TestCommand("Enabled", true)),
                new ContextMenuCommandItem(new TestCommand("Disabled", false)),
            };

            _view.OpenAt(owner, 20, 30, 100, items);
            _view.RenderCurrent();

            Assert.IsTrue(_view.Open);
            Assert.AreSame(owner, _view.Owner);
            ContextMenuPanelView panel = FindRenderedPanels().Single();
            Assert.IsTrue(panel.gameObject.activeSelf);
            ContextMenuCommandView[] rows = FindRenderedRows(panel);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("Enabled", FindCommandText(rows[0]).text);
            Assert.AreEqual("Disabled", FindCommandText(rows[1]).text);
            Assert.IsTrue(FindDismissHitArea().gameObject.activeSelf);
        }

        [Test]
        public void OpenAt_SurfaceEdges_KeepsPanelWithinAuthoredSurface()
        {
            RectTransform surface = _view.transform as RectTransform;
            Vector2 size = surface.sizeDelta;
            ContextMenuCommandItem[] items =
            {
                new ContextMenuCommandItem(new TestCommand("Command", true)),
            };

            _view.OpenAt(null, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 100, items);
            _view.RenderCurrent();

            RectInt panelRect = UILayout.GetSourceRect(
                FindRenderedPanels().Single().transform as RectTransform
            );
            Assert.GreaterOrEqual(panelRect.x, 0);
            Assert.GreaterOrEqual(panelRect.y, 0);
            Assert.LessOrEqual(panelRect.xMax, Mathf.RoundToInt(size.x));
            Assert.LessOrEqual(panelRect.yMax, Mathf.RoundToInt(size.y));
        }

        [Test]
        public void GetMenuWidth_IconColumnAndLongText_ExpandsAuthoredWidth()
        {
            ContextMenuCommandItem[] plain =
            {
                new ContextMenuCommandItem(new TestCommand("A", true)),
            };
            ContextMenuCommandItem[] icon =
            {
                new ContextMenuCommandItem(
                    new TestCommand("A very long command label", true),
                    _texture
                ),
            };

            int plainWidth = _view.GetMenuWidth(60, plain);
            int iconWidth = _view.GetMenuWidth(60, icon);

            Assert.GreaterOrEqual(plainWidth, 60);
            Assert.Greater(iconWidth, plainWidth);
        }

        [Test]
        public void CreateVisuals_ActiveColor_UsesAuthoredAndProvidedColors()
        {
            Color32 activeColor = new Color32(1, 2, 3, 4);

            ContextMenuView.ContextMenuVisuals visuals = _view.CreateVisuals(activeColor);
            ContextMenuView.ContextMenuVisuals fallback = _view.CreateVisuals(null);

            Assert.AreEqual(activeColor, visuals.ActiveColor);
            Assert.AreEqual(visuals.EnabledColor, fallback.ActiveColor);
            Assert.AreEqual(new Color32(255, 255, 255, 255), visuals.EnabledColor);
            Assert.AreEqual(new Color32(128, 128, 128, 255), visuals.DisabledColor);
        }

        [Test]
        public void LeafCommandPointerLifecycle_EnabledCommand_SelectsAndClearsActiveState()
        {
            TestCommand command = new TestCommand("Command", true);
            ContextMenuCommandItem item = new ContextMenuCommandItem(command);
            IContextMenuCommand selected = null;
            _view.CommandSelected += value => selected = value;
            _view.OpenAt(null, 10, 10, 100, new[] { item });
            _view.RenderCurrent();
            ContextMenuCommandView row = FindRenderedRows(FindRenderedPanels().Single()).Single();
            PointerEventData leftClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            row.OnPointerEnter(leftClick);
            row.OnPointerClick(leftClick);
            row.OnPointerExit(leftClick);

            Assert.AreSame(command, selected);
            Assert.IsFalse(item.Active);
        }

        [Test]
        public void LeafCommandPointerClick_DisabledOrSecondaryClick_DoesNotSelect()
        {
            ContextMenuCommandItem item = new ContextMenuCommandItem(
                new TestCommand("Disabled", false)
            );
            int selectedCount = 0;
            _view.CommandSelected += _ => selectedCount++;
            _view.OpenAt(null, 10, 10, 100, new[] { item });
            _view.RenderCurrent();
            ContextMenuCommandView row = FindRenderedRows(FindRenderedPanels().Single()).Single();
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };
            PointerEventData leftClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            row.OnPointerEnter(leftClick);
            row.OnPointerDown(rightClick);
            row.OnPointerClick(rightClick);
            row.OnPointerClick(leftClick);

            Assert.AreEqual(0, selectedCount);
            Assert.IsFalse(item.Active);
        }

        [Test]
        public void ParentCommandPointerEnter_Submenu_RendersChildPanelAndSelectsChild()
        {
            TestCommand childCommand = new TestCommand("Child", true);
            ContextMenuCommandItem child = new ContextMenuCommandItem(childCommand);
            ContextMenuCommandItem parent = new ContextMenuCommandItem(
                new TestCommand("Parent", true),
                submenuCommands: new[] { child }
            );
            IContextMenuCommand selected = null;
            _view.CommandSelected += value => selected = value;
            _view.OpenAt(null, 10, 10, 100, new[] { parent });
            _view.RenderCurrent();
            ContextMenuCommandView parentRow = FindRenderedRows(FindRenderedPanels().Single())
                .Single();
            PointerEventData pointer = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            parentRow.OnPointerEnter(pointer);
            ContextMenuPanelView[] panels = FindRenderedPanels();
            ContextMenuCommandView childRow = FindRenderedRows(panels[1]).Single();
            childRow.OnPointerClick(pointer);
            parentRow.OnPointerExit(pointer);

            Assert.AreEqual(2, panels.Length);
            Assert.IsTrue(parent.Active);
            Assert.AreSame(childCommand, selected);
        }

        [Test]
        public void RenderCurrent_ShorterReplacementMenu_HidesUnusedRowsAndPanels()
        {
            ContextMenuCommandItem parent = new ContextMenuCommandItem(
                new TestCommand("Parent", true),
                submenuCommands: new[]
                {
                    new ContextMenuCommandItem(new TestCommand("Child", true)),
                }
            );
            _view.OpenAt(
                null,
                10,
                10,
                100,
                new[] { parent, new ContextMenuCommandItem(new TestCommand("Second", true)) }
            );
            _view.RenderCurrent();
            FindRenderedRows(FindRenderedPanels().Single())[0]
                .OnPointerEnter(new PointerEventData(null));
            ContextMenuPanelView[] originalPanels = FindRenderedPanels();
            ContextMenuCommandView originalSecondRow = FindRenderedRows(originalPanels[0])[1];
            ContextMenuPanelView originalSubmenu = originalPanels[1];

            _view.OpenAt(
                null,
                10,
                10,
                100,
                new[] { new ContextMenuCommandItem(new TestCommand("Replacement", true)) }
            );
            _view.RenderCurrent();

            Assert.IsFalse(originalSecondRow.gameObject.activeSelf);
            Assert.IsFalse(originalSubmenu.gameObject.activeSelf);
        }

        [Test]
        public void DismissBoundary_OpenAndClosedMenu_RaisesOnlyWhileOpen()
        {
            PointerEventData eventData = new PointerEventData(null);
            PointerEventData received = null;
            _view.DismissRequested += value => received = value;
            ContextMenuDismissBoundary boundary =
                _viewObject.GetComponentInChildren<ContextMenuDismissBoundary>(true);
            _view.OpenAt(
                null,
                0,
                0,
                100,
                new[] { new ContextMenuCommandItem(new TestCommand("Command", true)) }
            );

            boundary.OnPointerDown(eventData);
            _view.Reset();
            boundary.OnPointerDown(new PointerEventData(null));

            Assert.AreSame(eventData, received);
        }

        [Test]
        public void TryCancel_OpenThenClosedMenu_ResetsAndReportsStateTransition()
        {
            _view.OpenAt(
                new object(),
                0,
                0,
                100,
                new[] { new ContextMenuCommandItem(new TestCommand("Command", true)) }
            );
            _view.RenderCurrent();

            bool firstCancelled = _view.TryCancel();
            bool secondCancelled = _view.TryCancel();
            _view.RenderCurrent();

            Assert.IsTrue(firstCancelled);
            Assert.IsFalse(secondCancelled);
            Assert.IsFalse(_view.Open);
            Assert.IsNull(_view.Owner);
            Assert.IsFalse(FindDismissHitArea().gameObject.activeSelf);
            Assert.IsEmpty(FindRenderedPanels());
        }

        private RawImage FindDismissHitArea()
        {
            return _viewObject
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "DismissHitAreaImage");
        }

        private ContextMenuPanelView[] FindRenderedPanels()
        {
            return _viewObject
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

        private sealed class TestCommand : IContextMenuCommand
        {
            public TestCommand(string text, bool enabled)
            {
                Text = text;
                Enabled = enabled;
            }

            public string Text { get; }

            public bool Enabled { get; }
        }
    }
}
