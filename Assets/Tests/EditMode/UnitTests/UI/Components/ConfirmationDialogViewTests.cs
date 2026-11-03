using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class ConfirmationDialogViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";

        private EventSystem _eventSystem;
        private GameObject _rootObject;
        private GameObject _eventSystemObject;
        private ConfirmationDialogView _view;

        /// <summary>
        /// Creates and initializes a confirmation dialog for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
            UIComponentTestHelper.InvokeLifecycle(_eventSystem, "OnEnable");
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<ConfirmationDialogView>(true);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Destroys the confirmation dialog after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
            UIComponentTestHelper.InvokeLifecycle(_eventSystem, "OnDisable");
            UnityEngine.Object.DestroyImmediate(_eventSystemObject);
        }

        [Test]
        public void AuthoredPrefab_Blocker_FillsHostAndDialogSurfaceRemainsCentered()
        {
            RectTransform root = (RectTransform)_view.transform;
            RectTransform blocker = (RectTransform)root.Find("InputBlocker");
            RectTransform dialogSurface = (RectTransform)root.Find("DialogSurface");

            Assert.AreEqual(Vector2.zero, root.anchorMin);
            Assert.AreEqual(Vector2.one, root.anchorMax);
            Assert.AreEqual(Vector2.zero, root.offsetMin);
            Assert.AreEqual(Vector2.zero, root.offsetMax);
            Assert.AreEqual(Vector2.zero, blocker.anchorMin);
            Assert.AreEqual(Vector2.one, blocker.anchorMax);
            Assert.AreEqual(Vector2.zero, blocker.offsetMin);
            Assert.AreEqual(Vector2.zero, blocker.offsetMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), dialogSurface.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), dialogSurface.anchorMax);
            Assert.AreEqual(new Vector2(640f, 480f), dialogSurface.sizeDelta);
            Assert.AreEqual(Vector2.zero, dialogSurface.anchoredPosition);
        }

        [Test]
        public void Show_Message_AppliesPresentationAndDisplaysDialog()
        {
            _view.Show("Are you sure?");

            RawImage background = GetField<RawImage>("backgroundImage");
            Assert.AreEqual(background.texture != null, background.enabled);
            Assert.IsFalse(background.raycastTarget);
            Assert.AreEqual("Are you sure?", GetField<TextMeshProUGUI>("messageTextField").text);
            Assert.AreEqual(
                GetField<Color>("messageTextColor"),
                GetField<TextMeshProUGUI>("messageTextField").color
            );
            Assert.AreSame(
                GetField<Texture2D>("confirmButtonUpTexture"),
                GetField<RawImage>("confirmButtonImage").texture
            );
            Assert.AreSame(
                GetField<Texture2D>("cancelButtonUpTexture"),
                GetField<RawImage>("cancelButtonImage").texture
            );
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void Show_NullMessage_DisplaysEmptyText()
        {
            _view.Show(null);

            Assert.AreEqual(string.Empty, GetField<TextMeshProUGUI>("messageTextField").text);
        }

        [Test]
        public void Show_VisibleDialog_SelectsConfirmButton()
        {
            _view.Show("Confirm");

            Assert.AreSame(
                GetField<Button>("confirmButton").gameObject,
                _eventSystem.currentSelectedGameObject
            );
        }

        [Test]
        public void ShowAcknowledgment_Message_DisplaysNoticeWithOnlyAuthoredConfirmControl()
        {
            Button confirm = GetField<Button>("confirmButton");
            RawImage background = _view
                .transform.Find("DialogSurface/BackgroundImage")
                .GetComponent<RawImage>();
            _view.Show("Confirm");
            Texture confirmationBackground = background.texture;

            _view.ShowAcknowledgment("The session has ended.");

            Assert.AreEqual(
                "The session has ended.",
                GetField<TextMeshProUGUI>("messageTextField").text
            );
            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.IsTrue(confirm.gameObject.activeSelf);
            Assert.IsFalse(GetField<Button>("cancelButton").gameObject.activeSelf);
            Assert.AreNotSame(confirmationBackground, background.texture);
            Assert.AreEqual(
                new Vector2(290f, -284f),
                ((RectTransform)confirm.transform).anchoredPosition
            );
            Assert.AreSame(
                GetField<Texture2D>("confirmButtonUpTexture"),
                GetField<RawImage>("confirmButtonImage").texture
            );
        }

        [Test]
        public void ShowAcknowledgment_NullMessage_DisplaysEmptyText()
        {
            _view.ShowAcknowledgment(null);

            Assert.AreEqual(string.Empty, GetField<TextMeshProUGUI>("messageTextField").text);
        }

        [Test]
        public void ShowAcknowledgment_HiddenNotice_SelectsConfirmButton()
        {
            _view.ShowAcknowledgment("The session has ended.");

            Assert.AreSame(
                GetField<Button>("confirmButton").gameObject,
                _eventSystem.currentSelectedGameObject
            );
        }

        [Test]
        public void ShowAcknowledgment_VisibleNoticeUpdate_PreservesCurrentSelection()
        {
            _view.ShowAcknowledgment("The session has ended.");
            _eventSystem.SetSelectedGameObject(_rootObject);

            _view.ShowAcknowledgment("Returning to the main menu.");

            Assert.AreSame(_rootObject, _eventSystem.currentSelectedGameObject);
        }

        [Test]
        public void Show_AfterAcknowledgment_RestoresCancelResponse()
        {
            int canceledCount = 0;
            _view.Canceled += () => canceledCount++;
            _view.Show("Confirm");
            RawImage background = _view
                .transform.Find("DialogSurface/BackgroundImage")
                .GetComponent<RawImage>();
            Texture confirmationBackground = background.texture;
            RectTransform confirm = (RectTransform)GetField<Button>("confirmButton").transform;
            Vector2 confirmationPosition = confirm.anchoredPosition;
            _view.ShowAcknowledgment("The session has ended.");

            _view.Show("Are you sure?");
            Button cancel = GetField<Button>("cancelButton");
            Assert.IsTrue(cancel.gameObject.activeSelf);
            Assert.AreSame(confirmationBackground, background.texture);
            Assert.AreEqual(confirmationPosition, confirm.anchoredPosition);
            cancel.onClick.Invoke();

            Assert.AreEqual(1, canceledCount);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Hide_VisibleDialog_HidesWithoutResponse()
        {
            int responseCount = 0;
            _view.Confirmed += () => responseCount++;
            _view.Canceled += () => responseCount++;
            _view.Show("Confirm");

            _view.Hide();

            Assert.IsFalse(_view.gameObject.activeSelf);
            Assert.AreEqual(0, responseCount);
        }

        [Test]
        public void ConfirmButton_Click_HidesAndRaisesConfirmed()
        {
            int confirmedCount = 0;
            _view.Confirmed += () => confirmedCount++;
            _view.Show("Confirm");

            GetField<Button>("confirmButton").onClick.Invoke();

            Assert.AreEqual(1, confirmedCount);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void ConfirmButton_AcknowledgmentMode_RaisesConfirmedWhileNoticeRemainsVisible()
        {
            bool visibleWhenConfirmed = false;
            int confirmedCount = 0;
            _view.Confirmed += () =>
            {
                confirmedCount++;
                visibleWhenConfirmed = _view.gameObject.activeSelf;
            };
            _view.ShowAcknowledgment("The session has ended.");

            GetField<Button>("confirmButton").onClick.Invoke();

            Assert.AreEqual(1, confirmedCount);
            Assert.IsTrue(visibleWhenConfirmed);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void CancelButton_Click_HidesAndRaisesCanceled()
        {
            int canceledCount = 0;
            _view.Canceled += () => canceledCount++;
            _view.Show("Confirm");

            GetField<Button>("cancelButton").onClick.Invoke();

            Assert.AreEqual(1, canceledCount);
            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void CancelButton_AcknowledgmentMode_PreservesNoticeWithoutResponse()
        {
            int responseCount = 0;
            _view.Confirmed += () => responseCount++;
            _view.Canceled += () => responseCount++;
            _view.ShowAcknowledgment("The session has ended.");

            GetField<Button>("cancelButton").onClick.Invoke();

            Assert.AreEqual(0, responseCount);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void TryCancel_AcknowledgmentMode_ConsumesRequestWithoutHidingOrResponse()
        {
            int responseCount = 0;
            _view.Confirmed += () => responseCount++;
            _view.Canceled += () => responseCount++;
            _view.ShowAcknowledgment("The session has ended.");

            bool handled = ((ICancelable)_view).TryCancel();

            Assert.IsTrue(handled);
            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(0, responseCount);
        }

        [Test]
        public void TryCancel_ConfirmationMode_HidesAndRaisesCanceled()
        {
            int canceledCount = 0;
            _view.Canceled += () => canceledCount++;
            _view.Show("Are you sure?");

            bool handled = _view.TryCancel();

            Assert.IsTrue(handled);
            Assert.IsFalse(_view.gameObject.activeSelf);
            Assert.AreEqual(1, canceledCount);
        }

        [Test]
        public void TryCancel_HiddenNotice_DoesNotConsumeRequest()
        {
            _view.ShowAcknowledgment("The session has ended.");
            _view.Hide();

            Assert.IsFalse(_view.TryCancel());
        }

        [Test]
        public void OnDestroy_BoundDialog_UnbindsButtons()
        {
            int responseCount = 0;
            _view.Confirmed += () => responseCount++;
            _view.Canceled += () => responseCount++;
            _view.Show("Confirm");

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            GetField<Button>("confirmButton").onClick.Invoke();
            GetField<Button>("cancelButton").onClick.Invoke();

            Assert.AreEqual(0, responseCount);
        }

        /// <summary>
        /// Reads a private authored reference from the dialog under test.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(ConfirmationDialogView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
