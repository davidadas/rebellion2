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
        private OptionsSaveListView _saveListView;

        /// <summary>
        /// Creates the generated Options menu view for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _root.GetComponent<OptionsMenuView>();
            _saveListView = _root.GetComponentInChildren<OptionsSaveListView>(true);
            UIComponentTestHelper.InvokeLifecycle(_saveListView, "Awake");
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
        /// Verifies Options artwork is authored with stable content bindings and explicit borders.
        /// </summary>
        [Test]
        public void Artwork_GeneratedPrefab_UsesContentPipelineBindings()
        {
            string[] expectedBoundAddresses =
            {
                "Application/OptionsMenu/UI/ui_settingsmenu_badge_background",
                "Application/OptionsMenu/UI/ui_settingsmenu_frame_overlay",
                "Application/OptionsMenu/UI/ui_settingsmenu_panel_background",
                "Application/OptionsMenu/UI/ui_settingsmenu_row_background",
                "Application/OptionsMenu/UI/ui_settingsmenu_toggle_icon",
            };
            ContentSpriteBinding[] bindings = _root
                .GetComponentsInChildren<ContentSpriteBinding>(true)
                .Where(binding =>
                    binding.Address.StartsWith(
                        "Application/OptionsMenu/UI/ui_settingsmenu_",
                        StringComparison.Ordinal
                    )
                )
                .ToArray();

            CollectionAssert.IsSubsetOf(
                expectedBoundAddresses,
                bindings.Select(binding => binding.Address).Distinct().ToArray()
            );
            foreach (ContentSpriteBinding binding in bindings)
            {
                Image image = binding.GetComponent<Image>();
                Assert.IsNotNull(image.sprite, binding.Address);
                Assert.AreEqual(binding.Border, image.sprite.border, binding.Address);
            }

            Assert.AreEqual(
                "Application/OptionsMenu/UI/ui_settingsmenu_row_background",
                GetField<string>("_rowIdleSpriteAddress")
            );
            Assert.AreEqual(
                "Application/OptionsMenu/UI/ui_settingsmenu_row_selected_background",
                GetField<string>("_rowActiveSpriteAddress")
            );
            foreach (
                OptionsToggleRowView row in _root.GetComponentsInChildren<OptionsToggleRowView>(
                    true
                )
            )
            {
                Assert.AreEqual(
                    "Application/OptionsMenu/UI/ui_settingsmenu_toggle_icon",
                    typeof(OptionsToggleRowView)
                        .GetField(
                            "_offSpriteAddress",
                            BindingFlags.Instance | BindingFlags.NonPublic
                        )
                        .GetValue(row)
                );
                Assert.AreEqual(
                    "Application/OptionsMenu/UI/ui_settingsmenu_toggle_selected_icon",
                    typeof(OptionsToggleRowView)
                        .GetField(
                            "_onSpriteAddress",
                            BindingFlags.Instance | BindingFlags.NonPublic
                        )
                        .GetValue(row)
                );
            }

            Assert.IsTrue(
                _root
                    .GetComponentsInChildren<ContentTextureBinding>(true)
                    .Any(binding =>
                        binding.Address == "Application/OptionsMenu/UI/ui_settingsmenu_slider_knob"
                    )
            );
        }

        /// <summary>
        /// Verifies save-list widget ownership belongs to an authored subview.
        /// </summary>
        [Test]
        public void SaveLoadPage_UsesAuthoredSaveListSubview()
        {
            Assert.IsNotNull(_saveListView);
            Assert.AreSame(_saveListView, GetField<OptionsSaveListView>("_saveListView"));
            Assert.AreEqual("SaveLoadPage", _saveListView.name);
        }

        /// <summary>
        /// Verifies entering Save/Load starts at the top without pinning later renders there.
        /// </summary>
        [Test]
        public void SaveLoadPage_Entered_ScrollsToTopOnce()
        {
            OptionsSaveSlot[] slots = Enumerable
                .Range(0, 12)
                .Select(index => new OptionsSaveSlot(
                    $"Save {index}",
                    "Today",
                    null,
                    false,
                    $"save_{index}"
                ))
                .ToArray();
            ScrollRect scrollRect = _saveListView.GetComponentInChildren<ScrollRect>(true);

            _view.Render(CreateRenderData(slots));

            Assert.AreEqual(1f, scrollRect.verticalNormalizedPosition, 0.01f);

            scrollRect.verticalNormalizedPosition = 0.5f;
            _view.Render(CreateRenderData(slots));

            Assert.AreEqual(0.5f, scrollRect.verticalNormalizedPosition, 0.01f);

            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Graphics, slots));
            scrollRect.verticalNormalizedPosition = 0.5f;
            _view.Render(CreateRenderData(slots));

            Assert.AreEqual(1f, scrollRect.verticalNormalizedPosition, 0.01f);
        }

        /// <summary>
        /// Verifies entering Controls starts at the top without pinning later renders there.
        /// </summary>
        [Test]
        public void ControlsPage_Entered_ScrollsToTopOnce()
        {
            OptionsBindingRow[] bindings = Enumerable
                .Range(0, 24)
                .Select(index => new OptionsBindingRow($"Action {index}", "A"))
                .ToArray();
            ScrollRect scrollRect = GetField<ScrollAreaView>("_controlsScrollArea")
                .GetComponentInChildren<ScrollRect>(true);

            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls, bindings: bindings));

            Assert.AreEqual(1f, scrollRect.verticalNormalizedPosition, 0.01f);

            scrollRect.verticalNormalizedPosition = 0.5f;
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls, bindings: bindings));

            Assert.AreEqual(0.5f, scrollRect.verticalNormalizedPosition, 0.01f);

            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Graphics));
            scrollRect.verticalNormalizedPosition = 0.5f;
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls, bindings: bindings));

            Assert.AreEqual(1f, scrollRect.verticalNormalizedPosition, 0.01f);
        }

        /// <summary>
        /// Verifies the reserved Escape badge is locked while its additional binding remains editable.
        /// </summary>
        [Test]
        public void ControlsPage_OpenGameMenuRow_LocksPrimaryBadgeOnly()
        {
            OptionsBindingRow[] bindings =
            {
                new OptionsBindingRow(
                    "Open Game Menu",
                    "ESC",
                    "UNBOUND",
                    primaryEditable: false
                ),
            };

            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls, bindings: bindings));

            Button primary = _root
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "BindingPrimaryBadge0");
            Button secondary = _root
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "BindingSecondaryBadge0");
            Assert.IsFalse(primary.interactable);
            Assert.IsTrue(secondary.interactable);
        }

        /// <summary>
        /// Verifies every interactive control-row button uses the standard Options feedback states.
        /// </summary>
        [Test]
        public void ControlsPage_InteractiveButtons_UseOptionsFeedbackStates()
        {
            OptionsBindingRow[] bindings = { new OptionsBindingRow("Show Troopers", "T") };
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls, bindings: bindings));

            string[] buttonNames =
            {
                "BindingPrimaryBadge0",
                "BindingSecondaryBadge0",
                "BindingRestore0",
            };
            foreach (string buttonName in buttonNames)
            {
                Button button = _root
                    .GetComponentsInChildren<Button>(true)
                    .Single(candidate => candidate.name == buttonName);
                Assert.AreEqual(Selectable.Transition.ColorTint, button.transition, buttonName);
                Assert.AreNotEqual(button.colors.normalColor, button.colors.highlightedColor, buttonName);
                Assert.AreNotEqual(button.colors.normalColor, button.colors.pressedColor, buttonName);
            }
        }

        /// <summary>
        /// Verifies Controls precedes Save / Load in both tab presentation and selection routing.
        /// </summary>
        [Test]
        public void Tabs_PresentControlsBeforeSaveLoad_AndRouteSelections()
        {
            CollectionAssert.AreEqual(
                new[] { "GRAPHICS", "AUDIO", "CONTROLS", "SAVE / LOAD" },
                GetField<TextMeshProUGUI[]>("_tabLabelFields").Select(label => label.text).ToArray()
            );

            OptionsMenuTab selectedTab = OptionsMenuTab.Graphics;
            _view.TabSelected += tab => selectedTab = tab;
            Button[] tabButtons = GetField<Button[]>("_tabButtons");

            tabButtons[2].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.Controls, selectedTab);
            tabButtons[3].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.SaveLoad, selectedTab);
        }

        /// <summary>
        /// Verifies each controls row exposes its own restore-default request.
        /// </summary>
        [Test]
        public void ControlsPage_RestoreButton_ClickRaisesBindingRowRequest()
        {
            int restoredRow = -1;
            _view.BindingRestoreRequested += row => restoredRow = row;
            OptionsBindingRow[] bindings = { new OptionsBindingRow("Show Troopers", "T") };
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls, bindings: bindings));

            _root
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "BindingRestore0")
                .onClick.Invoke();

            Assert.AreEqual(0, restoredRow);
        }

        /// <summary>
        /// Verifies the per-binding restore control uses the authored reset icon rather than text.
        /// </summary>
        [Test]
        public void ControlsPage_RestoreTemplate_UsesContentBoundResetIcon()
        {
            Image template = GetField<Image>("_bindingRestoreTemplate");
            RawImage icon = template.GetComponentInChildren<RawImage>(true);

            Assert.IsNotNull(icon);
            Assert.AreEqual(
                "Application/OptionsMenu/UI/ui_settingsmenu_restore_default_icon",
                icon.GetComponent<ContentTextureBinding>().Address
            );
            Assert.IsEmpty(template.GetComponentsInChildren<TextMeshProUGUI>(true));
        }

        /// <summary>
        /// Verifies the Controls-page global defaults action is labeled as a restore-all action.
        /// </summary>
        [Test]
        public void SettingsPages_DefaultsAction_UsesRestoreDefaultsLabel()
        {
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls));

            Assert.AreEqual(
                "RESTORE DEFAULTS",
                GetField<Button>("_defaultsButton").GetComponentInChildren<TextMeshProUGUI>().text
            );
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
            _saveListView.AlignRenameInput();

            TMP_InputField input = GetSaveListField<TMP_InputField>("_renameField");
            Assert.IsTrue(input.customCaretColor);
            Assert.AreEqual(Color.white, input.caretColor);
            Assert.AreEqual(2, input.caretWidth);
            Assert.AreEqual(0.85f, input.caretBlinkRate);
            Assert.AreEqual(SaveGameManager.MaxDisplayNameLength, input.characterLimit);
            Assert.IsFalse(input.onFocusSelectAll);
            Assert.AreSame(input.transform, input.textViewport);
            Assert.IsNotNull(input.textViewport.GetComponent<RectMask2D>());
            Assert.AreEqual(
                TextAlignmentOptions.BaselineLeft,
                ((TextMeshProUGUI)input.textComponent).alignment
            );
            Assert.AreEqual(-2f, ((RectTransform)input.textComponent.transform).offsetMin.y);
            Assert.AreEqual(-2f, ((RectTransform)input.textComponent.transform).offsetMax.y);
        }

        /// <summary>
        /// Verifies taller glyph geometry cannot move the rename text baseline or caret container.
        /// </summary>
        [Test]
        public void RenameInput_TallGlyph_KeepsStableBaselineAndTextBounds()
        {
            _saveListView.AlignRenameInput();
            TMP_InputField input = GetSaveListField<TMP_InputField>("_renameField");
            TextMeshProUGUI text = (TextMeshProUGUI)input.textComponent;
            input.gameObject.SetActive(true);

            input.SetTextWithoutNotify("A");
            input.ForceLabelUpdate();
            text.ForceMeshUpdate();
            float ordinaryBaseline = text.textInfo.characterInfo[0].baseLine;
            Vector2 ordinaryPosition = text.rectTransform.anchoredPosition;
            Vector2 ordinarySize = text.rectTransform.sizeDelta;

            input.SetTextWithoutNotify("(");
            input.ForceLabelUpdate();
            text.ForceMeshUpdate();

            Assert.AreEqual(ordinaryBaseline, text.textInfo.characterInfo[0].baseLine, 0.01f);
            Assert.AreEqual(ordinaryPosition, text.rectTransform.anchoredPosition);
            Assert.AreEqual(ordinarySize, text.rectTransform.sizeDelta);
        }

        /// <summary>
        /// Verifies presentation clips stored metadata without silently normalizing it.
        /// </summary>
        [Test]
        public void SaveList_OverlongStoredName_TruncatesWithoutRewritingDomainData()
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
            Assert.AreEqual(TextWrappingModes.NoWrap, renderedName.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Truncate, renderedName.overflowMode);
            float renderedLineHeight =
                renderedName.font.faceInfo.lineHeight
                * renderedName.fontSize
                / renderedName.font.faceInfo.pointSize;
            Assert.GreaterOrEqual(renderedName.rectTransform.rect.height, renderedLineHeight);
        }

        /// <summary>
        /// Verifies a long save label is clipped before the row's delete control.
        /// </summary>
        [Test]
        public void SaveList_LongName_LabelEndsBeforeDeleteControl()
        {
            OptionsSaveSlot savedGame = new OptionsSaveSlot(
                new string('N', SaveGameManager.MaxDisplayNameLength),
                "Today",
                null,
                false,
                "test_save"
            );

            _view.Render(CreateRenderData(savedGame));

            RectInt nameRect = UILayout.GetSourceRect(
                _root
                    .GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Single(text => text.name == "SlotName0")
                    .rectTransform
            );
            RectInt deleteRect = UILayout.GetSourceRect(
                (RectTransform)
                    _root
                        .GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "SlotDelete0")
                        .transform
            );
            Assert.Less(nameRect.xMax, deleteRect.xMin);
        }

        /// <summary>
        /// Verifies inline editing hides the static save label beneath the input field.
        /// </summary>
        [Test]
        public void SaveList_Renaming_HidesStaticNameUntilEditingCloses()
        {
            OptionsSaveSlot savedGame = new OptionsSaveSlot(
                "Test Save",
                "Today",
                null,
                false,
                "test_save"
            );
            OptionsMenuRenderData data = CreateRenderData(savedGame);
            _view.Render(data);
            TextMeshProUGUI renderedName = _root
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "SlotName0");

            typeof(OptionsSaveListView)
                .GetMethod("BeginRename", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_saveListView, new object[] { 0 });
            _view.Render(data);

            Assert.IsFalse(renderedName.gameObject.activeSelf);

            _saveListView.CancelRename();

            Assert.IsTrue(renderedName.gameObject.activeSelf);
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
        /// Reads a private authored reference from the save-list subview under test.
        /// </summary>
        private T GetSaveListField<T>(string fieldName)
        {
            return (T)
                typeof(OptionsSaveListView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_saveListView);
        }

        /// <summary>
        /// Creates minimal render state for save-list presentation tests.
        /// </summary>
        private static OptionsMenuRenderData CreateRenderData(params OptionsSaveSlot[] saveSlots)
        {
            return CreateRenderDataForTab(OptionsMenuTab.SaveLoad, saveSlots);
        }

        /// <summary>
        /// Creates minimal render state for a selected Options menu page.
        /// </summary>
        private static OptionsMenuRenderData CreateRenderDataForTab(
            OptionsMenuTab activeTab,
            OptionsSaveSlot[] saveSlots = null,
            OptionsBindingRow[] bindings = null
        )
        {
            return new OptionsMenuRenderData(
                0,
                0,
                activeTab,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                bindings ?? Array.Empty<OptionsBindingRow>(),
                saveSlots ?? Array.Empty<OptionsSaveSlot>(),
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
