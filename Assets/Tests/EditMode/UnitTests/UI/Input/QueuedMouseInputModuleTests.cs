using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.Input
{
    [TestFixture]
    [Category("Integration")]
    public sealed class QueuedMouseInputModuleTests
    {
        private GameObject _root;
        private QueuedMouseTestEventSystem _events;
        private QueuedMouseTestInput _legacyInput;
        private InputTestFixture _inputFixture;
        private Mouse _mouse;
        private ContentAssets _assets;
        private ushort _buttons;

        /// <summary>
        /// Creates an isolated runtime input system and raycastable canvas.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _inputFixture = new InputTestFixture();
            _inputFixture.Setup();
            InputSystem.settings.disableRedundantEventsMerging = AssetDatabase
                .LoadAssetAtPath<InputSettings>(
                    "Assets/Input/InputSystemSettings.inputsettings.asset"
                )
                .disableRedundantEventsMerging;
            _mouse = InputSystem.AddDevice<Mouse>();
            _buttons = 0;
            _root = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject eventRoot = new GameObject("Events");
            eventRoot.transform.SetParent(_root.transform);
            _events = eventRoot.AddComponent<QueuedMouseTestEventSystem>();
            _legacyInput = eventRoot.AddComponent<QueuedMouseTestInput>();
            StandaloneInputModule module = eventRoot.AddComponent<QueuedMouseInputModule>();
            module.inputOverride = _legacyInput;
            _assets = new ContentAssets(ContentPackLoader.OpenActive().FileResolver);
            _events.SetFocus(true);
            ProcessFrame();
            ProcessFrame();
        }

        /// <summary>
        /// Releases the runtime controls and restores the input system.
        /// </summary>
        /// <returns>The play-mode cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
            _assets?.Dispose();
            _inputFixture?.TearDown();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Process_DefenseTabsWithOverlappingClickFrames_DeliversEveryClick()
        {
            GameObject window = Instantiate("DefenseWindow");
            DefenseWindowView view = window.GetComponent<DefenseWindowView>();
            Button first = FindButton(window, "PersonnelTabButtonImage");
            Button second = FindButton(window, "RegimentsTabButtonImage");
            const string faction = "Pack/Factions/Alliance/Strategy/UI/Windows/Defense/";
            const string shared = "Pack/Shared/Strategy/UI/Windows/Defense/";
            string[] normalPaths =
            {
                faction + "defense-tab",
                faction + "defense-window-tab-troop-available-tab",
                faction + "defense-window-tab-fighter-available-tab",
                shared + "defense-window-tab-shield-available-tab",
                shared + "defense-window-tab-battery-available-tab",
            };
            string[] activePaths =
            {
                faction + "defense-window-tab-personnel-active-tab",
                faction + "defense-window-tab-troop-active-tab",
                faction + "defense-window-tab-fighter-active-tab",
                shared + "defense-window-tab-shield-active-tab",
                shared + "defense-window-tab-battery-active-tab",
            };
            Texture[] normal = normalPaths.Select(_assets.GetTexture).ToArray();
            Texture[] active = activePaths.Select(_assets.GetTexture).ToArray();
            List<DefenseWindowTab> actions = new List<DefenseWindowTab>();
            void Render(DefenseWindowTab selected)
            {
                view.Render(
                    new DefenseWindowRenderData(
                        10,
                        10,
                        active[0],
                        "Test",
                        selected,
                        selected.ToString(),
                        string.Empty,
                        DefenseWindowRenderData
                            .OrderedTabs.Select(tab => new DefenseWindowTabRenderData(
                                tab,
                                tab == selected ? active[(int)tab] : normal[(int)tab],
                                active[(int)tab]
                            ))
                            .ToArray(),
                        Array.Empty<StrategyUnitCardRenderData>()
                    )
                );
            }
            Render(DefenseWindowTab.Personnel);
            view.TabRequested += (_, tab) =>
            {
                actions.Add(tab);
                Render(tab);
            };
            yield return null;
            AssertHit(first);
            AssertHit(second);

            RepeatOverlappingClicks(first, second, 100);

            CollectionAssert.AreEqual(
                Enumerable
                    .Range(0, 100)
                    .SelectMany(_ =>
                        new[] { DefenseWindowTab.Personnel, DefenseWindowTab.Regiments }
                    ),
                actions
            );
        }

        [UnityTest]
        public IEnumerator Process_MessageSelectionAndDeletionInOneFrame_PreservesActionOrder()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab"
            );
            GameObject panel = Object.Instantiate(
                prefab.GetComponentInChildren<MessagesIndexPanelView>(true).gameObject,
                _root.transform
            );
            UILayout.SetSourcePosition((RectTransform)panel.transform, 10, 10);
            panel.SetActive(true);
            ContentBindings.Apply(panel, _assets);
            MessagesIndexPanelView view = panel.GetComponent<MessagesIndexPanelView>();
            view.Initialize(() => true, panel.transform);
            Texture texture = _assets.GetTexture(
                "Application/Strategy/UI/Windows/ui_strategyview_messages_window_select_all_button_up"
            );
            MessagesIndexPanelRenderData data = new MessagesIndexPanelRenderData(
                MessagesTab.All,
                "Messages",
                MessagesTabCatalog
                    .OrderedTabs.Select(tab => new MessagesTabRenderData(tab, texture, texture))
                    .ToArray(),
                Array.Empty<MessageWindowRowRenderData>()
            );
            view.Render(data);
            List<string> actions = new List<string>();
            view.SelectAllRequested += () =>
            {
                actions.Add("Select");
                view.Render(data);
            };
            view.RemoveSelectedRequested += () =>
            {
                actions.Add("Delete");
                view.Render(data);
            };
            Button select = FindButton(panel, "SelectAllButtonImage");
            Button delete = FindButton(panel, "RemoveSelectedButtonImage");
            yield return null;
            AssertHit(select);
            AssertHit(delete);

            for (int i = 0; i < 100; i++)
            {
                QueueMouse(Position(select), true);
                QueueMouse(Position(select), false);
                QueueMouse(Position(delete), true);
                QueueMouse(Position(delete), false);
            }
            ProcessFrame();

            CollectionAssert.AreEqual(
                Enumerable.Range(0, 100).SelectMany(_ => new[] { "Select", "Delete" }),
                actions
            );
        }

        [UnityTest]
        public IEnumerator Process_OrdinaryClick_InvokesButtonOnce()
        {
            Button button = CreateButton("Button", 30);
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            yield return null;

            QueueMouse(Position(button), true);
            ProcessFrame();
            QueueMouse(Position(button), false);
            ProcessFrame();

            Assert.AreEqual(1, clicks);
        }

        [UnityTest]
        public IEnumerator Process_ReleaseOverAnotherButton_DoesNotClickEitherButton()
        {
            Button first = CreateButton("First", 30);
            Button second = CreateButton("Second", 160);
            int clicks = 0;
            first.onClick.AddListener(() => clicks++);
            second.onClick.AddListener(() => clicks++);
            yield return null;

            QueueMouse(Position(first), true);
            ProcessFrame();
            QueueMouse(Position(second), false);
            ProcessFrame();

            Assert.AreEqual(0, clicks);
        }

        [UnityTest]
        public IEnumerator Process_QueuedDragAcrossButtons_EndsWithOneDrop()
        {
            Button first = CreateButton("First", 30);
            Button second = CreateButton("Second", 160);
            QueuedMouseTestPointer source = first.gameObject.AddComponent<QueuedMouseTestPointer>();
            QueuedMouseTestPointer target =
                second.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            QueueMouse(Position(first), true);
            QueueMouse(Position(second), true);
            QueueMouse(Position(second), false);
            ProcessFrame();

            Assert.AreEqual(1, source.DragStarts);
            Assert.AreEqual(1, source.DragEnds);
            Assert.AreEqual(1, target.Drops);
            Assert.IsEmpty(source.ClickCounts);
        }

        [UnityTest]
        public IEnumerator Process_QuickClicksWithinOneFrame_PreservesDoubleClickCount()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            double start = _inputFixture.currentTime;
            QueueMouse(Position(button), true, start);
            QueueMouse(Position(button), false, start + 0.01);
            QueueMouse(Position(button), true, start + 0.1);
            QueueMouse(Position(button), false, start + 0.11);
            _inputFixture.currentTime = start + 0.2;
            ProcessFrame();

            CollectionAssert.AreEqual(new[] { 1, 2 }, pointer.ClickCounts);
        }

        [UnityTest]
        public IEnumerator Process_SlowClicksDeliveredWithinOneFrame_RemainSingleClicks()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            double start = _inputFixture.currentTime;
            QueueMouse(Position(button), true, start);
            QueueMouse(Position(button), false, start + 0.01);
            QueueMouse(Position(button), true, start + 0.5);
            QueueMouse(Position(button), false, start + 0.51);
            _inputFixture.currentTime = start + 0.6;
            ProcessFrame();

            CollectionAssert.AreEqual(new[] { 1, 1 }, pointer.ClickCounts);
        }

        [UnityTest]
        public IEnumerator Process_RightAndMiddleClicks_PreservesButtonIdentity()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            QueueButtons(Position(button), 2);
            QueueButtons(Position(button), 0);
            QueueButtons(Position(button), 4);
            QueueButtons(Position(button), 0);
            ProcessFrame();

            CollectionAssert.AreEqual(
                new[] { PointerEventData.InputButton.Right, PointerEventData.InputButton.Middle },
                pointer.ClickButtons
            );
        }

        [UnityTest]
        public IEnumerator Process_FocusLostAfterPress_CancelsClick()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            QueueMouse(Position(button), true);
            ProcessFrame();
            _events.SetFocus(false);
            QueueMouse(Position(button), false);
            ProcessFrame();
            _events.SetFocus(true);
            ProcessFrame();

            Assert.IsEmpty(pointer.ClickCounts);
            Assert.AreEqual(1, pointer.Releases);
        }

        [UnityTest]
        public IEnumerator Process_DeviceResetAfterPress_CancelsClickAndAcceptsNextClick()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            QueueMouse(Position(button), true);
            ProcessFrame();
            InputSystem.ResetDevice(_mouse);
            ProcessFrame();
            Assert.IsEmpty(pointer.ClickCounts);
            Assert.AreEqual(1, pointer.Releases);

            QueueMouse(Position(button), true);
            QueueMouse(Position(button), false);
            ProcessFrame();

            CollectionAssert.AreEqual(new[] { 1 }, pointer.ClickCounts);
        }

        [UnityTest]
        public IEnumerator Process_DisabledModule_DiscardsPendingClicks()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            QueueMouse(Position(button), true);
            QueueMouse(Position(button), false);
            InputSystem.Update();
            _events.GetComponent<QueuedMouseInputModule>().enabled = false;
            ProcessFrame();
            _events.GetComponent<QueuedMouseInputModule>().enabled = true;
            ProcessFrame();

            Assert.IsEmpty(pointer.ClickCounts);
        }

        [UnityTest]
        public IEnumerator Process_ClickDisablesModule_DiscardsRemainingClicks()
        {
            Button first = CreateButton("First", 30);
            Button second = CreateButton("Second", 160);
            int firstClicks = 0;
            int secondClicks = 0;
            first.onClick.AddListener(() =>
            {
                firstClicks++;
                _events.GetComponent<QueuedMouseInputModule>().enabled = false;
            });
            second.onClick.AddListener(() => secondClicks++);
            yield return null;

            QueueMouse(Position(first), true);
            QueueMouse(Position(first), false);
            QueueMouse(Position(second), true);
            QueueMouse(Position(second), false);
            ProcessFrame();

            Assert.AreEqual(1, firstClicks);
            Assert.AreEqual(0, secondClicks);
        }

        [UnityTest]
        public IEnumerator Process_ClickResetsMouse_DiscardsRemainingClicks()
        {
            Button first = CreateButton("First", 30);
            Button second = CreateButton("Second", 160);
            int secondClicks = 0;
            first.onClick.AddListener(() => InputSystem.ResetDevice(_mouse));
            second.onClick.AddListener(() => secondClicks++);
            yield return null;

            QueueMouse(Position(first), true);
            QueueMouse(Position(first), false);
            QueueMouse(Position(second), true);
            QueueMouse(Position(second), false);
            ProcessFrame();

            Assert.AreEqual(0, secondClicks);
        }

        [UnityTest]
        public IEnumerator Process_MultipleMouseEvents_DispatchesKeyboardSubmitOnce()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;
            _events.SetSelectedGameObject(button.gameObject);

            for (int i = 0; i < 10; i++)
            {
                QueueMouse(Position(button), true);
                QueueMouse(Position(button), false);
            }
            _legacyInput.Submit = true;
            ProcessFrame();

            Assert.AreEqual(1, pointer.Submits);
            Assert.AreEqual(1, pointer.SelectedUpdates);
            Assert.AreEqual(10, pointer.ClickCounts.Count);
        }

        [UnityTest]
        public IEnumerator Process_MultipleMouseEvents_DispatchesScrollOnce()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            QueueMouse(Position(button), true);
            QueueMouse(Position(button), false);
            _legacyInput.Scroll = Vector2.up;
            ProcessFrame();

            CollectionAssert.AreEqual(new[] { Vector2.up }, pointer.Scrolls);
        }

        [UnityTest]
        public IEnumerator Process_PartialMouseUpdates_PreservesHeldButtonAndPosition()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;

            InputSystem.QueueDeltaStateEvent(_mouse.position, Position(button));
            InputSystem.QueueDeltaStateEvent(_mouse.leftButton, (byte)1);
            InputSystem.QueueDeltaStateEvent(_mouse.leftButton, (byte)0);
            ProcessFrame();

            CollectionAssert.AreEqual(new[] { 1 }, pointer.ClickCounts);
        }

        [UnityTest]
        public IEnumerator Process_HighFrequencyHeldMovement_DispatchesOneDragPerFrame()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestPointer pointer =
                button.gameObject.AddComponent<QueuedMouseTestPointer>();
            yield return null;
            Vector2 origin = Position(button);
            QueueMouse(origin, true);
            ProcessFrame();

            for (int i = 1; i <= 1000; i++)
                QueueMouse(origin + Vector2.right * (i % 40 + 10), true);
            ProcessFrame();

            Assert.AreEqual(1, pointer.DragStarts);
            Assert.AreEqual(1, pointer.Drags);
        }

        [UnityTest]
        public IEnumerator Process_PenThenMouse_PreservesClicksAcrossInputSources()
        {
            Button button = CreateButton("Button", 30);
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            yield return null;
            InputSystem.AddDevice<Pen>();

            _legacyInput.Position = Position(button);
            _legacyInput.Down = true;
            ProcessFrame();
            _legacyInput.Up = true;
            ProcessFrame();
            Assert.AreEqual(1, clicks);

            QueueMouse(Position(button), true);
            QueueMouse(Position(button), false);
            ProcessFrame();

            Assert.AreEqual(2, clicks);
        }

        [UnityTest]
        public IEnumerator Process_NoInputSystemMouse_PreservesLegacyClicks()
        {
            Button button = CreateButton("Button", 30);
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            yield return null;
            InputSystem.RemoveDevice(_mouse);

            _legacyInput.Position = Position(button);
            _legacyInput.Down = true;
            ProcessFrame();
            _legacyInput.Up = true;
            ProcessFrame();

            Assert.AreEqual(1, clicks);
        }

        [UnityTest]
        public IEnumerator Process_QueuedClick_RestoresExistingInputOverride()
        {
            Button button = CreateButton("Button", 30);
            yield return null;

            QueueMouse(Position(button), true);
            QueueMouse(Position(button), false);
            ProcessFrame();

            Assert.AreSame(
                _legacyInput,
                _events.GetComponent<QueuedMouseInputModule>().inputOverride
            );
        }

        [UnityTest]
        public IEnumerator Process_ClickCallback_ReceivesExistingInputOverride()
        {
            Button button = CreateButton("Button", 30);
            BaseInput observedInput = null;
            button.onClick.AddListener(() =>
                observedInput = _events.GetComponent<QueuedMouseInputModule>().inputOverride
            );
            yield return null;

            QueueMouse(Position(button), true);
            QueueMouse(Position(button), false);
            ProcessFrame();

            Assert.AreSame(_legacyInput, observedInput);
        }

        [UnityTest]
        public IEnumerator Process_RaycastFailure_RestoresExistingInputOverride()
        {
            Button button = CreateButton("Button", 30);
            QueuedMouseTestRaycastFilter filter =
                button.gameObject.AddComponent<QueuedMouseTestRaycastFilter>();
            yield return null;
            filter.ThrowOnRaycast = true;
            QueueMouse(Position(button), true);

            Assert.Throws<InvalidOperationException>(() => ProcessFrame());

            Assert.AreSame(
                _legacyInput,
                _events.GetComponent<QueuedMouseInputModule>().inputOverride
            );
        }

        [UnityTest]
        public IEnumerator OnEnable_ReenabledModule_ReusesRequiredSnapshotInput()
        {
            QueuedMouseInputModule module = _events.GetComponent<QueuedMouseInputModule>();
            MouseSnapshotInput initial = _events.GetComponent<MouseSnapshotInput>();
            yield return null;

            for (int i = 0; i < 3; i++)
            {
                module.enabled = false;
                module.enabled = true;
                ProcessFrame();
            }

            CollectionAssert.AreEqual(
                new[] { initial },
                _events.GetComponents<MouseSnapshotInput>()
            );
        }

        /// <summary>
        /// Creates a raycastable button beneath the test canvas.
        /// </summary>
        /// <param name="name">The object name.</param>
        /// <param name="x">The horizontal position in source coordinates.</param>
        /// <returns>The button receiving routed pointer events.</returns>
        private Button CreateButton(string name, int x)
        {
            GameObject button = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            button.transform.SetParent(_root.transform, false);
            UILayout.SetSourceRect((RectTransform)button.transform, x, 30, 100, 40);
            return button.GetComponent<Button>();
        }

        /// <summary>
        /// Instantiates an authored strategy window and binds its content.
        /// </summary>
        /// <param name="name">The prefab name.</param>
        /// <returns>The active window instance.</returns>
        private GameObject Instantiate(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/UI/StrategyView/{name}.prefab"
            );
            GameObject window = Object.Instantiate(prefab, _root.transform);
            window.SetActive(true);
            ContentBindings.Apply(window, _assets);
            return window;
        }

        /// <summary>
        /// Alternates buttons while sharing one frame between release and the next press.
        /// </summary>
        /// <param name="first">The first button.</param>
        /// <param name="second">The second button.</param>
        /// <param name="count">The number of click pairs.</param>
        private void RepeatOverlappingClicks(Button first, Button second, int count)
        {
            for (int i = 0; i < count; i++)
            {
                QueueMouse(Position(first), true);
                ProcessFrame();
                QueueMouse(Position(first), false);
                QueueMouse(Position(second), true);
                ProcessFrame();
                QueueMouse(Position(second), false);
                ProcessFrame();
            }
        }

        /// <summary>
        /// Queues a primary-button sample through the input system.
        /// </summary>
        /// <param name="position">The mouse position in screen pixels.</param>
        /// <param name="held">Whether the primary button is held.</param>
        /// <param name="time">The event time, or negative to use the input runtime clock.</param>
        private void QueueMouse(Vector2 position, bool held, double time = -1)
        {
            QueueButtons(position, held ? (ushort)1 : (ushort)0, time);
        }

        /// <summary>
        /// Queues mouse state and exposes the equivalent frame-polled primary-button edges.
        /// </summary>
        /// <param name="position">The mouse position in screen pixels.</param>
        /// <param name="buttons">The held mouse button mask.</param>
        /// <param name="time">The event time, or negative to use the input runtime clock.</param>
        private void QueueButtons(Vector2 position, ushort buttons, double time = -1)
        {
            _legacyInput.Down |= (_buttons & 1) == 0 && (buttons & 1) != 0;
            _legacyInput.Up |= (_buttons & 1) != 0 && (buttons & 1) == 0;
            _legacyInput.Position = position;
            _buttons = buttons;
            InputSystem.QueueStateEvent(
                _mouse,
                new MouseState { position = position, buttons = buttons },
                time
            );
        }

        /// <summary>
        /// Processes native input samples and one complete event-system frame.
        /// </summary>
        private void ProcessFrame()
        {
            InputSystem.Update();
            Canvas.ForceUpdateCanvases();
            _events.ProcessFrame();
            _legacyInput.Down = false;
            _legacyInput.Up = false;
        }

        /// <summary>
        /// Verifies that the button is the first click handler at its screen position.
        /// </summary>
        /// <param name="button">The expected click target.</param>
        private void AssertHit(Button button)
        {
            PointerEventData pointer = new PointerEventData(_events)
            {
                position = Position(button),
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            _events.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, button.name);
            Assert.AreEqual(
                button.gameObject,
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                $"Wrong raycast target for {button.name}: {hits[0].gameObject.name}"
            );
        }

        /// <summary>
        /// Finds the screen-space center of a button.
        /// </summary>
        /// <param name="button">The button to locate.</param>
        /// <returns>The center in screen pixels.</returns>
        private static Vector2 Position(Button button)
        {
            RectTransform rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
        }

        /// <summary>
        /// Finds an authored button beneath a window.
        /// </summary>
        /// <param name="window">The window instance.</param>
        /// <param name="name">The button object name.</param>
        /// <returns>The unique matching button.</returns>
        private static Button FindButton(GameObject window, string name)
        {
            return window
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == name);
        }
    }

    public sealed class QueuedMouseTestEventSystem : EventSystem
    {
        /// <summary>
        /// Leaves frame processing under explicit test control.
        /// </summary>
        protected override void Update() { }

        /// <summary>
        /// Runs one event-system update, including its active input module.
        /// </summary>
        public void ProcessFrame()
        {
            base.Update();
        }

        /// <summary>
        /// Sets application focus without depending on the editor window.
        /// </summary>
        /// <param name="focus">Whether pointer input may be processed.</param>
        public void SetFocus(bool focus)
        {
            base.OnApplicationFocus(focus);
        }
    }

    public sealed class QueuedMouseTestRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool ThrowOnRaycast;

        /// <summary>
        /// Allows the test target to fail while Unity builds pointer event data.
        /// </summary>
        /// <param name="screenPoint">The tested position in screen pixels.</param>
        /// <param name="eventCamera">The camera used for the pointer raycast.</param>
        /// <returns>True when the pointer hit is accepted.</returns>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (ThrowOnRaycast)
                throw new InvalidOperationException("Raycast probe failure.");
            return true;
        }
    }

    public sealed class QueuedMouseTestInput : BaseInput
    {
        public Vector2 Position;
        public bool Down;
        public bool Up;
        public bool Submit;
        public Vector2 Scroll;
        public override bool mousePresent => true;
        public override Vector2 mousePosition => Position;
        public override Vector2 mouseScrollDelta => Scroll;
        public override int touchCount => 0;

        /// <summary>
        /// Reports the primary press edge for the simulated frame.
        /// </summary>
        /// <param name="button">The mouse button index.</param>
        /// <returns>Whether the primary button was pressed.</returns>
        public override bool GetMouseButtonDown(int button) => button == 0 && Down;

        /// <summary>
        /// Reports the primary release edge for the simulated frame.
        /// </summary>
        /// <param name="button">The mouse button index.</param>
        /// <returns>Whether the primary button was released.</returns>
        public override bool GetMouseButtonUp(int button) => button == 0 && Up;

        /// <summary>
        /// Leaves held-button state to the queued input device.
        /// </summary>
        /// <param name="button">The mouse button index.</param>
        /// <returns>False for every frame-polled button.</returns>
        public override bool GetMouseButton(int button) => false;

        /// <summary>
        /// Suppresses automatic axis navigation in pointer tests.
        /// </summary>
        /// <param name="axisName">The requested axis.</param>
        /// <returns>A neutral axis value.</returns>
        public override float GetAxisRaw(string axisName) => 0;

        /// <summary>
        /// Reports an explicitly requested keyboard submit.
        /// </summary>
        /// <param name="buttonName">The requested navigation button.</param>
        /// <returns>Whether Submit is requested for this frame.</returns>
        public override bool GetButtonDown(string buttonName) => Submit && buttonName == "Submit";
    }

    public sealed class QueuedMouseTestPointer
        : MonoBehaviour,
            IPointerClickHandler,
            IPointerUpHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler,
            ISubmitHandler,
            IUpdateSelectedHandler,
            IScrollHandler
    {
        public readonly List<int> ClickCounts = new List<int>();
        public readonly List<PointerEventData.InputButton> ClickButtons =
            new List<PointerEventData.InputButton>();
        public readonly List<Vector2> Scrolls = new List<Vector2>();
        public int Releases;
        public int DragStarts;
        public int Drags;
        public int DragEnds;
        public int Drops;
        public int Submits;
        public int SelectedUpdates;

        /// <summary>
        /// Records routed click counts and button identities.
        /// </summary>
        /// <param name="eventData">The routed pointer event.</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            ClickCounts.Add(eventData.clickCount);
            ClickButtons.Add(eventData.button);
        }

        /// <summary>
        /// Counts routed pointer releases.
        /// </summary>
        /// <param name="eventData">The routed pointer event.</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            Releases++;
        }

        /// <summary>
        /// Counts drag starts.
        /// </summary>
        /// <param name="eventData">The routed pointer event.</param>
        public void OnBeginDrag(PointerEventData eventData)
        {
            DragStarts++;
        }

        /// <summary>
        /// Counts drag updates.
        /// </summary>
        /// <param name="eventData">The routed pointer event.</param>
        public void OnDrag(PointerEventData eventData)
        {
            Drags++;
        }

        /// <summary>
        /// Counts completed or canceled drags.
        /// </summary>
        /// <param name="eventData">The routed pointer event.</param>
        public void OnEndDrag(PointerEventData eventData)
        {
            DragEnds++;
        }

        /// <summary>
        /// Counts drops received by this target.
        /// </summary>
        /// <param name="eventData">The routed pointer event.</param>
        public void OnDrop(PointerEventData eventData)
        {
            Drops++;
        }

        /// <summary>
        /// Counts keyboard submit events.
        /// </summary>
        /// <param name="eventData">The routed navigation event.</param>
        public void OnSubmit(BaseEventData eventData)
        {
            Submits++;
        }

        /// <summary>
        /// Counts updates of the selected control.
        /// </summary>
        /// <param name="eventData">The routed selection event.</param>
        public void OnUpdateSelected(BaseEventData eventData)
        {
            SelectedUpdates++;
        }

        /// <summary>
        /// Records scroll deltas received by this target.
        /// </summary>
        /// <param name="eventData">The routed scroll event.</param>
        public void OnScroll(PointerEventData eventData)
        {
            Scrolls.Add(eventData.scrollDelta);
        }
    }
}
