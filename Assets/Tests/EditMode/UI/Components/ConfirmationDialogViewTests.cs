using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class ConfirmationDialogViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";

        private GameObject _rootObject;
        private ConfirmationDialogView _view;

        /// <summary>
        /// Creates and initializes a confirmation dialog for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
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
        }

        /// <summary>
        /// Verifies the blocker fills its host while the fixed-size dialog art remains centered.
        /// </summary>
        [Test]
        public void AuthoredPrefab_BlockerFillsHostAndDialogSurfaceRemainsCentered()
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

        /// <summary>
        /// Verifies showing a prompt applies its text and authored presentation.
        /// </summary>
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

        /// <summary>
        /// Verifies a null prompt is displayed as empty text.
        /// </summary>
        [Test]
        public void Show_NullMessage_DisplaysEmptyText()
        {
            _view.Show(null);

            Assert.AreEqual(string.Empty, GetField<TextMeshProUGUI>("messageTextField").text);
        }

        /// <summary>
        /// Verifies hiding a prompt does not emit a response.
        /// </summary>
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

        /// <summary>
        /// Verifies the confirm button closes the prompt and emits confirmation.
        /// </summary>
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

        /// <summary>
        /// Verifies the cancel button closes the prompt and emits cancellation.
        /// </summary>
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

        /// <summary>
        /// Verifies destruction removes button listeners owned by the dialog.
        /// </summary>
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
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(ConfirmationDialogView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
