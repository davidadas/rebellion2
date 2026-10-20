using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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
        private Dictionary<CombatUnit, Transform> _presentations;

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
            _presentations = new Dictionary<CombatUnit, Transform>();
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
            CombatUnit combatant = AddHull(Vector3.zero);
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
            CombatUnit nearest = AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.AreSame(nearest, _controller.SelectedCombatant);
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
            CombatUnit combatant = AddHull(Vector3.zero);
            _controller.Initialize(_camera, _presentations);
            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            _controller.SelectAt(new Vector2(-1f, -1f));

            Assert.AreSame(combatant, _controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks enemy hull allows inspection.
        /// </summary>
        [Test]
        public void SelectAt_EnemyHull_AllowsInspection()
        {
            CombatUnit combatant = AddHull(Vector3.zero);
            combatant.GetUnit().OwnerInstanceID = "ENEMY";
            _controller.Initialize(_camera, _presentations);

            _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));

            Assert.AreSame(combatant, _controller.SelectedCombatant);
        }

        /// <summary>
        /// Checks rotated long hull uses its oriented envelope.
        /// </summary>
        [Test]
        public void SelectAt_RotatedLongHull_UsesItsOrientedEnvelope()
        {
            CombatUnit combatant = AddHull(Vector3.zero);
            combatant.GetUnit().ModelSize = new ModelDimensions
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
            CombatUnit combatant = AddHull(Vector3.zero);
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
            CombatUnit combatant = AddHull(Vector3.zero);
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
            CombatUnit combatant = AddHull(Vector3.zero);
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

            _controller.Initialize(_camera, new Dictionary<CombatUnit, Transform>());
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
                CombatUnit combatant = AddHull(Vector3.zero);
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
        /// Checks escape preserves selection for menu.
        /// </summary>
        /// <returns>The runtime interaction and mode transitions.</returns>
        [UnityTest]
        public IEnumerator SelectedCombatants_Escape_PreservesSelectionForMenu()
        {
            PreparePlayMode();
            yield return new EnterPlayMode();
            SetUp();
            InputTestFixture input = new InputTestFixture();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                CombatUnit combatant = AddHull(Vector3.zero);
                _controller.Initialize(_camera, _presentations);
                _controller.SelectAt(_camera.WorldToScreenPoint(Vector3.zero));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
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
                CombatUnit unit = AddHull(Vector3.zero);
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
            CombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            CombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
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
            CombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            CombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            _controller.Initialize(_camera, _presentations);

            _controller.SelectWithin(_camera.pixelRect);

            CollectionAssert.AreEqual(new[] { left, right }, _controller.SelectedCombatants);
        }

        /// <summary>
        /// Verifies retired combatants cannot be selected.
        /// </summary>
        [Test]
        public void SelectAt_RetreatedHull_DoesNotSelectHull()
        {
            CombatUnit combatant = AddHull(Vector3.zero);
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
            CombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            CombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
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
            CombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            CombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            CombatUnit other = AddHull(new Vector3(0f, 10f, 0f));
            left.GetUnit().TypeID = "CRUISER";
            right.GetUnit().TypeID = "CRUISER";
            other.GetUnit().TypeID = "DESTROYER";
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
            CombatUnit left = AddHull(new Vector3(-10f, 0f, 0f));
            CombatUnit right = AddHull(new Vector3(10f, 0f, 0f));
            left.GetUnit().TypeID = "CRUISER";
            right.GetUnit().TypeID = "CRUISER";
            _controller.Initialize(_camera, _presentations);
            _controller.SelectWithin(_camera.pixelRect);
            TacticalUnitCardView group = _instance.GetComponentInChildren<TacticalUnitCardView>();
            Assert.AreEqual(2, group.Combatants.Count);

            group.transform.Find("Member2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { right }, _controller.SelectedCombatants);
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
            CombatUnit unit = AddHull(Vector3.zero);
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
        private CombatUnit AddHull(Vector3 position)
        {
            CombatUnit combatant = CombatUnit.Create(
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
            GameObject root = new GameObject("Test Hull");
            root.transform.SetParent(_instance.transform, false);
            root.transform.position = position;
            _presentations.Add(combatant, root.transform);
            return combatant;
        }
    }
}
