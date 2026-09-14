using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyHudViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private Texture2D _backgroundTexture;
        private Texture2D _displayTexture;
        private Texture2D _notificationTexture;
        private Texture2D _pressedTexture;
        private GameObject _rootObject;
        private Texture2D _speedTexture;
        private Texture2D _upTexture;
        private StrategyHudView _view;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<StrategyHudView>(true);
            _backgroundTexture = new Texture2D(853, 480);
            _displayTexture = new Texture2D(45, 45);
            _notificationTexture = new Texture2D(24, 24);
            _pressedTexture = new Texture2D(40, 40);
            _speedTexture = new Texture2D(50, 16);
            _upTexture = new Texture2D(40, 40);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_speedTexture);
            UnityEngine.Object.DestroyImmediate(_pressedTexture);
            UnityEngine.Object.DestroyImmediate(_upTexture);
            UnityEngine.Object.DestroyImmediate(_notificationTexture);
            UnityEngine.Object.DestroyImmediate(_displayTexture);
            UnityEngine.Object.DestroyImmediate(_backgroundTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies render null data throws argument null exception.
        /// </summary>
        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        /// <summary>
        /// Verifies render complete presentation applies counters images buttons and notifications.
        /// </summary>
        [Test]
        public void Render_CompletePresentation_AppliesCountersImagesButtonsAndNotifications()
        {
            StrategyHudViewData data = CreateViewData(
                CreateButtons(2),
                CreateNotifications(2),
                new RectInt(700, 10, 80, 20)
            );

            _view.Render(data);

            Assert.AreSame(_backgroundTexture, GetField<RawImage>("backgroundImage").texture);
            Assert.AreEqual("123", GetField<TextMeshProUGUI>("tickTextField").text);
            Assert.AreEqual(Color.red, GetField<TextMeshProUGUI>("tickTextField").color);
            Assert.AreEqual(
                new RectInt(10, 12, 60, 18),
                UILayout.GetSourceRect(GetField<TextMeshProUGUI>("tickTextField").rectTransform)
            );
            Assert.AreEqual("456", GetField<TextMeshProUGUI>("rawMaterialsTextField").text);
            Assert.AreEqual("789", GetField<TextMeshProUGUI>("refinedMaterialsTextField").text);
            Assert.AreEqual("12", GetField<TextMeshProUGUI>("maintenanceTextField").text);
            Assert.AreSame(_speedTexture, GetField<RawImage>("speedIndicatorImage").texture);
            Assert.AreEqual(
                new RectInt(100, 20, 50, 16),
                UILayout.GetSourceRect(GetField<RawImage>("speedIndicatorImage").rectTransform)
            );
            Assert.AreSame(
                _displayTexture,
                GetField<RawImage>("galacticInformationDisplayImage").texture
            );
            UIRaycastArea[] buttons = GetField<UIRaycastArea[]>("buttonViews");
            RawImage[] buttonImages = GetField<RawImage[]>("mainButtonImages");
            Assert.AreSame(_upTexture, buttonImages[0].texture);
            Assert.AreSame(_upTexture, buttonImages[1].texture);
            Assert.AreEqual(
                new RectInt(30, 410, 40, 40),
                UILayout.GetSourceRect(buttonImages[0].rectTransform)
            );
            Assert.AreEqual(new RectInt(20, 400, 30, 30), GetSourceRect(buttons[0]));
            Assert.AreEqual(new RectInt(60, 400, 30, 30), GetSourceRect(buttons[1]));
            Assert.IsFalse(buttons[2].gameObject.activeSelf);
            Assert.IsFalse(buttonImages[2].gameObject.activeSelf);
            Assert.AreEqual(
                new RectInt(700, 10, 80, 20),
                GetSourceRect(GetField<UIRaycastArea>("speedContextView"))
            );
            RawImage[] notifications = GetField<RawImage[]>("messageNotificationImages");
            Assert.AreSame(_notificationTexture, notifications[0].texture);
            Assert.AreSame(_notificationTexture, notifications[1].texture);
            Assert.IsTrue(notifications[0].raycastTarget);
            Assert.IsFalse(notifications[2].gameObject.activeSelf);
            Button[] notificationButtons = GetField<Button[]>("messageNotificationButtons");
            Assert.IsTrue(notificationButtons[0].interactable);
            Assert.AreEqual(Selectable.Transition.None, notificationButtons[0].transition);
            Assert.AreSame(notifications[0], notificationButtons[0].targetGraphic);
        }

        /// <summary>
        /// Verifies render missing optional images hides images and disables notification.
        /// </summary>
        [Test]
        public void Render_MissingOptionalImages_HidesImagesAndDisablesNotification()
        {
            StrategyHudViewData data = new StrategyHudViewData(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<StrategyHudButtonViewData>(),
                new[]
                {
                    new StrategyHudMessageNotificationViewData(
                        MessagesTab.Support,
                        null,
                        new RectInt(0, 0, 20, 20)
                    ),
                }
            );

            _view.Render(data);

            Assert.IsFalse(GetField<RawImage>("backgroundImage").enabled);
            Assert.IsFalse(GetField<RawImage>("speedIndicatorImage").gameObject.activeSelf);
            Assert.IsFalse(
                GetField<RawImage>("galacticInformationDisplayImage").gameObject.activeSelf
            );
            Assert.IsFalse(GetField<RawImage>("pressedMainButtonImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<RawImage[]>("mainButtonImages")[0].gameObject.activeSelf);
            Assert.IsFalse(GetField<UIRaycastArea[]>("buttonViews")[0].gameObject.activeSelf);
            Assert.IsFalse(GetField<UIRaycastArea>("speedContextView").gameObject.activeSelf);
            Assert.IsFalse(
                GetField<RawImage[]>("messageNotificationImages")[0].gameObject.activeSelf
            );
            Assert.IsFalse(GetField<Button[]>("messageNotificationButtons")[0].interactable);
        }

        /// <summary>
        /// Verifies render legacy prefab without button images creates button image slots.
        /// </summary>
        [Test]
        public void Render_LegacyPrefabWithoutButtonImages_CreatesButtonImageSlots()
        {
            SetField("mainButtonImages", Array.Empty<RawImage>());

            _view.Render(CreateViewData(CreateButtons(2), CreateNotifications(0), null));

            RawImage[] buttonImages = GetField<RawImage[]>("mainButtonImages");
            Assert.AreEqual(GetField<UIRaycastArea[]>("buttonViews").Length, buttonImages.Length);
            Assert.IsTrue(buttonImages.All(image => image != null));
            Assert.AreSame(_upTexture, buttonImages[0].texture);
            Assert.AreSame(_upTexture, buttonImages[1].texture);
        }

        /// <summary>
        /// Verifies button pointer press and release emits control cue and toggles pressed artwork.
        /// </summary>
        [Test]
        public void ButtonPointer_PressAndRelease_EmitsControlCueAndTogglesPressedArtwork()
        {
            _view.Render(CreateViewData(CreateButtons(1), CreateNotifications(0), null));
            StrategyHudAction pressedAction = StrategyHudAction.None;
            _view.ControlPressed += action => pressedAction = action;
            UIRaycastArea button = GetField<UIRaycastArea[]>("buttonViews")[0];
            RawImage overlay = GetField<RawImage>("pressedMainButtonImage");
            PointerEventData eventData = CreatePointerEvent(PointerEventData.InputButton.Left);

            button.OnPointerDown(eventData);

            Assert.AreEqual(StrategyHudAction.Options, pressedAction);
            Assert.AreSame(_pressedTexture, overlay.texture);
            Assert.IsTrue(overlay.gameObject.activeSelf);
            Assert.AreEqual(
                new RectInt(30, 410, 40, 40),
                UILayout.GetSourceRect(overlay.rectTransform)
            );

            button.OnPointerUp(eventData);

            Assert.IsFalse(overlay.gameObject.activeSelf);
        }

        /// <summary>
        /// Verifies button pointer click inside hud emits action and source coordinates.
        /// </summary>
        [Test]
        public void ButtonPointer_ClickInsideHud_EmitsActionAndSourceCoordinates()
        {
            _view.Render(CreateViewData(CreateButtons(1), CreateNotifications(0), null));
            StrategyHudAction requestedAction = StrategyHudAction.None;
            int requestedX = -1;
            int requestedY = -1;
            _view.HudButtonRequested += (action, x, y) =>
            {
                requestedAction = action;
                requestedX = x;
                requestedY = y;
            };
            UIRaycastArea button = GetField<UIRaycastArea[]>("buttonViews")[0];

            button.OnPointerClick(CreatePointerEvent(PointerEventData.InputButton.Left));

            Assert.AreEqual(StrategyHudAction.Options, requestedAction);
            Assert.AreEqual(426, requestedX);
            Assert.AreEqual(240, requestedY);
        }

        /// <summary>
        /// Verifies button pointer click outside hud requests render instead of action.
        /// </summary>
        [Test]
        public void ButtonPointer_ClickOutsideHud_RequestsRenderInsteadOfAction()
        {
            _view.Render(CreateViewData(CreateButtons(1), CreateNotifications(0), null));
            int renderCount = 0;
            int actionCount = 0;
            _view.RenderRequested += () => renderCount++;
            _view.HudButtonRequested += (_, _, _) => actionCount++;
            PointerEventData eventData = CreatePointerEvent(PointerEventData.InputButton.Left);
            eventData.position = new Vector2(-10000f, -10000f);

            GetField<UIRaycastArea[]>("buttonViews")[0].OnPointerClick(eventData);

            Assert.AreEqual(1, renderCount);
            Assert.AreEqual(0, actionCount);
        }

        /// <summary>
        /// Verifies speed context right press inside hud emits source coordinates.
        /// </summary>
        [Test]
        public void SpeedContext_RightPressInsideHud_EmitsSourceCoordinates()
        {
            _view.Render(
                CreateViewData(
                    CreateButtons(1),
                    CreateNotifications(0),
                    new RectInt(700, 10, 80, 20)
                )
            );
            int requestedX = -1;
            int requestedY = -1;
            _view.SpeedContextRequested += (x, y) =>
            {
                requestedX = x;
                requestedY = y;
            };

            GetField<UIRaycastArea>("speedContextView")
                .OnPointerDown(CreatePointerEvent(PointerEventData.InputButton.Right));

            Assert.AreEqual(426, requestedX);
            Assert.AreEqual(240, requestedY);
        }

        /// <summary>
        /// Verifies notification button click emits tab assigned to rendered slot.
        /// </summary>
        [Test]
        public void NotificationButton_Click_EmitsTabAssignedToRenderedSlot()
        {
            _view.Render(CreateViewData(CreateButtons(0), CreateNotifications(2), null));
            MessagesTab? requestedTab = null;
            _view.MessageTabRequested += tab => requestedTab = tab;

            GetField<Button[]>("messageNotificationButtons")[1].onClick.Invoke();

            Assert.AreEqual(MessagesTab.Fleet, requestedTab);
        }

        /// <summary>
        /// Verifies unrendered and none buttons interact do not emit semantic requests.
        /// </summary>
        [Test]
        public void UnrenderedAndNoneButtons_Interact_DoNotEmitSemanticRequests()
        {
            StrategyHudButtonViewData noneButton = new StrategyHudButtonViewData(
                StrategyHudAction.None,
                new RectInt(20, 400, 30, 30),
                _upTexture,
                _pressedTexture,
                new RectInt(30, 410, 40, 40)
            );
            _view.Render(CreateViewData(new[] { noneButton }, CreateNotifications(0), null));
            int controlCount = 0;
            int commandCount = 0;
            _view.ControlPressed += _ => controlCount++;
            _view.HudButtonRequested += (_, _, _) => commandCount++;
            PointerEventData eventData = CreatePointerEvent(PointerEventData.InputButton.Left);
            UIRaycastArea[] buttons = GetField<UIRaycastArea[]>("buttonViews");

            buttons[0].OnPointerDown(eventData);
            buttons[0].OnPointerClick(eventData);
            buttons[1].OnPointerDown(eventData);
            buttons[1].OnPointerClick(eventData);

            Assert.AreEqual(0, controlCount);
            Assert.AreEqual(0, commandCount);
        }

        /// <summary>
        /// Verifies on destroy initialized view unbinds controls and raises destroyed event.
        /// </summary>
        [Test]
        public void OnDestroy_InitializedView_UnbindsControlsAndRaisesDestroyedEvent()
        {
            _view.Render(
                CreateViewData(
                    CreateButtons(1),
                    CreateNotifications(1),
                    new RectInt(700, 10, 80, 20)
                )
            );
            StrategyHudView destroyedView = null;
            int controlCount = 0;
            int tabCount = 0;
            int speedCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.ControlPressed += _ => controlCount++;
            _view.MessageTabRequested += _ => tabCount++;
            _view.SpeedContextRequested += (_, _) => speedCount++;
            PointerEventData leftClick = CreatePointerEvent(PointerEventData.InputButton.Left);
            PointerEventData rightPress = CreatePointerEvent(PointerEventData.InputButton.Right);

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            GetField<UIRaycastArea[]>("buttonViews")[0].OnPointerDown(leftClick);
            GetField<UIRaycastArea>("speedContextView").OnPointerDown(rightPress);
            GetField<Button[]>("messageNotificationButtons")[0].onClick.Invoke();

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, controlCount);
            Assert.AreEqual(0, speedCount);
            Assert.AreEqual(0, tabCount);
        }

        /// <summary>
        /// Creates view data.
        /// </summary>
        /// <param name="buttons">The buttons.</param>
        /// <param name="notifications">The notifications.</param>
        /// <param name="speedContextBounds">The speed context bounds.</param>
        /// <returns>The created view data.</returns>
        private StrategyHudViewData CreateViewData(
            StrategyHudButtonViewData[] buttons,
            StrategyHudMessageNotificationViewData[] notifications,
            RectInt? speedContextBounds
        )
        {
            return new StrategyHudViewData(
                _backgroundTexture,
                new StrategyHudCounterViewData("123", Color.red, new RectInt(10, 12, 60, 18)),
                new StrategyHudCounterViewData("456", Color.green, null),
                new StrategyHudCounterViewData("789", Color.blue, null),
                new StrategyHudCounterViewData("12", Color.yellow, null),
                _speedTexture,
                new RectInt(100, 20, 50, 16),
                _displayTexture,
                new RectInt(160, 20, 45, 45),
                speedContextBounds,
                buttons,
                notifications
            );
        }

        /// <summary>
        /// Creates buttons.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <returns>The created buttons.</returns>
        private StrategyHudButtonViewData[] CreateButtons(int count)
        {
            StrategyHudAction[] actions =
            {
                StrategyHudAction.Options,
                StrategyHudAction.GalacticInformationDisplay,
                StrategyHudAction.SystemFinder,
                StrategyHudAction.FleetFinder,
                StrategyHudAction.TroopFinder,
                StrategyHudAction.PersonnelFinder,
                StrategyHudAction.Encyclopedia,
            };
            return actions
                .Take(count)
                .Select(
                    (action, index) =>
                        new StrategyHudButtonViewData(
                            action,
                            new RectInt(20 + index * 40, 400, 30, 30),
                            _upTexture,
                            _pressedTexture,
                            new RectInt(30 + index * 40, 410, 40, 40)
                        )
                )
                .ToArray();
        }

        /// <summary>
        /// Creates notifications.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <returns>The created notifications.</returns>
        private StrategyHudMessageNotificationViewData[] CreateNotifications(int count)
        {
            MessagesTab[] tabs =
            {
                MessagesTab.Support,
                MessagesTab.Fleet,
                MessagesTab.Mission,
                MessagesTab.Resource,
                MessagesTab.Manufacturing,
                MessagesTab.Defense,
                MessagesTab.Conflict,
                MessagesTab.Chat,
                MessagesTab.Advice,
            };
            return tabs.Take(count)
                .Select(
                    (tab, index) =>
                        new StrategyHudMessageNotificationViewData(
                            tab,
                            _notificationTexture,
                            new RectInt(300 + index * 25, 430, 24, 24)
                        )
                )
                .ToArray();
        }

        /// <summary>
        /// Creates pointer event.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <returns>The created pointer event.</returns>
        private PointerEventData CreatePointerEvent(PointerEventData.InputButton button)
        {
            return new PointerEventData(null)
            {
                button = button,
                position = RectTransformUtility.WorldToScreenPoint(null, _view.transform.position),
            };
        }

        /// <summary>
        /// Gets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(StrategyHudView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        /// <summary>
        /// Sets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The value.</param>
        private void SetField(string fieldName, object value)
        {
            typeof(StrategyHudView)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, value);
        }

        /// <summary>
        /// Gets source rect.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <returns>The requested source rect.</returns>
        private static RectInt GetSourceRect(UIRaycastArea area)
        {
            return UILayout.GetSourceRect(area.transform as RectTransform);
        }
    }
}
