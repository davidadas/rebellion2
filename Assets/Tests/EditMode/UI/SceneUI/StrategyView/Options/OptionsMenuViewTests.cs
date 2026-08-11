using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Options
{
    [TestFixture]
    public sealed class OptionsMenuViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";

        private GameObject root;
        private OptionsMenuView view;

        [SetUp]
        public void SetUp()
        {
            root = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            view = root.GetComponent<OptionsMenuView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DisplaySteppers_Click_RaiseSemanticRequests()
        {
            int resolutionDelta = 0;
            int fullScreenDelta = 0;
            view.ResolutionStepRequested += delta => resolutionDelta = delta;
            view.FullScreenStepRequested += delta => fullScreenDelta = delta;

            GetField<Button>("resolutionNextButton").onClick.Invoke();
            GetField<Button>("fullScreenPrevButton").onClick.Invoke();

            Assert.AreEqual(1, resolutionDelta);
            Assert.AreEqual(-1, fullScreenDelta);
        }

        [Test]
        public void DisplaySteppers_Awake_ExpandAcrossCompleteValueBadge()
        {
            RectTransform resolutionPrevious =
                (RectTransform)GetField<Button>("resolutionPrevButton").transform;
            RectTransform resolutionNext =
                (RectTransform)GetField<Button>("resolutionNextButton").transform;
            RectTransform fullScreenPrevious =
                (RectTransform)GetField<Button>("fullScreenPrevButton").transform;
            RectTransform fullScreenNext =
                (RectTransform)GetField<Button>("fullScreenNextButton").transform;

            Assert.AreEqual(78f, resolutionPrevious.sizeDelta.x);
            Assert.AreEqual(77f, resolutionNext.sizeDelta.x);
            Assert.AreEqual(78f, fullScreenPrevious.sizeDelta.x);
            Assert.AreEqual(77f, fullScreenNext.sizeDelta.x);
            Assert.AreEqual(272f, resolutionNext.anchoredPosition.x);
            Assert.AreEqual(272f, fullScreenNext.anchoredPosition.x);
        }

        [Test]
        public void RenameInput_AlignmentConfiguresVisibleBlinkingCaret()
        {
            typeof(OptionsMenuView)
                .GetMethod("AlignRenameInput", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);

            TMP_InputField input = GetField<TMP_InputField>("slotRenameField");
            Assert.IsTrue(input.customCaretColor);
            Assert.AreEqual(Color.white, input.caretColor);
            Assert.AreEqual(2, input.caretWidth);
            Assert.AreEqual(0.85f, input.caretBlinkRate);
            Assert.IsFalse(input.onFocusSelectAll);
            Assert.AreSame(input.transform, input.textViewport);
            Assert.AreEqual(
                TextAlignmentOptions.MidlineLeft,
                ((TextMeshProUGUI)input.textComponent).alignment
            );
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(OptionsMenuView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);
        }
    }
}
