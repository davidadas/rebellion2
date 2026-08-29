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
        /// Verifies Awake permits content-backed sprites to be restored after instantiation.
        /// </summary>
        [Test]
        public void Awake_ContentNotInitialized_DoesNotThrow()
        {
            GameObject root = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                OptionsMenuView view = root.GetComponent<OptionsMenuView>();
                OptionsSaveListView saveListView = root.GetComponentInChildren<OptionsSaveListView>(
                    true
                );
                SetField(view, "_rowIdleSprite", null);
                SetField(view, "_rowActiveSprite", null);
                SetField(saveListView, "_rowIdleSprite", null);
                SetField(saveListView, "_rowActiveSprite", null);

                Assert.DoesNotThrow(() =>
                    UIComponentTestHelper.InvokeLifecycle(saveListView, "Awake")
                );
                Assert.DoesNotThrow(() => UIComponentTestHelper.InvokeLifecycle(view, "Awake"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
        /// Verifies an unsettled simulation disables overwriting the selected save.
        /// </summary>
        [Test]
        public void SaveLoadPage_UnsettledGame_DisablesSaveButton()
        {
            OptionsMenuRenderData data = new OptionsMenuRenderData(
                0,
                0,
                OptionsMenuTab.SaveLoad,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                Array.Empty<OptionsBindingRow>(),
                new[] { new OptionsSaveSlot("Save", "Today", null, false, "save") },
                0,
                true,
                false,
                -1,
                false
            );

            _view.Render(data);

            Button saveButton = (Button)
                typeof(OptionsSaveListView)
                    .GetField("_saveButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_saveListView);
            Assert.IsFalse(saveButton.interactable);
        }

        /// <summary>
        /// Verifies autosave numbers render in directly editable integer fields.
        /// </summary>
        [Test]
        public void GameplayPage_AutosaveFields_RenderAndRaiseEnteredValues()
        {
            string interval = null;
            string retained = null;
            _view.AutosaveIntervalChanged += value => interval = value;
            _view.AutosavesToKeepChanged += value => retained = value;
            OptionsMenuRenderData data = new OptionsMenuRenderData(
                0,
                0,
                OptionsMenuTab.Gameplay,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                Array.Empty<OptionsBindingRow>(),
                Array.Empty<OptionsSaveSlot>(),
                -1,
                true,
                true,
                -1,
                false,
                gameplayStates: new Dictionary<UserGameplayOption, bool>
                {
                    [UserGameplayOption.AutosaveEnabled] = true,
                },
                autosaveIntervalTicks: 250,
                autosavesToKeep: 7
            );

            _view.Render(data);
            TMP_InputField intervalField = GetField<TMP_InputField>("_autosaveIntervalInputField");
            TMP_InputField retainedField = GetField<TMP_InputField>("_autosavesToKeepInputField");
            intervalField.onEndEdit.Invoke("300");
            retainedField.onEndEdit.Invoke("8");

            Assert.AreEqual(TMP_InputField.ContentType.IntegerNumber, intervalField.contentType);
            Assert.AreEqual("250", intervalField.text);
            Assert.AreEqual("7", retainedField.text);
            Assert.AreEqual("300", interval);
            Assert.AreEqual("8", retained);
            RectTransform badge = _root
                .GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "AutosaveIntervalBadge");
            Assert.AreEqual(56f, badge.sizeDelta.x);
            RectTransform inputRect = (RectTransform)intervalField.transform;
            Assert.AreEqual(badge.anchoredPosition.y, inputRect.anchoredPosition.y);
            Assert.AreEqual(18f, inputRect.sizeDelta.y);
            Assert.AreNotSame(inputRect, intervalField.textViewport);
            Assert.AreEqual(-1f, intervalField.textViewport.anchoredPosition.y);
            Assert.AreEqual(16f, intervalField.textViewport.sizeDelta.y);
            Assert.AreEqual(TextAlignmentOptions.Center, intervalField.textComponent.alignment);
            Assert.AreEqual(0f, intervalField.textComponent.rectTransform.anchoredPosition.y);
            Assert.AreEqual(16f, intervalField.textComponent.rectTransform.sizeDelta.y);
            Assert.IsTrue(intervalField.interactable);
            Assert.IsTrue(retainedField.interactable);
        }

        /// <summary>
        /// Verifies disabling autosave also disables and dims its dependent values.
        /// </summary>
        [Test]
        public void GameplayPage_AutosaveDisabled_DisablesNumericFields()
        {
            OptionsMenuRenderData data = new OptionsMenuRenderData(
                0,
                0,
                OptionsMenuTab.Gameplay,
                string.Empty,
                string.Empty,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<float>(),
                Array.Empty<OptionsBindingRow>(),
                Array.Empty<OptionsSaveSlot>(),
                -1,
                true,
                true,
                -1,
                false,
                new Dictionary<UserGameplayOption, bool>
                {
                    [UserGameplayOption.AutosaveEnabled] = false,
                }
            );

            _view.Render(data);

            TMP_InputField intervalField = GetField<TMP_InputField>("_autosaveIntervalInputField");
            TMP_InputField retainedField = GetField<TMP_InputField>("_autosavesToKeepInputField");
            Assert.IsFalse(intervalField.interactable);
            Assert.IsFalse(retainedField.interactable);
            Assert.AreEqual(0.6f, GetField<Image>("_autosaveIntervalBadgeImage").color.r, 0.001f);
        }

        /// <summary>
        /// Verifies the Gameplay mode and footer use the requested player-facing labels.
        /// </summary>
        [Test]
        public void GeneratedLabels_UseGalacticModeAndReturnWording()
        {
            string[] labels = _root
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(field => field.text)
                .ToArray();

            CollectionAssert.Contains(labels, "GALACTIC MODE");
            CollectionAssert.Contains(labels, "RETURN TO GAME");
            CollectionAssert.Contains(labels, "RETURN TO MAIN MENU");
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
                new OptionsBindingRow("Open Game Menu", "ESC", "UNBOUND", primaryEditable: false),
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
                Assert.AreNotEqual(
                    button.colors.normalColor,
                    button.colors.highlightedColor,
                    buttonName
                );
                Assert.AreNotEqual(
                    button.colors.normalColor,
                    button.colors.pressedColor,
                    buttonName
                );
                Assert.Greater(
                    button.colors.highlightedColor.grayscale,
                    button.colors.normalColor.grayscale,
                    buttonName
                );
            }
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
            Assert.AreEqual(new RectInt(9, 2, 12, 12), UILayout.GetSourceRect(icon.rectTransform));
            Assert.IsEmpty(template.GetComponentsInChildren<TextMeshProUGUI>(true));
        }

        /// <summary>
        /// Verifies Gameplay is first and Controls precedes Save / Load in selection routing.
        /// </summary>
        [Test]
        public void Tabs_PresentControlsBeforeSaveLoad_AndRouteSelections()
        {
            CollectionAssert.AreEqual(
                new[] { "GAMEPLAY", "GRAPHICS", "AUDIO", "CONTROLS", "SAVE / LOAD" },
                GetField<TextMeshProUGUI[]>("_tabLabelFields").Select(label => label.text).ToArray()
            );

            OptionsMenuTab selectedTab = OptionsMenuTab.Gameplay;
            _view.TabSelected += tab => selectedTab = tab;
            Button[] tabButtons = GetField<Button[]>("_tabButtons");

            tabButtons[3].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.Controls, selectedTab);
            tabButtons[4].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.SaveLoad, selectedTab);
        }

        /// <summary>
        /// Verifies the settings-page defaults action uses the expected label.
        /// </summary>
        [Test]
        public void SettingsPages_DefaultsAction_UsesApplyDefaultsLabel()
        {
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls));

            Assert.AreEqual(
                "APPLY DEFAULTS",
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
        /// Verifies every left-side navigation row uses the same height and vertical gap.
        /// </summary>
        [Test]
        public void NavigationRows_UseConsistentVerticalRhythm()
        {
            Button[] tabRows = GetField<Button[]>("_tabButtons");
            Button[] footerRows =
            {
                GetField<Button>("_backToGameButton"),
                GetField<Button>("_mainMenuButton"),
                GetField<Button>("_quitButton"),
            };
            RectInt[] tabRects = tabRows
                .Select(row => UILayout.GetSourceRect((RectTransform)row.transform))
                .ToArray();
            RectInt[] footerRects = footerRows
                .Select(row => UILayout.GetSourceRect((RectTransform)row.transform))
                .ToArray();
            RectInt footerRoot = UILayout.GetSourceRect(
                (RectTransform)footerRows[0].transform.parent
            );

            CollectionAssert.AreEqual(
                new[] { 82, 118, 154, 190, 226 },
                tabRects.Select(rect => rect.y).ToArray()
            );
            CollectionAssert.AreEqual(
                new[] { 0, 36, 72 },
                footerRects.Select(rect => rect.y).ToArray()
            );
            Assert.AreEqual(new RectInt(38, 318, 163, 102), footerRoot);
            Assert.IsTrue(tabRects.Concat(footerRects).All(rect => rect.height == 30));
        }

        /// <summary>
        /// Verifies main-menu hosting replaces Back to Game with Back to Main Menu without gaps.
        /// </summary>
        [Test]
        public void RenderFooter_MainMenuHost_ShowsBackToMainMenuAndQuitWithoutGap()
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
            Assert.IsTrue(mainMenu.gameObject.activeSelf);
            Assert.IsTrue(quit.gameObject.activeSelf);
            Assert.IsNotNull(quit.transform.parent.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(0, UILayout.GetSourceRect((RectTransform)mainMenu.transform).x);
            Assert.AreEqual(36, UILayout.GetSourceRect((RectTransform)mainMenu.transform).y);
            Assert.AreEqual(0, UILayout.GetSourceRect((RectTransform)quit.transform).x);
            Assert.AreEqual(72, UILayout.GetSourceRect((RectTransform)quit.transform).y);
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

        private static void SetField(object target, string fieldName, object value)
        {
            target
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
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
                true,
                true,
                -1,
                false
            );
        }
    }
}
