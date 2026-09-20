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

        [Test]
        public void SaveLoadPage_Default_UsesAuthoredSaveListSubview()
        {
            Assert.IsNotNull(_saveListView);
            Assert.AreSame(_saveListView, GetField<OptionsSaveListView>("_saveListView"));
            Assert.AreEqual("SaveLoadPage", _saveListView.name);
        }

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

        [Test]
        public void GeneratedLabels_Default_UseGalaxyViewAndReturnWording()
        {
            TextMeshProUGUI[] fields = _root.GetComponentsInChildren<TextMeshProUGUI>(true);
            string[] labels = fields.Select(field => field.text).ToArray();

            CollectionAssert.Contains(labels, "GALAXY VIEW");
            CollectionAssert.Contains(labels, "Show Idle Bar");
            CollectionAssert.Contains(labels, "Keep Idle Bar Open");
            CollectionAssert.Contains(labels, "RETURN TO GAME");
            CollectionAssert.Contains(labels, "RETURN TO MAIN MENU");

            Assert.IsFalse(fields.Any(field => field.text == "IDLE BAR"));
        }

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

        [Test]
        public void Tabs_PresentModsBeforeSaveLoad_AndRouteSelections()
        {
            CollectionAssert.AreEqual(
                new[] { "GAMEPLAY", "GRAPHICS", "AUDIO", "CONTROLS", "MODS", "SAVE / LOAD" },
                GetField<TextMeshProUGUI[]>("_tabLabelFields").Select(label => label.text).ToArray()
            );

            OptionsMenuTab selectedTab = OptionsMenuTab.Gameplay;
            _view.TabSelected += tab => selectedTab = tab;
            Button[] tabButtons = GetField<Button[]>("_tabButtons");

            tabButtons[3].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.Controls, selectedTab);
            tabButtons[4].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.Mods, selectedTab);
            tabButtons[5].onClick.Invoke();
            Assert.AreEqual(OptionsMenuTab.SaveLoad, selectedTab);
        }

        [Test]
        public void ModsPage_AvailableMods_RendersEnablementAndIdentity()
        {
            OptionsMenuRenderData data = CreateRenderDataForTab(
                OptionsMenuTab.Mods,
                mods: new[]
                {
                    new OptionsModRow("first", "1.0.0", "First Mod"),
                    new OptionsModRow("second", "2.0.0", "Second Mod", false, true),
                },
                contentPackLabel: "Classic Galactic Civil War"
            );

            _view.Render(data);

            Assert.AreEqual(
                "2 MODS LOADED",
                GetField<TextMeshProUGUI>("_modsStatusTextField").text
            );
            TextMeshProUGUI restart = GetField<TextMeshProUGUI>("_modsRestartTextField");
            Assert.IsTrue(restart.gameObject.activeSelf);
            Assert.AreEqual("RESTART REQUIRED", restart.text);
            Assert.AreEqual(FontStyles.Bold, restart.fontStyle);
            Assert.AreEqual(
                "Classic Galactic Civil War",
                GetField<TextMeshProUGUI>("_contentPackValueField").text
            );
            int packDelta = 0;
            _view.ContentPackStepRequested += delta => packDelta = delta;
            GetField<Button>("_contentPackNextButton").onClick.Invoke();
            Assert.AreEqual(1, packDelta);
            List<OptionsToggleRowView> rows = GetField<List<OptionsToggleRowView>>("_modRows");
            Assert.IsTrue(rows[0].gameObject.activeSelf);
            Assert.IsTrue(rows[1].gameObject.activeSelf);
            Assert.AreEqual(
                "First Mod",
                GetPrivateField<TextMeshProUGUI>(rows[0], "_labelTextField").text
            );
            Assert.AreEqual(
                "OFF",
                GetPrivateField<TextMeshProUGUI>(rows[1], "_stateTextField").text
            );
            int requestedMod = -1;
            _view.ModToggleRequested += index => requestedMod = index;
            GetPrivateField<Button>(rows[1], "_button").onClick.Invoke();
            Assert.AreEqual(1, requestedMod);
            Assert.IsFalse(GetField<GameObject>("_settingsActions").activeSelf);
        }

        [Test]
        public void ModsPage_ManyMods_RendersEveryMod()
        {
            OptionsModRow[] mods = Enumerable
                .Range(1, 12)
                .Select(index => new OptionsModRow($"mod-{index}", "1.0.0", $"Mod {index}"))
                .ToArray();

            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Mods, mods: mods));

            List<OptionsToggleRowView> rows = GetField<List<OptionsToggleRowView>>("_modRows");
            Assert.AreEqual(12, rows.Count);
            Assert.IsTrue(rows[11].gameObject.activeSelf);
            Assert.AreEqual(
                "Mod 12",
                GetPrivateField<TextMeshProUGUI>(rows[11], "_labelTextField").text
            );
        }

        [Test]
        public void SettingsPages_DefaultsAction_UsesApplyDefaultsLabel()
        {
            _view.Render(CreateRenderDataForTab(OptionsMenuTab.Controls));

            Assert.AreEqual(
                "APPLY DEFAULTS",
                GetField<Button>("_defaultsButton").GetComponentInChildren<TextMeshProUGUI>().text
            );
        }

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

            Button rowButton = _root
                .GetComponentsInChildren<Image>(true)
                .Single(image => image.name == "SlotRow0")
                .GetComponent<Button>();
            rowButton.onClick.Invoke();
            rowButton.onClick.Invoke();
            _view.Render(data);

            Assert.IsFalse(renderedName.gameObject.activeSelf);

            _saveListView.CancelRename();

            Assert.IsTrue(renderedName.gameObject.activeSelf);
        }

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

        [Test]
        public void Render_MainMenuHost_ShowsMainMenuAndQuitActions()
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

            _view.Render(data);

            Button backToGame = GetField<Button>("_backToGameButton");
            Button mainMenu = GetField<Button>("_mainMenuButton");
            Button quit = GetField<Button>("_quitButton");
            Assert.IsFalse(backToGame.gameObject.activeSelf);
            Assert.IsTrue(mainMenu.gameObject.activeSelf);
            Assert.IsTrue(quit.gameObject.activeSelf);
        }

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
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(OptionsMenuView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        /// <summary>
        /// Gets private field.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested private field.</returns>
        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)
                target
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(target);
        }

        /// <summary>
        /// Sets field.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The value.</param>
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
        /// <param name="saveSlots">The save slots.</param>
        /// <returns>The created render data.</returns>
        private static OptionsMenuRenderData CreateRenderData(params OptionsSaveSlot[] saveSlots)
        {
            return CreateRenderDataForTab(OptionsMenuTab.SaveLoad, saveSlots);
        }

        /// <summary>
        /// Creates minimal render state for a selected Options menu page.
        /// </summary>
        /// <param name="activeTab">The active tab.</param>
        /// <param name="saveSlots">The save slots.</param>
        /// <param name="bindings">The bindings.</param>
        /// <param name="mods">The mods.</param>
        /// <param name="contentPackLabel">The content pack label.</param>
        /// <returns>The created render data for tab.</returns>
        private static OptionsMenuRenderData CreateRenderDataForTab(
            OptionsMenuTab activeTab,
            OptionsSaveSlot[] saveSlots = null,
            OptionsBindingRow[] bindings = null,
            OptionsModRow[] mods = null,
            string contentPackLabel = ""
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
                false,
                mods: mods,
                contentPackLabel: contentPackLabel
            );
        }
    }
}
