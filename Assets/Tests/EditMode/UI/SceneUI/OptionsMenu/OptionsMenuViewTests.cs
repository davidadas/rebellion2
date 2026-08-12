using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    [TestFixture]
    public sealed class OptionsMenuViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";

        private GameObject _root;
        private OptionsMenuView _view;

        /// <summary>
        /// Creates the generated Options menu view for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _root.GetComponent<OptionsMenuView>();
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        /// <summary>
        /// Destroys the generated Options menu instance after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// Verifies display arrows emit their semantic direction requests.
        /// </summary>
        [Test]
        public void DisplaySteppers_Click_RaiseSemanticRequests()
        {
            int resolutionDelta = 0;
            int fullScreenDelta = 0;
            _view.ResolutionStepRequested += delta => resolutionDelta = delta;
            _view.FullScreenStepRequested += delta => fullScreenDelta = delta;

            GetField<Button>("_resolutionNextButton").onClick.Invoke();
            GetField<Button>("_fullScreenPrevButton").onClick.Invoke();

            Assert.AreEqual(1, resolutionDelta);
            Assert.AreEqual(-1, fullScreenDelta);
        }

        /// <summary>
        /// Verifies display stepper hit targets cover the complete value badge.
        /// </summary>
        [Test]
        public void DisplaySteppers_Awake_ExpandAcrossCompleteValueBadge()
        {
            RectTransform resolutionPrevious = (RectTransform)
                GetField<Button>("_resolutionPrevButton").transform;
            RectTransform resolutionNext = (RectTransform)
                GetField<Button>("_resolutionNextButton").transform;
            RectTransform fullScreenPrevious = (RectTransform)
                GetField<Button>("_fullScreenPrevButton").transform;
            RectTransform fullScreenNext = (RectTransform)
                GetField<Button>("_fullScreenNextButton").transform;

            Assert.AreEqual(78f, resolutionPrevious.sizeDelta.x);
            Assert.AreEqual(77f, resolutionNext.sizeDelta.x);
            Assert.AreEqual(78f, fullScreenPrevious.sizeDelta.x);
            Assert.AreEqual(77f, fullScreenNext.sizeDelta.x);
            Assert.AreEqual(272f, resolutionNext.anchoredPosition.x);
            Assert.AreEqual(272f, fullScreenNext.anchoredPosition.x);
        }

        /// <summary>
        /// Verifies the rename input authors a visible, correctly aligned caret.
        /// </summary>
        [Test]
        public void RenameInput_AlignmentConfiguresVisibleBlinkingCaret()
        {
            _view.AlignRenameInput();

            TMP_InputField input = GetField<TMP_InputField>("_slotRenameField");
            Assert.IsTrue(input.customCaretColor);
            Assert.AreEqual(Color.white, input.caretColor);
            Assert.AreEqual(2, input.caretWidth);
            Assert.AreEqual(0.85f, input.caretBlinkRate);
            Assert.AreEqual(SaveGameManager.MaxDisplayNameLength, input.characterLimit);
            Assert.IsFalse(input.onFocusSelectAll);
            Assert.AreSame(input.transform, input.textViewport);
            Assert.AreEqual(
                TextAlignmentOptions.MidlineLeft,
                ((TextMeshProUGUI)input.textComponent).alignment
            );
        }

        /// <summary>
        /// Verifies presentation renders stored metadata without silently normalizing it.
        /// </summary>
        [Test]
        public void SaveList_OverlongStoredName_DoesNotRewriteDomainData()
        {
            string storedName = new string('N', SaveGameManager.MaxDisplayNameLength + 10);
            OptionsSaveSlot savedGame = new OptionsSaveSlot(
                storedName,
                "Today",
                null,
                false,
                "test_save"
            );

            _view.Render(CreateRenderData(savedGame));

            TextMeshProUGUI renderedName = _root
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "SlotName0");
            Assert.AreEqual(storedName, renderedName.text);
        }

        /// <summary>
        /// Verifies pooled save rows reactivate faction icons when reused.
        /// </summary>
        [Test]
        public void SaveList_AfterRowCountGrows_ReactivatesFactionIcon()
        {
            Texture2D factionIcon = new Texture2D(16, 16);
            try
            {
                OptionsSaveSlot createNew = new OptionsSaveSlot(
                    "Create New Save",
                    string.Empty,
                    null,
                    true,
                    null
                );
                OptionsSaveSlot savedGame = new OptionsSaveSlot(
                    "Test Save",
                    "Today",
                    factionIcon,
                    false,
                    "test_save"
                );

                _view.Render(CreateRenderData(createNew, savedGame));
                _view.Render(CreateRenderData(createNew));
                _view.Render(CreateRenderData(createNew, savedGame));

                RawImage renderedIcon = _root
                    .GetComponentsInChildren<RawImage>(true)
                    .Single(image => image.name == "SlotIcon1");
                Assert.IsTrue(renderedIcon.gameObject.activeSelf);
                Assert.IsTrue(renderedIcon.enabled);
                Assert.AreSame(factionIcon, renderedIcon.texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(factionIcon);
            }
        }

        /// <summary>
        /// Verifies main-menu hosting removes unavailable in-game footer rows without gaps.
        /// </summary>
        [Test]
        public void RenderFooter_MainMenuHost_HidesUnavailableRowsAndCollapsesQuitToTop()
        {
            OptionsMenuRenderData data = new OptionsMenuRenderData(
                0,
                0,
                OptionsMenuTab.Graphics,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                Array.Empty<OptionsBindingRow>(),
                Array.Empty<OptionsSaveSlot>(),
                -1,
                false,
                true,
                false,
                false,
                -1,
                false
            );

            typeof(OptionsMenuView)
                .GetMethod("RenderFooter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_view, new object[] { data });

            Button backToGame = GetField<Button>("_backToGameButton");
            Button mainMenu = GetField<Button>("_mainMenuButton");
            Button quit = GetField<Button>("_quitButton");
            Assert.IsFalse(backToGame.gameObject.activeSelf);
            Assert.IsFalse(mainMenu.gameObject.activeSelf);
            Assert.IsTrue(quit.gameObject.activeSelf);
            Assert.IsNotNull(quit.transform.parent.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(38, UILayout.GetSourceRect((RectTransform)quit.transform).x);
            Assert.AreEqual(226, UILayout.GetSourceRect((RectTransform)quit.transform).y);
        }

        /// <summary>
        /// Verifies binding-row clicks retain their model index after section headers.
        /// </summary>
        [Test]
        public void Controls_FirstActionAfterHeader_RaisesModelBindingIndex()
        {
            int requestedRow = -1;
            _view.RebindRequested += (row, _) => requestedRow = row;
            OptionsMenuRenderData data = new OptionsMenuRenderData(
                0,
                0,
                OptionsMenuTab.Controls,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                new[]
                {
                    new OptionsBindingRow("Strategy", string.Empty, string.Empty, true),
                    new OptionsBindingRow("Show Troopers", "N"),
                },
                Array.Empty<OptionsSaveSlot>(),
                -1,
                false,
                false,
                true,
                true,
                -1,
                false
            );

            _view.Render(data);
            GetField<List<Image>>("_bindingBadgeImages")[0].GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1, requestedRow);
        }

        /// <summary>
        /// Reads a private authored reference from the view under test.
        /// </summary>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(OptionsMenuView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        /// <summary>
        /// Creates minimal render state for save-list presentation tests.
        /// </summary>
        private static OptionsMenuRenderData CreateRenderData(params OptionsSaveSlot[] saveSlots)
        {
            return new OptionsMenuRenderData(
                0,
                0,
                OptionsMenuTab.SaveLoad,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                Array.Empty<OptionsBindingRow>(),
                saveSlots,
                -1,
                false,
                true,
                true,
                true,
                -1,
                false
            );
        }
    }
}
