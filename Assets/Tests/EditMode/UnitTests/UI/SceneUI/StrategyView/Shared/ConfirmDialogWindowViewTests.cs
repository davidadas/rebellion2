using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public class ConfirmDialogWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/ConfirmDialogWindow.prefab";

        private Texture2D _backgroundTexture;
        private GameObject _rootObject;
        private Texture2D _titleTexture;
        private ConfirmDialogWindowView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<ConfirmDialogWindowView>();
            _backgroundTexture = new Texture2D(330, 220);
            _titleTexture = new Texture2D(120, 20);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_titleTexture);
            UnityEngine.Object.DestroyImmediate(_backgroundTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_CompletePresentation_AppliesPositionArtworkControlsAndLines()
        {
            ConfirmDialogWindowRenderData data = CreateData("Confirm move", "Coruscant");

            _view.Render(data);

            RectInt windowBounds = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(125, windowBounds.x);
            Assert.AreEqual(80, windowBounds.y);
            Assert.AreSame(
                _backgroundTexture,
                GetField<RawImage>(_view, "backgroundImage").texture
            );
            Assert.AreSame(_titleTexture, GetField<RawImage>(_view, "titleImage").texture);
            Assert.AreSame(
                GetField<Texture2D>(_view, "confirmButtonUpTexture"),
                GetField<RawImage>(_view, "confirmButtonImage").texture
            );
            Assert.AreSame(
                GetField<Texture2D>(_view, "cancelButtonUpTexture"),
                GetField<RawImage>(_view, "cancelButtonImage").texture
            );
            List<TextMeshProUGUI> lines = GetField<List<TextMeshProUGUI>>(_view, "lineTextFields");
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("Confirm move", lines[0].text);
            Assert.AreEqual("Coruscant", lines[1].text);
            Assert.AreEqual(Color.white, lines[0].color);
            Assert.IsTrue(lines[0].gameObject.activeSelf);
            Assert.IsTrue(lines[1].gameObject.activeSelf);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void Render_ShorterPresentation_ReusesFirstLineAndHidesSurplusLines()
        {
            _view.Render(CreateData("Confirm move", "Coruscant"));
            List<TextMeshProUGUI> originalLines = new List<TextMeshProUGUI>(
                GetField<List<TextMeshProUGUI>>(_view, "lineTextFields")
            );

            _view.Render(CreateData("Cancel move"));

            List<TextMeshProUGUI> lines = GetField<List<TextMeshProUGUI>>(_view, "lineTextFields");
            Assert.AreEqual(2, lines.Count);
            Assert.AreSame(originalLines[0], lines[0]);
            Assert.AreSame(originalLines[1], lines[1]);
            Assert.AreEqual("Cancel move", lines[0].text);
            Assert.IsTrue(lines[0].gameObject.activeSelf);
            Assert.IsFalse(lines[1].gameObject.activeSelf);
        }

        [Test]
        public void Render_NullLineCollection_IsRejectedByRenderData()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogWindowRenderData(125, 80, _backgroundTexture, _titleTexture, null)
            );
        }

        [Test]
        public void ConfirmButton_Click_RaisesAcceptedChoice()
        {
            bool? confirmed = null;
            ConfirmDialogWindowView requestedView = null;
            _view.ChoiceRequested += (view, choice) =>
            {
                requestedView = view;
                confirmed = choice;
            };

            GetField<Button>(_view, "confirmButton").onClick.Invoke();

            Assert.AreSame(_view, requestedView);
            Assert.AreEqual(true, confirmed);
        }

        [Test]
        public void CancelButton_Click_RaisesRejectedChoice()
        {
            bool? confirmed = null;
            ConfirmDialogWindowView requestedView = null;
            _view.ChoiceRequested += (view, choice) =>
            {
                requestedView = view;
                confirmed = choice;
            };

            GetField<Button>(_view, "cancelButton").onClick.Invoke();

            Assert.AreSame(_view, requestedView);
            Assert.AreEqual(false, confirmed);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsButtonsAndRaisesDestroyedEvent()
        {
            ConfirmDialogWindowView destroyedView = null;
            int choiceCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.ChoiceRequested += (_, _) => choiceCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            GetField<Button>(_view, "confirmButton").onClick.Invoke();
            GetField<Button>(_view, "cancelButton").onClick.Invoke();

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, choiceCount);
        }

        private ConfirmDialogWindowRenderData CreateData(params string[] lines)
        {
            return new ConfirmDialogWindowRenderData(
                125,
                80,
                _backgroundTexture,
                _titleTexture,
                lines
            );
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            return (T)
                instance
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(instance);
        }
    }
}
