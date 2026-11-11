using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Tactical
{
    [TestFixture]
    public class TacticalBattleControllerTests
    {
        private readonly List<string> _playedSfx = new List<string>();
        private GameObject _root;
        private TacticalBattleView _view;
        private TacticalBattleController _controller;
        private TacticalSpaceBattle _completedBattle;
        private int _completionCount;
        private int _dirtyCount;
        private TacticalSpaceBattle _activeBattle;

        /// <summary>
        /// Builds the view and controller under test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _playedSfx.Clear();
            _completedBattle = null;
            _completionCount = 0;
            _dirtyCount = 0;
            _view = TacticalBattleViewTestFactory.Create(out _root);
            _controller = new TacticalBattleController(
                _view,
                () => null,
                path => _playedSfx.Add(path),
                () => _dirtyCount++
            );
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
        public void Constructor_NullView_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TacticalBattleController(null, () => null, _ => { }, () => { })
            );
        }

        [Test]
        public void Begin_Battle_ShowsViewAndDrawsEveryUnit()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);

            Begin(battle);

            Assert.IsTrue(_controller.IsActive);
            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(2, CountActiveUnitViews());
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void PauseButton_Clicked_StopsAdvancingAndPlaysControlSound()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            Begin(battle);

            TacticalBattleViewTestFactory.FindButton(_view, "PauseButton").onClick.Invoke();
            _controller.Update(1f);

            Assert.IsTrue(_controller.IsPaused);
            Assert.AreEqual(0, battle.ElapsedSeconds, 0.0001);
            CollectionAssert.AreEqual(new[] { StrategyUISoundPaths.ControlPress }, _playedSfx);
        }

        [Test]
        public void Update_WeaponFire_PlaysWeaponFireSound()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            Begin(battle);

            _controller.Update(0.5f);

            Assert.IsTrue(
                _playedSfx.Any(path => TacticalBattleAudio.WeaponFirePaths.Contains(path)),
                "Expected a weapon-fire cue after the first volley."
            );
        }

        [Test]
        public void Update_UnavailableSounds_PlaysNothing()
        {
            _controller = new TacticalBattleController(
                _view,
                () => null,
                path => _playedSfx.Add(path),
                () => _dirtyCount++,
                _ => false
            );
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            Begin(battle);

            _controller.Update(0.5f);

            Assert.IsEmpty(_playedSfx);
        }

        [Test]
        public void SpeedButton_Clicked_AdvancesBattleFaster()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            Begin(battle);

            TacticalBattleViewTestFactory.FindButton(_view, "SpeedButton").onClick.Invoke();
            _controller.Update(0.5f);

            Assert.AreEqual(2f, _controller.Speed);
            Assert.AreEqual(1, battle.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void SpeedButton_ClickedThroughEveryStep_ReachesEightTimesThenWraps()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            Begin(battle);
            Button speedButton = TacticalBattleViewTestFactory.FindButton(_view, "SpeedButton");

            speedButton.onClick.Invoke();
            speedButton.onClick.Invoke();
            float fourTimes = _controller.Speed;
            speedButton.onClick.Invoke();
            float eightTimes = _controller.Speed;
            _controller.Update(0.5f);
            speedButton.onClick.Invoke();

            Assert.AreEqual(4f, fourTimes);
            Assert.AreEqual(8f, eightTimes);
            Assert.AreEqual(4, battle.ElapsedSeconds, 0.0001);
            Assert.AreEqual(1f, _controller.Speed);
        }

        [Test]
        public void PointerClick_OnFriendlyShip_SelectsIt()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);

            Click(friendly, PointerEventData.InputButton.Left);

            CollectionAssert.AreEqual(new[] { friendly }, _controller.Selection);
            Click(enemy, PointerEventData.InputButton.Left);
            Assert.IsEmpty(_controller.Selection);
        }

        [Test]
        public void PointerClick_OnEnemyShip_InspectsWithoutSelecting()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out TacticalCapitalShipUnit enemy);
            Begin(battle);

            Click(enemy, PointerEventData.InputButton.Left);
            _controller.Update(0f);

            Assert.IsEmpty(_controller.Selection);
            Assert.AreSame(enemy, _controller.Inspected);
            Assert.AreEqual(enemy.DisplayName, FindText("NameTextField").text);
        }

        [Test]
        public void PointerClick_OnFriendlyShip_BoxesItInItsSideColor()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);

            Click(friendly, PointerEventData.InputButton.Left);
            _controller.Update(0f);

            Assert.IsTrue(_view.Scene.TryGetSelectionBox(friendly, out _, out Color color));
            Assert.IsFalse(_view.Scene.TryGetSelectionBox(enemy, out _, out _));
            Assert.AreEqual(
                TacticalBattleProjector.GetSideColor(battle, friendly.Side, null, null),
                color
            );
        }

        [Test]
        public void PointerClick_OnEnemyShip_BoxesItInTheEnemySideColor()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);

            Click(enemy, PointerEventData.InputButton.Left);
            _controller.Update(0f);

            Assert.IsTrue(_view.Scene.TryGetSelectionBox(enemy, out _, out Color color));
            Assert.IsFalse(_view.Scene.TryGetSelectionBox(friendly, out _, out _));
            Assert.AreEqual(
                TacticalBattleProjector.GetSideColor(battle, enemy.Side, null, null),
                color
            );
            Assert.AreNotEqual(
                TacticalBattleProjector.GetSideColor(battle, friendly.Side, null, null),
                color
            );
        }

        [Test]
        public void End_WithAShipSelected_ClearsItsBox()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            Click(friendly, PointerEventData.InputButton.Left);
            _controller.Update(0f);
            int shown = _view.Scene.SelectionBoxCount;

            _controller.End();

            Assert.AreEqual(1, shown);
            Assert.AreEqual(0, _view.Scene.SelectionBoxCount);
        }

        [Test]
        public void PointerRightClick_EmptySpaceWithSelection_IssuesMoveOrder()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            Click(friendly, PointerEventData.InputButton.Left);
            // Beside the selected ship, inside the arena and clear of the test enemy, whose huge
            // hull covers the far corner.
            Vector2 destination = ToSource(battle, friendly.X, friendly.Y + 30);

            Press(destination, PointerEventData.InputButton.Right);

            Assert.AreEqual(TacticalShipOrder.Move, friendly.Order);
            Assert.AreEqual(friendly.X, friendly.DestinationX, 0.5);
            Assert.AreEqual(friendly.Y + 30, friendly.DestinationY, 0.5);
        }

        [Test]
        public void PointerRightClick_WithAShipOnARaisedDeck_SendsItToWhereThePointerShowsItsDeck()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            friendly.Altitude = 12;
            Begin(battle);
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            // The point on screen where a place beside the ship, on the ship's own deck, shows.
            Assert.IsTrue(
                _view.Scene.TryProject(
                    friendly.X,
                    friendly.Y + 30,
                    _view.GetArenaSourceRect(),
                    out Vector2 destination,
                    friendly.Altitude
                )
            );

            Press(destination, PointerEventData.InputButton.Right);

            Assert.AreEqual(TacticalShipOrder.Move, friendly.Order);
            Assert.AreEqual(friendly.X, friendly.DestinationX, 0.5);
            Assert.AreEqual(friendly.Y + 30, friendly.DestinationY, 0.5);
        }

        [Test]
        public void PointerClick_OnTheEdgeOfADrawnHull_StillPicksTheShip()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out TacticalCapitalShipUnit enemy);
            Begin(battle);
            Assert.IsTrue(
                _view.Scene.TryProjectUnit(
                    enemy,
                    _view.GetArenaSourceRect(),
                    out Vector2 center,
                    out float diameter
                )
            );

            Press(center + new Vector2(diameter * 0.45f, 0f), PointerEventData.InputButton.Left);

            Assert.AreSame(enemy, _controller.Inspected);
        }

        [Test]
        public void CycleShips_Input_SelectsAndFramesTheNextFriendlyShip()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            TacticalCameraInput cycle = new TacticalCameraInput(
                Vector2.zero,
                0f,
                0f,
                0f,
                false,
                true,
                false
            );

            _controller.Update(0f, cycle);

            Assert.AreEqual(1, _controller.Selection.Count);
            Assert.AreSame(friendly, _controller.Selection[0]);
            Assert.AreSame(friendly, _controller.Inspected);
            Assert.AreEqual((float)friendly.X, _view.Scene.Focus.x, 0.001f);
            Assert.AreEqual((float)friendly.Y, _view.Scene.Focus.y, 0.001f);
        }

        [Test]
        public void PointerClick_OnAUnitAlreadyPicked_ZoomsToItAndFollowsUntilAPan()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            TacticalCameraInput pan = new TacticalCameraInput(
                Vector2.right,
                0f,
                0f,
                0f,
                false,
                false,
                false
            );
            Vector2 focusAtStart = _view.Scene.Focus;
            float distanceAtStart = _view.Scene.Distance;

            Click(friendly, PointerEventData.InputButton.Left);
            _controller.Update(0f);
            Vector2 focusAfterOneClick = _view.Scene.Focus;
            float distanceAfterOneClick = _view.Scene.Distance;
            Click(friendly, PointerEventData.InputButton.Left);
            Vector2 focusAtSecondClick = _view.Scene.Focus;
            float distanceAtSecondClick = _view.Scene.Distance;
            friendly.X += 15;
            friendly.Y += 5;
            _controller.Update(0f);
            Vector2 focusWhileFollowing = _view.Scene.Focus;
            _controller.Update(0.1f, pan);
            friendly.X += 15;
            _controller.Update(0f);

            // The first click only picks the ship; the camera stays where it was.
            Assert.AreEqual(focusAtStart, focusAfterOneClick);
            Assert.AreEqual(distanceAtStart, distanceAfterOneClick);
            Assert.AreEqual((float)(friendly.X - 30), focusAtSecondClick.x, 0.01f);
            Assert.AreEqual((float)(friendly.Y - 5), focusAtSecondClick.y, 0.01f);
            Assert.Less(distanceAtSecondClick, distanceAtStart);
            Assert.AreEqual((float)(friendly.X - 15), focusWhileFollowing.x, 0.01f);
            Assert.AreEqual((float)friendly.Y, focusWhileFollowing.y, 0.01f);
            Assert.Greater(Mathf.Abs(_view.Scene.Focus.x - (float)friendly.X), 0.5f);
        }

        [Test]
        public void PointerClick_OnAnEnemyAlreadyInspected_ZoomsToIt()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out _,
                out TacticalCapitalShipUnit enemy,
                defenderHull: 100
            );
            Begin(battle);
            float distanceAtStart = _view.Scene.Distance;

            Click(enemy, PointerEventData.InputButton.Left);
            float distanceAfterOneClick = _view.Scene.Distance;
            Click(enemy, PointerEventData.InputButton.Left);

            Assert.AreEqual(distanceAtStart, distanceAfterOneClick);
            Assert.Less(_view.Scene.Distance, distanceAtStart);
            Assert.AreEqual((float)enemy.X, _view.Scene.Focus.x, 0.001f);
            Assert.AreEqual((float)enemy.Y, _view.Scene.Focus.y, 0.001f);
            Assert.AreSame(enemy, _controller.Inspected);
            Assert.IsEmpty(_controller.Selection);
        }

        [Test]
        public void PauseInput_Space_TogglesPlayback()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            Begin(battle);
            TacticalCameraInput pause = new TacticalCameraInput(
                Vector2.zero,
                0f,
                0f,
                0f,
                false,
                false,
                true
            );

            _controller.Update(0f, pause);
            bool pausedAfterFirstPress = _controller.IsPaused;
            _controller.Update(0f, pause);

            Assert.IsTrue(pausedAfterFirstPress);
            Assert.IsFalse(_controller.IsPaused);
        }

        [Test]
        public void RosterSlot_Clicked_PicksThatShipAndSideToggleListsTheEnemy()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);

            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);
            Assert.AreSame(friendly, _controller.Inspected);
            Assert.AreEqual(1, _controller.Selection.Count);

            TacticalBattleViewTestFactory.FindButton(_view, "RosterSideButton").onClick.Invoke();
            _controller.Update(0f);
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            Assert.AreEqual("Alliance", FindText("RosterSideButtonLabel").text);
            Assert.AreSame(enemy, _controller.Inspected);
            Assert.IsEmpty(_controller.Selection);
        }

        [Test]
        public void RosterSlot_ClickedOnce_PicksTheShipWithoutMovingTheCamera()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            Vector2 focusAtStart = _view.Scene.Focus;
            float distanceAtStart = _view.Scene.Distance;

            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            Assert.AreSame(friendly, _controller.Inspected);
            CollectionAssert.AreEqual(new[] { friendly }, _controller.Selection);
            Assert.AreEqual(focusAtStart, _view.Scene.Focus);
            Assert.AreEqual(distanceAtStart, _view.Scene.Distance);
        }

        [Test]
        public void RosterSlot_ClickedAgainOnTheShipAlreadyPicked_ZoomsToIt()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            float distanceAtStart = _view.Scene.Distance;

            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            CollectionAssert.AreEqual(new[] { friendly }, _controller.Selection);
            Assert.AreEqual((float)friendly.X, _view.Scene.Focus.x, 0.001f);
            Assert.AreEqual((float)friendly.Y, _view.Scene.Focus.y, 0.001f);
            Assert.Less(_view.Scene.Distance, distanceAtStart);
        }

        [Test]
        public void RosterSlot_ShiftClicked_SelectsEveryShipFromTheLastPickThroughIt()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                4,
                0,
                out List<TacticalCapitalShipUnit> ships,
                out _,
                out _
            );
            Begin(battle);

            FindRosterSlot(1).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: true, add: false));
            FindRosterSlot(3).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            CollectionAssert.AreEquivalent(
                new[] { ships[1], ships[2], ships[3] },
                _controller.Selection
            );
            Assert.AreSame(ships[3], _controller.Inspected);
        }

        [Test]
        public void RosterSlot_ShiftClickedBackward_SelectsTheSameRun()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                4,
                0,
                out List<TacticalCapitalShipUnit> ships,
                out _,
                out _
            );
            Begin(battle);

            FindRosterSlot(2).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: true, add: false));
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            CollectionAssert.AreEquivalent(
                new[] { ships[0], ships[1], ships[2] },
                _controller.Selection
            );
        }

        [Test]
        public void RosterSlot_ShiftClickedOnASquadron_RunsOnFromTheShipsIntoTheSquadrons()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                2,
                3,
                out List<TacticalCapitalShipUnit> ships,
                out List<TacticalStarfighterUnit> squadrons,
                out _
            );
            Begin(battle);

            FindRosterSlot(1).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: true, add: false));
            FindRosterSlot(3).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            CollectionAssert.AreEquivalent(
                new TacticalBattleUnit[] { ships[1], squadrons[0], squadrons[1] },
                _controller.Selection
            );
        }

        [Test]
        public void RosterSlot_ControlClicked_AddsTheUnitAndTakesItOutAgain()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                3,
                1,
                out List<TacticalCapitalShipUnit> ships,
                out List<TacticalStarfighterUnit> squadrons,
                out _
            );
            Begin(battle);

            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: false, add: true));
            FindRosterSlot(2).GetComponent<Button>().onClick.Invoke();
            FindRosterSlot(3).GetComponent<Button>().onClick.Invoke();
            List<TacticalBattleUnit> afterAdding = _controller.Selection.ToList();
            FindRosterSlot(2).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            CollectionAssert.AreEquivalent(
                new TacticalBattleUnit[] { ships[0], ships[2], squadrons[0] },
                afterAdding
            );
            CollectionAssert.AreEquivalent(
                new TacticalBattleUnit[] { ships[0], squadrons[0] },
                _controller.Selection
            );
        }

        [Test]
        public void RosterSlot_ShiftClickedOnAnEnemy_InspectsItAndLeavesTheSelectionAlone()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                3,
                0,
                out List<TacticalCapitalShipUnit> ships,
                out _,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: true, add: false));
            FindRosterSlot(1).GetComponent<Button>().onClick.Invoke();

            TacticalBattleViewTestFactory.FindButton(_view, "RosterSideButton").onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: true, add: false));
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            CollectionAssert.AreEquivalent(new[] { ships[0], ships[1] }, _controller.Selection);
            Assert.AreSame(enemy, _controller.Inspected);
        }

        [Test]
        public void Scroll_SmallTrackpadDelta_ZoomsInProportionToTheDelta()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            Vector2 point = ToSource(_activeBattle, friendly.X, friendly.Y);
            float distanceAtStart = _view.Scene.Distance;

            _view.OnScroll(CreateScroll(point, 0.1f));
            float distanceAfterNudge = _view.Scene.Distance;
            _view.OnScroll(CreateScroll(point, 1f));
            float distanceAfterNotch = _view.Scene.Distance;

            // A wheel notch zooms one step; a trackpad's small delta zooms a tenth of one.
            Assert.AreEqual(Mathf.Pow(0.85f, 0.1f), distanceAfterNudge / distanceAtStart, 0.001f);
            Assert.AreEqual(0.85f, distanceAfterNotch / distanceAfterNudge, 0.001f);
        }

        [Test]
        public void PointerClick_WithControlHeld_AddsTheShipAndTakesItOutAgain()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                2,
                0,
                out List<TacticalCapitalShipUnit> ships,
                out _,
                out _
            );
            Begin(battle);

            Click(ships[0], PointerEventData.InputButton.Left);
            _controller.Update(0f, HoldSelectionKeys(extend: false, add: true));
            Click(ships[1], PointerEventData.InputButton.Left);
            List<TacticalBattleUnit> afterAdding = _controller.Selection.ToList();
            Click(ships[1], PointerEventData.InputButton.Left);

            CollectionAssert.AreEquivalent(new[] { ships[0], ships[1] }, afterAdding);
            CollectionAssert.AreEqual(new[] { ships[0] }, _controller.Selection);
        }

        [Test]
        public void PointerRightClick_WithARunOfShipsSelected_OrdersThemAll()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                3,
                1,
                out List<TacticalCapitalShipUnit> ships,
                out List<TacticalStarfighterUnit> squadrons,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);
            FindRosterSlot(0).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f, HoldSelectionKeys(extend: true, add: false));
            FindRosterSlot(3).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);

            Click(enemy, PointerEventData.InputButton.Right);

            Assert.AreEqual(4, _controller.Selection.Count);
            Assert.IsTrue(ships.All(ship => ship.Order == TacticalShipOrder.Attack));
            Assert.IsTrue(ships.All(ship => ReferenceEquals(ship.Target, enemy)));
            Assert.IsTrue(squadrons[0].IsDirected);
            Assert.AreSame(enemy, squadrons[0].Target);
        }

        [Test]
        public void RecoverButton_ClickedWithASquadronSelected_RecallsItToAHangar()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                1,
                1,
                out List<TacticalCapitalShipUnit> ships,
                out List<TacticalStarfighterUnit> squadrons,
                out _
            );
            ships[0].Ship.StarfighterCapacity = 1;
            Begin(battle);
            Button recover = TacticalBattleViewTestFactory.FindButton(_view, "RecoverButton");
            bool offeredWithNothingSelected = recover.interactable;

            FindRosterSlot(1).GetComponent<Button>().onClick.Invoke();
            _controller.Update(0f);
            bool offered = recover.interactable;
            recover.onClick.Invoke();
            _controller.Update(0f);

            Assert.IsFalse(offeredWithNothingSelected);
            Assert.IsTrue(offered);
            Assert.AreSame(ships[0], squadrons[0].RecoveryShip);
            Assert.IsFalse(recover.interactable);
            StringAssert.StartsWith("Orders: Return to", FindText("OrdersTextField").text);
        }

        [Test]
        public void PointerRightClick_OnAFriendlyShipWithASquadronSelected_PostsItToScreenThatShip()
        {
            TacticalSpaceBattle battle = CreateFleetBattle(
                2,
                1,
                out List<TacticalCapitalShipUnit> ships,
                out List<TacticalStarfighterUnit> squadrons,
                out _
            );
            ships[0].Ship.StarfighterCapacity = 1;
            ships[1].Ship.StarfighterCapacity = 1;
            Begin(battle);
            FindRosterSlot(2).GetComponent<Button>().onClick.Invoke();
            bool selected = _controller.Selection.Contains(squadrons[0]);
            // Clear of the hull it is sent to, so the click lands on the ship and not on it.
            squadrons[0].X = 0;
            squadrons[0].Y = 0;
            _controller.Update(0f);

            Click(ships[1], PointerEventData.InputButton.Right);

            Assert.IsTrue(selected);
            Assert.AreSame(ships[1], squadrons[0].ScreenedShip);
            Assert.IsNull(squadrons[0].RecoveryShip);
        }

        [Test]
        public void TargetPriorityButton_Clicked_FlipsTheSelectedShipsPriority()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);
            Click(friendly, PointerEventData.InputButton.Left);
            TacticalTargetPriority initial = friendly.TargetPriority;
            TacticalTargetPriority enemyInitial = enemy.TargetPriority;

            TacticalBattleViewTestFactory
                .FindButton(_view, "TargetPriorityButton")
                .onClick.Invoke();
            _controller.Update(0f);

            Assert.AreNotEqual(initial, friendly.TargetPriority);
            Assert.AreEqual(enemyInitial, enemy.TargetPriority);
            Assert.AreEqual(
                TacticalBattleProjector.DescribeTargetPriority(friendly),
                FindText("TargetPriorityButtonLabel").text
            );
        }

        [Test]
        public void PointerRightClick_OnEnemyWithSelection_IssuesAttackOrder()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Begin(battle);
            Click(friendly, PointerEventData.InputButton.Left);

            Click(enemy, PointerEventData.InputButton.Right);

            Assert.AreEqual(TacticalShipOrder.Attack, friendly.Order);
            Assert.AreSame(enemy, friendly.Target);
        }

        [Test]
        public void PointerDrag_BoxAroundFriendlyShip_SelectsIt()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            Vector2 center = ToSource(battle, friendly.X, friendly.Y);
            Vector2 start = center - new Vector2(30f, 30f);
            Vector2 end = center + new Vector2(30f, 30f);

            _view.OnPointerDown(CreateEvent(start, PointerEventData.InputButton.Left));
            _view.OnDrag(CreateEvent(end, PointerEventData.InputButton.Left));
            _controller.Update(0f);
            _view.OnPointerUp(CreateEvent(end, PointerEventData.InputButton.Left));

            CollectionAssert.AreEqual(new[] { friendly }, _controller.Selection);
        }

        [Test]
        public void RetreatButton_PlayerCanWithdraw_OrdersWithdrawal()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _, attackerCanWithdraw: true);
            Begin(battle);

            TacticalBattleViewTestFactory.FindButton(_view, "RetreatButton").onClick.Invoke();

            Assert.IsTrue(battle.HasOrderedWithdrawal(CombatSide.Attacker));
        }

        [Test]
        public void AutoButton_Clicked_HandsPlayerSideToSimulationAndClearsSelection()
        {
            TacticalSpaceBattle battle = CreateBattle(out TacticalCapitalShipUnit friendly, out _);
            Begin(battle);
            Click(friendly, PointerEventData.InputButton.Left);

            TacticalBattleViewTestFactory.FindButton(_view, "AutoButton").onClick.Invoke();

            Assert.IsTrue(battle.IsAutomatic(CombatSide.Attacker));
            Assert.IsEmpty(_controller.Selection);
        }

        [Test]
        public void Update_BattleCompletes_InvokesCompletionOnceAfterDelay()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _, defenderHull: 20);
            Begin(battle);

            for (int frame = 0; frame < 40 && _completionCount == 0; frame++)
                _controller.Update(0.5f);

            Assert.IsTrue(battle.IsComplete);
            Assert.AreEqual(1, _completionCount);
            Assert.AreSame(battle, _completedBattle);
            _controller.Update(0.5f);
            Assert.AreEqual(1, _completionCount);
        }

        [Test]
        public void Update_UnitDestroyed_ShowsAnExplosionInTheScene()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _, defenderHull: 20);
            Begin(battle);

            for (int frame = 0; frame < 40 && !battle.IsComplete; frame++)
                _controller.Update(0.5f);

            Assert.IsTrue(battle.IsComplete);
            Assert.AreEqual(1, _view.Scene.GetActiveExplosionPositions().Count());
        }

        [Test]
        public void End_ActiveBattle_HidesViewAndReleasesBattle()
        {
            Begin(CreateBattle(out _, out _));

            _controller.End();

            Assert.IsFalse(_controller.IsActive);
            Assert.IsFalse(_view.gameObject.activeSelf);
            Assert.AreEqual(0, CountActiveUnitViews());
        }

        [Test]
        public void TryCancel_ActiveBattle_TogglesPause()
        {
            Begin(CreateBattle(out _, out _));

            bool handled = _controller.TryCancel();

            Assert.IsTrue(handled);
            Assert.IsTrue(_controller.IsPaused);
            Assert.IsFalse(
                new TacticalBattleController(_view, () => null, _ => { }, () => { }).TryCancel()
            );
        }

        [Test]
        public void SuperlaserButton_ArmedThenEnemyShipClicked_FiresAndDisarms()
        {
            TacticalSpaceBattle battle = CreateStationBattle(
                playerHasStation: true,
                out _,
                out TacticalCapitalShipUnit enemy,
                out _
            );
            Begin(battle);
            TacticalBattleStation station = battle.Stations.Single();

            TacticalBattleViewTestFactory.FindButton(_view, "SuperlaserButton").onClick.Invoke();
            bool armed = _controller.IsSuperlaserArmed;
            _controller.Update(0f);
            string armedHint = FindText("SelectionTextField").text;
            Click(enemy, PointerEventData.InputButton.Left);

            Assert.IsTrue(armed);
            StringAssert.StartsWith("Superlaser armed.", armedHint);
            Assert.AreSame(enemy, station.StrikeTarget);
            Assert.IsFalse(_controller.IsSuperlaserArmed);
            Assert.IsNull(_controller.Inspected);
        }

        [Test]
        public void RightClick_WhileSuperlaserArmed_DisarmsWithoutGivingAnOrder()
        {
            TacticalSpaceBattle battle = CreateStationBattle(
                playerHasStation: true,
                out TacticalCapitalShipUnit friendly,
                out _,
                out _
            );
            Begin(battle);
            Click(friendly, PointerEventData.InputButton.Left);
            TacticalBattleViewTestFactory.FindButton(_view, "SuperlaserButton").onClick.Invoke();

            Press(
                ToSource(battle, friendly.X + 10, friendly.Y),
                PointerEventData.InputButton.Right
            );
            bool disarmedByRightClick = !_controller.IsSuperlaserArmed;
            TacticalBattleViewTestFactory.FindButton(_view, "SuperlaserButton").onClick.Invoke();
            bool cancelHandled = _controller.TryCancel();

            Assert.IsTrue(disarmedByRightClick);
            Assert.AreEqual(TacticalShipOrder.None, friendly.Order);
            Assert.IsTrue(battle.Stations.Single().IsSuperlaserReady);
            Assert.IsTrue(cancelHandled);
            Assert.IsFalse(_controller.IsSuperlaserArmed);
            Assert.IsFalse(_controller.IsPaused);
        }

        [Test]
        public void RightClick_OnEnemyStation_SendsTheSelectedSquadronToItsTrench()
        {
            TacticalSpaceBattle battle = CreateStationBattle(
                playerHasStation: false,
                out _,
                out _,
                out TacticalStarfighterUnit squadron
            );
            Begin(battle);
            TacticalBattleStation station = battle.Stations.Single();
            FindRosterSlot(1).GetComponent<Button>().onClick.Invoke();
            bool selected = _controller.Selection.Contains(squadron);
            _view.Scene.LookToward(station);
            Assert.IsTrue(
                _view.Scene.TryProjectStation(
                    station,
                    _view.GetArenaSourceRect(),
                    out Vector2 stationPoint,
                    out float stationDiameter
                ),
                "The station must be in front of the camera."
            );

            Press(stationPoint, PointerEventData.InputButton.Right);

            Assert.IsTrue(selected);
            Assert.Greater(stationDiameter, 0f);
            Assert.AreSame(station, squadron.TrenchRunTarget);
        }

        [Test]
        public void GetTrenchRunCutscenePath_ThemeListsTheShipType_ReturnsTheOutcomeMovie()
        {
            FactionTheme theme = new FactionTheme();
            theme.TrenchRunCutscenes.Add(
                new TrenchRunCutscene
                {
                    ShipTypeID = "station",
                    SuccessPath = "Movies/success",
                    FailurePath = "Movies/fail",
                }
            );

            Assert.AreEqual(
                "Movies/success",
                TacticalBattleController.GetTrenchRunCutscenePath(theme, "station", true)
            );
            Assert.AreEqual(
                "Movies/fail",
                TacticalBattleController.GetTrenchRunCutscenePath(theme, "station", false)
            );
            Assert.IsNull(TacticalBattleController.GetTrenchRunCutscenePath(theme, "other", true));
            Assert.IsNull(TacticalBattleController.GetTrenchRunCutscenePath(null, "station", true));
        }

        /// <summary>
        /// Creates a battle with a battle station on one side: the player's ship and torpedo
        /// squadron against a passive enemy ship.
        /// </summary>
        /// <param name="playerHasStation">Whether the station fights for the player or the enemy.</param>
        /// <param name="friendly">Receives the player's ship.</param>
        /// <param name="enemy">Receives the enemy ship.</param>
        /// <param name="squadron">Receives the player's torpedo squadron.</param>
        /// <returns>The created battle.</returns>
        private TacticalSpaceBattle CreateStationBattle(
            bool playerHasStation,
            out TacticalCapitalShipUnit friendly,
            out TacticalCapitalShipUnit enemy,
            out TacticalStarfighterUnit squadron
        )
        {
            CapitalShip attackerShip = CreateShip("attacker", 100, 0);
            // A small enemy hull leaves the station behind it clear to click.
            CapitalShip defenderShip = CreateShip("defender", playerHasStation ? 100000 : 100, 0);
            CapitalShip station = CreateShip("station", 25000, 0);
            station.TypeID = "station";
            station.CanDestroyPlanets = true;
            Starfighter fighter = new Starfighter
            {
                InstanceID = "x-wing",
                DisplayName = "x-wing",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                Hyperdrive = 1,
                SublightSpeed = 8,
                Agility = 4,
                LaserCannon = 5,
                LaserRange = 12,
                Torpedoes = 4,
                TorpedoRange = 7,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(7),
                playerHasStation ? new[] { attackerShip, station } : new[] { attackerShip },
                new[] { fighter },
                playerHasStation ? new[] { defenderShip } : new[] { defenderShip, station },
                new List<Starfighter>(),
                false,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            friendly = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == attackerShip);
            enemy = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == defenderShip);
            squadron = battle.Units.OfType<TacticalStarfighterUnit>().Single();
            _activeBattle = battle;
            return battle;
        }

        /// <summary>
        /// Creates a battle in which the player commands several ships and several squadrons
        /// against one passive enemy ship.
        /// </summary>
        /// <param name="shipCount">The number of player ships.</param>
        /// <param name="squadronCount">The number of player squadrons.</param>
        /// <param name="ships">Receives the player's ships in roster order.</param>
        /// <param name="squadrons">Receives the player's squadrons in roster order.</param>
        /// <param name="enemy">Receives the enemy ship.</param>
        /// <returns>The created battle.</returns>
        private TacticalSpaceBattle CreateFleetBattle(
            int shipCount,
            int squadronCount,
            out List<TacticalCapitalShipUnit> ships,
            out List<TacticalStarfighterUnit> squadrons,
            out TacticalCapitalShipUnit enemy
        )
        {
            CapitalShip[] attackerShips = Enumerable
                .Range(0, shipCount)
                .Select(index => CreateShip($"ship{index}", 100, 10))
                .ToArray();
            Starfighter[] fighters = Enumerable
                .Range(0, squadronCount)
                .Select(index => new Starfighter
                {
                    InstanceID = $"squadron{index}",
                    DisplayName = $"squadron{index}",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Hyperdrive = 1,
                    SublightSpeed = 8,
                    Agility = 4,
                    LaserCannon = 5,
                    LaserRange = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                })
                .ToArray();
            CapitalShip defenderShip = CreateShip("defender", 100000, 0);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(7),
                attackerShips,
                fighters,
                new[] { defenderShip },
                new List<Starfighter>(),
                false,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            ships = attackerShips
                .Select(ship =>
                    battle.Units.OfType<TacticalCapitalShipUnit>().Single(unit => unit.Ship == ship)
                )
                .ToList();
            squadrons = fighters
                .Select(fighter =>
                    battle
                        .Units.OfType<TacticalStarfighterUnit>()
                        .Single(unit => unit.Fighter == fighter)
                )
                .ToList();
            enemy = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == defenderShip);
            _activeBattle = battle;
            return battle;
        }

        /// <summary>
        /// Returns a frame of keyboard input with only the selection keys held.
        /// </summary>
        /// <param name="extend">Whether the key that selects a run of units is held.</param>
        /// <param name="add">Whether the key that adds one unit is held.</param>
        /// <returns>The keyboard input.</returns>
        private static TacticalCameraInput HoldSelectionKeys(bool extend, bool add)
        {
            return new TacticalCameraInput(
                Vector2.zero,
                0f,
                0f,
                0f,
                false,
                false,
                false,
                extend,
                add
            );
        }

        /// <summary>
        /// Begins presenting a battle through the controller.
        /// </summary>
        /// <param name="battle">The battle to present.</param>
        private void Begin(TacticalSpaceBattle battle)
        {
            _controller.Begin(
                battle,
                null,
                "FNEMP1",
                "FNALL1",
                "Empire",
                "Alliance",
                completed =>
                {
                    _completionCount++;
                    _completedBattle = completed;
                }
            );
            // Back the camera out to the play-space limit so both fleets are on screen, let the
            // hyperspace arrival finish, then redraw so the overlay reflects that view.
            _view.Scene.Zoom(-100f);
            _view.Scene.Advance(10f);
            _controller.Update(0f);
        }

        /// <summary>
        /// Finds the active roster slot at an index, skipping the template.
        /// </summary>
        /// <param name="index">The slot index in render order.</param>
        /// <returns>The slot view.</returns>
        private TacticalRosterSlotView FindRosterSlot(int index)
        {
            return _view
                .GetComponentsInChildren<TacticalRosterSlotView>(true)
                .Where(slot => slot.name != "RosterSlotTemplate" && slot.gameObject.activeSelf)
                .ElementAt(index);
        }

        /// <summary>
        /// Presses and releases a pointer button over a unit.
        /// </summary>
        /// <param name="unit">The unit to click.</param>
        /// <param name="button">The pointer button.</param>
        private void Click(TacticalBattleUnit unit, PointerEventData.InputButton button)
        {
            Vector2 point = ToSource(_activeBattle, unit.X, unit.Y);
            _view.OnPointerDown(CreateEvent(point, button));
            _view.OnPointerUp(CreateEvent(point, button));
        }

        /// <summary>
        /// Presses and releases a pointer button at a source-space point.
        /// </summary>
        /// <param name="point">The source-space point.</param>
        /// <param name="button">The pointer button.</param>
        private void Press(Vector2 point, PointerEventData.InputButton button)
        {
            _view.OnPointerDown(CreateEvent(point, button));
            _view.OnPointerUp(CreateEvent(point, button));
        }

        /// <summary>
        /// Converts a battle position into a source-space point on the view.
        /// </summary>
        /// <param name="battle">The battle being presented.</param>
        /// <param name="x">The battle horizontal coordinate.</param>
        /// <param name="y">The battle vertical coordinate.</param>
        /// <returns>The source-space point.</returns>
        private Vector2 ToSource(TacticalSpaceBattle battle, double x, double y)
        {
            Assert.IsTrue(
                _view.Scene.TryProject(x, y, _view.GetArenaSourceRect(), out Vector2 point),
                "The battle position must be in front of the camera."
            );
            return point;
        }

        /// <summary>
        /// Finds a named label within the view.
        /// </summary>
        /// <param name="objectName">The label object name.</param>
        /// <returns>The matching label.</returns>
        private TMPro.TextMeshProUGUI FindText(string objectName)
        {
            return _view
                .GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                .Single(text => text.name == objectName);
        }

        /// <summary>
        /// Counts the active pooled unit views under the view.
        /// </summary>
        /// <returns>The active unit view count.</returns>
        private int CountActiveUnitViews()
        {
            return _view
                .GetComponentsInChildren<TacticalUnitView>(true)
                .Count(unitView => unitView.gameObject.activeSelf);
        }

        /// <summary>
        /// Creates a wheel event at a source-space point on the view.
        /// </summary>
        /// <param name="sourcePoint">The source-space point.</param>
        /// <param name="delta">The vertical scroll delta.</param>
        /// <returns>The pointer event.</returns>
        private static PointerEventData CreateScroll(Vector2 sourcePoint, float delta)
        {
            PointerEventData eventData = CreateEvent(
                sourcePoint,
                PointerEventData.InputButton.Left
            );
            eventData.scrollDelta = new Vector2(0f, delta);
            return eventData;
        }

        /// <summary>
        /// Creates a pointer event at a source-space point on the view.
        /// </summary>
        /// <param name="sourcePoint">The source-space point.</param>
        /// <param name="button">The pointer button.</param>
        /// <returns>The pointer event.</returns>
        private static PointerEventData CreateEvent(
            Vector2 sourcePoint,
            PointerEventData.InputButton button
        )
        {
            return new PointerEventData(null)
            {
                position = TacticalBattleViewTestFactory.ToScreenPoint(sourcePoint),
                button = button,
            };
        }

        /// <summary>
        /// Creates a battle between one armed player ship and one passive enemy ship.
        /// </summary>
        /// <param name="friendly">Receives the player's ship.</param>
        /// <param name="enemy">Receives the enemy ship.</param>
        /// <param name="attackerCanWithdraw">Whether the player can withdraw.</param>
        /// <param name="defenderHull">The enemy hull strength.</param>
        /// <returns>The created battle.</returns>
        private TacticalSpaceBattle CreateBattle(
            out TacticalCapitalShipUnit friendly,
            out TacticalCapitalShipUnit enemy,
            bool attackerCanWithdraw = false,
            int defenderHull = 100000
        )
        {
            CapitalShip attackerShip = CreateShip("attacker", 100, 10);
            CapitalShip defenderShip = CreateShip("defender", defenderHull, 0);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(7),
                new[] { attackerShip },
                new List<Starfighter>(),
                new[] { defenderShip },
                new List<Starfighter>(),
                attackerCanWithdraw,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            friendly = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == attackerShip);
            enemy = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == defenderShip);
            _activeBattle = battle;
            return battle;
        }

        /// <summary>
        /// Creates combat parameters without automatic retreat.
        /// </summary>
        /// <returns>The combat parameters.</returns>
        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AdmiralLeadershipDivisor = 10,
                CommanderCombatDivisor = 20,
                LaserCannonCapitalDamageMultiplier = 1.0 / 6.0,
                AutoResolveFighterWeaponRechargeMultiplier = 3.751,
                AutoResolveMaximumIterations = 4096,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0,
                AutoResolveMinimumManeuverRatio = 0.1,
                AutoResolveTargetScanDivisor = 3,
                AutoResolveStartingDistance = 24,
                AutoResolveWithdrawalDistance = 10,
                AutoResolveComponentDamageInterval = 1,
                AutoResolveComponentDamageRollMaximum = 10,
                AutoResolveComponentDelayMinimum = 30,
                AutoResolveComponentDelayMaximum = 50,
                AutoResolveComponentDelayRecovery = 1,
            }.WithShippedTacticalSettings();
        }

        /// <summary>
        /// Creates a capital ship with one forward turbolaser lane.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <param name="hull">The hull strength.</param>
        /// <param name="weaponStrength">The forward turbolaser strength, or zero for unarmed.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateShip(string instanceId, int hull, int weaponStrength)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                Hyperdrive = 1,
                SublightSpeed = 3,
                Maneuverability = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                WeaponRecharge = Math.Max(weaponStrength, 0),
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = weaponStrength;
            if (weaponStrength > 0)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 150;
            return ship;
        }
    }
}
