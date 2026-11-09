using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Tactical
{
    [TestFixture]
    public class TacticalBattleViewTests
    {
        private GameObject _root;
        private TacticalBattleView _view;

        /// <summary>
        /// Builds the view under test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _view = TacticalBattleViewTestFactory.Create(out _root);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Destroys the test hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_UnitsAndShots_PoolsViewsAndAppliesLabels()
        {
            TacticalBattleRenderData data = CreateData(unitCount: 3, shotCount: 2);

            _view.Render(data);
            _view.Render(CreateData(unitCount: 1, shotCount: 0));

            TacticalUnitView[] unitViews = _view
                .GetComponentsInChildren<TacticalUnitView>(true)
                .Where(unitView => unitView.name != "UnitTemplate")
                .ToArray();
            RawImage[] shots = _view
                .GetComponentsInChildren<RawImage>(true)
                .Where(image =>
                    image.name.StartsWith("Shot", StringComparison.Ordinal)
                    && image.name != "ShotTemplate"
                )
                .ToArray();
            Assert.AreEqual(3, unitViews.Length);
            Assert.AreEqual(1, unitViews.Count(unitView => unitView.gameObject.activeSelf));
            Assert.AreEqual(2, shots.Length);
            Assert.IsFalse(shots.Any(shot => shot.gameObject.activeSelf));
            Assert.AreEqual("Battle at Corellia", FindText("TitleTextField").text);
            Assert.AreEqual("Empire: 1 ships", FindText("AttackerStatusTextField").text);
            Assert.AreEqual("Alliance: 2 ships", FindText("DefenderStatusTextField").text);
            Assert.AreEqual("0:42", FindText("StatusTextField").text);
            Assert.AreEqual("Hint", FindText("SelectionTextField").text);
            Assert.AreEqual("Retreat", FindText("RetreatButtonLabel").text);
            Assert.IsFalse(
                TacticalBattleViewTestFactory.FindButton(_view, "RetreatButton").interactable
            );
            Assert.IsTrue(
                TacticalBattleViewTestFactory.FindButton(_view, "AutoButton").interactable
            );
        }

        [Test]
        public void Render_WithoutPanel_ShowsEmptyPanelState()
        {
            _view.Render(CreateData(unitCount: 0, shotCount: 0));

            Assert.AreEqual("No ship selected", FindText("NameTextField").text);
            Assert.AreEqual("—", FindText("ShieldTextField").text);
            Assert.IsFalse(FindObject("PortraitImage").activeSelf);
            Assert.IsFalse(FindComponent<RawImage>("HullBarImage").enabled);
            Assert.AreEqual("—", FindText("System0Label").text);
            Assert.IsFalse(FindObject("Personnel0Image").activeSelf);
        }

        [Test]
        public void Render_WithPanel_FillsBarsSystemsAndPersonnel()
        {
            Texture2D portrait = new Texture2D(4, 4);
            try
            {
                TacticalPanelRenderData panel = new TacticalPanelRenderData(
                    "Dauntless",
                    "Task Force 1",
                    portrait,
                    showShields: true,
                    0.5f,
                    "100 / 200",
                    0.25f,
                    "300 / 1200",
                    new[]
                    {
                        new TacticalSystemRenderData("Shields", TacticalSystemState.Online),
                        new TacticalSystemRenderData("Engines", TacticalSystemState.Offline),
                        new TacticalSystemRenderData("Weapons", TacticalSystemState.Online),
                        new TacticalSystemRenderData("Hyperdrive", TacticalSystemState.Absent),
                    },
                    new[] { 1f, 0.5f, 0f, 0.25f },
                    new[] { new TacticalPersonnelRenderData(portrait, "Admiral Ackbar") },
                    "1 officer",
                    "Orders: Attack Victory",
                    "Tactics: Manual command",
                    "Targets: Fighters first",
                    true
                );

                _view.Render(CreateData(unitCount: 0, shotCount: 0, panel: panel));

                Assert.AreEqual("Dauntless", FindText("NameTextField").text);
                Assert.AreEqual("Task Force 1", FindText("SubtitleTextField").text);
                Assert.IsTrue(FindObject("PortraitImage").activeSelf);
                Assert.AreSame(portrait, FindComponent<RawImage>("PortraitImage").texture);
                Assert.AreEqual(
                    50,
                    UILayout
                        .GetSourceRect(FindComponent<RawImage>("ShieldBarImage").rectTransform)
                        .width
                );
                Assert.AreEqual(
                    25,
                    UILayout
                        .GetSourceRect(FindComponent<RawImage>("HullBarImage").rectTransform)
                        .width
                );
                Assert.AreEqual("100 / 200", FindText("ShieldTextField").text);
                Assert.AreEqual("300 / 1200", FindText("HullTextField").text);
                Assert.AreEqual("Engines", FindText("System1Label").text);
                Assert.Greater(
                    FindComponent<RawImage>("System1Image").color.r,
                    FindComponent<RawImage>("System1Image").color.g
                );
                Assert.Greater(
                    FindComponent<RawImage>("System0Image").color.g,
                    FindComponent<RawImage>("System0Image").color.r
                );
                Assert.AreEqual(
                    80,
                    UILayout
                        .GetSourceRect(FindComponent<RawImage>("Arc0BarImage").rectTransform)
                        .width
                );
                Assert.AreEqual(
                    40,
                    UILayout
                        .GetSourceRect(FindComponent<RawImage>("Arc1BarImage").rectTransform)
                        .width
                );
                Assert.IsFalse(FindComponent<RawImage>("Arc2BarImage").enabled);
                Assert.IsTrue(FindObject("Personnel0Image").activeSelf);
                Assert.IsFalse(FindObject("Personnel1Image").activeSelf);
                Assert.AreEqual("1 officer", FindText("PersonnelTextField").text);
                Assert.AreEqual("Orders: Attack Victory", FindText("OrdersTextField").text);
                Assert.AreEqual("Tactics: Manual command", FindText("TacticsTextField").text);
                Assert.AreEqual(
                    "Targets: Fighters first",
                    FindText("TargetPriorityButtonLabel").text
                );
                Assert.IsTrue(
                    TacticalBattleViewTestFactory
                        .FindButton(_view, "TargetPriorityButton")
                        .interactable
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(portrait);
            }
        }

        [Test]
        public void Render_Roster_LaysOutSlotsAndRoutesClicks()
        {
            int pressedShip = -1;
            int pressedSquadron = -1;
            _view.RosterShipPressed += index => pressedShip = index;
            _view.RosterSquadronPressed += index => pressedSquadron = index;
            TacticalRosterRenderData roster = new TacticalRosterRenderData(
                "Alliance",
                new[]
                {
                    new TacticalRosterSlotRenderData("1", null, true, true),
                    new TacticalRosterSlotRenderData("2", null, false, false),
                },
                "X-Wing Squadron",
                true,
                new[] { new TacticalRosterSlotRenderData("12", null, false, true) }
            );

            _view.Render(CreateData(unitCount: 0, shotCount: 0, roster: roster));
            TacticalRosterSlotView[] slots = _view
                .GetComponentsInChildren<TacticalRosterSlotView>(true)
                .Where(slot => slot.name != "RosterSlotTemplate")
                .ToArray();
            slots[1].GetComponent<Button>().onClick.Invoke();
            slots[2].GetComponent<Button>().onClick.Invoke();
            _view.Render(CreateData(unitCount: 0, shotCount: 0, roster: null));

            Assert.AreEqual(3, slots.Length);
            Assert.AreEqual("Alliance", FindText("RosterSideButtonLabel").text);
            Assert.AreEqual("X-Wing Squadron", FindText("RosterClassButtonLabel").text);
            Assert.Less(
                UILayout.GetSourceRect(slots[0].transform as RectTransform).x,
                UILayout.GetSourceRect(slots[1].transform as RectTransform).x
            );
            Assert.Less(
                UILayout.GetSourceRect(slots[1].transform as RectTransform).x,
                UILayout.GetSourceRect(slots[2].transform as RectTransform).x
            );
            Assert.AreEqual(1, pressedShip);
            Assert.AreEqual(0, pressedSquadron);
            Assert.IsFalse(FindObject("Roster").activeSelf);
        }

        [Test]
        public void Render_SelectionBox_ShowsBoxAtBounds()
        {
            TacticalBattleRenderData data = CreateData(
                unitCount: 0,
                shotCount: 0,
                selectionBox: new RectInt(10, 20, 30, 40)
            );

            _view.Render(data);

            GameObject box = FindObject("SelectionBoxImage");
            Assert.IsTrue(box.activeSelf);
            RectInt rect = UILayout.GetSourceRect(box.transform as RectTransform);
            Assert.AreEqual(new RectInt(10, 20, 30, 40), rect);
        }

        [Test]
        public void Hide_AfterRender_DeactivatesLayerAndPooledViews()
        {
            _view.Render(CreateData(unitCount: 2, shotCount: 1));

            _view.Hide();

            Assert.IsFalse(_view.gameObject.activeSelf);
            Assert.IsFalse(
                _view
                    .GetComponentsInChildren<TacticalUnitView>(true)
                    .Any(unitView => unitView.gameObject.activeSelf)
            );
        }

        [Test]
        public void OnPointerDown_OverInformationPanel_DoesNotRaiseArenaPress()
        {
            int pressCount = 0;
            int releaseCount = 0;
            _view.ArenaPressed += (_, _) => pressCount++;
            _view.ArenaReleased += (_, _) => releaseCount++;
            Vector2 panelPoint = new Vector2(
                TacticalBattleViewTestFactory.SurfaceWidth
                    - TacticalBattleViewTestFactory.PanelWidth / 2f,
                100f
            );
            Vector2 arenaPoint = new Vector2(100f, 100f);

            _view.OnPointerDown(CreateEvent(panelPoint));
            _view.OnPointerDown(CreateEvent(arenaPoint));
            _view.OnPointerUp(CreateEvent(panelPoint));

            Assert.AreEqual(1, pressCount);
            Assert.AreEqual(1, releaseCount);
        }

        [Test]
        public void GetArenaSourceRect_AuthoredLayout_ExcludesInformationPanel()
        {
            RectInt arena = _view.GetArenaSourceRect();

            Assert.AreEqual(0, arena.x);
            Assert.AreEqual(0, arena.y);
            Assert.AreEqual(
                Mathf.RoundToInt(TacticalBattleViewTestFactory.SurfaceWidth)
                    - TacticalBattleViewTestFactory.PanelWidth,
                arena.width
            );
            Assert.AreEqual(
                Mathf.RoundToInt(TacticalBattleViewTestFactory.SurfaceHeight),
                arena.height
            );
        }

        /// <summary>
        /// Creates frame data with generated units and shots.
        /// </summary>
        /// <param name="unitCount">The unit count.</param>
        /// <param name="shotCount">The shot count.</param>
        /// <param name="selectionBox">The optional selection bounds.</param>
        /// <param name="panel">The optional information panel.</param>
        /// <param name="roster">The optional roster bar.</param>
        /// <returns>The frame data.</returns>
        private static TacticalBattleRenderData CreateData(
            int unitCount,
            int shotCount,
            RectInt? selectionBox = null,
            TacticalPanelRenderData panel = null,
            TacticalRosterRenderData roster = null
        )
        {
            TacticalUnitRenderData[] units = Enumerable
                .Range(0, unitCount)
                .Select(index => new TacticalUnitRenderData(
                    100 + index * 50,
                    200,
                    40,
                    16,
                    180,
                    null,
                    0.5f,
                    0.25f,
                    true,
                    index == 0,
                    Color.green,
                    1f
                ))
                .ToArray();
            TacticalShotRenderData[] shots = Enumerable
                .Range(0, shotCount)
                .Select(index => new TacticalShotRenderData(
                    300,
                    200 + index,
                    120,
                    2,
                    45,
                    Color.red
                ))
                .ToArray();
            return new TacticalBattleRenderData(
                "Battle at Corellia",
                "Empire: 1 ships",
                "Alliance: 2 ships",
                "0:42",
                "Hint",
                panel,
                "Retreat",
                false,
                "Auto: Off",
                true,
                "Pause",
                "Speed 1x",
                null,
                null,
                new RectInt(0, 0, 100, 100),
                units,
                shots,
                selectionBox,
                roster
            );
        }

        /// <summary>
        /// Creates a left-button pointer event at a source-space point on the view.
        /// </summary>
        /// <param name="sourcePoint">The source-space point.</param>
        /// <returns>The pointer event.</returns>
        private static PointerEventData CreateEvent(Vector2 sourcePoint)
        {
            return new PointerEventData(null)
            {
                position = TacticalBattleViewTestFactory.ToScreenPoint(sourcePoint),
                button = PointerEventData.InputButton.Left,
            };
        }

        /// <summary>
        /// Finds a named label within the view.
        /// </summary>
        /// <param name="objectName">The label object name.</param>
        /// <returns>The matching label.</returns>
        private TMPro.TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TMPro.TextMeshProUGUI>(objectName);
        }

        /// <summary>
        /// Finds a named component within the view, ignoring the pooled unit views whose bars
        /// share names with the information panel.
        /// </summary>
        /// <typeparam name="T">The component type.</typeparam>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching component.</returns>
        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _view
                .GetComponentsInChildren<T>(true)
                .Single(component =>
                    component.name == objectName
                    && component.GetComponentInParent<TacticalUnitView>(true) == null
                );
        }

        /// <summary>
        /// Finds a named object within the view, ignoring the pooled unit views.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching object.</returns>
        private GameObject FindObject(string objectName)
        {
            return FindComponent<Transform>(objectName).gameObject;
        }
    }
}
