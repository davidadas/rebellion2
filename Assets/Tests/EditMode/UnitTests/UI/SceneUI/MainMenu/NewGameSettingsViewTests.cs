using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Units;
using Rebellion.Util.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.MainMenu
{
    [TestFixture]
    public sealed class NewGameSettingsViewTests
    {
        private static readonly Color DefaultTextColor = new Color(0.875f, 0.910f, 0.941f);
        private static readonly Color ModifiedTextColor = new Color(0.373f, 0.659f, 0.925f);
        private GameObject instance;
        private NewGameSettingsView view;
        private GameSummary summary;
        private OptionsMenuView optionsPrefab;
        private string presetDirectory;

        /// <summary>
        /// Instantiates the generated settings editor independently of application startup.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/MainMenu/NewGameSettings.prefab"
                )
            );
            instance.SetActive(true);
            view = instance.GetComponent<NewGameSettingsView>();
            view.InitializeContent(TestContent.Assets);
            summary = new GameSummary { Difficulty = GameDifficulty.Easy };
            optionsPrefab = AssetDatabase.LoadAssetAtPath<OptionsMenuView>(
                "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab"
            );
        }

        /// <summary>
        /// Releases the generated controls.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(instance);
            if (Directory.Exists(presetDirectory))
                Directory.Delete(presetDirectory, true);
            presetDirectory = null;
        }

        [Test]
        public void Initialize_SelectedDifficulty_ShowsPlayerAndAIValues()
        {
            view.Initialize(TestContent.Pack, summary);

            Assert.AreEqual("100", Input("mine-output", "Player").text);
            Assert.AreEqual(
                TestContent
                    .Pack.GameData.GameConfig.DifficultyModifiers[GameDifficulty.Easy]
                    .MineOutputPercent.ToString(CultureInfo.InvariantCulture),
                Input("mine-output", "AI").text
            );
            Assert.IsTrue(ApplyButton().interactable);
        }

        [Test]
        public void Initialize_ExistingCustomSettings_ShowsConfiguredValues()
        {
            summary.CustomSettings = new CustomGameSettings
            {
                PlayerDifficultyModifiers = new DifficultyModifiers
                {
                    ManufacturingSpeedPercent = 135,
                },
                AIDifficultyModifiers = new DifficultyModifiers { ManufacturingSpeedPercent = 165 },
                GenerationConfig = TestContent.Data.GenerationConfig.GetDeepCopy(),
                GarrisonRules = new GarrisonRuleSettings
                {
                    SupportThreshold = 70,
                    SupportDeficitPerRegiment = 5,
                    UprisingMultiplier = 3,
                },
                MovementRules = new MovementRuleSettings { TravelSpeedPercent = 150 },
            };

            view.Initialize(TestContent.Pack, summary);

            Assert.AreEqual("135", Input("manufacturing-speed", "Player").text);
            Assert.AreEqual("165", Input("manufacturing-speed", "AI").text);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            Assert.AreEqual("70", Input("garrison-support-threshold", "World").text);
            Assert.AreEqual("5", Input("garrison-support-deficit-per-regiment", "World").text);
            Assert.AreEqual("3", Input("garrison-uprising-multiplier", "World").text);
            Assert.AreEqual("150", Input("travel-speed", "World").text);
        }

        [Test]
        public void Initialize_NullContent_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => view.Initialize(null, summary));
        }

        [Test]
        public void Initialize_NullSummary_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => view.Initialize(TestContent.Pack, null));
        }

        [Test]
        public void Initialize_DifficultyModifiers_ShowsEightRequestedRows()
        {
            string[] expected =
            {
                "Mine Output Modifier",
                "Refinery Output Modifier",
                "Maintenance Modifier",
                "Manufacturing Speed Modifier",
                "Mission Success Bonus",
                "Mission Foil Bonus",
                "Mission Speed Bonus",
                "Detection Rating Multiplier",
            };
            view.Initialize(TestContent.Pack, summary);

            string[] actual = instance
                .GetComponentsInChildren<TMP_InputField>()
                .Select(input => input.transform.parent)
                .Distinct()
                .Select(row => row.Find("Label").GetComponent<TMP_Text>().text)
                .ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Initialize_DifficultyModifiers_GroupsRowsUnderTwoSectionHeadings()
        {
            view.Initialize(TestContent.Pack, summary);

            string[] actual = instance
                .GetComponentsInChildren<TMP_Text>()
                .Where(text => text.name.StartsWith("Section-", StringComparison.Ordinal))
                .OrderByDescending(text => text.rectTransform.anchoredPosition.y)
                .Select(text => text.text)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "ECONOMY", "MISSIONS AND INTELLIGENCE" }, actual);
        }

        [Test]
        public void EditValue_PlayerColumn_LeavesAIColumnUnchanged()
        {
            view.Initialize(TestContent.Pack, summary);
            string aiValue = Input("mine-output", "AI").text;

            Input("mine-output", "Player").onEndEdit.Invoke("125");

            Assert.AreEqual("125", Input("mine-output", "Player").text);
            Assert.AreEqual(aiValue, Input("mine-output", "AI").text);
            Assert.IsTrue(ApplyButton().interactable);
        }

        [Test]
        public void EditValue_ValidNumber_HighlightsModifiedValueAndPreservesInputGeometry()
        {
            view.Initialize(TestContent.Pack, summary);
            TMP_InputField input = Input("mine-output", "Player");
            RectInt viewportRect = UILayout.GetSourceRect(input.textViewport);
            RectInt textRect = UILayout.GetSourceRect(input.textComponent.rectTransform);

            input.onEndEdit.Invoke("125");

            input = Input("mine-output", "Player");
            Assert.AreEqual(ModifiedTextColor, input.textComponent.color);
            Assert.AreEqual(viewportRect, UILayout.GetSourceRect(input.textViewport));
            Assert.AreEqual(textRect, UILayout.GetSourceRect(input.textComponent.rectTransform));
        }

        [Test]
        public void Initialize_DifficultyInput_RetainsSharedTextInputBehaviorAndGeometry()
        {
            TMP_InputField sharedInput = AssetDatabase
                .LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Common/TextInput.prefab")
                .GetComponent<TMP_InputField>();

            view.Initialize(TestContent.Pack, summary);

            TMP_InputField input = Input("mine-output", "Player");
            Assert.AreEqual(sharedInput.caretWidth, input.caretWidth);
            Assert.AreEqual(sharedInput.customCaretColor, input.customCaretColor);
            Assert.AreEqual(sharedInput.selectionColor, input.selectionColor);
            Assert.AreEqual(sharedInput.onFocusSelectAll, input.onFocusSelectAll);
            Assert.AreEqual(Vector2.zero, input.textViewport.anchorMin);
            Assert.AreEqual(Vector2.one, input.textViewport.anchorMax);
            Assert.AreEqual(Vector2.zero, input.textComponent.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, input.textComponent.rectTransform.anchorMax);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("bad")]
        [TestCase("-1")]
        [TestCase("1.5")]
        public void EditValue_InvalidMineModifier_RetainsPreviousValue(string invalid)
        {
            view.Initialize(TestContent.Pack, summary);
            string original = Input("mine-output", "Player").text;

            Input("mine-output", "Player").onEndEdit.Invoke(invalid);

            Assert.AreEqual(original, Input("mine-output", "Player").text);
            Assert.IsTrue(ApplyButton().interactable);
        }

        [TestCase("mine-output")]
        [TestCase("refinery-output")]
        public void EditValue_ResourceOutputAboveSafeLimit_RetainsPreviousValue(string key)
        {
            view.Initialize(TestContent.Pack, summary);
            string original = Input(key, "Player").text;

            Input(key, "Player").onEndEdit.Invoke("1001");

            Assert.AreEqual(original, Input(key, "Player").text);
        }

        [Test]
        public void EditValue_MissionPointBonus_AcceptsNegativeInteger()
        {
            view.Initialize(TestContent.Pack, summary);

            Input("mission-success", "Player").onEndEdit.Invoke("-15");

            Assert.AreEqual("-15", Input("mission-success", "Player").text);
        }

        [TestCase("mission-success", "-101")]
        [TestCase("mission-success", "101")]
        [TestCase("mission-foil", "-101")]
        [TestCase("mission-foil", "101")]
        public void EditValue_ChanceModifierOutsideEffectiveRange_RetainsPreviousValue(
            string key,
            string invalid
        )
        {
            view.Initialize(TestContent.Pack, summary);
            TMP_InputField input = Input(key, "Player");
            string original = input.text;

            input.onEndEdit.Invoke(invalid);

            Assert.AreEqual(original, input.text);
        }

        [Test]
        public void EditValue_DetectionMultiplier_AcceptsPositiveDecimalOnly()
        {
            view.Initialize(TestContent.Pack, summary);

            Input("detection-rating", "Player").onEndEdit.Invoke("1.25");
            Assert.AreEqual("1.25", Input("detection-rating", "Player").text);

            Input("detection-rating", "Player").onEndEdit.Invoke("0");
            Assert.AreEqual("1.25", Input("detection-rating", "Player").text);
        }

        [Test]
        public void ResetValue_PlayerColumn_RestoresOnlyPlayerDefault()
        {
            view.Initialize(TestContent.Pack, summary);
            Input("mine-output", "Player").onEndEdit.Invoke("125");
            Input("mine-output", "AI").onEndEdit.Invoke("175");

            ResetButton("mine-output", "Player").onClick.Invoke();

            Assert.AreEqual("100", Input("mine-output", "Player").text);
            Assert.AreEqual("175", Input("mine-output", "AI").text);
            Assert.AreEqual(DefaultTextColor, Input("mine-output", "Player").textComponent.color);
            Assert.AreEqual(ModifiedTextColor, Input("mine-output", "AI").textComponent.color);
        }

        [Test]
        public void RestoreDefaults_EditedColumns_RestoresAuthoredValues()
        {
            view.Initialize(TestContent.Pack, summary);
            Input("mine-output", "Player").onEndEdit.Invoke("125");
            Input("mine-output", "AI").onEndEdit.Invoke("175");

            view.RestoreDefaults();

            Assert.AreEqual("100", Input("mine-output", "Player").text);
            Assert.AreEqual(
                TestContent
                    .Pack.GameData.GameConfig.DifficultyModifiers[GameDifficulty.Easy]
                    .MineOutputPercent.ToString(CultureInfo.InvariantCulture),
                Input("mine-output", "AI").text
            );
        }

        [Test]
        public void RestoreDefaults_WorldGeneration_PreservesDifficultyModifierEdits()
        {
            view.Initialize(TestContent.Pack, summary);
            Input("mine-output", "Player").onEndEdit.Invoke("125");
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            view.RestoreDefaults();
            view.SelectCategory(NewGameSettingsCategory.DifficultyModifiers);

            Assert.AreEqual("125", Input("mine-output", "Player").text);
        }

        [Test]
        public void RestoreDefaults_DifficultyModifiers_PreservesWorldGenerationEdits()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            TableInput("mine-chance-per-resource", "Player").onEndEdit.Invoke("7");
            view.SelectCategory(NewGameSettingsCategory.DifficultyModifiers);

            view.RestoreDefaults();
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Assert.AreEqual("7", TableInput("mine-chance-per-resource", "Player").text);
        }

        [Test]
        public void Apply_EditedColumns_PublishesPlayerAndAIModifiers()
        {
            GameSummary applied = null;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            Input("manufacturing-speed", "Player").onEndEdit.Invoke("125");
            Input("mission-foil", "AI").onEndEdit.Invoke("20");

            ApplyButton().onClick.Invoke();

            Assert.AreEqual(
                125,
                applied.CustomSettings.PlayerDifficultyModifiers.ManufacturingSpeedPercent
            );
            Assert.AreEqual(
                20,
                applied.CustomSettings.AIDifficultyModifiers.MissionFoilChancePoints
            );
            Assert.IsTrue(ApplyButton().interactable);
        }

        [Test]
        public void Initialize_WorldGenerationNavigation_ShowsSingleNumericSection()
        {
            view.Initialize(TestContent.Pack, summary);

            string[] labels = instance
                .transform.Find("ContentRoot")
                .GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Tab", StringComparison.Ordinal))
                .Select(button => button.GetComponentInChildren<TMP_Text>().text)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "GAME SETUP",
                    "DIFFICULTY",
                    "WORLD SETTINGS",
                    "FACTION MODIFIERS",
                    "SAVE / LOAD",
                },
                labels
            );
        }

        [Test]
        public void SelectCategory_GameSetup_ShowsEveryLaunchSelection()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);

            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Choice-content-pack",
                    "Choice-faction",
                    "Choice-difficulty",
                    "Choice-galaxy-size",
                    "Choice-victory-condition",
                },
                instance
                    .GetComponentsInChildren<OptionsChoiceFieldView>()
                    .Select(choice => choice.name)
                    .ToArray()
            );
            Assert.AreEqual(summary.Seed.ToString(CultureInfo.InvariantCulture), SeedInput().text);
            foreach (
                OptionsChoiceFieldView choice in instance.GetComponentsInChildren<OptionsChoiceFieldView>()
            )
            {
                RectTransform rowLabel = (RectTransform)choice.transform.parent.Find("Label");
                Assert.AreEqual(
                    rowLabel.anchoredPosition.y,
                    ((RectTransform)choice.transform).anchoredPosition.y
                );
            }

            Transform seedRow = SeedInput().transform.parent;
            float seedLabelY = ((RectTransform)seedRow.Find("Label")).anchoredPosition.y;
            Assert.AreEqual(seedLabelY, ((RectTransform)SeedInput().transform).anchoredPosition.y);
            Assert.AreEqual(
                seedLabelY,
                ((RectTransform)SeedResetButton().transform).anchoredPosition.y
            );
            CollectionAssert.AreEqual(
                new[] { "CAMPAIGN", "EVENTS" },
                instance
                    .GetComponentsInChildren<TMP_Text>()
                    .Where(text => text.name.StartsWith("Section-", StringComparison.Ordinal))
                    .Select(text => text.text)
                    .ToArray()
            );
        }

        [Test]
        public void EditSeed_ValidInteger_PublishesEnteredSeed()
        {
            GameSummary launched = null;
            view.StartRequested += (_, configured) => launched = configured;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            SeedInput().onEndEdit.Invoke("-1729");
            ApplyButton().onClick.Invoke();

            Assert.AreEqual(-1729, launched.Seed);
        }

        [Test]
        public void RandomizeSeed_ClickedResetButton_SelectsNewSeed()
        {
            summary.Seed = 0;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            SeedResetButton().onClick.Invoke();

            Assert.AreNotEqual("0", SeedInput().text);
            Assert.IsTrue(
                int.TryParse(
                    SeedInput().text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _
                )
            );
            Assert.AreEqual(DefaultTextColor, SeedInput().textComponent.color);
        }

        [Test]
        public void ChangeGameSummary_WhenValuesDifferFromInitialState_RemainsNeutral()
        {
            summary = GameLaunchContext.CreateDefaultSummary(TestContent.Pack);
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            StepSetupChoice("difficulty", 1);
            SeedResetButton().onClick.Invoke();
            EventToggle(GameEventCategory.Story).GetComponentInChildren<Button>().onClick.Invoke();

            Assert.AreEqual(DefaultTextColor, SetupChoiceValue("difficulty").color);
            Assert.AreEqual(DefaultTextColor, SeedInput().textComponent.color);
            Assert.AreNotEqual(
                ModifiedTextColor,
                ToggleStateText(EventToggle(GameEventCategory.Story)).color
            );
        }

        [Test]
        public void SelectCategory_GameSetup_UsesSharedTopContentInset()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);

            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            RectTransform scrollRoot = (RectTransform)
                instance.transform.Find("ContentRoot/DifficultyPage/SettingsScrollArea");
            Assert.AreEqual(16f, -scrollRoot.anchoredPosition.y);
            Assert.AreEqual(310f, scrollRoot.sizeDelta.y);
        }

        /// <summary>
        /// Keeps the themed settings scrollbar at its authored width when switching layouts.
        /// </summary>
        [Test]
        public void SelectCategory_GameSetup_PreservesSharedScrollbarStyle()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);
            ScrollAreaView area = instance.GetComponentInChildren<ScrollAreaView>();
            Scrollbar scrollbar = area.GetComponentInChildren<Scrollbar>(true);
            RectInt bounds = UILayout.GetSourceRect(scrollbar.transform as RectTransform);

            Assert.AreEqual(12, bounds.width);
            Assert.AreEqual(342, bounds.xMax);
            Assert.IsEmpty(scrollbar.GetComponentsInChildren<RawImage>(true));
            Assert.IsTrue(
                scrollbar
                    .handleRect.GetComponentsInChildren<Image>(true)
                    .Any(image => image.type == Image.Type.Sliced)
            );
            Assert.LessOrEqual(area.ViewportWidth, bounds.x);
        }

        /// <summary>
        /// Fits every cloned selector and both arrow glyphs within the available viewport.
        /// </summary>
        [Test]
        public void SelectCategory_GameSetup_KeepsClonedSelectorsWithinViewport()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);
            ScrollAreaView area = instance.GetComponentInChildren<ScrollAreaView>();

            foreach (
                OptionsChoiceFieldView choice in instance.GetComponentsInChildren<OptionsChoiceFieldView>()
            )
            {
                RectTransform root = choice.transform as RectTransform;
                Assert.LessOrEqual(
                    UILayout.GetSourceRect(root).xMax,
                    area.ViewportWidth,
                    choice.name
                );
                foreach (string name in new[] { "Badge", "Previous/Glyph", "Value", "Next/Glyph" })
                {
                    RectTransform child = root.Find(name) as RectTransform;
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        root,
                        child
                    );
                    Assert.GreaterOrEqual(
                        bounds.min.x,
                        root.rect.xMin - 0.001f,
                        choice.name + "/" + name
                    );
                    Assert.LessOrEqual(
                        bounds.max.x,
                        root.rect.xMax + 0.001f,
                        choice.name + "/" + name
                    );
                }
            }
        }

        [Test]
        public void SelectCategory_GameSetup_BindsClonedSelectorBackgrounds()
        {
            OptionsMenuView source = UnityEngine.Object.Instantiate(optionsPrefab);
            try
            {
                Image sourceBadge = source
                    .GetComponentsInChildren<Image>(true)
                    .Single(image => image.name == "ContentPackBadge");
                sourceBadge.sprite = null;

                view.Initialize(TestContent.Pack, summary, source, null);
                view.SelectCategory(NewGameSettingsCategory.GameSetup);

                foreach (
                    OptionsChoiceFieldView choice in instance.GetComponentsInChildren<OptionsChoiceFieldView>()
                )
                {
                    Image badge = choice.transform.Find("Badge").GetComponent<Image>();
                    Assert.IsNotNull(badge.sprite, choice.name);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
        }

        [Test]
        public void SelectCategory_GameSetup_ShowsOneTogglePerClassifiedEventCategory()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);

            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            OptionsToggleRowView[] toggles = instance
                .GetComponentsInChildren<OptionsToggleRowView>()
                .OrderBy(toggle => toggle.OptionIndex)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { GameEventCategory.Story, GameEventCategory.Disaster },
                toggles.Select(toggle => (GameEventCategory)toggle.OptionIndex).ToArray()
            );
            CollectionAssert.AreEqual(
                new[] { "Story Events", "Disaster Events" },
                instance
                    .GetComponentsInChildren<Transform>()
                    .Where(transform => transform.name.StartsWith("Setup-event-"))
                    .OrderBy(transform =>
                        transform.name.EndsWith(nameof(GameEventCategory.Story)) ? 0 : 1
                    )
                    .Select(transform => transform.Find("Label").GetComponent<TMP_Text>().text)
                    .ToArray()
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Disables all story events in the game.",
                    "Disables all disaster events in the game.",
                },
                instance
                    .GetComponentsInChildren<Transform>()
                    .Where(transform => transform.name.StartsWith("Setup-event-"))
                    .OrderBy(transform =>
                        transform.name.EndsWith(nameof(GameEventCategory.Story)) ? 0 : 1
                    )
                    .Select(transform =>
                        transform.Find("Description").GetComponent<TMP_Text>().text
                    )
                    .ToArray()
            );
            Assert.IsTrue(toggles.All(toggle => ToggleState(toggle) == "ON"));
        }

        [TestCase(GameEventCategory.Story)]
        [TestCase(GameEventCategory.Disaster)]
        public void Apply_DisabledEventCategory_PublishesOnlySelectedCategory(
            GameEventCategory category
        )
        {
            GameSummary launched = null;
            view.StartRequested += (_, configured) => launched = configured;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            EventToggle(category).GetComponentInChildren<Button>().onClick.Invoke();
            ApplyButton().onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { category },
                launched.CustomSettings.GenerationConfig.DisabledEventCategories
            );
            Assert.IsNull(summary.CustomSettings);
            Assert.AreEqual("OFF", ToggleState(EventToggle(category)));
            Assert.AreNotEqual(ModifiedTextColor, ToggleStateText(EventToggle(category)).color);
        }

        [Test]
        public void RestoreDefaults_GameSummary_ReenablesEveryEventCategory()
        {
            GameSummary launched = null;
            summary.CustomSettings = new CustomGameSettings
            {
                GenerationConfig = TestContent.Data.GenerationConfig.GetDeepCopy(),
            };
            summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                new List<GameEventCategory> { GameEventCategory.Story, GameEventCategory.Disaster };
            view.StartRequested += (_, configured) => launched = configured;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            view.RestoreDefaults();
            ApplyButton().onClick.Invoke();

            Assert.IsEmpty(launched.CustomSettings.GenerationConfig.DisabledEventCategories);
            Assert.AreEqual("ON", ToggleState(EventToggle(GameEventCategory.Story)));
            Assert.AreEqual("ON", ToggleState(EventToggle(GameEventCategory.Disaster)));
            Assert.AreEqual(
                Color.white,
                ToggleStateText(EventToggle(GameEventCategory.Story)).color
            );
            Assert.AreEqual(
                Color.white,
                ToggleStateText(EventToggle(GameEventCategory.Disaster)).color
            );
        }

        [Test]
        public void SelectCategory_GameSetup_ExplainsBaseDifficultyDefaults()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);

            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            Transform row = instance
                .GetComponentsInChildren<Transform>()
                .Single(candidate => candidate.name == "Setup-difficulty");
            Assert.AreEqual("Base Difficulty", row.Find("Label").GetComponent<TMP_Text>().text);
            Assert.AreEqual(
                "Sets the starting values. You can change them afterward.",
                row.Find("Description").GetComponent<TMP_Text>().text
            );
        }

        [Test]
        public void SelectCategory_ClonedChoiceHover_ShowsClonedValueTooltip()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);
            OptionsChoiceFieldView choice = instance
                .GetComponentsInChildren<OptionsChoiceFieldView>()
                .Single(candidate => candidate.name == "Choice-content-pack");
            choice.Render("A CONTENT PACK NAME THAT CANNOT FIT IN THE SELECTOR");
            OverflowTextTooltip tooltip = choice
                .transform.Find("Value")
                .GetComponent<OverflowTextTooltip>();
            OverflowTextTooltipTrigger trigger = choice
                .transform.Find("Next")
                .GetComponent<OverflowTextTooltipTrigger>();

            trigger.OnPointerEnter(new PointerEventData(null));
            tooltip.AdvanceHover(float.PositiveInfinity);

            Transform tooltipRoot = choice.transform.Find("ValueTooltip");
            Assert.IsNotNull(tooltipRoot);
            Assert.IsTrue(tooltipRoot.gameObject.activeSelf);
        }

        [Test]
        public void SelectCategory_SaveLoad_AlignsClonedPageWithContentPanel()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);

            view.SelectCategory(NewGameSettingsCategory.SaveLoad);

            RectTransform contentRoot = (RectTransform)instance.transform.Find("ContentRoot");
            RectTransform settingsPage = (RectTransform)contentRoot.Find("DifficultyPage");
            RectTransform presetPage = (RectTransform)contentRoot.Find("CustomGamePresetPage");
            Assert.IsNotNull(presetPage);
            Assert.AreEqual(settingsPage.anchoredPosition, presetPage.anchoredPosition);
            Assert.AreEqual(settingsPage.sizeDelta, presetPage.sizeDelta);
            Assert.AreEqual(settingsPage.anchorMin, presetPage.anchorMin);
            Assert.AreEqual(settingsPage.anchorMax, presetPage.anchorMax);
            Assert.AreEqual(settingsPage.pivot, presetPage.pivot);
            Assert.AreEqual(Vector3.one, presetPage.localScale);
            Assert.IsTrue(ApplyButton().gameObject.activeSelf);
        }

        [TestCase("SaveIcon")]
        [TestCase("SaveIconDisabled")]
        [TestCase("LoadIcon")]
        [TestCase("LoadIconDisabled")]
        public void SelectCategory_SaveLoadWithStrippedTextures_RestoresCommandArtwork(string name)
        {
            OptionsMenuView source = UnityEngine.Object.Instantiate(optionsPrefab);
            try
            {
                ClearSaveListTextures(source.gameObject);

                view.Initialize(TestContent.Pack, summary, source, null);
                view.SelectCategory(NewGameSettingsCategory.SaveLoad);

                RawImage image = instance
                    .GetComponentInChildren<OptionsSaveListView>(true)
                    .GetComponentsInChildren<RawImage>(true)
                    .Single(candidate => candidate.name == name);
                ContentTextureBinding binding = image.GetComponent<ContentTextureBinding>();
                string address =
                    binding != null
                        ? binding.Address
                        : image.GetComponent<ContentPressVisualBinding>().UpAddress;
                Assert.AreSame(
                    ContentBindings.RequireTexture(TestContent.Assets, address),
                    image.texture
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
        }

        [TestCase("SaveIcon")]
        [TestCase("LoadIcon")]
        public void SelectCategory_SaveLoadWithStrippedTextures_RestoresPressedArtwork(string name)
        {
            OptionsMenuView source = UnityEngine.Object.Instantiate(optionsPrefab);
            try
            {
                ClearSaveListTextures(source.gameObject);

                view.Initialize(TestContent.Pack, summary, source, null);
                view.SelectCategory(NewGameSettingsCategory.SaveLoad);
                OptionsSaveListView saveList =
                    instance.GetComponentInChildren<OptionsSaveListView>();
                saveList.Render(
                    new[]
                    {
                        new OptionsSaveSlot("Preset", string.Empty, null, false, "preset.json"),
                    },
                    0,
                    true,
                    false
                );
                Button button = PresetButton(name);
                ContentPressVisualBinding binding =
                    button.GetComponent<ContentPressVisualBinding>();

                button
                    .GetComponent<RawImagePressVisual>()
                    .OnPointerDown(
                        new PointerEventData(null) { button = PointerEventData.InputButton.Left }
                    );

                Assert.AreSame(
                    ContentBindings.RequireTexture(TestContent.Assets, binding.DownAddress),
                    button.GetComponent<RawImage>().texture
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
        }

        [TestCase("SaveIcon")]
        [TestCase("SaveIconDisabled")]
        [TestCase("LoadIcon")]
        [TestCase("LoadIconDisabled")]
        public void InitializeContent_ExistingPresetPage_RestoresCommandArtwork(string name)
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.SaveLoad);
            ClearSaveListTextures(instance);

            view.InitializeContent(TestContent.Assets);

            RawImage image = instance
                .GetComponentInChildren<OptionsSaveListView>(true)
                .GetComponentsInChildren<RawImage>(true)
                .Single(candidate => candidate.name == name);
            ContentTextureBinding binding = image.GetComponent<ContentTextureBinding>();
            string address =
                binding != null
                    ? binding.Address
                    : image.GetComponent<ContentPressVisualBinding>().UpAddress;
            Assert.AreSame(
                ContentBindings.RequireTexture(TestContent.Assets, address),
                image.texture
            );
        }

        [Test]
        public void Apply_CycledGameSetup_PublishesCompleteLaunchSummary()
        {
            GameSummary launched = null;
            view.StartRequested += (_, configured) => launched = configured;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);
            StepSetupChoice("faction", 1);
            StepSetupChoice("difficulty", 1);
            StepSetupChoice("galaxy-size", 1);
            StepSetupChoice("victory-condition", 1);

            ApplyButton().onClick.Invoke();

            Assert.IsNotNull(launched);
            Assert.AreEqual("FNEMP1", launched.PlayerFactionID);
            Assert.AreEqual(GameDifficulty.Medium, launched.Difficulty);
            Assert.AreEqual(GameSize.Small, launched.GalaxySize);
            Assert.AreEqual(GameVictoryCondition.Headquarters, launched.VictoryCondition);
            Assert.AreEqual(GameResourceAvailability.Normal, launched.ResourceAvailability);
            Assert.IsNotNull(launched.CustomSettings);
            Assert.IsNotNull(launched.CustomSettings.PlayerDifficultyModifiers);
            Assert.IsNull(launched.CustomSettings.AIDifficultyModifiers);
            Assert.IsNotNull(launched.CustomSettings.GenerationConfig);
        }

        [Test]
        public void CycleDifficulty_ProfileLinkedAIValues_UsesSelectedProfile()
        {
            DifficultyModifiers expected = TestContent.Pack.GameData.GameConfig.DifficultyModifiers[
                GameDifficulty.Medium
            ];
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            StepSetupChoice("difficulty", 1);
            view.SelectCategory(NewGameSettingsCategory.DifficultyModifiers);

            Assert.AreEqual(
                expected.MineOutputPercent.ToString(CultureInfo.InvariantCulture),
                Input("mine-output", "AI").text
            );
            Assert.AreEqual(DefaultTextColor, Input("mine-output", "AI").textComponent.color);
            Assert.AreEqual(
                expected.ManufacturingSpeedPercent.ToString(CultureInfo.InvariantCulture),
                Input("manufacturing-speed", "AI").text
            );
            Assert.AreEqual(
                DefaultTextColor,
                Input("manufacturing-speed", "AI").textComponent.color
            );
        }

        [Test]
        public void CycleDifficulty_ManuallyOverriddenAIValue_PreservesOnlyOverride()
        {
            DifficultyModifiers expected = TestContent.Pack.GameData.GameConfig.DifficultyModifiers[
                GameDifficulty.Medium
            ];
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
            Input("mine-output", "AI").onEndEdit.Invoke("777");
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            StepSetupChoice("difficulty", 1);
            view.SelectCategory(NewGameSettingsCategory.DifficultyModifiers);

            Assert.AreEqual("777", Input("mine-output", "AI").text);
            Assert.AreEqual(ModifiedTextColor, Input("mine-output", "AI").textComponent.color);
            Assert.AreEqual(
                expected.ManufacturingSpeedPercent.ToString(CultureInfo.InvariantCulture),
                Input("manufacturing-speed", "AI").text
            );
            Assert.AreEqual(
                DefaultTextColor,
                Input("manufacturing-speed", "AI").textComponent.color
            );
        }

        [Test]
        public void CycleDifficulty_LoadedAIModifiers_PreservesOnlyValuesOutsideProfile()
        {
            DifficultyModifiers easy = TestContent.Pack.GameData.GameConfig.DifficultyModifiers[
                GameDifficulty.Easy
            ];
            DifficultyModifiers medium = TestContent.Pack.GameData.GameConfig.DifficultyModifiers[
                GameDifficulty.Medium
            ];
            summary.CustomSettings = new CustomGameSettings
            {
                AIDifficultyModifiers = easy.CreateCopy(),
                GenerationConfig = TestContent.Data.GenerationConfig.GetDeepCopy(),
            };
            summary.CustomSettings.AIDifficultyModifiers.MineOutputPercent = 777;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            StepSetupChoice("difficulty", 1);
            view.SelectCategory(NewGameSettingsCategory.DifficultyModifiers);

            Assert.AreEqual("777", Input("mine-output", "AI").text);
            Assert.AreEqual(
                medium.ManufacturingSpeedPercent.ToString(CultureInfo.InvariantCulture),
                Input("manufacturing-speed", "AI").text
            );
        }

        [Test]
        public void SelectCategory_ContentPackChoice_RequestsNextInstalledPack()
        {
            string requested = null;
            ContentPackDefinition[] installed =
            {
                TestContent.Pack.Definition,
                new ContentPackDefinition { ID = "alternate", DisplayName = "Alternate" },
            };
            view.ContentPackRequested += packID => requested = packID;
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null, installed);
            view.SelectCategory(NewGameSettingsCategory.GameSetup);

            StepSetupChoice("content-pack", 1);

            Assert.AreEqual("alternate", requested);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HandlePresetSaveRequested_ExistingPreset_WaitsForConfirmation(bool submitName)
        {
            (CustomGamePresetManager manager, string fileName) = RequestPresetOverwrite(submitName);
            ConfirmationDialogView confirmation = PresetConfirmation();

            Assert.IsTrue(confirmation.gameObject.activeSelf);
            StringAssert.Contains(
                "Overwrite \"Existing Preset\"?",
                confirmation.GetComponentInChildren<TMP_Text>().text
            );
            Assert.AreEqual(111, manager.LoadPreset(fileName).Seed);
            Assert.AreEqual("Existing Preset", manager.GetPresets().Single().DisplayName);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfirmPresetAction_Overwrite_WritesRequestedPreset(bool submitName)
        {
            (CustomGamePresetManager manager, string fileName) = RequestPresetOverwrite(submitName);

            PresetConfirmationButton(true).onClick.Invoke();

            Assert.AreEqual(222, manager.LoadPreset(fileName).Seed);
            Assert.AreEqual(
                submitName ? "Updated Preset" : "Existing Preset",
                manager.GetPresets().Single().DisplayName
            );
            Assert.IsFalse(PresetConfirmation().gameObject.activeSelf);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelPresetAction_Overwrite_PreservesStoredPreset(bool submitName)
        {
            RequestPresetOverwrite(submitName);
            string path = Directory.GetFiles(presetDirectory, "*.customgame.xml").Single();
            byte[] before = File.ReadAllBytes(path);

            PresetConfirmationButton(false).onClick.Invoke();
            PresetConfirmationButton(true).onClick.Invoke();

            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.IsFalse(PresetConfirmation().gameObject.activeSelf);
        }

        [Test]
        public void Initialize_PendingPresetOverwrite_DiscardsConfirmation()
        {
            (CustomGamePresetManager manager, string fileName) = RequestPresetOverwrite(false);

            view.Initialize(TestContent.Pack, summary, optionsPrefab, manager);
            PresetConfirmationButton(true).onClick.Invoke();

            Assert.AreEqual(111, manager.LoadPreset(fileName).Seed);
            Assert.IsFalse(PresetConfirmation().gameObject.activeSelf);
        }

        [Test]
        public void RequestClose_PendingPresetOverwrite_DiscardsConfirmation()
        {
            (CustomGamePresetManager manager, string fileName) = RequestPresetOverwrite(false);

            instance
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Close")
                .onClick.Invoke();
            PresetConfirmationButton(true).onClick.Invoke();

            Assert.AreEqual(111, manager.LoadPreset(fileName).Seed);
            Assert.IsFalse(PresetConfirmation().gameObject.activeSelf);
        }

        [Test]
        public void ConfirmPresetAction_ReorderedPreset_KeepsSavedPresetSelected()
        {
            (CustomGamePresetManager manager, _) = InitializePresetPage();
            GameSummary other = GameLaunchContext.CreateDefaultSummary(TestContent.Pack);
            other.Seed = 333;
            manager.CreatePreset("Other Preset", other);
            view.Initialize(TestContent.Pack, summary, optionsPrefab, manager);
            SeedInput().onEndEdit.Invoke("222");
            view.SelectCategory(NewGameSettingsCategory.SaveLoad);
            PresetButton("SlotRow2").onClick.Invoke();
            PresetButton("SaveIcon").onClick.Invoke();

            PresetConfirmationButton(true).onClick.Invoke();
            PresetButton("LoadIcon").onClick.Invoke();
            GameSummary launched = null;
            view.StartRequested += (_, configured) => launched = configured;
            ApplyButton().onClick.Invoke();

            Assert.AreEqual(222, launched.Seed);
        }

        [TestCase(false, 1)]
        [TestCase(true, 0)]
        public void HandlePresetDeleteRequested_Confirmation_DeletesOnlyWhenAccepted(
            bool accepted,
            int expectedCount
        )
        {
            (CustomGamePresetManager manager, _) = InitializePresetPage();
            PresetButton("SlotDelete1").onClick.Invoke();

            PresetConfirmationButton(accepted).onClick.Invoke();

            Assert.AreEqual(expectedCount, manager.GetPresets().Count);
        }

        [Test]
        public void HandlePresetSlotRenamed_NewPreset_CreatesWithoutOverwriteConfirmation()
        {
            (CustomGamePresetManager manager, _) = InitializePresetPage();
            PresetButton("SlotRow0").onClick.Invoke();
            instance
                .GetComponentInChildren<OptionsSaveListView>(true)
                .GetComponentInChildren<TMP_InputField>(true)
                .text = "New Preset";

            PresetButton("SaveIcon").onClick.Invoke();

            CollectionAssert.AreEquivalent(
                new[] { "Existing Preset", "New Preset" },
                manager.GetPresets().Select(entry => entry.DisplayName)
            );
            Assert.IsFalse(PresetConfirmation().gameObject.activeSelf);
        }

        [Test]
        public void SelectCategory_RepeatedSaveLoadRender_EmitsOneSaveRequest()
        {
            InitializePresetPage();
            OptionsSaveListView saveList = instance.GetComponentInChildren<OptionsSaveListView>(
                true
            );
            int requests = 0;
            saveList.SaveRequested += () => requests++;
            view.SelectCategory(NewGameSettingsCategory.SaveLoad);
            view.SelectCategory(NewGameSettingsCategory.SaveLoad);
            PresetButton("SlotRow1").onClick.Invoke();

            PresetButton("SaveIcon").onClick.Invoke();

            Assert.AreEqual(1, requests);
        }

        [Test]
        public void LoadPreset_InstalledContentPacks_RetainsContentPackChoices()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-custom-game-view-tests-" + Guid.NewGuid().ToString("N")
            );
            try
            {
                CustomGamePresetManager manager = new CustomGamePresetManager(directory);
                GameSummary preset = GameLaunchContext.CreateDefaultSummary(TestContent.Pack);
                manager.CreatePreset("Preset", preset);
                ContentPackDefinition[] installed =
                {
                    TestContent.Pack.Definition,
                    new ContentPackDefinition { ID = "alternate", DisplayName = "Alternate" },
                };
                string requested = null;
                view.ContentPackRequested += packID => requested = packID;
                view.Initialize(TestContent.Pack, preset, optionsPrefab, manager, installed);
                view.SelectCategory(NewGameSettingsCategory.SaveLoad);
                instance
                    .GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "SlotRow1")
                    .onClick.Invoke();
                instance
                    .GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "LoadIcon")
                    .onClick.Invoke();
                view.SelectCategory(NewGameSettingsCategory.GameSetup);

                StepSetupChoice("content-pack", 1);

                Assert.AreEqual("alternate", requested);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Initialize_FactionModifiers_RendersConfiguredFactionColumnsAndConsumedValues()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.FactionModifiers);
            string[] headings = instance
                .GetComponentsInChildren<TMP_Text>()
                .Where(text => text.name.StartsWith("Column", StringComparison.Ordinal))
                .Select(text => text.text)
                .ToArray();

            CollectionAssert.AreEqual(
                TestContent
                    .Pack.GameData.Factions.Select(faction =>
                        faction.GetDisplayName().ToUpperInvariant()
                    )
                    .ToArray(),
                headings.Take(TestContent.Pack.GameData.Factions.Length).ToArray()
            );
            Assert.AreEqual(
                TestContent
                    .Pack.GameData.Factions[0]
                    .Settings.UprisingResistance.ToString(CultureInfo.InvariantCulture),
                TableInput("uprising-resistance", "Player").text
            );
            Assert.AreEqual(
                "Usable supply gained from raw and refined materials. Higher provides more supply. Range: 1 or higher.",
                TableRow("refinement-multiplier").Find("Description").GetComponent<TMP_Text>().text
            );
            Assert.AreEqual(
                "Maintenance from each working mine and refinery pair. Higher supports more units. Range: 1 to 500.",
                TableRow("resource-processing").Find("Description").GetComponent<TMP_Text>().text
            );
            Assert.IsFalse(
                instance
                    .GetComponentsInChildren<Transform>()
                    .Any(candidate => candidate.name == "Setting-troop-effectiveness")
            );
        }

        [Test]
        public void Apply_EditedFactionModifier_PublishesOverrideWithoutMutatingContent()
        {
            GameSummary applied = null;
            int authored = TestContent.Pack.GameData.Factions[0].Settings.UprisingResistance;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.FactionModifiers);

            TableInput("uprising-resistance", "Player").onEndEdit.Invoke("3");
            Assert.AreEqual(
                ModifiedTextColor,
                TableInput("uprising-resistance", "Player").textComponent.color
            );
            ApplyButton().onClick.Invoke();

            GameGenerationConfig config = applied.CustomSettings.GenerationConfig;
            Assert.AreEqual(3, config.FactionModifiers[0].Settings.UprisingResistance);
            Assert.AreEqual(
                authored,
                TestContent.Pack.GameData.Factions[0].Settings.UprisingResistance
            );
        }

        [Test]
        public void EditValue_ProcessingPerFacilityAboveSafeLimit_RetainsPreviousValue()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.FactionModifiers);
            TMP_InputField input = TableInput("resource-processing", "Player");
            string original = input.text;

            input.onEndEdit.Invoke("501");

            Assert.AreEqual(original, TableInput("resource-processing", "Player").text);
        }

        [Test]
        public void Initialize_SelectedGalaxySize_ShowsFactionOfficerLimits()
        {
            summary.GalaxySize = GameSize.Small;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Assert.AreEqual(
                TestContent
                    .Pack.GameData.GenerationConfig.GalaxyClassification.FactionSetups[0]
                    .NumStartingOfficers.Small.ToString(CultureInfo.InvariantCulture),
                TableInput("starting-officers", "Player").text
            );
            Assert.AreEqual(
                DefaultTextColor,
                TableInput("starting-officers", "Player").textComponent.color
            );
            Assert.AreEqual(
                2,
                TableRow("starting-officers").GetComponentsInChildren<TMP_InputField>().Length
            );
        }

        [Test]
        public void Initialize_MissingFactionStartingForceBudget_HidesAbsentTableCell()
        {
            ContentPack source = TestContent.Pack;
            GameGenerationConfig generationConfig = source.GameData.GenerationConfig.GetDeepCopy();
            generationConfig.UnitDeployment.FactionBudgets.RemoveAt(1);
            ContentPack pack = CopyPackWithGenerationConfig(source, generationConfig);

            Assert.DoesNotThrow(() =>
            {
                view.Initialize(pack, summary);
                view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            });

            Transform row = TableRow("starting-forces");
            Assert.AreEqual(1, row.GetComponentsInChildren<TMP_InputField>().Length);
            Assert.IsTrue(row.Find("PlayerValue").gameObject.activeSelf);
            Assert.IsFalse(row.Find("AIValue").gameObject.activeSelf);
        }

        [Test]
        public void Apply_PerFactionStartingForceBudgets_PublishesSelectedGalaxySizeValues()
        {
            GameSummary applied = null;
            summary.GalaxySize = GameSize.Small;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            TableInput("starting-forces", "Player").onEndEdit.Invoke("40");
            TableInput("starting-forces", "AI").onEndEdit.Invoke("50");
            ApplyButton().onClick.Invoke();

            GameGenerationConfig config = applied.CustomSettings.GenerationConfig;
            Assert.AreEqual(
                40,
                config
                    .UnitDeployment.FactionBudgets[0]
                    .BudgetLevels.Single(level => level.GalaxySize == (int)GameSize.Small)
                    .Percentage
            );
            Assert.AreEqual(
                50,
                config
                    .UnitDeployment.FactionBudgets[1]
                    .BudgetLevels.Single(level => level.GalaxySize == (int)GameSize.Small)
                    .Percentage
            );
        }

        [Test]
        public void Initialize_HardDifficulty_ShowsConfiguredAIStartingUnitBonuses()
        {
            summary.Difficulty = GameDifficulty.Hard;
            summary.GalaxySize = GameSize.Medium;
            summary.PlayerFactionID = TestContent.Pack.GameData.Factions[0].InstanceID;
            string aiFactionID = TestContent.Pack.GameData.Factions[1].InstanceID;
            FactionBudget aiBudget =
                TestContent.Pack.GameData.GenerationConfig.UnitDeployment.FactionBudgets.Single(
                    budget => budget.FactionID == aiFactionID
                );
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            int starfighterBonus = aiBudget
                .Bonuses.Single(bonus =>
                    bonus.AIOnly
                    && bonus.Difficulty == GameDifficulty.Hard
                    && bonus.Category == StartingUnitBudgetCategory.Starfighter
                )
                .BudgetLevels.Single(level => level.GalaxySize == (int)GameSize.Medium)
                .Percentage;
            int regimentBonus = aiBudget
                .Bonuses.Single(bonus =>
                    bonus.AIOnly
                    && bonus.Difficulty == GameDifficulty.Hard
                    && bonus.Category == StartingUnitBudgetCategory.Regiment
                )
                .BudgetLevels.Single(level => level.GalaxySize == (int)GameSize.Medium)
                .Percentage;

            Assert.AreEqual(
                starfighterBonus.ToString(CultureInfo.InvariantCulture),
                TableInput("starting-starfighter-bonus", "AI").text
            );
            Assert.AreEqual(
                regimentBonus.ToString(CultureInfo.InvariantCulture),
                TableInput("starting-regiment-bonus", "AI").text
            );
            Assert.AreEqual(
                1,
                TableRow("starting-starfighter-bonus")
                    .GetComponentsInChildren<TMP_InputField>()
                    .Length
            );
        }

        [Test]
        public void Apply_MissingAIStartingUnitBonuses_CreatesSelectedDifficultyValues()
        {
            GameSummary applied = null;
            summary.Difficulty = GameDifficulty.Easy;
            summary.GalaxySize = GameSize.Small;
            summary.PlayerFactionID = TestContent.Pack.GameData.Factions[0].InstanceID;
            string aiFactionID = TestContent.Pack.GameData.Factions[1].InstanceID;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            TableInput("starting-starfighter-bonus", "AI").onEndEdit.Invoke("12");
            TableInput("starting-regiment-bonus", "AI").onEndEdit.Invoke("8");
            ApplyButton().onClick.Invoke();

            FactionBudget aiBudget =
                applied.CustomSettings.GenerationConfig.UnitDeployment.FactionBudgets.Single(
                    budget => budget.FactionID == aiFactionID
                );
            Assert.AreEqual(
                12,
                aiBudget
                    .Bonuses.Single(bonus =>
                        bonus.AIOnly
                        && bonus.Difficulty == GameDifficulty.Easy
                        && bonus.Category == StartingUnitBudgetCategory.Starfighter
                    )
                    .BudgetLevels.Single(level => level.GalaxySize == (int)GameSize.Small)
                    .Percentage
            );
            Assert.AreEqual(
                8,
                aiBudget
                    .Bonuses.Single(bonus =>
                        bonus.AIOnly
                        && bonus.Difficulty == GameDifficulty.Easy
                        && bonus.Category == StartingUnitBudgetCategory.Regiment
                    )
                    .BudgetLevels.Single(level => level.GalaxySize == (int)GameSize.Small)
                    .Percentage
            );
        }

        [Test]
        public void Apply_PerFactionResearchLevels_PublishesIndependentValues()
        {
            GameSummary applied = null;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            TableInput("starting-research", "Player").onEndEdit.Invoke("3");
            TableInput("starting-research", "AI").onEndEdit.Invoke("7");
            ApplyButton().onClick.Invoke();

            GameGenerationConfig config = applied.CustomSettings.GenerationConfig;
            Assert.AreEqual(3, config.GalaxyClassification.FactionSetups[0].StartingResearchLevel);
            Assert.AreEqual(7, config.GalaxyClassification.FactionSetups[1].StartingResearchLevel);
        }

        [Test]
        public void Initialize_SelectedResourceAvailability_HidesUnusedProfiles()
        {
            summary.ResourceAvailability = GameResourceAvailability.Normal;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            string[] settingNames = instance
                .GetComponentsInChildren<Transform>()
                .Where(transform => transform.name.StartsWith("Setting-"))
                .Select(transform => transform.name)
                .ToArray();
            Assert.IsTrue(settingNames.Any(name => name.StartsWith("Setting-normal-")));
            Assert.IsFalse(settingNames.Any(name => name.StartsWith("Setting-limited-")));
            Assert.IsFalse(settingNames.Any(name => name.StartsWith("Setting-abundant-")));
        }

        [Test]
        public void Initialize_WorldGenerationSettings_ShowsSectionHeadings()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            string[] labels = instance
                .GetComponentsInChildren<TMP_Text>()
                .Select(text => text.text)
                .ToArray();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "FACTION STARTING CONDITIONS",
                    "PLANET ENERGY",
                    "RAW MATERIALS",
                    "PLANET RESOURCE LIMITS",
                    "RIM COLONIZATION",
                    "STARTING PLANET SUPPORT",
                    "STARTING FACILITIES",
                    "STARTING GARRISONS",
                    "LIVE GARRISON REQUIREMENTS",
                    "MOVEMENT",
                },
                labels
            );
            CollectionAssert.IsEmpty(
                labels.Intersect(
                    new[]
                    {
                        "STARTING RESEARCH",
                        "STARTING OFFICERS",
                        "STARTING FORCES",
                        "CORE WORLD CONTROL",
                    }
                )
            );
        }

        [Test]
        public void Initialize_WorldGenerationSectionHeading_MatchesOptionsSectionHeading()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            TMP_Text actual = instance
                .GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Section-FACTION-STARTING-CONDITIONS");
            TMP_Text expected = AssetDatabase
                .LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab")
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "SavingHeader");

            Assert.AreEqual(expected.fontSize, actual.fontSize);
            Assert.AreEqual(expected.fontStyle, actual.fontStyle);
            Assert.AreEqual(expected.enableAutoSizing, actual.enableAutoSizing);
            Assert.AreEqual(expected.alignment, actual.alignment);
            Assert.AreEqual(expected.color, actual.color);
            Assert.AreEqual(expected.rectTransform.sizeDelta, actual.rectTransform.sizeDelta);
        }

        [Test]
        public void Initialize_WorldGenerationSectionHeading_HasNoBottomMargin()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            Transform[] transforms = instance.GetComponentsInChildren<Transform>();
            RectTransform section = (RectTransform)
                transforms.Single(candidate =>
                    candidate.name == "Section-FACTION-STARTING-CONDITIONS"
                );
            RectTransform header = transforms
                .Where(candidate => candidate.name == "GenerationTableHeader")
                .Cast<RectTransform>()
                .OrderByDescending(candidate => candidate.anchoredPosition.y)
                .First();

            Assert.AreEqual(16f, section.anchoredPosition.y - header.anchoredPosition.y);
        }

        [Test]
        public void Initialize_WorldGenerationNextSection_UsesEightPixelTopGap()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            Transform[] transforms = instance.GetComponentsInChildren<Transform>();
            RectTransform previousRow = (RectTransform)
                transforms.Single(candidate => candidate.name == "Setting-weak-core-worlds");
            RectTransform section = (RectTransform)
                transforms.Single(candidate => candidate.name == "Section-PLANET-ENERGY");

            Assert.AreEqual(
                -previousRow.anchoredPosition.y + previousRow.sizeDelta.y + 8f,
                -section.anchoredPosition.y
            );
        }

        [Test]
        public void Initialize_WorldGenerationLineItems_UseFourPixelGap()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            Transform[] transforms = instance.GetComponentsInChildren<Transform>();
            RectTransform firstRow = (RectTransform)
                transforms.Single(candidate => candidate.name == "Setting-strong-core-worlds");
            RectTransform secondRow = (RectTransform)
                transforms.Single(candidate => candidate.name == "Setting-weak-core-worlds");

            Assert.AreEqual(
                -firstRow.anchoredPosition.y + firstRow.sizeDelta.y + 4f,
                -secondRow.anchoredPosition.y
            );
        }

        [Test]
        public void Initialize_CoreWorldControl_RendersFactionColumnsAndSharedRows()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Transform row = instance
                .GetComponentsInChildren<Transform>()
                .Single(candidate => candidate.name == "Setting-strong-core-worlds");
            string[] headings = instance
                .GetComponentsInChildren<TMP_Text>()
                .Where(text => text.name.StartsWith("Column", StringComparison.Ordinal))
                .Select(text => text.text)
                .ToArray();

            Assert.AreEqual(
                "Strong Core Worlds %",
                row.Find("Label").GetComponent<TMP_Text>().text
            );
            Assert.AreEqual(
                "Core worlds this faction starts owning with strong popular support. Range: 0% to 100%.",
                row.Find("Description").GetComponent<TMP_Text>().text
            );
            Assert.AreEqual(2, row.GetComponentsInChildren<TMP_InputField>().Length);
            CollectionAssert.IsSubsetOf(new[] { "ALLIANCE", "EMPIRE" }, headings);
        }

        [Test]
        public void Initialize_StartingSupport_RendersThreeColumnTable()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Transform row = instance
                .GetComponentsInChildren<Transform>()
                .Single(candidate => candidate.name == "Setting-support-base");

            Assert.AreEqual("Base Support", row.Find("Label").GetComponent<TMP_Text>().text);
            Assert.AreEqual(3, row.GetComponentsInChildren<TMP_InputField>().Length);
        }

        [Test]
        public void Initialize_MineChanceControl_UsesCompactLabel()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Transform row = instance
                .GetComponentsInChildren<Transform>()
                .Single(candidate => candidate.name == "Setting-mine-chance-per-resource");
            TMP_Text label = row.Find("Label").GetComponent<TMP_Text>();

            Assert.AreEqual("Mine Chance per Resource %", label.text);
            Assert.LessOrEqual(label.preferredWidth, label.rectTransform.rect.width);
        }

        [Test]
        public void Initialize_StartingFacilityControls_ShowCoreAndRimChances()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Assert.AreEqual("43", TableInput("starting-refinery-chance", "Player").text);
            Assert.AreEqual("5", TableInput("starting-refinery-chance", "AI").text);
            Assert.AreEqual("3", TableInput("starting-shipyard-chance", "Player").text);
            Assert.AreEqual("1", TableInput("starting-shipyard-chance", "AI").text);
            Assert.AreEqual("3", TableInput("starting-construction-yard-chance", "Player").text);
            Assert.AreEqual("1", TableInput("starting-construction-yard-chance", "AI").text);
        }

        [Test]
        public void Apply_StartingFacilityChances_ReassignsOnlyEmptyOutcomes()
        {
            GameSummary applied = null;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            TableInput("starting-refinery-chance", "Player").onEndEdit.Invoke("30");
            TableInput("starting-shipyard-chance", "Player").onEndEdit.Invoke("10");
            ApplyButton().onClick.Invoke();

            FacilityGenerationChanceEditor editor = new FacilityGenerationChanceEditor(
                TestContent.Pack.GameData.Buildings
            );
            GameGenerationConfig config = applied.CustomSettings.GenerationConfig;
            Assert.AreEqual(30, editor.GetChance(config, true, BuildingType.Refinery));
            Assert.AreEqual(10, editor.GetChance(config, true, BuildingType.Shipyard));
            Assert.AreEqual(3, editor.GetChance(config, true, BuildingType.TrainingFacility));
            Assert.AreEqual(3, editor.GetChance(config, true, BuildingType.ConstructionFacility));
            Assert.AreEqual(
                43,
                editor.GetChance(
                    TestContent.Pack.GameData.GenerationConfig,
                    true,
                    BuildingType.Refinery
                )
            );
        }

        [Test]
        public void EditValue_StartingFacilityChanceExceedsEmptyCapacity_RetainsPreviousValue()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            TMP_InputField input = TableInput("starting-shipyard-chance", "Player");

            input.onEndEdit.Invoke("40");

            Assert.AreEqual("3", TableInput("starting-shipyard-chance", "Player").text);
        }

        [Test]
        public void Initialize_WorldGenerationTables_CenterEveryHeadingOverItsInput()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            Transform[] transforms = instance.GetComponentsInChildren<Transform>();
            Transform[] tableHeaders = transforms
                .Where(candidate => candidate.name == "GenerationTableHeader")
                .ToArray();
            string[] inputPrefixes = { "Player", "AI", "World" };

            Assert.IsNotEmpty(tableHeaders);
            foreach (Transform header in tableHeaders)
            {
                float headerY = ((RectTransform)header).anchoredPosition.y;
                Transform firstRow = transforms
                    .Where(candidate => candidate.name.StartsWith("Setting-"))
                    .Where(candidate => ((RectTransform)candidate).anchoredPosition.y < headerY)
                    .OrderByDescending(candidate => ((RectTransform)candidate).anchoredPosition.y)
                    .First();
                TMP_Text[] headings = header
                    .GetComponentsInChildren<TMP_Text>()
                    .Where(text => text.name.StartsWith("Column", StringComparison.Ordinal))
                    .OrderBy(text => text.name)
                    .ToArray();

                for (int index = 0; index < headings.Length; index++)
                {
                    RectTransform headingRect = headings[index].rectTransform;
                    RectTransform inputRect = (RectTransform)
                        firstRow.Find(inputPrefixes[index] + "Value");
                    float headingCenter =
                        headingRect.anchoredPosition.x + headingRect.sizeDelta.x / 2f;
                    float inputCenter = inputRect.anchoredPosition.x + inputRect.sizeDelta.x / 2f;
                    Assert.AreEqual(inputCenter, headingCenter);
                    Assert.AreEqual(inputRect.sizeDelta.x, headingRect.sizeDelta.x);
                    Assert.AreEqual(9f, headings[index].fontSize);
                    Assert.IsFalse(headings[index].enableAutoSizing);
                    Assert.AreEqual(
                        ((RectTransform)firstRow.Find("Label")).anchoredPosition.y,
                        inputRect.anchoredPosition.y
                    );
                    Assert.AreEqual(
                        inputRect.anchoredPosition.y,
                        ((RectTransform)firstRow.Find(inputPrefixes[index] + "Reset"))
                            .anchoredPosition
                            .y
                    );
                }
            }
        }

        [Test]
        public void Apply_EditedGenerationValue_PublishesDetachedScenarioOverride()
        {
            GameSummary applied = null;
            int authored = TestContent
                .Pack
                .GameData
                .GenerationConfig
                .FacilityGeneration
                .CoreMineMultiplier;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            TableInput("mine-chance-per-resource", "Player").onEndEdit.Invoke("7");
            ApplyButton().onClick.Invoke();

            GameGenerationConfig config = applied.CustomSettings.GenerationConfig;
            Assert.AreEqual(7, config.FacilityGeneration.CoreMineMultiplier);
            Assert.AreEqual(
                authored,
                TestContent.Pack.GameData.GenerationConfig.FacilityGeneration.CoreMineMultiplier
            );
        }

        [Test]
        public void Apply_EditedGarrisonRules_PublishesRuntimeOverridesWithoutMutatingContent()
        {
            GameSummary applied = null;
            int authoredThreshold = TestContent
                .Pack
                .GameData
                .GameConfig
                .AI
                .Garrison
                .SupportThreshold;
            int authoredDivisor = TestContent.Pack.GameData.GameConfig.AI.Garrison.GarrisonDivisor;
            int authoredMultiplier = TestContent
                .Pack
                .GameData
                .GameConfig
                .AI
                .Garrison
                .UprisingMultiplier;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Input("garrison-support-threshold", "World").onEndEdit.Invoke("70");
            Input("garrison-support-deficit-per-regiment", "World").onEndEdit.Invoke("5");
            Input("garrison-uprising-multiplier", "World").onEndEdit.Invoke("3");
            ApplyButton().onClick.Invoke();

            Assert.AreEqual(70, applied.CustomSettings.GarrisonRules.SupportThreshold);
            Assert.AreEqual(5, applied.CustomSettings.GarrisonRules.SupportDeficitPerRegiment);
            Assert.AreEqual(3, applied.CustomSettings.GarrisonRules.UprisingMultiplier);
            Assert.AreEqual(
                authoredThreshold,
                TestContent.Pack.GameData.GameConfig.AI.Garrison.SupportThreshold
            );
            Assert.AreEqual(
                authoredDivisor,
                TestContent.Pack.GameData.GameConfig.AI.Garrison.GarrisonDivisor
            );
            Assert.AreEqual(
                authoredMultiplier,
                TestContent.Pack.GameData.GameConfig.AI.Garrison.UprisingMultiplier
            );
        }

        [Test]
        public void Apply_EditedMovementRule_PublishesRuntimeOverrideWithoutMutatingContent()
        {
            GameSummary applied = null;
            double authoredDivisor = TestContent.Pack.GameData.GameConfig.Movement.DistanceDivisor;
            view.StartRequested += (_, launchSummary) => applied = launchSummary;
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);

            Input("travel-speed", "World").onEndEdit.Invoke("150");
            ApplyButton().onClick.Invoke();

            Assert.AreEqual(150, applied.CustomSettings.MovementRules.TravelSpeedPercent);
            Assert.AreEqual(
                authoredDivisor,
                TestContent.Pack.GameData.GameConfig.Movement.DistanceDivisor
            );
        }

        [Test]
        public void EditGenerationValue_InvalidResourceRange_RetainsPreviousValue()
        {
            view.Initialize(TestContent.Pack, summary);
            view.SelectCategory(NewGameSettingsCategory.WorldGeneration);
            TMP_InputField minimum = TableInput("normal-resource-minimum", "Player");
            string original = minimum.text;

            minimum.onEndEdit.Invoke(
                (
                    TestContent
                        .Pack.GameData.GenerationConfig.PlanetResources.Profiles.Single(profile =>
                            profile.Availability == GameResourceAvailability.Normal
                        )
                        .EnergyMax + 1
                ).ToString(CultureInfo.InvariantCulture)
            );

            Assert.AreEqual(original, TableInput("normal-resource-minimum", "Player").text);
        }

        [Test]
        public void Initialize_DifficultyRow_ShowsDescriptionBelowName()
        {
            view.Initialize(TestContent.Pack, summary);
            Transform row = Input("mine-output", "Player").transform.parent;

            RectTransform label = (RectTransform)row.Find("Label");
            TMP_Text description = row.Find("Description").GetComponent<TMP_Text>();
            Assert.AreEqual(
                "How fast mines produce raw materials. Higher is faster. Range: 0% to 1,000%.",
                description.text
            );
            Assert.AreEqual(9f, description.fontSize);
            Assert.Greater(
                -description.rectTransform.anchoredPosition.y,
                -label.anchoredPosition.y
            );
            Assert.IsNull(row.Find("Info"));
        }

        [Test]
        public void Initialize_DifficultyLineItems_SizeToContentWithFourPixelGap()
        {
            view.Initialize(TestContent.Pack, summary);
            RectTransform mineRow = (RectTransform)Input("mine-output", "Player").transform.parent;
            RectTransform refineryRow = (RectTransform)
                Input("refinery-output", "Player").transform.parent;
            RectTransform description = (RectTransform)mineRow.Find("Description");

            Assert.AreEqual(
                -description.anchoredPosition.y + description.sizeDelta.y,
                mineRow.sizeDelta.y
            );
            Assert.AreEqual(
                -mineRow.anchoredPosition.y + mineRow.sizeDelta.y + 4f,
                -refineryRow.anchoredPosition.y
            );
        }

        [Test]
        public void Initialize_DifficultyColumns_UsesTopLeftAlignedSharedLayoutAndDoubleSpacing()
        {
            view.Initialize(TestContent.Pack, summary);
            RectTransform playerField = (RectTransform)Input("mine-output", "Player").transform;
            RectTransform playerReset = (RectTransform)playerField.parent.Find("PlayerReset");
            RectTransform aiField = (RectTransform)Input("mine-output", "AI").transform;
            RectTransform page = (RectTransform)
                instance.transform.Find("ContentRoot/DifficultyPage");
            RectTransform scroll = (RectTransform)page.Find("SettingsScrollArea");
            RectTransform playerHeading = (RectTransform)page.Find("PlayerHeading");
            RectTransform aiHeading = (RectTransform)page.Find("AIHeading");
            RectTransform rowLabel = (RectTransform)playerField.parent.Find("Label");

            Assert.AreEqual(
                8f,
                aiField.anchoredPosition.x
                    - playerReset.anchoredPosition.x
                    - playerReset.sizeDelta.x
            );
            Assert.AreEqual(69f, -page.anchoredPosition.y);
            Assert.AreEqual(36f, -scroll.anchoredPosition.y);
            Assert.AreEqual(
                scroll.anchoredPosition.x + playerField.anchoredPosition.x,
                playerHeading.anchoredPosition.x
            );
            Assert.AreEqual(
                scroll.anchoredPosition.x + aiField.anchoredPosition.x,
                aiHeading.anchoredPosition.x
            );
            Assert.AreEqual(playerField.sizeDelta.x, playerHeading.sizeDelta.x);
            Assert.AreEqual(aiField.sizeDelta.x, aiHeading.sizeDelta.x);
            Assert.AreEqual(rowLabel.anchoredPosition.y, playerField.anchoredPosition.y);
            Assert.AreEqual(rowLabel.sizeDelta.y, playerField.sizeDelta.y);
            Assert.AreEqual(rowLabel.anchoredPosition.y, playerReset.anchoredPosition.y);
            Assert.AreEqual(rowLabel.anchoredPosition.y, aiField.anchoredPosition.y);
            TMP_Text expectedLabel = optionsPrefab
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "GameLaunchSpeedLabel");
            Assert.AreEqual(expectedLabel.fontSize, rowLabel.GetComponent<TMP_Text>().fontSize);
            Assert.AreEqual(
                expectedLabel.enableAutoSizing,
                rowLabel.GetComponent<TMP_Text>().enableAutoSizing
            );
            Assert.AreEqual(9f, playerHeading.GetComponent<TMP_Text>().fontSize);
            Assert.AreEqual(9f, aiHeading.GetComponent<TMP_Text>().fontSize);
            Assert.AreEqual(
                TextAlignmentOptions.MidlineLeft,
                playerHeading.GetComponent<TMP_Text>().alignment
            );
            Assert.AreEqual(
                TextAlignmentOptions.MidlineLeft,
                aiHeading.GetComponent<TMP_Text>().alignment
            );
        }

        [Test]
        public void Initialize_Window_OmitsRedundantContextAndCancelControls()
        {
            view.Initialize(TestContent.Pack, summary);

            Assert.IsNull(instance.transform.Find("ContentRoot/Subtitle"));
            Assert.IsNull(instance.transform.Find("ContentRoot/Scope"));
            Assert.IsNull(instance.transform.Find("ContentRoot/Cancel"));
            Assert.IsNull(instance.transform.Find("ContentRoot/ChangeCount"));
        }

        [TestCase("Player")]
        [TestCase("AI")]
        public void Initialize_DifficultyRow_ResetUsesOptionsSpacing(string column)
        {
            view.Initialize(TestContent.Pack, summary);
            RectTransform field = (RectTransform)Input("mine-output", column).transform;
            RectTransform reset = (RectTransform)field.parent.Find(column + "Reset");

            Assert.AreEqual(
                field.anchoredPosition.x + field.sizeDelta.x + 2f,
                reset.anchoredPosition.x
            );
            Assert.AreEqual(new Vector2(30f, 16f), reset.sizeDelta);
        }

        [Test]
        public void Initialize_WindowHeading_MatchesSharedMenuTypography()
        {
            view.Initialize(TestContent.Pack, summary);

            TMP_Text header = instance
                .transform.Find("ContentRoot/HeaderTextField")
                .GetComponent<TMP_Text>();
            TMP_Text expected = optionsPrefab
                .transform.Find("ContentRoot/HeaderTextField")
                .GetComponent<TMP_Text>();

            Assert.AreEqual(expected.alignment, header.alignment);
            Assert.AreEqual(expected.enableAutoSizing, header.enableAutoSizing);
            Assert.AreEqual(expected.fontSizeMin, header.fontSizeMin);
            Assert.AreEqual(expected.fontSizeMax, header.fontSizeMax);
            Assert.AreEqual(
                UILayout.GetSourceRect(expected.rectTransform),
                UILayout.GetSourceRect(header.rectTransform)
            );
            Assert.LessOrEqual(header.preferredWidth, header.rectTransform.rect.width);
        }

        [Test]
        public void Initialize_BackNavigation_MatchesOptionsFooterTypography()
        {
            view.Initialize(TestContent.Pack, summary, optionsPrefab, null);
            TMP_Text expected = optionsPrefab
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "MainMenuLabel");
            TMP_Text actual = instance
                .transform.Find("ContentRoot/Close")
                .GetComponentInChildren<TMP_Text>();
            TMP_Text saveLoad = instance
                .transform.Find("ContentRoot/Tab4")
                .GetComponentInChildren<TMP_Text>();

            Assert.AreEqual(expected.text, actual.text);
            Assert.AreEqual(expected.color, actual.color);
            Assert.AreEqual(expected.alignment, actual.alignment);
            Assert.AreEqual(expected.fontStyle, actual.fontStyle);
            Assert.AreEqual(FontStyles.Normal, saveLoad.fontStyle);
        }

        [Test]
        public void Initialize_StartAction_UsesAccentBoldTypography()
        {
            view.Initialize(TestContent.Pack, summary);
            TMP_Text label = ApplyButton().GetComponentInChildren<TMP_Text>();

            Assert.AreEqual(new Color(0.373f, 0.659f, 0.925f), label.color);
            Assert.AreEqual(FontStyles.Bold, label.fontStyle);
            Assert.AreEqual(114f, ((RectTransform)ApplyButton().transform).sizeDelta.x);
        }

        /// <summary>
        /// Creates a preset page using an isolated directory and a known saved seed.
        /// </summary>
        /// <returns>The isolated preset manager and existing preset identity.</returns>
        private (CustomGamePresetManager Manager, string FileName) InitializePresetPage()
        {
            presetDirectory = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-preset-confirm-tests-" + Guid.NewGuid().ToString("N")
            );
            CustomGamePresetManager manager = new CustomGamePresetManager(presetDirectory);
            summary = GameLaunchContext.CreateDefaultSummary(TestContent.Pack);
            summary.Seed = 111;
            string fileName = manager.CreatePreset("Existing Preset", summary);
            view.Initialize(TestContent.Pack, summary, optionsPrefab, manager);
            view.SelectCategory(NewGameSettingsCategory.SaveLoad);
            return (manager, fileName);
        }

        /// <summary>
        /// Requests replacement of an existing preset through the save controls.
        /// </summary>
        /// <param name="submitName">Whether to submit an edited name instead of clicking Save.</param>
        /// <returns>The isolated preset manager and existing preset identity.</returns>
        private (CustomGamePresetManager Manager, string FileName) RequestPresetOverwrite(
            bool submitName
        )
        {
            (CustomGamePresetManager Manager, string FileName) preset = InitializePresetPage();
            view.SelectCategory(NewGameSettingsCategory.GameSetup);
            SeedInput().onEndEdit.Invoke("222");
            view.SelectCategory(NewGameSettingsCategory.SaveLoad);
            Button row = PresetButton("SlotRow1");
            row.onClick.Invoke();
            if (submitName)
            {
                row.onClick.Invoke();
                instance
                    .GetComponentInChildren<OptionsSaveListView>(true)
                    .GetComponentInChildren<TMP_InputField>(true)
                    .onSubmit.Invoke("Updated Preset");
            }
            else
                PresetButton("SaveIcon").onClick.Invoke();
            return preset;
        }

        /// <summary>
        /// Removes authored command textures to reproduce stripped player-build references.
        /// </summary>
        /// <param name="source">The hierarchy containing the shared Save/Load page.</param>
        private static void ClearSaveListTextures(GameObject source)
        {
            OptionsSaveListView saveList = source.GetComponentInChildren<OptionsSaveListView>(true);
            foreach (RawImage image in saveList.GetComponentsInChildren<RawImage>(true))
                image.texture = null;
            foreach (
                RawImagePressVisual visual in saveList.GetComponentsInChildren<RawImagePressVisual>(
                    true
                )
            )
                visual.SetTextures(null, null);
        }

        /// <summary>
        /// Finds a button in the preset list.
        /// </summary>
        /// <param name="name">The authored button name.</param>
        /// <returns>The matching button.</returns>
        private Button PresetButton(string name) =>
            instance
                .GetComponentInChildren<OptionsSaveListView>(true)
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == name);

        /// <summary>
        /// Finds the preset confirmation dialog.
        /// </summary>
        /// <returns>The confirmation dialog.</returns>
        private ConfirmationDialogView PresetConfirmation() =>
            instance.GetComponentInChildren<ConfirmationDialogView>(true);

        /// <summary>
        /// Finds the requested preset confirmation button.
        /// </summary>
        /// <param name="accepted">Whether to select Yes instead of No.</param>
        /// <returns>The matching confirmation button.</returns>
        private Button PresetConfirmationButton(bool accepted) =>
            PresetConfirmation()
                .GetComponentsInChildren<Button>(true)
                .Single(button =>
                    button.name == (accepted ? "ConfirmButtonImage" : "CancelButtonImage")
                );

        /// <summary>
        /// Finds an authored input by its row identity and recipient column.
        /// </summary>
        /// <param name="key">The row identity.</param>
        /// <param name="column">The recipient column.</param>
        /// <returns>The active numeric input.</returns>
        private TMP_InputField Input(string key, string column) =>
            instance
                .GetComponentsInChildren<TMP_InputField>()
                .Single(input =>
                    input.transform.parent.name == "Setting-" + key
                    && input.name == column + "Value"
                );

        /// <summary>
        /// Finds an authored reset button by row identity and recipient column.
        /// </summary>
        /// <param name="key">The row identity.</param>
        /// <param name="column">The recipient column.</param>
        /// <returns>The reset button.</returns>
        private Button ResetButton(string key, string column) =>
            Input(key, column).transform.parent.Find(column + "Reset").GetComponent<Button>();

        /// <summary>
        /// Finds an active table input by its shared row identity and authored control prefix.
        /// </summary>
        /// <param name="rowKey">The shared table-row identity.</param>
        /// <param name="prefix">The authored control prefix.</param>
        /// <returns>The active integer input.</returns>
        private TMP_InputField TableInput(string rowKey, string prefix) =>
            instance
                .GetComponentsInChildren<TMP_InputField>()
                .Single(input =>
                    input.transform.parent.name == "Setting-" + rowKey
                    && input.name == prefix + "Value"
                );

        /// <summary>
        /// Finds an active generation-table row by its shared identity.
        /// </summary>
        /// <param name="rowKey">The shared table-row identity.</param>
        /// <returns>The active row transform.</returns>
        private Transform TableRow(string rowKey) =>
            instance
                .GetComponentsInChildren<Transform>()
                .Single(transform => transform.name == "Setting-" + rowKey);

        /// <summary>
        /// Copies a content pack with replacement game-generation settings.
        /// </summary>
        /// <param name="source">The source content pack.</param>
        /// <param name="generationConfig">The replacement generation settings.</param>
        /// <returns>A content pack containing the replacement settings.</returns>
        private static ContentPack CopyPackWithGenerationConfig(
            ContentPack source,
            GameGenerationConfig generationConfig
        )
        {
            GameDataCatalog sourceData = source.GameData;
            GameDataCatalog gameData = new GameDataCatalog(
                sourceData.GameConfig,
                generationConfig,
                sourceData.Factions,
                sourceData.PlanetSectors,
                sourceData.Buildings,
                sourceData.CapitalShips,
                sourceData.Starfighters,
                sourceData.Regiments,
                sourceData.SpecialForces,
                sourceData.Officers,
                sourceData.GameEvents,
                sourceData.MessageDefinitions,
                sourceData.EncyclopediaEntries,
                sourceData.FactionThemes,
                sourceData.OfficerTraits
            );
            return new ContentPack(
                source.ContentRootPath,
                source.PackRootPath,
                source.FileResolver,
                source.Definition,
                source.Scenario,
                source.Factions,
                gameData,
                new Dictionary<string, ContentPreloadManifest>()
            );
        }

        /// <summary>
        /// Finds the editable seed field on the setup page.
        /// </summary>
        /// <returns>The active seed input.</returns>
        private TMP_InputField SeedInput() =>
            instance
                .GetComponentsInChildren<TMP_InputField>()
                .Single(input => input.transform.parent.name == "Setup-seed");

        /// <summary>
        /// Finds the button that generates a new seed.
        /// </summary>
        /// <returns>The active new-seed button.</returns>
        private Button SeedResetButton() =>
            SeedInput().transform.parent.Find("WorldReset").GetComponent<Button>();

        /// <summary>
        /// Finds the displayed value for one Game Setup selector.
        /// </summary>
        /// <param name="key">The stable setup-choice identity.</param>
        /// <returns>The selector's value label.</returns>
        private TMP_Text SetupChoiceValue(string key) =>
            instance
                .GetComponentsInChildren<OptionsChoiceFieldView>()
                .Single(candidate => candidate.name == "Choice-" + key)
                .transform.Find("Value")
                .GetComponent<TMP_Text>();

        /// <summary>
        /// Finds the Apply button.
        /// </summary>
        /// <returns>The Apply button.</returns>
        private Button ApplyButton() =>
            instance.transform.Find("ContentRoot/Apply").GetComponent<Button>();

        /// <summary>
        /// Finds the event-category toggle for the requested category.
        /// </summary>
        /// <param name="category">The event category represented by the toggle.</param>
        /// <returns>The matching event-category toggle.</returns>
        private OptionsToggleRowView EventToggle(GameEventCategory category) =>
            instance
                .GetComponentsInChildren<OptionsToggleRowView>()
                .Single(toggle => toggle.OptionIndex == (int)category);

        /// <summary>
        /// Reads the state label displayed by an event-category toggle.
        /// </summary>
        /// <param name="toggle">The toggle to inspect.</param>
        /// <returns>The currently displayed state label.</returns>
        private static string ToggleState(OptionsToggleRowView toggle) =>
            ToggleStateText(toggle).text;

        /// <summary>
        /// Finds the displayed state field for one event-category toggle.
        /// </summary>
        /// <param name="toggle">The toggle to inspect.</param>
        /// <returns>The toggle's state label.</returns>
        private static TMP_Text ToggleStateText(OptionsToggleRowView toggle) =>
            toggle.transform.Find("StateTextField").GetComponent<TMP_Text>();

        /// <summary>
        /// Steps one active game-setup choice through its shared Options selector.
        /// </summary>
        /// <param name="key">The stable setup-choice identity.</param>
        /// <param name="delta">The requested step direction.</param>
        private void StepSetupChoice(string key, int delta)
        {
            OptionsChoiceFieldView choice = instance
                .GetComponentsInChildren<OptionsChoiceFieldView>()
                .Single(candidate => candidate.name == "Choice-" + key);
            choice
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == (delta < 0 ? "Previous" : "Next"))
                .onClick.Invoke();
        }
    }
}
