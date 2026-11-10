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
        public void PointerClick_OnUnit_SnapsTheCameraOntoItAndFollowsUntilAPan()
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

            Click(friendly, PointerEventData.InputButton.Left);
            Vector2 focusAtClick = _view.Scene.Focus;
            friendly.X += 15;
            friendly.Y += 5;
            _controller.Update(0f);
            Vector2 focusWhileFollowing = _view.Scene.Focus;
            _controller.Update(0.1f, pan);
            friendly.X += 15;
            _controller.Update(0f);

            Assert.AreEqual((float)(friendly.X - 30), focusAtClick.x, 0.01f);
            Assert.AreEqual((float)(friendly.Y - 5), focusAtClick.y, 0.01f);
            Assert.AreEqual((float)(friendly.X - 15), focusWhileFollowing.x, 0.01f);
            Assert.AreEqual((float)friendly.Y, focusWhileFollowing.y, 0.01f);
            Assert.Greater(Mathf.Abs(_view.Scene.Focus.x - (float)friendly.X), 0.5f);
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
        public void RosterSlot_Clicked_FocusesThatShipAndSideToggleListsTheEnemy()
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
            };
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
