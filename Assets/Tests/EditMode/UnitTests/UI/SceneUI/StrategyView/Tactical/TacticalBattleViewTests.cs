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
                new[] { new[] { new TacticalRosterSlotRenderData("12", null, false, true) } }
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
        public void Render_Roster_ManySquadrons_TakeTheirOwnRowsAndShrinkToFit()
        {
            TacticalRosterSlotRenderData[] ships =
            {
                new TacticalRosterSlotRenderData("1", null, false, true),
            };
            TacticalRosterSlotRenderData[][] groups =
            {
                Enumerable
                    .Range(0, 60)
                    .Select(_ => new TacticalRosterSlotRenderData("12", null, false, true))
                    .ToArray(),
                Enumerable
                    .Range(0, 40)
                    .Select(_ => new TacticalRosterSlotRenderData("8", null, false, true))
                    .ToArray(),
            };
            RectInt before = UILayout.GetSourceRect(
                FindObject("Roster").GetComponent<RectTransform>()
            );

            _view.Render(
                CreateData(
                    unitCount: 0,
                    shotCount: 0,
                    roster: new TacticalRosterRenderData("Alliance", ships, groups)
                )
            );

            TacticalRosterSlotView[] slotViews = _view
                .GetComponentsInChildren<TacticalRosterSlotView>(false)
                .Where(slot => slot.name != "RosterSlotTemplate")
                .ToArray();
            RectInt[] slots = slotViews
                .Select(slot => UILayout.GetSourceRect(slot.transform as RectTransform))
                .ToArray();
            bool[] labelsShown = slotViews
                .Select(slot =>
                    slot.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).gameObject.activeSelf
                )
                .ToArray();
            RectInt after = UILayout.GetSourceRect(
                FindObject("Roster").GetComponent<RectTransform>()
            );
            RectInt[] squadronSlots = slots.Where(rect => rect.y > 0).ToArray();
            Assert.AreEqual(101, slots.Length);
            Assert.AreEqual(100, squadronSlots.Length);
            Assert.Greater(squadronSlots.Select(rect => rect.y).Distinct().Count(), 1);
            Assert.Less(squadronSlots[0].width, 34);
            Assert.GreaterOrEqual(squadronSlots[0].width, 16);
            Assert.IsTrue(squadronSlots.All(rect => rect.xMax <= before.width));
            // Narrow slots drop their labels; the full-size ship slot keeps its number.
            Assert.AreEqual(1, labelsShown.Count(shown => shown));
            Assert.IsTrue(labelsShown[Array.IndexOf(slots, slots.First(rect => rect.y == 0))]);
            Assert.Greater(after.height, before.height);
            Assert.AreEqual(before.width, after.width);
        }

        [Test]
        public void Render_Roster_ManyShips_ShrinkToFitThenWrapClearOfTheSideToggle()
        {
            RectInt bar = UILayout.GetSourceRect(
                FindObject("Roster").GetComponent<RectTransform>()
            );

            RectInt[] twenty = RenderShipRoster(20);
            RectInt[] sixty = RenderShipRoster(60);
            RectInt grown = UILayout.GetSourceRect(
                FindObject("Roster").GetComponent<RectTransform>()
            );

            Assert.AreEqual(20, twenty.Length);
            Assert.IsTrue(twenty.All(rect => rect.xMax <= bar.width));
            Assert.IsTrue(twenty.All(rect => rect.width >= 24 && rect.width <= 34));
            Assert.AreEqual(60, sixty.Length);
            Assert.IsTrue(sixty.All(rect => rect.xMax <= bar.width && rect.width >= 24));
            Assert.Greater(sixty.Select(rect => rect.y).Distinct().Count(), 1);
            Assert.IsTrue(sixty.All(rect => rect.x >= 64));
            Assert.Greater(grown.height, bar.height);
        }

        [Test]
        public void Render_DisabledButton_DimsItsCaption()
        {
            _view.Render(CreateData(unitCount: 0, shotCount: 0));

            Assert.IsFalse(
                TacticalBattleViewTestFactory.FindButton(_view, "RetreatButton").interactable
            );
            Assert.Less(FindText("RetreatButtonLabel").color.a, 0.5f);
            Assert.IsTrue(
                TacticalBattleViewTestFactory.FindButton(_view, "AutoButton").interactable
            );
            Assert.AreEqual(1f, FindText("AutoButtonLabel").color.a, 0.001f);
        }

        [Test]
        public void Render_Roster_SquadronAboardAShip_IsTintedApartFromALiveOneAndALostOne()
        {
            TacticalRosterRenderData roster = new TacticalRosterRenderData(
                "Empire",
                null,
                new[]
                {
                    new[]
                    {
                        new TacticalRosterSlotRenderData("12", null, false, true),
                        new TacticalRosterSlotRenderData("9", null, false, false, aboard: true),
                        new TacticalRosterSlotRenderData("0", null, false, false),
                    },
                }
            );

            _view.Render(CreateData(unitCount: 0, shotCount: 0, roster: roster));
            Color[] tints = _view
                .GetComponentsInChildren<TacticalRosterSlotView>(false)
                .Where(slot => slot.name != "RosterSlotTemplate")
                .Select(slot =>
                    slot.GetComponentsInChildren<RawImage>(true)
                        .Single(image => image.name == "SlotPortraitImage")
                        .color
                )
                .ToArray();

            Assert.AreEqual(3, tints.Length);
            Assert.AreEqual(Color.white, tints[0]);
            Assert.AreNotEqual(tints[0], tints[1]);
            Assert.AreNotEqual(tints[2], tints[1]);
            Assert.Greater(tints[1].b, tints[1].r);
            Assert.AreEqual(tints[2].r, tints[2].g, 0.001f);
        }

        [Test]
        public void Render_RecoverInteractable_LightsTheRecoverButtonAndRoutesItsClick()
        {
            int requests = 0;
            _view.RecoverRequested += () => requests++;
            Button recover = TacticalBattleViewTestFactory.FindButton(_view, "RecoverButton");

            _view.Render(CreateData(unitCount: 0, shotCount: 0));
            bool idle = recover.interactable;
            float idleAlpha = FindText("RecoverButtonLabel").color.a;
            _view.Render(CreateData(unitCount: 0, shotCount: 0, recoverInteractable: true));
            recover.onClick.Invoke();

            Assert.IsFalse(idle);
            Assert.Less(idleAlpha, 0.5f);
            Assert.IsTrue(recover.interactable);
            Assert.AreEqual(1f, FindText("RecoverButtonLabel").color.a, 0.001f);
            Assert.AreEqual(1, requests);
        }

        /// <summary>
        /// Renders a roster holding only ships and returns the bounds of its visible slots.
        /// </summary>
        /// <param name="shipCount">The number of ships to list.</param>
        /// <returns>The source-space bounds of every visible roster slot.</returns>
        private RectInt[] RenderShipRoster(int shipCount)
        {
            TacticalRosterSlotRenderData[] ships = Enumerable
                .Range(0, shipCount)
                .Select(index => new TacticalRosterSlotRenderData(
                    (index + 1).ToString(),
                    null,
                    false,
                    true
                ))
                .ToArray();
            _view.Render(
                CreateData(
                    unitCount: 0,
                    shotCount: 0,
                    roster: new TacticalRosterRenderData("Empire", ships, null)
                )
            );
            return _view
                .GetComponentsInChildren<TacticalRosterSlotView>(false)
                .Where(slot => slot.name != "RosterSlotTemplate")
                .Select(slot => UILayout.GetSourceRect(slot.transform as RectTransform))
                .ToArray();
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
        public void Render_Superlaser_ShowsTheChargeBarAndMakesRoomForItInTheHeader()
        {
            Button button = TacticalBattleViewTestFactory.FindButton(_view, "SuperlaserButton");
            RawImage bar = _view
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "SuperlaserBarImage");
            RawImage frame = _view
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "PortraitFrameImage");
            _view.Render(CreateData(0, 0));
            bool hiddenWithoutAStation = !button.gameObject.activeSelf;
            int frameTop = UILayout.GetSourceRect(frame.rectTransform).y;
            int pressCount = 0;
            _view.SuperlaserPressed += () => pressCount++;

            _view.Render(
                CreateData(
                    0,
                    0,
                    superlaser: new TacticalSuperlaserRenderData(
                        "Superlaser charging 50%",
                        0.5f,
                        false,
                        false
                    )
                )
            );
            bool chargingInteractable = button.interactable;
            int halfWidth = UILayout.GetSourceRect(bar.rectTransform).width;
            int loweredFrameTop = UILayout.GetSourceRect(frame.rectTransform).y;
            _view.Render(
                CreateData(
                    0,
                    0,
                    superlaser: new TacticalSuperlaserRenderData(
                        "Superlaser ready: click to aim",
                        1f,
                        true,
                        false
                    )
                )
            );
            button.onClick.Invoke();
            int fullWidth = UILayout.GetSourceRect(bar.rectTransform).width;
            _view.Render(CreateData(0, 0));

            Assert.IsTrue(hiddenWithoutAStation);
            Assert.IsFalse(chargingInteractable);
            Assert.AreEqual(
                UILayout.GetSourceRect(button.transform as RectTransform).width,
                fullWidth
            );
            Assert.AreEqual(fullWidth / 2, halfWidth);
            Assert.Greater(loweredFrameTop, frameTop);
            Assert.AreEqual(1, pressCount);
            Assert.IsTrue(button.interactable);
            Assert.AreEqual(
                "Superlaser ready: click to aim",
                FindText("SuperlaserButtonLabel").text
            );
            Assert.IsFalse(button.gameObject.activeSelf);
            Assert.AreEqual(frameTop, UILayout.GetSourceRect(frame.rectTransform).y);
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
        /// <param name="superlaser">The optional superlaser charge bar.</param>
        /// <param name="recoverInteractable">Whether the recover button accepts input.</param>
        /// <returns>The frame data.</returns>
        private static TacticalBattleRenderData CreateData(
            int unitCount,
            int shotCount,
            RectInt? selectionBox = null,
            TacticalPanelRenderData panel = null,
            TacticalRosterRenderData roster = null,
            TacticalSuperlaserRenderData superlaser = null,
            bool recoverInteractable = false
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
                roster,
                superlaser,
                recoverInteractable
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
