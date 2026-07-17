using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuConfirmDialogViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuWindow.prefab";

        private GameObject _rootObject;
        private SaveMenuConfirmDialogView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<SaveMenuConfirmDialogView>(true);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
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

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(SaveMenuConfirmDialogView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
