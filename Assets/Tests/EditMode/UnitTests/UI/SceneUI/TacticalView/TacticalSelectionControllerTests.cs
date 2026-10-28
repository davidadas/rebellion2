using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalSelectionControllerTests
    {
        private GameObject _instance;
        private TacticalSelectionController _controller;
        private UnityEngine.Camera _camera;
        private Dictionary<SpaceCombatUnit, Transform> _presentations;

        /// <summary>
        /// Creates an isolated generated tactical root and simple measured hulls.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            if (_instance != null)
                UnityEngine.Object.DestroyImmediate(_instance);
            _instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _controller = _instance.GetComponent<TacticalSelectionController>();
            _instance.GetComponent<TacticalViewController>().enabled = false;
            _camera = _instance.GetComponent<TacticalViewController>().BattleCamera;
            _camera.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -40f),
                Quaternion.identity
            );
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1000f;
            _presentations = new Dictionary<SpaceCombatUnit, Transform>();
        }

        /// <summary>
        /// Releases the scene-owned model roots and selection overlay.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_instance);
        }

        /// <summary>
        /// Checks Control+A and Command+A select only active, deployed combatants owned by the viewer.
        /// </summary>
        /// <returns>The two primary-modifier input sequences and resulting selection updates.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_PrimaryModifierA_SelectsAllOwnedVisibleCombatants()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
                SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
                SpaceCombatUnit enemy = AddHull(new Vector3(0f, 10f, 0f));
                SpaceCombatUnit carried = AddHull(new Vector3(0f, -10f, 0f));
                left.OwnerInstanceID = "PLAYER";
                right.OwnerInstanceID = "PLAYER";
                enemy.OwnerInstanceID = "ENEMY";
                carried.OwnerInstanceID = "PLAYER";
                _presentations[carried].gameObject.SetActive(false);
                _controller.Initialize(_camera, _presentations, "PLAYER");

                input.Press(keyboard.leftCtrlKey);
                input.Press(keyboard.aKey);
                yield return null;

                CollectionAssert.AreEquivalent(
                    new[] { left, right },
                    _controller.SelectedCombatants
                );

                input.Release(keyboard.aKey);
                input.Release(keyboard.leftCtrlKey);
                yield return null;
                _controller.SelectAt(_camera.ViewportToScreenPoint(new Vector3(0.95f, 0.95f)));
                Assert.IsEmpty(_controller.SelectedCombatants);

                input.Press(keyboard.leftMetaKey);
                input.Press(keyboard.aKey);
                yield return null;

                CollectionAssert.AreEquivalent(
                    new[] { left, right },
                    _controller.SelectedCombatants
                );
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks valid scene starts without selection.
        /// </summary>
        [Test]
        public void Initialize_ValidScene_StartsWithoutSelection()
        {
            _controller.Initialize(_camera, _presentations);

            Assert.IsNull(_controller.SelectedCombatant);
            Assert.IsTrue(
                _instance.transform.Find("SelectionOverlay/BattleDock").gameObject.activeSelf
            );
        }

        /// <summary>
        /// Checks null camera throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullCamera_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _controller.Initialize(null, _presentations)
            );
        }

        /// <summary>
        /// Checks null presentations throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullPresentations_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_camera, null));
        }

        /// <summary>
        /// Checks hull center selects combatant and shows overlay.
        /// </summary>
        [Test]
        public void SelectAt_HullCenter_SelectsCombatantAndShowsOverlay()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.AreSame(combatant, _controller.SelectedCombatant);
            Assert.IsTrue(
                _instance.transform.Find("SelectionOverlay/SelectionFrame").gameObject.activeSelf
            );
            Assert.IsTrue(
                _instance.transform.Find("SelectionOverlay/BattleDock").gameObject.activeSelf
            );
        }

        /// <summary>
        /// Checks overlapping hulls selects nearest regardless of enumeration order.
        /// </summary>
        [Test]
        public void SelectAt_OverlappingHulls_SelectsNearestRegardlessOfEnumerationOrder()
        {
            AddHull(new Vector3(0f, 0f, 20f));
            SpaceCombatUnit nearest = AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.AreSame(nearest, _controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks empty space inside a large foreground hull envelope cannot steal a smaller ship's
        /// visible center.
        /// </summary>
        [Test]
        public void SelectAt_VisibleShipInsideLargerProjectedEnvelope_SelectsVisibleShip()
        {
            SpaceCombatUnit foreground = AddHull(Vector3.zero);
            foreground.ModelSize = new ModelDimensions
            {
                Width = 30f,
                Height = 20f,
                Depth = 10f,
            };
            SpaceCombatUnit visible = AddHull(new Vector3(8f, 0f, 20f));
            visible.ModelSize = new ModelDimensions
            {
                Width = 4f,
                Height = 4f,
                Depth = 8f,
            };
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[visible].position));

            Assert.AreSame(visible, _controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks empty space clears selection.
        /// </summary>
        [Test]
        public void SelectAt_EmptySpace_ClearsSelection()
        {
            AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            _controller.SelectAt(_camera.ViewportToScreenPoint(new Vector3(0.95f, 0.95f)));

            Assert.IsNull(_controller.SelectedCombatant);
            Assert.IsFalse(
                _instance.transform.Find("SelectionOverlay/SelectionFrame").gameObject.activeSelf
            );
        }

        /// <summary>
        /// Checks outside camera viewport preserves selection.
        /// </summary>
        [Test]
        public void SelectAt_OutsideCameraViewport_PreservesSelection()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            _controller.SelectAt(new Vector2(-1f, -1f));

            Assert.AreSame(combatant, _controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks a hostile hull cannot enter the locally controlled selection.
        /// </summary>
        [Test]
        public void SelectAt_EnemyHull_DoesNotSelectCombatant()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            combatant.OwnerInstanceID = "ENEMY";
            _controller.Initialize(_camera, _presentations, "PLAYER");

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsNull(_controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks rotated long hull uses its oriented envelope.
        /// </summary>
        [Test]
        public void SelectAt_RotatedLongHull_UsesItsOrientedEnvelope()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            combatant.ModelSize = new ModelDimensions
            {
                Width = 2f,
                Height = 2f,
                Depth = 20f,
            };
            _presentations[combatant].rotation = Quaternion.Euler(0f, 90f, 0f);
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(new Vector3(8f, 0f, 0f)));

            Assert.AreSame(combatant, _controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks outside clip planes does not select hull.
        /// </summary>
        /// <param name="depth">The depth under test.</param>
        [TestCase(-60f)]
        [TestCase(1100f)]
        public void SelectAt_OutsideClipPlanes_DoesNotSelectHull(float depth)
        {
            AddHull(new Vector3(0f, 0f, depth));
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsNull(_controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks inactive hull does not select hull.
        /// </summary>
        [Test]
        public void SelectAt_InactiveHull_DoesNotSelectHull()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            _presentations[combatant].gameObject.SetActive(false);
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsNull(_controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks destroyed hull does not select hull.
        /// </summary>
        [Test]
        public void SelectAt_DestroyedHull_DoesNotSelectHull()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            UnityEngine.Object.DestroyImmediate(_presentations[combatant].gameObject);
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsNull(_controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks status panel preserves selection.
        /// </summary>
        /// <returns>The runtime interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator SelectAt_StatusPanel_PreservesSelection()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));
            Assert.AreSame(combatant, _controller.SelectedCombatant);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform panel = (RectTransform)
                _instance.transform.Find("SelectionOverlay/BattleDock");
            Vector2 panelCenter = RectTransformUtility.WorldToScreenPoint(
                null,
                panel.TransformPoint(panel.rect.center)
            );

            _controller.SelectAt(panelCenter);

            Assert.AreSame(combatant, _controller.SelectedCombatant);
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks rebound battle discards previous targets and selection.
        /// </summary>
        [Test]
        public void Initialize_ReboundBattle_DiscardsPreviousTargetsAndSelection()
        {
            AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            _controller.Initialize(_camera, new Dictionary<SpaceCombatUnit, Transform>());
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsNull(_controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks selected hull discards battle binding.
        /// </summary>
        [Test]
        public void Clear_SelectedHull_DiscardsBattleBinding()
        {
            AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            _controller.Clear();
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsNull(_controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks left click selects hull.
        /// </summary>
        /// <returns>The runtime interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_LeftClick_SelectsHull()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                SpaceCombatUnit combatant = AddHull(Vector3.zero);
                _controller.Initialize(_camera, _presentations);
                Vector2 point = _camera.WorldToScreenPoint(Vector3.zero);
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point }.WithButton(MouseButton.Left)
                );
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                yield return null;

                Assert.AreSame(combatant, _controller.SelectedCombatant);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks Control-click and Command-click preserve the existing world selection.
        /// </summary>
        /// <returns>The two primary-modifier click sequences.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_PrimaryModifierLeftClick_AddsHull()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
                SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
                _controller.Initialize(_camera, _presentations);

                foreach (var modifier in new[] { keyboard.leftCtrlKey, keyboard.leftMetaKey })
                {
                    _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[left].position));
                    input.Press(modifier);
                    Vector2 point = _camera.WorldToScreenPoint(_presentations[right].position);
                    InputSystem.QueueStateEvent(
                        mouse,
                        new MouseState { position = point }.WithButton(MouseButton.Left)
                    );
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    yield return null;
                    input.Release(modifier);
                    yield return null;

                    CollectionAssert.AreEqual(
                        new[] { left, right },
                        _controller.SelectedCombatants
                    );
                }
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks tactical cancellation clears selection without releasing the battle binding.
        /// </summary>
        /// <returns>The runtime interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator TryDeselect_SelectedCombatant_ClearsSelection()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            try
            {
                AddHull(Vector3.zero);
                _controller.Initialize(_camera, _presentations);
                _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

                Assert.IsTrue(_controller.TryDeselect());

                Assert.IsNull(_controller.SelectedCombatant);
                Assert.IsEmpty(_controller.SelectedCombatants);
                Assert.IsFalse(_controller.TryDeselect());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks two button camera gesture does not select hull.
        /// </summary>
        /// <returns>The runtime interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_TwoButtonCameraGesture_DoesNotSelectHull()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                AddHull(Vector3.zero);
                _controller.Initialize(_camera, _presentations);
                Vector2 point = _camera.WorldToScreenPoint(Vector3.zero);
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point }
                        .WithButton(MouseButton.Right)
                        .WithButton(MouseButton.Left)
                );
                yield return null;
                yield return null;

                Assert.IsNull(_controller.SelectedCombatant);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks right clicks request movement while an orbit gesture does not.
        /// </summary>
        /// <returns>The runtime pointer interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator MoveRequested_RightClickThenOrbit_EmitsOnlyClickOrder()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                AddHull(Vector3.zero);
                _controller.Initialize(_camera, _presentations);
                _camera
                    .GetComponent<TacticalViewCameraController>()
                    .Configure(
                        new BattleMapBounds
                        {
                            MinimumX = -1000f,
                            MaximumX = 1000f,
                            MinimumY = -1000f,
                            MaximumY = 1000f,
                            MinimumZ = -1000f,
                            MaximumZ = 1000f,
                        },
                        new Bounds(Vector3.zero, Vector3.one * 2000f),
                        Vector3.zero
                    );
                int orders = 0;
                _controller.MoveRequested += _ => orders++;
                Vector2 point = _camera.WorldToScreenPoint(Vector3.zero);
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point }.WithButton(MouseButton.Right)
                );
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                Assert.AreEqual(1, orders);

                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point }.WithButton(MouseButton.Right)
                );
                yield return null;
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point + new Vector2(40f, 0f) }.WithButton(
                        MouseButton.Right
                    )
                );
                yield return null;
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point + new Vector2(40f, 0f) }
                );
                yield return null;

                Assert.AreEqual(1, orders);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks Control-right-click and Command-right-click request synchronized movement.
        /// </summary>
        /// <returns>The runtime modifier and pointer input frames.</returns>
        [UnityTest]
        public IEnumerator MoveTogetherRequested_PrimaryModifierRightClick_EmitsSynchronizedOrder()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                SpaceCombatUnit unit = AddHull(Vector3.zero);
                unit.OwnerInstanceID = "PLAYER";
                _controller.Initialize(_camera, _presentations, "PLAYER");
                _camera
                    .GetComponent<TacticalViewCameraController>()
                    .Configure(
                        new BattleMapBounds
                        {
                            MinimumX = -1000f,
                            MaximumX = 1000f,
                            MinimumY = -1000f,
                            MaximumY = 1000f,
                            MinimumZ = -1000f,
                            MaximumZ = 1000f,
                        },
                        new Bounds(Vector3.zero, Vector3.one * 2000f),
                        Vector3.zero
                    );
                _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));
                int normalOrders = 0;
                int synchronizedOrders = 0;
                _controller.MoveRequested += _ => normalOrders++;
                _controller.MoveTogetherRequested += _ => synchronizedOrders++;
                Vector2 point = _camera.WorldToScreenPoint(new Vector3(20f, 0f, 20f));

                foreach (var modifier in new[] { keyboard.leftCtrlKey, keyboard.leftMetaKey })
                {
                    input.Press(modifier);
                    InputSystem.QueueStateEvent(
                        mouse,
                        new MouseState { position = point }.WithButton(MouseButton.Right)
                    );
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    yield return null;
                    input.Release(modifier);
                    yield return null;
                }

                Assert.AreEqual(0, normalOrders);
                Assert.AreEqual(2, synchronizedOrders);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks a hostile hull under the pointer preserves the platform cursor.
        /// </summary>
        /// <returns>The runtime pointer frame and rendered cursor state.</returns>
        [UnityTest]
        public IEnumerator ContextualCursor_FriendlySelectionOverEnemy_RemainsHidden()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                SpaceCombatUnit friendly = AddHull(new Vector3(-10f, 0f, 0f));
                SpaceCombatUnit enemy = AddHull(new Vector3(10f, 0f, 0f));
                friendly.OwnerInstanceID = "PLAYER";
                enemy.OwnerInstanceID = "ENEMY";
                _controller.Initialize(_camera, _presentations, "PLAYER");
                _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[friendly].position));
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState
                    {
                        position = _camera.WorldToScreenPoint(_presentations[enemy].position),
                    }
                );

                yield return null;

                TacticalHudGraphic cursor = _instance
                    .transform.Find("SelectionOverlay/ContextualCursor")
                    .GetComponent<TacticalHudGraphic>();
                Assert.IsFalse(cursor.gameObject.activeSelf);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks the Move key consumes a destination click without changing selection and Stop is connected.
        /// </summary>
        /// <returns>The runtime command interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator MoveRequested_MoveButtonThenWorldClick_PreservesSelectionAndWiresStop()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                SpaceCombatUnit unit = AddHull(Vector3.zero);
                _controller.Initialize(_camera, _presentations);
                Vector2 point = _camera.WorldToScreenPoint(Vector3.zero);
                _controller.SelectAt(point);
                int orders = 0;
                int stops = 0;
                _controller.MoveRequested += _ => orders++;
                _controller.StopRequested += () => stops++;
                TacticalSelectionView view =
                    _instance.GetComponentInChildren<TacticalSelectionView>();
                view.MoveButton.onClick.Invoke();

                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point }.WithButton(MouseButton.Left)
                );
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                view.StopButton.onClick.Invoke();

                Assert.AreEqual(1, orders);
                Assert.AreEqual(1, stops);
                Assert.AreSame(unit, _controller.SelectedCombatant);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Verifies additive world selection and toggle removal.
        /// </summary>
        [Test]
        public void SelectAt_AdditiveClicks_SelectsTwoAndTogglesOneOff()
        {
            SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[left].position));
            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[right].position), true);
            CollectionAssert.AreEqual(new[] { left, right }, _controller.SelectedCombatants);

            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[left].position), true);

            CollectionAssert.AreEqual(new[] { right }, _controller.SelectedCombatants);
        }

        /// <summary>
        /// Verifies drag selection includes every visible hull center in its bounds.
        /// </summary>
        [Test]
        public void SelectWithin_VisibleHullCenters_SelectsAllInsideRectangle()
        {
            SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            _controller.Initialize(_camera, _presentations);

            _controller.SelectWithin(_camera.pixelRect);

            CollectionAssert.AreEqual(new[] { left, right }, _controller.SelectedCombatants);
        }

        [Test]
        public void SelectControlGroup_AssignedFriendlySelection_RestoresItsLiveMembers()
        {
            SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            left.OwnerInstanceID = "PLAYER";
            right.OwnerInstanceID = "PLAYER";
            _controller.Initialize(_camera, _presentations, "PLAYER");
            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[left].position));
            _controller.AssignControlGroup(0);
            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[right].position));

            _controller.SelectControlGroup(0);

            CollectionAssert.AreEqual(new[] { left }, _controller.SelectedCombatants);
        }

        [Test]
        public void AssignControlGroup_EnemyInspection_DoesNotCreateCommandGroup()
        {
            SpaceCombatUnit enemy = AddHull(Vector3.zero);
            enemy.OwnerInstanceID = "ENEMY";
            _controller.Initialize(_camera, _presentations, "PLAYER");
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            _controller.AssignControlGroup(0);
            _controller.SelectAt(_camera.ViewportToScreenPoint(new Vector3(0.95f, 0.95f)));
            _controller.SelectControlGroup(0);

            Assert.IsEmpty(_controller.SelectedCombatants);
        }

        /// <summary>
        /// Checks right-clicking an eligible carrier emits targeted recovery instead of movement.
        /// </summary>
        /// <returns>The pointer input frames and resulting targeted order.</returns>
        [UnityTest]
        public IEnumerator DockAtRequested_FighterOverEligibleCarrier_EmitsTargetedRecoveryOnly()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                ActiveBattle battle = new ActiveBattle { Kind = BattleKind.Space };
                CapitalShip carrierSource = new CapitalShip
                {
                    InstanceID = "CARRIER",
                    OwnerInstanceID = "PLAYER",
                    StarfighterCapacity = 1,
                    ModelSize = new ModelDimensions
                    {
                        Width = 10f,
                        Height = 6f,
                        Depth = 14f,
                    },
                };
                Starfighter fighterSource = new Starfighter
                {
                    InstanceID = "FIGHTER",
                    OwnerInstanceID = "PLAYER",
                    CurrentSquadronSize = 12,
                    SublightSpeed = 10,
                    ModelSize = new ModelDimensions
                    {
                        Width = 2f,
                        Height = 1f,
                        Depth = 3f,
                    },
                };
                SpaceCombatUnit carrier = battle.AddCombatants(carrierSource)[0];
                SpaceCombatUnit fighter = battle.AddCombatants(fighterSource)[0];
                AddPresentation(fighter, new Vector3(-10f, 0f, 0f));
                AddPresentation(carrier, new Vector3(10f, 0f, 0f));
                TacticalCarrierOperations operations = new TacticalCarrierOperations(
                    battle,
                    new Dictionary<SpaceCombatUnit, System.Numerics.Vector3[][]>
                    {
                        [carrier] = new[]
                        {
                            new[] { System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitZ },
                        },
                    }
                );
                _controller.Initialize(_camera, _presentations, "PLAYER");
                _controller.BindCarrierOperations(operations);
                _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[fighter].position));
                _camera
                    .GetComponent<TacticalViewCameraController>()
                    .Configure(
                        new BattleMapBounds
                        {
                            MinimumX = -1000f,
                            MaximumX = 1000f,
                            MinimumY = -1000f,
                            MaximumY = 1000f,
                            MinimumZ = -1000f,
                            MaximumZ = 1000f,
                        },
                        new Bounds(Vector3.zero, Vector3.one * 2000f),
                        Vector3.zero
                    );
                SpaceCombatUnit target = null;
                int moves = 0;
                _controller.DockAtRequested += value => target = value;
                _controller.MoveRequested += _ => moves++;
                Vector2 point = _camera.WorldToScreenPoint(_presentations[carrier].position);
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = point }.WithButton(MouseButton.Right)
                );
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;

                Assert.AreSame(carrier, target);
                Assert.AreEqual(0, moves);
            }
            finally
            {
                input.TearDown();
                UnityEngine.Object.DestroyImmediate(_instance);
            }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Verifies retired combatants cannot be selected.
        /// </summary>
        [Test]
        public void SelectAt_RetreatedHull_DoesNotSelectHull()
        {
            SpaceCombatUnit combatant = AddHull(Vector3.zero);
            combatant.HasRetreated = true;
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.IsEmpty(_controller.SelectedCombatants);
        }

        /// <summary>
        /// Checks authored roster buttons change the actual shared selection.
        /// </summary>
        /// <returns>The play-mode interaction and cleanup.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_RosterClick_SelectsClickedCombatant()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            _controller.Initialize(_camera, _presentations);
            _controller.SelectWithin(_camera.pixelRect);
            Assert.IsNotNull(_instance);
            Assert.IsNotNull(_camera);
            CollectionAssert.AreEquivalent(new[] { left, right }, _controller.SelectedCombatants);
            TacticalUnitCardView card = null;
            foreach (
                TacticalUnitCardView candidate in _instance.GetComponentsInChildren<TacticalUnitCardView>()
            )
                if (candidate.Combatant == right)
                {
                    Assert.IsNull(
                        card,
                        "Each selected combatant must have exactly one roster card."
                    );
                    card = candidate;
                }
            Assert.IsNotNull(card);

            UnityEngine.UI.Button button = card.GetComponent<UnityEngine.UI.Button>();
            Assert.IsNotNull(button);
            Assert.IsNotNull(button.onClick);
            button.onClick.Invoke();

            CollectionAssert.AreEqual(new[] { right }, _controller.SelectedCombatants);
            Assert.AreNotSame(left, _controller.SelectedCombatant);
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks grouped roster buttons select all represented members, not just the first ship.
        /// </summary>
        /// <returns>The play-mode interaction and cleanup.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_GroupedRosterClick_SelectsAllGroupMembers()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            SpaceCombatUnit other = AddHull(new Vector3(0f, 10f, 0f));
            left.TypeID = "CRUISER";
            right.TypeID = "CRUISER";
            other.TypeID = "DESTROYER";
            _controller.Initialize(_camera, _presentations);
            _controller.SelectWithin(_camera.pixelRect);
            TacticalUnitCardView group = null;
            foreach (
                TacticalUnitCardView card in _instance.GetComponentsInChildren<TacticalUnitCardView>()
            )
                if (card.Combatants.Count == 2)
                    group = card;
            Assert.IsNotNull(group);

            group.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            CollectionAssert.AreEquivalent(new[] { left, right }, _controller.SelectedCombatants);
            CollectionAssert.DoesNotContain(_controller.SelectedCombatants, other);
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks an individual pip selects its member without selecting its entire type group.
        /// </summary>
        /// <returns>The play-mode interaction and cleanup.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_MemberPipClick_SelectsOnlyThatMember()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            SpaceCombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            SpaceCombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            left.TypeID = "CRUISER";
            right.TypeID = "CRUISER";
            _controller.Initialize(_camera, _presentations);
            _controller.SelectWithin(_camera.pixelRect);
            TacticalUnitCardView group = _instance.GetComponentInChildren<TacticalUnitCardView>();
            Assert.AreEqual(2, group.Combatants.Count);
            SpaceCombatUnit secondMember = group.Combatants[1];

            group.transform.Find("Member2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { secondMember }, _controller.SelectedCombatants);
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks double-clicking a roster card centers once on its visible battle presentation.
        /// </summary>
        /// <returns>The runtime card interaction and camera update.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatant_RosterDoubleClick_CentersCameraWithoutFollowing()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            SpaceCombatUnit unit = AddHull(new Vector3(30f, 5f, 20f));
            TacticalViewCameraController cameraInput =
                _camera.GetComponent<TacticalViewCameraController>();
            cameraInput.Configure(
                new BattleMapBounds
                {
                    MinimumX = -1000f,
                    MaximumX = 1000f,
                    MinimumY = -1000f,
                    MaximumY = 1000f,
                    MinimumZ = -1000f,
                    MaximumZ = 1000f,
                },
                new Bounds(Vector3.zero, Vector3.one * 2000f),
                Vector3.zero
            );
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(_presentations[unit].position));
            TacticalUnitCardView card = _instance.GetComponentInChildren<TacticalUnitCardView>();
            PointerEventData pointer = new PointerEventData(
                _instance.GetComponentInChildren<EventSystem>()
            )
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };

            card.OnPointerClick(pointer);

            Assert.AreEqual(_presentations[unit].position, cameraInput.Pivot);
            Assert.IsNull(cameraInput.FollowTarget);
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks the independent HUD button tracks the primary unit and toggles off without moving it.
        /// </summary>
        /// <returns>The runtime button presses, moving presentation, and cleanup.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatant_FollowButton_TracksMovingUnitAndTogglesOff()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            SpaceCombatUnit unit = AddHull(Vector3.zero);
            TacticalViewCameraController cameraInput =
                _camera.GetComponent<TacticalViewCameraController>();
            cameraInput.Configure(
                new BattleMapBounds
                {
                    MinimumX = -1000f,
                    MaximumX = 1000f,
                    MinimumY = -1000f,
                    MaximumY = 1000f,
                    MinimumZ = -1000f,
                    MaximumZ = 1000f,
                },
                new Bounds(Vector3.zero, Vector3.one * 2000f),
                Vector3.zero
            );
            _controller.Initialize(_camera, _presentations);
            Assert.IsNotNull(
                _instance,
                "The tactical fixture root must remain alive after binding selection."
            );
            TacticalSelectionView view = _instance.GetComponentInChildren<TacticalSelectionView>();
            Assert.IsNotNull(view, "The tactical fixture must contain its selection view.");
            Assert.IsNotNull(
                view.FollowButton,
                "The generated follow button reference must be bound."
            );
            Assert.IsFalse(view.FollowButton.interactable);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));
            Assert.IsTrue(view.FollowButton.interactable);
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform followRect = (RectTransform)view.FollowButton.transform;
            EventSystem followEvents = _instance.GetComponentInChildren<EventSystem>();
            Assert.IsNotNull(
                followEvents,
                "The tactical prefab must provide its own event system."
            );
            PointerEventData pointer = new PointerEventData(followEvents)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    followRect.TransformPoint(followRect.rect.center)
                ),
                button = PointerEventData.InputButton.Left,
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            followEvents.RaycastAll(pointer, hits);
            GameObject followHit = null;
            foreach (RaycastResult hit in hits)
                if (
                    hit.gameObject.GetComponentInParent<TacticalCommandButton>()
                    == view.FollowButton
                )
                {
                    followHit = hit.gameObject;
                    break;
                }
            Assert.IsNotNull(followHit, "The follow key must receive actual HUD pointer hits.");
            ExecuteEvents.ExecuteHierarchy(followHit, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreSame(_presentations[unit], cameraInput.FollowTarget);
            Assert.AreEqual(
                "UNFOLLOW",
                view.FollowButton.GetComponentInChildren<TMPro.TMP_Text>().text
            );
            _presentations[unit].position = new Vector3(25f, 10f, 40f);
            yield return null;
            yield return null;
            Assert.AreEqual(_presentations[unit].position, cameraInput.Pivot);
            view.FollowButton.onClick.Invoke();
            Assert.IsNull(cameraInput.FollowTarget);
            Assert.AreEqual(
                "FOLLOW SELECTED",
                view.FollowButton.GetComponentInChildren<TMPro.TMP_Text>().text
            );
            UnityEngine.Object.DestroyImmediate(_instance);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Isolates runtime tests from application startup scenes and their input infrastructure.
        /// </summary>
        private void PreparePlayMode()
        {
            UnityEngine.Object.DestroyImmediate(_instance);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Adds a detached combatant and a measured presentation root.
        /// </summary>
        /// <param name="position">The hull position.</param>
        /// <returns>The combatant paired with the new root.</returns>
        private SpaceCombatUnit AddHull(Vector3 position)
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new CapitalShip
                {
                    DisplayName = "Test Cruiser",
                    ModelSize = new ModelDimensions
                    {
                        Width = 10f,
                        Height = 6f,
                        Depth = 14f,
                    },
                    MaxHullStrength = 100,
                    CurrentHullStrength = 75,
                    MaxShieldStrength = 50,
                }
            );
            AddPresentation(combatant, position);
            return combatant;
        }

        /// <summary>
        /// Adds a measured combatant root to the fixture's presentation index.
        /// </summary>
        /// <param name="combatant">The battle unit to present.</param>
        /// <param name="position">The hull position.</param>
        private void AddPresentation(SpaceCombatUnit combatant, Vector3 position)
        {
            GameObject root = new GameObject("Test Hull");
            root.transform.SetParent(_instance.transform, false);
            root.transform.position = position;
            _presentations.Add(combatant, root.transform);
        }
    }
}
